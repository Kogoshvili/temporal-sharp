using Kogoshvili.Temporal.MapSmoke.AppA.Contracts;
using Temporalio.Activities;

namespace Kogoshvili.Temporal.MapSmoke.AppA.Worker;

// Scenario 2: MainActivities — Greet, Counter, and Local are called by
// workflows. The class is registered on queue-a via AddAllActivities, so every
// method here (including Uncalled) belongs to the queue-a box.
public sealed class MainActivities
{
    [Activity]
    public string Greet(string name) => $"Hello, {name}!";

    [Activity]
    public string Counter(int count) => $"Count {count}";

    [Activity]
    public string Local(string name) => $"Locally processed {name}";

    [Activity]
    public string Uncalled(string name) => "Registered but never called";
}

// Scenario 2b: declared [Activity] that is neither registered on any worker
// nor called from any workflow — the true orphan (no caller, no queue).
public sealed class OrphanActivities
{
    [Activity]
    public string NeverReferenced(string name) => "No worker, no caller";
}

// Scenario 11: OrderActivities implements the IOrderActivities contract from
// AppA.Contracts (attributes repeated on the impl — the SDK only discovers
// them on implementing methods). Instance methods over an instance field,
// registered on queue-a via AddAllActivities(instance).
public sealed class OrderActivities : IOrderActivities
{
    private readonly HashSet<string> processed = [];

    [Activity]
    public Task<string> Process(string input)
    {
        processed.Add(input);
        return Task.FromResult($"processed {input}");
    }

    [Activity]
    public Task<string> Ship(string input) => Task.FromResult($"shipped {input}");
}

// Scenario 12: heartbeat pair — HeartbeatGood reports progress via
// ActivityExecutionContext, HeartbeatMissing does not; both call sites set
// HeartbeatTimeout, so only the latter renders the heartbeat-issue edge.
public sealed class HeartbeatActivities
{
    [Activity]
    public string HeartbeatGood(string name)
    {
        ActivityExecutionContext.Current.Heartbeat(name);
        return $"heartbeated {name}";
    }

    [Activity]
    public string HeartbeatMissing(string name) => $"no heartbeat for {name}";
}

// Scenario 11c: ambiguous implementations of IAmbiguousActivities. AmbiguousImplA
// is registered on queue-a; AmbiguousImplB is loaded but never registered (and
// unattributed — the [Activity] attribute lives on the interface), which is
// what turns the contract into a Contract node on the map.
public sealed class AmbiguousImplA : IAmbiguousActivities
{
    [Activity]
    public Task<string> Run(string input) => Task.FromResult($"impl-a {input}");
}

public sealed class AmbiguousImplB : IAmbiguousActivities
{
    public Task<string> Run(string input) => Task.FromResult($"impl-b {input}");
}
