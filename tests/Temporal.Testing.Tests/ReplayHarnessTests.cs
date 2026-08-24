using Kogoshvili.Temporal.Testing;
using Temporalio.Client;
using Temporalio.Worker;

namespace Kogoshvili.Temporal.Testing.Tests;

public class ReplayHarnessTests : IAsyncLifetime
{
    private ReplayHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await ReplayHarness.StartTimeSkippingAsync();
    }

    public async Task DisposeAsync()
    {
        await _harness.DisposeAsync();
    }

    [Fact]
    public async Task DeterministicWorkflow_Replays_WithoutFailure()
    {
        var workerOptions = new TemporalWorkerOptions("replay-test-queue")
            .AddWorkflow<GreetingWorkflow>();

        var result = await _harness.VerifyAsync<GreetingWorkflow, string>(
            workerOptions,
            workflow => workflow.RunAsync("world"),
            new WorkflowOptions { Id = "greeting-replay", TaskQueue = "replay-test-queue" });

        Assert.True(result.Succeeded, result.ReplayFailure?.ToString());
    }

    [Fact]
    public async Task DeterministicWorkflow_Snapshot_RoundTrips()
    {
        var workerOptions = new TemporalWorkerOptions("replay-test-queue")
            .AddWorkflow<GreetingWorkflow>();

        var (_, history) = await _harness.CaptureAsync<GreetingWorkflow, string>(
            workerOptions,
            workflow => workflow.RunAsync("world"),
            new WorkflowOptions { Id = "greeting-snapshot", TaskQueue = "replay-test-queue" });

        var json = Snapshot.ToJson(history);
        Snapshot.AssertEquivalent(json, json);
    }

    [Fact]
    public async Task Replay_FromJson_ReplaysGoldenFile()
    {
        var workerOptions = new TemporalWorkerOptions("replay-test-queue")
            .AddWorkflow<GreetingWorkflow>();

        var (_, history) = await _harness.CaptureAsync<GreetingWorkflow, string>(
            workerOptions,
            workflow => workflow.RunAsync("world"),
            new WorkflowOptions { Id = "greeting-json", TaskQueue = "replay-test-queue" });

        var result = await Replay.FromJsonAsync<GreetingWorkflow>(
            Snapshot.ToJson(history), "greeting-json");

        Assert.Null(result.ReplayFailure);
    }

    [Fact]
    public async Task Replay_FromDirectory_ReplaysCheckedInSnapshots()
    {
        var workerOptions = new TemporalWorkerOptions("replay-test-queue")
            .AddWorkflow<GreetingWorkflow>();

        var (_, history) = await _harness.CaptureAsync<GreetingWorkflow, string>(
            workerOptions,
            workflow => workflow.RunAsync("world"),
            new WorkflowOptions { Id = "greeting-dir", TaskQueue = "replay-test-queue" });

        var dir = Path.Combine(Path.GetTempPath(), $"temporal-replay-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "greeting-dir.json"), Snapshot.ToJson(history));

            var results = await Replay.FromDirectoryAsync<GreetingWorkflow>(dir);

            Assert.Single(results);
            Assert.Null(results[0].ReplayFailure);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
