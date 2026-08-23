; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TMP0101 | Determinism | Error | Wall-clock time in workflow code
TMP0111 | Determinism | Error | Sleep/block in workflow code
TMP0121 | Determinism | Error | Non-deterministic randomness in workflow code
TMP0131 | Determinism | Error | I/O or environment access in workflow code
TMP0141 | Determinism | Error | Concurrent work started in workflow code
TMP0142 | Determinism | Error | Blocking synchronization primitive in workflow code
TMP0143 | Determinism | Warning | Raw task scheduling in workflow code
TMP0144 | Determinism | Error | Raw TaskCompletionSource coordination in workflow code
TMP0145 | Determinism | Error | Reflection / dynamic invocation in workflow code
TMP0151 | Determinism | Error | Non-deterministic collection enumeration in workflow code
TMP0161 | Determinism | Warning | Culture-sensitive parse/format in workflow code
TMP0102 | Determinism | Error | Stopwatch elapsed wall-clock time in workflow code
TMP1101 | WorkflowState | Error | Static field mutation in workflow code
TMP1102 | WorkflowState | Error | [ThreadStatic] state mutation in workflow code
TMP1103 | WorkflowState | Error | Static property setter in workflow code
TMP1104 | WorkflowState | Error | Static collection mutation in workflow code
TMP1105 | WorkflowState | Error | Static state mutated via method call in workflow code
TMP1106 | WorkflowState | Error | Ambient AsyncLocal/ThreadLocal state in workflow code
TMP2101 | SdkMisuse | Error | Activity options missing required timeout
TMP2102 | SdkMisuse | Disabled | ScheduleToCloseTimeout set without StartToCloseTimeout
TMP2103 | SdkMisuse | Disabled | WaitConditionAsync called without a timeout
TMP2104 | SdkMisuse | Warning | WaitConditionAsync timeout result ignored
TMP2111 | SdkMisuse | Disabled | Workflow target named by string
TMP2121 | SdkMisuse | Error | Continue-as-new exception not thrown
TMP2131 | SdkMisuse | Warning | Non-replay-aware logging in workflow code
TMP2141 | SdkMisuse | Error | Non-serializable type in workflow/activity signature
TMP2151 | SdkMisuse | Disabled | Sensitive-data parameter or property
TMP2161 | SdkMisuse | Disabled | Search attribute never upserted
TMP2171 | SdkMisuse | Disabled | Lossy-number parameter in workflow/activity signature
TMP3101 | SdkMisuse | Warning | Long-running activity does not heartbeat
TMP3102 | SdkMisuse | Error | HeartbeatTimeout set but activity never heartbeats
TMP3103 | SdkMisuse | Warning | Heartbeat called without HeartbeatTimeout
TMP3104 | SdkMisuse | Warning | Heartbeat called unnecessarily
TMP3201 | SdkMisuse | Error | Invalid workflow entry method
TMP3202 | SdkMisuse | Error | Invalid activity declaration
TMP3203 | SdkMisuse | Error | Activity method mutates instance state
TMP3301 | SdkMisuse | Error | Patch both applied and deprecated
TMP3302 | SdkMisuse | Warning | Non-constant patch id
