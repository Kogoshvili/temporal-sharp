using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Static access point for activity-options presets configured via
/// <c>Temporal:ActivityOptions</c>. Workflows cannot use dependency injection
/// (they run in the deterministic replay sandbox), so presets are exposed
/// statically: the starter seeds this registry once, at <c>AddTemporal</c>
/// time, before any workflow runs, and workflows only read from it.
/// </summary>
public static class ActivityOptionsRegistry
{
    private static readonly object Sync = new();
    private static IReadOnlyDictionary<string, ActivityOptions> presets = new Dictionary<string, ActivityOptions>(StringComparer.Ordinal);
    private static ActivityOptions? defaultOptions;

    /// <summary>Gets the default preset, or <c>null</c> if none was configured.</summary>
    public static ActivityOptions? GetDefault() => defaultOptions;

    /// <summary>
    /// Gets the named preset. Throws <see cref="KeyNotFoundException"/> when no
    /// preset with the given name was configured.
    /// </summary>
    public static ActivityOptions Get(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        return presets.TryGetValue(name, out var options)
            ? options
            : throw new KeyNotFoundException($"No activity-options preset named '{name}' is configured.");
    }

    /// <summary>Attempts to get the named preset, returning <c>false</c> when absent.</summary>
    public static bool TryGet(string name, out ActivityOptions? options) =>
        presets.TryGetValue(name, out options);

    /// <summary>Gets the configured preset names.</summary>
    public static IReadOnlyCollection<string> Names => presets.Keys.ToArray();

    /// <summary>
    /// Replaces the registry contents. Called once by the starter during
    /// registration; the registry is read-only during workflow execution.
    /// </summary>
    internal static void Replace(ActivityOptions? defaultOptions, IReadOnlyDictionary<string, ActivityOptions> presets)
    {
        lock (Sync)
        {
            ActivityOptionsRegistry.defaultOptions = defaultOptions;
            ActivityOptionsRegistry.presets = new Dictionary<string, ActivityOptions>(presets, StringComparer.Ordinal);
        }
    }
}
