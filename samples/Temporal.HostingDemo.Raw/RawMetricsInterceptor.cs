using System.Diagnostics;
using System.Diagnostics.Metrics;
using Temporalio.Client;
using Temporalio.Client.Interceptors;

namespace Kogoshvili.Temporal.HostingDemo.Raw;

/// <summary>
/// Hand-rolled client interceptor that records workflow-start counts and
/// durations. The starter ships an equivalent built-in interceptor (internal)
/// and wires it up for you via <c>Metrics:Enabled</c>, expanding it to signal,
/// query, update, cancel, and terminate operations plus activity executions.
/// </summary>
public sealed class RawMetricsInterceptor : IClientInterceptor
{
    private readonly Counter<long> workflowStartCount;
    private readonly Histogram<double> workflowStartDuration;

    public RawMetricsInterceptor(Meter meter)
    {
        workflowStartCount = meter.CreateCounter<long>(
            "temporal.client.workflow.start.count",
            "workflows",
            "Number of workflow start requests issued by this client.");
        workflowStartDuration = meter.CreateHistogram<double>(
            "temporal.client.workflow.start.duration",
            "ms",
            "Duration of workflow start requests issued by this client.");
    }

    public ClientOutboundInterceptor InterceptClient(ClientOutboundInterceptor next) =>
        new Outbound(workflowStartCount, workflowStartDuration, next);

    private sealed class Outbound : ClientOutboundInterceptor
    {
        private readonly Counter<long> workflowStartCount;
        private readonly Histogram<double> workflowStartDuration;

        public Outbound(
            Counter<long> workflowStartCount,
            Histogram<double> workflowStartDuration,
            ClientOutboundInterceptor next)
            : base(next)
        {
            this.workflowStartCount = workflowStartCount;
            this.workflowStartDuration = workflowStartDuration;
        }

        public override async Task<WorkflowHandle<TWorkflow, TResult>> StartWorkflowAsync<TWorkflow, TResult>(
            StartWorkflowInput input)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                return await base.StartWorkflowAsync<TWorkflow, TResult>(input).ConfigureAwait(false);
            }
            finally
            {
                stopwatch.Stop();
                workflowStartCount.Add(1);
                workflowStartDuration.Record(stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }
}
