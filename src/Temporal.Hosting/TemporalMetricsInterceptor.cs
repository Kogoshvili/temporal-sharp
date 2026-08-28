using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Client.Interceptors;
using Temporalio.Worker.Interceptors;

namespace Kogoshvili.Temporal.Hosting;

/// <summary>
/// Client and worker interceptor that records high-level client operations and
/// activity executions to a <see cref="System.Diagnostics.Metrics.Meter"/>,
/// filling the tag gaps the Temporal Core SDK's own metrics do not cover
/// (notably <c>workflow.id</c> and allowlisted OpenTelemetry baggage).
/// </summary>
internal sealed class TemporalMetricsInterceptor : IClientInterceptor, IWorkerInterceptor
{
    private readonly string ns;
    private readonly IReadOnlyCollection<string> baggageTagKeys;

    private readonly Counter<long> workflowStartCount;
    private readonly Histogram<double> workflowStartDuration;
    private readonly Counter<long> workflowSignalCount;
    private readonly Histogram<double> workflowSignalDuration;
    private readonly Counter<long> workflowQueryCount;
    private readonly Histogram<double> workflowQueryDuration;
    private readonly Counter<long> workflowUpdateCount;
    private readonly Histogram<double> workflowUpdateDuration;
    private readonly Counter<long> workflowCancelCount;
    private readonly Histogram<double> workflowCancelDuration;
    private readonly Counter<long> workflowTerminateCount;
    private readonly Histogram<double> workflowTerminateDuration;
    private readonly Counter<long> activityExecutionCount;
    private readonly Histogram<double> activityExecutionDuration;

    public TemporalMetricsInterceptor(Meter meter, string ns, IReadOnlyCollection<string> baggageTagKeys)
    {
        this.ns = ns;
        this.baggageTagKeys = baggageTagKeys;

        workflowStartCount = meter.CreateCounter<long>(
            "temporal.client.workflow.start.count",
            "workflows",
            "Number of workflow start requests issued by this client.");
        workflowStartDuration = meter.CreateHistogram<double>(
            "temporal.client.workflow.start.duration",
            "ms",
            "Duration of workflow start requests issued by this client.");

        workflowSignalCount = meter.CreateCounter<long>(
            "temporal.client.workflow.signal.count",
            "signals",
            "Number of workflow signal requests issued by this client.");
        workflowSignalDuration = meter.CreateHistogram<double>(
            "temporal.client.workflow.signal.duration",
            "ms",
            "Duration of workflow signal requests issued by this client.");

        workflowQueryCount = meter.CreateCounter<long>(
            "temporal.client.workflow.query.count",
            "queries",
            "Number of workflow query requests issued by this client.");
        workflowQueryDuration = meter.CreateHistogram<double>(
            "temporal.client.workflow.query.duration",
            "ms",
            "Duration of workflow query requests issued by this client.");

        workflowUpdateCount = meter.CreateCounter<long>(
            "temporal.client.workflow.update.count",
            "updates",
            "Number of workflow update requests issued by this client.");
        workflowUpdateDuration = meter.CreateHistogram<double>(
            "temporal.client.workflow.update.duration",
            "ms",
            "Duration of workflow update requests issued by this client.");

        workflowCancelCount = meter.CreateCounter<long>(
            "temporal.client.workflow.cancel.count",
            "cancellations",
            "Number of workflow cancellation requests issued by this client.");
        workflowCancelDuration = meter.CreateHistogram<double>(
            "temporal.client.workflow.cancel.duration",
            "ms",
            "Duration of workflow cancellation requests issued by this client.");

        workflowTerminateCount = meter.CreateCounter<long>(
            "temporal.client.workflow.terminate.count",
            "terminations",
            "Number of workflow termination requests issued by this client.");
        workflowTerminateDuration = meter.CreateHistogram<double>(
            "temporal.client.workflow.terminate.duration",
            "ms",
            "Duration of workflow termination requests issued by this client.");

        activityExecutionCount = meter.CreateCounter<long>(
            "temporal.worker.activity.execution.count",
            "executions",
            "Number of activity executions processed by this worker.");
        activityExecutionDuration = meter.CreateHistogram<double>(
            "temporal.worker.activity.execution.duration",
            "ms",
            "Duration of activity executions processed by this worker.");
    }

