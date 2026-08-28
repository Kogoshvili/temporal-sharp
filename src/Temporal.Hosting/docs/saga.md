# Saga (compensation)

`Saga` collects compensation operations for a workflow and runs them when a
step fails. It is a port of the Java SDK's `Saga` helper: compensations are
registered *before* the forward activity they undo, then unwound via
`CompensateAsync()` from a catch block.

## Minimal setup

`Saga` needs no configuration. Register each compensation before the forward
activity it undoes, then call `CompensateAsync()` on failure:

```csharp
using Kogoshvili.Temporal.Hosting;
using Temporalio.Workflows;

[Workflow]
public sealed class SagaWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string orderId)
    {
        var saga = new Saga();
        var options = new ActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10) };

        try
        {
            saga.AddCompensation(async () =>
                await Workflow.ExecuteActivityAsync(
                    () => Activities.CancelReservation(orderId), options));

            await Workflow.ExecuteActivityAsync(() => Activities.Reserve(orderId), options);

            saga.AddCompensation(async () =>
                await Workflow.ExecuteActivityAsync(
                    () => Activities.CancelAllocation(orderId), options));

            await Workflow.ExecuteActivityAsync(() => Activities.Allocate(orderId), options);

            // Fails, so the two compensations run in LIFO order:
            // cancel-allocation, then cancel-reservation.
            await Workflow.ExecuteActivityAsync(() => Activities.Charge(orderId), options);
        }
        catch (Exception ex)
        {
            Workflow.Logger.LogWarning(ex, "Charge failed; compensating");
            await saga.CompensateAsync();
            return "compensated";
        }

        return "completed without compensation";
    }
}
```

A compensation is a plain closure, so it can also be synchronous via
`AddCompensation(Action)`:

```csharp
saga.AddCompensation(() => SomeLoggingUndo(orderId));
```

## Configuration

`Saga` has no configuration of its own. It is affected only by activity options,
because compensations are ordinary `Func<Task>` closures that typically call
`Workflow.ExecuteActivityAsync(...)`. Their retry policy and timeouts come from
whatever `ActivityOptions` those calls use — resolved from
`Temporal:ActivityOptions` when going through the `ActivityOps` facade:

```json
{
  "Temporal": {
    "ActivityOptions": {
      "Default": {
        "ScheduleToCloseTimeout": "00:05:00",
        "HeartbeatTimeout": "00:00:30"
      }
    }
  }
}
```

```csharp
saga.AddCompensation(async () =>
    await ActivityOps.ExecuteAsync(
        () => Activities.CancelReservation(orderId)));
```

## Full configuration

The only knobs are on `SagaOptions`, passed to the `Saga` constructor:

```csharp
var saga = new Saga(new SagaOptions
{
    ParallelCompensation = true,
    ContinueWithError = false,
});
```

| Property | Default | Effect |
| --- | --- | --- |
| `ParallelCompensation` | `false` | When `false`, compensations run in reverse registration order (LIFO). When `true`, all compensations run and any failures are aggregated. |
| `ContinueWithError` | `false` | Only applies when `ParallelCompensation` is `false`. When `true`, a failed compensation is swallowed and the remaining ones still run; when `false` (default), compensation stops at the first failure and rethrows it. |

Sequential compensation runs in LIFO order and rethrows the first failure by
default; with `ContinueWithError = true` it swallows that failure and continues
through the rest. Parallel compensation runs every operation and surfaces all
failures as a single `ApplicationFailureException` wrapping an
`AggregateException`:

```csharp
var saga = new Saga(new SagaOptions { ParallelCompensation = true });

try
{
    // ... forward activities, each preceded by AddCompensation ...
}
catch (Exception)
{
    try
    {
        await saga.CompensateAsync();
    }
    catch (ApplicationFailureException ex) when (ex.InnerException is AggregateException agg)
    {
        // Every failed compensation is an inner exception of agg.
    }
}
```

There is no bound on the number of compensations; each is stored in registration
order and only invoked when `CompensateAsync()` is called. `CompensateAsync` is
idempotent over the registered set but does not clear it, so calling it more than
once runs the same compensations again. `AddCompensation` throws
`ArgumentNullException` for a `null` operation.
