# Kogoshvili.Temporal.Codec

Composable payload codecs for the [Temporal](https://temporal.io) .NET SDK,
built on `Temporalio.Converters.IPayloadCodec`. They plug into a
`DataConverter` (via `DataConverter.Default with { PayloadCodec = ... }`) and
run on both the client and the workers — encryption and claim-checking happen
before anything is sent to the Temporal service.

## What it provides

- **`EncryptionCodec`** — AES-GCM end-to-end encryption of every payload, with a
  key id in the metadata for key rotation. Compatible with the encryption
  samples from the other Temporal SDKs.
- **`ClaimCheckCodec`** — offloads payloads larger than a threshold to a
  pluggable `IClaimCheckStore`, leaving only a small reference in the workflow
  history.
- **`FileSystemClaimCheckStore`** — the built-in store, one file per blob.
- **`CompositePayloadCodec`** — chains codecs in order on encode, reverse on
  decode.
- **`ISecretResolver`** — abstraction for fetching a secret (encryption key,
  connection string, access key) from a secret store; Azure Key Vault and AWS
  Secrets Manager implementations ship in `Kogoshvili.Temporal.Cloud`.
- **`IClaimCheckStoreFactory`** / **`ClaimCheckStoreSettings`** — abstraction
  for building a cloud claim-check store from resolved settings, so the hosting
  starter stays free of cloud SDK dependencies.
- **`Secret<T>`** — a per-field secret value, encrypted independently of the
  payload codec so it stays unreadable even after the surrounding payload has
  been decrypted (for example by the Temporal UI's codec server). Encrypts to
  the same `binary/encrypted` shape the encryption codec emits.
- **`SecretEncryptionInterceptor`** — a client + worker interceptor that
  encrypts `Secret<T>` values on the way out and decrypts them on the way in,
  keyed from an `ISecretResolver`.

## Usage

```csharp
using Kogoshvili.Temporal.Codec;
using Temporalio.Client;
using Temporalio.Converters;

// Encrypt, then offload anything over 1 MiB to a local directory.
// The key must be exactly 16, 24, or 32 ASCII bytes.
var codec = new CompositePayloadCodec(
    new EncryptionCodec("test-key-16bytes"),
    new ClaimCheckCodec(new FileSystemClaimCheckStore("/tmp/claim-check"), thresholdBytes: 1024 * 1024));

var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    DataConverter = DataConverter.Default with { PayloadCodec = codec },
});
```

Order matters: `new CompositePayloadCodec(encryption, claimCheck)` produces
`serialize → encrypt → offload` on encode and `fetch → decrypt → deserialize` on
decode, so the blobs in the store are ciphertext.

## Per-field secrets

Encrypt the *whole* payload and every field is hidden, but a single sensitive
field (an SSN, an access token) can be encrypted on its own so it stays
unreadable even after the payload around it is decrypted — for example by the
Temporal UI when it points at your codec server. Use `Secret<T>` for that field
and pair it with the `SecretEncryptionInterceptor`:

```csharp
using Kogoshvili.Temporal.Codec;

class Patient
{
    public string Name { get; set; }
    public Secret<string> Ssn { get; set; }
}

var interceptor = new SecretEncryptionInterceptor(
    resolver, secretId: "ssn-key", keyId: "ssn-v1");

// On the client, Secret<T> values in workflow/signal/query arguments are
// encrypted automatically; on the worker, activity arguments are decrypted
// automatically before the activity runs.
var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    Interceptors = new[] { interceptor },
});
```

A `Secret<T>` is carried opaquely through a workflow — construct it on the
client with plaintext and let the interceptor encrypt it; read `.Value` in an
activity after the interceptor has decrypted it. Its serialized form is the
same `{ encoding, encryption-key-id, data }` shape the encryption codec emits,
so it is indistinguishable from an encrypted payload in the UI.

`Secret<T>` implements the non-generic `ISecret` marker interface, and its
JSON form is produced by `SecretJsonConverterFactory` (a
`System.Text.Json.JsonConverterFactory`). The converter fails loudly if asked to
serialize a `Secret<T>` still holding plaintext — encryption happens in the
interceptor before serialization, so plaintext never reaches the wire.

Azure Blob and AWS S3 stores are provided by
`Kogoshvili.Temporal.Cloud`, and a ready-made HTTP codec server (for the
Temporal UI / CLI) by `Kogoshvili.Temporal.CodecServer`.

Not affiliated with or endorsed by Temporal Technologies.
