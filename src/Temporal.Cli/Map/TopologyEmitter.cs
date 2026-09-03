using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kogoshvili.Temporal.Cli.Map;

/// <summary>
/// Renders a <see cref="TopologyGraph"/> as JSON or a Mermaid flowchart.
/// </summary>
internal static class TopologyEmitter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ToJson(TopologyGraph graph) => JsonSerializer.Serialize(graph, JsonOptions);

    /// <summary>
    /// Mermaid init directive: white background (dark-theme viewers render the
    /// default transparent background poorly), max-width scaling, and tighter
    /// spacing so wide graphs do not stretch horizontally.
    /// </summary>
    public const string MermaidInitDirective =
        "%%{init: {\"theme\":\"base\",\"themeVariables\":{\"background\":\"#ffffff\",\"fontFamily\":\"Segoe UI, Helvetica, Arial, sans-serif\"},\"flowchart\":{\"useMaxWidth\":true,\"nodeSpacing\":35,\"rankSpacing\":45}}}%%";

    public static string ToMermaid(TopologyGraph graph, bool contracts = true, bool includeLegend = true)
    {
        var layout = TopologyLayout.Compute(graph);
        var sb = new StringBuilder();
        sb.AppendLine(MermaidInitDirective);
        sb.AppendLine("flowchart LR");
        sb.AppendLine("    classDef workflow fill:#e3f2fd,stroke:#1565c0,color:#000;");
        sb.AppendLine("    classDef activity fill:#fff3e0,stroke:#ef6c00,color:#000;");
        sb.AppendLine("    classDef nexus fill:#f3e5f5,stroke:#7b1fa2,color:#000;");
        sb.AppendLine("    classDef unknown fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5,color:#000;");
        sb.AppendLine("    classDef contract fill:#ede7f6,stroke:#5e35b1,stroke-dasharray: 3 3,color:#000;");
        sb.AppendLine("    classDef caller fill:#e0f7fa,stroke:#00838f,color:#000;");
        sb.AppendLine();

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var node in graph.Nodes)
        {
            ids[node.Id] = "n" + index;
            index++;
        }

        var queueNames = graph.Nodes
            .Where(n => n.Kind == TopologyNodeKinds.TaskQueue)
            .ToDictionary(n => n.Id, n => n.Name, StringComparer.Ordinal);
        var slugs = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < layout.QueueOrder.Count; i++)
        {
            slugs[layout.QueueOrder[i]] = "q" + i;
        }

        // Multi-queue nodes are rendered as clones inside every one of their
        // queue boxes (mermaid nodes cannot belong to two subgraphs).
        var cloneIds = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var kv in layout.MultiQueueLinks)
        {
            foreach (var queueId in kv.Value)
            {
                if (!cloneIds.TryGetValue(kv.Key, out var byQueue))
                {
                    cloneIds[kv.Key] = byQueue = new Dictionary<string, string>(StringComparer.Ordinal);
                }

                byQueue[queueId] = ids[kv.Key] + "_" + slugs[queueId];
            }
        }

        var boxed = layout.QueueMembers.Values
            .SelectMany(m => m)
            .Concat(layout.UnknownQueueMembers)
            .Concat(layout.OrphanMembers)
            .Concat(layout.MultiQueueNodes)
            .ToHashSet(StringComparer.Ordinal);

        void EmitNode(TopologyNode node, string? idOverride = null)
        {
            sb.Append("    ")
              .Append(idOverride ?? ids[node.Id])
              .Append("[\"")
              .Append(BuildLabel(node, contracts))
              .Append("\"]:::")
              .Append(ClassFor(node.Kind))
              .AppendLine();
        }

        string? firstMainNodeId = null;
        foreach (var node in graph.Nodes.Where(n => IsRenderable(n) && !boxed.Contains(n.Id)))
        {
            firstMainNodeId ??= ids[node.Id];
            EmitNode(node);
        }

        firstMainNodeId ??= layout.QueueOrder.Count > 0 ? slugs[layout.QueueOrder[0]] : null;

        foreach (var queueId in layout.QueueOrder)
        {
            sb.AppendLine();
            sb.Append("    subgraph ")
              .Append(slugs[queueId])
              .Append("[\"📥 ")
              .Append(Escape(queueNames.GetValueOrDefault(queueId, queueId)))
              .AppendLine("\"]");
            foreach (var member in layout.QueueMembers[queueId])
            {
                EmitNode(graph.Nodes.Single(n => n.Id == member));
            }

            foreach (var kv in layout.MultiQueueLinks)
            {
                if (kv.Value.Contains(queueId))
                {
                    EmitNode(graph.Nodes.Single(n => n.Id == kv.Key), cloneIds[kv.Key][queueId]);
                }
            }

            sb.AppendLine("    end");
            sb.Append("    style ").Append(slugs[queueId]).AppendLine(" fill:#e8f5e9,stroke:#2e7d32,color:#000");
        }

        if (layout.UnknownQueueMembers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    subgraph uq[\"❓ Unknown task queue\"]");
            foreach (var member in layout.UnknownQueueMembers)
            {
                EmitNode(graph.Nodes.Single(n => n.Id == member));
            }

            sb.AppendLine("    end");
            sb.AppendLine("    style uq fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5,color:#000");
        }

        if (layout.OrphanMembers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    subgraph orp[\"🪤 Orphaned activities (no static caller)\"]");
            foreach (var member in layout.OrphanMembers)
            {
                EmitNode(graph.Nodes.Single(n => n.Id == member));
            }

            sb.AppendLine("    end");
            sb.AppendLine("    style orp fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5,color:#000");
        }

        sb.AppendLine();
        string EndpointId(string nodeId, string fromNodeId)
        {
            if (cloneIds.TryGetValue(nodeId, out var byQueue))
            {
                var containing = byQueue.Keys.FirstOrDefault(
                    q => layout.QueueMembers.TryGetValue(q, out var m) && m.Contains(fromNodeId));
                return containing is not null ? byQueue[containing] : byQueue.Values.First();
            }

            return ids[nodeId];
        }

        var heartbeatIssueIndexes = new List<int>();
        var linkIndex = 0;
        foreach (var edge in graph.Edges)
        {
            if (edge.Kind == TopologyEdgeKinds.TaskQueue)
            {
                // Queue membership is conveyed by the boxes, not arrows.
                continue;
            }

            sb.Append("    ")
              .Append(EndpointId(edge.From, edge.To))
              .Append(' ')
              .Append(ArrowFor(edge, contracts));
            if (EdgeLabel(edge, contracts) is { } label)
            {
                sb.Append("|\"").Append(label).Append("\"|");
            }

            sb.Append(' ')
              .Append(EndpointId(edge.To, edge.From))
              .AppendLine();
            if (edge.HeartbeatIssue == true)
            {
                heartbeatIssueIndexes.Add(linkIndex);
            }

            linkIndex++;
        }

        if (heartbeatIssueIndexes.Count > 0)
        {
            sb.Append("    linkStyle ")
              .Append(string.Join(",", heartbeatIssueIndexes))
              .AppendLine(" stroke:#c62828,stroke-dasharray: 4 3");
        }

        if (includeLegend)
        {
            EmitMermaidLegend(sb, graph);
        }

        // Pin the legend before the main flow, then chain the disconnected
        // unknown/orphan boxes after it — dagre has no absolute positioning,
        // so invisible links are the only way to control reading order.
        if (firstMainNodeId is not null)
        {
            if (includeLegend)
            {
                sb.Append("    legend ~~~ ").AppendLine(firstMainNodeId);
            }
            if (layout.UnknownQueueMembers.Count > 0)
            {
                sb.Append("    ").Append(firstMainNodeId).AppendLine(" ~~~ uq");
                if (layout.OrphanMembers.Count > 0)
                {
                    sb.AppendLine("    uq ~~~ orp");
                }
            }
            else if (layout.OrphanMembers.Count > 0)
            {
                sb.Append("    ").Append(firstMainNodeId).AppendLine(" ~~~ orp");
            }
        }

        return sb.ToString();
    }

    private static void EmitMermaidLegend(StringBuilder sb, TopologyGraph graph)
    {
        sb.AppendLine();
        sb.AppendLine("    subgraph legend[\"📖 Legend\"]");
        sb.AppendLine("    lgWf[\"Workflow\"]:::workflow");
        sb.AppendLine("    lgAct[\"Activity\"]:::activity");
        sb.AppendLine("    lgSa[\"Standalone activity\"]:::activity");
        sb.AppendLine("    lgCt[\"Contract (ambiguous impls)\"]:::contract");
        sb.AppendLine("    lgCl[\"Caller (client)\"]:::caller");
        sb.AppendLine("    lgUn[\"Unresolved (string-named / cross-repo)\"]:::unknown");
        sb.AppendLine("    lgAr[\"--> call (#1, #3 = order, 🔁 = in loop)<br/>--o local activity<br/>-.-> child workflow<br/>==> nexus<br/><--> activity heartbeats<br/>--x heartbeat timeout, no heartbeat (issue)\"]:::unknown");
        sb.AppendLine("    end");
        sb.AppendLine("    style legend fill:#fafafa,stroke:#90a4ae,color:#000");
    }

    /// <summary>
    /// True when the node should appear on the canvas. Task-queue nodes and
    /// the Unknown:TaskQueue boundary marker are container metadata instead.
    /// </summary>
    private static bool IsRenderable(TopologyNode n) =>
        n.Kind != TopologyNodeKinds.TaskQueue &&
        !(n.Kind == TopologyNodeKinds.Unknown && n.UnknownKind == "taskQueue");

    /// <summary>
    /// Renders the edge annotation: call-order / loop for activity edges (e.g.
    /// <c>#1, #3</c>, <c>#2 🔁</c>) plus, under the contracts flag, the
    /// call-site options summary (e.g. <c>[StartToClose=30s]</c>).
    /// </summary>
    private static string? EdgeLabel(TopologyEdge edge, bool contracts)
    {
        string? label;
        if (edge.Kind is TopologyEdgeKinds.Activity or TopologyEdgeKinds.LocalActivity)
        {
            label = null;
            if (edge.Order is { Length: > 0 } orders)
            {
                label = string.Join(", ", orders.Select(o => "#" + o));
            }

            if (edge.InLoop == true)
            {
                label = label is null ? "🔁" : label + " 🔁";
            }
        }
        else if (edge.Kind == TopologyEdgeKinds.StandaloneActivity)
        {
            label = "standalone";
        }
        else
        {
            label = null;
        }

        if (contracts && edge.CallOptions is { } options)
        {
            label = label is null ? "[" + options + "]" : label + " [" + options + "]";
        }

        return label;
    }

    private static string BuildLabel(TopologyNode node, bool contracts)
    {
        if (node.Kind == TopologyNodeKinds.Unknown)
        {
            var category = node.UnknownKind is null ? "Unknown" : Capitalize(node.UnknownKind);
            return "❓ " + Escape(category + ": " + node.Name);
        }

        if (node.Kind == TopologyNodeKinds.Caller)
        {
            return "🖥 " + Escape(node.Name);
        }

        if (node.Kind == TopologyNodeKinds.Contract)
        {
            return "⧉ " + Escape(node.Name);
        }

        var suffix = node.Standalone == true ? " ⚡" : string.Empty;
        suffix += node.Heartbeats == true ? " 💓" : string.Empty;
        suffix += node.Unresolved == true ? " ❔" : string.Empty;
        if (node.Handlers.Count == 0)
        {
            return Escape(node.Name) + suffix + LocationSubLine(node);
        }

        var sb = new StringBuilder(Escape(node.Name) + suffix);
        foreach (var handler in node.Handlers)
        {
            sb.Append("<br/><i>")
              .Append(Escape(handler.Kind))
              .Append(": ")
              .Append(Escape(handler.Name));
            if (contracts && handler.Signature is { } signature)
            {
                sb.Append('(').Append(Escape(signature)).Append(')');
            }

            sb.Append("</i>");
        }

        return sb.Append(LocationSubLine(node)).ToString();
    }

    /// <summary>
    /// A plain-markdown legend for file formats with space outside the diagram
    /// (markdown output); mirrors the in-graph legend.
    /// </summary>
    public const string MarkdownLegend = """
        | Marker | Meaning |
        | --- | --- |
        | Blue box | Workflow |
        | Orange ellipse | Activity |
        | Purple diamond | Nexus operation |
        | Green `📥` box | Task queue — contained nodes run on it |
        | `🖥` teal box | Caller (client-side entry point) |
        | `⧉` dashed box | Contract (ambiguous interface impls) |
        | Red dashed box | Unknown task queue / orphaned activities |
        | `⚡` | Standalone activity |
        | `💓` | Activity calls `Heartbeat(...)` |
        | `-->` | Activity call — `#1, #3` call order, `🔁` inside a loop |
        | `--o` | Local activity call |
        | `-.->` | Child workflow |
        | `==>` | Nexus operation/service |
        | `<-->` | Activity heartbeats back to its caller |
        | `--x` (red) | Issue: heartbeat timeout set, but no heartbeat call |
        | `❓` | String-named target not resolved by name |
        | `❔` | Contract member with no implementation |
        | `<sub>Repo/Path:Line</sub>` | Where the node lives; `?` when unknown |
        | Multi-queue nodes | Duplicated inside every queue they belong to |
        """;

    /// <summary>
    /// Renders the <c>Repo/Path:Line</c> provenance sub-line, or a bare
    /// question mark when the node has no source location.
    /// </summary>
    private static string LocationSubLine(TopologyNode node)
    {
        if (string.IsNullOrEmpty(node.Repo) && string.IsNullOrEmpty(node.Path))
        {
            return "<br/><sub>?</sub>";
        }

        var location = string.IsNullOrEmpty(node.Path)
            ? node.Repo!
            : node.Repo is null ? node.Path! : node.Repo + "/" + node.Path;
        if (node.Line is { } line)
        {
            location += ":" + line;
        }

        return "<br/><sub>" + Escape(location) + "</sub>";
    }

    private static string ClassFor(string kind) => kind switch
    {
        TopologyNodeKinds.Workflow => "workflow",
        TopologyNodeKinds.Activity => "activity",
        TopologyNodeKinds.Nexus => "nexus",
        TopologyNodeKinds.TaskQueue => "taskQueue",
        TopologyNodeKinds.Contract => "contract",
        TopologyNodeKinds.Caller => "caller",
        _ => "unknown",
    };

    private static string ArrowFor(TopologyEdge edge, bool contracts)
    {
        if (edge.Kind is TopologyEdgeKinds.Activity or TopologyEdgeKinds.LocalActivity)
        {
            if (edge.HeartbeatIssue == true)
            {
                return "--x";
            }

            if (edge.Heartbeats == true)
            {
                return "<-->";
            }

            return edge.Kind == TopologyEdgeKinds.LocalActivity ? "--o" : "-->";
        }

        return edge.Kind switch
        {
            TopologyEdgeKinds.ChildWorkflow => "-.->",
            TopologyEdgeKinds.Nexus => "==>",
            _ => "-->",
        };
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");

    private static string Capitalize(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);

    /// <summary>
    /// Renders the graph as Graphviz DOT.
    /// </summary>
    public static string ToDot(TopologyGraph graph, bool contracts = true)
    {
        var layout = TopologyLayout.Compute(graph);
        var sb = new StringBuilder();
        sb.AppendLine("digraph temporal_topology {");
        sb.AppendLine("    graph [rankdir=LR, splines=spline, compound=true, bgcolor=\"#ffffff\", nodesep=0.35, ranksep=0.55];");
        sb.AppendLine("    node [fontname=\"Helvetica\"];");
        sb.AppendLine();

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var node in graph.Nodes)
        {
            ids[node.Id] = "n" + index;
            index++;
        }

        var nodesById = graph.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var queueNames = graph.Nodes
            .Where(n => n.Kind == TopologyNodeKinds.TaskQueue)
            .ToDictionary(n => n.Id, n => n.Name, StringComparer.Ordinal);
        var slugs = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < layout.QueueOrder.Count; i++)
        {
            slugs[layout.QueueOrder[i]] = "cluster_q" + i;
        }

        var boxed = layout.QueueMembers.Values
            .SelectMany(m => m)
            .Concat(layout.UnknownQueueMembers)
            .Concat(layout.OrphanMembers)
            .ToHashSet(StringComparer.Ordinal);

        void EmitNode(TopologyNode node, string indent)
        {
            var isDashed = node.Kind is TopologyNodeKinds.Unknown or TopologyNodeKinds.Contract;
            sb.Append(indent)
              .Append(ids[node.Id])
              .Append(" [label=").Append(DotQuote(DotLabel(node, contracts)))
              .Append(", shape=").Append(ShapeFor(node.Kind))
              .Append(", style=\"").Append(isDashed ? "filled,dashed" : "filled").Append('"')
              .Append(", fillcolor=").Append(DotQuote(ColorFor(node.Kind)))
              .AppendLine("];");
        }

        foreach (var node in graph.Nodes.Where(n => IsRenderable(n) && !boxed.Contains(n.Id)))
        {
            EmitNode(node, "    ");
        }

        foreach (var queueId in layout.QueueOrder)
        {
            sb.AppendLine();
            sb.Append("    subgraph ").Append(slugs[queueId]).AppendLine(" {");
            sb.Append("        label=").Append(DotQuote("📥 " + queueNames.GetValueOrDefault(queueId, queueId))).AppendLine(";");
            sb.AppendLine("        style=\"rounded,filled\";");
            sb.AppendLine("        fillcolor=\"#e8f5e9\";");
            sb.AppendLine("        color=\"#2e7d32\";");
            foreach (var member in layout.QueueMembers[queueId])
            {
                EmitNode(nodesById[member], "        ");
            }

            sb.AppendLine("    }");
        }

        if (layout.UnknownQueueMembers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    subgraph cluster_uq {");
            sb.AppendLine("        label=\"❓ Unknown task queue\";");
            sb.AppendLine("        style=\"rounded,dashed\";");
            sb.AppendLine("        fillcolor=\"#fbe9e7\";");
            sb.AppendLine("        color=\"#c62828\";");
            foreach (var member in layout.UnknownQueueMembers)
            {
                EmitNode(nodesById[member], "        ");
            }

            sb.AppendLine("    }");
        }

        if (layout.OrphanMembers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("    subgraph cluster_orp {");
            sb.AppendLine("        label=\"🪤 Orphaned activities (no static caller)\";");
            sb.AppendLine("        style=\"rounded,dashed\";");
            sb.AppendLine("        fillcolor=\"#fbe9e7\";");
            sb.AppendLine("        color=\"#c62828\";");
            foreach (var member in layout.OrphanMembers)
            {
                EmitNode(nodesById[member], "        ");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine();
        foreach (var kv in layout.MultiQueueLinks)
        {
            foreach (var queueId in kv.Value)
            {
                if (!layout.QueueMembers.TryGetValue(queueId, out var members) || members.Count == 0)
                {
                    continue;
                }

                sb.Append("    ")
                  .Append(ids[kv.Key])
                  .Append(" -> ")
                  .Append(ids[members[0]])
                  .Append(" [color=\"#2e7d32\", style=dotted, lhead=").Append(slugs[queueId]).AppendLine("];");
            }
        }

        // Invisible edges pin the disconnected unknown/orphan clusters after
        // the main flow instead of letting Graphviz float them above it.
        var firstMainNode = graph.Nodes.FirstOrDefault(n => IsRenderable(n) && !boxed.Contains(n.Id));
        if (firstMainNode is not null)
        {
            if (layout.UnknownQueueMembers.Count > 0)
            {
                sb.Append("    ").Append(ids[firstMainNode.Id]).Append(" -> ")
                  .Append(ids[layout.UnknownQueueMembers[0]]).AppendLine(" [style=invis];");
            }

            if (layout.OrphanMembers.Count > 0)
            {
                sb.Append("    ").Append(ids[firstMainNode.Id]).Append(" -> ")
                  .Append(ids[layout.OrphanMembers[0]]).AppendLine(" [style=invis];");
            }
        }

        foreach (var edge in graph.Edges)
        {
            if (edge.Kind == TopologyEdgeKinds.TaskQueue)
            {
                // Queue membership is conveyed by the clusters, not edges.
                continue;
            }

            var attrs = new List<string>
            {
                "color=" + DotQuote(DotEdgeColorFor(edge)),
                "style=" + DotQuote(DotEdgeStyleFor(edge)),
            };
            if (edge.Heartbeats == true && edge.Kind is TopologyEdgeKinds.Activity or TopologyEdgeKinds.LocalActivity)
            {
                attrs.Add("dir=both");
            }

            if (edge.HeartbeatIssue == true)
            {
                attrs.Add("arrowhead=x");
            }

            if (DotEdgeLabel(edge, contracts) is { } label)
            {
                attrs.Add("label=" + DotQuote(label));
            }

            sb.Append("    ")
              .Append(ids[edge.From])
              .Append(" -> ")
              .Append(ids[edge.To])
              .Append(" [")
              .Append(string.Join(", ", attrs))
              .AppendLine("];");
        }

        EmitDotLegend(sb, graph, ids);

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitDotLegend(StringBuilder sb, TopologyGraph graph, Dictionary<string, string> ids)
    {
        sb.AppendLine();
        sb.AppendLine("    subgraph cluster_legend {");
        sb.AppendLine("        label=\"📖 Legend\";");
        sb.AppendLine("        style=\"rounded\";");
        sb.AppendLine("        color=\"#90a4ae\";");
        sb.AppendLine("        lgWf [label=\"Workflow\", shape=box, style=\"filled\", fillcolor=\"#e3f2fd\"];");
        sb.AppendLine("        lgAct [label=\"Activity / ⚡ standalone / 💓 heartbeats\", shape=ellipse, style=\"filled\", fillcolor=\"#fff3e0\"];");
        sb.AppendLine("        lgCt [label=\"⧉ Contract (ambiguous impls)\", shape=box, style=\"filled,dashed\", fillcolor=\"#ede7f6\"];");
        sb.AppendLine("        lgCl [label=\"🖥 Caller (client)\", shape=box, style=\"filled\", fillcolor=\"#e0f7fa\"];");
        sb.AppendLine("        lgUn [label=\"Unresolved (string-named / cross-repo)\", shape=box, style=\"filled,dashed\", fillcolor=\"#fbe9e7\"];");
        sb.AppendLine("        lgAr [label=\"-> call (#order, 🔁 loop)\\n-o> local activity\\n.-> child workflow\\n=> nexus\\n<->> heartbeats\\n-x> heartbeat issue\", shape=box, style=\"filled\", fillcolor=\"#ffffff\"];");
        sb.AppendLine("    }");
    }

    private static string DotLabel(TopologyNode node, bool contracts)
    {
        if (node.Kind == TopologyNodeKinds.Unknown)
        {
            var category = node.UnknownKind is null ? "Unknown" : Capitalize(node.UnknownKind);
            return DotEscape(category + ": " + node.Name);
        }

        var prefix = node.Kind switch
        {
            TopologyNodeKinds.Caller => "🖥 ",
            TopologyNodeKinds.Contract => "⧉ ",
            _ => string.Empty,
        };
        var suffix = node.Standalone == true ? " ⚡" : string.Empty;
        var heart = node.Heartbeats == true ? " 💓" : string.Empty;
        var sb = new StringBuilder(DotEscape(prefix + node.Name + suffix + heart));
        foreach (var handler in node.Handlers)
        {
            sb.Append("\\n").Append(DotEscape(handler.Kind + ": " + handler.Name));
            if (contracts && handler.Signature is { } signature)
            {
                sb.Append(DotEscape("(" + signature + ")"));
            }
        }

        return sb.ToString();
    }

    private static string DotEdgeColorFor(TopologyEdge edge)
    {
        if (edge.HeartbeatIssue == true)
        {
            return "#c62828";
        }

        return edge.Kind switch
        {
            TopologyEdgeKinds.ChildWorkflow => "#1565c0",
            TopologyEdgeKinds.Nexus => "#7b1fa2",
            TopologyEdgeKinds.Signal or TopologyEdgeKinds.Query or TopologyEdgeKinds.Update => "#00838f",
            TopologyEdgeKinds.StandaloneActivity or TopologyEdgeKinds.StartWorkflow => "#00838f",
            _ => "#ef6c00",
        };
    }

    private static string DotEdgeStyleFor(TopologyEdge edge) => edge.Kind switch
    {
        TopologyEdgeKinds.ChildWorkflow => "dashed",
        TopologyEdgeKinds.Nexus => "bold",
        TopologyEdgeKinds.LocalActivity => "dotted",
        _ => "solid",
    };

    private static string? DotEdgeLabel(TopologyEdge edge, bool contracts) => EdgeLabel(edge, contracts);

    private static string ShapeFor(string kind) => kind switch
    {
        TopologyNodeKinds.Activity => "ellipse",
        TopologyNodeKinds.Nexus => "diamond",
        TopologyNodeKinds.TaskQueue => "hexagon",
        _ => "box",
    };

    private static string ColorFor(string kind) => kind switch
    {
        TopologyNodeKinds.Workflow => "#e3f2fd",
        TopologyNodeKinds.Activity => "#fff3e0",
        TopologyNodeKinds.Nexus => "#f3e5f5",
        TopologyNodeKinds.TaskQueue => "#e8f5e9",
        TopologyNodeKinds.Contract => "#ede7f6",
        TopologyNodeKinds.Caller => "#e0f7fa",
        _ => "#fbe9e7",
    };

    private static string DotQuote(string value) => "\"" + value + "\"";

    private static string DotEscape(string text) =>
        text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");

    /// <summary>
    /// Renders the graph as a single self-contained HTML document: the topology
    /// JSON is embedded inline and drawn by a CDN-loaded Mermaid.js, with node
    /// hover tooltips (file/line), click-to-highlight, and a per-kind filter
    /// legend. No build step is required to view it.
    /// </summary>
    public static string ToHtml(TopologyGraph graph, string title, bool contracts = true)
    {
        // Neutralize a "</script>" sequence that could terminate the script block;
        // System.Text.Json already HTML-escapes "<" but be explicit for safety.
        var json = ToJson(graph).Replace("</", "<\\/");
        return HtmlTemplate
            .Replace("__TITLE__", HtmlEscape(title))
            .Replace("__JSON__", json)
            .Replace("__CONTRACTS__", contracts ? "true" : "false");
    }

    private static string HtmlEscape(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");

    private const string HtmlTemplate = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>temporal-sharp map — __TITLE__</title>
        <style>
            :root { color-scheme: light; }
            body { font-family: -apple-system, "Segoe UI", Roboto, Helvetica, Arial, sans-serif; margin: 0; padding: 24px; color: #1c2733; }
            h1 { font-size: 20px; margin: 0 0 4px; }
            #subtitle { color: #5b6b7c; margin: 0 0 16px; font-size: 13px; word-break: break-all; }
            #legend { display: flex; flex-wrap: wrap; gap: 12px; margin-bottom: 16px; }
            .legend-item { display: inline-flex; align-items: center; gap: 6px; font-size: 13px; cursor: pointer; user-select: none; }
            .swatch { width: 12px; height: 12px; border-radius: 3px; display: inline-block; border: 1px solid rgba(0,0,0,0.15); }
            .swatch-workflow { background: #e3f2fd; border-color: #1565c0; }
            .swatch-activity { background: #fff3e0; border-color: #ef6c00; }
            .swatch-nexus { background: #f3e5f5; border-color: #7b1fa2; }
            .swatch-taskQueue { background: #e8f5e9; border-color: #2e7d32; }
            .swatch-unknown { background: #fbe9e7; border-color: #c62828; }
            #diagram { border: 1px solid #e3e9ef; border-radius: 8px; padding: 16px; overflow: auto; background: #ffffff; }
            #status { margin-top: 12px; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 12px; color: #5b6b7c; min-height: 16px; }
            #diagram g.node { cursor: pointer; }
            #diagram g.node.hl > rect, #diagram g.node.hl > polygon, #diagram g.node.hl > path,
            #diagram g.node.hl > ellipse, #diagram g.node.hl > circle {
                stroke-width: 3px; stroke: #d32f2f;
            }
            #diagram g.node.dim { opacity: 0.15; }
        </style>
        </head>
        <body>
        <h1>Workflow topology</h1>
        <p id="subtitle">__TITLE__</p>
        <div id="legend"></div>
        <div id="diagram"></div>
        <div id="status">Click a node to highlight its neighbours; hover for source location.</div>

        <script id="topology-data" type="application/json">__JSON__</script>
        <script src="https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js"></script>
        <script>
        (function () {
            var graph = JSON.parse(document.getElementById('topology-data').textContent);
            var classDefs = {
                workflow: 'fill:#e3f2fd,stroke:#1565c0,color:#000',
                activity: 'fill:#fff3e0,stroke:#ef6c00,color:#000',
                nexus: 'fill:#f3e5f5,stroke:#7b1fa2,color:#000',
                unknown: 'fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5,color:#000',
                contract: 'fill:#ede7f6,stroke:#5e35b1,stroke-dasharray: 3 3,color:#000',
                caller: 'fill:#e0f7fa,stroke:#00838f,color:#000'
            };
            var arrows = { activity: '-->', localActivity: '--o', childWorkflow: '-.->', nexus: '==>' };
            var contracts = __CONTRACTS__;

            var ids = {};
            graph.nodes.forEach(function (n, i) { ids[n.id] = 'n' + i; });

            function esc(s) {
                return String(s == null ? '' : s)
                    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
            }
            function label(n) {
                if (n.kind === 'unknown') {
                    var c = n.unknownKind ? n.unknownKind[0].toUpperCase() + n.unknownKind.slice(1) : 'Unknown';
                    return esc(c + ': ' + n.name);
                }
                var prefix = n.kind === 'caller' ? '\ud83d\udda5 ' : (n.kind === 'contract' ? '\u29c9 ' : '');
                var suffix = n.standalone ? ' \u26a1' : '';
                suffix += n.heartbeats ? ' \ud83d\udc93' : '';
                var l = prefix + esc(n.name) + suffix;
                (n.handlers || []).forEach(function (h) {
                    l += '<br/><i>' + esc(h.kind) + ': ' + esc(h.name);
                    if (contracts && h.signature) { l += '(' + esc(h.signature) + ')'; }
                    l += '</i>';
                });
                return l;
            }

            // Container membership mirrors TopologyLayout.Compute: one box per
            // task queue, an unknown-queue box, and an orphan box; multi-queue
            // nodes stay outside with edges into each of their boxes.
            var callerKinds = { activity: 1, localActivity: 1, childWorkflow: 1, nexus: 1, standaloneActivity: 1 };
            var called = {};
            var queuesByNode = {};
            graph.edges.forEach(function (e) {
                if (callerKinds[e.kind]) { called[e.to] = true; }
                if (e.kind === 'taskQueue') {
                    var l = queuesByNode[e.from] || (queuesByNode[e.from] = []);
                    if (l.indexOf(e.to) < 0) { l.push(e.to); }
                }
            });
            var queueOrder = [], queueMembers = {}, multiLinks = {}, unknownMembers = [], orphans = [];
            graph.nodes.forEach(function (n) {
                if (n.kind === 'taskQueue') { return; }
                var all = queuesByNode[n.id] || [];
                var real = all.filter(function (q) { return q.indexOf('TaskQueue:') === 0; });
                real.forEach(function (q) { if (queueOrder.indexOf(q) < 0) { queueOrder.push(q); } });
                if (real.length === 1) {
                    var m = queueMembers[real[0]] || (queueMembers[real[0]] = []);
                    m.push(n.id);
                } else if (real.length > 1) {
                    multiLinks[n.id] = real;
                } else if (n.kind === 'activity' && !called[n.id]) {
                    orphans.push(n.id);
                } else if (all.length > 0) {
                    unknownMembers.push(n.id);
                }
            });
            queueOrder.sort();
            var slugs = {};
            queueOrder.forEach(function (q, i) { slugs[q] = 'q' + i; });
            var queueNames = {};
            graph.nodes.forEach(function (n) { if (n.kind === 'taskQueue') { queueNames[n.id] = n.name; } });

            var boxed = {};
            Object.keys(queueMembers).forEach(function (q) { queueMembers[q].forEach(function (id) { boxed[id] = 1; }); });
            unknownMembers.forEach(function (id) { boxed[id] = 1; });
            orphans.forEach(function (id) { boxed[id] = 1; });

            function nodeLine(n) {
                return '    ' + ids[n.id] + '["' + label(n) + '"]:::' + (classDefs[n.kind] ? n.kind : 'unknown');
            }

            var lines = ['flowchart LR'];
            Object.keys(classDefs).forEach(function (k) { lines.push('    classDef ' + k + ' ' + classDefs[k] + ';'); });
            lines.push('');
            graph.nodes.forEach(function (n) {
                if (n.kind !== 'taskQueue' && !(n.kind === 'unknown' && n.unknownKind === 'taskQueue') && !boxed[n.id]) { lines.push(nodeLine(n)); }
            });
            queueOrder.forEach(function (q, i) {
                lines.push('');
                lines.push('    subgraph q' + i + '["\ud83d\udce5 ' + esc(queueNames[q] || q) + '"]');
                queueMembers[q].forEach(function (id) {
                    lines.push(nodeLine(graph.nodes.filter(function (n) { return n.id === id; })[0]));
                });
                lines.push('    end');
                lines.push('    style q' + i + ' fill:#e8f5e9,stroke:#2e7d32,color:#000');
            });
            if (unknownMembers.length) {
                lines.push('');
                lines.push('    subgraph uq["\u2753 Unknown task queue"]');
                unknownMembers.forEach(function (id) {
                    lines.push(nodeLine(graph.nodes.filter(function (n) { return n.id === id; })[0]));
                });
                lines.push('    end');
                lines.push('    style uq fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5,color:#000');
            }
            if (orphans.length) {
                lines.push('');
                lines.push('    subgraph orp["\ud83e\udea4 Orphaned activities (no static caller)"]');
                orphans.forEach(function (id) {
                    lines.push(nodeLine(graph.nodes.filter(function (n) { return n.id === id; })[0]));
                });
                lines.push('    end');
                lines.push('    style orp fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5,color:#000');
            }
            lines.push('');
            Object.keys(multiLinks).forEach(function (nodeId) {
                multiLinks[nodeId].forEach(function (q) { lines.push('    ' + ids[nodeId] + ' --> ' + slugs[q]); });
            });

            function callLabel(e) {
                if (e.kind === 'standaloneActivity') { return 'standalone'; }
                if (e.kind !== 'activity' && e.kind !== 'localActivity') { return null; }
                var t = null;
                if (e.order && e.order.length) { t = e.order.map(function (o) { return '#' + o; }).join(', '); }
                if (e.inLoop) { t = (t ? t + ' ' : '') + '\ud83d\udd01'; }
                if (contracts && e.callOptions) { t = (t ? t + ' ' : '') + '[' + e.callOptions + ']'; }
                return t;
            }
            function edgeArrow(e) {
                if ((e.kind === 'activity' || e.kind === 'localActivity') && (e.heartbeats || e.heartbeatIssue)) {
                    return e.heartbeatIssue ? '--x' : '<-->';
                }
                return arrows[e.kind] || '-->';
            }
            var issueIndexes = [], linkIndex = 0;
            graph.edges.forEach(function (e) {
                if (e.kind === 'taskQueue') { return; }
                var line = '    ' + endpointId(e.from, e.to) + ' ' + edgeArrow(e);
                var cl = callLabel(e);
                if (cl) { line += '|"' + cl + '"|'; }
                lines.push(line + ' ' + endpointId(e.to, e.from));
                if (e.heartbeatIssue) { issueIndexes.push(linkIndex); }
                linkIndex++;
            });
            if (issueIndexes.length) {
                lines.push('    linkStyle ' + issueIndexes.join(',') + ' stroke:#c62828,stroke-dasharray: 4 3');
            }

            // The legend lives on the page (outside the schematic).
            var legendText = document.createElement('div');
            legendText.style.cssText = 'font-size:12px;color:#5b6b7c;margin-top:8px;max-width:820px;line-height:1.6';
            legendText.innerHTML = '<b>Arrows &amp; markers</b> &mdash; '
                + '--&gt; call (#1, #3 = order, \ud83d\udd01 = in loop) &middot; '
                + '--o local activity &middot; -.-> child workflow &middot; ==&gt; nexus &middot; '
                + '&lt;--&gt; heartbeats &middot; --x heartbeat issue &middot; '
                + '\u26a1 standalone &middot; \ud83d\udc93 heartbeats &middot; \u2753 unresolved &middot; '
                + '\u2754 no impl &middot; sub-line = Repo/Path:Line';
            document.getElementById('legend').appendChild(legendText);

            var firstMain = graph.nodes.filter(function (n) {
                return n.kind !== 'taskQueue' && !(n.kind === 'unknown' && n.unknownKind === 'taskQueue') && !boxed[n.id];
            })[0];
            var anchor = firstMain ? ids[firstMain.id] : (queueOrder.length ? 'q0' : null);
            if (anchor) {
                if (unknownMembers.length) {
                    lines.push('    ' + anchor + ' ~~~ uq');
                    if (orphans.length) { lines.push('    uq ~~~ orp'); }
                } else if (orphans.length) {
                    lines.push('    ' + anchor + ' ~~~ orp');
                }
            }

            mermaid.initialize({
                startOnLoad: false,
                securityLevel: 'loose',
                theme: 'base',
                themeVariables: { background: '#ffffff', fontFamily: 'Segoe UI, Helvetica, Arial, sans-serif' },
                flowchart: { htmlLabels: true, useMaxWidth: true, nodeSpacing: 35, rankSpacing: 45, wrappingWidth: 220 }
            });
            mermaid.render('topology-svg', lines.join('\n')).then(function (result) {
                var container = document.getElementById('diagram');
                container.innerHTML = result.svg;

                var kindByNode = new Map();
                var allNodes = Array.from(container.querySelectorAll('g.node'));

                allNodes.forEach(function (el) {
                    var m = el.id.match(/^flowchart-([^-]+)-/);
                    var key = m && m[1];
                    var data = key ? graph.nodes[parseInt(key.slice(1), 10)] : null;
                    if (!data) { return; }
                    kindByNode.set(el, data);

                    var t = document.createElementNS('http://www.w3.org/2000/svg', 'title');
                    var tip = data.kind + ' \u00b7 ' + data.name;
                    if (data.file) { tip += '\n' + data.file + (data.line ? ':' + data.line : ''); }
                    if (data.handlers && data.handlers.length) {
                        tip += '\nhandlers: ' + data.handlers.map(function (h) { return h.kind + ':' + h.name; }).join(', ');
                    }
                    t.textContent = tip;
                    el.insertBefore(t, el.firstChild);

                    el.addEventListener('click', function () { highlight(el, data); });
                });

                function highlight(el, data) {
                    allNodes.forEach(function (g) { g.classList.remove('hl', 'dim'); });
                    el.classList.add('hl');
                    var neighbours = new Set([data.id]);
                    graph.edges.forEach(function (e) {
                        if (e.from === data.id) { neighbours.add(e.to); }
                        if (e.to === data.id) { neighbours.add(e.from); }
                    });
                    allNodes.forEach(function (g) {
                        var d = kindByNode.get(g);
                        if (d && d.id !== data.id && !neighbours.has(d.id)) { g.classList.add('dim'); }
                    });
                    document.getElementById('status').textContent =
                        'Selected: ' + data.id + ' (' + data.kind + ')' +
                        (data.file ? ' @ ' + data.file + (data.line ? ':' + data.line : '') : '');
                }

                var legend = document.getElementById('legend');
                var counts = {};
                graph.nodes.forEach(function (n) { counts[n.kind] = (counts[n.kind] || 0) + 1; });
                Object.keys(classDefs).forEach(function (k) {
                    var labelEl = document.createElement('label');
                    labelEl.className = 'legend-item';
                    var cb = document.createElement('input');
                    cb.type = 'checkbox';
                    cb.checked = true;
                    cb.addEventListener('change', function () {
                        allNodes.forEach(function (g) {
                            var d = kindByNode.get(g);
                            if (d && d.kind === k) { g.style.display = cb.checked ? '' : 'none'; }
                        });
                    });
                    var swatch = document.createElement('span');
                    swatch.className = 'swatch swatch-' + k;
                    labelEl.appendChild(cb);
                    labelEl.appendChild(swatch);
                    labelEl.appendChild(document.createTextNode(k + ' (' + (counts[k] || 0) + ')'));
                    legend.appendChild(labelEl);
                });
            });
        })();
        </script>
        </body>
        </html>
        """;
}
