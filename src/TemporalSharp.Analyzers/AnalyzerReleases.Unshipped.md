; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TMP0101 | TemporalSharp.Determinism | Error | Wall-clock time in workflow code
TMP0111 | TemporalSharp.Determinism | Error | Sleep/block in workflow code
TMP0121 | TemporalSharp.Determinism | Error | Non-deterministic randomness in workflow code
TMP0131 | TemporalSharp.Determinism | Error | I/O or environment access in workflow code
TMP0141 | TemporalSharp.Determinism | Error | Concurrent work started in workflow code
TMP0142 | TemporalSharp.Determinism | Error | Blocking synchronization primitive in workflow code
TMP0143 | TemporalSharp.Determinism | Warning | Raw task scheduling in workflow code
TMP0151 | TemporalSharp.Determinism | Error | Non-deterministic collection enumeration in workflow code
TMP0102 | TemporalSharp.Determinism | Error | Stopwatch elapsed wall-clock time in workflow code
TMP1101 | TemporalSharp.WorkflowState | Error | Static field mutation in workflow code
TMP1102 | TemporalSharp.WorkflowState | Error | [ThreadStatic] state mutation in workflow code
TMP1103 | TemporalSharp.WorkflowState | Error | Static property setter in workflow code
TMP1104 | TemporalSharp.WorkflowState | Error | Static collection mutation in workflow code
TMP1105 | TemporalSharp.WorkflowState | Error | Static state mutated via method call in workflow code
TMP2101 | TemporalSharp.SdkMisuse | Error | Activity options missing required timeout
TMP2102 | TemporalSharp.SdkMisuse | Disabled | ScheduleToCloseTimeout set without StartToCloseTimeout
TMP2111 | TemporalSharp.SdkMisuse | Disabled | Workflow target named by string
TMP2121 | TemporalSharp.SdkMisuse | Error | Continue-as-new exception not thrown
TMP2131 | TemporalSharp.SdkMisuse | Warning | Non-replay-aware logging in workflow code
TMP2141 | TemporalSharp.SdkMisuse | Error | Non-serializable type in workflow/activity signature
TMP2151 | TemporalSharp.SdkMisuse | Disabled | Sensitive-data parameter or property
TMP2161 | TemporalSharp.SdkMisuse | Disabled | Search attribute never upserted
TMP2171 | TemporalSharp.SdkMisuse | Disabled | Lossy-number parameter in workflow/activity signature
TMP3101 | TemporalSharp.SdkMisuse | Warning | Long-running activity does not heartbeat
TMP3102 | TemporalSharp.SdkMisuse | Error | HeartbeatTimeout set but activity never heartbeats
TMP3103 | TemporalSharp.SdkMisuse | Warning | Heartbeat called without HeartbeatTimeout
TMP3104 | TemporalSharp.SdkMisuse | Warning | Heartbeat called unnecessarily
TMP3201 | TemporalSharp.SdkMisuse | Error | Invalid workflow entry method
TMP3202 | TemporalSharp.SdkMisuse | Error | Invalid activity declaration
TMP3301 | TemporalSharp.SdkMisuse | Error | Patch both applied and deprecated
TMP3302 | TemporalSharp.SdkMisuse | Warning | Non-constant patch id
