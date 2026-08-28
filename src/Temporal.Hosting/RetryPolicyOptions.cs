namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Retry-policy options shared by activity and workflow options presets,
/// mirroring the SDK's <see cref="Temporalio.Common.RetryPolicy"/>. Every
/// property is nullable: <c>null</c> means "leave the SDK default untouched".
/// </summary>
public sealed class RetryPolicyOptions
{
    /// <summary>Gets or sets the backoff interval for the first retry. SDK default is 1s.</summary>
    public TimeSpan? InitialInterval { get; set; }

    /// <summary>Gets or sets the backoff multiplier. SDK default is 2.0.</summary>
    public float? BackoffCoefficient { get; set; }

    /// <summary>Gets or sets the maximum backoff interval, or <c>null</c> for none.</summary>
    public TimeSpan? MaximumInterval { get; set; }

    /// <summary>Gets or sets the maximum number of attempts, or <c>0</c> for unlimited. SDK default is 0.</summary>
    public int? MaximumAttempts { get; set; }

    /// <summary>Gets or sets error types that must not be retried.</summary>
    public IReadOnlyCollection<string>? NonRetryableErrorTypes { get; set; }
}
