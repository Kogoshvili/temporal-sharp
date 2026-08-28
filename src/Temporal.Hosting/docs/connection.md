# Connection & TLS

`Kogoshvili.Temporal.Hosting` reaches a Temporal server through the shared
`Temporal` connection section, covering the target host, RPC retry, transport
tuning, TLS certificate sources, startup connection-wait, and an optional
in-process test server.

## Minimal setup

The smallest working registration binds the `Temporal` section and specifies
only a target host; every other block is optional and defaults to the SDK's
behavior.

```csharp
using Kogoshvili.Temporal.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddTemporal(builder.Configuration)
    .AddTemporalWorker("my-task-queue")
    .AddDiscoveredTypes();

await builder.Build().RunAsync();
```

```jsonc
{
  "Temporal": {
    "TargetHost": "localhost:7233"
  }
}
```

`TargetHost` defaults to `localhost:7233` and `Namespace` defaults to `default`,
so this minimal file is optional. `ConnectionWait` is enabled by default and
waits for the server to be reachable before workers poll.

## Configuration

The connection options are the shared transport-level subset of `TemporalOptions`
(`Kogoshvili.Temporal.Configuration.TemporalConnectionOptions`). Add a namespace,
API key, and RPC retry policy:

```jsonc
{
  "Temporal": {
    "TargetHost": "localhost:7233",
    "Namespace": "default",
    "ApiKey": "my-api-key",
    "RpcRetry": {
      "InitialInterval": "00:00:00.100",
      "RandomizationFactor": 0.2,
      "Multiplier": 1.5,
      "MaxInterval": "00:00:05",
      "MaxElapsedTime": "00:00:10",
      "MaxRetries": 10
    }
  }
}
```

`RpcRetry` maps to the SDK's `RpcRetryOptions`; its defaults match the SDK, so
setting only a subset leaves the rest untouched, and a `null` section keeps the
SDK defaults entirely.

Transport options follow the same pattern — each `null` section leaves the SDK
default unchanged:

```jsonc
{
  "Temporal": {
    "TargetHost": "localhost:7233",
    "KeepAlive": {
      "Interval": "00:00:30",
      "Timeout": "00:00:15"
    },
    "HttpConnectProxy": {
      "TargetHost": "proxy.example.com:8080",
      "Username": "user",
      "Password": "pass"
    },
    "DnsLoadBalancing": {
      "ResolutionInterval": "00:00:30"
    },
    "GrpcCompression": {
      "Mode": "gzip"
    }
  }
}
```

`KeepAlive` controls HTTP/2 keep-alive pings; `HttpConnectProxy` routes the
connection through an HTTP CONNECT proxy (basic auth only when both `Username`
and `Password` are set); `DnsLoadBalancing` periodically re-resolves the target
and load balances across addresses; `GrpcCompression:Mode` is `"gzip"` (the SDK
default) or `"none"`.

`ConnectionWait` makes the starter wait for the server before workers poll. On
startup a hosted service connects the shared lazy client, retrying with
exponential backoff (`InitialDelay` doubled up to `MaxDelay`) until success or
`Timeout` elapses. It is enabled by default and ignored when the test server is
used.

```jsonc
{
  "Temporal": {
    "TargetHost": "localhost:7233",
    "ConnectionWait": {
      "Enabled": true,
      "Timeout": "00:01:00",
      "InitialDelay": "00:00:01",
      "MaxDelay": "00:00:15"
    }
  }
}
```

Set `Timeout` to `null` to retry indefinitely.

## Full configuration

### TLS sources

`Temporal:Tls:Source` selects where client certificates come from. `Tls` is
`null` by default (no TLS). `Tls:Disabled` (default `false`) explicitly skips
TLS, and `Tls:Domain` sets the expected server hostname/domain.

- **`file`** (default) — PEM files read from `Tls:ServerRootCACertPath`,
  `Tls:ClientCertPath`, and `Tls:ClientPrivateKeyPath`. Resolved synchronously by
  `ClientOptionsFactory`, so it also works with the `temporal-sharp` CLI and the
  testing harness.

```jsonc
{
  "Temporal": {
    "Tls": {
      "Source": "file",
      "ServerRootCACertPath": "/etc/temporal/ca.pem",
      "ClientCertPath": "/etc/temporal/client.pem",
      "ClientPrivateKeyPath": "/etc/temporal/client.key"
    }
  }
}
```

