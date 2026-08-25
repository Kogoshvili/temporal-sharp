namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Logging configuration for the starter. When enabled, the SDK runtime's Core
/// logs are forwarded into the application's <see cref="Microsoft.Extensions.Logging.ILogger"/>
/// pipeline under the configured category.
/// </summary>
public sealed class TemporalLoggingOptions
{
    /// <summary>Gets or sets a value indicating whether Core log forwarding is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the logger category forwarded Core logs use, controllable via
    /// <c>Logging:LogLevel</c> in configuration.
    /// </summary>
    public string Category { get; set; } = "Temporalio.Core";
}
