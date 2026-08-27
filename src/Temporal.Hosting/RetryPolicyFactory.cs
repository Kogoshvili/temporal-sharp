using Temporalio.Common;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Maps a configuration-bound <see cref="RetryPolicyOptions"/> onto the SDK's
/// <see cref="RetryPolicy"/>. Null preset properties leave the SDK defaults
/// untouched. Shared by the activity and workflow options factories.
/// </summary>
internal static class RetryPolicyFactory
{
    public static RetryPolicy Build(RetryPolicyOptions retry)
    {
        var policy = new RetryPolicy();

        if (retry.InitialInterval is { } initialInterval)
        {
            policy.InitialInterval = initialInterval;
        }

        if (retry.BackoffCoefficient is { } backoffCoefficient)
        {
            policy.BackoffCoefficient = backoffCoefficient;
        }

        if (retry.MaximumInterval is { } maximumInterval)
        {
            policy.MaximumInterval = maximumInterval;
        }

        if (retry.MaximumAttempts is { } maximumAttempts)
        {
            policy.MaximumAttempts = maximumAttempts;
        }

        if (retry.NonRetryableErrorTypes is { } nonRetryable)
        {
            policy.NonRetryableErrorTypes = nonRetryable;
        }

        return policy;
    }
}
