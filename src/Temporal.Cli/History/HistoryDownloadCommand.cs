using Kogoshvili.Temporal.Configuration;
using Temporalio.Client;

namespace Kogoshvili.Temporal.Cli.History;

/// <summary>
/// Downloads recorded workflow histories as JSON files so they can be replayed
/// later (e.g. via <c>Kogoshvili.Temporal.Testing</c>). Connects using shared
/// configuration (appsettings.json + <c>Temporal__*</c> environment variables).
/// </summary>
internal static class HistoryDownloadCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        HistoryDownloadOptions options;
        try
        {
            options = HistoryDownloadOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            HistoryDownloadOptions.PrintUsage(Console.Error);
            return 2;
        }

        try
        {
            var configuration = TemporalConfig.BuildConfiguration(options.Config);
            var client = await TemporalConfig.ConnectAsync(configuration).ConfigureAwait(false);

            Directory.CreateDirectory(options.OutDir);

            var query = BuildQuery(options.WorkflowType, options.ExecutionStatus);
            var count = 0;
            await foreach (var history in client.ListWorkflowHistoriesAsync(query))
            {
                if (options.Limit is { } limit && count >= limit)
                {
                    break;
                }

                var fileName = SanitizeFileName(history.Id) + ".json";
                var path = Path.Combine(options.OutDir, fileName);
                await File.WriteAllTextAsync(path, history.ToJson()).ConfigureAwait(false);
                Console.Out.WriteLine($"Wrote {path}");
                count++;
            }

            Console.Out.WriteLine($"Downloaded {count} workflow histories to {options.OutDir}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
    }

    private static string BuildQuery(string workflowType, string? executionStatus)
    {
        var query = $"WorkflowType = '{workflowType}'";
        if (!string.IsNullOrWhiteSpace(executionStatus))
        {
            query += $" AND ExecutionStatus = '{executionStatus}'";
        }

        return query;
    }

    private static string SanitizeFileName(string workflowId)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = workflowId.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
