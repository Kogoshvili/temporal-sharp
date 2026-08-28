# Kogoshvili.Temporal.Templates

`dotnet new` templates for the Kogoshvili.Temporal tool suite.

## Install

```sh
dotnet new install Kogoshvili.Temporal.Templates
```

## Templates

### `temporal-codec-server`

A ready-to-run HTTP codec server for the Temporal Web UI and CLI. Generates an
ASP.NET Core app that maps `/encode` and `/decode` (plus the namespace-scoped
routes) over the same `IPayloadCodec` your workers use.

```sh
dotnet new temporal-codec-server -o MyCodecServer
dotnet run --project MyCodecServer
```

| Parameter | Values | Default | Purpose |
| --- | --- | --- | --- |
| `--codec` | `encryption` · `claim-check` · `both` | `both` | Which payload codec(s) to wire. |
| `--auth` | `none` · `pass-token` · `cross-origin` | `none` | Authentication mode for the endpoints. |
| `--keySource` | `config` · `azureKeyVault` · `awsSecretsManager` | `config` | Where the encryption key comes from. |
| `--port` | integer | `5000` | HTTP listen port. |

The encryption key, key id, claim-check directory/threshold, and auth endpoints
are all read from `appsettings.json` under the `CodecServer` section. When
`--keySource` is `azureKeyVault` or `awsSecretsManager`, the generated project
references `Kogoshvili.Temporal.Cloud` and resolves the key from the vault at
startup.

Not affiliated with or endorsed by Temporal Technologies.
