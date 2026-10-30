using System.Collections.Immutable;
using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>The actual registered policy contexts. Values are typed policy
/// state only; no GameState, borrowed query cache or operation survives a step.</summary>
public sealed class OriginalAiStrategyContextBank
{
    private readonly Dictionary<OriginalAiPolicyActor, Context> contexts = [];
    private readonly OriginalAiSharedListRuntime sharedLists = new();
    public OriginalAiPolicySource Source { get; }
    private readonly OriginalAiPolicyRootBundles? roots;
    public OriginalAiEvaluationRuntime Evaluations { get; }
    public OriginalAiCautionGroupPlanningRuntime CautionGroups { get; } = new();

    public OriginalAiStrategyContextBank(OriginalAiPolicyExecutionState state,
        ImmutableDictionary<int, OriginalAiPolicyActorState>? militaryGroupContexts)
    {
        ArgumentNullException.ThrowIfNull(state);
        Source = state.Source; roots = state.RootBundles;
        Evaluations = new(state.Evaluations);
        if (state.Actors.IsDefault) throw Missing("saved_actor_contexts");
        if (militaryGroupContexts is null) throw Missing("military_group_context_owner");
        sharedLists.ImportSaved(state.Actors.Select(actor => actor.Blackboard)
            .Concat(militaryGroupContexts.Values.Select(actor => actor.Blackboard)));
        foreach (var actor in state.Actors)
            if (actor.Actor.Kind == OriginalAiPolicyActorKind.MilitaryGroup || !contexts.TryAdd(actor.Actor, new(actor, sharedLists)))
                throw Missing("duplicate_or_misowned_saved_actor");
        foreach (var pair in militaryGroupContexts) RefreshMilitaryGroupRegistration(pair.Key, pair.Value);
    }

    /// <summary>Only the real fresh policy constructor calls this. Loading an
    /// older unknown policy owner does not reconstruct it from world rosters.</summary>
    public static OriginalAiStrategyContextBank CreateFresh(OriginalAiPolicySource source)
        => new(new(source, [], []), ImmutableDictionary<int, OriginalAiPolicyActorState>.Empty);

    /// <summary>Actual construction/registration is explicit. Re-registration
    /// reuses the existing context, preserving blackboard aliases and cursors.</summary>
    public void Register(OriginalAiPolicyActor actor, uint bundleKey)
    {
        if (contexts.TryGetValue(actor, out var current))
        {
            if (current.BundleKey != bundleKey) throw Missing("registered_bundle_identity");
            return;
        }
        contexts.Add(actor, new(CreateRegistration(actor, bundleKey), sharedLists, registeredHere: true));
    }

    public static OriginalAiPolicyActorState CreateRegistration(OriginalAiPolicyActor actor, uint bundleKey)
        => new(actor, bundleKey, [], InitialBlackboard(actor.Kind));

    public static OriginalAiMilitaryGroupPrototype CreateMilitaryGroupPrototype(uint bundleKey)
        => new(bundleKey, InitialBlackboard(OriginalAiPolicyActorKind.MilitaryGroup));

    private static OriginalAiBlackboardSnapshot InitialBlackboard(OriginalAiPolicyActorKind kind)
        => new([new(ResourceNameHash.Compute("AgentType"), new OriginalAiIntegerValue((int)kind))], []);

    public bool Contains(OriginalAiPolicyActor actor) => contexts.ContainsKey(actor);
    public void Remove(OriginalAiPolicyActor actor) => contexts.Remove(actor);

    /// <summary>Refresh one actually reached group registration after domain
    /// commands. A real reset/removal cannot leave a stale reused GroupId in
    /// the operation-local bank; unchanged immutable records retain its work.</summary>
    public void RefreshMilitaryGroupRegistration(int group, OriginalAiPolicyActorState? registered)
    {
        var key = new OriginalAiPolicyActor(OriginalAiPolicyActorKind.MilitaryGroup, group);
        if (registered is null) { contexts.Remove(key); return; }
        if (registered.Actor != key) throw Missing("military_group_registration_identity");
        if (contexts.TryGetValue(key, out var context) && ReferenceEquals(context.RegisteredState, registered)) return;
        contexts[key] = new(registered, sharedLists);
    }
    public void ResetNodes(OriginalAiPolicyActor actor)
    {
        if (contexts.TryGetValue(actor, out var context)) context.ResetNodes();
    }
    public void ClearBlackboard(OriginalAiPolicyActor actor)
    {
        if (contexts.TryGetValue(actor, out var context)) context.ClearBlackboard();
    }

    internal bool TryReadDistrictList(int district, uint key, out OriginalAiOperation.CandidateList list)
    {
        if (contexts.TryGetValue(new(OriginalAiPolicyActorKind.District, district), out var context))
            return context.TryReadList(key, out list);
        list = null!; return false;
    }

    public OriginalAiOperation? Open(OriginalAiActor actor, uint bundleKey,
        CommandPlanningContext planning, PlayableRuleSet rules, ArmyCampaignRuleAdapter adapter)
    {
        var key = Identity(actor);
        if (!contexts.TryGetValue(key, out var context)) return null;
        if (context.BundleKey != bundleKey) throw Missing("current_bundle_identity");
        var operation = new OriginalAiOperation(planning, rules, adapter, actor, OriginalAiInitialBindings.Empty);
        operation.UseContextBank(this);
        operation.UseEvaluationRuntime(Evaluations);
        operation.UseCautionGroupPlanningRuntime(CautionGroups);
        context.Begin(operation);
        return operation;
    }

