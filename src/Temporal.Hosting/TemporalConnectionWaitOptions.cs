namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Startup connection-wait configuration. When <see cref="Enabled"/> is
/// <c>true</c>, the starter waits (retrying with exponential backoff) for the
/// Temporal server to be reachable before workers start polling, rather than
/// letting the first worker's connect attempt fail the host. Duration values
/// are bound from configuration as time-span strings (e.g. <c>"00:01:00"</c>).
/// This is ignored when the test server is enabled, which starts its own
/// in-process server.
/// </summary>
public sealed class TemporalConnectionWaitOptions
{
    /// <summary>Gets or sets a value indicating whether to wait for the server on startup.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum time to keep retrying before giving up, or
    /// <c>null</c> to retry indefinitely. Default is one minute.
    /// </summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Gets or sets the delay before the first retry. Default is one second.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets or sets the maximum delay between retries. Default is 15 seconds.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(15);
}
