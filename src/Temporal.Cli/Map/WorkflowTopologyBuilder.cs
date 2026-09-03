using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using Kogoshvili.Temporal.Analyzers.Analysis;

namespace Kogoshvili.Temporal.Cli.Map;

/// <summary>
/// Walks a loaded solution with semantic analysis and produces a static
/// topology graph: workflow nodes (with their run/signal/query/update handlers
/// as ports), activity nodes, task-queue nodes, and edges for activities, child
/// workflows, nexus operations, and task-queue association. Calls made through
/// the string-named SDK overloads (whose target is not statically resolvable)
/// become <c>Unknown:*</c> boundary nodes so cross-repo targets stay visible.
/// </summary>
internal static class WorkflowTopologyBuilder
{
    private const string UnknownActivity = "activity";
    private const string UnknownChildWorkflow = "childWorkflow";
    private const string UnknownNexusService = "nexusService";
    private const string UnknownNexusOperation = "nexusOperation";
    private const string UnknownTaskQueue = "taskQueue";

    // The Kogoshvili.Temporal.Hosting workflow-side facades, matched by
    // fully-qualified name so the CLI stays free of a dependency on the hosting
    // assembly.
    private const string ActivityOpsType = "Kogoshvili.Temporal.Hosting.ActivityOps";
    private const string ChildWorkflowOpsType = "Kogoshvili.Temporal.Hosting.ChildWorkflowOps";

    // The hosting starter's worker-registration extension class.
    private const string HostingExtensionsType = "Microsoft.Extensions.DependencyInjection.TemporalServiceCollectionExtensions";

    // The Temporalio.Extensions.Hosting facade: AddHostedTemporalWorker(...)
    // plus the AddWorkflow<T>()/Add*Activities<T>() builder extensions.
    private const string SdkHostingExtensionsType = "Microsoft.Extensions.DependencyInjection.TemporalHostingServiceCollectionExtensions";
    private const string SdkWorkerOptionsBuilderExtensionsType = "Temporalio.Extensions.Hosting.TemporalWorkerServiceOptionsBuilderExtensions";

    private static readonly SymbolDisplayFormat FullNameFormat = SymbolDisplayFormat.CSharpErrorMessageFormat;

    public static Task<TopologyGraph> BuildAsync(
        Solution solution,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? inputPaths = null)
        => BuildAsync(new[] { solution }, cancellationToken, inputPaths);

    /// <summary>
    /// Builds a single topology graph from multiple solutions. A shared
    /// <see cref="BuilderState"/> keys nodes by fully-qualified type/method name
    /// (see <c>TypeFullName</c>/<c>MethodFullName</c>), so a
    /// workflow in one solution that invokes a <c>[Workflow]</c>/<c>[Activity]</c>
    /// member defined in another solution (via a shared contract assembly) is
    /// stitched to the same node instead of becoming an <c>Unknown:*</c> boundary.
    /// </summary>
    public static async Task<TopologyGraph> BuildAsync(
        IReadOnlyList<Solution> solutions,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? inputPaths = null)
    {
        var state = new BuilderState();

        // Pass 1: index every workflow and activity declared in source, across
        // all solutions. Repo identity comes from the input path (or the
        // solution's own file path as fallback).
        for (var i = 0; i < solutions.Count; i++)
        {
            var (repo, repoRoot) = RepoIdentity(solutions[i], inputPaths is { Count: > 0 } ? inputPaths[i] : null);
            foreach (var (model, root) in await GetSemanticModelsAsync(solutions[i], cancellationToken).ConfigureAwait(false))
            {
                state.SetCurrentRepo(repo, repoRoot);
                state.CollectDeclarations(root, model);
            }
        }

        // Pass 2: resolve call edges from workflow bodies and worker/client
        // registrations, now that every node is indexed.
        for (var i = 0; i < solutions.Count; i++)
        {
            var (repo, repoRoot) = RepoIdentity(solutions[i], inputPaths is { Count: > 0 } ? inputPaths[i] : null);
            foreach (var (model, root) in await GetSemanticModelsAsync(solutions[i], cancellationToken).ConfigureAwait(false))
            {
                state.SetCurrentRepo(repo, repoRoot);
                state.CollectEdges(root, model);
            }
        }

        return state.Build();
    }

    private static (string? Repo, string? Root) RepoIdentity(Solution solution, string? inputPath)
    {
        var path = inputPath ?? solution.FilePath;
        if (string.IsNullOrEmpty(path))
        {
            return (null, null);
        }

        var fileName = System.IO.Path.GetFileName(path);
        var root = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        return fileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            ? (fileName[..^4], root)
            : (fileName, root);
    }

    private static async Task<IReadOnlyList<(SemanticModel Model, SyntaxNode Root)>> GetSemanticModelsAsync(
        Solution solution,
        CancellationToken cancellationToken)
    {
        var trees = new List<(SemanticModel Model, SyntaxNode Root)>();
        foreach (var projectId in solution.ProjectIds)
        {
            var project = solution.GetProject(projectId);
            if (project is null)
            {
                continue;
            }

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation is null)
            {
                continue;
            }

            foreach (var syntaxTree in compilation.SyntaxTrees)
            {
                var root = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
                trees.Add((compilation.GetSemanticModel(syntaxTree), root));
            }
        }

