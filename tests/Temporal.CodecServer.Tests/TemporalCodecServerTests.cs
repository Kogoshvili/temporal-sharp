using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Google.Protobuf;
using Kogoshvili.Temporal.Codec;
using Kogoshvili.Temporal.CodecServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Kogoshvili.Temporal.CodecServer.Tests;

public class TemporalCodecServerTests
{
    private const string Key = "test-key-test-key-test-key-test!";

    [Fact]
    public async Task EncodeThenDecode_RoundTripsOverHttp()
    {
        await using var app = await CreateAppAsync(new EncryptionCodec(Key));

        var plaintext = Payloads(new Payload
        {
            Metadata = { ["encoding"] = ByteString.CopyFromUtf8("json/plain") },
            Data = ByteString.CopyFromUtf8("hello"),
        });

        var encoded = await PostAsync(app, "/encode", plaintext);
        Assert.Single(encoded.Payloads_);
        Assert.Equal("binary/encrypted", encoded.Payloads_[0].Metadata["encoding"].ToStringUtf8());

        var decoded = await PostAsync(app, "/decode", encoded);
        Assert.Equal("hello", decoded.Payloads_[0].Data.ToStringUtf8());
    }

    [Fact]
    public async Task Decode_NamespaceRoute_Works()
    {
        await using var app = await CreateAppAsync(new EncryptionCodec(Key));

        var encoded = await PostAsync(app, "/encode", Payloads(new Payload
        {
            Metadata = { ["encoding"] = ByteString.CopyFromUtf8("json/plain") },
            Data = ByteString.CopyFromUtf8("namespaced"),
        }));

        var decoded = await PostAsync(app, "/my-namespace/decode", encoded);
        Assert.Equal("namespaced", decoded.Payloads_[0].Data.ToStringUtf8());
    }

    [Fact]
    public async Task Encode_NonJsonContentType_Returns415()
    {
        await using var app = await CreateAppAsync(new EncryptionCodec(Key));

        using var content = new StringContent("not json", Encoding.UTF8, "text/plain");
        var response = await app.Client.PostAsync("/encode", content);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task Encode_NoCodecRegistered_Returns500()
    {
        await using var app = await CreateAppAsync(codec: null);

        var response = await app.Client.PostAsync(
            "/encode",
            new StringContent(Json(Payloads(new Payload { Data = ByteString.CopyFromUtf8("x") })), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task Preflight_ReturnsCorsHeaders()
    {
        await using var app = await CreateAppAsync(new EncryptionCodec(Key));

        using var request = new HttpRequestMessage(HttpMethod.Options, "/decode");
        request.Headers.Add("Origin", "https://cloud.temporal.io");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await app.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("https://cloud.temporal.io", response.Headers.GetValues("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task PassAccessToken_WithoutToken_Returns401()
    {
        await using var app = await CreateAppAsync(
            new EncryptionCodec(Key),
            configure: o =>
            {
                o.Auth.PassAccessToken = true;
                o.Auth.RequireHttpsMetadata = false;
                o.Auth.Authority = "https://invalid.example.com";
            },
            withAuthMiddleware: true);

        var response = await app.Client.PostAsync(
            "/decode",
            new StringContent(Json(Payloads(new Payload())), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<TestApp> CreateAppAsync(
        IPayloadCodec? codec,
        Action<TemporalCodecServerOptions>? configure = null,
        bool withAuthMiddleware = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        if (codec is not null)
        {
            builder.Services.AddSingleton(codec);
        }

        builder.Services.AddTemporalCodecServer(configure);

        var app = builder.Build();
        app.UseCors();
        if (withAuthMiddleware)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.MapTemporalCodecServer();

        await app.StartAsync();
        return new TestApp(app, app.GetTestClient());
    }

    private static async Task<Payloads> PostAsync(TestApp app, string path, Payloads payloads)
    {
        var response = await app.Client.PostAsync(path, new StringContent(Json(payloads), Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        return JsonParser.Default.Parse<Payloads>(await response.Content.ReadAsStringAsync());
    }

    private static Payloads Payloads(Payload payload) => new() { Payloads_ = { payload } };

    private static string Json(Payloads payloads) => JsonFormatter.Default.Format(payloads);

    private sealed class TestApp : IAsyncDisposable
    {
        public TestApp(WebApplication app, HttpClient client)
        {
            App = app;
            Client = client;
        }

        public WebApplication App { get; }

        public HttpClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
        }
    }
}
