using OpenTelemetry;
using Temporalio.Extensions.OpenTelemetry;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// <see cref="TracingInterceptor"/> subclass that additionally attaches
/// allowlisted OpenTelemetry baggage entries as <c>baggage.&lt;key&gt;</c>
/// attributes on every span it creates (client, workflow, and activity).
/// </summary>
internal sealed class BaggageTracingInterceptor : TracingInterceptor
{
    private readonly IReadOnlyCollection<string> baggageTagKeys;

    public BaggageTracingInterceptor(IReadOnlyCollection<string> baggageTagKeys)
        => this.baggageTagKeys = baggageTagKeys;

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object?>> CreateWorkflowTags(string workflowId) =>
        base.CreateWorkflowTags(workflowId).Concat(BaggageTags());

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object?>> CreateUpdateTags(
        string workflowId, string? updateId) =>
        base.CreateUpdateTags(workflowId, updateId).Concat(BaggageTags());

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object?>> CreateStandaloneActivityTags(
        string activityId) =>
        base.CreateStandaloneActivityTags(activityId).Concat(BaggageTags());

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object?>> CreateInWorkflowTags() =>
        base.CreateInWorkflowTags().Concat(BaggageTags());

    /// <inheritdoc />
    protected override IEnumerable<KeyValuePair<string, object?>> CreateInActivityTags() =>
        base.CreateInActivityTags().Concat(BaggageTags());

    private IEnumerable<KeyValuePair<string, object?>> BaggageTags()
    {
        if (baggageTagKeys.Count == 0)
        {
            return Enumerable.Empty<KeyValuePair<string, object?>>();
        }

        var baggage = Baggage.Current;
        var tags = new List<KeyValuePair<string, object?>>(baggageTagKeys.Count);
        foreach (var key in baggageTagKeys)
        {
            if (baggage.GetBaggage(key) is { } value)
            {
                tags.Add(new($"baggage.{key}", value));
            }
        }

        return tags;
    }
}
