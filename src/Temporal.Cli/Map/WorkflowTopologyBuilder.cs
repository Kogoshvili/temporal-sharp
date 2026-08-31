using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

    // The Kogoshvili.Temporal.Hosting workflow-side facades, matched by
    // fully-qualified name so the CLI stays free of a dependency on the hosting
    // assembly.
    private const string ActivityOpsType = "Kogoshvili.Temporal.Hosting.ActivityOps";
    private const string ChildWorkflowOpsType = "Kogoshvili.Temporal.Hosting.ChildWorkflowOps";

    // The hosting starter's worker-registration extension class.
    private const string HostingExtensionsType = "Microsoft.Extensions.DependencyInjection.TemporalServiceCollectionExtensions";

    private static readonly SymbolDisplayFormat FullNameFormat = SymbolDisplayFormat.CSharpErrorMessageFormat;

    public static Task<TopologyGraph> BuildAsync(Solution solution, CancellationToken cancellationToken)
        => BuildAsync(new[] { solution }, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        var state = new BuilderState();

        // Pass 1: index every workflow and activity declared in source, across
        // all solutions.
        foreach (var solution in solutions)
        {
            foreach (var (model, root) in await GetSemanticModelsAsync(solution, cancellationToken).ConfigureAwait(false))
            {
                state.CollectDeclarations(root, model);
            }
        }

        // Pass 2: resolve call edges from workflow bodies and worker/client
        // registrations, now that every node is indexed.
        foreach (var solution in solutions)
        {
            foreach (var (model, root) in await GetSemanticModelsAsync(solution, cancellationToken).ConfigureAwait(false))
            {
                state.CollectEdges(root, model);
            }
        }

        return state.Build();
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
        private readonly HashSet<TopologyEdge> _edges = new();
        private readonly Dictionary<Compilation, HashSet<string>> _workflowsByCompilation = new();

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
                    TrackCompilationWorkflow(model, AddWorkflowNode(typeSymbol));
                }

                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is IMethodSymbol method && WorkflowDetection.IsActivityMethod(method))
                    {
                        AddActivityNode(method);
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
                else if (SdkNames.ClientWorkflowStartMethods.Contains(target.Name))
                {
                    HandleClientStart(invocation, model);
                }
            }

            CollectWorkerRegistrations(root, model);
            CollectHostedWorkerRegistrations(root, model);
        }

        public TopologyGraph Build()
        {
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
            if (TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                if (WorkflowDetection.IsActivityMethod(targetMethod!))
                {
                    AddEdge(workflowId, AddActivityNode(targetMethod!), kind);
                }
                else
                {
                    AddEdge(workflowId, AddUnknownNode(UnknownActivity, FriendlyName(targetMethod!)), kind);
                }

                return;
            }

            if (TryResolveStringTarget(invocation, model, out var name))
            {
                AddEdge(workflowId, AddUnknownNode(UnknownActivity, name), kind);
            }
        }

        private void AddChildWorkflowEdge(string workflowId, InvocationExpressionSyntax invocation, SemanticModel model)
        {
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

            if (TryResolveStringTarget(invocation, model, out var name))
            {
                AddEdge(workflowId, AddUnknownNode(UnknownChildWorkflow, name), TopologyEdgeKinds.ChildWorkflow);
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
            if (!TryResolveTypedLambdaTarget(model, invocation, out var targetMethod))
            {
                return;
            }

            var containingType = targetMethod!.ContainingType;
            if (containingType is null || !WorkflowDetection.IsWorkflowType(containingType))
            {
                return;
            }

            var workflowId = AddWorkflowNode(containingType);
            if (TryGetClientStartTaskQueue(invocation, model, out var taskQueue))
            {
                AddEdge(workflowId, AddTaskQueueNode(taskQueue), TopologyEdgeKinds.TaskQueue);
            }
        }

        private void CollectWorkerRegistrations(SyntaxNode root, SemanticModel model)
        {
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                    memberAccess.Name.Identifier.ValueText != "AddWorkflow")
                {
                    continue;
                }

                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
                    method.TypeArguments.Length != 1 ||
                    method.ContainingType is null ||
                    TypeNames.FullName(method.ContainingType) != SdkNames.TemporalWorkerOptionsType)
                {
                    continue;
                }

                if (ResolveWorkerTaskQueue(memberAccess.Expression, model) is not { } taskQueue)
                {
                    continue;
                }

                var workflowType = method.TypeArguments[0] as INamedTypeSymbol;
                if (workflowType is null || !WorkflowDetection.IsWorkflowType(workflowType))
                {
                    continue;
                }

                AddEdge(AddWorkflowNode(workflowType), AddTaskQueueNode(taskQueue), TopologyEdgeKinds.TaskQueue);
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
                // types; associate the workflows declared in the same compilation
                // (best-effort proxy for the scanned assembly).
                foreach (var workflowId in GetWorkflowsInCompilation(model))
                {
                    AddEdge(workflowId, AddTaskQueueNode(taskQueue), TopologyEdgeKinds.TaskQueue);
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

        private void TrackCompilationWorkflow(SemanticModel model, string workflowNodeId)
        {
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
                        handlers.Add(new TopologyHandler(TopologyHandlerKinds.Run, method.Name));
                        break;
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowSignalMethod(method):
                        handlers.Add(new TopologyHandler(TopologyHandlerKinds.Signal, method.Name));
                        break;
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowQueryMethod(method):
                        handlers.Add(new TopologyHandler(TopologyHandlerKinds.Query, method.Name));
                        break;
                    case IMethodSymbol method when WorkflowDetection.IsWorkflowUpdateMethod(method):
                        handlers.Add(new TopologyHandler(TopologyHandlerKinds.Update, method.Name));
                        break;
                    case IPropertySymbol property when WorkflowDetection.IsWorkflowQueryProperty(property):
                        handlers.Add(new TopologyHandler(TopologyHandlerKinds.Query, property.Name));
                        break;
                }
            }

            var (file, line) = GetLocation(type);
            _nodes[id] = new TopologyNode(id, TopologyNodeKinds.Workflow, type.Name, file, line, null, handlers);
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
            _nodes[id] = new TopologyNode(
                id,
                TopologyNodeKinds.Activity,
                FriendlyName(method),
                file,
                line,
                null,
                Array.Empty<TopologyHandler>());
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

        private void AddEdge(string from, string to, string kind)
        {
            _edges.Add(new TopologyEdge(from, to, kind, null));
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
                if (argument.Expression is BaseObjectCreationExpressionSyntax creation &&
                    TryGetInitializerTaskQueue(creation, model, out taskQueue))
                {
                    return true;
                }
            }

            taskQueue = string.Empty;
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
                TryGetStringConstant(argumentList.Arguments[0].Expression, model, out taskQueue))
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
                        return TryGetStringConstant(assignment.Right, model, out taskQueue);
                    }
                }
            }

            taskQueue = string.Empty;
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

        private static string FriendlyName(IMethodSymbol method)
        {
            var typeName = method.ContainingType?.Name;
            return string.IsNullOrEmpty(typeName) ? method.Name : typeName + "." + method.Name;
        }

        private static string TypeFullName(ITypeSymbol type) => type.ToDisplayString(FullNameFormat);

        private static string MethodFullName(IMethodSymbol method) => method.ToDisplayString(FullNameFormat);

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
