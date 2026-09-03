using Microsoft.Extensions.Configuration;
using Temporalio.Client;
using Temporalio.Worker;

namespace Kogoshvili.Temporal.MapSmoke.AppA.Worker;

public static class Program
{
    public static async Task Main()
    {
        var client = await TemporalClient.ConnectAsync(new("localhost:7233") { Namespace = "default" });
        using var cts = new CancellationTokenSource();

        // Scenario 6: queue-a worker — MainWorkflow, OrderWorkflow (Scenario 11),
        // and HeartbeatWorkflow (Scenario 12) plus all activity classes;
        // AmbiguousImplB is deliberately left unregistered. Uncalled is
        // deliberately not registered.
        var workerA = new TemporalWorker(
            client,
            new TemporalWorkerOptions("queue-a")
                .AddWorkflow<MainWorkflow>()
                .AddWorkflow<OrderWorkflow>()
                .AddWorkflow<HeartbeatWorkflow>()
                .AddAllActivities<MainActivities>(new MainActivities())
                .AddAllActivities<OrderActivities>(new OrderActivities())
                .AddAllActivities<HeartbeatActivities>(new HeartbeatActivities())
                .AddAllActivities<AmbiguousImplA>(new AmbiguousImplA()));

        // Scenario 4: DualQueueWorkflow registered on a second and third
        // worker — queue-a and queue-c.
        var workerDualA = new TemporalWorker(
            client, new TemporalWorkerOptions("queue-a").AddWorkflow<DualQueueWorkflow>());
        var workerDualC = new TemporalWorker(
            client, new TemporalWorkerOptions("queue-c").AddWorkflow<DualQueueWorkflow>());

        // Scenario 5: ConfigQueueWorkflow registered via a non-constant queue name.
        var configQueue = Environment.GetEnvironmentVariable("QUEUE") ?? "fallback";
        var workerConfig = new TemporalWorker(
            client, new TemporalWorkerOptions(configQueue).AddWorkflow<ConfigQueueWorkflow>());

        // Scenario 15: env-default queue — OtherWorkflow polls a queue named by
        // the ORDER_QUEUE env var with a constant fallback; the map tool
        // renders the fallback ("orders-fallback").
        var workerEnv = new TemporalWorker(
            client,
            new TemporalWorkerOptions(GetEnvVarWithDefault("ORDER_QUEUE", "orders-fallback"))
                .AddWorkflow<OtherWorkflow>());

        // Scenario 16: config-driven queue — appsettings.json ("config-q"),
        // overridden by appsettings.Production.json ("config-q-prod");
        // appsettings.Development.json is ignored by the map tool.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Production.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
#pragma warning disable CS8604 // key is present in appsettings.json; kept inline so the map tool resolves the queue
        var workerConfigFile = new TemporalWorker(
            client,
            new TemporalWorkerOptions(configuration["Temporal:Worker:TaskQueue"])
                .AddWorkflow<ConfigFileWorkflow>());
#pragma warning restore CS8604

        var workers = Task.WhenAll(
            workerA.ExecuteAsync(cts.Token),
            workerDualA.ExecuteAsync(cts.Token),
            workerDualC.ExecuteAsync(cts.Token),
            workerConfig.ExecuteAsync(cts.Token),
            workerEnv.ExecuteAsync(cts.Token),
            workerConfigFile.ExecuteAsync(cts.Token));

        // Scenario 6: start MainWorkflow via the client on queue-a.
        await client.StartWorkflowAsync(
            (MainWorkflow w) => w.RunAsync("world"),
            new WorkflowOptions { Id = "map-smoke-main", TaskQueue = "queue-a" });

        // Scenario 13: standalone activity — Ship runs without a workflow,
        // addressed by ID "standalone-1" on "standalone-q".
        var orderActivities = new OrderActivities();
        await client.StartActivityAsync(
            () => orderActivities.Ship("x"),
            new StartActivityOptions("standalone-1", "standalone-q")
            {
                ScheduleToCloseTimeout = TimeSpan.FromSeconds(10.0),
            });

        // Scenario 14: client-side signal, query, and update against
        // OrderWorkflow through a typed handle.
        var handle = client.GetWorkflowHandle<OrderWorkflow>("order-1");
        await handle.SignalAsync((OrderWorkflow w) => w.ApproveAsync("ops"));
        _ = await handle.QueryAsync((OrderWorkflow w) => w.GetStatus());
        var update = await handle.StartUpdateAsync(
            (OrderWorkflow w) => w.SetPriorityAsync(3),
            new WorkflowUpdateStartOptions { WaitForStage = WorkflowUpdateStage.Accepted });

        Console.WriteLine(
            $"Workflows started on queue-a; workers polling; update {update}. Press Ctrl+C to exit.");
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await workers;
        }
        catch (OperationCanceledException)
        {
        }
    }

    // Scenario 15 helper: the map tool recognizes the "...EnvVar..." method
    // name and renders the constant fallback as the queue name.
    private static string GetEnvVarWithDefault(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) ?? fallback;
}
