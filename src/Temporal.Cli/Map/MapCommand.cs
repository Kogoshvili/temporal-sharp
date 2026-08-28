using Kogoshvili.Temporal.Cli.Analysis;

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
            var graph = await WorkflowTopologyBuilder.BuildAsync(solutions, CancellationToken.None).ConfigureAwait(false);

            var title = string.Join(", ", paths);
            var content = options.Format switch
            {
                MapOutputFormat.Json => TopologyEmitter.ToJson(graph),
                MapOutputFormat.Html => TopologyEmitter.ToHtml(graph, title),
                MapOutputFormat.Dot => TopologyEmitter.ToDot(graph),
                _ => TopologyEmitter.ToMermaid(graph),
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
}
