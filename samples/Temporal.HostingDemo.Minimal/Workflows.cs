using Kogoshvili.Temporal.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Workflows;

namespace Kogoshvili.Temporal.HostingDemo.Minimal;

// Every feature here works with the starter's defaults: the default
// activity-options preset (Temporal:ActivityOptions:Default), the default
// workflow options (Temporal:Workflows:Default), and no named presets.

[Workflow]
public sealed class GreetingWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string name) =>
        await ActivityOps.ExecuteAsync(() => DemoActivities.Greet(name));
}

public static class DemoActivities
{
    [Activity]
    public static string Greet(string name) => $"Hello from Kogoshvili.Temporal.Hosting, {name}!";

    [Activity]
    public static string LocalEcho(string value) => value.ToUpperInvariant();

    [Activity]
    public static string Reserve(string orderId) => $"reserved {orderId}";

    [Activity]
    public static string Allocate(string orderId) => $"allocated {orderId}";

    [Activity]
    public static string Charge(string orderId) =>
        throw new InvalidOperationException($"charge failed for {orderId}");

    [Activity]
    public static string CancelReservation(string orderId) => $"cancel-reservation {orderId}";

    [Activity]
    public static string CancelAllocation(string orderId) => $"cancel-allocation {orderId}";
}

/// <summary>Checkpoint recorded on every heartbeat, used to resume on retry.</summary>
public sealed record DownloadProgress(int BytesDownloaded, int TotalBytes);

/// <summary>
/// A long-running activity built on the <see cref="HeartbeatingActivity"/> base:
/// resume from the last checkpoint, an opt-in background auto-heartbeat, and
/// explicit checkpoints — all defaults, no named presets.
/// </summary>
public sealed class DownloadActivities : HeartbeatingActivity
{
    [Activity]
    public async Task<int> DownloadAsync(int totalBytes)
    {
        var progress = await LoadProgressAsync<DownloadProgress>()
            ?? new DownloadProgress(0, totalBytes);

        using var heartbeat = StartAutoHeartbeat();

        while (progress.BytesDownloaded < progress.TotalBytes)
        {
            CheckCancellation();
            await Task.Delay(50);
            progress = progress with { BytesDownloaded = progress.BytesDownloaded + 1 };
            Heartbeat(progress);
        }

        return progress.BytesDownloaded;
    }
}

[Workflow]
public sealed class DownloadWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(int totalBytes)
    {
        var downloaded = await ActivityOps.ExecuteAsync(
            () => new DownloadActivities().DownloadAsync(totalBytes));

        return $"Downloaded {downloaded}/{totalBytes} bytes.";
    }
}

[Workflow]
public sealed class LocalActivityWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync()
    {
        var result = await ActivityOps.ExecuteLocalAsync(
            () => DemoActivities.LocalEcho("local"));

        return $"Local activity: {result}";
    }
}

/// <summary>
/// Demonstrates the <see cref="Saga"/> compensation helper with the default
/// activity preset: forward activities register compensations that unwind in
/// reverse (LIFO) order when <c>Charge</c> fails.
/// </summary>
[Workflow]
public sealed class SagaWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string orderId)
    {
        var saga = new Saga();

        try
        {
            saga.AddCompensation(() =>
                ActivityOps.ExecuteAsync(() => DemoActivities.CancelReservation(orderId)));

            await ActivityOps.ExecuteAsync(() => DemoActivities.Reserve(orderId));

            saga.AddCompensation(() =>
                ActivityOps.ExecuteAsync(() => DemoActivities.CancelAllocation(orderId)));

            await ActivityOps.ExecuteAsync(() => DemoActivities.Allocate(orderId));

            await ActivityOps.ExecuteAsync(() => DemoActivities.Charge(orderId));
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

/// <summary>
/// A simple child workflow run via <see cref="ChildWorkflowOps"/>. It is only
/// ever started as a child, so its task queue and child workflow ID resolve from
/// the starter's defaults (<c>Temporal:Workflows:Default</c> and the shipped
/// child ID convention).
/// </summary>
[Workflow]
public sealed class ChildWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string value) =>
        await ActivityOps.ExecuteAsync(() => DemoActivities.Greet(value));
}

/// <summary>
/// Demonstrates <see cref="ChildWorkflowOps.ExecuteAsync"/>: starts a child
/// workflow with options and an ID resolved from config, with no per-call
/// plumbing.
/// </summary>
[Workflow]
public sealed class ParentWorkflow
{
    [WorkflowRun]
    public async Task<string> RunAsync(string value)
    {
        var childResult = await ChildWorkflowOps.ExecuteAsync<ChildWorkflow, string, string>(value);

        return $"parent -> child said: {childResult}";
    }
}
