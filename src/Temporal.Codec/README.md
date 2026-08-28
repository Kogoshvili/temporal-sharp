# Kogoshvili.Temporal.Codec

Composable payload codecs for the [Temporal](https://temporal.io) .NET SDK,
built on `Temporalio.Converters.IPayloadCodec`. Plug one into a `DataConverter`
and it runs on both the client and workers — encryption and claim-checking
happen before anything is sent to the Temporal service.

## Minimal setup

A single `EncryptionCodec` AES-GCM encrypts every payload. The key must be
exactly 16, 24, or 32 ASCII bytes (the `string` overload is for demos; prefer
the `byte[]` overload for production key material).

```csharp
using Kogoshvili.Temporal.Codec;
using Temporalio.Client;
using Temporalio.Converters;

var codec = new EncryptionCodec("test-key-16bytes");

var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    DataConverter = DataConverter.Default with { PayloadCodec = codec },
});
```

The codec records a key id (`"default"` by default) in each payload's metadata
so the decode side can detect key rotation.

## Configuration

This library is code-only — there is no `appsettings.json` section. Ordering is
expressed via `CompositePayloadCodec`, which chains codecs left-to-right on
encode and right-to-left on decode:

```csharp
using Kogoshvili.Temporal.Codec;

var codec = new CompositePayloadCodec(
    new EncryptionCodec("test-key-16bytes"),
    new ClaimCheckCodec(new FileSystemClaimCheckStore("/tmp/claim-check")));
```

`new CompositePayloadCodec(encryption, claimCheck)` produces
`serialize -> encrypt -> offload` on encode and
`fetch -> decrypt -> deserialize` on decode, so the blobs in the store are
ciphertext.

## Full configuration

The remaining pieces compose on top of the minimal codec.

**Claim-checking** offloads payloads larger than a threshold (default 1 MiB) to
an `IClaimCheckStore`, leaving a small reference in the workflow history:

```csharp
using Kogoshvili.Temporal.Codec;

var store = new FileSystemClaimCheckStore("/tmp/claim-check");
var claimCheck = new ClaimCheckCodec(store, thresholdBytes: 512 * 1024);
```

The filesystem store is built directly. Cloud-backed stores (Azure Blob, AWS
S3) ship in `Kogoshvili.Temporal.Cloud` and are built from a
`ClaimCheckStoreSettings` record through an `IClaimCheckStoreFactory`, keeping
this package free of cloud SDK dependencies.

**Per-field secrets** encrypt a single field (an SSN, an access token) so it
stays unreadable even after the surrounding payload is decrypted — for example
by the Temporal UI's codec server. Use `Secret<T>` for the field and pair it
with a `SecretEncryptionInterceptor`, keyed from an `ISecretResolver`:

```csharp
using Kogoshvili.Temporal.Codec;
using Temporalio.Client;

class Patient
{
    public string Name { get; set; }
    public Secret<string> Ssn { get; set; }
}

var interceptor = new SecretEncryptionInterceptor(
    resolver, secretId: "ssn-key", keyId: "ssn-v1");

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

`Secret<T>` implements the non-generic `ISecret` marker interface, and its JSON
form is produced by `SecretJsonConverterFactory` (a
`System.Text.Json.JsonConverterFactory`). The converter fails loudly if asked
to serialize a `Secret<T>` still holding plaintext — encryption happens in the
interceptor before serialization, so plaintext never reaches the wire.

Azure Blob and AWS S3 stores are provided by `Kogoshvili.Temporal.Cloud`, and a
ready-made HTTP codec server (for the Temporal UI / CLI) by
`Kogoshvili.Temporal.CodecServer`.

Not affiliated with or endorsed by Temporal Technologies.
