using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.SampleApp;

// SDK feature-misuse (TMP21xx). Note: TMP2103, TMP2111, TMP2151,
// TMP2161, and TMP2171 are opt-in; they are enabled in this sample's
// .editorconfig so they show up here too.
[Workflow]
public class SdkMisuseViolations
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // TMP2101 — activity options set neither required timeout
        var noTimeout = new ActivityOptions { TaskQueue = "default" };

        // TMP2111 (opt-in) — string-named activity target
        await Workflow.ExecuteActivityAsync("Greet", null, noTimeout);

        // TMP2121 — continue-as-new exception created but never thrown
        Workflow.CreateContinueAsNewException(() => RunAsync(), new ContinueAsNewOptions());

        // TMP2131 — non-replay-aware logging
        Console.WriteLine("hello");

        // TMP2103 (opt-in) — WaitConditionAsync without a timeout
        await Workflow.WaitConditionAsync(() => true);

        // TMP2104 — WaitConditionAsync timeout result discarded
        await Workflow.WaitConditionAsync(() => true, TimeSpan.FromMinutes(1));
    }
}

// TMP2141 — non-serializable parameter in a workflow signature.
[Workflow]
public class NonSerializableParamWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(System.Action callback) => await Task.CompletedTask;
}

// TMP2141 — non-serializable return type in a workflow signature.
[Workflow]
public class NonSerializableReturnWorkflow
{
    [WorkflowRun]
    public async Task<System.Action> RunAsync() => () => { };
}

// TMP2151 (opt-in) — sensitive-data parameter names.
[Workflow]
public class SensitiveDataWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(string password, string apiKey) => await Task.CompletedTask;
}

// TMP2171 (opt-in) — lossy-number parameters.
[Workflow]
public class LossyParamWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(object payload, dynamic value) => await Task.CompletedTask;
}

// TMP2135 — nonRetryable set on an ApplicationFailureException thrown from
// workflow code (the flag only affects activity retries).
[Workflow]
public class WorkflowNonRetryableViolations
{
    [WorkflowRun]
    public Task RunAsync()
    {
        throw new ApplicationFailureException("bad input", nonRetryable: true);
    }
}

// TMP3210 — workflow constructor schedules a blocking command.
[Workflow]
public class ConstructorCommandViolations
{
    public ConstructorCommandViolations()
    {
        _ = Workflow.DelayAsync(1000);
    }

    [WorkflowRun]
    public async Task RunAsync() => await Task.CompletedTask;
}

// TMP3213 — standalone-activity client API called from workflow code (also
// exercises TMP3212: client types referenced from workflow code).
[Workflow]
public class StandaloneActivityClientViolations
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        Temporalio.Client.TemporalClient client = null!;
        await client.ExecuteActivityAsync(
            "Greet",
            null,
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromMinutes(1) });
    }
}
