# Kogoshvili.Temporal.Cloud

Azure and AWS integrations for the Temporal .NET SDK: credential resolution,
Blob/S3 claim-check stores, Key Vault/Secrets Manager TLS certificate sources,
and secret resolvers. Pair with `Kogoshvili.Temporal.Codec` to offload large
workflow payloads to cloud storage or fetch codec keys from a vault.

## Minimal setup

The smallest integration is a single claim-check store or a credential
resolver. An Azure Blob store needs only a connection string and a container
name (created if absent):

```csharp
using Kogoshvili.Temporal.Cloud;

var store = new AzureBlobClaimCheckStore(
    "<connection-string>", "temporal-claim-check");

var key = await store.StoreAsync(new byte[] { 1, 2, 3 });
var bytes = await store.LoadAsync(key);
```

The S3 equivalent takes AWS credentials, a region endpoint, and a bucket name:

```csharp
using Amazon;
using Kogoshvili.Temporal.Cloud;

var store = new S3ClaimCheckStore(
    AwsCredentialResolver.Resolve(),
    RegionEndpoint.USEast1,
    "temporal-claim-check");
```

Credential resolvers are also usable on their own: `AzureCredentialResolver.Resolve()`
returns a `TokenCredential` from the default Azure chain (managed/workload
identity, env vars, CLI, interactive login), and `AwsCredentialResolver.Resolve()`
returns `AWSCredentials` from the AWS default fallback chain.

## Configuration

This library is code-only: it has no configuration of its own. Instead it plugs
into the hosting starter (`Kogoshvili.Temporal.Hosting`), which selects cloud
services by name from the standard `Temporal:` section. Register a cloud TLS
certificate source, then select it with `Temporal:Tls:Source`:

```csharp
builder.Services.AddAzureKeyVaultCertificateSource();
// or: builder.Services.AddAwsSecretsManagerCertificateSource();
```

```json
{
  "Temporal": {
    "Tls": {
      "Source": "azureKeyVault",
      "AzureKeyVault": {
        "VaultUri": "https://my-vault.vault.azure.net",
        "CertificateName": "temporal-client",
        "Password": null
      }
    }
  }
}
```

For claim-check offload, register a store factory and select it with
`Temporal:DataConverter:ClaimCheck:Store` (`azureBlob` or `s3`):

```csharp
builder.Services.AddAzureBlobClaimCheckStore();
// or: builder.Services.AddS3ClaimCheckStore();
```

```json
{
  "Temporal": {
    "DataConverter": {
      "ClaimCheck": {
        "Enabled": true,
        "Store": "azureBlob",
        "AccountUri": "https://myaccount.blob.core.windows.net",
        "ContainerName": "temporal-claim-check"
      }
    }
  }
}
```

An Azure Blob store authenticates via managed identity when `AccountUri` is set,
or via `ConnectionString` otherwise. For S3, set `Region`, `BucketName`, and
optionally `AccessKey`/`SecretKey`/`SessionToken` or `RoleArn`.

## Full configuration

The complete set of cloud integrations covers Azure and AWS symmetric features:

**Claim-check stores.** `AzureBlobClaimCheckStore` and `S3ClaimCheckStore` are
`IClaimCheckStore` implementations for offloading large payloads. They accept
pre-configured clients for flexibility:

```csharp
using Azure.Storage.Blobs;
using Kogoshvili.Temporal.Cloud;

var container = new BlobContainerClient("<connection-string>", "temporal-claim-check");
var blobStore = new AzureBlobClaimCheckStore(container);

var s3Store = new S3ClaimCheckStore(
    new Amazon.S3.AmazonS3Client(
        AwsCredentialResolver.Resolve(),
        Amazon.RegionEndpoint.USEast1),
    "temporal-claim-check");
```

The managed-identity Blob constructor takes an account URI and a credential:

```csharp
var store = new AzureBlobClaimCheckStore(
    new Uri("https://myaccount.blob.core.windows.net"),
    AzureCredentialResolver.Resolve(),
    "temporal-claim-check");
```

**Secret resolvers.** `AzureKeyVaultSecretResolver` and
`AwsSecretsManagerSecretResolver` are `ISecretResolver` implementations for
fetching arbitrary secrets (for example a codec key) from a vault:

```csharp
using Kogoshvili.Temporal.Cloud;
using Kogoshvili.Temporal.Codec;

var resolver = new AzureKeyVaultSecretResolver(
    "https://my-vault.vault.azure.net",
    AzureCredentialResolver.Resolve());

var key = Convert.FromBase64String(await resolver.ResolveAsync("my-encryption-key"));
```

```csharp
var resolver = new AwsSecretsManagerSecretResolver(
    AwsCredentialResolver.Resolve(),
    "us-east-1");

var key = Convert.FromBase64String(await resolver.ResolveAsync("my-encryption-key"));
```

Both resolvers also accept a pre-configured SDK client (`SecretClient` / an
`IAmazonSecretsManager`).

**TLS certificate sources.** `AzureKeyVaultCertificateSource` and
`AwsSecretsManagerCertificateSource` implement `ITlsCertificateSource` for
Temporal Cloud mTLS. The Azure source fetches a PFX secret and converts it to
PEM; `PfxToPem` is public for direct use:

```csharp
using Kogoshvili.Temporal.Cloud;

var material = AzureKeyVaultCertificateSource.PfxToPem(
    pfxBytes, password: "pfx-password");
```

The AWS source reads the client certificate and private key as PEM (or base64
PEM) secrets, optionally including a server root CA:

```json
{
  "Temporal": {
    "Tls": {
      "Source": "awsSecretsManager",
      "AwsSecretsManager": {
        "Region": "us-east-1",
        "CertificateSecretId": "temporal-client-cert",
        "PrivateKeySecretId": "temporal-client-key",
        "ServerRootCACertSecretId": null
      }
    }
  }
}
```

**Dependency-injection wiring.** Each feature exposes `Add*` extensions that
register against the default credential chain; every one accepts an explicit
credential for custom or test setups:

```csharp
builder.Services.AddAzureKeyVaultSecretResolver(
    "https://my-vault.vault.azure.net", credential);
builder.Services.AddAwsSecretsManagerSecretResolver("us-east-1", credentials);
builder.Services.AddAzureKeyVaultCertificateSource(credential);
builder.Services.AddAwsSecretsManagerCertificateSource(credentials);
builder.Services.AddAzureBlobClaimCheckStore();
builder.Services.AddS3ClaimCheckStore();
```

The hosting starter discovers these by name: `azureKeyVault` / `awsSecretsManager`
for TLS and secret resolution, and `azureBlob` / `s3` for claim-check stores.

The cloud stores live in a separate package so the lightweight
`Kogoshvili.Temporal.Codec` and `Kogoshvili.Temporal.Hosting` packages don't pull
in the Azure/AWS SDKs.

Not affiliated with or endorsed by Temporal Technologies.
