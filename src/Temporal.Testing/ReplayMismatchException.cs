namespace Kogoshvili.Temporal.Testing;

/// <summary>
/// Thrown when a workflow replay diverges from its recorded history. Carries the
/// original replay failure produced by <c>WorkflowReplayer</c>.
/// </summary>
public sealed class ReplayMismatchException : Exception
{
    public ReplayMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ReplayMismatchException(string message)
        : base(message)
    {
    }
}
