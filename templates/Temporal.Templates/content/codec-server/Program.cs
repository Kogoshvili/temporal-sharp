using Kogoshvili.Temporal.Codec;
using Kogoshvili.Temporal.CodecServer;
#if (UseVault)
using Kogoshvili.Temporal.Cloud;
#endif
using Temporalio.Converters;

var builder = WebApplication.CreateBuilder(args);

// The payload codec the workers use — encryption and/or claim-check.
builder.Services.AddSingleton<IPayloadCodec>(sp => BuildCodec(builder.Configuration, sp));

// The codec-server endpoints, with the selected authentication mode.
builder.Services.AddTemporalCodecServer(options =>
{
#if (UsePassToken)
    options.Auth.PassAccessToken = true;
#endif
#if (UseCrossOrigin)
    options.Auth.IncludeCrossOriginCredentials = true;
    options.Auth.OidcAuthority = builder.Configuration["CodecServer:OidcAuthority"];
    options.Auth.ClientId = builder.Configuration["CodecServer:ClientId"];
    options.Auth.ClientSecret = builder.Configuration["CodecServer:ClientSecret"];
#endif
});

#if (UseAzure)
builder.Services.AddAzureKeyVaultSecretResolver(builder.Configuration["CodecServer:VaultUri"]!);
#endif
#if (UseAws)
builder.Services.AddAwsSecretsManagerSecretResolver(builder.Configuration["CodecServer:Region"]!);
#endif

var app = builder.Build();

app.UseCors();
#if (UseAuth)
app.UseAuthentication();
app.UseAuthorization();
#endif

app.MapTemporalCodecServer();

await app.RunAsync();

static IPayloadCodec BuildCodec(Microsoft.Extensions.Configuration.IConfiguration config, IServiceProvider services)
{
    var codecs = new List<IPayloadCodec>();

#if (EnableEncryption)
    var encryption = new EncryptionCodec(
        ResolveKey(config, services),
        keyId: config["CodecServer:KeyId"] ?? "default");
    codecs.Add(encryption);
#endif

#if (EnableClaimCheck)
    codecs.Add(new ClaimCheckCodec(
        new FileSystemClaimCheckStore(config["CodecServer:ClaimCheckDirectory"] ?? "claim-check"),
        thresholdBytes: int.TryParse(config["CodecServer:ThresholdBytes"], out var threshold) ? threshold : 1024 * 1024));
#endif

    return codecs.Count switch
    {
        1 => codecs[0],
        _ => new CompositePayloadCodec(codecs),
    };
}

static byte[] ResolveKey(Microsoft.Extensions.Configuration.IConfiguration config, IServiceProvider services)
{
#if (UseVault)
    var resolver = services.GetRequiredService<ISecretResolver>();
    var secret = resolver.ResolveAsync(config["CodecServer:SecretId"]!).GetAwaiter().GetResult();
    return config["CodecServer:Encoding"] == "base64"
        ? Convert.FromBase64String(secret)
        : System.Text.Encoding.ASCII.GetBytes(secret);
#else
    var key = config["CodecServer:Key"]
        ?? throw new InvalidOperationException("CodecServer:Key is required when the key source is 'config'.");
    return config["CodecServer:Encoding"] == "base64"
        ? Convert.FromBase64String(key)
        : System.Text.Encoding.ASCII.GetBytes(key);
#endif
}
