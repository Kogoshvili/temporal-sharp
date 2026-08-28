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

    public static string ToMermaid(TopologyGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TB");
        sb.AppendLine("    classDef workflow fill:#e3f2fd,stroke:#1565c0;");
        sb.AppendLine("    classDef activity fill:#fff3e0,stroke:#ef6c00;");
        sb.AppendLine("    classDef nexus fill:#f3e5f5,stroke:#7b1fa2;");
        sb.AppendLine("    classDef taskQueue fill:#e8f5e9,stroke:#2e7d32;");
        sb.AppendLine("    classDef unknown fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5;");
        sb.AppendLine();

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var node in graph.Nodes)
        {
            ids[node.Id] = "n" + index;
            index++;
        }

        foreach (var node in graph.Nodes)
        {
            sb.Append("    ")
              .Append(ids[node.Id])
              .Append("[\"")
              .Append(BuildLabel(node))
              .Append("\"]:::")
              .Append(ClassFor(node.Kind))
              .AppendLine();
        }

        sb.AppendLine();
        foreach (var edge in graph.Edges)
        {
            sb.Append("    ")
              .Append(ids[edge.From])
              .Append(' ')
              .Append(ArrowFor(edge.Kind))
              .Append(' ')
              .Append(ids[edge.To])
              .AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildLabel(TopologyNode node)
    {
        if (node.Kind == TopologyNodeKinds.Unknown)
        {
            var category = node.UnknownKind is null ? "Unknown" : Capitalize(node.UnknownKind);
            return Escape(category + ": " + node.Name);
        }

        if (node.Handlers.Count == 0)
        {
            return Escape(node.Name);
        }

        var sb = new StringBuilder(Escape(node.Name));
        foreach (var handler in node.Handlers)
        {
            sb.Append("<br/><i>")
              .Append(Escape(handler.Kind))
              .Append(": ")
              .Append(Escape(handler.Name))
              .Append("</i>");
        }

        return sb.ToString();
    }

    private static string ClassFor(string kind) => kind switch
    {
        TopologyNodeKinds.Workflow => "workflow",
        TopologyNodeKinds.Activity => "activity",
        TopologyNodeKinds.Nexus => "nexus",
        TopologyNodeKinds.TaskQueue => "taskQueue",
        _ => "unknown",
    };

    private static string ArrowFor(string kind) => kind switch
    {
        TopologyEdgeKinds.ChildWorkflow => "-.->",
        TopologyEdgeKinds.Nexus => "==>",
        TopologyEdgeKinds.TaskQueue => "-->|task queue|",
        _ => "-->",
    };

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
    public static string ToDot(TopologyGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("digraph temporal_topology {");
        sb.AppendLine("    graph [rankdir=TB, splines=spline];");
        sb.AppendLine("    node [fontname=\"Helvetica\"];");
        sb.AppendLine();

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var node in graph.Nodes)
        {
            ids[node.Id] = "n" + index;
            index++;
        }

        foreach (var node in graph.Nodes)
        {
            var isUnknown = node.Kind == TopologyNodeKinds.Unknown;
            sb.Append("    ")
              .Append(ids[node.Id])
              .Append(" [label=").Append(DotQuote(DotLabel(node)))
              .Append(", shape=").Append(ShapeFor(node.Kind))
              .Append(", style=\"").Append(isUnknown ? "filled,dashed" : "filled").Append('"')
              .Append(", fillcolor=").Append(DotQuote(ColorFor(node.Kind)))
              .AppendLine("];");
        }

        sb.AppendLine();
        foreach (var edge in graph.Edges)
        {
            sb.Append("    ")
              .Append(ids[edge.From])
              .Append(" -> ")
              .Append(ids[edge.To])
              .Append(" [color=").Append(DotQuote(EdgeColorFor(edge.Kind)))
              .Append(", style=").Append(StyleFor(edge.Kind));
            if (edge.Kind == TopologyEdgeKinds.TaskQueue)
            {
                sb.Append(", label=\"task queue\"");
            }

            sb.AppendLine("];");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string DotLabel(TopologyNode node)
    {
        if (node.Kind == TopologyNodeKinds.Unknown)
        {
            var category = node.UnknownKind is null ? "Unknown" : Capitalize(node.UnknownKind);
            return DotEscape(category + ": " + node.Name);
        }

        var sb = new StringBuilder(DotEscape(node.Name));
        foreach (var handler in node.Handlers)
        {
            sb.Append("\\n").Append(DotEscape(handler.Kind + ": " + handler.Name));
        }

        return sb.ToString();
    }

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
        _ => "#fbe9e7",
    };

    private static string EdgeColorFor(string kind) => kind switch
    {
        TopologyEdgeKinds.ChildWorkflow => "#1565c0",
        TopologyEdgeKinds.Nexus => "#7b1fa2",
        TopologyEdgeKinds.TaskQueue => "#2e7d32",
        _ => "#ef6c00",
    };

    private static string StyleFor(string kind) => kind switch
    {
        TopologyEdgeKinds.ChildWorkflow => "dashed",
        TopologyEdgeKinds.Nexus => "bold",
        _ => "solid",
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
    public static string ToHtml(TopologyGraph graph, string title)
    {
        // Neutralize a "</script>" sequence that could terminate the script block;
        // System.Text.Json already HTML-escapes "<" but be explicit for safety.
        var json = ToJson(graph).Replace("</", "<\\/");
        return HtmlTemplate
            .Replace("__TITLE__", HtmlEscape(title))
            .Replace("__JSON__", json);
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
                workflow: 'fill:#e3f2fd,stroke:#1565c0',
                activity: 'fill:#fff3e0,stroke:#ef6c00',
                nexus: 'fill:#f3e5f5,stroke:#7b1fa2',
                taskQueue: 'fill:#e8f5e9,stroke:#2e7d32',
                unknown: 'fill:#fbe9e7,stroke:#c62828,stroke-dasharray: 5 5'
            };
            var arrows = { activity: '-->', localActivity: '-->', childWorkflow: '-.->', nexus: '==>', taskQueue: '-->|task queue|' };

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
                var l = esc(n.name);
                (n.handlers || []).forEach(function (h) { l += '<br/><i>' + esc(h.kind) + ': ' + esc(h.name) + '</i>'; });
                return l;
            }

            var lines = ['flowchart TB'];
            Object.keys(classDefs).forEach(function (k) { lines.push('    classDef ' + k + ' ' + classDefs[k] + ';'); });
            lines.push('');
            graph.nodes.forEach(function (n) {
                lines.push('    ' + ids[n.id] + '["' + label(n) + '"]:::' + (classDefs[n.kind] ? n.kind : 'unknown'));
            });
            lines.push('');
            graph.edges.forEach(function (e) {
                lines.push('    ' + ids[e.from] + ' ' + (arrows[e.kind] || '-->') + ' ' + ids[e.to]);
            });

            mermaid.initialize({ startOnLoad: false, securityLevel: 'loose', flowchart: { htmlLabels: true, useMaxWidth: true } });
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