- **`environment`** — inline `Tls:ServerRootCACert` / `Tls:ClientCert` /
  `Tls:ClientPrivateKey` strings (base64 or raw PEM), typically injected as
  environment variables. Also resolved synchronously.

```jsonc
{
  "Temporal": {
    "Tls": {
      "Source": "environment",
      "ServerRootCACert": "LS0tLS1CRUdJTi...",
      "ClientCert": "LS0tLS1CRUdJTi...",
      "ClientPrivateKey": "LS0tLS1CRUdJTi..."
    }
  }
}
```

- **`azureKeyVault`** / **`awsSecretsManager`** — fetched asynchronously at
  startup by `TemporalCertificateLoader`, before the connection waiter and
  workers start. Register the source from `Kogoshvili.Temporal.Cloud`:

```csharp
builder.Services.AddAzureKeyVaultCertificateSource(); // or AddAwsSecretsManagerCertificateSource()
```

```jsonc
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

Azure Key Vault stores the certificate as a PFX secret and converts it to PEM at
startup (`Password` is the optional PFX password). For AWS:

```jsonc
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

### In-process test server

`TestServer:Enabled` runs an in-process Temporal dev server instead of connecting
to a real one, mirroring `spring.temporal.test-server.enabled`. `Port` defaults
to `0`, asking the OS for an ephemeral free port that is shared with the lazy
client after startup; set a concrete port to pin the server. `ConnectionWait` is
skipped automatically when the test server is used, and the cloud TLS loader is
not registered.

```jsonc
{
  "Temporal": {
    "TestServer": {
      "Enabled": true,
      "Port": 0
    }
  }
}
```

### Reference

| Key | Type | Default | Notes |
| --- | --- | --- | --- |
| `TargetHost` | string | `localhost:7233` | `host:port` of the server |
| `Namespace` | string | `default` | Default/fallback namespace |
| `ApiKey` | string | `null` | Sent on every call |
| `Tls:Disabled` | bool | `false` | Explicitly skip TLS |
| `Tls:Domain` | string | `null` | Expected server hostname/domain |
| `Tls:Source` | string | `file` | `file`, `environment`, `azureKeyVault`, `awsSecretsManager` |
| `RpcRetry:InitialInterval` | timespan | `00:00:00.100` | |
| `RpcRetry:RandomizationFactor` | float | `0.2` | |
| `RpcRetry:Multiplier` | float | `1.5` | |
| `RpcRetry:MaxInterval` | timespan | `00:00:05` | |
| `RpcRetry:MaxElapsedTime` | timespan? | `00:00:10` | `null` for no limit |
| `RpcRetry:MaxRetries` | int | `10` | |
| `KeepAlive:Interval` | timespan | `00:00:30` | |
| `KeepAlive:Timeout` | timespan | `00:00:15` | |
| `HttpConnectProxy:TargetHost` | string | `null` | `null` connects directly |
| `HttpConnectProxy:Username` | string | `null` | |
| `HttpConnectProxy:Password` | string | `null` | |
| `DnsLoadBalancing:ResolutionInterval` | timespan | `00:00:30` | `null` section disables |
| `GrpcCompression:Mode` | string | `gzip` | `gzip` or `none` |
| `ConnectionWait:Enabled` | bool | `true` | |
| `ConnectionWait:Timeout` | timespan? | `00:01:00` | `null` retries indefinitely |
| `ConnectionWait:InitialDelay` | timespan | `00:00:01` | |
| `ConnectionWait:MaxDelay` | timespan | `00:00:15` | |
| `TestServer:Enabled` | bool | `false` | |
| `TestServer:Port` | int | `0` | `0` = ephemeral |

### Precedence and reload behavior

Configuration is bound through `IOptionsMonitor<TemporalOptions>` and
`Temporal__*` environment variables override the file. Reload is
**validate-only**, not *apply*: an invalid new value is rejected with
`OptionsValidationException` on next access, but the client connection, workers,
codecs, and runtime are constructed once from a snapshot at startup and are not
reconfigured when the value changes. The `HealthChecks:Enabled` toggle is read
per invocation and applies live; `ActivityOptions` presets are seeded once and
not reloaded. True live reload (reconnecting the client and restarting workers)
is not implemented.

When the `Tls:Source` is `file` or `environment`, the file-path and inline
content properties are mutually exclusive. A cloud source must be registered via
`Kogoshvili.Temporal.Cloud`; otherwise startup fails with
`InvalidOperationException`.
