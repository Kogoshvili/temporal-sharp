using Google.Protobuf;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Kogoshvili.Temporal.CodecServer;

/// <summary>
/// Registers and maps the Temporal codec-server HTTP endpoints (<c>/encode</c>
/// and <c>/decode</c>) that the Temporal Web UI and CLI call to encode/decode
/// payloads. The endpoints wrap the <see cref="IPayloadCodec"/> registered in
/// the service container, so a worker and its codec server share the same codec.
/// </summary>
public static class TemporalCodecServerExtensions
{
    internal const string CorsPolicyName = "TemporalCodecServer";
    internal const string AuthPolicyName = "TemporalCodecServer";

    /// <summary>
    /// Registers codec-server configuration, CORS, and (optionally) the JWT-bearer
    /// and OAuth2 authorization-code authentication schemes. Call this in
    /// <c>ConfigureServices</c> before <see cref="MapTemporalCodecServer"/>.
    /// </summary>
    public static IServiceCollection AddTemporalCodecServer(
        this IServiceCollection services,
        Action<TemporalCodecServerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new TemporalCodecServerOptions();
        configure?.Invoke(options);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddCors(cors => cors.AddPolicy(CorsPolicyName, policy =>
        {
            policy.WithOrigins(options.AllowedOrigins)
                .WithMethods(HttpMethods.Post, HttpMethods.Options)
                .WithHeaders("X-Namespace", "Content-Type", "Authorization");

            if (options.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        }));

        var schemes = new List<string>();
        string? defaultScheme = null;

        if (options.Auth.IncludeCrossOriginCredentials)
        {
            defaultScheme ??= CookieAuthenticationDefaults.AuthenticationScheme;
            schemes.Add(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        if (options.Auth.PassAccessToken)
        {
            defaultScheme ??= JwtBearerDefaults.AuthenticationScheme;
            schemes.Add(JwtBearerDefaults.AuthenticationScheme);
        }

        if (defaultScheme is not null)
        {
            var auth = services.AddAuthentication(defaultScheme);

            if (options.Auth.PassAccessToken)
            {
                auth.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, bearer =>
                {
                    bearer.Authority = options.Auth.Authority;
                    bearer.Audience = options.Auth.Audience;
                    bearer.RequireHttpsMetadata = options.Auth.RequireHttpsMetadata;
                });
            }

            if (options.Auth.IncludeCrossOriginCredentials)
            {
                auth.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, cookie =>
                {
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SameSite = SameSiteMode.None;
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    cookie.LoginPath = options.LoginPath;
                    cookie.LogoutPath = options.LogoutPath;
                    cookie.ExpireTimeSpan = TimeSpan.FromHours(8);
                })
                .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, oidc =>
                {
                    oidc.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    oidc.Authority = options.Auth.OidcAuthority;
                    oidc.ClientId = options.Auth.ClientId;
                    oidc.ClientSecret = options.Auth.ClientSecret;
                    oidc.ResponseType = "code";
                    oidc.SaveTokens = true;
                    oidc.Scope.Add("openid");
                    oidc.Scope.Add("profile");
                });
            }

            services.AddAuthorization(authz => authz.AddPolicy(AuthPolicyName, policy =>
                policy.AddAuthenticationSchemes(schemes.ToArray()).RequireAuthenticatedUser()));
        }

        return services;
    }

    /// <summary>
    /// Maps the codec-server endpoints. The <c>/encode</c> and <c>/decode</c>
    /// routes are also mounted under a namespace segment (<c>/&lt;namespace&gt;/encode</c>)
    /// to match the URL shapes the Temporal Web UI and CLI send.
    /// </summary>
    public static IEndpointRouteBuilder MapTemporalCodecServer(
        this IEndpointRouteBuilder endpoints,
        TemporalCodecServerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        options ??= endpoints.ServiceProvider.GetService<IOptions<TemporalCodecServerOptions>>()?.Value
            ?? new TemporalCodecServerOptions();

        MapProtected(endpoints, "/encode", EncodeAsync, options);
        MapProtected(endpoints, "/decode", DecodeAsync, options);
        MapProtected(endpoints, "/{namespace}/encode", EncodeAsync, options);
        MapProtected(endpoints, "/{namespace}/decode", DecodeAsync, options);

        if (options.Auth.IncludeCrossOriginCredentials)
        {
            endpoints.MapGet(options.LoginPath, LoginAsync);
            endpoints.MapPost(options.LoginPath, LoginAsync);
            endpoints.MapGet(options.LogoutPath, LogoutAsync);
            endpoints.MapPost(options.LogoutPath, LogoutAsync);
        }

        return endpoints;
    }

    private static IEndpointConventionBuilder MapProtected(
        IEndpointRouteBuilder endpoints,
        string pattern,
        Delegate handler,
        TemporalCodecServerOptions options)
    {
        var builder = endpoints.MapPost(pattern, handler).RequireCors(CorsPolicyName);

        if (options.Auth.PassAccessToken || options.Auth.IncludeCrossOriginCredentials)
        {
            builder.RequireAuthorization(AuthPolicyName);
        }

        return builder;
    }

    private static Task<IResult> EncodeAsync(HttpContext context) =>
        ApplyCodecAsync(context, (codec, payloads) => codec.EncodeAsync(payloads));

    private static Task<IResult> DecodeAsync(HttpContext context) =>
        ApplyCodecAsync(context, (codec, payloads) => codec.DecodeAsync(payloads));

    private static async Task<IResult> ApplyCodecAsync(
        HttpContext context,
        Func<IPayloadCodec, IReadOnlyCollection<Payload>, Task<IReadOnlyCollection<Payload>>> apply)
    {
        if (!context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        if (context.RequestServices.GetService<IPayloadCodec>() is not { } codec)
        {
            return Results.Problem(
                "No IPayloadCodec is registered in the service container.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        Payloads incoming;
        using (var reader = new StreamReader(context.Request.Body))
        {
            incoming = JsonParser.Default.Parse<Payloads>(await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false));
        }

        var outgoing = new Payloads { Payloads_ = { await apply(codec, incoming.Payloads_).ConfigureAwait(false) } };

        return Results.Text(JsonFormatter.Default.Format(outgoing), "application/json");
    }

    private static async Task LoginAsync(HttpContext context)
    {
        await context.ChallengeAsync(
            OpenIdConnectDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = "/" }).ConfigureAwait(false);
    }

    private static async Task LogoutAsync(HttpContext context)
    {
        await context.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new AuthenticationProperties { RedirectUri = "/" }).ConfigureAwait(false);
    }
}
