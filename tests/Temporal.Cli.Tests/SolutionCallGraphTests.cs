using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Kogoshvili.Temporal.Cli.Analysis;

namespace Kogoshvili.Temporal.Cli.Tests;

public class SolutionCallGraphTests
{
    [Fact]
    public async Task CrossProjectWorkflowToHelper_IsReachable()
    {
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);

        using var workspace = new AdhocWorkspace();

        var helperProject = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "Helper",
            "Helper",
            LanguageNames.CSharp,
            metadataReferences: references);

        var workflowProject = ProjectInfo.Create(
            ProjectId.CreateNewId(),
            VersionStamp.Create(),
            "Workflow",
            "Workflow",
            LanguageNames.CSharp,
            metadataReferences: references,
            projectReferences: new[] { new ProjectReference(helperProject.Id) });

        var solution = workspace.CurrentSolution
            .AddProject(helperProject)
            .AddProject(workflowProject);

        var helperDoc = DocumentInfo.Create(
            DocumentId.CreateNewId(helperProject.Id),
            "Helper.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("""
                public static class Helper
                {
                    public static void DoWork()
                    {
                        var g = System.Guid.NewGuid();
                    }
                }
                """), VersionStamp.Create())));

        var workflowDoc = DocumentInfo.Create(
            DocumentId.CreateNewId(workflowProject.Id),
            "Workflow.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From("""
                [Temporalio.Workflows.Workflow]
                public class W
                {
                    [Temporalio.Workflows.WorkflowRun]
                    public void Run()
                    {
                        Helper.DoWork();
                    }
                }

                namespace Temporalio.Workflows
                {
                    public sealed class WorkflowAttribute : System.Attribute { }
                    public sealed class WorkflowRunAttribute : System.Attribute { }
                }
                """), VersionStamp.Create())));

        solution = solution
            .AddDocument(helperDoc)
            .AddDocument(workflowDoc);

        var reachable = await SolutionCallGraph.ComputeReachableAsync(solution, CancellationToken.None);

        Assert.Contains(reachable, k => k.Contains("Helper.DoWork"));
        Assert.Contains(reachable, k => k.Contains("Guid.NewGuid"));
    }
}
