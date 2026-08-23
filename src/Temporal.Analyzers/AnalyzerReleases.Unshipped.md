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
TMP3208 | SdkMisuse | Error | [WorkflowUpdate] must return a concrete Task<T>
TMP3209 | SdkMisuse | Error | Continue-as-new invoked inside a [WorkflowUpdate]
TMP3211 | SdkMisuse | Warning | Query/signal/update Name must be a constant string literal
TMP3212 | SdkMisuse | Error | Temporalio.Client / worker types referenced from workflow code
TMP3213 | SdkMisuse | Warning | StartWorkflowAsync without an explicit workflow id
TMP3214 | SdkMisuse | Warning | Workflow and activity methods mixed in one class
TMP3215 | SdkMisuse | Error | [WorkflowUpdateValidator] mutates state or blocks
TMP3216 | SdkMisuse | Warning | Signal/update handler schedules activities/child workflows/delays
TMP3217 | SdkMisuse | Warning | Workflow may complete while async handlers are pending
TMP3218 | SdkMisuse | Error | [WorkflowInit] and [WorkflowRun] parameter lists mismatch
TMP3219 | SdkMisuse | Error | [Workflow] parameterized constructor without [WorkflowInit]
TMP2123 | SdkMisuse | Warning | catch swallows a cancellation
TMP2124 | SdkMisuse | Warning | Cleanup after cancellation not in a non-cancellable scope
TMP2106 | SdkMisuse | Warning | RetryPolicy set on a non-idempotent activity
TMP2107 | SdkMisuse | Warning | Non-idempotent activity without an idempotency-key argument
TMP2122 | SdkMisuse | Warning | Continue-as-new without passing current workflow state
TMP2125 | SdkMisuse | Warning | Unbounded loop without a continue-as-new check
TMP2132 | SdkMisuse | Warning | Non-ApplicationFailure exception thrown from workflow code
TMP2133 | SdkMisuse | Warning | Debug.Assert / Trace.Assert in workflow code
TMP2146 | SdkMisuse | Error | Use of internal Temporalio.* namespaces
TMP2142 | SdkMisuse | Warning | BigInteger in a payload without a converter
TMP2143 | SdkMisuse | Warning | Exception used as a param/return payload
TMP2144 | SdkMisuse | Warning | Oversized inline literal/collection payload
TMP2172 | SdkMisuse | Warning | object/dynamic/JsonElement in nested payload members
TMP3105 | SdkMisuse | Error | ActivityExecutionContext captured across an await
TMP3106 | SdkMisuse | Warning | Console.* / non-SDK logger in an activity
TMP3107 | SdkMisuse | Warning | HttpClient call without a CancellationToken
TMP3108 | SdkMisuse | Warning | HeartbeatTimeout much shorter than StartToCloseTimeout
TMP3109 | SdkMisuse | Warning | Activity heartbeats in a loop but never checks the CancellationToken
TMP2162 | SdkMisuse | Warning | Workflow.UpsertTypedSearchAttributes inside a loop
TMP2163 | SdkMisuse | Warning | Search-attribute removal not using the unset shape
TMP3303 | SdkMisuse | Error | Same patch id Patched more than once
TMP3305 | SdkMisuse | Warning | Patched result discarded (does not guard a change)
TMP3307 | SdkMisuse | Warning | Patch fallback removed without DeprecatePatch
TMP4101 | BestPractice | Warning | Multiple positional parameters on a workflow/activity method (prefer a single object)
TMP4103 | BestPractice | Warning | Polling loop with a constant Workflow.DelayAsync (use Workflow.WaitConditionAsync)
TMP4104 | BestPractice | Disabled | CPU-heavy loop with no await in workflow code
TMP4105 | BestPractice | Warning | Hard-coded task-queue name instead of a shared constant
TMP4106 | BestPractice | Warning | Consecutive ExecuteLocalActivityAsync calls with no intervening workflow command
TMP4107 | BestPractice | Warning | Local activity performs blocking or network I/O
TMP4201 | BestPractice | Disabled | Workflow.NewGuid without a determinism comment
TMP4202 | BestPractice | Disabled | Workflow.DeprecatePatch without an explanatory comment
TMP4203 | BestPractice | Disabled | Versioning change without a replay-tested comment
TMP2147 | SdkMisuse | Disabled | using directive for a namespace configured as unsafe for workflow code
TMP0148 | Determinism | Info | Task.WhenAll in workflow code (use Workflow.WhenAllAsync)

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TMP2102 | SdkMisuse | Disabled | ScheduleToCloseTimeout set without StartToCloseTimeout (invalid premise; either timeout is valid)

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
TMP3203 | SdkMisuse | Warning | SdkMisuse | Error | Activity instance-state mutation is a race risk, not a determinism error
