using Kogoshvili.Temporal.Cli.Analysis;
using Microsoft.CodeAnalysis;

namespace Kogoshvili.Temporal.Cli.Map;

/// <summary>
/// The <c>temporal-sharp map</c> subcommand: produces a static topology graph of
/// the workflows, activities, child workflows, nexus operations, and task queues
/// in a solution.
/// </summary>
internal static class MapCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        MapOptions options;
        try
        {
            options = MapOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            MapOptions.PrintUsage(Console.Error);
            return 2;
        }

        try
        {
            var paths = MapOptions.ResolvePaths(options.Paths);
            var solutions = await ProjectLoader.LoadAsync(paths, CancellationToken.None).ConfigureAwait(false);
            if (!options.IncludeTests)
            {
                solutions = solutions.Select(RemoveTestProjects).ToArray();
            }

            var graph = await WorkflowTopologyBuilder.BuildAsync(solutions, CancellationToken.None).ConfigureAwait(false);

            var title = string.Join(", ", paths);
            var content = options.Format switch
            {
                MapOutputFormat.Json => TopologyEmitter.ToJson(graph),
                MapOutputFormat.Html => TopologyEmitter.ToHtml(graph, title, options.Contracts),
                MapOutputFormat.Dot => TopologyEmitter.ToDot(graph, options.Contracts),
                MapOutputFormat.Markdown => RenderMarkdown(graph, options.Contracts),
                _ => TopologyEmitter.ToMermaid(graph, options.Contracts),
            };

            if (options.Output is not null)
            {
                File.WriteAllText(options.Output, content);
                Console.Out.WriteLine($"Wrote {options.Output}");
            }
            else
            {
                Console.Out.WriteLine(content);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    /// <summary>
    /// Markdown output: the diagram in a mermaid fence (without the in-graph
    /// legend) plus the legend as regular text in the space outside the
    /// schematic.
    /// </summary>
    internal static string RenderMarkdownForTests(TopologyGraph graph, bool contracts) => RenderMarkdown(graph, contracts);

    private static string RenderMarkdown(TopologyGraph graph, bool contracts)
    {
        return "```mermaid\n"
            + TopologyEmitter.ToMermaid(graph, contracts, includeLegend: false).TrimEnd()
            + "\n```\n\n## Legend\n\n"
            + TopologyEmitter.MarkdownLegend;
    }

    /// <summary>
    /// Drops every project recognized as a test project (by name convention or
    /// test-framework reference) from the solution, so its mock activities and
    /// test workflows never enter the graph.
    /// </summary>
    private static Solution RemoveTestProjects(Solution solution)
    {
        foreach (var project in solution.Projects.Where(TestProjectFilter.IsTestProject).ToList())
        {
            solution = solution.RemoveProject(project.Id);
        }

        return solution;
    }
}