        return trees;
    }

    private sealed class BuilderState
    {
        private readonly Dictionary<string, TopologyNode> _nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _workflowNodeIds = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _activityNodeIds = new(StringComparer.Ordinal);
        private readonly Dictionary<(string From, string To, string Kind), EdgeAccumulator> _edges = new();
        private readonly Dictionary<Compilation, HashSet<string>> _workflowsByCompilation = new();
        private readonly Dictionary<Compilation, HashSet<string>> _activitiesByCompilation = new();

        // 1-based call ordinals per calling workflow (document order).
        private readonly Dictionary<string, int> _activityCallOrdinals = new(StringComparer.Ordinal);

        // Interface/abstract member → implementing members (FQN-keyed so the
        // index stitches across solutions, matching the graph's node keys).
        private readonly Dictionary<string, List<string>> _workflowImplsByInterface = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _activityImplsByContractMethod = new(StringComparer.Ordinal);

        // Workflow type name (as used by string-named starts) → node id.
        private readonly Dictionary<string, List<string>> _workflowNodeIdsByName = new(StringComparer.Ordinal);

        // Activity members declared on interfaces/abstract bases (attribute on
        // the contract); their nodes are removed when implementations exist.
        private readonly HashSet<string> _contractActivityMethodFqns = new(StringComparer.Ordinal);

        // [Workflow] interface FQNs whose implementations supersede them.
        private readonly HashSet<string> _workflowInterfaceFqns = new(StringComparer.Ordinal);

        // Repo identity of the compilation currently being scanned.
        private (string? Repo, string? Root) _currentRepo;

        // SDK-visible names: activity/workflow names (explicit or derived) and
        // signal/query/update names per workflow node, for string-call resolution.
        private readonly Dictionary<string, List<string>> _activityNodesByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _signalNamesByWorkflow = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _queryNamesByWorkflow = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _updateNamesByWorkflow = new(StringComparer.Ordinal);

        public void SetCurrentRepo(string? repo, string? root) => _currentRepo = (repo, root);

        public void CollectDeclarations(SyntaxNode root, SemanticModel model)
        {
            foreach (var typeDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
                {
                    continue;
                }

                if (WorkflowDetection.IsWorkflowType(typeSymbol))
                {
                    var workflowNodeId = AddWorkflowNode(typeSymbol);
                    TrackCompilationWorkflow(model, workflowNodeId);
                    IndexWorkflowName(WorkflowDisplayName(typeSymbol), workflowNodeId);
                    IndexHandlerNames(typeSymbol, workflowNodeId);
                    foreach (var iface in typeSymbol.AllInterfaces)
                    {
                        if (WorkflowDetection.IsWorkflowType(iface))
                        {
                            AddToIndex(_workflowImplsByInterface, TypeFullName(iface), workflowNodeId);
                            AddToIndex(_workflowNodeIdsByName, WorkflowDisplayName(iface), workflowNodeId);
                        }
                    }
                }

                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is not IMethodSymbol method)
                    {
                        continue;
                    }

                    if (WorkflowDetection.IsActivityMethod(method))
                    {
                        var activityNodeId = AddActivityNode(method);
                        TrackCompilationActivity(model, activityNodeId);
                        IndexActivityContracts(method, activityNodeId);
                        AddToIndex(_activityNodesByName, ActivityDisplayName(method), activityNodeId);
                        if (MethodHeartbeats(method))
                        {
                            MarkHeartbeats(activityNodeId);
                        }

                        if (method.ContainingType?.TypeKind is TypeKind.Interface or TypeKind.Class &&
                            method.IsAbstract)
                        {
                            _contractActivityMethodFqns.Add(MethodFullName(method));
                        }
                    }
                    else
                    {
                        // The [Activity] attribute may live on the interface or
                        // abstract base while the implementation is here — the
                        // impl is the real activity node ("impl node wins").
                        var contract = FindImplementedInterfaceMethod(method) ?? FindOverriddenActivityMethod(method);
                        if (contract is not null && WorkflowDetection.IsActivityMethod(contract))
                        {
                            var implNodeId = AddActivityNode(method);
                            TrackCompilationActivity(model, implNodeId);
                            AddToIndex(_activityImplsByContractMethod, MethodFullName(contract), implNodeId);
                            if (MethodHeartbeats(method))
                            {
                                MarkHeartbeats(implNodeId);
                            }
                        }
                    }
                }
            }
        }

        public void CollectEdges(SyntaxNode root, SemanticModel model)
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol target || target.ContainingType is null)
                {
                    continue;
                }

                var containingType = target.ContainingType;
                if (SdkNames.IsWorkflowType(containingType))
                {
                    HandleWorkflowCommand(invocation, target, model);
                }
                else if (TypeNames.FullName(containingType) == SdkNames.NexusWorkflowClientType)
                {
                    HandleNexusOperation(invocation, target, model);
                }
                else if (TypeNames.FullName(containingType) == ActivityOpsType)
                {
                    HandleActivityOpsCommand(invocation, target, model);
                }
                else if (TypeNames.FullName(containingType) == ChildWorkflowOpsType)
                {
                    HandleChildWorkflowOpsCommand(invocation, target, model);
                }
                else if (IsStandaloneActivityCall(containingType, target))
                {
                    HandleStandaloneActivity(invocation, model);
                }
                else if (TypeNames.FullName(containingType.OriginalDefinition) == SdkNames.ExternalWorkflowHandleType &&
                    target.Name == "SignalAsync")
                {
                    HandleExternalWorkflowSignal(invocation, model);
                }
                else if (IsWorkflowHandleOp(containingType))
                {
                    HandleClientWorkflowOp(invocation, target, model);
                }
                else if (SdkNames.ClientWorkflowStartMethods.Contains(target.Name))
                {
                    HandleClientStart(invocation, model);
                }
            }

            CollectWorkerRegistrations(root, model);
            CollectHostedWorkerRegistrations(root, model);
            CollectSdkHostedRegistrations(root, model);
        }

        public TopologyGraph Build()
        {
            // An activity/workflow declared on a contract (interface/abstract
            // base) is superseded by its implementations — drop the stray
            // declaration node before queue attribution so it never picks up
            // unknown-queue edges.
            foreach (var fqn in _contractActivityMethodFqns)
            {
                var nodeId = "Activity:" + fqn;
                if (_activityImplsByContractMethod.ContainsKey(fqn))
                {
                    if (!_edges.Keys.Any(k => k.From == nodeId || k.To == nodeId))
                    {
                        _nodes.Remove(nodeId);
                    }
                }
                else if (_nodes.TryGetValue(nodeId, out var contractNode))
                {
                    // Called via the contract but no implementation is loaded.
                    _nodes[nodeId] = contractNode with { Unresolved = true };
                }
            }

            foreach (var fqn in _workflowInterfaceFqns)
            {
                var nodeId = "Workflow:" + fqn;
                if (_workflowImplsByInterface.ContainsKey(fqn))
                {
                    if (!_edges.Keys.Any(k => k.From == nodeId || k.To == nodeId))
                    {
                        _nodes.Remove(nodeId);
                    }
                }
                else if (_nodes.TryGetValue(nodeId, out var interfaceNode))
                {
                    _nodes[nodeId] = interfaceNode with { Unresolved = true };
                }
            }

            // Activities inherit the caller workflow's queue when they have no
            // explicit evidence (registration or call-site routing): the SDK
            // routes them to the calling workflow's task queue.
            var taskQueueTargets = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var (key, _) in _edges)
            {
                if (key.Kind == TopologyEdgeKinds.TaskQueue)
                {
                    if (!taskQueueTargets.TryGetValue(key.From, out var targets))
                    {
                        taskQueueTargets[key.From] = targets = [];
                    }

                    if (key.To.StartsWith("TaskQueue:", StringComparison.Ordinal))
                    {
                        targets.Add(key.To);
                    }
                }
            }

            foreach (var node in _nodes.Values.ToArray())
            {
                if (node.Kind == TopologyNodeKinds.Activity && !taskQueueTargets.ContainsKey(node.Id))
                {
                    var callers = _edges.Keys
                        .Where(k => k.To == node.Id &&
                                    (k.Kind == TopologyEdgeKinds.Activity || k.Kind == TopologyEdgeKinds.LocalActivity))
                        .Select(k => k.From)
                        .Distinct()
                        .ToList();
                    var inherited = callers
                        .SelectMany(c => taskQueueTargets.TryGetValue(c, out var targets) ? targets : Enumerable.Empty<string>())
                        .Distinct()
                        .ToList();
                    foreach (var queue in inherited)
                    {
                        AddEdge(node.Id, queue, TopologyEdgeKinds.TaskQueue);
                        if (!taskQueueTargets.TryGetValue(node.Id, out var targets))
                        {
                            taskQueueTargets[node.Id] = targets = [];
                        }

                        targets.Add(queue);
                    }
                }
            }

            // Every workflow and activity runs on some task queue; anything we
            // still could not statically resolve gets an edge to a shared
            // Unknown:TaskQueue boundary node so it still lands in an
            // "unknown queue" container.
            var unknownQueueNodeId = AddUnknownNode(UnknownTaskQueue, "unknown");
            foreach (var node in _nodes.Values.ToArray())
            {
                if (node.Kind is TopologyNodeKinds.Workflow or TopologyNodeKinds.Activity &&
                    !_edges.Keys.Any(e => e.From == node.Id && e.Kind == TopologyEdgeKinds.TaskQueue))
                {
                    AddEdge(node.Id, unknownQueueNodeId, TopologyEdgeKinds.TaskQueue);
                }
            }

            var nodes = _nodes.Values
                .Select(n => n with
                {
                    Handlers = n.Handlers
                        .OrderBy(h => h.Kind, StringComparer.Ordinal)
                        .ThenBy(h => h.Name, StringComparer.Ordinal)
                        .ToArray(),
                })
                .OrderBy(n => n.Id, StringComparer.Ordinal)
                .ToArray();

            var edges = _edges
                .Select(kv => new TopologyEdge(
                    kv.Key.From,
                    kv.Key.To,
                    kv.Key.Kind,
                    null,
                    kv.Value.Orders.Count > 0 ? [.. kv.Value.Orders] : null,
                    kv.Value.InLoop ? true : null,
                    kv.Value.CallOptions,
                    kv.Value.Heartbeats ? true : null,
                    kv.Value.HeartbeatIssue ? true : null))
                .OrderBy(e => e.From, StringComparer.Ordinal)
                .ThenBy(e => e.To, StringComparer.Ordinal)
                .ThenBy(e => e.Kind, StringComparer.Ordinal)
                .ToArray();

            return new TopologyGraph(nodes, edges);
        }

        private void HandleWorkflowCommand(
            InvocationExpressionSyntax invocation,
            IMethodSymbol target,
            SemanticModel model)
        {
            var workflowId = GetEnclosingWorkflowNodeId(invocation, model);
            if (workflowId is null)
            {
                return;
            }

            if (target.Name == "ExecuteActivityAsync")
            {
                AddActivityEdge(workflowId, invocation, model, TopologyEdgeKinds.Activity);
            }
            else if (target.Name == "ExecuteLocalActivityAsync")
            {
                AddActivityEdge(workflowId, invocation, model, TopologyEdgeKinds.LocalActivity);
            }
            else if (SdkNames.ChildWorkflowStartMethods.Contains(target.Name))
            {
                AddChildWorkflowEdge(workflowId, invocation, model);
            }
            else if (target.Name == "CreateNexusWorkflowClient")
            {
                AddNexusServiceEdge(workflowId, invocation, model);
            }
        }

        private void HandleActivityOpsCommand(
            InvocationExpressionSyntax invocation,
            IMethodSymbol target,
            SemanticModel model)
        {
            var workflowId = GetEnclosingWorkflowNodeId(invocation, model);
            if (workflowId is null)
            {
                return;
            }

            if (target.Name == "ExecuteAsync")
            {
                AddActivityEdge(workflowId, invocation, model, TopologyEdgeKinds.Activity);
            }
            else if (target.Name == "ExecuteLocalAsync")
            {
                AddActivityEdge(workflowId, invocation, model, TopologyEdgeKinds.LocalActivity);
            }
        }

        private void HandleChildWorkflowOpsCommand(
            InvocationExpressionSyntax invocation,
            IMethodSymbol target,
            SemanticModel model)
        {
            var workflowId = GetEnclosingWorkflowNodeId(invocation, model);
            if (workflowId is null)
            {
                return;
            }

            // Lambda overloads resolve the child workflow from the lambda target.
            if (TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                var containingType = targetMethod!.ContainingType;
                if (containingType is not null && WorkflowDetection.IsWorkflowType(containingType))
                {
                    AddEdge(workflowId, AddWorkflowNode(containingType), TopologyEdgeKinds.ChildWorkflow);
                }
                else
                {
                    AddEdge(workflowId, AddUnknownNode(UnknownChildWorkflow, FriendlyName(targetMethod)), TopologyEdgeKinds.ChildWorkflow);
                }

                return;
            }

            // Non-lambda overloads (single-parameter object, no-argument) carry the
            // child workflow type as a generic type argument.
            foreach (var typeArgument in target.TypeArguments)
            {
                if (typeArgument is INamedTypeSymbol named && WorkflowDetection.IsWorkflowType(named))
                {
                    AddEdge(workflowId, AddWorkflowNode(named), TopologyEdgeKinds.ChildWorkflow);
                    return;
                }
            }

            // String-named overloads take the workflow name as the first argument.
            if (TryResolveStringTarget(invocation, model, out var name))
            {
                AddEdge(workflowId, AddUnknownNode(UnknownChildWorkflow, name), TopologyEdgeKinds.ChildWorkflow);
            }
        }

        private void AddActivityEdge(string workflowId, InvocationExpressionSyntax invocation, SemanticModel model, string kind)
        {
            var ordinal = NextActivityOrdinal(workflowId);
            var inLoop = IsInsideLoop(invocation);

            // Call-site routing: ActivityOptions { TaskQueue = ... } sends the
            // activity to a queue other than the workflow's own.
            string? routedQueueId = TryGetClientStartTaskQueue(invocation, model, out var routedQueue)
                ? AddTaskQueueNode(routedQueue)
                : null;
            var (callOptions, heartbeatTimeout) = TryGetCallOptions(invocation, model);

            if (TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                var (targetId, heartbeats) = ResolveActivityTarget(targetMethod!);
                AddCallEdge(workflowId, targetId, kind, ordinal, inLoop, callOptions, heartbeats,
                    heartbeatTimeout && !heartbeats ? true : null);
                if (routedQueueId is not null)
                {
                    AddEdge(targetId, routedQueueId, TopologyEdgeKinds.TaskQueue);
                }

                return;
            }

            if (TryResolveStringTarget(invocation, model, out var name))
            {
                // SDK name rules: resolve against the activity-name index
                // ([Activity("Name")] or the method name verbatim).
                string targetId;
                bool heartbeats;
                if (_activityNodesByName.TryGetValue(name, out var nameHits) && nameHits.Count == 1)
                {
                    targetId = nameHits[0];
                    heartbeats = _nodes[targetId].Heartbeats == true;
                }
                else
                {
                    targetId = AddUnknownNode(UnknownActivity, name);
                    heartbeats = false;
                }

                AddCallEdge(workflowId, targetId, kind, ordinal, inLoop, callOptions, heartbeats,
                    heartbeatTimeout && !heartbeats ? true : null);
                if (routedQueueId is not null)
                {
                    AddEdge(targetId, routedQueueId, TopologyEdgeKinds.TaskQueue);
                }
            }
        }

        /// <summary>
        /// Resolves a typed-lambda activity target to a node, honoring the
        /// contract implementation index: interface/abstract members resolve to
        /// their unique implementation, to a contract boundary node when
        /// ambiguous, or to themselves when no implementation is loaded.
        /// </summary>
        private (string NodeId, bool Heartbeats) ResolveActivityTarget(IMethodSymbol method)
        {
            var isContractMember = method.ContainingType?.TypeKind == TypeKind.Interface || method.IsAbstract;
            if (!isContractMember && WorkflowDetection.IsActivityMethod(method))
            {
                var nodeId = AddActivityNode(method);
                return (nodeId, _nodes[nodeId].Heartbeats == true);
            }

            if (_activityImplsByContractMethod.TryGetValue(MethodFullName(method), out var impls))
            {
                if (impls.Count == 1)
                {
                    return (impls[0], _nodes[impls[0]].Heartbeats == true);
                }

                return (AddContractNode(MethodFullName(method), FriendlyName(method)), false);
            }

            if (WorkflowDetection.IsActivityMethod(method))
            {
                // The activity is declared on the contract itself and no
                // implementation is loaded; keep it as a first-class node.
                var nodeId = AddActivityNode(method);
                return (nodeId, _nodes[nodeId].Heartbeats == true);
            }

            return (AddUnknownNode(UnknownActivity, FriendlyName(method)), false);
        }

        /// <summary>
        /// Resolves a workflow type (possibly a [Workflow] interface) to a
        /// node: the unique implementation wins; ambiguous interfaces become
        /// contract boundary nodes; otherwise the type itself is used.
        /// </summary>
        private string ResolveWorkflowNodeId(INamedTypeSymbol type)
        {
            if (type.TypeKind == TypeKind.Interface &&
                _workflowImplsByInterface.TryGetValue(TypeFullName(type), out var impls))
            {
                if (impls.Count == 1)
                {
                    return impls[0];
                }

                return AddContractNode(TypeFullName(type), type.Name);
            }

            return AddWorkflowNode(type);
        }

        private void AddChildWorkflowEdge(string workflowId, InvocationExpressionSyntax invocation, SemanticModel model)
        {
            if (TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                var containingType = targetMethod!.ContainingType;
                if (containingType is not null && WorkflowDetection.IsWorkflowType(containingType))
                {
                    AddEdge(workflowId, ResolveWorkflowNodeId(containingType), TopologyEdgeKinds.ChildWorkflow);
                }
                else
                {
                    AddEdge(workflowId, AddUnknownNode(UnknownChildWorkflow, FriendlyName(targetMethod)), TopologyEdgeKinds.ChildWorkflow);
                }

                return;
            }

            if (TryResolveStringTarget(invocation, model, out var name))
            {
                var targetId = ResolveWorkflowIdByName(name) ?? AddUnknownNode(UnknownChildWorkflow, name);
                AddEdge(workflowId, targetId, TopologyEdgeKinds.ChildWorkflow);
            }
        }

        private void AddNexusServiceEdge(string workflowId, InvocationExpressionSyntax invocation, SemanticModel model)
        {
            if (TryResolveStringTarget(invocation, model, out var name))
            {
                AddEdge(workflowId, AddUnknownNode(UnknownNexusService, name), TopologyEdgeKinds.Nexus);
            }
        }

        private void HandleNexusOperation(InvocationExpressionSyntax invocation, IMethodSymbol target, SemanticModel model)
        {
            if (!SdkNames.NexusOperationMethods.Contains(target.Name))
            {
                return;
            }

            var workflowId = GetEnclosingWorkflowNodeId(invocation, model);
            if (workflowId is null)
            {
                return;
            }

            if (TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                AddEdge(workflowId, AddNexusNode(targetMethod!), TopologyEdgeKinds.Nexus);
            }
            else if (TryResolveStringTarget(invocation, model, out var name))
            {
                AddEdge(workflowId, AddUnknownNode(UnknownNexusOperation, name), TopologyEdgeKinds.Nexus);
            }
        }

        private void HandleClientStart(InvocationExpressionSyntax invocation, SemanticModel model)
        {
            var callerId = GetEnclosingCallerNodeId(invocation, model);
            if (TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                var containingType = targetMethod!.ContainingType;
                if (containingType is null || !WorkflowDetection.IsWorkflowType(containingType))
                {
                    return;
                }

                var workflowId = ResolveWorkflowNodeId(containingType);
                if (callerId is not null)
                {
                    AddEdge(callerId, workflowId, TopologyEdgeKinds.StartWorkflow);
                }

                if (TryGetClientStartTaskQueue(invocation, model, out var taskQueue))
                {
                    AddEdge(workflowId, AddTaskQueueNode(taskQueue), TopologyEdgeKinds.TaskQueue);
                }
            }
            else if (TryResolveStringTarget(invocation, model, out var workflowName))
            {
                if (callerId is null)
                {
                    return;
                }

                // String-named start: resolve against known workflow type names.
                var workflowId = ResolveWorkflowIdByName(workflowName) ?? AddUnknownNode("workflow", workflowName);
                AddEdge(callerId, workflowId, TopologyEdgeKinds.StartWorkflow);
                if (TryGetClientStartTaskQueue(invocation, model, out var taskQueue))
                {
                    AddEdge(workflowId, AddTaskQueueNode(taskQueue), TopologyEdgeKinds.TaskQueue);
                }
            }
        }

        private static bool IsStandaloneActivityCall(INamedTypeSymbol containingType, IMethodSymbol target) =>
            target.Name is "StartActivityAsync" or "ExecuteActivityAsync" &&
            TypeNames.FullName(containingType) is
                "Temporalio.Client.ITemporalClient" or
                "Temporalio.Client.TemporalClient" or
                "Temporalio.Client.ITemporalClientExtensions";

        private void HandleStandaloneActivity(InvocationExpressionSyntax invocation, SemanticModel model)
        {
            var callerId = GetEnclosingCallerNodeId(invocation, model);
            if (callerId is null)
            {
                return;
            }

            var (callOptions, _) = TryGetCallOptions(invocation, model);
            string? routedQueueId = TryGetClientStartTaskQueue(invocation, model, out var routedQueue)
                ? AddTaskQueueNode(routedQueue)
                : null;

            if (TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                var (activityId, _) = ResolveActivityTarget(targetMethod!);
                if (_nodes[activityId].Kind == TopologyNodeKinds.Activity)
                {
                    _nodes[activityId] = _nodes[activityId] with { Standalone = true };
                }

                AddEdge(callerId, activityId, TopologyEdgeKinds.StandaloneActivity, callOptions);
                if (routedQueueId is not null)
                {
                    AddEdge(activityId, routedQueueId, TopologyEdgeKinds.TaskQueue);
                }

                return;
            }

            if (TryResolveStringTarget(invocation, model, out var name))
            {
                var activityId = AddUnknownNode(UnknownActivity, name);
                AddEdge(callerId, activityId, TopologyEdgeKinds.StandaloneActivity, callOptions);
                if (routedQueueId is not null)
                {
                    AddEdge(activityId, routedQueueId, TopologyEdgeKinds.TaskQueue);
                }
            }
        }

        private static bool IsWorkflowHandleOp(INamedTypeSymbol containingType) =>
            TypeNames.FullName(containingType.OriginalDefinition).StartsWith("Temporalio.Client.WorkflowHandle", StringComparison.Ordinal);

        private void HandleClientWorkflowOp(InvocationExpressionSyntax invocation, IMethodSymbol target, SemanticModel model)
        {
            var callerId = GetEnclosingCallerNodeId(invocation, model);
            if (callerId is null)
            {
                return;
            }

            var edgeKind = target.Name switch
            {
                "SignalAsync" => TopologyEdgeKinds.Signal,
                "QueryAsync" => TopologyEdgeKinds.Query,
                _ => TopologyEdgeKinds.Update,
            };

            if (TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                var containingType = targetMethod!.ContainingType;
                if (containingType is null || !WorkflowDetection.IsWorkflowType(containingType))
                {
                    return;
                }

                AddEdge(callerId, ResolveWorkflowNodeId(containingType), edgeKind);
                return;
            }

            // String overload: the target workflow comes from the handle's
            // generic argument when present, otherwise from the handler-name
            // index (SDK naming: attribute name or Async-trimmed method name).
            if (TryResolveStringTarget(invocation, model, out var name))
            {
                var handleType = ResolveWorkflowTypeFromHandle(invocation, model);
                if (handleType is not null)
                {
                    AddEdge(callerId, ResolveWorkflowNodeId(handleType), edgeKind);
                    return;
                }

                var index = edgeKind switch
                {
                    TopologyEdgeKinds.Signal => _signalNamesByWorkflow,
                    TopologyEdgeKinds.Query => _queryNamesByWorkflow,
                    _ => _updateNamesByWorkflow,
                };
                if (index.TryGetValue(name, out var hits) && hits.Count == 1)
                {
                    AddEdge(callerId, hits[0], edgeKind);
                    return;
                }

                AddEdge(callerId, AddUnknownNode("workflow", name), edgeKind);
            }
        }

        /// <summary>
        /// Finds the workflow type a handle refers to: scans the receiver chain
        /// (or a local variable's initializer) for
        /// <c>GetWorkflowHandle&lt;TWorkflow&gt;</c>.
        /// </summary>
        private static INamedTypeSymbol? ResolveWorkflowTypeFromHandle(ExpressionSyntax receiver, SemanticModel model)
        {
            var scopes = new List<SyntaxNode> { receiver };
            if (model.GetSymbolInfo(receiver).Symbol is ILocalSymbol local)
            {
                foreach (var reference in local.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: var init })
                    {
                        scopes.Add(init);
                    }
                }
            }

            foreach (var scope in scopes)
            {
                foreach (var invocation in scope.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
                        method.Name == "GetWorkflowHandle" &&
                        method.TypeArguments.Length == 1 &&
                        method.TypeArguments[0] is INamedTypeSymbol workflowType)
                    {
                        return workflowType;
                    }
                }
            }

            return null;
        }

        private void HandleExternalWorkflowSignal(InvocationExpressionSyntax invocation, SemanticModel model)
        {
            var workflowId = GetEnclosingWorkflowNodeId(invocation, model);
            if (workflowId is null)
            {
                return;
            }

            if (!TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                return;
            }

            var containingType = targetMethod!.ContainingType;
            if (containingType is not null && WorkflowDetection.IsWorkflowType(containingType))
            {
                AddEdge(workflowId, ResolveWorkflowNodeId(containingType), TopologyEdgeKinds.Signal);
            }
        }

        private string? GetEnclosingCallerNodeId(InvocationExpressionSyntax invocation, SemanticModel model)
        {
            var enclosingMethod = SymbolUtilities.GetEnclosingRegularMethod(model.GetEnclosingSymbol(invocation.SpanStart));
            var containingType = enclosingMethod?.ContainingType;
            if (containingType is null || WorkflowDetection.IsWorkflowType(containingType))
            {
                return null;
            }

            return AddCallerNode(containingType);
        }

        private string AddCallerNode(INamedTypeSymbol type)
        {
            var fullName = TypeFullName(type);
            var id = "Caller:" + fullName;
            if (_nodes.ContainsKey(id))
            {
                return id;
            }

            var (file, line) = GetLocation(type);
            var (repo, path) = NodeLocation(file);
            _nodes[id] = new TopologyNode(
                id,
                TopologyNodeKinds.Caller,
                type.Name,
                file,
                line,
                null,
                Array.Empty<TopologyHandler>())
            {
                Repo = repo,
                Path = path,
            };
            return id;
        }

        private string AddContractNode(string contractFqn, string name)
        {
            var id = "Contract:" + contractFqn;
            if (_nodes.ContainsKey(id))
            {
                return id;
            }

            _nodes[id] = new TopologyNode(
                id,
                TopologyNodeKinds.Contract,
                name,
                null,
                null,
                null,
                Array.Empty<TopologyHandler>());
            return id;
        }

        /// <summary>
        /// Summarizes call-site options for the contracts view: timeout values
        /// and retry policy. The second return value reports whether a
        /// heartbeat timeout is configured (a heartbeat-mismatch signal).
        /// </summary>
        private static (string? Summary, bool HeartbeatTimeout) TryGetCallOptions(
            InvocationExpressionSyntax invocation, SemanticModel model)
        {
            var parts = new List<string>();
            var heartbeatTimeout = false;
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (argument.Expression is not BaseObjectCreationExpressionSyntax creation ||
                    creation.Initializer is not { } initializer)
                {
                    continue;
                }

                foreach (var item in initializer.Expressions)
                {
                    if (item is not AssignmentExpressionSyntax assignment ||
                        assignment.Left is not IdentifierNameSyntax { Identifier.ValueText: var propertyName })
                    {
                        continue;
                    }

                    switch (propertyName)
                    {
                        case "StartToCloseTimeout":
                            parts.Add("StartToClose=" + TryGetTimeSpanSummary(assignment.Right, model));
                            break;
                        case "ScheduleToCloseTimeout":
                            parts.Add("ScheduleToClose=" + TryGetTimeSpanSummary(assignment.Right, model));
                            break;
                        case "HeartbeatTimeout":
                            heartbeatTimeout = true;
                            parts.Add("HeartbeatTimeout=" + TryGetTimeSpanSummary(assignment.Right, model));
                            break;
                        case "RetryPolicy" when assignment.Right is BaseObjectCreationExpressionSyntax retryCreation &&
                                                TryGetRetrySummary(retryCreation, model, out var retrySummary):
                            parts.Add(retrySummary);
                            break;
                    }
                }
            }

            return (parts.Count > 0 ? string.Join("; ", parts) : null, heartbeatTimeout);
        }

        private static bool TryGetRetrySummary(BaseObjectCreationExpressionSyntax creation, SemanticModel model, out string summary)
        {
            var type = model.GetTypeInfo(creation).Type;
            if (type is null || !TypeNames.FullName(type).EndsWith("RetryPolicy", StringComparison.Ordinal))
            {
                summary = string.Empty;
                return false;
            }

            if (creation.Initializer is { } initializer)
            {
                foreach (var item in initializer.Expressions)
                {
                    if (item is AssignmentExpressionSyntax
                        {
                            Left: IdentifierNameSyntax { Identifier.ValueText: "MaximumAttempts" }
                        } assignment &&
                        model.GetConstantValue(assignment.Right).Value is int attempts)
                    {
                        summary = "Retry:max" + attempts;
                        return true;
                    }
                }
            }

            summary = "Retry";
            return true;
        }

        private static string TryGetTimeSpanSummary(ExpressionSyntax expression, SemanticModel model)
        {
            if (expression is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var factor = memberAccess.Name.Identifier.ValueText switch
                {
                    "FromSeconds" => "s",
                    "FromMinutes" => "m",
                    "FromHours" => "h",
                    "FromMilliseconds" => "ms",
                    _ => null,
                };
                if (factor is not null &&
                    invocation.ArgumentList.Arguments.Count == 1 &&
                    model.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression).Value is { } rawValue)
                {
                    var seconds = System.Convert.ToDouble(rawValue, System.Globalization.CultureInfo.InvariantCulture);
                    return FormattableString.Invariant($"{seconds:0.##}{factor}");
                }
            }

            return "?";
        }

        private void CollectWorkerRegistrations(SyntaxNode root, SemanticModel model)
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                var memberName = memberAccess.Name.Identifier.ValueText;
                if (memberName is not ("AddWorkflow" or "AddActivity" or "AddAllActivities"))
                {
                    continue;
                }

                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                    method.ContainingType is null ||
                    TypeNames.FullName(method.ContainingType) != SdkNames.TemporalWorkerOptionsType)
                {
                    continue;
                }

                if (ResolveWorkerTaskQueue(memberAccess.Expression, model) is not { } taskQueue)
                {
                    continue;
                }

                var queueNodeId = AddTaskQueueNode(taskQueue);
                switch (memberName)
                {
                    case "AddWorkflow":
                        if (method.TypeArguments.Length == 1 &&
                            method.TypeArguments[0] is INamedTypeSymbol workflowType &&
                            WorkflowDetection.IsWorkflowType(workflowType))
                        {
                            AddEdge(AddWorkflowNode(workflowType), queueNodeId, TopologyEdgeKinds.TaskQueue);
                        }

                        break;

                    case "AddActivity":
                        foreach (var activity in ResolveRegisteredActivities(invocation, method, model))
                        {
                            AddEdge(activity, queueNodeId, TopologyEdgeKinds.TaskQueue);
                        }

                        break;

                    case "AddAllActivities":
                        foreach (var type in ResolveRegisteredActivityTypes(invocation, method, model))
                        {
                            foreach (var activityId in EnumerateActivityNodeIds(type))
                            {
                                AddEdge(activityId, queueNodeId, TopologyEdgeKinds.TaskQueue);
                            }
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Resolves the activity members registered via
        /// <c>AddActivity</c> — either the typed-lambda target or a method-group
        /// argument.
        /// </summary>
        private IEnumerable<string> ResolveRegisteredActivities(
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            SemanticModel model)
        {
            if (method.TypeArguments.Length == 1 && method.TypeArguments[0] is INamedTypeSymbol genericTarget)
            {
                return EnumerateActivityNodeIds(genericTarget);
            }

            if (TryResolveTypedLambdaTarget(model, invocation, out var lambdaTarget) &&
                WorkflowDetection.IsActivityMethod(lambdaTarget!))
            {
                return [AddActivityNode(lambdaTarget!)];
            }

            var methodGroups = new List<string>();
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (model.GetSymbolInfo(argument.Expression).Symbol is IMethodSymbol candidate &&
                    WorkflowDetection.IsActivityMethod(candidate))
                {
                    methodGroups.Add(AddActivityNode(candidate));
                }
            }

            return methodGroups;
        }

        /// <summary>
        /// Resolves the activity types registered via <c>AddAllActivities</c> —
        /// either the generic type argument or the <c>typeof(...)</c> argument.
        /// </summary>
        private IEnumerable<INamedTypeSymbol> ResolveRegisteredActivityTypes(
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            SemanticModel model)
        {
            if (method.TypeArguments.Length == 1 && method.TypeArguments[0] is INamedTypeSymbol genericType)
            {
                return [genericType];
            }

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (argument.Expression is TypeOfExpressionSyntax typeOf &&
                    model.GetTypeInfo(typeOf.Type).Type is INamedTypeSymbol named)
                {
                    return [named];
                }
            }

            return [];
        }

        private IEnumerable<string> EnumerateActivityNodeIds(INamedTypeSymbol type)
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IMethodSymbol method && WorkflowDetection.IsActivityMethod(method))
                {
                    yield return AddActivityNode(method);
                }
            }
        }

        private void CollectHostedWorkerRegistrations(SyntaxNode root, SemanticModel model)
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                var name = memberAccess.Name.Identifier.ValueText;
                if (name != "AddWorkflow" && name != "AddDiscoveredTypes")
                {
                    continue;
                }

                if (ResolveHostedWorkerTaskQueue(memberAccess.Expression, model) is not { } taskQueue)
                {
                    continue;
                }

                if (name == "AddWorkflow")
                {
                    if (model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
                        method.TypeArguments.Length == 1 &&
                        method.TypeArguments[0] is INamedTypeSymbol workflowType &&
                        WorkflowDetection.IsWorkflowType(workflowType))
                    {
                        AddEdge(AddWorkflowNode(workflowType), AddTaskQueueNode(taskQueue), TopologyEdgeKinds.TaskQueue);
                    }

                    continue;
                }

                // AddDiscoveredTypes scans the assembly for [Workflow]/[Activity]
                // types; associate the workflows and activities declared in the
                // same compilation (best-effort proxy for the scanned assembly).
                foreach (var workflowId in GetWorkflowsInCompilation(model))
                {
                    AddEdge(workflowId, AddTaskQueueNode(taskQueue), TopologyEdgeKinds.TaskQueue);
                }

                foreach (var activityId in GetActivitiesInCompilation(model))
                {
                    AddEdge(activityId, AddTaskQueueNode(taskQueue), TopologyEdgeKinds.TaskQueue);
                }
            }
        }

        private static string? ResolveHostedWorkerTaskQueue(ExpressionSyntax receiver, SemanticModel model)
        {
            foreach (var invocation in receiver.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method &&
                    method.Name == "AddTemporalWorker" &&
                    TypeNames.FullName(method.ContainingType) == HostingExtensionsType &&
                    invocation.ArgumentList.Arguments.Count > 0 &&
                    TryGetStringConstant(invocation.ArgumentList.Arguments[0].Expression, model, out var taskQueue))
                {
                    return taskQueue;
                }
            }

            return null;
        }

        /// <summary>
        /// Detects Temporalio.Extensions.Hosting registrations:
        /// <c>services.AddHostedTemporalWorker(..., "queue")...</c> followed by
        /// <c>AddWorkflow&lt;T&gt;()</c> / <c>AddScopedActivities&lt;T&gt;()</c>
        /// (and singleton/transient variants) chained or split across locals.
        /// </summary>
        private void CollectSdkHostedRegistrations(SyntaxNode root, SemanticModel model)
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                {
                    continue;
                }

                var memberName = memberAccess.Name.Identifier.ValueText;
                if (memberName is not ("AddWorkflow" or "AddScopedActivities" or "AddSingletonActivities" or "AddTransientActivities"))
                {
                    continue;
                }

                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                    method.ContainingType is null ||
                    TypeNames.FullName(method.ContainingType) != SdkWorkerOptionsBuilderExtensionsType)
                {
                    continue;
                }

                if (ResolveSdkHostedTaskQueue(memberAccess.Expression, model) is not { } taskQueue)
                {
                    continue;
                }

                var queueNodeId = AddTaskQueueNode(taskQueue);
                if (memberName == "AddWorkflow")
                {
                    if (ResolveRegisteredType(invocation, method, model) is { } workflowType &&
                        WorkflowDetection.IsWorkflowType(workflowType))
                    {
                        AddEdge(AddWorkflowNode(workflowType), queueNodeId, TopologyEdgeKinds.TaskQueue);
                    }

                    continue;
                }

                if (ResolveRegisteredType(invocation, method, model) is { } activitiesType)
                {
                    foreach (var activityId in EnumerateActivityNodeIds(activitiesType))
                    {
                        AddEdge(activityId, queueNodeId, TopologyEdgeKinds.TaskQueue);
                    }
                }
            }
        }

        private static INamedTypeSymbol? ResolveRegisteredType(
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            SemanticModel model)
        {
            if (method.TypeArguments.Length == 1 && method.TypeArguments[0] is INamedTypeSymbol generic)
            {
                return generic;
            }

            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (argument.Expression is TypeOfExpressionSyntax typeOf &&
                    model.GetTypeInfo(typeOf.Type).Type is INamedTypeSymbol named)
                {
                    return named;
                }
            }

            return null;
        }

        private static string? ResolveSdkHostedTaskQueue(ExpressionSyntax receiver, SemanticModel model)
        {
            var scopes = new List<SyntaxNode> { receiver };
            if (model.GetSymbolInfo(receiver).Symbol is ILocalSymbol local)
            {
                foreach (var reference in local.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax() is VariableDeclaratorSyntax { Initializer.Value: var init })
                    {
                        scopes.Add(init);
                    }
                }
            }

            foreach (var scope in scopes)
            {
                foreach (var invocation in scope.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                        method.ContainingType is null ||
                        method.Name != "AddHostedTemporalWorker" ||
                        TypeNames.FullName(method.ContainingType) != SdkHostingExtensionsType)
                    {
                        continue;
                    }

                    if (TryGetNamedQueueArgument(invocation.ArgumentList.Arguments, method.Parameters, model, out var queue) ||
                        TryGetStringConstantFromTaskQueueAssignment(invocation, model, out queue))
                    {
                        return queue;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Covers overloads where the queue arrives via an options object or
        /// configure lambda: any <c>TaskQueue = "..."</c> assignment or
        /// <c>TemporalWorkerOptions("...")</c> creation inside the call.
        /// </summary>
        private static bool TryGetStringConstantFromTaskQueueAssignment(
            InvocationExpressionSyntax invocation,
            SemanticModel model,
            out string value)
        {
            foreach (var creation in invocation.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                var type = model.GetTypeInfo(creation).Type;
                if (type is not null && TypeNames.FullName(type) == SdkNames.TemporalWorkerOptionsType &&
                    TryGetWorkerOptionsTaskQueue(creation, model, out value))
                {
                    return true;
                }
            }

            foreach (var assignment in invocation.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                         .Where(a => a.Left is IdentifierNameSyntax { Identifier.ValueText: "TaskQueue" } ||
                                     a.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "TaskQueue" }))
            {
                if (TryGetStringConstant(assignment.Right, model, out value))
                {
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private void IndexWorkflowName(string displayName, string workflowNodeId)
        {
            AddToIndex(_workflowNodeIdsByName, displayName, workflowNodeId);
        }

        /// <summary>Indexes the workflow's signal/query/update member names for string-call resolution.</summary>
        private void IndexHandlerNames(INamedTypeSymbol type, string workflowNodeId)
        {
            foreach (var member in type.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowSignalMethod(method):
                        AddToIndex(_signalNamesByWorkflow, HandlerDisplayName(method, WorkflowDetection.GetAttributeName(method, WorkflowDetection.WorkflowSignalAttributeName), trimAsync: true), workflowNodeId);
                        break;
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowQueryMethod(method):
                        AddToIndex(_queryNamesByWorkflow, HandlerDisplayName(method, WorkflowDetection.GetAttributeName(method, WorkflowDetection.WorkflowQueryAttributeName), trimAsync: false), workflowNodeId);
                        break;
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowUpdateMethod(method):
                        AddToIndex(_updateNamesByWorkflow, HandlerDisplayName(method, WorkflowDetection.GetAttributeName(method, WorkflowDetection.WorkflowUpdateAttributeName), trimAsync: true), workflowNodeId);
                        break;
                    case IPropertySymbol property when WorkflowDetection.IsWorkflowQueryProperty(property):
                        AddToIndex(_queryNamesByWorkflow, property.Name, workflowNodeId);
                        break;
                }
            }
        }

        /// <summary>
        /// SDK display-name rules for workflow handlers: the attribute's name
        /// when present, otherwise the method name — with the trailing "Async"
        /// trimmed for signals and updates (not queries).
        /// </summary>
        private static string HandlerDisplayName(IMethodSymbol method, string? attributeName, bool trimAsync)
        {
            if (attributeName is not null)
            {
                return attributeName;
            }

            var name = method.Name;
            if (trimAsync && name.Length > 5 && name.EndsWith("Async", StringComparison.Ordinal))
            {
                name = name[..^5];
            }

            return name;
        }

        /// <summary>
        /// Resolves a workflow display name to a node, ignoring nodes that are
        /// interface declarations (their display names also belong to impls).
        /// </summary>
        private string? ResolveWorkflowIdByName(string name)
        {
            if (!_workflowNodeIdsByName.TryGetValue(name, out var hits))
            {
                return null;
            }

            var concrete = hits
                .Where(id => !_workflowInterfaceFqns.Contains(id.StartsWith("Workflow:", StringComparison.Ordinal) ? id["Workflow:".Length..] : id))
                .ToList();
            return concrete.Count == 1 ? concrete[0] : null;
        }

        /// <summary>SDK activity name: the attribute's name or the method name verbatim.</summary>
        private string ActivityDisplayName(IMethodSymbol method) =>
            WorkflowDetection.GetAttributeName(method, WorkflowDetection.ActivityAttributeName) ?? method.Name;

        /// <summary>
        /// SDK workflow name: the attribute's name, otherwise the type name —
        /// with the leading "I" trimmed for interfaces when followed by
        /// another capital.
        /// </summary>
        private string WorkflowDisplayName(INamedTypeSymbol type)
        {
            var name = WorkflowDetection.GetAttributeName(type, WorkflowDetection.WorkflowAttributeName);
            if (name is not null)
            {
                return name;
            }

            name = type.Name;
            if (type.TypeKind == TypeKind.Interface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            {
                name = name[1..];
            }

            return name;
        }

        /// <summary>
        /// Indexes an activity implementation under every contract member it
        /// implements — interface methods (implicit or explicit) and the
        /// abstract/virtual methods it overrides.
        /// </summary>
        private void IndexActivityContracts(IMethodSymbol method, string activityNodeId)
        {
            var interfaceMethod = FindImplementedInterfaceMethod(method);
            if (interfaceMethod is not null)
            {
                AddToIndex(_activityImplsByContractMethod, MethodFullName(interfaceMethod), activityNodeId);
            }

            for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
            {
                AddToIndex(_activityImplsByContractMethod, MethodFullName(overridden), activityNodeId);
            }
        }

        private static IMethodSymbol? FindOverriddenActivityMethod(IMethodSymbol method)
        {
            for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
            {
                if (WorkflowDetection.IsActivityMethod(overridden))
                {
                    return overridden;
                }
            }

            return null;
        }

        private static IMethodSymbol? FindImplementedInterfaceMethod(IMethodSymbol method)
        {
            if (method.ContainingType is not INamedTypeSymbol type)
            {
                return null;
            }

            foreach (var iface in type.AllInterfaces)
            {
                foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
                {
                    if (type.FindImplementationForInterfaceMember(member) is IMethodSymbol impl &&
                        SymbolEqualityComparer.Default.Equals(impl, method))
                    {
                        return member;
                    }
                }
            }

            return null;
        }

        private static void AddToIndex(Dictionary<string, List<string>> index, string key, string nodeId)
        {
            if (!index.TryGetValue(key, out var list))
            {
                index[key] = list = [];
            }

            if (!list.Contains(nodeId))
            {
                list.Add(nodeId);
            }
        }

        private static bool MethodHeartbeats(IMethodSymbol method)
        {
            foreach (var syntaxReference in method.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax() is not MethodDeclarationSyntax methodSyntax)
                {
                    continue;
                }

                foreach (var invocation in methodSyntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                        memberAccess.Name.Identifier.ValueText == "Heartbeat")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void MarkHeartbeats(string activityNodeId)
        {
            _nodes[activityNodeId] = _nodes[activityNodeId] with { Heartbeats = true };
        }

        private void TrackCompilationWorkflow(SemanticModel model, string workflowNodeId)        {
            var compilation = model.Compilation;
            if (!_workflowsByCompilation.TryGetValue(compilation, out var workflows))
            {
                workflows = new HashSet<string>(StringComparer.Ordinal);
                _workflowsByCompilation[compilation] = workflows;
            }

            workflows.Add(workflowNodeId);
        }

        private IEnumerable<string> GetWorkflowsInCompilation(SemanticModel model) =>
            _workflowsByCompilation.TryGetValue(model.Compilation, out var workflows)
                ? workflows
                : Enumerable.Empty<string>();

        private string? GetEnclosingWorkflowNodeId(InvocationExpressionSyntax invocation, SemanticModel model)
        {
            var enclosingMethod = SymbolUtilities.GetEnclosingRegularMethod(model.GetEnclosingSymbol(invocation.SpanStart));
            var containingType = enclosingMethod?.ContainingType;
            if (containingType is null || !WorkflowDetection.IsWorkflowType(containingType))
            {
                return null;
            }

            return AddWorkflowNode(containingType);
        }

        private string AddWorkflowNode(INamedTypeSymbol type)
        {
            var fullName = TypeFullName(type);
            if (type.TypeKind == TypeKind.Interface)
            {
                _workflowInterfaceFqns.Add(fullName);
            }

            if (_workflowNodeIds.TryGetValue(fullName, out var existing))
            {
                return existing;
            }

            var id = "Workflow:" + fullName;
            var handlers = new List<TopologyHandler>();
            foreach (var member in type.GetMembers())
            {
                switch (member)
                {
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowRunMethod(method):
                        handlers.Add(new TopologyHandler(TopologyHandlerKinds.Run, method.Name, HandlerSignature(method)));
                        break;
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowSignalMethod(method):
                        handlers.Add(new TopologyHandler(TopologyHandlerKinds.Signal, method.Name, HandlerSignature(method)));
                        break;
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowQueryMethod(method):
                        handlers.Add(new TopologyHandler(TopologyHandlerKinds.Query, method.Name, HandlerSignature(method)));
                        break;
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowUpdateMethod(method):
                        handlers.Add(new TopologyHandler(TopologyHandlerKinds.Update, method.Name, HandlerSignature(method)));
                        break;
                    case IPropertySymbol property when WorkflowDetection.IsWorkflowQueryProperty(property):
                        handlers.Add(new TopologyHandler(
                            TopologyHandlerKinds.Query,
                            property.Name,
                            "→ " + ShortType(property.Type)));
                        break;
                }
            }

            var (file, line) = GetLocation(type);
            var (repo, path) = NodeLocation(file);
            _nodes[id] = new TopologyNode(id, TopologyNodeKinds.Workflow, type.Name, file, line, null, handlers)
            {
                Repo = repo,
                Path = path,
            };
            _workflowNodeIds[fullName] = id;
            return id;
        }

        private string AddActivityNode(IMethodSymbol method)
        {
            var fullName = MethodFullName(method);
            if (_activityNodeIds.TryGetValue(fullName, out var existing))
            {
                return existing;
            }

            var id = "Activity:" + fullName;
            var (file, line) = GetLocation(method);
            var (repo, path) = NodeLocation(file);
            _nodes[id] = new TopologyNode(
                id,
                TopologyNodeKinds.Activity,
                FriendlyName(method),
                file,
                line,
                null,
                Array.Empty<TopologyHandler>())
            {
                Repo = repo,
                Path = path,
            };
            _activityNodeIds[fullName] = id;
            return id;
        }

        private string AddNexusNode(IMethodSymbol method)
        {
            var id = "Nexus:" + MethodFullName(method);
            if (_nodes.ContainsKey(id))
            {
                return id;
            }

            var (file, line) = GetLocation(method);
            _nodes[id] = new TopologyNode(
                id,
                TopologyNodeKinds.Nexus,
                FriendlyName(method),
                file,
                line,
                null,
                Array.Empty<TopologyHandler>());
            return id;
        }

        private string AddTaskQueueNode(string name)
        {
            var id = "TaskQueue:" + name;
            if (_nodes.ContainsKey(id))
            {
                return id;
            }

            _nodes[id] = new TopologyNode(
                id,
                TopologyNodeKinds.TaskQueue,
                name,
                null,
                null,
                null,
                Array.Empty<TopologyHandler>());
            return id;
        }

        private string AddUnknownNode(string unknownKind, string name)
        {
            var id = $"Unknown:{Capitalize(unknownKind)}:\"{name}\"";
            if (_nodes.ContainsKey(id))
            {
                return id;
            }

            _nodes[id] = new TopologyNode(
                id,
                TopologyNodeKinds.Unknown,
                name,
                null,
                null,
                unknownKind,
                Array.Empty<TopologyHandler>());
            return id;
        }

        private void AddEdge(string from, string to, string kind) => AddEdge(from, to, kind, null);

        private void AddEdge(string from, string to, string kind, string? callOptions)
        {
            var key = (from, to, kind);
            if (!_edges.TryGetValue(key, out var accumulator))
            {
                accumulator = new EdgeAccumulator();
                _edges[key] = accumulator;
            }

            accumulator.CallOptions ??= callOptions;
        }

        private void AddCallEdge(
            string from,
            string to,
            string kind,
            int ordinal,
            bool inLoop,
            string? callOptions = null,
            bool? heartbeats = null,
            bool? heartbeatIssue = null)
        {
            AddEdge(from, to, kind, callOptions);
            var accumulator = _edges[(from, to, kind)];
            accumulator.Orders.Add(ordinal);
            accumulator.InLoop |= inLoop;
            accumulator.Heartbeats |= heartbeats == true;
            accumulator.HeartbeatIssue |= heartbeatIssue == true;
        }

        private int NextActivityOrdinal(string workflowId)
        {
            _activityCallOrdinals.TryGetValue(workflowId, out var current);
            var next = current + 1;
            _activityCallOrdinals[workflowId] = next;
            return next;
        }

        /// <summary>
        /// True when the invocation is syntactically nested in a loop, without
        /// crossing a method, lambda, or accessor boundary.
        /// </summary>
        private static bool IsInsideLoop(SyntaxNode node)
        {
            foreach (var ancestor in node.Ancestors())
            {
                if (ancestor is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
                {
                    return true;
                }

                if (ancestor is MethodDeclarationSyntax or ConstructorDeclarationSyntax or
                    LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or AccessorDeclarationSyntax)
                {
                    return false;
                }
            }

            return false;
        }

        private void TrackCompilationActivity(SemanticModel model, string activityNodeId)
        {
            var compilation = model.Compilation;
            if (!_activitiesByCompilation.TryGetValue(compilation, out var activities))
            {
                activities = new HashSet<string>(StringComparer.Ordinal);
                _activitiesByCompilation[compilation] = activities;
            }

            activities.Add(activityNodeId);
        }

        private IEnumerable<string> GetActivitiesInCompilation(SemanticModel model) =>
            _activitiesByCompilation.TryGetValue(model.Compilation, out var activities)
                ? activities
                : Enumerable.Empty<string>();

        private sealed class EdgeAccumulator
        {
            public List<int> Orders { get; } = [];

            public bool InLoop { get; set; }

            public string? CallOptions { get; set; }

            public bool Heartbeats { get; set; }

            public bool HeartbeatIssue { get; set; }
        }

        private static bool TryResolveTypedLambdaTarget(
            SemanticModel model,
            InvocationExpressionSyntax invocation,
            out IMethodSymbol? target)
        {
            target = null;
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                var expression = argument.Expression;
                while (expression is CastExpressionSyntax cast)
                {
                    expression = cast.Expression;
                }

                if (expression is not LambdaExpressionSyntax lambda)
                {
                    continue;
                }

                var body = lambda.Body;
                while (body is ParenthesizedExpressionSyntax parenthesized)
                {
                    body = parenthesized.Expression;
                }

                if (body is AwaitExpressionSyntax awaitExpression)
                {
                    body = awaitExpression.Expression;
                }

                if (body is InvocationExpressionSyntax bodyInvocation)
                {
                    target = model.GetSymbolInfo(bodyInvocation).Symbol as IMethodSymbol;
                    return target is not null;
                }
            }

            return false;
        }

        private static bool TryResolveStringTarget(
            InvocationExpressionSyntax invocation,
            SemanticModel model,
            out string name)
        {
            name = string.Empty;
            var arguments = invocation.ArgumentList.Arguments;
            if (arguments.Count == 0)
            {
                return false;
            }

            var value = model.GetConstantValue(arguments[0].Expression);
            if (value.HasValue && value.Value is string stringValue)
            {
                name = stringValue;
                return true;
            }

            return false;
        }

        private static bool TryGetClientStartTaskQueue(
            InvocationExpressionSyntax invocation,
            SemanticModel model,
            out string taskQueue)
        {
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (argument.Expression is BaseObjectCreationExpressionSyntax creation)
                {
                    // Initializer form: new XOptions { TaskQueue = "..." }.
                    if (TryGetInitializerTaskQueue(creation, model, out taskQueue))
                    {
                        return true;
                    }

                    // Constructor form: new StartActivityOptions("id", "queue").
                    if (creation.ArgumentList is { } creationArgs &&
                        model.GetSymbolInfo(creation).Symbol is IMethodSymbol constructor &&
                        TryGetNamedArgumentConstant(creationArgs.Arguments, constructor.Parameters, "taskQueue", model, out taskQueue))
                    {
                        return true;
                    }
                }
            }

            taskQueue = string.Empty;
            return false;
        }

        private static bool TryGetNamedQueueArgument(
            SeparatedSyntaxList<ArgumentSyntax> arguments,
            ImmutableArray<IParameterSymbol> parameters,
            SemanticModel model,
            out string value)
        {
            if (TryGetNamedArgumentConstant(arguments, parameters, "taskQueue", model, out value))
            {
                return true;
            }

            // Non-constant queue argument (env/config) — try resolution by position.
            for (var i = 0; i < parameters.Length && i < arguments.Count; i++)
            {
                if (parameters[i].Name == "taskQueue" &&
                    arguments[i].NameColon is null &&
                    TryGetQueueFromExpression(arguments[i].Expression, model, out value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetNamedArgumentConstant(
            SeparatedSyntaxList<ArgumentSyntax> arguments,
            ImmutableArray<IParameterSymbol> parameters,
            string parameterName,
            SemanticModel model,
            out string value)
        {
            foreach (var argument in arguments)
            {
                if (argument.NameColon?.Name.Identifier.ValueText == parameterName &&
                    TryGetStringConstant(argument.Expression, model, out value))
                {
                    return true;
                }
            }

            for (var i = 0; i < parameters.Length && i < arguments.Count; i++)
            {
                if (parameters[i].Name == parameterName &&
                    arguments[i].NameColon is null &&
                    TryGetStringConstant(arguments[i].Expression, model, out value))
                {
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        private static string? ResolveWorkerTaskQueue(ExpressionSyntax receiver, SemanticModel model)
        {
            foreach (var creation in receiver.DescendantNodesAndSelf().OfType<BaseObjectCreationExpressionSyntax>())
            {
                var type = model.GetTypeInfo(creation).Type;
                if (type is not null && TypeNames.FullName(type) == SdkNames.TemporalWorkerOptionsType)
                {
                    if (TryGetWorkerOptionsTaskQueue(creation, model, out var queue))
                    {
                        return queue;
                    }
                }
            }

            if (model.GetSymbolInfo(receiver).Symbol is ILocalSymbol local)
            {
                if (local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is VariableDeclaratorSyntax
                    {
                        Initializer.Value: BaseObjectCreationExpressionSyntax init
                    })
                {
                    if (TryGetWorkerOptionsTaskQueue(init, model, out var queue))
                    {
                        return queue;
                    }
                }
            }

            return null;
        }

        private static bool TryGetWorkerOptionsTaskQueue(
            BaseObjectCreationExpressionSyntax creation,
            SemanticModel model,
            out string taskQueue)
        {
            if (creation.ArgumentList is { Arguments.Count: > 0 } argumentList &&
                TryGetQueueFromExpression(argumentList.Arguments[0].Expression, model, out taskQueue))
            {
                return true;
            }

            if (TryGetInitializerTaskQueue(creation, model, out taskQueue))
            {
                return true;
            }

            taskQueue = string.Empty;
            return false;
        }

        private static bool TryGetInitializerTaskQueue(
            BaseObjectCreationExpressionSyntax creation,
            SemanticModel model,
            out string taskQueue)
        {
            if (creation.Initializer is { } initializer)
            {
                foreach (var item in initializer.Expressions)
                {
                    if (item is AssignmentExpressionSyntax
                        {
                            Left: IdentifierNameSyntax { Identifier.ValueText: "TaskQueue" }
                        } assignment)
                    {
                        return TryGetQueueFromExpression(assignment.Right, model, out taskQueue);
                    }
                }
            }

            taskQueue = string.Empty;
            return false;
        }

        /// <summary>
        /// Queue-name extraction: a string constant, or a best-effort
        /// env-default / configuration-key resolution.
        /// </summary>
        private static bool TryGetQueueFromExpression(ExpressionSyntax expression, SemanticModel model, out string value)
        {
            if (TryGetStringConstant(expression, model, out value))
            {
                return true;
            }

            var resolved = ConfigQueueResolver.Resolve(expression, model);
            if (resolved is not null)
            {
                value = resolved;
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static bool TryGetStringConstant(ExpressionSyntax expression, SemanticModel model, out string value)
        {
            var constant = model.GetConstantValue(expression);
            if (constant.HasValue && constant.Value is string stringValue)
            {
                value = stringValue;
                return true;
            }

            value = string.Empty;
            return false;
        }

        /// <summary>Compact signature for handler ports, e.g. "string → Task&lt;string&gt;".</summary>
        private static string? HandlerSignature(IMethodSymbol method)
        {
            var parameters = string.Join(
                ", ",
                method.Parameters.Select(p => ShortType(p.Type)));
            var returns = ShortType(method.ReturnType);
            return parameters.Length == 0 ? "→ " + returns : parameters + " → " + returns;
        }

        private static string ShortType(ITypeSymbol type) =>
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        private static string FriendlyName(IMethodSymbol method)
        {
            var typeName = method.ContainingType?.Name;
            return string.IsNullOrEmpty(typeName) ? method.Name : typeName + "." + method.Name;
        }

        private static string TypeFullName(ITypeSymbol type) => type.ToDisplayString(FullNameFormat);

        private static string MethodFullName(IMethodSymbol method) => method.ToDisplayString(FullNameFormat);

        private (string? Repo, string? Path) NodeLocation(string? file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return (_currentRepo.Repo, null);
            }

            if (_currentRepo.Root is null)
            {
                return (_currentRepo.Repo, file);
            }

            string relative;
            try
            {
                relative = System.IO.Path.GetRelativePath(_currentRepo.Root, file);
            }
            catch (System.ArgumentException)
            {
                relative = file;
            }

            return (_currentRepo.Repo, relative);
        }

        private static (string? File, int? Line) GetLocation(ISymbol symbol)
        {
            var syntaxReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxReference is null)
            {
                return (null, null);
            }

            var lineSpan = syntaxReference.SyntaxTree.GetLineSpan(syntaxReference.Span);
            if (!lineSpan.IsValid)
            {
                return (null, null);
            }

            return (lineSpan.Path, lineSpan.StartLinePosition.Line + 1);
        }

        private static string Capitalize(string value) =>
            value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value.Substring(1);
    }
}
