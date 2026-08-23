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
TMP0122 | Determinism | Error | Cryptographic randomness (RandomNumberGenerator / RNGCryptoServiceProvider) in workflow code
TMP0171 | Determinism | Error | Finalizer on a [Workflow] type
TMP0172 | Determinism | Error | System timers (Threading.Timer / Timers.Timer / PeriodicTimer) in workflow code
TMP0174 | Determinism | Error | WeakReference / ConditionalWeakTable in workflow code
TMP0177 | Determinism | Error | Static constructor / static field initializer / module initializer scheduling workflow commands
TMP0175 | Determinism | Warning | Control flow depending on non-deterministic time or randomness
TMP0104 | Determinism | Warning | Workflow.UtcNow compared to a persisted timestamp
TMP0123 | Determinism | Warning | Workflow.Random / Workflow.NewGuid used for a persisted id or payload
TMP0181 | Determinism | Warning | Busy-wait polling loop with a constant Workflow.DelayAsync

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TMP2102 | SdkMisuse | Disabled | ScheduleToCloseTimeout set without StartToCloseTimeout (invalid premise; either timeout is valid)

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
TMP3203 | SdkMisuse | Warning | SdkMisuse | Error | Activity instance-state mutation is a race risk, not a determinism error
