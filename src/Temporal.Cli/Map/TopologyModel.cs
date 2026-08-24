namespace Kogoshvili.Temporal.Cli.Map;

/// <summary>Node categories emitted by the topology graph.</summary>
internal static class TopologyNodeKinds
{
    public const string Workflow = "workflow";
    public const string Activity = "activity";
    public const string Nexus = "nexus";
    public const string TaskQueue = "taskQueue";
    public const string Unknown = "unknown";
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
}

/// <summary>A workflow entry point / message handler port.</summary>
internal sealed record TopologyHandler(string Kind, string Name);

/// <summary>A single node in the topology graph.</summary>
internal sealed record TopologyNode(
    string Id,
    string Kind,
    string Name,
    string? File,
    int? Line,
    string? UnknownKind,
    IReadOnlyList<TopologyHandler> Handlers);

/// <summary>A directed edge between two topology nodes.</summary>
internal sealed record TopologyEdge(
    string From,
    string To,
    string Kind,
    string? Label);

/// <summary>The full, immutable topology of a solution.</summary>
internal sealed record TopologyGraph(
    IReadOnlyList<TopologyNode> Nodes,
    IReadOnlyList<TopologyEdge> Edges);
