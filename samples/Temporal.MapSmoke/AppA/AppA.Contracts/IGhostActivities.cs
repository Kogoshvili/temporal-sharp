using Temporalio.Activities;

namespace Kogoshvili.Temporal.MapSmoke.AppA.Contracts;

// Scenario 12: interface activity with NO implementation in any input — the
// call stays on the contract node marked with the light question mark (❔).
public interface IGhostActivities
{
    [Activity]
    string Vanish(string input);
}
