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

## Usage

```csharp
using Kogoshvili.Temporal.Codec;
using Temporalio.Client;
using Temporalio.Converters;

// Encrypt, then offload anything over 1 MiB to a local directory.
var codec = new CompositePayloadCodec(
    new EncryptionCodec("demo-key-16-bytes!"),
    new ClaimCheckCodec(new FileSystemClaimCheckStore("/tmp/claim-check"), thresholdBytes: 1024 * 1024));

var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    DataConverter = DataConverter.Default with { PayloadCodec = codec },
});
```

Order matters: `new CompositePayloadCodec(encryption, claimCheck)` produces
`serialize → encrypt → offload` on encode and `fetch → decrypt → deserialize` on
decode, so the blobs in the store are ciphertext.

Azure Blob and AWS S3 stores are provided by
`Kogoshvili.Temporal.Cloud`, and a ready-made HTTP codec server (for the
Temporal UI / CLI) by `Kogoshvili.Temporal.CodecServer`.

Not affiliated with or endorsed by Temporal Technologies.
