; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TMP0113 | Determinism | Error | ConfigureAwait(false) in workflow code
TMP0146 | Determinism | Error | Task.Run / TaskFactory.StartNew in workflow code (use Workflow.RunTaskAsync)
TMP0147 | Determinism | Error | Mutex / Semaphore / SemaphoreSlim in workflow code (use Temporalio.Workflows.*)
TMP0112 | Determinism | Error | Un-awaited (floating) Task/ValueTask in workflow code
TMP3204 | SdkMisuse | Error | [WorkflowQuery] must not be async or return void/Task/Task<T>
TMP3205 | SdkMisuse | Error | [WorkflowSignal] must return void or Task
TMP3206 | SdkMisuse | Error | [WorkflowQuery] mutates workflow state
TMP3207 | SdkMisuse | Error | Workflow command API called inside a [WorkflowQuery]

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TMP2102 | SdkMisuse | Disabled | ScheduleToCloseTimeout set without StartToCloseTimeout (invalid premise; either timeout is valid)

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
TMP3203 | SdkMisuse | Warning | SdkMisuse | Error | Activity instance-state mutation is a race risk, not a determinism error
