namespace Kogoshvili.Temporal.Testing;

/// <summary>
/// Thrown when a workflow replay diverges from its recorded history. Carries the
/// original replay failure produced by <c>WorkflowReplayer</c>.
/// </summary>
public sealed class ReplayMismatchException : Exception
{
    /// <summary>Initializes the exception with a message and the underlying replay failure.</summary>
    public ReplayMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes the exception with a message.</summary>
    public ReplayMismatchException(string message)
        : base(message)
    {
    }
}
