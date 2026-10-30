using System.Collections.Immutable;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace San14.Data.AI.Original;

public sealed record OriginalAiUnresolvedNode(uint BundleKey, uint TreeKey, int NodeIndex,
    uint TypeId, string Requirement)
{
    /// <summary>Serialized native words, retained for offline grouping by
    /// factory and shape. These are diagnostics, not another execution input.</summary>
    public ImmutableArray<uint> Arguments { get; init; } = [];
    public uint? ConditionQueryId { get; init; }
    public int? ConditionWordIndex { get; init; }
}

/// <summary>A compiled synchronous region, with all unsupported bindings
/// reported separately. Unsupported nodes are never converted to Failure.</summary>
public sealed class OriginalAiCompiledRegion
{
    private readonly Func<OriginalAiOperation, OriginalAiNodeStatus>? execute;
    public uint BundleKey { get; }
    public uint TreeKey { get; }
    public int RootNodeIndex { get; }
    public ImmutableArray<OriginalAiUnresolvedNode> Unresolved { get; }
    public bool IsBound => Unresolved.IsEmpty;
    internal OriginalAiCompiledRegion(uint bundle, uint tree, int root,
        ImmutableArray<OriginalAiUnresolvedNode> unresolved,
        Func<OriginalAiOperation, OriginalAiNodeStatus>? execute)
        => (BundleKey, TreeKey, RootNodeIndex, Unresolved, this.execute) = (bundle, tree, root, unresolved, execute);
    public OriginalAiNodeStatus Invoke(OriginalAiOperation operation)
    {
        if (execute is null) throw new InvalidDataException(string.Join("; ", Unresolved.Select(item =>
            $"original_ai.bundle.{item.BundleKey:X8}.tree.{item.TreeKey:X8}.node.{item.NodeIndex}.type.{item.TypeId:X8}: {item.Requirement}")));
        return execute(operation);
    }
}

