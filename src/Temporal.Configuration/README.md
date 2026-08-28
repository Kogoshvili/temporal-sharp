# Kogoshvili.Temporal.Configuration

Shared Temporal connection configuration for the Kogoshvili.Temporal tool
suite. It centralizes the "how do I reach and authenticate against Temporal"
logic so the hosting starter, the testing harness, and the `temporal-sharp` CLI
all behave the same way.

## What it provides

- **`TemporalConnectionOptions`** — the connection shape: target host, namespace,
  API key, TLS, plus the connection-level option groups below.
- **`TemporalTlsOptions`** — mTLS certificates from files, environment
  variables, or (via `Kogoshvili.Temporal.Cloud`) Azure Key Vault / AWS Secrets
  Manager.
- **`ITlsCertificateSource`** / **`TlsCertificateMaterial`** — pluggable
  certificate resolution (`FileTlsCertificateSource`,
  `EnvironmentTlsCertificateSource`).
- **`ClientOptionsFactory`** — mutates a `TemporalClientConnectOptions` in place
  from the resolved options (`Apply(...)`), including TLS material.
- **`TemporalConfig`** — loads options from `appsettings.json` + `Temporal__*`
  environment variables and builds an authenticated `ITemporalClient`.
- Connection option groups:
  - **`TemporalRpcRetryOptions`** — RPC retry policy (interval, multiplier, max
    retries/elapsed).
  - **`TemporalKeepAliveOptions`** — HTTP/2 keep-alive ping interval and timeout.
  - **`TemporalHttpConnectProxyOptions`** — HTTP CONNECT proxy (target host,
    username, password).
  - **`TemporalDnsLoadBalancingOptions`** — periodic DNS re-resolution interval.
  - **`TemporalGrpcCompressionOptions`** — transport gRPC compression mode.
- **`TlsContent`** — helpers for decoding/encoding PEM (base64 or raw).
- **`AzureKeyVaultTlsOptions`** / **`AwsSecretsManagerTlsOptions`** — the
  nested config for the cloud TLS certificate sources.

## Configuration

```json
{
  "Temporal": {
    "TargetHost": "my-namespace.a1b2c.tmprl.cloud:7233",
    "Namespace": "my-namespace.a1b2c",
    "ApiKey": "…",
    "Tls": {
      "Disabled": false,
      "Domain": null,
      "Source": "file",
      "ServerRootCACertPath": "/path/to/ca.pem",
      "ClientCertPath": "/path/to/client.pem",
      "ClientPrivateKeyPath": "/path/to/client.key"
    },
    "RpcRetry": {
      "InitialInterval": "00:00:00.100",
      "Multiplier": 1.5,
      "MaxInterval": "00:00:05",
      "MaxElapsedTime": "00:00:10",
      "MaxRetries": 10
    },
    "KeepAlive": { "Interval": "00:00:30", "Timeout": "00:00:15" },
    "HttpConnectProxy": { "TargetHost": null, "Username": null, "Password": null },
    "DnsLoadBalancing": { "ResolutionInterval": null },
    "GrpcCompression": { "Mode": "gzip" }
  }
}
```

Environment variables override the file (`Temporal__TargetHost`,
`Temporal__Namespace`, `Temporal__ApiKey`, `Temporal__Tls__ClientCertPath`,
…).

## TLS sources

`TemporalTlsOptions.Source` selects where certificates come from:

- **`file`** (default) — PEM files at `ServerRootCACertPath`, `ClientCertPath`,
  and `ClientPrivateKeyPath`.
- **`environment`** — inline `ServerRootCACert`/`ClientCert`/`ClientPrivateKey`
  strings (base64 or raw PEM), typically injected as environment variables:

  ```json
  {
    "Temporal": {
      "Tls": {
        "Source": "environment",
        "ClientCert": "LS0tLS1CRUdJTiBDRVJUSUZJQ0FURS0tLS0t…",
        "ClientPrivateKey": "LS0tLS1CRUdJTiBQUklWQVRFIEtFWS0tLS0t…"
      }
    }
  }
  ```

- **`azureKeyVault`** / **`awsSecretsManager`** — fetched at startup by the
  hosting starter. These are not resolved by `ClientOptionsFactory` (which is
  synchronous); register the matching source from `Kogoshvili.Temporal.Cloud`
  and let `TemporalCertificateLoader` apply it.

```csharp
// Register the cloud source, then select it in config:
builder.Services.AddAzureKeyVaultCertificateSource();
```

```json
{
  "Temporal": {
    "Tls": {
      "Source": "azureKeyVault",
      "AzureKeyVault": {
        "VaultUri": "https://my-vault.vault.azure.net",
        "CertificateName": "temporal-client"
      }
    }
  }
}
```

`Tls:Disabled` skips TLS entirely, and `Tls:Domain` sets the expected server
hostname/domain.

## Usage

```csharp
using Kogoshvili.Temporal.Configuration;
using Temporalio.Client;

// Connect from appsettings.json + environment variables:
ITemporalClient client = await TemporalConfig.ConnectAsync();

// Or bind manually and connect:
var options = TemporalConfig.Load();
ITemporalClient client2 = await TemporalConfig.ConnectAsync(options);
```

`TemporalConfig` also exposes `Load(IConfiguration)` (bind from an existing
configuration), `BuildConfiguration(appSettingsPath)` (build the merged
`appsettings.json` + env-var configuration), and
`ToConnectOptions(TemporalConnectionOptions)` (map options to the SDK connect
options).

Not affiliated with or endorsed by Temporal Technologies.
