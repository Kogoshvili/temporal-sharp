using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Kogoshvili.Temporal.Cli.Map;

namespace Kogoshvili.Temporal.Cli.Tests;

public class WorkflowTopologyBuilderTests
{
    private const string Source = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class WorkflowSignalAttribute : System.Attribute { }
            public sealed class WorkflowQueryAttribute : System.Attribute { }
            public sealed class WorkflowUpdateAttribute : System.Attribute { }

            public sealed class ActivityOptions { }
            public sealed class ChildWorkflowOptions { }
            public sealed class NexusOperationOptions { }

            public sealed class NexusWorkflowClient
            {
                public NexusWorkflowOperationHandle StartNexusOperationAsync(string operation, object? arg, NexusOperationOptions? options) => new();
            }

            public sealed class NexusWorkflowOperationHandle { }

            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    string activity, System.Collections.Generic.IReadOnlyCollection<object?>? args, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static ChildWorkflowHandle StartChildWorkflowAsync<TWorkflow, TResult>(
                    System.Linq.Expressions.Expression<System.Func<TWorkflow, System.Threading.Tasks.Task<TResult>>> workflowRunCall,
                    ChildWorkflowOptions? options)
                    => new();

                public static NexusWorkflowClient CreateNexusWorkflowClient(string service) => new();
            }

            public sealed class ChildWorkflowHandle { }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Temporalio.Worker
        {
            public sealed class TemporalWorkerOptions
            {
                public TemporalWorkerOptions(string taskQueue) { TaskQueue = taskQueue; }
                public string? TaskQueue { get; set; }
                public TemporalWorkerOptions AddWorkflow<TWorkflow>() => this;
            }
        }

        [Temporalio.Workflows.Workflow]
        public class MyWorkflow
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task<string> Run()
            {
                var opts = new Temporalio.Workflows.ActivityOptions();
                await Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => MyActivities.Do(), opts);
                await Temporalio.Workflows.Workflow.ExecuteActivityAsync("Legacy", null, opts);
                await Temporalio.Workflows.Workflow.StartChildWorkflowAsync(
                    (Child wf) => wf.Run(), new Temporalio.Workflows.ChildWorkflowOptions());
                var nexus = Temporalio.Workflows.Workflow.CreateNexusWorkflowClient("svc");
                _ = nexus.StartNexusOperationAsync("Op", null, new Temporalio.Workflows.NexusOperationOptions());
                return "ok";
            }

            [Temporalio.Workflows.WorkflowSignal]
            public System.Threading.Tasks.Task Approve() => System.Threading.Tasks.Task.CompletedTask;

            [Temporalio.Workflows.WorkflowQuery]
            public string Status() => "ok";
        }

        [Temporalio.Workflows.Workflow]
        public class Child
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task<string> Run() => "child";
        }

        public static class MyActivities
        {
            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task Do() => System.Threading.Tasks.Task.CompletedTask;
        }

        public static class Setup
        {
            public static Temporalio.Worker.TemporalWorkerOptions Create() =>
                new Temporalio.Worker.TemporalWorkerOptions("my-queue")
                    .AddWorkflow<MyWorkflow>()
                    .AddWorkflow<Child>();
        }
        """;

    [Fact]
    public async Task BuildsNodesAndEdges()
    {
        var graph = await BuildAsync(Source);

        Assert.Contains(graph.Nodes, n => n.Id == "Workflow:MyWorkflow");
        Assert.Contains(graph.Nodes, n => n.Id == "Workflow:Child");
        Assert.Contains(graph.Nodes, n => n.Kind == TopologyNodeKinds.Activity && n.Name == "MyActivities.Do");
        Assert.Contains(graph.Nodes, n => n.Id == "TaskQueue:my-queue");
        Assert.Contains(graph.Nodes, n => n.Id == "Unknown:Activity:\"Legacy\"");
        Assert.Contains(graph.Nodes, n => n.Id == "Unknown:NexusOperation:\"Op\"");
        Assert.Contains(graph.Nodes, n => n.Id == "Unknown:NexusService:\"svc\"");

        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Activity && e.To.StartsWith("Activity:", StringComparison.Ordinal));
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.ChildWorkflow && e.To == "Workflow:Child");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue && e.To == "TaskQueue:my-queue");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Nexus);

        var workflow = Assert.Single(graph.Nodes, n => n.Id == "Workflow:MyWorkflow");
        Assert.Contains(workflow.Handlers, h => h.Kind == TopologyHandlerKinds.Run && h.Name == "Run");
        Assert.Contains(workflow.Handlers, h => h.Kind == TopologyHandlerKinds.Signal && h.Name == "Approve");
        Assert.Contains(workflow.Handlers, h => h.Kind == TopologyHandlerKinds.Query && h.Name == "Status");
    }

    [Fact]
    public async Task EmitsMermaidAndJson()
    {
        var graph = await BuildAsync(Source);

        var mermaid = TopologyEmitter.ToMermaid(graph);
        Assert.Contains("flowchart LR", mermaid);
        Assert.Contains("classDef workflow", mermaid);

        var json = TopologyEmitter.ToJson(graph);
        Assert.Contains("\"kind\": \"workflow\"", json);
        Assert.Contains("\"kind\": \"taskQueue\"", json);
    }

    [Fact]
    public async Task MermaidHasWhiteBackgroundAndWidthLimits()
    {
        var graph = await BuildAsync(Source);
        var mermaid = TopologyEmitter.ToMermaid(graph);

        Assert.StartsWith("%%{init:", mermaid);
        Assert.Contains("\"background\":\"#ffffff\"", mermaid);
        Assert.Contains("useMaxWidth\":true", mermaid);

        var dot = TopologyEmitter.ToDot(graph);
        Assert.Contains("bgcolor=\"#ffffff\"", dot);
    }

    [Fact]
    public async Task MermaidPinsOrphanAndUnknownBoxesToFlowEnd()
    {
        var graph = await BuildAsync(QueueEvidenceSource);
        var mermaid = TopologyEmitter.ToMermaid(graph);

        // Invisible links from the main flow into the disconnected boxes keep
        // them after the main content instead of floating above it.
        Assert.Contains(" ~~~ uq", mermaid);
        Assert.Contains(" ~~~ orp", mermaid);
    }

    [Fact]
    public async Task EmitsHtmlAndDot()
    {
        var graph = await BuildAsync(Source);

        var html = TopologyEmitter.ToHtml(graph, "sample");
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.DoesNotContain("__JSON__", html);
        Assert.DoesNotContain("__TITLE__", html);
        Assert.Contains("mermaid.min.js", html);
        Assert.Contains("flowchart LR", html);
        Assert.Contains("\"kind\": \"workflow\"", html);
        Assert.Contains("classList", html); // click-to-highlight interactivity

        var dot = TopologyEmitter.ToDot(graph);
        Assert.StartsWith("digraph temporal_topology", dot);
        Assert.Contains("shape=box", dot);
        Assert.Contains("shape=ellipse", dot);
        Assert.Contains(" -> ", dot);
    }

    [Fact]
    public async Task ActivityWorkerRegistrationAssociatesTaskQueue()
    {
        var graph = await BuildAsync(QueueEvidenceSource);

        // .AddActivity(() => ...) registers the lambda target on the worker's queue.
        Assert.Contains(graph.Nodes, n => n.Id == "TaskQueue:reg-q");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From.StartsWith("Activity:QA.Act1", StringComparison.Ordinal) && e.To == "TaskQueue:reg-q");
    }

    [Fact]
    public async Task AddAllActivitiesAssociatesEveryActivityMethod()
    {
        var graph = await BuildAsync(QueueEvidenceSource);

        // Generic form: AddAllActivities<QA>(null) — both [Activity] methods.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From.StartsWith("Activity:QA.Act1", StringComparison.Ordinal) && e.To == "TaskQueue:all-q");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From.StartsWith("Activity:QA.Act2", StringComparison.Ordinal) && e.To == "TaskQueue:all-q");

        // typeof form: AddAllActivities(typeof(QA), null) — same coverage.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From.StartsWith("Activity:QA.Act1", StringComparison.Ordinal) && e.To == "TaskQueue:all-q2");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From.StartsWith("Activity:QA.Act2", StringComparison.Ordinal) && e.To == "TaskQueue:all-q2");
    }

    [Fact]
    public async Task ActivityOptionsTaskQueueRoutesActivityToQueue()
    {
        var graph = await BuildAsync(QueueEvidenceSource);

        // The call site's ActivityOptions { TaskQueue = ... } overrides the queue.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From.StartsWith("Activity:QA.Act1", StringComparison.Ordinal) && e.To == "TaskQueue:route-q");
    }

    [Fact]
    public async Task NodesWithoutDetectedQueueGetUnknownTaskQueueEdge()
    {
        var graph = await BuildAsync(QueueEvidenceSource);

        var unknownQueue = Assert.Single(graph.Nodes, n =>
            n.Kind == TopologyNodeKinds.Unknown && n.UnknownKind == "taskQueue");

        // QWorkflow is registered nowhere; QOrphan.Never is neither registered nor called.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From == "Workflow:QWorkflow" && e.To == unknownQueue.Id);
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From.StartsWith("Activity:QOrphan.Never", StringComparison.Ordinal) && e.To == unknownQueue.Id);
    }

    [Fact]
    public async Task ActivityCallsAreOrderedAndLoopMarked()
    {
        var graph = await BuildAsync(OrderedLoopSource);

        var act1 = graph.Edges.Single(e => e.Kind == TopologyEdgeKinds.Activity && e.To.StartsWith("Activity:OL.Act1", StringComparison.Ordinal));
        Assert.Equal(new[] { 1, 3 }, act1.Order);
        Assert.Null(act1.InLoop);

        var act2 = graph.Edges.Single(e => e.Kind == TopologyEdgeKinds.Activity && e.To.StartsWith("Activity:OL.Act2", StringComparison.Ordinal));
        Assert.Equal(new[] { 2 }, act2.Order);
        Assert.True(act2.InLoop);
    }

    [Fact]
    public void TestProjectFilterDetectsNamesAndReferences()
    {
        Assert.True(TestProjectFilter.IsTestProjectName("/repo/tests/Heracles.Workflow.Tests.csproj"));
        Assert.True(TestProjectFilter.IsTestProjectName("/repo/src/App.Test.csproj"));
        Assert.False(TestProjectFilter.IsTestProjectName("/repo/src/AppA.Worker.csproj"));
        Assert.False(TestProjectFilter.IsTestProjectName("/repo/src/Contest.csproj"));

        Assert.True(TestProjectFilter.HasTestFrameworkReferences(new[] { "xunit.core.dll" }));
        Assert.True(TestProjectFilter.HasTestFrameworkReferences(new[] { "nunit.framework.dll" }));
        Assert.True(TestProjectFilter.HasTestFrameworkReferences(new[] { "Microsoft.VisualStudio.TestPlatform.TestFramework.dll" }));
        Assert.False(TestProjectFilter.HasTestFrameworkReferences(new[] { "Temporalio.dll", "Microsoft.Extensions.Hosting.dll" }));
    }

    [Fact]
    public void MapOptionsParsesIncludeTests()
    {
        Assert.False(MapOptions.Parse(new[] { "some.sln" }).IncludeTests);
        Assert.True(MapOptions.Parse(new[] { "some.sln", "--include-tests" }).IncludeTests);
    }

    [Fact]
    public async Task MarkdownOutputMovesLegendOutsideDiagram()
    {
        var graph = await BuildAsync(QueueEvidenceSource);
        var md = MapOptions.Parse(new[] { "some.sln", "--format", "markdown" });
        Assert.Equal(MapOutputFormat.Markdown, md.Format);

        var content = Kogoshvili.Temporal.Cli.Map.MapCommand.RenderMarkdownForTests(graph, contracts: true);
        Assert.StartsWith("```mermaid", content);
        Assert.DoesNotContain("📖 Legend", content);
        Assert.Contains("## Legend", content);
        Assert.Contains("heartbeat timeout", content);
    }

    [Fact]
    public void MapOptionsParsesContractsFlag()
    {
        Assert.True(MapOptions.Parse(new[] { "some.sln" }).Contracts);
        Assert.False(MapOptions.Parse(new[] { "some.sln", "--no-contracts" }).Contracts);
    }

    [Fact]
    public async Task QueueNamesResolveFromEnvDefaultsAndConfigKeys()
    {
        // Env-default fallback: the constant argument is the resolvable queue.
        var graph = await BuildAsync(EnvQueueSource);
        Assert.Contains(graph.Nodes, n => n.Id == "TaskQueue:orders-fallback");

        // Config-key navigation (pure JSON part).
        const string json = """{"Temporal": {"Worker": {"TaskQueue": "cfg-q"}}}""";
        Assert.Equal("cfg-q", ConfigQueueResolver.NavigateJson(json, "Temporal:Worker:TaskQueue"));
        Assert.Equal("prod-q", ConfigQueueResolver.NavigateJson("""{"Temporal":{"TaskQueue":"base-q"}}""", "Temporal:TaskQueue", """{"Temporal":{"TaskQueue":"prod-q"}}"""));
        Assert.Null(ConfigQueueResolver.NavigateJson("{}", "Missing:Key"));
    }

    [Fact]
    public async Task MermaidGroupsNodesIntoTaskQueueBoxes()
    {
        var graph = await BuildAsync(QueueEvidenceSource);
        var mermaid = TopologyEmitter.ToMermaid(graph);

        // One box per queue (boxes are name-sorted); members go inside subgraphs.
        Assert.Contains("subgraph q", mermaid);
        Assert.Contains("\"📥 reg-q\"", mermaid);
        Assert.Contains("\"📥 all-q\"", mermaid);
        Assert.DoesNotContain("-->|task queue|", mermaid);

        // Queue-less, uncalled activities land in the orphan box at the bottom.
        Assert.Contains("Orphaned activities", mermaid);

        // Queue-less but called workflows land in the unknown-queue box.
        Assert.Contains("Unknown task queue", mermaid);
    }

    [Fact]
    public async Task MermaidLabelsEdgesWithCallOrderAndLoops()
    {
        var graph = await BuildAsync(OrderedLoopSource);
        var mermaid = TopologyEmitter.ToMermaid(graph);

        Assert.Contains("|\"#1, #3\"|", mermaid);
        Assert.Contains("|\"#2 🔁\"|", mermaid);
    }

    [Fact]
    public async Task DotGroupsNodesIntoTaskQueueClusters()
    {
        var graph = await BuildAsync(QueueEvidenceSource);
        var dot = TopologyEmitter.ToDot(graph);

        Assert.Contains("subgraph cluster_", dot);
        Assert.Contains("📥 reg-q", dot);
        Assert.DoesNotContain("label=\"task queue\"", dot);
        Assert.Contains("Orphaned activities", dot);
    }

    [Fact]
    public async Task MultiSolution_StitchesAcrossSolutions()
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
        var contractRef = MetadataReference.CreateFromImage(CompileAssembly(ContractSource, references, "Contract"));

        using var workspaceA = new AdhocWorkspace();
        var projectA = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "App",
            "App",
            LanguageNames.CSharp,
            metadataReferences: references.Add(contractRef));
        var documentA = DocumentInfo.Create(
            DocumentId.CreateNewId(projectA.Id),
            "Workflow.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(CrossRepoWorkflowSource), VersionStamp.Create())));
        var solutionA = workspaceA.CurrentSolution.AddProject(projectA).AddDocument(documentA);

        using var workspaceB = new AdhocWorkspace();
        var projectB = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "Contract",
            "Contract",
            LanguageNames.CSharp,
            metadataReferences: references);
        var documentB = DocumentInfo.Create(
            DocumentId.CreateNewId(projectB.Id),
            "Contract.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(ContractSource), VersionStamp.Create())));
        var solutionB = workspaceB.CurrentSolution.AddProject(projectB).AddDocument(documentB);

        var graph = await WorkflowTopologyBuilder.BuildAsync(new[] { solutionA, solutionB }, CancellationToken.None);

        // The activity called from solution A resolves to the [Activity] declared
        // in solution B — a real node with a source location, not a boundary node.
        var activity = Assert.Single(graph.Nodes, n => n.Id.StartsWith("Activity:Shared.SharedActivities.Do"));
        Assert.NotNull(activity.File);

        Assert.DoesNotContain(graph.Nodes, n => n.Kind == TopologyNodeKinds.Unknown && n.Name.Contains("SharedActivities"));
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Activity &&
                                         e.From == "Workflow:MyWorkflow" &&
                                         e.To == activity.Id);
    }

    private const string FacadeSource = """
        using Microsoft.Extensions.DependencyInjection;

        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { }
            public sealed class ChildWorkflowOptions { }
            public sealed class ChildWorkflowHandle { }

            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Kogoshvili.Temporal.Hosting
        {
            public static class ActivityOps
            {
                public static System.Threading.Tasks.Task<TResult> ExecuteAsync<TResult>(
                    System.Linq.Expressions.Expression<System.Func<TResult>> activityCall, string? preset = null)
                    => System.Threading.Tasks.Task.FromResult<TResult>(default!);

                public static System.Threading.Tasks.Task<TResult> ExecuteLocalAsync<TResult>(
                    System.Linq.Expressions.Expression<System.Func<TResult>> activityCall, string? preset = null)
                    => System.Threading.Tasks.Task.FromResult<TResult>(default!);
            }

            public static class ChildWorkflowOps
            {
                public static System.Threading.Tasks.Task<TResult> ExecuteAsync<TWorkflow, TResult>(
                    System.Linq.Expressions.Expression<System.Func<TWorkflow, System.Threading.Tasks.Task<TResult>>> runCall,
                    ChildWorkflowOptions? options = null)
                    => System.Threading.Tasks.Task.FromResult<TResult>(default!);

                public static System.Threading.Tasks.Task<TResult> ExecuteAsync<TWorkflow, TParams, TResult>(
                    TParams args, ChildWorkflowOptions? options = null)
                    => System.Threading.Tasks.Task.FromResult<TResult>(default!);

                public static System.Threading.Tasks.Task<TResult> ExecuteAsync<TWorkflow, TResult>(
                    ChildWorkflowOptions? options = null)
                    => System.Threading.Tasks.Task.FromResult<TResult>(default!);
            }
        }

        namespace Microsoft.Extensions.DependencyInjection
        {
            public static class TemporalServiceCollectionExtensions
            {
                public static object AddTemporalWorker(this object services, string taskQueue) => services;
                public static object AddWorkflow<TWorkflow>(this object builder) => builder;
                public static object AddDiscoveredTypes(this object builder) => builder;
            }
        }

        [Temporalio.Workflows.Workflow]
        public class MyWorkflow
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task<string> Run()
            {
                var a = await Kogoshvili.Temporal.Hosting.ActivityOps.ExecuteAsync(() => MyActivities.Do());
                var l = await Kogoshvili.Temporal.Hosting.ActivityOps.ExecuteLocalAsync(() => MyActivities.LocalDo());
                var c1 = await Kogoshvili.Temporal.Hosting.ChildWorkflowOps.ExecuteAsync<Child, string>((Child wf) => wf.Run());
                var c2 = await Kogoshvili.Temporal.Hosting.ChildWorkflowOps.ExecuteAsync<ChildGeneric, string, string>("input");
                var c3 = await Kogoshvili.Temporal.Hosting.ChildWorkflowOps.ExecuteAsync<ChildNoArg, string>();
                return a + l + c1 + c2 + c3;
            }
        }

        [Temporalio.Workflows.Workflow]
        public class Child
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task<string> Run() => "child";
        }

        [Temporalio.Workflows.Workflow]
        public class ChildGeneric
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task<string> Run() => "child-generic";
        }

        [Temporalio.Workflows.Workflow]
        public class ChildNoArg
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task<string> Run() => "child-noarg";
        }

        public static class MyActivities
        {
            [Temporalio.Activities.Activity]
            public static string Do() => "do";

            [Temporalio.Activities.Activity]
            public static string LocalDo() => "local";
        }

        public static class Setup
        {
            public static void Register(object services)
            {
                services
                    .AddTemporalWorker("my-queue")
                    .AddWorkflow<MyWorkflow>()
                    .AddDiscoveredTypes();
            }
        }
        """;

    [Fact]
    public async Task FacadeApisProduceActivityAndChildEdges()
    {
        var graph = await BuildAsync(FacadeSource);

        var doActivity = Assert.Single(graph.Nodes, n => n.Kind == TopologyNodeKinds.Activity && n.Name == "MyActivities.Do");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Activity && e.From == "Workflow:MyWorkflow" && e.To == doActivity.Id);

        var localActivity = Assert.Single(graph.Nodes, n => n.Kind == TopologyNodeKinds.Activity && n.Name == "MyActivities.LocalDo");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.LocalActivity && e.From == "Workflow:MyWorkflow" && e.To == localActivity.Id);

        // Lambda, single-parameter generic, and no-argument overloads each resolve
        // their own child workflow type.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.ChildWorkflow && e.To == "Workflow:Child");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.ChildWorkflow && e.To == "Workflow:ChildGeneric");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.ChildWorkflow && e.To == "Workflow:ChildNoArg");
    }

    [Fact]
    public async Task HostedWorkerRegistrationAssociatesTaskQueue()
    {
        var graph = await BuildAsync(FacadeSource);

        Assert.Contains(graph.Nodes, n => n.Id == "TaskQueue:my-queue");

        // Explicit .AddWorkflow<T>() registration.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue && e.From == "Workflow:MyWorkflow" && e.To == "TaskQueue:my-queue");

        // .AddDiscoveredTypes() associates the compilation's remaining workflows.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue && e.From == "Workflow:Child" && e.To == "TaskQueue:my-queue");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue && e.From == "Workflow:ChildGeneric" && e.To == "TaskQueue:my-queue");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue && e.From == "Workflow:ChildNoArg" && e.To == "TaskQueue:my-queue");
    }

    private const string QueueEvidenceSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { public string? TaskQueue { get; set; } }

            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Temporalio.Worker
        {
            public sealed class TemporalWorkerOptions
            {
                public TemporalWorkerOptions(string taskQueue) { }
                public TemporalWorkerOptions AddWorkflow<TWorkflow>() => this;
                public TemporalWorkerOptions AddActivity(System.Delegate del) => this;
                public TemporalWorkerOptions AddAllActivities<TActivity>(TActivity? instance) => this;
                public TemporalWorkerOptions AddAllActivities(System.Type type, object? instance) => this;
            }
        }

        [Temporalio.Workflows.Workflow]
        public class QWorkflow
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task Run()
            {
                await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                    () => QA.Act1(), new Temporalio.Workflows.ActivityOptions { TaskQueue = "route-q" });
            }
        }

        public sealed class QA
        {
            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task Act1() => System.Threading.Tasks.Task.CompletedTask;

            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task Act2() => System.Threading.Tasks.Task.CompletedTask;

            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task Act3() => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed class QOrphan
        {
            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task Never() => System.Threading.Tasks.Task.CompletedTask;
        }

        public static class QSetup
        {
            public static Temporalio.Worker.TemporalWorkerOptions Lambda() =>
                new Temporalio.Worker.TemporalWorkerOptions("reg-q").AddActivity(() => QA.Act1());

            public static Temporalio.Worker.TemporalWorkerOptions Generic() =>
                new Temporalio.Worker.TemporalWorkerOptions("all-q").AddAllActivities<QA>(null);

            public static Temporalio.Worker.TemporalWorkerOptions TypeOf() =>
                new Temporalio.Worker.TemporalWorkerOptions("all-q2").AddAllActivities(typeof(QA), null);
        }
        """;

    private const string OrderedLoopSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { public string? TaskQueue { get; set; } }

            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        [Temporalio.Workflows.Workflow]
        public class OLWorkflow
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task Run()
            {
                var opts = new Temporalio.Workflows.ActivityOptions();
                await Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => OL.Act1(), opts);
                foreach (var i in new[] { 1, 2, 3 })
                {
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => OL.Act2(), opts);
                }

                await Temporalio.Workflows.Workflow.ExecuteActivityAsync(() => OL.Act1(), opts);
            }
        }

        public static class OL
        {
            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task Act1() => System.Threading.Tasks.Task.CompletedTask;

            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task Act2() => System.Threading.Tasks.Task.CompletedTask;
        }
        """;

    private const string MultiQueueSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { }

            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Temporalio.Worker
        {
            public sealed class TemporalWorkerOptions
            {
                public TemporalWorkerOptions(string taskQueue) { }
                public TemporalWorkerOptions AddWorkflow<TWorkflow>() => this;
            }
        }

        [Temporalio.Workflows.Workflow]
        public class DualWorkflow
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task Run()
            {
                await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                    () => MQ.Act(), new Temporalio.Workflows.ActivityOptions());
            }
        }

        public static class MQ
        {
            [Temporalio.Activities.Activity]
            public static System.Threading.Tasks.Task Act() => System.Threading.Tasks.Task.CompletedTask;
        }

        public static class MQSetup
        {
            public static Temporalio.Worker.TemporalWorkerOptions A() =>
                new Temporalio.Worker.TemporalWorkerOptions("queue-a").AddWorkflow<DualWorkflow>();

            public static Temporalio.Worker.TemporalWorkerOptions C() =>
                new Temporalio.Worker.TemporalWorkerOptions("queue-c").AddWorkflow<DualWorkflow>();
        }
        """;

    [Fact]
    public async Task SdkHostedWorkerRegistrationsAssociateTaskQueue()
    {
        var graph = await BuildAsync(SdkHostedSource);

        Assert.Contains(graph.Nodes, n => n.Id == "TaskQueue:hosted-q");

        // .AddWorkflow<T>() chained off AddHostedTemporalWorker.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From == "Workflow:HWorkflow" && e.To == "TaskQueue:hosted-q");

        // .AddScopedActivities<T>() registers the type's activity methods.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From.StartsWith("Activity:HActivities.HAct", StringComparison.Ordinal) && e.To == "TaskQueue:hosted-q");
    }

    private const string SdkHostedSource = """
        using Microsoft.Extensions.DependencyInjection;
        using Temporalio.Extensions.Hosting;

        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Temporalio.Extensions.Hosting
        {
            public interface ITemporalWorkerServiceOptionsBuilder { }
            public sealed class TemporalWorkerServiceOptionsBuilder : ITemporalWorkerServiceOptionsBuilder { }

            public static class TemporalWorkerServiceOptionsBuilderExtensions
            {
                public static ITemporalWorkerServiceOptionsBuilder AddWorkflow<TWorkflow>(
                    this ITemporalWorkerServiceOptionsBuilder builder) => builder;

                public static ITemporalWorkerServiceOptionsBuilder AddScopedActivities<TActivities>(
                    this ITemporalWorkerServiceOptionsBuilder builder) => builder;
            }
        }

        namespace Microsoft.Extensions.DependencyInjection
        {
            public sealed class StubServiceCollection { }

            public static class TemporalHostingServiceCollectionExtensions
            {
                public static Temporalio.Extensions.Hosting.ITemporalWorkerServiceOptionsBuilder AddHostedTemporalWorker(
                    this StubServiceCollection services,
                    string clientTargetHost,
                    string clientNamespace,
                    string taskQueue)
                    => new Temporalio.Extensions.Hosting.TemporalWorkerServiceOptionsBuilder();
            }
        }

        [Temporalio.Workflows.Workflow]
        public class HWorkflow
        {
            [Temporalio.Workflows.WorkflowRun]
            public async System.Threading.Tasks.Task Run() => "ok";
        }

        public sealed class HActivities
        {
            [Temporalio.Activities.Activity]
            public System.Threading.Tasks.Task HAct() => System.Threading.Tasks.Task.CompletedTask;
        }

        public sealed class HostedSetup
        {
            public Microsoft.Extensions.DependencyInjection.StubServiceCollection Services { get; } = new();

            public void Configure()
            {
                Services
                    .AddHostedTemporalWorker("localhost:7233", "default", "hosted-q")
                    .AddWorkflow<HWorkflow>()
                    .AddScopedActivities<HActivities>();
            }
        }
        """;

    [Fact]
    public async Task InterfaceCallsResolveToImplementation()
    {
        var graph = await BuildAsync(ContractsSource);

        // Unambiguous: the interface-typed call resolves to the impl member.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Activity &&
            e.From == "Workflow:Main.CwWorkflow" && e.To.StartsWith("Activity:Main.UniqueActivities.Run", StringComparison.Ordinal));
        Assert.DoesNotContain(graph.Nodes, n => n.Kind == TopologyNodeKinds.Unknown && n.Name.Contains("UniqueActivities"));
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.ChildWorkflow &&
            e.From == "Workflow:Main.CwWorkflow" && e.To == "Workflow:Main.CwChild");

        // Ambiguous: two impls of the same interface member → contract node.
        var contract = Assert.Single(graph.Nodes, n => n.Kind == TopologyNodeKinds.Contract);
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Activity && e.To == contract.Id);

        // Contract-side declaration nodes are superseded by their impls.
        Assert.DoesNotContain(graph.Nodes, n => n.Id == "Workflow:Main.IWf");
        Assert.DoesNotContain(graph.Nodes, n => n.Id == "Activity:Main.IUniqueAct.Run(string)");
        Assert.DoesNotContain(graph.Nodes, n => n.Id == "Activity:Main.ISharedAct.Run(string)");
    }

    [Fact]
    public async Task ClientCallsProduceCallerNodesWithEdges()
    {
        var graph = await BuildAsync(ClientCallsSource);

        var caller = Assert.Single(graph.Nodes, n => n.Kind == TopologyNodeKinds.Caller);
        Assert.Equal("Program", caller.Name);
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Signal && e.From == caller.Id && e.To == "Workflow:Main.CwWorkflow");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Query && e.From == caller.Id);
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Update && e.From == caller.Id);
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.StartWorkflow && e.From == caller.Id);
    }

    [Fact]
    public async Task ExternalWorkflowSignalsProduceEdges()
    {
        var graph = await BuildAsync(ExternalSignalSource);

        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Signal &&
            e.From == "Workflow:Main.CwWorkflow" && e.To == "Workflow:Main.CwOther");
    }

    [Fact]
    public async Task StandaloneActivityCallsAreDetected()
    {
        var graph = await BuildAsync(StandaloneSource);

        var activity = Assert.Single(graph.Nodes, n => n.Kind == TopologyNodeKinds.Activity && n.Id.StartsWith("Activity:Main.CwActivities.Run", StringComparison.Ordinal));
        Assert.True(activity.Standalone);
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.StandaloneActivity &&
            e.From.StartsWith("Caller:", StringComparison.Ordinal) && e.To == activity.Id);
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.To == "TaskQueue:sa-q" && e.From == activity.Id);
    }

    [Fact]
    public async Task ActivitiesInheritCallerTaskQueue()
    {
        // The activity has no registration or explicit routing, but its caller
        // workflow is registered on a queue — SDK semantics inherit it.
        var graph = await BuildAsync(InheritanceSource);
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.TaskQueue &&
            e.From.StartsWith("Activity:Main.IaActivities.Run", StringComparison.Ordinal) && e.To == "TaskQueue:inh-q");
    }

    [Fact]
    public async Task HeartbeatsAreDetectedAndIssuesFlagged()
    {
        var graph = await BuildAsync(HeartbeatSource);

        var good = Assert.Single(graph.Nodes, n => n.Kind == TopologyNodeKinds.Activity && n.Name == "HbActivities.HeartbeatAct");
        Assert.True(good.Heartbeats);
        var goodEdge = graph.Edges.Single(e => e.Kind == TopologyEdgeKinds.Activity && e.To == good.Id);
        Assert.True(goodEdge.Heartbeats);
        Assert.Contains("HeartbeatTimeout=10s", goodEdge.CallOptions);
        Assert.Contains("Retry:max3", goodEdge.CallOptions);

        var bad = Assert.Single(graph.Nodes, n => n.Kind == TopologyNodeKinds.Activity && n.Name == "HbActivities.NoHeartbeatAct");
        Assert.Null(bad.Heartbeats);
        var badEdge = graph.Edges.Single(e => e.Kind == TopologyEdgeKinds.Activity && e.To == bad.Id);
        Assert.True(badEdge.HeartbeatIssue);

        var mermaid = TopologyEmitter.ToMermaid(graph);
        var linkLines = mermaid.Split('\n')
            .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, "^\\s+n\\d+ ") &&
                        (l.Contains("-->") || l.Contains("--x") || l.Contains("<-->") ||
                         l.Contains("-.->") || l.Contains("==>")))
            .ToList();
        var issueIndex = linkLines.FindIndex(l => l.Contains("--x"));
        Assert.True(issueIndex >= 0);
        Assert.Contains($"linkStyle {issueIndex} ", mermaid);
    }

    [Fact]
    public void UnrestoredProjectsAreDetected()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ts-map-test-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var restoredDir = System.IO.Path.Combine(dir, "Restored");
            var unrestoredDir = System.IO.Path.Combine(dir, "Unrestored");
            System.IO.Directory.CreateDirectory(restoredDir);
            System.IO.Directory.CreateDirectory(unrestoredDir);
            var restored = System.IO.Path.Combine(restoredDir, "Restored.csproj");
            var unrestored = System.IO.Path.Combine(unrestoredDir, "Unrestored.csproj");
            System.IO.File.WriteAllText(restored, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            System.IO.File.WriteAllText(unrestored, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(restoredDir, "obj"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(restoredDir, "obj", "project.assets.json"), "{}");

            var missing = ProjectRestoreCheck.FindUnrestoredProjects(new[] { restored, unrestored });
            Assert.Equal(new[] { unrestored }, missing);
        }
        finally
        {
            System.IO.Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task ContractsFlagAddsSignaturesAndCallOptions()
    {
        var graph = await BuildAsync(ContractsSource);
        var with = TopologyEmitter.ToMermaid(graph, contracts: true);
        Assert.Contains("run: RunAsync", with);
        Assert.Contains("StartToClose", with);

        var without = TopologyEmitter.ToMermaid(graph, contracts: false);
        Assert.DoesNotContain("→ Task<", without);
        Assert.DoesNotContain("StartToClose", without);
    }

    [Fact]
    public async Task LocalActivityEdgesUseDistinctArrow()
    {
        var graph = await BuildAsync(StandaloneSource);
        var mermaid = TopologyEmitter.ToMermaid(graph);
        Assert.Contains("--o", mermaid);
    }

    [Fact]
    public async Task MermaidIncludesLegend()
    {
        var graph = await BuildAsync(Source);
        var mermaid = TopologyEmitter.ToMermaid(graph);
        Assert.Contains("Legend", mermaid);
    }

    private const string ContractsSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { public System.TimeSpan? StartToCloseTimeout { get; set; } }

            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task ExecuteActivityAsync<TActivity>(
                    System.Linq.Expressions.Expression<System.Func<TActivity, object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task<TResult> StartChildWorkflowAsync<TWorkflow, TResult>(
                    System.Linq.Expressions.Expression<System.Func<TWorkflow, System.Threading.Tasks.Task<TResult>>> workflowRunCall)
                    => default!;
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Main
        {
            [Temporalio.Workflows.Workflow]
            public interface IWf
            {
                [Temporalio.Workflows.WorkflowRun]
                System.Threading.Tasks.Task<string> RunAsync();
            }

            public interface IUniqueAct
            {
                [Temporalio.Activities.Activity]
                System.Threading.Tasks.Task<string> Run(string input);
            }

            public interface ISharedAct
            {
                [Temporalio.Activities.Activity]
                System.Threading.Tasks.Task<string> Run(string input);
            }

            [Temporalio.Workflows.Workflow]
            public class CwWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task<string> RunAsync()
                {
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        (IUniqueAct a) => a.Run("x"), new Temporalio.Workflows.ActivityOptions { StartToCloseTimeout = System.TimeSpan.FromSeconds(30) });
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        (ISharedAct a) => a.Run("x"), new Temporalio.Workflows.ActivityOptions());
                    await Temporalio.Workflows.Workflow.StartChildWorkflowAsync<IWf, string>((IWf w) => w.RunAsync());
                    return "ok";
                }
            }

            [Temporalio.Workflows.Workflow]
            public class CwChild : IWf
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task<string> RunAsync() => System.Threading.Tasks.Task.FromResult("child");
            }

            public sealed class UniqueActivities : IUniqueAct
            {
                public System.Threading.Tasks.Task<string> Run(string input) => System.Threading.Tasks.Task.FromResult(input);
            }

            public sealed class SharedA : ISharedAct
            {
                public System.Threading.Tasks.Task<string> Run(string input) => System.Threading.Tasks.Task.FromResult(input);
            }

            public sealed class SharedB : ISharedAct
            {
                public System.Threading.Tasks.Task<string> Run(string input) => System.Threading.Tasks.Task.FromResult(input);
            }
        }
        """;

    private const string InheritanceSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { }

            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Temporalio.Worker
        {
            public sealed class TemporalWorkerOptions
            {
                public TemporalWorkerOptions(string taskQueue) { }
                public TemporalWorkerOptions AddWorkflow<TWorkflow>() => this;
            }
        }

        namespace Main
        {
            [Temporalio.Workflows.Workflow]
            public class IaWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task RunAsync()
                {
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        () => IaActivities.Run(), new Temporalio.Workflows.ActivityOptions());
                }
            }

            public static class IaActivities
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task Run() => System.Threading.Tasks.Task.CompletedTask;
            }

            public static class IaSetup
            {
                public static Temporalio.Worker.TemporalWorkerOptions Create() =>
                    new Temporalio.Worker.TemporalWorkerOptions("inh-q").AddWorkflow<IaWorkflow>();
            }
        }
        """;

    private const string ClientCallsSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class WorkflowSignalAttribute : System.Attribute { }
            public sealed class WorkflowQueryAttribute : System.Attribute { }
            public sealed class WorkflowUpdateAttribute : System.Attribute { }
        }

        namespace Temporalio.Client
        {
            public sealed class WorkflowHandle<TWorkflow>
            {
                public System.Threading.Tasks.Task SignalAsync(System.Linq.Expressions.Expression<System.Action<TWorkflow>> signalCall) => System.Threading.Tasks.Task.CompletedTask;

                public System.Threading.Tasks.Task<TQueryResult> QueryAsync<TQueryResult>(System.Linq.Expressions.Expression<System.Func<TWorkflow, TQueryResult>> queryCall) => default!;

                public System.Threading.Tasks.Task StartUpdateAsync(System.Linq.Expressions.Expression<System.Action<TWorkflow>> updateCall) => System.Threading.Tasks.Task.CompletedTask;
            }

            public sealed class TemporalClient
            {
                public WorkflowHandle<TWorkflow> GetWorkflowHandle<TWorkflow>(string id) => new();

                public System.Threading.Tasks.Task StartWorkflowAsync(string workflow, object?[]? args, string? queue) => System.Threading.Tasks.Task.CompletedTask;
            }
        }

        namespace Main
        {
            [Temporalio.Workflows.Workflow]
            public class CwWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task RunAsync() => System.Threading.Tasks.Task.CompletedTask;

                [Temporalio.Workflows.WorkflowSignal]
                public System.Threading.Tasks.Task Approve() => System.Threading.Tasks.Task.CompletedTask;

                [Temporalio.Workflows.WorkflowQuery]
                public string Status() => "ok";

                [Temporalio.Workflows.WorkflowUpdate]
                public System.Threading.Tasks.Task Rename() => System.Threading.Tasks.Task.CompletedTask;
            }

            public static class Program
            {
                public static async System.Threading.Tasks.Task Main()
                {
                    var client = new Temporalio.Client.TemporalClient();
                    var handle = client.GetWorkflowHandle<CwWorkflow>("wf-1");
                    await handle.SignalAsync((CwWorkflow w) => w.Approve());
                    await handle.QueryAsync((CwWorkflow w) => w.Status());
                    await handle.StartUpdateAsync((CwWorkflow w) => w.Rename());
                    await client.StartWorkflowAsync("CwWorkflow", null, "q");
                }
            }
        }
        """;

    private const string ExternalSignalSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class WorkflowSignalAttribute : System.Attribute { }

            public sealed class ExternalWorkflowHandle<TWorkflow>
            {
                public System.Threading.Tasks.Task SignalAsync(System.Linq.Expressions.Expression<System.Action<TWorkflow>> signalCall) => System.Threading.Tasks.Task.CompletedTask;
            }

            public static class Workflow
            {
                public static ExternalWorkflowHandle<TWorkflow> GetExternalWorkflowHandle<TWorkflow>(string id) => new();
            }
        }

        namespace Main
        {
            [Temporalio.Workflows.Workflow]
            public class CwWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task RunAsync()
                {
                    var handle = Temporalio.Workflows.Workflow.GetExternalWorkflowHandle<CwOther>("other");
                    await handle.SignalAsync((CwOther w) => w.Poke());
                }
            }

            [Temporalio.Workflows.Workflow]
            public class CwOther
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task RunAsync() => System.Threading.Tasks.Task.CompletedTask;

                [Temporalio.Workflows.WorkflowSignal]
                public System.Threading.Tasks.Task Poke() => System.Threading.Tasks.Task.CompletedTask;
            }
        }
        """;

    private const string StandaloneSource = """
        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Temporalio.Client
        {
            public sealed class StartActivityOptions
            {
                public StartActivityOptions(string id, string taskQueue) { }
                public string? TaskQueue { get; set; }
            }

            public sealed class TemporalClient
            {
                public System.Threading.Tasks.Task<object?> StartActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, StartActivityOptions options)
                    => System.Threading.Tasks.Task.FromResult<object?>(null);
            }
        }

        namespace Main
        {
            public static class CwActivities
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task<string> Run(string input) => System.Threading.Tasks.Task.FromResult(input);
            }

            public static class Program
            {
                public static async System.Threading.Tasks.Task Main()
                {
                    var client = new Temporalio.Client.TemporalClient();
                    await client.StartActivityAsync(
                        () => CwActivities.Run("x"),
                        new Temporalio.Client.StartActivityOptions("act-1", "sa-q"));
                }
            }
        }
        """;

    private const string HeartbeatSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions
            {
                public System.TimeSpan? HeartbeatTimeout { get; set; }
                public RetryPolicy? RetryPolicy { get; set; }
            }

            public sealed class RetryPolicy
            {
                public int? MaximumAttempts { get; set; }
            }

            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }

            public sealed class ActivityExecutionContext
            {
                public static ActivityExecutionContext Current { get; } = new();
                public void Heartbeat(params object?[] details) { }
            }
        }

        namespace Main
        {
            [Temporalio.Workflows.Workflow]
            public class HbWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task RunAsync()
                {
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        () => HbActivities.HeartbeatAct(), new Temporalio.Workflows.ActivityOptions
                        {
                            HeartbeatTimeout = System.TimeSpan.FromSeconds(10),
                            RetryPolicy = new() { MaximumAttempts = 3 },
                        });
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        () => HbActivities.NoHeartbeatAct(), new Temporalio.Workflows.ActivityOptions { HeartbeatTimeout = System.TimeSpan.FromSeconds(10) });
                }
            }

            public static class HbActivities
            {
                [Temporalio.Activities.Activity]
                public static async System.Threading.Tasks.Task HeartbeatAct()
                {
                    Temporalio.Activities.ActivityExecutionContext.Current.Heartbeat(1);
                    await System.Threading.Tasks.Task.CompletedTask;
                }

                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task NoHeartbeatAct() => System.Threading.Tasks.Task.CompletedTask;
            }
        }
        """;

    private const string EnvQueueSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
        }

        namespace Temporalio.Worker
        {
            public sealed class TemporalWorkerOptions
            {
                public TemporalWorkerOptions(string taskQueue) { }
                public TemporalWorkerOptions AddWorkflow<TWorkflow>() => this;
            }
        }

        namespace Main
        {
            [Temporalio.Workflows.Workflow]
            public class EnvWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task RunAsync() => System.Threading.Tasks.Task.CompletedTask;
            }

            public static class EnvSetup
            {
                public static Temporalio.Worker.TemporalWorkerOptions Create() =>
                    new Temporalio.Worker.TemporalWorkerOptions(GetEnvVarWithDefault("TEMPORAL_TASK_QUEUE", "orders-fallback"))
                        .AddWorkflow<EnvWorkflow>();

                public static string GetEnvVarWithDefault(string name, string fallback) => fallback;
            }
        }
        """;

    [Fact]
    public async Task StringCallsResolveThroughSdkNameRules()
    {
        var graph = await BuildAsync(NameMatchingSource);

        // Explicit [Activity("CustomName")] matches the string call.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Activity &&
            e.To.StartsWith("Activity:Main.NamedActivities.Custom", StringComparison.Ordinal));

        // Default activity name = method name verbatim (no Async trim).
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Activity &&
            e.To.StartsWith("Activity:Main.NamedActivities.DoAsync", StringComparison.Ordinal));
        Assert.DoesNotContain(graph.Edges, e => e.Kind == TopologyEdgeKinds.Activity &&
            e.To.StartsWith("Activity:Main.NamedActivities.Do(", StringComparison.Ordinal));

        // Workflow interface name trim: "OrderWf" resolves to the impl of IOrderWf.
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.ChildWorkflow && e.To == "Workflow:Main.OrderWfImpl");

        // Explicit [Workflow("CustomWf")].
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.ChildWorkflow && e.To == "Workflow:Main.NamedWfImpl");
    }

    [Fact]
    public async Task StringClientOpsResolveViaNameIndex()
    {
        var graph = await BuildAsync(StringClientOpsSource);

        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Signal && e.From.StartsWith("Caller:", StringComparison.Ordinal) && e.To == "Workflow:Main.CwWorkflow");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Query && e.To == "Workflow:Main.CwWorkflow");
        Assert.Contains(graph.Edges, e => e.Kind == TopologyEdgeKinds.Update && e.To == "Workflow:Main.CwWorkflow");
        // The Async suffix is trimmed for signal names: "Approve" matches ApproveAsync.
    }

    [Fact]
    public async Task ContractsWithoutImplementationsAreMarkedUnresolved()
    {
        var graph = await BuildAsync(NoImplContractSource);

        var activity = Assert.Single(graph.Nodes, n => n.Id.StartsWith("Activity:Main.IGhostActivities.Run", StringComparison.Ordinal));
        Assert.True(activity.Unresolved);
        var workflow = Assert.Single(graph.Nodes, n => n.Id.StartsWith("Workflow:Main.IGhostWf", StringComparison.Ordinal));
        Assert.True(workflow.Unresolved);
    }

    [Fact]
    public async Task NodesCarryRepoAndPathFromInputSolutions()
    {
        var graph = await BuildAsync(Source, inputPaths: new[] { "/repo/MyApp/MyApp.sln" });

        var workflow = Assert.Single(graph.Nodes, n => n.Id == "Workflow:MyWorkflow");
        Assert.Equal("MyApp", workflow.Repo);
        Assert.NotNull(workflow.Path);
        Assert.EndsWith(".cs", workflow.Path);
    }

    [Fact]
    public async Task MermaidDuplicatesMultiQueueNodesIntoBoxes()
    {
        var graph = await BuildAsync(MultiQueueSource);
        var mermaid = TopologyEmitter.ToMermaid(graph);

        // The dual-queue workflow appears inside BOTH boxes (duplicated), and
        // pointer edges to queue boxes are gone.
        Assert.Equal(2, mermaid.Split("DualWorkflow").Length - 1);
        Assert.DoesNotContain(" --> q0", mermaid);
        Assert.DoesNotContain(" --> q1", mermaid);
    }

    [Fact]
    public async Task MermaidChainsLegendFirstAndOrphansLast()
    {
        var graph = await BuildAsync(QueueEvidenceSource);
        var mermaid = TopologyEmitter.ToMermaid(graph);

        Assert.Contains("legend ~~~ ", mermaid);
        Assert.Contains(" ~~~ uq", mermaid);
        Assert.Contains("uq ~~~ orp", mermaid);
    }

    [Fact]
    public async Task MermaidMarksUnknownAndUnresolvedNodes()
    {
        var graph = await BuildAsync(NoImplContractSource);
        var mermaid = TopologyEmitter.ToMermaid(graph);

        // No-impl contract members carry the light question mark.
        Assert.Contains("❔", mermaid);
    }

    private const string NameMatchingSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { public WorkflowAttribute() { } public WorkflowAttribute(string name) { } }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { }
            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    string activity, System.Collections.Generic.IReadOnlyCollection<object?>? args, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;

                public static System.Threading.Tasks.Task StartChildWorkflowAsync(string workflow, object?[]? args)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { public ActivityAttribute() { } public ActivityAttribute(string name) { } }
        }

        namespace Main
        {
            [Temporalio.Workflows.Workflow]
            public class NmWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task RunAsync()
                {
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync("CustomName", null, new Temporalio.Workflows.ActivityOptions());
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync("DoAsync", null, new Temporalio.Workflows.ActivityOptions());
                    await Temporalio.Workflows.Workflow.StartChildWorkflowAsync("OrderWf", null);
                    await Temporalio.Workflows.Workflow.StartChildWorkflowAsync("CustomWf", null);
                }
            }

            public static class NamedActivities
            {
                [Temporalio.Activities.Activity("CustomName")]
                public static System.Threading.Tasks.Task Custom() => System.Threading.Tasks.Task.CompletedTask;

                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task DoAsync() => System.Threading.Tasks.Task.CompletedTask;
            }

            [Temporalio.Workflows.Workflow]
            public interface IOrderWf
            {
                [Temporalio.Workflows.WorkflowRun]
                System.Threading.Tasks.Task RunAsync();
            }

            [Temporalio.Workflows.Workflow]
            public class OrderWfImpl : IOrderWf
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task RunAsync() => System.Threading.Tasks.Task.CompletedTask;
            }

            [Temporalio.Workflows.Workflow("CustomWf")]
            public class NamedWfImpl
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task RunAsync() => System.Threading.Tasks.Task.CompletedTask;
            }
        }
        """;

    private const string StringClientOpsSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class WorkflowSignalAttribute : System.Attribute { }
            public sealed class WorkflowQueryAttribute : System.Attribute { }
            public sealed class WorkflowUpdateAttribute : System.Attribute { }
        }

        namespace Temporalio.Client
        {
            public sealed class WorkflowHandle
            {
                public System.Threading.Tasks.Task SignalAsync(string signal, object?[]? args) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task<TQueryResult> QueryAsync<TQueryResult>(string query, object?[]? args) => default!;
                public System.Threading.Tasks.Task StartUpdateAsync(string update, object?[]? args) => System.Threading.Tasks.Task.CompletedTask;
            }

            public sealed class TemporalClient
            {
                public WorkflowHandle GetWorkflowHandle(string id) => new();
            }
        }

        namespace Main
        {
            [Temporalio.Workflows.Workflow]
            public class CwWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public System.Threading.Tasks.Task RunAsync() => System.Threading.Tasks.Task.CompletedTask;

                [Temporalio.Workflows.WorkflowSignal]
                public System.Threading.Tasks.Task ApproveAsync() => System.Threading.Tasks.Task.CompletedTask;

                [Temporalio.Workflows.WorkflowQuery]
                public string Status() => "ok";

                [Temporalio.Workflows.WorkflowUpdate]
                public System.Threading.Tasks.Task RenameAsync() => System.Threading.Tasks.Task.CompletedTask;
            }

            public static class Program
            {
                public static async System.Threading.Tasks.Task Main()
                {
                    var client = new Temporalio.Client.TemporalClient();
                    var handle = client.GetWorkflowHandle("wf-1");
                    await handle.SignalAsync("Approve", null);
                    await handle.QueryAsync<string>("Status", null);
                    await handle.StartUpdateAsync("Rename", null);
                }
            }
        }
        """;

    private const string NoImplContractSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { }

            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync<TActivity>(
                    System.Linq.Expressions.Expression<System.Func<TActivity, object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
        }

        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }

        namespace Main
        {
            [Temporalio.Workflows.Workflow]
            public class GhostWorkflow
            {
                [Temporalio.Workflows.WorkflowRun]
                public async System.Threading.Tasks.Task RunAsync()
                {
                    await Temporalio.Workflows.Workflow.ExecuteActivityAsync(
                        (IGhostActivities a) => a.Run(), new Temporalio.Workflows.ActivityOptions());
                }
            }

            [Temporalio.Workflows.Workflow]
            public interface IGhostWf
            {
                [Temporalio.Workflows.WorkflowRun]
                System.Threading.Tasks.Task RunAsync();
            }

            public interface IGhostActivities
            {
                [Temporalio.Activities.Activity]
                System.Threading.Tasks.Task Run();
            }
        }
        """;

    private static byte[] CompileAssembly(string source, IReadOnlyList<MetadataReference> references, string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())));

        return stream.ToArray();
    }

    private const string ContractSource = """
        namespace Temporalio.Workflows
        {
            public sealed class WorkflowAttribute : System.Attribute { }
            public sealed class WorkflowRunAttribute : System.Attribute { }
            public sealed class ActivityOptions { }
            public static class Workflow
            {
                public static System.Threading.Tasks.Task ExecuteActivityAsync(
                    System.Linq.Expressions.Expression<System.Func<object?>> activityCall, ActivityOptions options)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
        }
        namespace Temporalio.Activities
        {
            public sealed class ActivityAttribute : System.Attribute { }
        }
        namespace Shared
        {
            public static class SharedActivities
            {
                [Temporalio.Activities.Activity]
                public static System.Threading.Tasks.Task Do() => System.Threading.Tasks.Task.CompletedTask;
            }
        }
        """;

    private const string CrossRepoWorkflowSource = """
        using Temporalio.Workflows;

        [Workflow]
        public class MyWorkflow
        {
            [WorkflowRun]
            public async System.Threading.Tasks.Task Run()
            {
                await Workflow.ExecuteActivityAsync(() => Shared.SharedActivities.Do(), new ActivityOptions());
            }
        }
        """;

    private static async Task<TopologyGraph> BuildAsync(string source, IReadOnlyList<string>? inputPaths = null)
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var project = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "Topology",
            "Topology",
            LanguageNames.CSharp,
            metadataReferences: references);

        var document = DocumentInfo.Create(
            DocumentId.CreateNewId(projectId),
            "Topology.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(source), VersionStamp.Create())));

        var solution = workspace.CurrentSolution
            .AddProject(project)
            .AddDocument(document);

        return await WorkflowTopologyBuilder.BuildAsync(solution, CancellationToken.None, inputPaths);
    }
}
