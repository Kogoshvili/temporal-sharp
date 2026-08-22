; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
TMP0101 | TemporalSharp.Determinism | Warning | Wall-clock time in workflow code
TMP0111 | TemporalSharp.Determinism | Warning | Sleep/block in workflow code
TMP0121 | TemporalSharp.Determinism | Warning | Non-deterministic randomness in workflow code
TMP0131 | TemporalSharp.Determinism | Warning | I/O or environment access in workflow code
TMP1101 | TemporalSharp.WorkflowState | Warning | Static state mutation in workflow code
TMP2101 | TemporalSharp.SdkMisuse | Warning | Activity options missing required timeout
TMP2111 | TemporalSharp.SdkMisuse | Warning | Workflow target named by string
TMP2121 | TemporalSharp.SdkMisuse | Warning | Continue-as-new exception not thrown
TMP2131 | TemporalSharp.SdkMisuse | Warning | Non-replay-aware logging in workflow code
