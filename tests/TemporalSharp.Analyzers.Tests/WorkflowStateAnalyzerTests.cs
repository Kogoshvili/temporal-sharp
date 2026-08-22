using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using TemporalSharp.Analyzers.Analyzers;

namespace TemporalSharp.Analyzers.Tests;

public class WorkflowStateAnalyzerTests
{
    private static Task Verify(string source)
    {
        var test = new CSharpAnalyzerTest<WorkflowStateAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        return test.RunAsync();
    }

    [Fact]
    public Task StaticFieldAssignment_InWorkflow_Reports()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private static int counter;

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP1101:counter = 5|};
                }
            }
            """);

    [Fact]
    public Task StaticPropertySetter_InWorkflow_Reports()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private static int Limit { get; set; }

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP1103:Limit = 3|};
                }
            }
            """);

    [Fact]
    public Task StaticFieldIncrement_InWorkflow_Reports()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private static int counter;

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP1101:counter++|};
                }
            }
            """);

    [Fact]
    public Task InstanceFieldAssignment_InWorkflow_DoesNotReport()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private int counter;

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    counter = 5;
                }
            }
            """);

    [Fact]
    public Task StaticFieldAssignment_InNonWorkflow_DoesNotReport()
        => Verify(TestStubs.Attributes + """
            public class PlainClass
            {
                private static int counter;

                public void DoSomething()
                {
                    counter = 5;
                }
            }
            """);

    [Fact]
    public Task ThreadStaticFieldAssignment_InWorkflow_Reports()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [System.ThreadStatic]
                private static int counter;

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP1102:counter = 5|};
                }
            }
            """);

    [Fact]
    public Task StaticCollectionAdd_InWorkflow_Reports()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private static readonly System.Collections.Generic.List<int> items = new();

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP1104:items.Add(1)|};
                }
            }
            """);

    [Fact]
    public Task StaticCollectionClear_InWorkflow_Reports()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private static readonly System.Collections.Generic.Dictionary<int, int> map = new();

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP1104:map.Clear()|};
                }
            }
            """);

    [Fact]
    public Task InstanceCollectionAdd_InWorkflow_DoesNotReport()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private readonly System.Collections.Generic.List<int> items = new();

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    items.Add(1);
                }
            }
            """);

    [Fact]
    public Task StaticCollectionAdd_OutsideWorkflow_DoesNotReport()
        => Verify(TestStubs.Attributes + """
            public class PlainClass
            {
                private static readonly System.Collections.Generic.List<int> items = new();

                public void DoSomething()
                {
                    items.Add(1);
                }
            }
            """);

    [Fact]
    public Task SdkManagedStaticCollectionMutation_InWorkflow_DoesNotReport()
        => Verify(TestStubs.Attributes + TestStubs.Sdk + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    Temporalio.Workflows.Workflow.Signals.Remove("name");
                }
            }
            """);

    [Fact]
    public Task StaticObjectMethodCall_InWorkflow_Reports()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private static readonly Store cache = new();

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    {|TMP1105:cache.Set("key", 1)|};
                }
            }

            public class Store
            {
                public void Set(string key, int value) { }
                public int Get(string key) => 0;
            }
            """);

    [Fact]
    public Task StaticObjectReadMethodCall_InWorkflow_DoesNotReport()
        => Verify(TestStubs.Attributes + """
            [Temporalio.Workflows.Workflow]
            public class MyWorkflow
            {
                private static readonly Store cache = new();

                [Temporalio.Workflows.WorkflowRun]
                public void Run()
                {
                    var v = cache.Get("key");
                }
            }

            public class Store
            {
                public void Set(string key, int value) { }
                public int Get(string key) => 0;
            }
            """);
}
