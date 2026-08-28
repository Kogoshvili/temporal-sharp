# Kogoshvili.Temporal.CodecServer

A ready-made HTTP codec server for the Temporal .NET SDK. It exposes the
`/encode` and `/decode` endpoints the Temporal Web UI and CLI use to encode and
decode workflow payloads, wrapping the same `IPayloadCodec` your workers use —
so encryption keys never leave your environment and the UI can still display
decoded data.

It is an ASP.NET Core library: call `AddTemporalCodecServer()` /
`MapTemporalCodecServer()` from any `WebApplication` (including the same app that
hosts your workers via `Kogoshvili.Temporal.Hosting`).

## Minimal setup

Register the `IPayloadCodec` your workers already use, then add and map the
codec server. No configuration is required — CORS and no-auth defaults apply.

```csharp
using Kogoshvili.Temporal.Codec;
using Kogoshvili.Temporal.CodecServer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IPayloadCodec>(
    new EncryptionCodec("test-key-test-key-test-key-test!"));

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
header is accepted (and permitted by CORS) but not consumed by the codec.

## Configuration

`AddTemporalCodecServer` takes an options delegate for CORS and authentication.

```csharp
builder.Services.AddTemporalCodecServer(o =>
{
    o.AllowedOrigins = new[] { "https://my.ui.example.com" };
    o.AllowCredentials = true;
});
```

CORS defaults to the Temporal Cloud UI plus the common local dev servers, with
`X-Namespace`, `Content-Type`, and `Authorization` headers allowed:

```csharp
// Defaults
o.AllowedOrigins = new[]
{
    "https://cloud.temporal.io",
    "http://localhost:8080",
    "http://localhost:8233",
};
o.AllowCredentials = true;   // required for the cross-origin-credentials auth mode
```

`AllowedOrigins` must be an explicit list (no wildcard) when
`AllowCredentials` is `true`. The same options can instead be passed to
`MapTemporalCodecServer(options)`, which wins over any configured options.

Enable authentication with a single flag plus the relevant OIDC settings:

```csharp
builder.Services.AddTemporalCodecServer(o =>
{
    o.Auth.PassAccessToken = true;                    // validate the UI's JWT
    // o.Auth.IncludeCrossOriginCredentials = true;   // or your own login flow
});
```

## Full configuration

Two auth modes, matching the Temporal Web UI's codec-server options.

**Pass access token** (`Auth.PassAccessToken = true`) validates the JWT the UI
forwards in the `Authorization` header against the OIDC provider's JWKS. It
defaults to Temporal Cloud, so no `Authority`/`Audience` are needed for Cloud.

```csharp
builder.Services.AddTemporalCodecServer(o =>
{
    o.Auth.PassAccessToken = true;
    o.Auth.Authority = "https://login.tmprl.cloud";          // default
    o.Auth.Audience = "https://saas-api.tmprl.cloud";        // default
    o.Auth.RequireHttpsMetadata = true;                      // default; set false for localhost HTTP
});
```

**Include cross-origin credentials** (`Auth.IncludeCrossOriginCredentials = true`)
gives the codec server its own session via an OAuth2 authorization-code flow.
Opening the Temporal UI redirects through your IdP and back; login and logout
routes are mapped at `LoginPath` (`/codec/login`) and `LogoutPath`
(`/codec/logout`). Requires `AllowCredentials = true` (the default) and a
`ClientId`/`ClientSecret` registered with your IdP.

```csharp
builder.Services.AddTemporalCodecServer(o =>
{
    o.Auth.IncludeCrossOriginCredentials = true;
    o.Auth.OidcAuthority = "https://login.example.com";
    o.Auth.ClientId = "my-codec-server";
    o.Auth.ClientSecret = "...";

    o.AllowCredentials = true;
    o.LoginPath = "/codec/login";    // default
    o.LogoutPath = "/codec/logout";  // default
});
```

The two modes can be combined; when both are set, either an accepted JWT or a
valid session cookie satisfies the endpoint's authorization policy.

Security notes:

- The codec server can decode sensitive data, so run it over HTTPS
  (`RequireHttpsMetadata = true` is the default) and restrict ingress (VPN or
  `localhost`) unless you have enabled authentication.
- In the cross-origin-credentials mode, the session cookie is sent with
  `HttpOnly`, `SameSite=None`, and `Secure=Always`, and expires after 8 hours.

Not affiliated with or endorsed by Temporal Technologies.
