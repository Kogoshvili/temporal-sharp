# Codec Server

A ready-made HTTP codec server for the Temporal Web UI and CLI. It exposes the
`/encode` and `/decode` endpoints that decode (and encode) the workflow payloads
your workers write, wrapping the same `IPayloadCodec` the workers use — so
encryption keys never leave your environment while the UI can still display
decoded data.

## Run

```sh
dotnet run
```

By default it listens on `http://localhost:PORT` (see `Urls` in
`appsettings.json`). Point the Web UI at it via the codec-server (eyeglasses)
control, or use the CLI:

```sh
temporal workflow show --workflow-id <id> --codec-endpoint http://localhost:PORT
```

## How it works

This app registers a single `IPayloadCodec` and maps the codec-server endpoints
over it. The codec is built from the `CodecServer` section of `appsettings.json`.

<!--#if (EnableEncryption) -->
### Encryption

Every payload is AES-GCM encrypted before it is sent to the server (and
decrypted on the way back). `KeyId` is stamped into each payload for key
rotation.

<!--#if (UseVault) -->
The encryption key is resolved from a secret store at startup via
`ISecretResolver`:

- `SecretId` — the secret name (Azure Key Vault) or secret id (AWS Secrets
  Manager) holding the AES-GCM key.
- `Encoding` — how the secret decodes into key bytes: `raw` (ASCII), `base64`,
  or `hex`.

<!--#if (UseAzure) -->
- `VaultUri` — the Azure Key Vault URI the resolver connects to.
<!--#endif -->
<!--#if (UseAws) -->
- `Region` — the AWS region the resolver connects to.
<!--#endif -->
<!--#else -->
The encryption key is read directly from `Key` in `appsettings.json` (an ASCII
string of 16, 24, or 32 bytes). `Encoding` controls how it decodes into bytes
(`raw` or `base64`). For production, switch to a vault source and store the key
in your key-management system.
<!--#endif -->
<!--#endif -->

<!--#if (EnableClaimCheck) -->
### Claim-check

Payloads larger than `ThresholdBytes` are offloaded to a store and replaced by a
small reference in the workflow history. This app uses the filesystem store —
blobs are written to `ClaimCheckDirectory` (one file per blob). For Azure Blob or
S3, swap in a store from `Kogoshvili.Temporal.Cloud`:

```csharp
using Kogoshvili.Temporal.Codec;
using Kogoshvili.Temporal.Cloud;
using Temporalio.Converters;

// Azure Blob
var store = new AzureBlobClaimCheckStore(
    "<connection-string>", "temporal-claim-check");

// Or S3, using the default credential chain:
// var store = new S3ClaimCheckStore(
//     AwsCredentialResolver.Resolve(),
//     Amazon.RegionEndpoint.USEast1,
//     "temporal-claim-check");

var codec = new ClaimCheckCodec(store, thresholdBytes: 1024 * 1024);
```
<!--#endif -->

<!--#if (EnableEncryption && EnableClaimCheck) -->
The codecs are composed so that encoding runs `serialize → encrypt → offload`,
meaning the blobs in the claim-check store are already ciphertext.
<!--#endif -->

## Pointing your workers at the same codec

This app is standalone, but the codec it wraps must match what your workers use,
or the UI will fail to decode their payloads. There are two ways to wire the same
codec into your workers.

### With the official Temporal .NET SDK

Build the same codec and set it as the client's `DataConverter`; every worker
created from that client inherits it:

```csharp
using Kogoshvili.Temporal.Codec;
<!--#if (UseVault) -->
using Kogoshvili.Temporal.Cloud;
<!--#endif -->
using Temporalio.Client;
using Temporalio.Converters;
using Temporalio.Worker;

<!--#if (EnableEncryption) -->
<!--#if (UseVault) -->
<!--#if (UseAzure) -->
var resolver = new AzureKeyVaultSecretResolver(
    "https://my-vault.vault.azure.net", AzureCredentialResolver.Resolve());
<!--#else -->
var resolver = new AwsSecretsManagerSecretResolver(
    AwsCredentialResolver.Resolve(), "us-east-1");
<!--#endif -->
var secret = await resolver.ResolveAsync("temporal-codec-key");
var key = Convert.FromBase64String(secret);
var encryption = new EncryptionCodec(key, keyId: "default");
<!--#else -->
var encryption = new EncryptionCodec("0123456789abcdef", keyId: "default");
<!--#endif -->
<!--#endif -->
<!--#if (EnableClaimCheck) -->
var claimCheck = new ClaimCheckCodec(
    new FileSystemClaimCheckStore("claim-check"), thresholdBytes: 1024 * 1024);
<!--#endif -->

<!--#if (EnableEncryption && EnableClaimCheck) -->
var codec = new CompositePayloadCodec(encryption, claimCheck);
<!--#elif (EnableEncryption) -->
var codec = encryption;
<!--#else -->
var codec = claimCheck;
<!--#endif -->

var client = await TemporalClient.ConnectAsync(new("localhost:7233")
{
    DataConverter = DataConverter.Default with { PayloadCodec = codec },
});

using var worker = new TemporalWorker(client, new TemporalWorkerOptions("my-task-queue")
    .AddWorkflow<MyWorkflow>());
await worker.ExecuteAsync(() => { /* run */ });
```

### With Kogoshvili.Temporal.Hosting

The starter builds the codec from configuration — set the `Temporal:DataConverter`
section and it is applied to the client and every worker automatically. Point the
codec server at the same values:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("my-task-queue")
    .AddDiscoveredTypes();

using var host = builder.Build();
await host.RunAsync();
```

```jsonc
// appsettings.json
{
  "Temporal": {
    "DataConverter": {
<!--#if (EnableEncryption) -->
      "Encryption": {
        "Enabled": true,
<!--#if (UseVault) -->
        "Source": "azureKeyVault",   // or "awsSecretsManager"
        "SecretId": "temporal-codec-key",
        "Encoding": "base64"
<!--#else -->
        "Key": "0123456789abcdef"
<!--#endif -->
      },
<!--#endif -->
<!--#if (EnableClaimCheck) -->
      "ClaimCheck": {
        "Enabled": true,
        "Store": "filesystem",
        "Directory": "claim-check",
        "ThresholdBytes": 1048576
      }
<!--#endif -->
    }
  }
}
```

Alternatively, host these endpoints inside your worker app instead of running
this standalone server: call `AddTemporalCodecServer()` and
`MapTemporalCodecServer()` (from `Kogoshvili.Temporal.CodecServer`) alongside
`AddTemporal`.

<!--#if (UseAuth) -->
## Authentication

<!--#if (UsePassToken) -->
`Auth.PassAccessToken` is enabled: the endpoints validate the JWT access token
the Temporal Web UI forwards in the `Authorization` header against the OIDC
provider's JWKS (defaulting to Temporal Cloud).
<!--#endif -->
<!--#if (UseCrossOrigin) -->
`Auth.IncludeCrossOriginCredentials` is enabled: the server keeps its own session
via an OAuth2 authorization-code flow, so opening the Temporal UI redirects
through your IdP and back. Set `OidcAuthority`, `ClientId`, and `ClientSecret`
in `appsettings.json`.
<!--#endif -->

Because the codec server can decode sensitive data, run it over HTTPS and
restrict ingress (VPN or `localhost`) unless authentication is enabled.
<!--#endif -->

Not affiliated with or endorsed by Temporal Technologies.
