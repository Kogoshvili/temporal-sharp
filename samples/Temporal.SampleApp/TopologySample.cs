using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Worker;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// Demonstrates the topology `temporal-sharp map` produces: workflows, their
// signal/query handlers, activities (typed + string-named), child workflows,
// nexus operations, and task-queue registrations (worker + client).

[Workflow]
public class OrderWorkflow
{
    private bool approved;
    private string status = "created";

    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        // Typed activity call.
        await Workflow.ExecuteActivityAsync(
            () => OrderActivities.ChargeCustomer(),
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

        // String-named activity (cross-repo target -> boundary node).
        await Workflow.ExecuteActivityAsync(
            "LegacyPayment",
            null,
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });

        // Child workflow via a typed lambda.
        await Workflow.StartChildWorkflowAsync(
            (ChildWorkflow wf) => wf.RunAsync("order-1"),
            new ChildWorkflowOptions()).GetResultAsync();

        // Nexus operation via a string-named service and operation.
        var nexus = Workflow.CreateNexusWorkflowClient("shipping-nexus");
        await nexus.StartNexusOperationAsync("ShipPackage", "order-1", new NexusOperationOptions()).GetResultAsync();

        await Workflow.WaitConditionAsync(() => approved, TimeSpan.FromMinutes(5));

        return status;
    }

    [WorkflowSignal]
    public Task ApproveAsync()
    {
        approved = true;
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public string Status() => status;
}

[Workflow]
public class ChildWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string orderId)
    {
        await Workflow.DelayAsync(100);
        return "shipped:" + orderId;
    }

    [WorkflowQuery]
    public string Progress() => "pending";
}

public static class OrderActivities
{
    [Activity]
    public static async Task ChargeCustomer()
    {
        await Task.Delay(1);
    }

    [Activity]
    public static Task RefundCustomer() => Task.CompletedTask;
}

// Worker registration associates workflows and activities with a task queue.
public static class WorkerSetup
{
    public static TemporalWorkerOptions CreateOptions()
    {
        return new TemporalWorkerOptions("order-task-queue")
            .AddWorkflow<OrderWorkflow>()
            .AddWorkflow<ChildWorkflow>()
            .AddActivity(OrderActivities.ChargeCustomer)
            .AddActivity(OrderActivities.RefundCustomer);
    }
}

// Client start associates a workflow with a task queue at start time.
public static class ClientStarter
{
    public static async Task StartAsync(TemporalClient client)
    {
        await client.StartWorkflowAsync(
            (OrderWorkflow wf) => wf.RunAsync(),
            new StartWorkflowOptions { Id = "order-1", TaskQueue = "order-task-queue" });
    }
}
