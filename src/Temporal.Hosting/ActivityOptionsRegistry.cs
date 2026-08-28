using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Static access point for activity-options presets configured via
/// <c>Temporal:ActivityOptions</c>. A single preset maps to both a regular
/// <see cref="ActivityOptions"/> and a <see cref="LocalActivityOptions"/>.
/// Workflows cannot use dependency injection (they run in the deterministic
/// replay sandbox), so presets are exposed statically: the starter seeds this
/// registry once, at <c>AddTemporal</c> time, before any workflow runs, and
/// workflows only read from it.
/// </summary>
public static class ActivityOptionsRegistry
{
    private static readonly object Sync = new();

    private static IReadOnlyDictionary<string, ActivityOptions> presets = new Dictionary<string, ActivityOptions>(StringComparer.Ordinal);
    private static IReadOnlyDictionary<string, LocalActivityOptions> localPresets = new Dictionary<string, LocalActivityOptions>(StringComparer.Ordinal);
    private static ActivityOptions? defaultOptions;
    private static LocalActivityOptions? localDefaultOptions;

    /// <summary>
    /// The built-in default preset for regular activities, used when
    /// <c>Temporal:ActivityOptions:Default</c> is not configured. A five-minute
    /// schedule-to-close timeout with no retry cap (retries forever, the SDK
    /// default).
    /// </summary>
    public static ActivityOptions BuiltInDefault { get; } = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(5),
    };

    /// <summary>
    /// The built-in default preset for local activities, used when
    /// <c>Temporal:ActivityOptions:LocalDefault</c> is not configured. A
    /// ten-second schedule-to-close timeout with no retry cap.
    /// </summary>
    public static LocalActivityOptions BuiltInLocalDefault { get; } = new()
    {
        ScheduleToCloseTimeout = TimeSpan.FromSeconds(10),
    };

    /// <summary>Gets a clone of the regular default preset, falling back to <see cref="BuiltInDefault"/> when none was configured.</summary>
    public static ActivityOptions GetDefault() => Clone(defaultOptions) ?? Clone(BuiltInDefault)!;

    /// <summary>Gets a clone of the local default preset, falling back to <see cref="BuiltInLocalDefault"/> when none was configured.</summary>
    public static LocalActivityOptions GetLocalDefault() => Clone(localDefaultOptions) ?? Clone(BuiltInLocalDefault)!;

    /// <summary>
    /// Gets a clone of the named preset as a regular <see cref="ActivityOptions"/>.
    /// Throws <see cref="KeyNotFoundException"/> when no preset with the given
    /// name was configured.
    /// </summary>
    public static ActivityOptions Get(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        return presets.TryGetValue(name, out var options)
            ? Clone(options)!
            : throw new KeyNotFoundException($"No activity-options preset named '{name}' is configured.");
    }

    /// <summary>
    /// Gets a clone of the named preset as a <see cref="LocalActivityOptions"/>.
    /// Throws <see cref="KeyNotFoundException"/> when no preset with the given
    /// name was configured.
    /// </summary>
    public static LocalActivityOptions GetLocal(string name)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        return localPresets.TryGetValue(name, out var options)
            ? Clone(options)!
            : throw new KeyNotFoundException($"No activity-options preset named '{name}' is configured.");
    }

    /// <summary>
    /// Resolves a preset by name as a regular <see cref="ActivityOptions"/>, or the
    /// regular default preset when <paramref name="name"/> is <c>null</c>.
    /// Returns a clone so callers may mutate it safely.
    /// </summary>
    public static ActivityOptions Resolve(string? name) =>
        name is null ? GetDefault() : Get(name);

    /// <summary>
    /// Resolves a preset by name as a <see cref="LocalActivityOptions"/>, or the
    /// local default preset when <paramref name="name"/> is <c>null</c>. Returns
    /// a clone so callers may mutate it safely.
    /// </summary>
    public static LocalActivityOptions ResolveLocal(string? name) =>
        name is null ? GetLocalDefault() : GetLocal(name);

    /// <summary>Attempts to get a clone of the named preset as a regular <see cref="ActivityOptions"/>, returning <c>false</c> when absent.</summary>
    public static bool TryGet(string name, out ActivityOptions? options)
    {
        if (presets.TryGetValue(name, out var found))
        {
            options = Clone(found);
            return true;
        }

        options = null;
        return false;
    }

    /// <summary>Gets the configured preset names.</summary>
    public static IReadOnlyCollection<string> Names => presets.Keys.ToArray();

    /// <summary>
    /// Replaces the registry contents. Called once by the starter during
    /// registration; the registry is read-only during workflow execution.
    /// </summary>
    internal static void Replace(
        ActivityOptions? defaultOptions,
        IReadOnlyDictionary<string, ActivityOptions> presets,
        LocalActivityOptions? localDefaultOptions,
        IReadOnlyDictionary<string, LocalActivityOptions> localPresets)
    {
        lock (Sync)
        {
            ActivityOptionsRegistry.defaultOptions = defaultOptions;
            ActivityOptionsRegistry.presets = new Dictionary<string, ActivityOptions>(presets, StringComparer.Ordinal);
            ActivityOptionsRegistry.localDefaultOptions = localDefaultOptions;
            ActivityOptionsRegistry.localPresets = new Dictionary<string, LocalActivityOptions>(localPresets, StringComparer.Ordinal);
        }
    }

    internal static void Replace(ActivityOptions? defaultOptions, IReadOnlyDictionary<string, ActivityOptions> presets) =>
        Replace(defaultOptions, presets, null, new Dictionary<string, LocalActivityOptions>());

    private static ActivityOptions? Clone(ActivityOptions? options) =>
        options is null ? null : (ActivityOptions)options.Clone();

    private static LocalActivityOptions? Clone(LocalActivityOptions? options) =>
        options is null ? null : (LocalActivityOptions)options.Clone();
}
