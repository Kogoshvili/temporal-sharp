# Activity-options presets

`Temporal:ActivityOptions` seeds default and named activity-options presets
(timeouts, retry policy, cancellation type, task queue). A single preset maps to
both a regular `ActivityOptions` and a `LocalActivityOptions` — regular-only
fields (`HeartbeatTimeout`, `TaskQueue`) and the local-only `LocalRetryThreshold`
apply only where supported. Workflows resolve them through the static
`ActivityOps` facade — workflows run in the replay sandbox and cannot use DI, so
the registry is populated once at `AddTemporal` time and only read during
execution:

```csharp
[Workflow]
public sealed class MyWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name)
    {
        await ActivityOps.ExecuteAsync(() => MyActivities.DoIt(name));             // default preset
        await ActivityOps.ExecuteAsync(() => MyActivities.DoIt(name), "long-running"); // named preset
        await ActivityOps.ExecuteAsync(() => MyActivities.DoIt(name),              // explicit options
            new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(30) });

        await ActivityOps.ExecuteLocalAsync(() => MyActivities.LocalDoIt(name));   // local default
        await ActivityOps.ExecuteLocalAsync(() => MyActivities.LocalDoIt(name), "fast");

        return "done";
    }
}
```

`ActivityOps` mirrors `Workflow.ExecuteActivityAsync` /
`Workflow.ExecuteLocalActivityAsync` with no reduction in surface: pass a preset
name, omit it for the default, or pass an explicit `ActivityOptions` /
`LocalActivityOptions`. The regular and local defaults are independent —
`Temporal:ActivityOptions:Default` and `Temporal:ActivityOptions:LocalDefault`
respectively — while the `Presets` map is shared by both.

Each preset must set `ScheduleToCloseTimeout` or `StartToCloseTimeout` (the
SDK's own rule); unset properties leave the SDK defaults, and an unset `Retry`
means "retry forever". Presets are captured at startup and are **not**
live-reloaded, to keep workflow replay deterministic. `ActivityOptionsRegistry`
remains available for cases that need the raw options object (`Get`, `GetLocal`,
`GetDefault`, `GetLocalDefault`, `Resolve`, `ResolveLocal`); all return clones so
callers may mutate the result safely.

If you don't configure a default preset, a **built-in default** is used so
`ActivityOps.ExecuteAsync(call)` / `ActivityOps.ExecuteLocalAsync(call)` work
out of the box: regular activities default to a five-minute
`ScheduleToCloseTimeout`, local activities to ten seconds. Override them with
`Temporal:ActivityOptions:Default` and `Temporal:ActivityOptions:LocalDefault`,
or read the built-ins directly via `ActivityOptionsRegistry.BuiltInDefault` /
`ActivityOptionsRegistry.BuiltInLocalDefault`.
