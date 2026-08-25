# Kogoshvili.Temporal.Cloud

Azure and AWS integration for the Temporal .NET SDK: credential resolution and
claim-check payload stores backed by Azure Blob Storage and Amazon S3. Pair with
`Kogoshvili.Temporal.Codec` to offload large workflow payloads to cloud storage.

## What it provides

- **`AzureCredentialResolver`** — `DefaultAzureCredential` from the standard
  Azure chain (env vars, managed/workload identity, CLI, interactive login).
- **`AwsCredentialResolver`** — AWS credentials from the default fallback chain
  (env vars, shared credentials/profile files, ECS/EC2 roles, SSO).
- **`AzureBlobClaimCheckStore`** — an `IClaimCheckStore` over an Azure Blob
  container.
- **`S3ClaimCheckStore`** — an `IClaimCheckStore` over an Amazon S3 bucket.

## Usage

```csharp
using Kogoshvili.Temporal.Cloud;
using Kogoshvili.Temporal.Codec;
using Temporalio.Client;
using Temporalio.Converters;

// Azure Blob
var blobStore = new AzureBlobClaimCheckStore(
    "<connection-string>", "temporal-claim-check");

// Or S3, using the default credential chain
var s3Store = new S3ClaimCheckStore(
    AwsCredentialResolver.Resolve(),
    Amazon.RegionEndpoint.USEast1,
    "temporal-claim-check");

var codec = new CompositePayloadCodec(
    new EncryptionCodec("demo-key-16-bytes!"),
    new ClaimCheckCodec(s3Store, thresholdBytes: 1024 * 1024));

var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    DataConverter = DataConverter.Default with { PayloadCodec = codec },
});
```

The cloud stores live in a separate package so the lightweight
`Kogoshvili.Temporal.Codec` and `Kogoshvili.Temporal.Hosting` packages don't pull
in the Azure/AWS SDKs.

Not affiliated with or endorsed by Temporal Technologies.
