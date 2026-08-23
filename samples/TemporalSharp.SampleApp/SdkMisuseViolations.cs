using Temporalio.Workflows;

namespace TemporalSharp.SampleApp;

// SDK feature-misuse (TMP21xx). Note: TMP2102, TMP2103, TMP2111, TMP2151,
// TMP2161, and TMP2171 are opt-in; they are enabled in this sample's
// .editorconfig so they show up here too.
[Workflow]
public class SdkMisuseViolations
{
    [WorkflowRun]
    public async Task RunAsync()
    {
        // TMP2101 — activity options set no required timeout
        var noTimeout = new ActivityOptions { TaskQueue = "default" };

        // TMP2102 (opt-in) — ScheduleToCloseTimeout without StartToCloseTimeout
        _ = new ActivityOptions { ScheduleToCloseTimeout = TimeSpan.FromMinutes(5) };

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
