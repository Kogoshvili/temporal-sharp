using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// Query/signal handler contract violations (TMP3204-TMP3207). Queries must be
// synchronous and read-only; signals must return void or Task.

// TMP3204 — [WorkflowQuery] method is async.
[Workflow]
public class AsyncQueryWorkflow
{
    [WorkflowQuery]
    public async Task<int> GetValueAsync() => 1;
}

// TMP3204 — [WorkflowQuery] method returns void.
[Workflow]
public class VoidQueryWorkflow
{
    [WorkflowQuery]
    public void GetValue() { }
}

// TMP3205 — [WorkflowSignal] method returns a value.
[Workflow]
public class ValueSignalWorkflow
{
    [WorkflowSignal]
    public int Handle(int value) => value;
}

// TMP3205 — [WorkflowSignal] method returns Task<T>.
[Workflow]
public class GenericSignalWorkflow
{
    [WorkflowSignal]
    public Task<int> Handle(int value) => Task.FromResult(value);
}

// TMP3206 — [WorkflowQuery] method mutates instance state.
[Workflow]
public class MutatingQueryWorkflow
{
    private int _count;

    [WorkflowQuery]
    public int GetCount()
    {
        _count = 42;
        return _count;
    }
}

// TMP3207 — [WorkflowQuery] method calls a Workflow command API.
[Workflow]
public class CommandApiQueryWorkflow
{
    [WorkflowQuery]
    public string GetValue()
    {
        _ = Workflow.DelayAsync(100);
        return "ok";
    }
}
