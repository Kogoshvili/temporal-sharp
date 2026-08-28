using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Temporalio.Client;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class WorkflowOpsTypeNameTests
{
    [Fact]
    public void WorkflowName_UsesClassNameByDefault() =>
        Assert.Equal("GreetingWorkflow", WorkflowOps.WorkflowName<GreetingWorkflow>());

    [Fact]
    public void WorkflowName_RespectsAttributeName() =>
        Assert.Equal("CustomName", WorkflowOps.WorkflowName<CustomNamedWorkflow>());

    [Fact]
    public void WorkflowName_NonWorkflowType_Throws() =>
        Assert.Throws<ArgumentException>(() => WorkflowOps.WorkflowName<NotAWorkflow>());
}

public class WorkflowOpsTests
{
    private static WorkflowOps CreateOps() =>
        new(
            TemporalClient.CreateLazy(new TemporalClientConnectOptions("127.0.0.1:1")),
            new WorkflowOptionsRegistry(Options.Create(new TemporalOptions())));

    [Fact]
    public void Handle_ReturnsTypedHandleWithId()
    {
        var handle = CreateOps().Handle<GreetingWorkflow>("wf-1");

        Assert.Equal("wf-1", handle.Id);
    }

    [Fact]
    public void AddTemporal_RegistersWorkflowOps()
    {
        var services = new ServiceCollection();
        services.AddTemporal();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IWorkflowOps>());
        Assert.IsType<WorkflowOps>(provider.GetService<IWorkflowOps>());
    }
}

[Workflow("CustomName")]
public class CustomNamedWorkflow
{
    [WorkflowRun]
    public Task RunAsync() => Task.CompletedTask;
}

public class NotAWorkflow
{
}
