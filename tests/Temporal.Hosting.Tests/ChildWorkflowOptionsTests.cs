using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Temporalio.Workflows;
using Xunit;

namespace Kogoshvili.Temporal.Hosting.Tests;

public class ChildWorkflowOptionsFactoryTests
{
    [Fact]
    public void Apply_MapsSharedAndChildFields()
    {
        var preset = new WorkflowOptionsPreset
        {
            RunTimeout = TimeSpan.FromMinutes(5),
            TaskTimeout = TimeSpan.FromSeconds(10),
            ExecutionTimeout = TimeSpan.FromMinutes(60),
            TaskQueue = "child-queue",
            ParentClosePolicy = ParentClosePolicy.RequestCancel,
            CancellationType = ChildWorkflowCancellationType.TryCancel,
            Retry = new RetryPolicyOptions { MaximumAttempts = 3 },
        };

        var options = ChildWorkflowOptionsFactory.Build(preset);

        Assert.Equal(TimeSpan.FromMinutes(5), options.RunTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), options.TaskTimeout);
        Assert.Equal(TimeSpan.FromMinutes(60), options.ExecutionTimeout);
        Assert.Equal("child-queue", options.TaskQueue);
        Assert.Equal(ParentClosePolicy.RequestCancel, options.ParentClosePolicy);
        Assert.Equal(ChildWorkflowCancellationType.TryCancel, options.CancellationType);
        Assert.NotNull(options.RetryPolicy);
        Assert.Equal(3, options.RetryPolicy!.MaximumAttempts);
    }

    [Fact]
    public void Apply_NullPreset_LeavesSdkDefaults()
    {
        var options = ChildWorkflowOptionsFactory.Build(null);

        Assert.Null(options.RunTimeout);
        Assert.Null(options.RetryPolicy);
        Assert.Equal(ParentClosePolicy.Terminate, options.ParentClosePolicy);
        Assert.Equal(ChildWorkflowCancellationType.WaitCancellationCompleted, options.CancellationType);
    }
}

[Collection("ActivityOptionsRegistry")]
public class ChildWorkflowOptionsRegistryTests
{
    [Fact]
    public void Resolve_AppliesDefaultPreset()
    {
        ChildWorkflowOptionsRegistry.Replace(
            new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(5) },
            null,
            null);

        var options = ChildWorkflowOptionsRegistry.Resolve("MyWorkflow");

        Assert.Equal(TimeSpan.FromMinutes(5), options.RunTimeout);
    }

    [Fact]
    public void Resolve_PerTypeOverridesDefault()
    {
        ChildWorkflowOptionsRegistry.Replace(
            new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(5) },
            new Dictionary<string, WorkflowOptionsPreset>
            {
                ["MyWorkflow"] = new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(30) },
            },
            null);

        var options = ChildWorkflowOptionsRegistry.Resolve("MyWorkflow");

        Assert.Equal(TimeSpan.FromMinutes(30), options.RunTimeout);
    }

    [Fact]
    public void Resolve_UnknownType_LeavesDefault()
    {
        ChildWorkflowOptionsRegistry.Replace(
            new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(5) },
            new Dictionary<string, WorkflowOptionsPreset>
            {
                ["MyWorkflow"] = new WorkflowOptionsPreset { RunTimeout = TimeSpan.FromMinutes(30) },
            },
            null);

        var options = ChildWorkflowOptionsRegistry.Resolve("OtherWorkflow");

        Assert.Equal(TimeSpan.FromMinutes(5), options.RunTimeout);
    }

    [Fact]
    public void ResolveChildIdFormat_DefaultWhenUnset()
    {
        ChildWorkflowOptionsRegistry.Replace(null, null, null);

        Assert.Equal(WorkflowIdOptions.DefaultChildFormat, ChildWorkflowOptionsRegistry.ResolveChildIdFormat());
    }

    [Fact]
    public void ResolveChildIdFormat_EmptyOptsOut()
    {
        ChildWorkflowOptionsRegistry.Replace(null, null, "");

        Assert.Null(ChildWorkflowOptionsRegistry.ResolveChildIdFormat());
    }

    [Fact]
    public void ResolveChildIdFormat_CustomTemplate()
    {
        ChildWorkflowOptionsRegistry.Replace(null, null, "sub-{Parent}-{Guid:N}");

        Assert.Equal("sub-{Parent}-{Guid:N}", ChildWorkflowOptionsRegistry.ResolveChildIdFormat());
    }
}

public class ChildWorkflowIdFormatterTests
{
    [Fact]
    public void Format_SubstitutesTypeQueueParentAndGuid()
    {
        var id = WorkflowIdFormatter.Format("{Parent}-{Type}-{Queue}-{Guid:N}", "PaymentWorkflow", "billing", "order-123");

        Assert.Matches(@"^order-123-PaymentWorkflow-billing-[0-9a-f]{32}$", id);
    }

    [Fact]
    public void Format_NoParent_LeavesEmpty()
    {
        var id = WorkflowIdFormatter.Format("{Parent}-{Type}", "PaymentWorkflow");

        Assert.Equal("-PaymentWorkflow", id);
    }

    [Fact]
    public void Format_TypeS_StripsWorkflowSuffix()
    {
        var id = WorkflowIdFormatter.Format("{Type:s}-{Guid:N}", "OrderWorkflow");

        Assert.Matches(@"^Order-[0-9a-f]{32}$", id);
    }

    [Fact]
    public void Format_TypeS_StripsCaseInsensitive()
    {
        Assert.Equal("Order", WorkflowIdFormatter.Format("{Type:s}", "OrderWorkflow"));
        Assert.Equal("order", WorkflowIdFormatter.Format("{Type:s}", "orderWorkflow"));
        Assert.Equal("MoneyTransfer", WorkflowIdFormatter.Format("{Type:s}", "MoneyTransferWORKFLOW"));
    }

    [Fact]
    public void Format_TypeS_NoSuffixUnchanged()
    {
        Assert.Equal("Billing", WorkflowIdFormatter.Format("{Type:s}", "Billing"));
    }

    [Fact]
    public void Format_TypeBare_KeepsFullName()
    {
        Assert.Equal("OrderWorkflow", WorkflowIdFormatter.Format("{Type}", "OrderWorkflow"));
    }
}

[Collection("ActivityOptionsRegistry")]
public class ChildWorkflowSeedingTests
{
    [Fact]
    public void AddTemporal_SeedsChildRegistryFromWorkflowsConfig()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Temporal:TargetHost"] = "host:7233",
                ["Temporal:Workflows:Id:ChildFormat"] = "sub-{Parent}-{Guid:N}",
                ["Temporal:Workflows:Default:RunTimeout"] = "00:05:00",
                ["Temporal:Workflows:Default:ParentClosePolicy"] = "RequestCancel",
                ["Temporal:Workflows:ByType:MyWorkflow:RunTimeout"] = "00:30:00",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTemporal(configuration);

        using var provider = services.BuildServiceProvider();

        Assert.Equal("sub-{Parent}-{Guid:N}", ChildWorkflowOptionsRegistry.ResolveChildIdFormat());

        var options = ChildWorkflowOptionsRegistry.Resolve("MyWorkflow");
        Assert.Equal(TimeSpan.FromMinutes(30), options.RunTimeout);
        Assert.Equal(ParentClosePolicy.RequestCancel, options.ParentClosePolicy);
    }
}
