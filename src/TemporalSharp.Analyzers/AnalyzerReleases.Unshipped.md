; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TMP0101 | TemporalSharp.Determinism | Warning | Wall-clock time in workflow code
TMP0111 | TemporalSharp.Determinism | Warning | Sleep/block in workflow code
TMP0121 | TemporalSharp.Determinism | Warning | Non-deterministic randomness in workflow code
TMP0131 | TemporalSharp.Determinism | Warning | I/O or environment access in workflow code
TMP0141 | TemporalSharp.Determinism | Warning | Concurrent work started in workflow code
TMP0142 | TemporalSharp.Determinism | Warning | Blocking synchronization primitive in workflow code
TMP0151 | TemporalSharp.Determinism | Warning | Non-deterministic collection enumeration in workflow code
TMP0102 | TemporalSharp.Determinism | Warning | Stopwatch elapsed wall-clock time in workflow code
TMP1101 | TemporalSharp.WorkflowState | Warning | Static field mutation in workflow code
TMP1102 | TemporalSharp.WorkflowState | Warning | [ThreadStatic] state mutation in workflow code
TMP1103 | TemporalSharp.WorkflowState | Warning | Static property setter in workflow code
TMP1104 | TemporalSharp.WorkflowState | Warning | Static collection mutation in workflow code
TMP2101 | TemporalSharp.SdkMisuse | Warning | Activity options missing required timeout
TMP2102 | TemporalSharp.SdkMisuse | Disabled | ScheduleToCloseTimeout set without StartToCloseTimeout
TMP2111 | TemporalSharp.SdkMisuse | Warning | Workflow target named by string
TMP2121 | TemporalSharp.SdkMisuse | Warning | Continue-as-new exception not thrown
TMP2131 | TemporalSharp.SdkMisuse | Warning | Non-replay-aware logging in workflow code
TMP2141 | TemporalSharp.SdkMisuse | Warning | Non-serializable type in workflow/activity signature
TMP2151 | TemporalSharp.SdkMisuse | Disabled | Sensitive-data parameter or property
TMP2161 | TemporalSharp.SdkMisuse | Disabled | Search attribute never upserted
TMP2171 | TemporalSharp.SdkMisuse | Disabled | Lossy-number parameter in workflow/activity signature
TMP3101 | TemporalSharp.SdkMisuse | Warning | Long-running activity does not heartbeat
TMP3102 | TemporalSharp.SdkMisuse | Warning | HeartbeatTimeout set but activity never heartbeats
TMP3201 | TemporalSharp.SdkMisuse | Warning | Invalid workflow entry method
TMP3202 | TemporalSharp.SdkMisuse | Warning | Invalid activity method
TMP3301 | TemporalSharp.SdkMisuse | Warning | Workflow versioning (patch) misuse