    public void Complete(OriginalAiOperation operation, bool clearBlackboard)
    {
        if (!contexts.TryGetValue(Identity(operation.Actor), out var context)) throw Missing("completed_actor_context");
        if (clearBlackboard) operation.ClearBlackboard();
        context.Complete(operation);
    }

    public OriginalAiPolicyExecutionState Capture() => new(Source,
        contexts.Where(pair => pair.Key.Kind != OriginalAiPolicyActorKind.MilitaryGroup)
            .Select(pair => pair.Value.Capture(pair.Key)).ToImmutableArray(), Evaluations.Capture()) { RootBundles = roots };

    public OriginalAiPolicyExecutionState Capture(OriginalAiPolicyExecutionState actualOwner)
    {
        if (actualOwner.Source != Source) throw Missing("capture_source_identity");
        var actors = actualOwner.Actors.ToDictionary(row => row.Actor);
        foreach (var pair in contexts)
        {
            if (pair.Key.Kind == OriginalAiPolicyActorKind.MilitaryGroup) continue;
            if (actors.TryGetValue(pair.Key, out var actual))
            {
                if (ReferenceEquals(actual, pair.Value.RegisteredState)) actors[pair.Key] = pair.Value.Capture(pair.Key);
            }
            else if (pair.Value.RegisteredHere) actors.Add(pair.Key, pair.Value.Capture(pair.Key));
        }
        return new(Source, actors.Values.ToImmutableArray(), Evaluations.Capture()) { RootBundles = actualOwner.RootBundles };
    }

    /// <summary>Publish only registrations still owned by the real military
    /// group state. A context released/recreated by a command takes precedence
    /// over this operation's old shadow; no retired context is resurrected.</summary>
    public ImmutableDictionary<int, OriginalAiPolicyActorState> CaptureMilitaryGroups(
        ImmutableDictionary<int, OriginalAiPolicyActorState>? actualRegistrations)
    {
        if (actualRegistrations is null) throw Missing("military_group_capture_owner");
        return actualRegistrations.ToImmutableDictionary(pair => pair.Key, pair =>
        {
            var key = new OriginalAiPolicyActor(OriginalAiPolicyActorKind.MilitaryGroup, pair.Key);
            return contexts.TryGetValue(key, out var context) && ReferenceEquals(context.RegisteredState, pair.Value)
                ? context.Capture(key) : pair.Value;
        });
    }

    public static OriginalAiPolicyActor Identity(OriginalAiActor actor) => actor.AgentKind switch
    {
        OriginalAiAgentKind.Force => new(OriginalAiPolicyActorKind.Force, actor.ForceId),
        OriginalAiAgentKind.District => new(OriginalAiPolicyActorKind.District,
            actor.DistrictId ?? throw Missing("District_identity")),
        OriginalAiAgentKind.Troops => new(OriginalAiPolicyActorKind.MilitaryGroup,
            actor.Troops?.GroupId ?? throw Missing("military_group_identity")),
        OriginalAiAgentKind.Army => new(OriginalAiPolicyActorKind.Army,
            actor.ArmyId ?? throw Missing("Army_domain_identity")),
        _ => throw Missing("actor_kind")
    };

    private sealed class Context(OriginalAiPolicyActorState state, OriginalAiSharedListRuntime sharedLists, bool registeredHere = false)
    {
        internal readonly bool RegisteredHere = registeredHere;
        internal readonly OriginalAiPolicyActorState RegisteredState = state;
        internal readonly uint BundleKey = state.BundleKey;
        private ImmutableArray<OriginalAiPolicyControl> controls = state.Controls;
        private OriginalAiBlackboardSnapshot blackboard = sharedLists.Normalize(state.Blackboard);
        private OriginalAiBehaviorRuntime? runtime;

        internal void Begin(OriginalAiOperation operation)
        {
            runtime ??= new OriginalAiBehaviorRuntime(new OriginalAiActorExecution(
                new((int)(operation.Actor.AgentKind ?? throw Missing("actor_kind")), operation.Actor.ForceId, operation.Actor.DistrictId,
                    operation.Actor.Troops?.GroupId, operation.Actor.ArmyId),
                controls.Select(item => new OriginalAiRunningChild(item.BundleKey, item.NodeIndex, item.ChildIndex)).ToImmutableArray()));
            operation.UseBehaviorRuntime(runtime);
            operation.RestoreBlackboard(blackboard, sharedLists);
        }

        internal void Complete(OriginalAiOperation operation)
        {
            blackboard = operation.CaptureBlackboard(sharedLists);
            controls = (runtime ?? throw Missing("current_runtime")).Capture(new(
                (int)(operation.Actor.AgentKind ?? throw Missing("actor_kind")), operation.Actor.ForceId, operation.Actor.DistrictId,
                operation.Actor.Troops?.GroupId, operation.Actor.ArmyId)).RunningChildren
                .Select(item => new OriginalAiPolicyControl(item.BundleKey, item.NodeIndex, item.ChildIndex)).ToImmutableArray();
        }

        internal void ResetNodes()
        {
            controls = [];
            runtime?.Reset();
        }

        internal void ClearBlackboard() => blackboard = OriginalAiBlackboardSnapshot.Empty;

        internal bool TryReadList(uint key, out OriginalAiOperation.CandidateList list)
            => sharedLists.TryReadList(blackboard, key, out list);

        internal OriginalAiPolicyActorState Capture(OriginalAiPolicyActor actor)
            => new(actor, BundleKey, controls, sharedLists.Capture(blackboard));
    }

    private static InvalidDataException Missing(string field) => new("original_ai.strategy_contexts." + field);
}
