using System.Reflection;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Convention-based discovery of workflow and activity types in an assembly,
/// mirroring Spring Boot's <c>workers-auto-discovery</c>.
/// </summary>
public static class WorkerDiscovery
{
    /// <summary>Finds non-abstract classes marked with <c>[Workflow]</c> in the assembly.</summary>
    public static IReadOnlyCollection<Type> FindWorkflowTypes(Assembly assembly) =>
        GetLoadableTypes(assembly)
            .Where(type => type.IsClass && !type.IsAbstract &&
                type.IsDefined(typeof(WorkflowAttribute), inherit: false))
            .ToArray();

    /// <summary>Finds classes that declare at least one <c>[Activity]</c> method in the assembly.</summary>
    public static IReadOnlyCollection<Type> FindActivityTypes(Assembly assembly) =>
        GetLoadableTypes(assembly)
            .Where(type => type.IsClass && !type.IsInterface && HasActivityMethod(type))
            .ToArray();

    /// <summary>
    /// Resolves the registration lifetime for an activity type, honoring an
    /// explicit <see cref="ActivityLifetimeAttribute"/> and falling back to
    /// <see cref="ActivityLifetime.Static"/> for static classes and
    /// <see cref="ActivityLifetime.Scoped"/> for instance classes.
    /// </summary>
    public static ActivityLifetime GetActivityLifetime(Type activityType)
    {
        var attribute = activityType.GetCustomAttribute<ActivityLifetimeAttribute>(inherit: false);
        if (attribute is not null)
        {
            return attribute.Lifetime;
        }

        return activityType.IsAbstract && activityType.IsSealed
            ? ActivityLifetime.Static
            : ActivityLifetime.Scoped;
    }

    private static bool HasActivityMethod(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Any(method => method.IsDefined(typeof(ActivityAttribute), inherit: false));

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
