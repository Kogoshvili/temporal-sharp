# Payload codecs and per-field secrets

`Temporal:DataConverter` builds a shared `DataConverter` from the codecs you
enable and applies it to the client, so workers (which inherit the client's
converter) encode and decode consistently. Everything is opt-in; with no codec
enabled the SDK's default data converter is used unchanged.

## Minimal setup

The simplest codec is whole-payload encryption. Enable it with an inline key and
every payload the client sends is AES-GCM encrypted before it reaches the
server:

```jsonc
// appsettings.json
{
  "Temporal": {
    "DataConverter": {
      "Encryption": {
        "Enabled": true,
        "Key": "0123456789abcdef0123456789abcdef" // 16/24/32 ASCII bytes
      }
    }
  }
}
```

`Source` defaults to `config`, so the inline `Key` is used directly. `Key` must
be 16, 24, or 32 ASCII bytes; `KeyId` defaults to `default` and is stamped into
each payload's metadata for key rotation.

## Configuration

Each codec is a sub-section of `Temporal:DataConverter`. The three are
independent and compose when more than one is enabled.

### Encryption

Encrypts every payload end to end. The `Source` selects where the key comes
from.

```jsonc
"DataConverter": {
  "Encryption": {
    "Enabled": true,
    "Source": "config",      // config | azureKeyVault | awsSecretsManager
    "Key": "0123456789abcdef0123456789abcdef", // inline, Source=config only
    "KeyId": "v1",           // key id for rotation (default "default")
    "SecretId": "enc-key",   // vault secret name/id, Source=vault only
    "Encoding": "raw"        // raw | base64 | hex (vault secret decode)
  }
}
```

- `config` (default) uses the inline `Key` synchronously at registration.
- `azureKeyVault` / `awsSecretsManager` resolve `SecretId` from the vault at
  startup (`TemporalSecretLoader`) and decode it into key bytes via `Encoding`.
  Register a resolver first (see [Vault keys](#vault-keys)).

### Claim check

Offloads payloads larger than `ThresholdBytes` to a store, leaving a small
reference in the workflow history.

```jsonc
"DataConverter": {
  "ClaimCheck": {
    "Enabled": true,
    "ThresholdBytes": 1048576, // default 1 MiB
    "Store": "filesystem",     // filesystem | azureBlob | s3
    "Directory": "claim-check" // filesystem store path (default "claim-check")
  }
}
```

`filesystem` (default) is built synchronously. `azureBlob` and `s3` are built
via a registered `IClaimCheckStoreFactory` from `Kogoshvili.Temporal.Cloud` and
resolved at startup.

### Secret (per-field)

Encrypts a single `Secret<T>` field independently of the surrounding payload, so
it stays unreadable even after the payload around it is decrypted (for example
by the Temporal UI pointing at your codec server). Model the field as
`Secret<T>` from `Kogoshvili.Temporal.Codec`:

```csharp
using Kogoshvili.Temporal.Codec;

public sealed class Patient
{
    public string Name { get; set; } = "";
    public Secret<string> Ssn { get; set; } = new("");
}
```

Enable it under `DataConverter`:

```jsonc
"DataConverter": {
  "Secret": {
    "Enabled": true,
    "Source": "azureKeyVault", // azureKeyVault | awsSecretsManager (default azureKeyVault)
    "SecretId": "ssn-key",     // vault secret name/id holding the AES-GCM key
    "KeyId": "ssn-v1",         // key id stamped on encrypted secrets
    "Encoding": "raw"          // raw | base64 | hex
  }
}
```

Unlike whole-payload encryption the key is always sourced from a vault via an
`ISecretResolver`, selected by `Source`. A `SecretEncryptionInterceptor` (a
client + worker interceptor, not a payload codec) encrypts `Secret<T>` values in
workflow/signal/query arguments on the way out and decrypts activity arguments
on the way in, resolving and caching the key lazily. Carry the `Secret<T>`
opaquely through the workflow and read `.Value` in the activity.

## Full configuration

### Vault keys

To source the encryption key (or secret key) from a vault instead of an inline
string, register a resolver from `Kogoshvili.Temporal.Cloud` and point `Source`
at it:

```csharp
builder.Services.AddAzureKeyVaultSecretResolver("https://my-vault.vault.azure.net");
// or: builder.Services.AddAwsSecretsManagerSecretResolver("us-east-1");
```

```jsonc
"DataConverter": {
  "Encryption": {
    "Enabled": true,
    "Source": "azureKeyVault",
    "SecretId": "my-encryption-key",
    "Encoding": "base64"
  }
}
```

The resolver is selected by name (`azureKeyVault` / `awsSecretsManager`).
`TemporalSecretLoader` runs at startup, resolves the key, and swaps it into the
client's data converter before any worker connects.

### Cloud claim-check stores

Register a store factory and point `Store` at it:

```csharp
builder.Services.AddAzureBlobClaimCheckStore();
// or: builder.Services.AddS3ClaimCheckStore();
```

Azure Blob:

```jsonc
"DataConverter": {
  "ClaimCheck": {
    "Enabled": true,
    "Store": "azureBlob",
    "AccountUri": "https://myaccount.blob.core.windows.net", // managed identity
    "ContainerName": "claim-check"
  }
}
```

`ConnectionString` (or `ConnectionStringSecretId`, a Key Vault secret name) can
replace `AccountUri` when a managed identity is not used.

S3:

```jsonc
"DataConverter": {
  "ClaimCheck": {
    "Enabled": true,
    "Store": "s3",
    "Region": "us-east-1",
    "BucketName": "claim-check"
  }
}
```

`AccessKeySecretId` + `SecretKeySecretId` (optionally `SessionTokenSecretId`)
supply explicit credentials from AWS Secrets Manager in place of the default
credential chain; `RoleArn` + `RoleSessionName` assume an IAM role.

### Codec server

When encryption or claim check is enabled, the composed codec is registered as a
singleton `IPayloadCodec`. A `Kogoshvili.Temporal.CodecServer` hosted in the same
app resolves that same instance, so its `/encode` and `/decode` endpoints use
exactly the codec the client and workers use:

```csharp
builder.Services.AddTemporalCodecServer();
// ...
app.MapTemporalCodecServer();
```

Point the Temporal Web UI or CLI at it to read back the payloads this worker
writes:

```
temporal workflow show --workflow-id <id> --codec-endpoint http://localhost:5000
```

### Double encryption

`DataConverter:Encryption` and `DataConverter:Secret` are independent. When both
are enabled a `Secret<T>` field is encrypted twice: once by the per-field
interceptor and once by the whole-payload codec. The secret's serialized form
shares the encryption codec's `{ encoding, encryption-key-id, data }` shape.
