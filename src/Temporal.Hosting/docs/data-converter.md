# Payload codecs and per-field secrets

`DataConverter` builds a shared `DataConverter` from the enabled codecs and
applies it to the client (workers inherit it, so client and workers always
encode consistently). All codecs are opt-in:

- **`DataConverter:Encryption`** — AES-GCM encrypts every payload before it is
  sent to the server, with a key id for rotation. The key is an ASCII string of
  16, 24, or 32 bytes. `Source` selects where the key comes from: `config`
  (inline `Key`, default), `azureKeyVault`, or `awsSecretsManager` (fetching
  `SecretId` from the vault at startup via `TemporalSecretLoader`). `Encoding`
  controls how the fetched secret decodes into bytes (`raw`, `base64`, or
  `hex`). In production, source the key from your KMS via a vault source.
- **`DataConverter:ClaimCheck`** — offloads payloads larger than
  `ThresholdBytes` to a store, leaving a small reference in the workflow history.
  `Store` selects the backend: `filesystem` (default, writing to `Directory`),
  `azureBlob`, or `s3`. The cloud stores are built via a registered
  `IClaimCheckStoreFactory` and configured with `AccountUri`/`ConnectionString`/
  `ConnectionStringSecretId`/`ContainerName` (Azure) or `Region`/`BucketName`/
  `AccessKeySecretId`/`SecretKeySecretId`/`SessionTokenSecretId`/`RoleArn`/
  `RoleSessionName` (S3). See `Kogoshvili.Temporal.Cloud`.
- **`DataConverter:Secret`** — per-field `Secret<T>` encryption (see below).

The composed codec is registered as a singleton `IPayloadCodec`, so a
`Kogoshvili.Temporal.CodecServer` hosted in the same app can expose `/encode`
and `/decode` over HTTP for the Temporal Web UI and CLI using the exact same
codec.

## Per-field secrets

`DataConverter:Encryption` hides the *whole* payload, but a single sensitive
field (an SSN, an access token) can be encrypted on its own so it stays
unreadable even after the payload around it is decrypted — for example by the
Temporal UI when it points at your codec server. Model that field as
`Secret<T>` (from `Kogoshvili.Temporal.Codec`) and enable
`Temporal:DataConverter:Secret`:

```jsonc
"DataConverter": {
  "Secret": {
    "Enabled": true,
    "Source": "azureKeyVault",       // or "awsSecretsManager"
    "SecretId": "ssn-key",           // vault secret holding the AES-GCM key
    "KeyId": "ssn-v1",               // key id stamped on encrypted secrets
    "Encoding": "raw"                // raw | base64 | hex
  }
}
```

The key is always sourced from a vault (Azure Key Vault or AWS Secrets Manager)
via `ISecretResolver`, registered with `AddAzureKeyVaultSecretResolver` /
`AddAwsSecretsManagerSecretResolver` (see `Kogoshvili.Temporal.Cloud`). At
startup `TemporalSecretLoader` resolves the key and wires a
`SecretEncryptionInterceptor` onto the client and workers: `Secret<T>` values in
workflow/signal/query arguments are encrypted on the way out, and activity
arguments are decrypted on the way in.

A `Secret<T>` is carried opaquely through a workflow — construct it on the
client with plaintext and let the interceptor encrypt it; read `.Value` in an
activity after the interceptor has decrypted it. Its serialized form is the
same `{ encoding, encryption-key-id, data }` shape the encryption codec emits.
