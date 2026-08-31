# Kogoshvili.Temporal.Configuration

Shared Temporal connection configuration for the Kogoshvili.Temporal tool
suite. It centralizes the "how do I reach and authenticate against Temporal"
logic so the hosting starter, the testing harness, and the `temporal-sharp` CLI
all behave the same way.

## Minimal setup

Add `Temporal:TargetHost` (and, for a non-default namespace, `Temporal:Namespace`)
to `appsettings.json`:

```json
{
  "Temporal": {
    "TargetHost": "my-namespace.a1b2c.tmprl.cloud:7233",
    "Namespace": "my-namespace.a1b2c"
  }
}
```

Then connect:

```csharp
using Kogoshvili.Temporal.Configuration;
using Temporalio.Client;

ITemporalClient client = await TemporalConfig.ConnectAsync();
```

`ConnectAsync()` binds the `Temporal` section of `appsettings.json` (loaded from
the current directory) merged with `Temporal__*` environment variables, and
returns an authenticated `ITemporalClient`. When no section is present,
`TargetHost` defaults to `localhost:7233` and `Namespace` to `default`.

## Configuration

All connection settings live under the `Temporal` section. Each group is
optional; leave a group out to keep the SDK defaults.

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
      "RandomizationFactor": 0.2,
      "Multiplier": 1.5,
      "MaxInterval": "00:00:05",
      "MaxElapsedTime": "00:00:10",
      "MaxRetries": 10
    },

    "KeepAlive": { "Interval": "00:00:30", "Timeout": "00:00:15" },

    "HttpConnectProxy": { "TargetHost": "proxy:8080", "Username": null, "Password": null },

    "DnsLoadBalancing": { "ResolutionInterval": "00:00:30" },

    "GrpcCompression": { "Mode": "gzip" }
  }
}
```

Top-level keys:

- **`TargetHost`** — the server `host:port`. Default `localhost:7233`.
- **`Namespace`** — the Temporal namespace. Default `default`.
- **`ApiKey`** — API key sent on every call, or `null` for none.
- **`Tls`** — mTLS settings; see below. `null` means no TLS.
- **`RpcRetry`** — RPC retry policy: `InitialInterval` (100ms),
  `RandomizationFactor` (jitter, 0.2), `Multiplier` (1.5), `MaxInterval` (5s),
  `MaxElapsedTime` (10s, `null` for none), and `MaxRetries` (10). Durations bind
  as time-span strings.
- **`KeepAlive`** — HTTP/2 keep-alive ping `Interval` (30s) and `Timeout` (15s).
- **`HttpConnectProxy`** — HTTP CONNECT proxy. Set `TargetHost` to route through
  it; add `Username`/`Password` for basic auth. Omit the group to connect
  directly.
- **`DnsLoadBalancing`** — when set, DNS is re-resolved periodically and
  connections load-balance across addresses. `ResolutionInterval` defaults to
  30s.
- **`GrpcCompression`** — transport gRPC compression `Mode`: `"gzip"` (default)
  or `"none"`.

## Full configuration

### TLS sources

`Tls:Source` selects where certificates come from:

- **`file`** (default) — PEM files at `ServerRootCACertPath`, `ClientCertPath`,
  and `ClientPrivateKeyPath`.
- **`environment`** — inline `ServerRootCACert` / `ClientCert` /
  `ClientPrivateKey` strings, raw PEM or base64 (typically injected as
  environment variables):

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

- **`azureKeyVault`** — a PFX secret in Azure Key Vault, converted to PEM at
  startup:

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

- **`awsSecretsManager`** — PEM text secrets in AWS Secrets Manager:

  ```json
  {
    "Temporal": {
      "Tls": {
        "Source": "awsSecretsManager",
        "AwsSecretsManager": {
          "Region": "us-east-1",
          "CertificateSecretId": "temporal/client-cert",
          "PrivateKeySecretId": "temporal/client-key",
          "ServerRootCACertSecretId": null
        }
      }
    }
  }
  ```

`Tls:Disabled` skips TLS entirely, and `Tls:Domain` sets the expected server
hostname/domain. Setting both a `*Path` and inline certificate content at once
is rejected by `TemporalTlsOptions.Validate()`.

### Cloud TLS resolution

> **Important:** `TemporalConfig.ConnectAsync()` and
> `TemporalConfig.ToConnectOptions()` resolve only the `file` and
> `environment` sources. If `Tls:Source` is `azureKeyVault` or
> `awsSecretsManager`, a client built through `Kogoshvili.Temporal.Configuration`
> alone connects **without** the configured client certificate — no error is
> raised. Use the cloud sources only through the hosting starter, or resolve
> the certificate material yourself (via an `ITlsCertificateSource`) and call
> `ClientOptionsFactory.BuildTls(TlsCertificateMaterial, TemporalTlsOptions)`.

The `file` and `environment` sources are resolved synchronously by
`ClientOptionsFactory` when the connect options are built. The cloud sources
(`azureKeyVault` / `awsSecretsManager`) are asynchronous and are skipped there;
the hosting starter resolves them via `Kogoshvili.Temporal.Cloud`'s certificate
loader, which calls `ClientOptionsFactory.BuildTls(TlsCertificateMaterial,
TemporalTlsOptions)` with the pre-resolved material.

### Environment-variable overrides

Environment variables override `appsettings.json` using the standard
double-underscore convention: `Temporal__TargetHost`, `Temporal__Namespace`,
`Temporal__ApiKey`, `Temporal__Tls__ClientCertPath`, and so on.

### `TemporalConfig` API

`TemporalConfig` is the programmatic entry point:

```csharp
using Kogoshvili.Temporal.Configuration;
using Temporalio.Client;

// Build appsettings.json + environment variables.
Microsoft.Extensions.Configuration.IConfigurationRoot config =
    TemporalConfig.BuildConfiguration(); // optional path overload

// Bind options from an existing configuration root.
TemporalConnectionOptions options = TemporalConfig.Load(config);

// Or load from the default file + env vars directly.
TemporalConnectionOptions options2 = TemporalConfig.Load();

// Map options to SDK connect options (applies TLS, retry, proxy, …).
TemporalClientConnectOptions connect = TemporalConfig.ToConnectOptions(options);

// Connect from options, or just connect with defaults.
ITemporalClient client = await TemporalConfig.ConnectAsync(options);
ITemporalClient client2 = await TemporalConfig.ConnectAsync();
```

`ClientOptionsFactory.Apply(TemporalClientConnectOptions,
TemporalConnectionOptions)` is the lower-level routine that mutates a connect
options instance in place from the resolved connection options.

### Certificate material

`ITlsCertificateSource` plugs in a new certificate source: register an
implementation in the service container and set `Tls:Source` to its `Name`.
`FileTlsCertificateSource` and `EnvironmentTlsCertificateSource` ship for the
`file` and `environment` sources, resolving to a `TlsCertificateMaterial`
record (`ServerRootCACert`, `ClientCert`, `ClientPrivateKey`). `TlsContent`
provides `Decode` (raw-PEM-or-base64 to bytes) and `EncodePem` (DER to PEM)
helpers for inline material.

Not affiliated with or endorsed by Temporal Technologies.
