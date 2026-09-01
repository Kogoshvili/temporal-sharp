using System.Numerics;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// P2 — contracts, lifecycle, payload, activity, search-attribute, and
// versioning violations (TMP2xxx / TMP3xxx).

// TMP3208 — [WorkflowUpdate] must return a concrete Task<T>.
[Workflow]
public class InvalidUpdateWorkflow
{
    [WorkflowUpdate]
    public void Update(int x) { }
}

// TMP3209 — continue-as-new invoked inside an update handler.
[Workflow]
public class ContinueAsNewUpdateWorkflow
{
    [WorkflowUpdate]
    public async Task<int> Update(int x)
    {
        Workflow.CreateContinueAsNewException("wf", null, new ContinueAsNewOptions());
        return x;
    }
}

// TMP3212 — client type referenced from workflow code.
[Workflow]
public class ClientTypeWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        Temporalio.Client.TemporalClient? client = null;
    }
}

// TMP3214 — workflow and activity methods mixed in one class.
[Workflow]
public class MixedContract
{
    [WorkflowRun]
    public async Task RunAsync() => await Task.CompletedTask;

    [Activity]
    public async Task DoWork() => await Task.CompletedTask;
}

// TMP3215 — update validator mutates state and blocks.
[Workflow]
public class ValidatorWorkflow
{
    private int count;

    [WorkflowUpdateValidator]
    public void Validate(int x)
    {
        count = x;
        Workflow.DelayAsync(100);
    }
}

// TMP3216 — signal handler schedules a workflow command.
[Workflow]
public class HandlerWorkflow
{
    [WorkflowSignal]
    public async Task Handle() => await Workflow.DelayAsync(100);
}

// TMP3217 — async handler but the workflow never awaits AllHandlersFinished.
[Workflow]
public class PendingHandlerWorkflow
{
    [WorkflowSignal]
    public async Task Handle() => await Task.Delay(1);

    [WorkflowRun]
    public async Task RunAsync() => await Task.Delay(1);
}

// TMP3218 — [WorkflowInit] and [WorkflowRun] parameter lists mismatch.
[Workflow]
public class InitMismatchWorkflow
{
    [WorkflowInit]
    public InitMismatchWorkflow(int x) { }

    [WorkflowRun]
    public async Task RunAsync() => await Task.CompletedTask;
}

// TMP3219 — parameterized constructor without [WorkflowInit].
[Workflow]
public class ParameterizedCtorWorkflow
{
    public ParameterizedCtorWorkflow(int x) { }

    [WorkflowRun]
    public async Task RunAsync() => await Task.CompletedTask;
}

// TMP3220 — parameterized constructor chained from the parameterless one as a
// singleton workaround; the worker never invokes the parameterized constructor.
[Workflow]
public class ChainedCtorWorkflow
{
    private const string LegacyName = "legacy";

    private readonly string name;

    public ChainedCtorWorkflow()
        : this(LegacyName)
    {
    }

    public ChainedCtorWorkflow(string name)
    {
        this.name = name;
    }

    [WorkflowRun]
    public async Task RunAsync() => await Task.CompletedTask;
}

// TMP2148 — Workflow.Unsafe used to branch workflow logic.
[Workflow]
public class UnsafeReplayBranchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        if (Workflow.Unsafe.IsReplaying)
        {
            await Workflow.ExecuteActivityAsync(
                "Notify",
                null,
                new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) });
        }
    }
}

// TMP2122 — continue-as-new without passing workflow state.
[Workflow]
public class StatelessContinueAsNewWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.CreateContinueAsNewException("wf", null, new ContinueAsNewOptions());
    }
}

// TMP2123 — catch swallows a continue-as-new exception.
[Workflow]
public class SwallowContinueAsNewWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        try
        {
            throw Workflow.CreateContinueAsNewException("wf", new object[] { 1 }, new ContinueAsNewOptions());
        }
        catch (Exception) { }
    }
}

// TMP2124 — cleanup awaits outside a non-cancellable scope.
[Workflow]
public class CancellableCleanupWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        try { await Task.Delay(1); }
        finally { await Task.Delay(1); }
    }
}

// TMP2125 — unbounded loop without a continue-as-new check.
[Workflow]
public class UnboundedLoopWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        while (true) { await Workflow.DelayAsync(1000); }
    }
}

// TMP2132 — throwing a base Exception instead of ApplicationFailure.
[Workflow]
public class BaseExceptionWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        throw new Exception("boom");
    }
}

// TMP2133 — Debug.Assert in workflow code.
[Workflow]
public class AssertWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        System.Diagnostics.Debug.Assert(true);
    }
}

// TMP2142 — BigInteger parameter without a converter.
[Workflow]
public class BigIntegerWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(BigInteger x) => await Task.CompletedTask;
}

// TMP2143 — Exception used as a payload.
[Workflow]
public class ExceptionPayloadWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(Exception e) => await Task.CompletedTask;
}

// TMP2172 — nested lossy member in a payload DTO.
public class NestedDto
{
    public object Value { get; set; }
}

[Workflow]
public class NestedLossyWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(NestedDto dto) => await Task.CompletedTask;
}

// TMP2144 — oversized inline collection payload.
[Workflow]
public class LargePayloadWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var xs = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };
    }
}

// TMP3106 — non-SDK logger in an activity.
public static class ContextActivities
{
    [Activity]
    public static Task ConsoleLog()
    {
        Console.WriteLine("log");
        return Task.CompletedTask;
    }

    // TMP3107 — HttpClient call without a CancellationToken.
    [Activity]
    public static async Task Fetch()
    {
        var client = new HttpClient();
        await client.GetAsync("https://example.com");
    }

    // TMP3109 — heartbeats in a loop but never checks the CancellationToken.
    [Activity]
    public static async Task LongPoll()
    {
        for (var i = 0; i < 10; i++)
        {
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(1);
        }
    }
}

// TMP3108 — HeartbeatTimeout much shorter than StartToCloseTimeout.
[Workflow]
public class TimeoutMismatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        var opts = new ActivityOptions
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(10),
            HeartbeatTimeout = TimeSpan.FromSeconds(1),
        };
    }
}

// TMP2107 — non-idempotent activity without an idempotency-key argument.
public static class NonIdempotentActivities
{
    [Activity]
    public static async Task Send() => await Task.CompletedTask;
}

// TMP2162 — upsert inside a loop.
[Workflow]
public class UpsertLoopWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        for (var i = 0; i < 3; i++)
        {
            Workflow.UpsertTypedSearchAttributes(SearchAttributeKey.ForKeyword("count").ValueSet(i));
        }
    }
}

// TMP2163 — removing a search attribute with ValueSet(null).
[Workflow]
public class UnsetShapeWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.UpsertTypedSearchAttributes(SearchAttributeKey.ForKeyword("count").ValueSet(null));
    }
}

// TMP3303 — the same patch id Patched twice.
[Workflow]
public class DuplicatePatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        if (Workflow.Patched("v1")) { } else { }
        if (Workflow.Patched("v1")) { } else { }
    }
}

// TMP3305 — Patched result discarded.
[Workflow]
public class DiscardedPatchWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        Workflow.Patched("v1");
    }
}

// TMP3307 — patch fallback removed without DeprecatePatch.
[Workflow]
public class RemovedFallbackWorkflow
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        if (Workflow.Patched("v1")) { New(); }
    }

    private void New() { }
}
