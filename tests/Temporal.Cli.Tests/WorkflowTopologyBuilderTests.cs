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
        Assert.Contains(workflow.Handlers, h => h is (TopologyHandlerKinds.Run, "Run"));
        Assert.Contains(workflow.Handlers, h => h is (TopologyHandlerKinds.Signal, "Approve"));
        Assert.Contains(workflow.Handlers, h => h is (TopologyHandlerKinds.Query, "Status"));
    }

    [Fact]
    public async Task EmitsMermaidAndJson()
    {
        var graph = await BuildAsync(Source);

        var mermaid = TopologyEmitter.ToMermaid(graph);
        Assert.StartsWith("flowchart TB", mermaid);
        Assert.Contains("classDef workflow", mermaid);
        Assert.Contains("-->|task queue|", mermaid);

        var json = TopologyEmitter.ToJson(graph);
        Assert.Contains("\"kind\": \"workflow\"", json);
        Assert.Contains("\"kind\": \"taskQueue\"", json);
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
        Assert.Contains("flowchart TB", html);
        Assert.Contains("\"kind\": \"workflow\"", html);
        Assert.Contains("classList", html); // click-to-highlight interactivity

        var dot = TopologyEmitter.ToDot(graph);
        Assert.StartsWith("digraph temporal_topology", dot);
        Assert.Contains("shape=box", dot);
        Assert.Contains("shape=ellipse", dot);
        Assert.Contains(" -> ", dot);
        Assert.Contains("label=\"task queue\"", dot);
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

    private static async Task<TopologyGraph> BuildAsync(string source)
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

        return await WorkflowTopologyBuilder.BuildAsync(solution, CancellationToken.None);
    }
}
