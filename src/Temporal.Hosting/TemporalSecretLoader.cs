using Kogoshvili.Temporal.Codec;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Converters;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Resolves vault-backed payload-codec material (an encryption key and cloud
/// claim-check store credentials) at startup and applies it to the shared
/// client's <see cref="DataConverter"/> before workers connect. The synchronous
/// sources (inline key and filesystem store) are handled by
/// <see cref="TemporalDataConverterFactory"/> at registration and skipped here.
/// </summary>
public sealed class TemporalSecretLoader : IHostedService
{
    private readonly TemporalClientConnectOptions connectOptions;
    private readonly IOptions<TemporalOptions> temporalOptions;
    private readonly IEnumerable<ISecretResolver> secretResolvers;
    private readonly IEnumerable<IClaimCheckStoreFactory> storeFactories;
    private readonly ILogger<TemporalSecretLoader> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporalSecretLoader"/> class.
    /// </summary>
    public TemporalSecretLoader(
        TemporalClientConnectOptions connectOptions,
        IOptions<TemporalOptions> temporalOptions,
        IEnumerable<ISecretResolver> secretResolvers,
        IEnumerable<IClaimCheckStoreFactory> storeFactories,
        ILogger<TemporalSecretLoader> logger)
    {
        this.connectOptions = connectOptions;
        this.temporalOptions = temporalOptions;
        this.secretResolvers = secretResolvers;
        this.storeFactories = storeFactories;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var dataConverter = temporalOptions.Value.DataConverter;
        if (!RequiresResolution(dataConverter))
        {
            return;
        }

        var codecs = new List<IPayloadCodec>();

        if (dataConverter.Encryption.Enabled)
        {
            var key = dataConverter.Encryption.Source == "config"
                ? System.Text.Encoding.ASCII.GetBytes(dataConverter.Encryption.Key!)
                : TemporalDataConverterFactory.DecodeKey(
                    await ResolveSecretAsync(dataConverter.Encryption.Source, dataConverter.Encryption.SecretId!, cancellationToken).ConfigureAwait(false),
                    dataConverter.Encryption.Encoding);

            codecs.Add(new EncryptionCodec(key, dataConverter.Encryption.KeyId));
        }

        if (dataConverter.ClaimCheck.Enabled)
        {
            codecs.Add(new ClaimCheckCodec(
                await BuildStoreAsync(dataConverter.ClaimCheck, cancellationToken).ConfigureAwait(false),
                dataConverter.ClaimCheck.ThresholdBytes));
        }

        connectOptions.DataConverter = DataConverter.Default with { PayloadCodec = TemporalDataConverterFactory.Compose(codecs) };
        logger.LogInformation("Resolved vault-backed payload codec material.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool RequiresResolution(TemporalDataConverterOptions options) =>
        (options.Encryption.Enabled && options.Encryption.Source != "config")
        || (options.ClaimCheck.Enabled && options.ClaimCheck.Store != "filesystem");

    private async Task<IClaimCheckStore> BuildStoreAsync(TemporalClaimCheckCodecOptions options, CancellationToken cancellationToken)
    {
        if (options.Store == "filesystem")
        {
            return new FileSystemClaimCheckStore(options.Directory);
        }

        var factory = storeFactories.FirstOrDefault(f => f.Name == options.Store)
            ?? throw new InvalidOperationException(
                $"No claim-check store factory named '{options.Store}' is registered. " +
                "Register one via Kogoshvili.Temporal.Cloud (e.g. AddAzureBlobClaimCheckStore).");

        string? connectionString = options.ConnectionString;
        if (options.Store == "azureBlob" && options.ConnectionStringSecretId is not null)
        {
            connectionString = await ResolveSecretAsync("azureKeyVault", options.ConnectionStringSecretId, cancellationToken).ConfigureAwait(false);
        }

        string? accessKey = null;
        string? secretKey = null;
        string? sessionToken = null;
        if (options.Store == "s3" && options.AccessKeySecretId is not null)
        {
            accessKey = await ResolveSecretAsync("awsSecretsManager", options.AccessKeySecretId, cancellationToken).ConfigureAwait(false);
            secretKey = await ResolveSecretAsync("awsSecretsManager", options.SecretKeySecretId!, cancellationToken).ConfigureAwait(false);
            if (options.SessionTokenSecretId is not null)
            {
                sessionToken = await ResolveSecretAsync("awsSecretsManager", options.SessionTokenSecretId, cancellationToken).ConfigureAwait(false);
            }
        }

        var settings = new ClaimCheckStoreSettings
        {
            Directory = options.Directory,
            AccountUri = options.AccountUri,
            ConnectionString = connectionString,
            ContainerName = options.ContainerName,
            Region = options.Region,
            BucketName = options.BucketName,
            AccessKey = accessKey,
            SecretKey = secretKey,
            SessionToken = sessionToken,
            RoleArn = options.RoleArn,
            RoleSessionName = options.RoleSessionName,
        };

        return factory.Create(settings);
    }

    private async Task<string> ResolveSecretAsync(string source, string secretId, CancellationToken cancellationToken)
    {
        var resolver = secretResolvers.FirstOrDefault(r => r.Name == source)
            ?? throw new InvalidOperationException(
                $"No secret resolver named '{source}' is registered. " +
                "Register one via Kogoshvili.Temporal.Cloud (e.g. AddAzureKeyVaultSecretResolver).");

        return await resolver.ResolveAsync(secretId, cancellationToken).ConfigureAwait(false);
    }
}
