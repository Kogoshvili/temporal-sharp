using Temporalio.Activities;

namespace Kogoshvili.Temporal.MapSmoke.AppB.Worker;

// Scenario 7: BActivities — activities-only project (no workflows). Process is
// called cross-queue from AppA's MainWorkflow; ProcessOnlyB is never called
// from AppA (orphan-with-queue case).
public sealed class BActivities
{
    [Activity]
    public string Process(string name) => $"Processed {name} on queue-b";

    [Activity]
    public string ProcessOnlyB(string name) => $"B-only work for {name}";

    // Scenario 7b: explicit activity name — AppA calls "LegacyPayment" by
    // string, which the map resolves here across the two solutions.
    [Activity("LegacyPayment")]
    public string RecordLegacyPayment(string name) => $"Legacy payment recorded for {name}";
}
