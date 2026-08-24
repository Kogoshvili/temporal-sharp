# Kogoshvili.Temporal.Configuration

Shared Temporal connection configuration for the Kogoshvili.Temporal tool
suite. It centralizes the "how do I reach and authenticate against Temporal"
logic so the hosting starter, the testing harness, and the `temporal-sharp` CLI
all behave the same way.

## What it provides

- **`TemporalConnectionOptions`** — target host, namespace, API key, TLS.
- **`TemporalTlsOptions`** — mTLS certificate paths and expected domain.
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
