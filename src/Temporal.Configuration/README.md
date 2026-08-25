# Kogoshvili.Temporal.Configuration

Shared Temporal connection configuration for the Kogoshvili.Temporal tool
suite. It centralizes the "how do I reach and authenticate against Temporal"
logic so the hosting starter, the testing harness, and the `temporal-sharp` CLI
all behave the same way.

## What it provides

- **`TemporalConnectionOptions`** — target host, namespace, API key, TLS.
- **`TemporalTlsOptions`** — mTLS certificates from files, environment
  variables, or (via `Kogoshvili.Temporal.Cloud`) Azure Key Vault / AWS Secrets
  Manager.
- **`ITlsCertificateSource`** / **`TlsCertificateMaterial`** — pluggable
  certificate resolution.
- **`ClientOptionsFactory`** — maps options to `TemporalClientConnectOptions`.
- **`TemporalConfig`** — loads options from `appsettings.json` + `Temporal__*`
  environment variables and builds an authenticated `ITemporalClient`.

## Configuration

```json
{
  "Temporal": {
    "TargetHost": "my-namespace.a1b2c.tmprl.cloud:7233",
    "Namespace": "my-namespace.a1b2c",
    "ApiKey": "…",
    "Tls": {
      "Domain": null,
      "ClientCertPath": "/path/to/client.pem",
      "ClientPrivateKeyPath": "/path/to/client.key"
    }
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

Not affiliated with or endorsed by Temporal Technologies.
