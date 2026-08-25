# Kogoshvili.Temporal.CodecServer

A ready-made HTTP codec server for the Temporal .NET SDK. It exposes the
`/encode` and `/decode` endpoints the Temporal Web UI and CLI use to encode and
decode workflow payloads, wrapping the same `IPayloadCodec` your workers use —
so encryption keys never leave your environment and the UI can still display
decoded data.

It is an ASP.NET Core library: call `AddTemporalCodecServer()` /
`MapTemporalCodecServer()` from any `WebApplication` (including the same app that
hosts your workers via `Kogoshvili.Temporal.Hosting`).

## Usage

```csharp
using Kogoshvili.Temporal.Codec;
using Kogoshvili.Temporal.CodecServer;

var builder = WebApplication.CreateBuilder(args);

// The codec the workers already use.
builder.Services.AddSingleton<IPayloadCodec>(new EncryptionCodec("test-key-test-key-test-key-test!"));

// Optional auth + CORS. See below.
builder.Services.AddTemporalCodecServer();

var app = builder.Build();

app.UseCors();            // enables the codec-server CORS policy (for the Web UI)
app.UseAuthentication();
app.UseAuthorization();

app.MapTemporalCodecServer();

app.Run();
```

The endpoints accept the Temporal `Payloads` protobuf-as-JSON envelope and follow
the [codec server protocol](https://github.com/temporalio/samples-go/tree/main/codec-server#codec-server-protocol):
`POST /encode` and `POST /decode`, plus `POST /{namespace}/encode` /
`POST /{namespace}/decode` for namespace-scoped deployments. The `X-Namespace`
header is left available to the codec.

## CORS

`AddTemporalCodecServer` registers a CORS policy allowing the Temporal Cloud UI
(`https://cloud.temporal.io`) and the local dev UI (`http://localhost:8080`,
`http://localhost:8233`), with `X-Namespace`, `Content-Type`, and `Authorization`
headers. Override with `AllowedOrigins` / `AllowCredentials`.

## Authentication

Two modes, matching the Temporal Web UI's codec-server options:

- **Pass access token** (`Auth:PassAccessToken = true`) — validates the JWT the
  UI forwards in the `Authorization` header against the OIDC provider's JWKS.
  Defaults to Temporal Cloud (`Authority: https://login.tmprl.cloud`,
  `Audience: https://saas-api.tmprl.cloud`).
- **Include cross-origin credentials** (`Auth:IncludeCrossOriginCredentials = true`)
  — the codec server keeps its own session via an OAuth2 authorization-code flow,
  so opening the Temporal UI redirects through your IdP and back (a login route
  is mapped at `/codec/login`). Set `OidcAuthority`, `ClientId`, and
  `ClientSecret`.

```csharp
builder.Services.AddTemporalCodecServer(o =>
{
    o.Auth.PassAccessToken = true;                    // validate the UI's JWT
    // o.Auth.IncludeCrossOriginCredentials = true;   // or your own login flow
    // o.Auth.OidcAuthority = "https://login.example.com";
    // o.Auth.ClientId = "...";
});
```

Because the codec server can decode sensitive data, run it over HTTPS and
restrict ingress (VPN or `localhost`) unless you have enabled authentication.

Not affiliated with or endorsed by Temporal Technologies.
