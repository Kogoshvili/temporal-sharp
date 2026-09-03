using Temporalio.Client;
using Temporalio.Worker;

namespace Kogoshvili.Temporal.MapSmoke.AppB.Worker;

public static class Program
{
    public static async Task Main()
    {
        var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });
        using var cts = new CancellationTokenSource();

        // Scenario 8: activities-only worker — both BActivities activities
        // registered on queue-b.
        var worker = new TemporalWorker(
            client,
            new TemporalWorkerOptions("queue-b").AddAllActivities<BActivities>(new BActivities()));

        Console.WriteLine("BActivities polling queue-b. Press Ctrl+C to exit.");
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await worker.ExecuteAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
