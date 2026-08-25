using Kogoshvili.Temporal.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Resolves TLS certificate material for the cloud sources
/// (<c>azureKeyVault</c>/<c>awsSecretsManager</c>) at startup and applies it to
/// the shared client connect options before the connection waiter and workers
/// start. The synchronous <c>file</c> and <c>environment</c> sources are handled
/// directly by <see cref="ClientOptionsFactory"/> and skipped here.
/// </summary>
public sealed class TemporalCertificateLoader : IHostedService
{
    private readonly IOptions<TemporalClientConnectOptions> connectOptions;
    private readonly IOptionsMonitor<TemporalOptions> temporalOptions;
    private readonly IEnumerable<ITlsCertificateSource> sources;
    private readonly ILogger<TemporalCertificateLoader> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporalCertificateLoader"/> class.
    /// </summary>
    public TemporalCertificateLoader(
        IOptions<TemporalClientConnectOptions> connectOptions,
        IOptionsMonitor<TemporalOptions> temporalOptions,
        IEnumerable<ITlsCertificateSource> sources,
        ILogger<TemporalCertificateLoader> logger)
    {
        this.connectOptions = connectOptions;
        this.temporalOptions = temporalOptions;
        this.sources = sources;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tls = temporalOptions.CurrentValue.Tls;
        if (tls is null || tls.Disabled || tls.Source is "file" or "environment")
        {
            return;
        }

        var source = sources.FirstOrDefault(s => s.Name == tls.Source)
            ?? throw new InvalidOperationException(
                $"No TLS certificate source named '{tls.Source}' is registered. " +
                "Register one via Kogoshvili.Temporal.Cloud (e.g. AddAzureKeyVaultCertificateSource).");

        var material = await source.ResolveAsync(tls, cancellationToken).ConfigureAwait(false);
        connectOptions.Value.Tls = ClientOptionsFactory.BuildTls(material, tls);
        logger.LogInformation("Resolved TLS certificate material from source '{Source}'.", tls.Source);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
