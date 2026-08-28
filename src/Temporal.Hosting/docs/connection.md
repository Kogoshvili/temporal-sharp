# Connection & TLS

## Connection retry and startup wait

`RpcRetry` maps onto the SDK's connection-level `RpcRetryOptions`, controlling
the retry policy for server calls (`InitialInterval`, `Multiplier`, `MaxRetries`,
and so on). Set it to `null` (the default) to keep the SDK defaults.

`ConnectionWait` makes the starter wait for the server to be reachable before
workers poll: on startup a hosted service connects the shared lazy client,
retrying with exponential backoff (`InitialDelay` → `MaxDelay`) until success or
`Timeout` (set `Timeout` to `null` to retry indefinitely). It is enabled by
default and ignored when the test server is used.

## Configuration reload

Options are bound through `IOptionsMonitor<TemporalOptions>`, so the options
**value** reflects `appsettings.json` changes, and
`IValidateOptions<TemporalOptions>` re-validates on every reload — an invalid
new value is rejected with `OptionsValidationException` on next access.

However, reload is **validate-only**, not *apply*: the client connection,
workers, codecs, and runtime are constructed once from a snapshot at
registration/startup and are **not** reconfigured when the value changes. The
Temporal .NET SDK treats connection and worker options as snapshots too — the
hosted `TemporalWorkerService` clones its options once at construction and never
subscribes to changes, and the only runtime-mutable connection properties are
`ApiKey`, `RpcMetadata`, and `RpcBinaryMetadata`. Exceptions:

- `TemporalHealthCheck` reads the current value per invocation, so
  `HealthChecks:Enabled` toggles live.
- `ActivityOptions` presets are seeded once and deliberately not live-reloaded
  (see the activity-options doc).

True live reload (reconnecting the client and restarting workers on change) is
not implemented.

## Connection transport options

Beyond `RpcRetry`, the remaining connection-level SDK knobs are exposed from
configuration (each `null` = leave the SDK default untouched):

| Key | SDK property |
| --- | --- |
| `Temporal:KeepAlive` (`Interval`, `Timeout`) | `KeepAliveOptions` |
| `Temporal:HttpConnectProxy` (`TargetHost`, `Username`, `Password`) | `HttpConnectProxyOptions` |
| `Temporal:DnsLoadBalancing` (`ResolutionInterval`) | `DnsLoadBalancingOptions` |
| `Temporal:GrpcCompression:Mode` (`"gzip"` or `"none"`) | `GrpcCompression` |

## TLS sources

`Temporal:Tls:Source` selects where client certificates come from:

- **`file`** (default) — PEM files at `Tls:ClientCertPath`,
  `Tls:ClientPrivateKeyPath`, and `Tls:ServerRootCACertPath`.
- **`environment`** — inline `Tls:ClientCert` / `Tls:ClientPrivateKey` /
  `Tls:ServerRootCACert` strings (base64 or raw PEM), typically injected as
  environment variables (`Temporal__Tls__ClientCert=…`).
- **`azureKeyVault`** / **`awsSecretsManager`** — fetched asynchronously at
  startup by `TemporalCertificateLoader` before the connection waiter and
  workers start. Register the source from `Kogoshvili.Temporal.Cloud` and
  configure its section:

```csharp
builder.Services.AddAzureKeyVaultCertificateSource(); // or AddAwsSecretsManagerCertificateSource()
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

For AWS:

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

`Tls:Disabled` (default `false`) skips TLS entirely, and `Tls:Domain` sets the
expected server hostname/domain. Azure Key Vault stores certificates as PFX;
`AzureKeyVaultCertificateSource` converts them to the PEM form the SDK requires
(`Password` is the optional PFX password). The `file` and `environment` sources
are resolved synchronously by `ClientOptionsFactory`, so they also work with the
`temporal-sharp` CLI and the testing harness.

## In-process test server

`TestServer:Enabled` runs an in-process Temporal dev server instead of
connecting to a real one (`TestServer:Port`, `0` for an ephemeral port).
`ConnectionWait` is skipped automatically when the test server is used.
