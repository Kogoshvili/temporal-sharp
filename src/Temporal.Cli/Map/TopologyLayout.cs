namespace Kogoshvili.Temporal.Cli.Map;

/// <summary>
/// Computes how nodes are grouped for rendering: one container per task queue
/// (workflows and activities with exactly one statically resolved queue), an
/// unknown-queue container for nodes whose queue could not be resolved, and an
/// orphan container for uncalled activities with no queue. Nodes that belong
/// to several queues stay outside all containers — the emitters draw edges
/// from them to each of their queue boxes instead.
/// </summary>
internal static class TopologyLayout
{
    public sealed class Result
    {
        /// <summary>Task-queue node ids in display order (sorted by name).</summary>
        public IReadOnlyList<string> QueueOrder { get; init; } = [];

        /// <summary>Queue node id → member node ids, sorted.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> QueueMembers { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>();

        /// <summary>Node ids associated with two or more queues.</summary>
        public IReadOnlyList<string> MultiQueueNodes { get; init; } = [];

        /// <summary>Multi-queue node id → its queue node ids (display order).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> MultiQueueLinks { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>();

        /// <summary>Nodes whose only queue evidence is unresolvable.</summary>
        public IReadOnlyList<string> UnknownQueueMembers { get; init; } = [];

        /// <summary>Uncalled activities with no detected queue.</summary>
        public IReadOnlyList<string> OrphanMembers { get; init; } = [];
    }

    public static Result Compute(TopologyGraph graph)
    {
        var callerKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            TopologyEdgeKinds.Activity,
            TopologyEdgeKinds.LocalActivity,
            TopologyEdgeKinds.ChildWorkflow,
            TopologyEdgeKinds.Nexus,
        };
        var called = graph.Edges
            .Where(e => callerKinds.Contains(e.Kind))
            .Select(e => e.To)
            .ToHashSet(StringComparer.Ordinal);

        var queuesByNode = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            if (edge.Kind != TopologyEdgeKinds.TaskQueue)
            {
                continue;
            }

            if (!queuesByNode.TryGetValue(edge.From, out var list))
            {
                queuesByNode[edge.From] = list = [];
            }

            if (!list.Contains(edge.To))
            {
                list.Add(edge.To);
            }
        }

        var queueOrder = new List<string>();
        var queueMembers = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var multi = new List<string>();
        var multiLinks = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var unknownMembers = new List<string>();
        var orphans = new List<string>();

        foreach (var node in graph.Nodes)
        {
            // Task-queue nodes and the Unknown:TaskQueue boundary marker are
            // container metadata, not rendered nodes.
            if (node.Kind == TopologyNodeKinds.TaskQueue ||
                (node.Kind == TopologyNodeKinds.Unknown && node.UnknownKind == "taskQueue"))
            {
                continue;
            }

            var all = queuesByNode.TryGetValue(node.Id, out var q) ? q : [];
            var real = all.Where(t => t.StartsWith("TaskQueue:", StringComparison.Ordinal)).ToList();
            foreach (var queue in real)
            {
                if (!queueOrder.Contains(queue))
                {
                    queueOrder.Add(queue);
                }
            }

            switch (real.Count)
            {
                case 1:
                    if (!queueMembers.TryGetValue(real[0], out var members))
                    {
                        queueMembers[real[0]] = members = [];
                    }

                    members.Add(node.Id);
                    break;

                case > 1:
                    multi.Add(node.Id);
                    multiLinks[node.Id] = queueOrder
                        .Where(real.Contains)
                        .ToList();
                    break;

                default:
                    if (node.Kind == TopologyNodeKinds.Activity && !called.Contains(node.Id))
                    {
                        orphans.Add(node.Id);
                    }
                    else if (all.Count > 0)
                    {
                        unknownMembers.Add(node.Id);
                    }

                    break;
            }
        }

        queueOrder.Sort(StringComparer.Ordinal);
        foreach (var queue in queueOrder)
        {
            // A queue whose activities all landed in the multi-queue bucket has
            // no single-queue members; keep an empty members list so emitters
            // can still render the box.
            if (!queueMembers.TryGetValue(queue, out _))
            {
                queueMembers[queue] = [];
            }
        }

        return new Result
        {
            QueueOrder = queueOrder,
            QueueMembers = queueMembers.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal),
            MultiQueueNodes = multi,
            MultiQueueLinks = multiLinks,
            UnknownQueueMembers = unknownMembers,
            OrphanMembers = orphans,
        };
    }
}
