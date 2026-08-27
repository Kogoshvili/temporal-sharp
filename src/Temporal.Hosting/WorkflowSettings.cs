using System.Text.Json;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Static access point for workflow settings configured via
/// <c>Temporal:WorkflowSettings</c>. Workflows call
/// <see cref="GetAsync{TSettings}"/> to read their own typed settings.
/// </summary>
/// <remarks>
/// Settings are read through a local activity, so the value is recorded in
/// workflow history and stays stable across replay even if the configuration is
/// live-reloaded. Read once at the start of the workflow and reuse the value to
/// keep a single run internally consistent.
/// </remarks>
public static class WorkflowSettings
{
    private static readonly LocalActivityOptions DefaultOptions = new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(10),
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reads the settings for the current workflow type, deserialized into
    /// <typeparamref name="TSettings"/>.
    /// </summary>
    /// <typeparam name="TSettings">The workflow's settings type.</typeparam>
    /// <param name="workflowType">
    /// Workflow type name; defaults to the current workflow's type.
    /// </param>
    /// <param name="options">Local activity options; defaults to a 10s timeout.</param>
    public static async Task<TSettings> GetAsync<TSettings>(
        string? workflowType = null,
        LocalActivityOptions? options = null)
    {
        var type = workflowType ?? Workflow.Info.WorkflowType;

        var json = await Workflow.ExecuteLocalActivityAsync(
            (WorkflowSettingsActivity activity) => activity.Read(type),
            options ?? DefaultOptions).ConfigureAwait(false);

        return JsonSerializer.Deserialize<TSettings>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                $"No workflow settings were resolved for '{type}'. " +
                $"Set 'Temporal:WorkflowSettings:ByType:{type}' or 'Temporal:WorkflowSettings:Default'.");
    }
}
