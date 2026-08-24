// Stand-in Temporal types so the demo builds without the real SDK package.
// The analyzer matches these purely by name.
namespace Temporalio.Workflows
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class WorkflowAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Method)]
    public sealed class WorkflowRunAttribute : System.Attribute { }
}
