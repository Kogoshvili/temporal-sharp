using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Testing;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Hosted service that starts and owns an in-process Temporal dev server when
/// <see cref="TemporalTestServerOptions.Enabled"/> is <c>true</c>. Registered
/// before any worker service so the dev server is listening before workers
/// connect. The resolved <c>host:port</c> is written back to the shared
/// <see cref="TemporalClientConnectOptions"/> consumed by the lazy client.
/// </summary>
public sealed class TemporalTestServerService : IHostedService
{
    private readonly IOptionsMonitor<TemporalOptions> options;
    private readonly TemporalClientConnectOptions connectOptions;
    private readonly ILogger<TemporalTestServerService> logger;
    private WorkflowEnvironment? environment;

    /// <summary>Initializes the test-server service.</summary>
    /// <param name="options">The temporal options snapshot, used to read the test-server toggle and port.</param>
    /// <param name="connectOptions">The shared connect options whose target host is rewritten to the running server.</param>
    /// <param name="logger">The logger.</param>
    public TemporalTestServerService(
        IOptionsMonitor<TemporalOptions> options,
        TemporalClientConnectOptions connectOptions,
        ILogger<TemporalTestServerService> logger)
    {
        this.options = options;
        this.connectOptions = connectOptions;
        this.logger = logger;
    }

    /// <summary>
    /// Starts the in-process dev server (unless disabled) and writes the
    /// resolved <c>host:port</c> back into the shared connect options.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.CurrentValue.TestServer.Enabled)
        {
            return;
        }

        // Port 0 asks the OS for an ephemeral free port.
        var port = options.CurrentValue.TestServer.Port;
        var environmentOptions = new WorkflowEnvironmentStartLocalOptions
        {
            TargetHost = port == 0 ? "127.0.0.1:0" : $"127.0.0.1:{port}",
            Namespace = options.CurrentValue.Namespace,
        };

        logger.LogInformation("Starting Temporal test server on {TargetHost}", environmentOptions.TargetHost);
        environment = await WorkflowEnvironment.StartLocalAsync(environmentOptions).ConfigureAwait(false);

        // The dev server picked a concrete port; share it with the lazy client
        // so it connects to the running server on first use.
        connectOptions.TargetHost = environment.Client.Connection.Options.TargetHost;
        logger.LogInformation("Temporal test server started on {TargetHost}", connectOptions.TargetHost);
    }

    /// <summary>Stops and disposes the in-process dev server.</summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (environment is { } started)
        {
            await started.DisposeAsync().ConfigureAwait(false);
        }
    }
}
