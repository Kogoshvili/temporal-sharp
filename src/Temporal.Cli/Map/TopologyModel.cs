namespace Kogoshvili.Temporal.Cli.Map;

/// <summary>Node categories emitted by the topology graph.</summary>
internal static class TopologyNodeKinds
{
    public const string Workflow = "workflow";
    public const string Activity = "activity";
    public const string Nexus = "nexus";
    public const string TaskQueue = "taskQueue";
    public const string Unknown = "unknown";
    public const string Contract = "contract";
    public const string Caller = "caller";
}

/// <summary>Handler kinds surfaced as workflow sub-nodes/ports.</summary>
internal static class TopologyHandlerKinds
{
    public const string Run = "run";
    public const string Signal = "signal";
    public const string Query = "query";
    public const string Update = "update";
}

/// <summary>Edge categories emitted by the topology graph.</summary>
internal static class TopologyEdgeKinds
{
    public const string Activity = "activity";
    public const string LocalActivity = "localActivity";
    public const string ChildWorkflow = "childWorkflow";
    public const string Nexus = "nexus";
    public const string TaskQueue = "taskQueue";
    public const string Signal = "signal";
    public const string Query = "query";
    public const string Update = "update";
    public const string StartWorkflow = "startWorkflow";
    public const string StandaloneActivity = "standaloneActivity";
}

/// <summary>A workflow entry point / message handler port.</summary>
internal sealed record TopologyHandler(string Kind, string Name, string? Signature = null);

/// <summary>
/// A single node in the topology graph. <see cref="Repo"/> is the input
/// solution name and <see cref="Path"/> the file relative to it;
/// <see cref="Unresolved"/> marks contract members with no implementation.
/// </summary>
internal sealed record TopologyNode(
    string Id,
    string Kind,
    string Name,
    string? File,
    int? Line,
    string? UnknownKind,
    IReadOnlyList<TopologyHandler> Handlers,
    bool? Standalone = null,
    bool? Heartbeats = null,
    bool? Unresolved = null,
    string? Repo = null,
    string? Path = null);

/// <summary>
/// A directed edge between two topology nodes. <see cref="Order"/> carries the
/// 1-based ordinals of the call sites (per calling workflow, in document
/// order) for activity edges; <see cref="InLoop"/> marks call sites nested in a
/// loop; <see cref="CallOptions"/> summarizes timeouts/retry from the call-site
/// options; <see cref="Heartbeats"/> marks heartbeat-reporting calls;
/// <see cref="HeartbeatIssue"/> flags a heartbeat timeout without a heartbeat.
/// Nullable members are omitted from JSON when absent.
/// </summary>
internal sealed record TopologyEdge(
    string From,
    string To,
    string Kind,
    string? Label,
    int[]? Order = null,
    bool? InLoop = null,
    string? CallOptions = null,
    bool? Heartbeats = null,
    bool? HeartbeatIssue = null);

/// <summary>The full, immutable topology of a solution.</summary>
internal sealed record TopologyGraph(
    IReadOnlyList<TopologyNode> Nodes,
    IReadOnlyList<TopologyEdge> Edges);