/// <summary>Immutable resource definitions with one-time, strongly typed
/// lowering. Compilation never captures a borrowed GameState or an operation.</summary>
public sealed class OriginalAiBehaviorProgram
{
    private readonly OriginalAiBehaviorDocument document;
    private readonly CompiledOriginalAiScriptLibrary? scripts;
    private readonly Dictionary<(uint Bundle, int Root), OriginalAiCompiledRegion> regions = [];
    private readonly object compilationGate = new();
    public OriginalAiBehaviorProgram(OriginalAiBehaviorDocument document, CompiledOriginalAiScriptLibrary? scripts = null)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported || !RuntimeFeature.IsDynamicCodeCompiled)
            throw new PlatformNotSupportedException("Original AI requires its configured compiled-expression backend.");
        this.document = document; this.scripts = scripts;
    }
    public OriginalAiCompiledRegion Compile(uint bundleKey, int rootNodeIndex)
    {
        lock (compilationGate)
        {
            if (regions.TryGetValue((bundleKey, rootNodeIndex), out var cached)) return cached;
            var bundle = document.Bundles.SingleOrDefault(item => item.ResourceKey == bundleKey)
                ?? throw new InvalidDataException($"original_ai.bundle.{bundleKey:X8}");
            if ((uint)rootNodeIndex >= (uint)bundle.Tree.Nodes.Length) throw new ArgumentOutOfRangeException(nameof(rootNodeIndex));
            var missing = ImmutableArray.CreateBuilder<OriginalAiUnresolvedNode>();
            if (!bundle.Blackboard.Values.IsEmpty)
                missing.Add(new(bundle.ResourceKey, bundle.Tree.Key, rootNodeIndex, bundle.Tree.Nodes[rootNodeIndex].TypeId,
                    "serialized blackboard initial-value binding"));
            var operation = Expression.Parameter(typeof(OriginalAiOperation), "operation");
            Expression body = Lower(bundle, rootNodeIndex, operation, missing, scripts);
            var unresolved = missing.ToImmutable();
            var region = new OriginalAiCompiledRegion(bundleKey, bundle.Tree.Key, rootNodeIndex, unresolved,
                unresolved.IsEmpty ? Expression.Lambda<Func<OriginalAiOperation, OriginalAiNodeStatus>>(body, operation).Compile() : null);
            regions.Add((bundleKey, rootNodeIndex), region); return region;
        }
    }

    private static Expression Lower(OriginalAiBehaviorBundle bundle, int index, ParameterExpression operation,
        ImmutableArray<OriginalAiUnresolvedNode>.Builder missing, CompiledOriginalAiScriptLibrary? scripts)
    {
        var node = bundle.Tree.Nodes[index];
        var args = node.Arguments;
        Expression Unsupported(string reason)
        {
            missing.Add(new(bundle.ResourceKey, bundle.Tree.Key, index, node.TypeId, reason)
            { Arguments = node.Arguments });
            // This expression is never compiled when any binding is missing.
            return Expression.Default(typeof(OriginalAiNodeStatus));
        }
        Expression Native(Func<OriginalAiOperation, OriginalAiNodeStatus> action)
        {
            Func<OriginalAiOperation, OriginalAiNodeStatus> bound = current =>
            {
                try { return action(current); }
                catch (InvalidDataException error)
                {
                    throw new InvalidDataException($"original_ai.bundle.{bundle.ResourceKey:X8}.tree.{bundle.Tree.Key:X8}." +
                        $"node.{node.Index}.type.{node.TypeId:X8}: {error.Message}", error);
                }
            };
            return Expression.Invoke(Expression.Constant(bound), operation);
        }
        if (node.IsNative)
        {
            if (node.ChildIndices.Length != 0 && node.TypeId != 0x78F9C619)
                return Unsupported("native child/continuation semantics");
            switch (node.TypeId)
            {
                case 0x874694D6 when args.Length == 5:
                    return Native(current => current.Development(args[0], args[1], unchecked((int)args[2]),
                        unchecked((int)args[3]), unchecked((int)args[4])));
                case 0xA8131535 when args.Length == 3:
                    return Native(current => current.Appoint(args[0], args[1], args[2]));
                case 0x72E99C29 when args.Length == 8:
                {
                    if (scripts is null) return Unsupported("normal param_s compiled script resource");
                    Func<OriginalAiScriptArguments, int> script;
                    try { script = scripts.Get(unchecked((int)args[0])); }
                    catch (KeyNotFoundException) { return Unsupported($"param_s script {args[0]}"); }
                    //05C720 clears seven argument keys. All installed CallScript
                    //records write six; the seventh retains its actual zero.
                    var keys = args.Slice(2, args.Length - 2);
                    return Native(current => current.CallScript(script, args[1], keys));
                }
                default:
                {
                    int missingBeforeChildren = missing.Count;
                    var childExpressions = node.ChildIndices.Select(child =>
                        Lower(bundle, child, operation, missing, scripts)).ToArray();
                    // Traverse every child for precise missing locations, but
                    // never JIT delegates for a native subtree already known
                    // to be unavailable. The enclosing region cannot execute.
                    if (missing.Count != missingBeforeChildren)
                        return Expression.Default(typeof(OriginalAiNodeStatus));
                    var children = childExpressions.Select((child, childIndex) =>
                    {
                        var update = Expression.Lambda<Func<OriginalAiOperation, OriginalAiNodeStatus>>(
                            child, operation).Compile();
                        if (node.TypeId != 0x78F9C619) return update;
                        var subtree = ControlSubtree(bundle, node.ChildIndices[childIndex]);
                        // The native list loop explicitly enters, updates and
                        // exits each child for each element, even on Running.
                        return current =>
                        {
                            current.ExitBehaviorSubtree(bundle.ResourceKey, subtree);
                            try { return update(current); }
                            finally { current.ExitBehaviorSubtree(bundle.ResourceKey, subtree); }
                        };
                    }).ToArray();
                    string? preciseMissing = null;
                    if (children.Length == 0)
                    {
                        if (OriginalAiNumericBindings.TryBind(node, out var numericAction, out var numericMissing) && numericAction is not null)
                            return Native(numericAction);
                        preciseMissing ??= numericMissing;
                        if (OriginalAiControlLeafBindings.TryBind(node, out var controlAction, out var controlMissing) && controlAction is not null)
                            return Native(controlAction);
                        preciseMissing ??= controlMissing;
                        if (OriginalAiArmyPositionBindings.TryBind(node, out var positionAction, out var positionMissing) && positionAction is not null)
                            return Native(positionAction);
                        preciseMissing ??= positionMissing;
                        if (OriginalAiArmyNeighborBindings.TryBind(node, out var neighborAction, out var neighborMissing) && neighborAction is not null)
                            return Native(neighborAction);
                        preciseMissing ??= neighborMissing;
                        if (OriginalAiArmyCommandBindings.TryBind(node, out var armyAction, out var armyMissing) && armyAction is not null)
                            return Native(armyAction);
                        preciseMissing ??= armyMissing;
                        if (OriginalAiHexFormationInformationBindings.TryBind(node, out var hexFormationAction, out var hexFormationMissing) && hexFormationAction is not null)
                            return Native(hexFormationAction);
                        preciseMissing ??= hexFormationMissing;
                        if (OriginalAiDepartureBindings.TryBind(node, out var departureAction, out var departureMissing) && departureAction is not null)
                            return Native(departureAction);
                        preciseMissing ??= departureMissing;
                        if (OriginalAiDepartureTroopsBindings.TryBind(node, out var departureTroopsAction, out var departureTroopsMissing) && departureTroopsAction is not null)
                            return Native(departureTroopsAction);
                        preciseMissing ??= departureTroopsMissing;
                        if (OriginalAiDefinitionBindings.TryBind(node, out var definitionAction, out var definitionMissing) && definitionAction is not null)
                            return Native(definitionAction);
                        preciseMissing ??= definitionMissing;
                        if (OriginalAiBaseInformationBindings.TryBind(node, out var baseInfoAction, out var baseInfoMissing) && baseInfoAction is not null)
                            return Native(baseInfoAction);
                        preciseMissing ??= baseInfoMissing;
                        if (OriginalAiTroopsInformationBindings.TryBind(node, out var troopsInfoAction, out var troopsInfoMissing) && troopsInfoAction is not null)
                            return Native(troopsInfoAction);
                        preciseMissing ??= troopsInfoMissing;
                        if (OriginalAiCautionGroupPlanningBindings.TryBind(node, out var cautionPlanningAction, out var cautionPlanningMissing) && cautionPlanningAction is not null)
                            return Native(cautionPlanningAction);
                        preciseMissing ??= cautionPlanningMissing;
                        if (OriginalAiCautionInformationBindings.TryBind(node, out var cautionInfoAction, out var cautionInfoMissing) && cautionInfoAction is not null)
                            return Native(cautionInfoAction);
                        preciseMissing ??= cautionInfoMissing;
                        if (OriginalAiTransportBindings.TryBind(node, out var transportAction, out var transportMissing) && transportAction is not null)
                            return Native(transportAction);
                        preciseMissing ??= transportMissing;
                        if (OriginalAiHireBindings.TryBind(node, out var hireAction, out var hireMissing) && hireAction is not null)
                            return Native(hireAction);
                        preciseMissing ??= hireMissing;
                        if (OriginalAiPersonInformationBindings.TryBind(node, out var personAction, out var personMissing) && personAction is not null)
                            return Native(personAction);
                        preciseMissing ??= personMissing;
                        if (OriginalAiIntelligenceCommandBindings.TryBind(node, out var homeAction, out var homeMissing) && homeAction is not null)
                            return Native(homeAction);
                        preciseMissing ??= homeMissing;
                        if (OriginalAiIntelligenceMissionBindings.TryBind(node, out var missionAction, out var missionMissing) && missionAction is not null)
                            return Native(missionAction);
                        preciseMissing ??= missionMissing;
                        if (OriginalAiSpyAllocationBindings.TryBind(node, out var allocationAction, out var allocationMissing) && allocationAction is not null)
                            return Native(allocationAction);
                        preciseMissing ??= allocationMissing;
                        if (node.TypeId == 0x8B432DF4)
                            preciseMissing = "original_ai.friendship.District_actor_source_receiver: the installed consumer reads its actual District receiver, not the Force relationship owner";
                        if (OriginalAiPersonnelCommandBindings.TryBind(node, out var personnelAction, out var personnelMissing) && personnelAction is not null)
                            return Native(personnelAction);
                        preciseMissing ??= personnelMissing;
                        if (OriginalAiEconomyCommandBindings.TryBind(node, out var economyAction, out var economyMissing) && economyAction is not null)
                            return Native(economyAction);
                        preciseMissing ??= economyMissing;
                        if (OriginalAiTradeInputBindings.TryBind(node, out var tradeInputAction, out var tradeInputMissing) && tradeInputAction is not null)
                            return Native(tradeInputAction);
                        preciseMissing ??= tradeInputMissing;
                        if (node.TypeId == 0x5551802C)
                            preciseMissing = "DoctrinePanelAuto requires its actual category initialization, retained node focus and source reset transaction; the player UI auto session is not this producer";
                        if (OriginalAiVariableBindings.TryBind(node, out var variableAction, out var variableMissing) && variableAction is not null)
                            return Native(variableAction);
                        preciseMissing ??= variableMissing;
                    }
                    if (OriginalAiListBindings.TryBind(node, children, out var listAction, out var listMissing) && listAction is not null)
                        return Native(listAction);
                    preciseMissing ??= listMissing;
                    if (OriginalAiStrategyMemoryBindings.TryBind(node, children, out var memoryAction, out var memoryMissing) && memoryAction is not null)
                        return Native(memoryAction);
                    preciseMissing ??= memoryMissing;
                    if (children.Length == 0)
                    {
                        if (OriginalAiCautionBindings.TryBind(node, out var cautionAction, out var cautionMissing) && cautionAction is not null)
                            return Native(cautionAction);
                        preciseMissing ??= cautionMissing;
                        if (OriginalAiTroopsBindings.TryBind(node, out var troopsAction, out var troopsMissing) && troopsAction is not null)
                            return Native(troopsAction);
                        preciseMissing ??= troopsMissing;
                        if (OriginalAiFightingStrengthBindings.TryBind(node, out var fightingAction, out var fightingMissing) && fightingAction is not null)
                            return Native(fightingAction);
                        preciseMissing ??= fightingMissing;
                        if (OriginalAiDeepInteriorBindings.TryBind(node, out var deepAction, out var deepMissing) && deepAction is not null)
                            return Native(deepAction);
                        preciseMissing ??= deepMissing;
                        if (OriginalAiForceInformationBindings.TryBind(node, out var forceAction, out var forceMissing) && forceAction is not null)
                            return Native(forceAction);
                        preciseMissing ??= forceMissing;
                        if (OriginalAiDistrictPurposeBindings.TryBind(node, out var purposeAction, out var purposeMissing) && purposeAction is not null)
                            return Native(purposeAction);
                        preciseMissing ??= purposeMissing;
                        if (OriginalAiAncestorInputBindings.TryBind(node, out var inputAction, out var inputMissing) && inputAction is not null)
                            return Native(inputAction);
                        preciseMissing ??= inputMissing;
                        if (OriginalAiAncestorCandidateBindings.TryBind(node, out var candidateAction, out _) && candidateAction is not null)
                            return Native(candidateAction);
                        if (OriginalAiCityDutyBindings.TryBind(node, out var dutyAction, out var dutyMissing) && dutyAction is not null)
                            return Native(dutyAction);
                    }
                    if (OriginalAiDomesticBindings.TryBind(node, children, out var action, out var unresolved) && action is not null)
                        return Native(action);
                    return Unsupported(preciseMissing ?? unresolved ?? "native binding or serialized parameter shape");
                }
            }
        }
        if (node.TypeId == 0xBD1BADA3)
        {
            if (args.Length != 1 || node.ChildIndices.Length != 1)
                return Unsupported("static-link child ownership or serialized parameter shape");
            //B2F520 forwards the source child status. Its argument is a link
            //identity, not an actor selector, condition or scheduling policy.
            return Lower(bundle, node.ChildIndices[0], operation, missing, scripts);
        }
        if (node.TypeId == 0xF3836309)
        {
            if (index != 0 || args.Length != 0 || node.ChildIndices.Length != 1)
                return Unsupported("root ownership/child shape");
            int missingBeforeChild = missing.Count;
            var childExpression = Lower(bundle, node.ChildIndices[0], operation, missing, scripts);
            if (missing.Count != missingBeforeChild) return Expression.Default(typeof(OriginalAiNodeStatus));
            var child = Expression.Lambda<Func<OriginalAiOperation, OriginalAiNodeStatus>>(childExpression, operation).Compile();
            var subtree = ControlSubtree(bundle, node.ChildIndices[0]);
            return Native(current => current.ControlRoot(bundle.ResourceKey, node.Index, child, subtree));
        }
        if (node.TypeId == 0x416D418C)
        {
            if (args.Length != 3 || args[0] != 0 || args[1] > 2 || args[2] > 1)
                return Unsupported("non-source child ordering or sequential parameter shape");
            int missingBeforeChildren = missing.Count;
            var childExpressions = node.ChildIndices.Select(child =>
                Lower(bundle, child, operation, missing, scripts)).ToArray();
            if (missing.Count != missingBeforeChildren) return Expression.Default(typeof(OriginalAiNodeStatus));
            var children = childExpressions.Select(child =>
                Expression.Lambda<Func<OriginalAiOperation, OriginalAiNodeStatus>>(child, operation).Compile()).ToImmutableArray();
            var eligible = node.ChildIndices.Select(child => ControlEligible(bundle, child)).ToImmutableArray();
            var subtrees = node.ChildIndices.Select(child => ControlSubtree(bundle, child)).ToImmutableArray();
            return Native(current => current.ControlSequence(bundle.ResourceKey, node.Index,
                (OriginalAiSequentialPolicy)args[1], args[2] != 0, children, eligible, subtrees));
        }
        if (node.TypeId == OriginalAiScriptConditions.DecoratorTypeId)
        {
            var condition = OriginalAiScriptConditions.Read(args, node.Index);
            if (condition.Mode != OriginalAiConditionMode.Guard)
                return Unsupported("condition repeat requires the source update continuation");
            if (node.ChildIndices.IsEmpty) return Expression.Constant(OriginalAiNodeStatus.Failure);
            if (node.ChildIndices.Length != 1) return Unsupported("condition first-child ownership");
            int missingBeforeGuard = missing.Count;
            Expression predicate = OriginalAiScriptConditions.Lower(condition, query =>
            {
                if (query.Record.QueryId is 20 or 21 or 23)
                    return OriginalAiScriptConditions.LowerScalarQuery(query,
                        Expression.Convert(operation, typeof(IOriginalAiConditionScalarValues)));
                if (OriginalAiConditionFamiliesBindings.TryBindCondition(query, out var conditionFamilyPredicate, out _) && conditionFamilyPredicate is not null)
                    return Expression.Invoke(Expression.Constant(conditionFamilyPredicate), operation);
                if (OriginalAiGeneralConditionBindings.TryBindCondition(query, out var generalPredicate, out _) && generalPredicate is not null)
                    return Expression.Invoke(Expression.Constant(generalPredicate), operation);
                if (OriginalAiStrategyConditionBindings.TryBindCondition(query, out var strategyPredicate, out _) && strategyPredicate is not null)
                    return Expression.Invoke(Expression.Constant(strategyPredicate), operation);
                if (OriginalAiForceInformationBindings.TryBindCondition(query, out var forcePredicate, out _) && forcePredicate is not null)
                    return Expression.Invoke(Expression.Constant(forcePredicate), operation);
                if (OriginalAiListBindings.TryBindCondition(query, out var listPredicate, out _) && listPredicate is not null)
                    return Expression.Invoke(Expression.Constant(listPredicate), operation);
                if (OriginalAiAncestorCandidateBindings.TryBindCondition(query, out var ancestorPredicate, out _) && ancestorPredicate is not null)
                    return Expression.Invoke(Expression.Constant(ancestorPredicate), operation);
                if (OriginalAiCityDutyBindings.TryBindCondition(query, out var dutyPredicate, out _) && dutyPredicate is not null)
                    return Expression.Invoke(Expression.Constant(dutyPredicate), operation);
                if (OriginalAiDomesticBindings.TryBindCondition(query, out var boundPredicate, out var unresolved) && boundPredicate is not null)
                    return Expression.Invoke(Expression.Constant(boundPredicate), operation);
                missing.Add(new(bundle.ResourceKey, bundle.Tree.Key, node.Index, node.TypeId,
                    unresolved ?? $"condition callback {query.Record.QueryId}, word {query.Record.WordIndex}")
                {
                    Arguments = query.Record.Parameters,
                    ConditionQueryId = query.Record.QueryId,
                    ConditionWordIndex = query.Record.WordIndex
                });
                // Unsupported bindings prevent compilation of the region.
                return Expression.Constant(false);
            });
            var childExpression = Lower(bundle, node.ChildIndices[0], operation, missing, scripts);
            if (missing.Count != missingBeforeGuard) return Expression.Default(typeof(OriginalAiNodeStatus));
            var test = Expression.Lambda<Func<OriginalAiOperation, bool>>(predicate, operation).Compile();
            var child = Expression.Lambda<Func<OriginalAiOperation, OriginalAiNodeStatus>>(childExpression, operation).Compile();
            var subtree = ControlSubtree(bundle, node.ChildIndices[0]);
            return Native(current => current.ControlGuard(bundle.ResourceKey, node.Index, test, child, subtree));
        }
        return Unsupported("control type/condition binding");
    }

    private static ImmutableHashSet<int> ControlSubtree(OriginalAiBehaviorBundle bundle, int root)
    {
        var result = ImmutableHashSet.CreateBuilder<int>();
        var remaining = new Stack<int>(); remaining.Push(root);
        while (remaining.TryPop(out int index))
        {
            result.Add(index);
            foreach (int child in bundle.Tree.Nodes[index].ChildIndices) remaining.Push(child);
        }
        return result.ToImmutable();
    }

    private static bool ControlEligible(OriginalAiBehaviorBundle bundle, int index)
    {
        var node = bundle.Tree.Nodes[index];
        // All named native factory leaf classes use the source constant-true
        // eligibility callback. The native Loop and ordinary control parents
        // use any-child eligibility, independently of a Guard's predicate.
        if (node.IsNative && node.TypeId != 0x78F9C619) return true;
        return node.ChildIndices.Any(child => ControlEligible(bundle, child));
    }
}
