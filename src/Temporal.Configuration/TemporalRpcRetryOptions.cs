namespace Kogoshvili.Temporal.Configuration;

/// <summary>
/// Retry options applied to the Temporal connection, mirroring the SDK's
/// <c>RpcRetryOptions</c>. Defaults match the SDK so configuring only a subset
/// of properties leaves the rest at their SDK defaults. Duration values are
/// bound from configuration as time-span strings (e.g. <c>"00:00:01"</c>).
/// </summary>
public sealed class TemporalRpcRetryOptions
{
    /// <summary>Gets or sets the initial retry interval. Default is 100ms.</summary>
    public TimeSpan InitialInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>Gets or sets the randomization factor (jitter). Default is 0.2.</summary>
    public float RandomizationFactor { get; set; } = 0.2F;

    /// <summary>Gets or sets the backoff multiplier. Default is 1.5.</summary>
    public float Multiplier { get; set; } = 1.5F;

    /// <summary>Gets or sets the maximum backoff interval. Default is 5s.</summary>
    public TimeSpan MaxInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Gets or sets the maximum elapsed time, or <c>null</c> for none. Default is 10s.</summary>
    public TimeSpan? MaxElapsedTime { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets or sets the maximum number of retries. Default is 10.</summary>
    public int MaxRetries { get; set; } = 10;
}
