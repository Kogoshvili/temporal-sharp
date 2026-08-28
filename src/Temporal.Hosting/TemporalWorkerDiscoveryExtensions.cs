using System.Reflection;
using System.Runtime.CompilerServices;
using Kogoshvili.Temporal.Hosting;
using Temporalio.Extensions.Hosting;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Convention-based auto-discovery extensions for a hosted Temporal worker.
/// Calling one of these opts a worker into scanning an assembly for
/// <c>[Workflow]</c>/<c>[Activity]</c> types instead of registering them
/// explicitly.
/// </summary>
public static class TemporalWorkerDiscoveryExtensions
{
    /// <summary>
    /// Scans the entry assembly (or, when absent, the calling assembly) and
    /// registers every discovered workflow/activity type on the worker.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ITemporalWorkerServiceOptionsBuilder AddDiscoveredTypes(
        this ITemporalWorkerServiceOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly();
        return builder.AddDiscoveredTypes(assembly);
    }

    /// <summary>
    /// Scans the given assembly and registers every discovered workflow/activity
    /// type on the worker.
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddDiscoveredTypes(
        this ITemporalWorkerServiceOptionsBuilder builder, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(assembly);

        AddDiscoveredTypes(builder, new[] { assembly });
        return builder;
    }

    /// <summary>
    /// Scans the assemblies of the given marker types and registers every
    /// discovered workflow/activity type on the worker. Use this instead of the
    /// assembly overload when the entry assembly is not the worker assembly
    /// (e.g. under <c>dotnet test</c>).
    /// </summary>
    public static ITemporalWorkerServiceOptionsBuilder AddDiscoveredTypes(
        this ITemporalWorkerServiceOptionsBuilder builder, Type markerType, params Type[] markerTypes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(markerType);

        var assemblies = Prepend(markerType, markerTypes)
            .Select(type => type.Assembly)
            .Distinct()
            .ToArray();

        AddDiscoveredTypes(builder, assemblies);
        return builder;
    }

    private static void AddDiscoveredTypes(
        ITemporalWorkerServiceOptionsBuilder builder, IReadOnlyCollection<Assembly> assemblies)
    {
        foreach (var workflowType in assemblies.SelectMany(WorkerDiscovery.FindWorkflowTypes))
        {
            builder.AddWorkflow(workflowType);
        }

        foreach (var activityType in assemblies.SelectMany(WorkerDiscovery.FindActivityTypes))
        {
            switch (WorkerDiscovery.GetActivityLifetime(activityType))
            {
                case ActivityLifetime.Singleton:
                    builder.AddSingletonActivities(activityType);
                    break;
                case ActivityLifetime.Transient:
                    builder.AddTransientActivities(activityType);
                    break;
                case ActivityLifetime.Static:
                    builder.AddStaticActivities(activityType);
                    break;
                default:
                    builder.AddScopedActivities(activityType);
                    break;
            }
        }
    }

    private static Type[] Prepend(Type first, Type[] rest)
    {
        var types = new Type[rest.Length + 1];
        types[0] = first;
        Array.Copy(rest, 0, types, 1, rest.Length);
        return types;
    }
}
