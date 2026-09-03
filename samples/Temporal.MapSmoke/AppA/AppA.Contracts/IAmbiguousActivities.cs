using Temporalio.Activities;

namespace Kogoshvili.Temporal.MapSmoke.AppA.Contracts;

// Scenario 11c: deliberately ambiguous contract — two implementations exist
// (only AmbiguousImplA is registered on a worker), so the grapher renders a
// Contract node instead of guessing.
public interface IAmbiguousActivities
{
    [Activity]
    Task<string> Run(string input);
}
