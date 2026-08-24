using Kogoshvili.Temporal.Hosting;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class WorkerDiscoveryTests
{
    [Fact]
    public void FindWorkflowTypes_ReturnsConcreteWorkflowClasses()
    {
        var types = WorkerDiscovery.FindWorkflowTypes(typeof(GreetingWorkflow).Assembly);

        Assert.Contains(typeof(GreetingWorkflow), types);
        Assert.DoesNotContain(typeof(AbstractWorkflow), types);
        Assert.DoesNotContain(typeof(NoWorkflowClass), types);
    }

    [Fact]
    public void FindActivityTypes_ReturnsClassesWithActivityMethods()
    {
        var types = WorkerDiscovery.FindActivityTypes(typeof(InstanceActivity).Assembly);

        Assert.Contains(typeof(InstanceActivity), types);
        Assert.Contains(typeof(StaticActivity), types);
        Assert.DoesNotContain(typeof(NoActivityClass), types);
    }

    [Fact]
    public void GetActivityLifetime_DefaultsByClassKind()
    {
        Assert.Equal(ActivityLifetime.Scoped, WorkerDiscovery.GetActivityLifetime(typeof(InstanceActivity)));
        Assert.Equal(ActivityLifetime.Static, WorkerDiscovery.GetActivityLifetime(typeof(StaticActivity)));
    }

    [Fact]
    public void GetActivityLifetime_HonorsAttribute()
    {
        Assert.Equal(ActivityLifetime.Singleton, WorkerDiscovery.GetActivityLifetime(typeof(SingletonActivity)));
        Assert.Equal(ActivityLifetime.Transient, WorkerDiscovery.GetActivityLifetime(typeof(TransientActivity)));
    }
}

[Workflow]
public class GreetingWorkflow
{
    [WorkflowRun]
    public Task<string> RunAsync(string name) => Task.FromResult($"Hello, {name}!");
}

[Workflow]
public abstract class AbstractWorkflow
{
    [WorkflowRun]
    public Task RunAsync() => Task.CompletedTask;
}

public class NoWorkflowClass
{
}

public class InstanceActivity
{
    [Activity]
    public Task<string> RunAsync(string input) => Task.FromResult(input);
}

[ActivityLifetime(ActivityLifetime.Singleton)]
public class SingletonActivity
{
    [Activity]
    public Task SingletonRunAsync() => Task.CompletedTask;
}

[ActivityLifetime(ActivityLifetime.Transient)]
public class TransientActivity
{
    [Activity]
    public Task TransientRunAsync() => Task.CompletedTask;
}

public static class StaticActivity
{
    [Activity]
    public static Task StaticRunAsync() => Task.CompletedTask;
}

public class NoActivityClass
{
    public Task RunAsync() => Task.CompletedTask;
}
