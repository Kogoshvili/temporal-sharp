using System.Reflection;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Convention-based discovery of workflow and activity types in an assembly,
/// mirroring Spring Boot's <c>workers-auto-discovery</c>.
/// </summary>
internal static class WorkerDiscovery
{
    public static IReadOnlyCollection<Type> FindWorkflowTypes(Assembly assembly) =>
        GetLoadableTypes(assembly)
            .Where(type => type.IsClass && !type.IsAbstract &&
                type.IsDefined(typeof(WorkflowAttribute), inherit: false))
            .ToArray();

    public static IReadOnlyCollection<Type> FindActivityTypes(Assembly assembly) =>
        GetLoadableTypes(assembly)
            .Where(type => type.IsClass && !type.IsInterface && HasActivityMethod(type))
            .ToArray();

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
