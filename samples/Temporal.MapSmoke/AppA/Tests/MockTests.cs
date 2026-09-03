using Temporalio.Activities;

namespace Kogoshvili.Temporal.MapSmoke.AppA.Tests;

// Scenario 9: MockActivities — test-only activities; MockRun is never called
// anywhere, so it disappears from maps when test projects are excluded.
public sealed class MockActivities
{
    [Activity]
    public Task<string> MockRun(string name) => Task.FromResult($"mock {name}");
}

public class MockTests
{
    // Scenario 10: trivial fact — this project only needs to compile.
    [Fact]
    public void Sanity() => Assert.True(true);
}