    /// <inheritdoc />
    public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next) =>
        new Outbound(this, next);

    /// <inheritdoc />
    public WorkflowInboundInterceptor InterceptWorkflow(WorkflowInboundInterceptor next) => next;

    /// <inheritdoc />
    public ActivityInboundInterceptor InterceptActivity(ActivityInboundInterceptor next) =>
        new ActivityInbound(this, next);

    private TagList BuildTags(bool error, params (string Key, object? Value)[] extra)
    {
        var tags = new TagList();
        tags.Add("namespace", ns);
        foreach (var (key, value) in extra)
        {
            if (value is not null)
            {
                tags.Add(key, value);
            }
        }

        tags.Add("error", error);
        AppendBaggageTags(ref tags);
        return tags;
    }

    private void AppendBaggageTags(ref TagList tags)
    {
        if (baggageTagKeys.Count == 0)
        {
            return;
        }

        var baggage = Baggage.Current;
        foreach (var key in baggageTagKeys)
        {
            if (baggage.GetBaggage(key) is { } value)
            {
                tags.Add($"baggage.{key}", value);
            }
        }
    }

    private sealed class Outbound : ClientOutboundInterceptor
    {
        private readonly TemporalMetricsInterceptor root;

        public Outbound(TemporalMetricsInterceptor root, ClientOutboundInterceptor next)
            : base(next) => this.root = root;

        public override async Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input)
        {
            var stopwatch = Stopwatch.StartNew();
            var error = false;
            try
            {
                return await base.StartWorkflowAsync<TWorkflow, TResult>(input).ConfigureAwait(false);
            }
            catch
            {
                error = true;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var tags = root.BuildTags(error, ("workflow.type", (object?)input.Workflow));
                root.workflowStartCount.Add(1, tags);
                root.workflowStartDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            }
        }

        public override async Task SignalWorkflowAsync(SignalWorkflowInput input)
        {
            var stopwatch = Stopwatch.StartNew();
            var error = false;
            try
            {
                await base.SignalWorkflowAsync(input).ConfigureAwait(false);
            }
            catch
            {
                error = true;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var tags = root.BuildTags(error, ("workflow.id", (object?)input.Id), ("signal", (object?)input.Signal));
                root.workflowSignalCount.Add(1, tags);
                root.workflowSignalDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            }
        }

        public override async Task<TResult> QueryWorkflowAsync<TResult>(QueryWorkflowInput input)
        {
            var stopwatch = Stopwatch.StartNew();
            var error = false;
            try
            {
                return await base.QueryWorkflowAsync<TResult>(input).ConfigureAwait(false);
            }
            catch
            {
                error = true;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var tags = root.BuildTags(error, ("workflow.id", (object?)input.Id), ("query", (object?)input.Query));
                root.workflowQueryCount.Add(1, tags);
                root.workflowQueryDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            }
        }

        public override async Task<WorkflowUpdateHandle<TResult>> StartWorkflowUpdateAsync<TResult>(
            StartWorkflowUpdateInput input)
        {
            var stopwatch = Stopwatch.StartNew();
            var error = false;
            try
            {
                return await base.StartWorkflowUpdateAsync<TResult>(input).ConfigureAwait(false);
            }
            catch
            {
                error = true;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var tags = root.BuildTags(error, ("workflow.id", (object?)input.Id), ("update", (object?)input.Update));
                root.workflowUpdateCount.Add(1, tags);
                root.workflowUpdateDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            }
        }

        public override async Task CancelWorkflowAsync(CancelWorkflowInput input)
        {
            var stopwatch = Stopwatch.StartNew();
            var error = false;
            try
            {
                await base.CancelWorkflowAsync(input).ConfigureAwait(false);
            }
            catch
            {
                error = true;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var tags = root.BuildTags(error, ("workflow.id", (object?)input.Id));
                root.workflowCancelCount.Add(1, tags);
                root.workflowCancelDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            }
        }

        public override async Task TerminateWorkflowAsync(TerminateWorkflowInput input)
        {
            var stopwatch = Stopwatch.StartNew();
            var error = false;
            try
            {
                await base.TerminateWorkflowAsync(input).ConfigureAwait(false);
            }
            catch
            {
                error = true;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var tags = root.BuildTags(error, ("workflow.id", (object?)input.Id));
                root.workflowTerminateCount.Add(1, tags);
                root.workflowTerminateDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            }
        }
    }

    private sealed class ActivityInbound : ActivityInboundInterceptor
    {
        private readonly TemporalMetricsInterceptor root;

        public ActivityInbound(TemporalMetricsInterceptor root, ActivityInboundInterceptor next)
            : base(next) => this.root = root;

        public override async Task<object?> ExecuteActivityAsync(ExecuteActivityInput input)
        {
            var stopwatch = Stopwatch.StartNew();
            var error = false;
            try
            {
                return await base.ExecuteActivityAsync(input).ConfigureAwait(false);
            }
            catch
            {
                error = true;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var info = ActivityExecutionContext.Current.Info;
                var tags = root.BuildTags(
                    error,
                    ("activity.type", (object?)info.ActivityType),
                    ("workflow.id", (object?)info.WorkflowId),
                    ("workflow.type", (object?)info.WorkflowType),
                    ("task.queue", (object?)info.TaskQueue));
                root.activityExecutionCount.Add(1, tags);
                root.activityExecutionDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            }
        }
    }
}
