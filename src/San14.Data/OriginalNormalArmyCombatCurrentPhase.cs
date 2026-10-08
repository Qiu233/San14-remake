using San14.Core;

namespace San14.Data;

public enum OriginalNormalArmyCombatStage6Gate
{
    InputsUnresolved, TutorialControlSkipsTactic, NormalWordReachesSnapshotTroops, TacticProducerRequired
}

/// <summary>The actual stage1..5 Army pair receipt. Stage6's threshold is
/// descriptor.wordA from phase generation, not the target's later live troops.
/// This does not contain frame22, an application packet or a combat commit.</summary>
public sealed record OriginalNormalArmyCombatPairCurrentDamage(
    OriginalNormalArmyCombatPair Pair, OriginalBattleRemakeCurrentDamageReceipt Damage,
    OriginalNormalArmyCombatStage6Gate Stage6Gate);

/// <summary>Stage5's snapshot gate and the reached finite automatic selector.
/// No effects, tree/frame22, aftermath or combat application are committed.</summary>
public sealed record OriginalNormalArmyCombatPairAutomaticSelection(
    OriginalNormalArmyCombatPairCurrentDamage Stage5,
    OriginalAutomaticTacticCurrentSelection? Selection);

public enum OriginalAutomaticTacticPrefixOutcome { CurrentInputsUnresolved, NativeSkip, EffectsTailWaitingCommitted }
public sealed record OriginalAutomaticTacticPrefixResult(OriginalAutomaticTacticPrefixOutcome Outcome,
    GameState State, OriginalNormalArmyCombatPairAutomaticSelection? Selection,
    PlayableNativeTacticActivationResult? Activation);

/// <summary>Operation-local descriptor/cache owner for Army-only ordinary
/// combat. It is generated from one reached source capture. Each later pair
/// builds its attributes from fresh owned definitions and live Army quantities,
/// retaining the original complete count14 cache. No batch precomputation of
/// opposite-direction damage or inferred source callbacks is performed.</summary>
public sealed partial class OriginalNormalArmyCombatCurrentPhase
{
    private readonly OriginalNormalArmyCombatPlan plan;
    private readonly Dictionary<int, OriginalNormalArmyCombatDescriptor> descriptors;
    private readonly Dictionary<int, (int Officer, int Slot)> identities;
    private readonly NativeSubdayClock clock;
    private readonly NativeMapSeasonState season;
    private readonly Func<GameState, OriginalEventInitializationContext?> captureDefinitions;
    private readonly Func<OriginalEventInitializationContext, OriginalTacticRemakeCurrentContext> createContext;
    private readonly Func<OriginalTacticRemakeCurrentContext, OriginalEventInitializationContext,
        OriginalBattleRemakeCurrentRegionFactor> createCurrentRegion;
    private readonly Func<OriginalEventInitializationContext,
        IReadOnlyDictionary<int, OriginalNormalArmyCombatDescriptor>, OriginalBattleRemakeCurrentDamage> createDamage;
    private readonly IPlayableNativeTacticActivationTailSource? activationTail;
    private readonly OriginalOwnedArmyCampaignRules? currentApplication;

    private OriginalNormalArmyCombatCurrentPhase(GameState initial, OriginalNormalArmyCombatPlan ownedPlan,
        Dictionary<int, OriginalNormalArmyCombatDescriptor> cache,
        Func<GameState, OriginalEventInitializationContext?> capture,
        Func<OriginalEventInitializationContext, OriginalTacticRemakeCurrentContext> contextFactory,
        Func<OriginalTacticRemakeCurrentContext, OriginalEventInitializationContext,
            OriginalBattleRemakeCurrentRegionFactor> regionFactory,
        Func<OriginalEventInitializationContext, IReadOnlyDictionary<int, OriginalNormalArmyCombatDescriptor>,
            OriginalBattleRemakeCurrentDamage> damageFactory,
        IPlayableNativeTacticActivationTailSource? currentActivationTail,
        OriginalOwnedArmyCampaignRules? currentApplicationSource)
    {
        plan = Copy(ownedPlan); descriptors = cache;
        identities = initial.Units.ToDictionary(row => row.Key,
            row => (row.Value.OfficerId, row.Value.NativeArmyOrigin!.NativeSlotIndex!.Value));
        clock = initial.NativeClock!; season = initial.NativeMapSeason!;
        captureDefinitions = capture; createDamage = damageFactory;
        createContext = contextFactory; createCurrentRegion = regionFactory;
        activationTail = currentActivationTail;
        currentApplication = currentApplicationSource;
    }

    public OriginalNormalArmyCombatPlan Plan => Copy(plan);

    internal static OriginalNormalArmyCombatCurrentPhase? Capture(GameState state,
        OriginalNormalArmyCombatPhaseMoment moment,
        Func<OriginalEventInitializationContext, OriginalTacticRemakeCurrentContext> createContext,
        Func<GameState, OriginalEventInitializationContext?> capture,
        Func<OriginalEventInitializationContext, IReadOnlyDictionary<int, OriginalNormalArmyCombatDescriptor>,
            OriginalBattleRemakeCurrentDamage> damageFactory,
        Func<OriginalTacticRemakeCurrentContext, OriginalEventInitializationContext,
            OriginalBattleRemakeCurrentRegionFactor> regionFactory,
        string language, string executable, string revision,
        IPlayableNativeTacticActivationTailSource? currentActivationTail = null,
        OriginalOwnedArmyCampaignRules? currentApplicationSource = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(moment);
        if (!OriginalNormalArmyCombatSourcePlanner.IsDueAtSourceCalendarPhase(moment)) return null;
        if (state.NativeClock is null || state.NativeMapSeason is null || capture(state) is not { } definitions)
            return null;
        var queries = new OriginalNormalArmyCombatCurrentQueries(createContext(definitions));
        var planner = new OriginalNormalArmyCombatSourcePlanner(queries, language, executable, revision);
        if (planner.Resolve(state, moment) is not { } resolved) return null;
        var cache = new Dictionary<int, OriginalNormalArmyCombatDescriptor>();
        foreach (UnitState army in state.Units.Values)
        {
            if (queries.ReadDescriptor(state, army) is not { } descriptor) return null;
            cache.Add(army.Id, descriptor with { });
        }
        return new(state, resolved, cache, capture, createContext, regionFactory, damageFactory,
            currentActivationTail, currentApplicationSource);
    }

    /// <summary>Read the pair only when it is actually reached. The caller owns
    /// earlier pair application and the complete stage6..8 transactions. Unknown
    /// native inputs reject this receipt without changing state or random stream.</summary>
    public OriginalNormalArmyCombatPairCurrentDamage? ResolveStage5(GameState current, int pairIndex)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (pairIndex < 0 || pairIndex >= plan.OrderedPairs.Length)
            throw new ArgumentOutOfRangeException(nameof(pairIndex));
        if (current.Date != clock.AnchorDate || current.NativeClock != clock || current.NativeMapSeason != season ||
            current.ContentHashes.GetValueOrDefault(plan.Moment.SourceScenarioUri) != plan.Moment.SourceScenarioSha256)
            return null;
        foreach (UnitState army in current.Units.Values)
            if (!identities.TryGetValue(army.Id, out var identity) || army.OfficerId != identity.Officer ||
                army.NativeArmyOrigin?.NativeSlotIndex != identity.Slot) return null;
        var pair = plan.OrderedPairs[pairIndex];
        if (!current.Units.TryGetValue(pair.Attacker.ArmyId, out var attacker) ||
            !current.Units.TryGetValue(pair.Target.ArmyId, out var target) ||
            captureDefinitions(current) is not { } definitions ||
            createDamage(definitions, descriptors).Resolve(current, attacker, target) is not { } damage)
            return null;
        bool? tutorial = OriginalTacticRemakeCurrentBattleControl.Read(current, 0x48);
        var gate = tutorial switch {
            null => OriginalNormalArmyCombatStage6Gate.InputsUnresolved,
            true => OriginalNormalArmyCombatStage6Gate.TutorialControlSkipsTactic,
            false when damage.NativeFrameWord20 >= pair.Target.TroopsA => OriginalNormalArmyCombatStage6Gate.NormalWordReachesSnapshotTroops,
            _ => OriginalNormalArmyCombatStage6Gate.TacticProducerRequired
        };
        return new(pair with { Attacker = pair.Attacker with { }, Target = pair.Target with { } }, damage, gate);
    }

    /// <summary>Continue the same phase cache through stage6 selection. Known
    /// tutorial/unsigned snapshot-troop skips do not query player/rate. A reached
    /// automatic selector uses current quantities without rescanning stage5's
    /// captured primary count or replacing descriptor.TroopsA by live troops.</summary>
    public OriginalNormalArmyCombatPairAutomaticSelection? ResolveStage6AutomaticSelection(GameState current, int pairIndex)
    {
        var stage5 = ResolveStage5(current, pairIndex);
        if (stage5 is null || stage5.Stage6Gate == OriginalNormalArmyCombatStage6Gate.InputsUnresolved) return null;
        if (stage5.Stage6Gate != OriginalNormalArmyCombatStage6Gate.TacticProducerRequired)
            return new(stage5, null);
        if (captureDefinitions(current) is not { } definitions) return null;
        var context = createContext(definitions);
        var selection = context.ResolveAutomaticCurrentSelection(current, stage5.Pair,
            createCurrentRegion(context, definitions));
        return selection is null ? null : new(stage5, selection);
    }

    /// <summary>The reached current pair's automatic non-damage prefix only.
    /// It cannot complete normal debit/aftermath, a manager tree or frame24.
    /// Only the actual hour10 factory installs the necessary operation owner.
    /// The caller must apply ordinary damage before advancing the next pair.</summary>
    public OriginalAutomaticTacticPrefixResult TryApplyAutomaticTacticPrefix(GameState current, int pairIndex)
    {
        if (activationTail is null) return Unresolved();
        var selection = ResolveStage6AutomaticSelection(current, pairIndex);
        if (selection is null || selection.Selection?.Status == OriginalAutomaticTacticSelectionStatus.MultiCandidateScoringRequired)
            return Unresolved();
        if (selection.Selection?.Status != OriginalAutomaticTacticSelectionStatus.Selected)
            return new(OriginalAutomaticTacticPrefixOutcome.NativeSkip, current, selection, null);
        //The existing public activation writer publishes a complete client
        //world. Its deferred-zero callback scope is not implemented yet.
        if (current.Units.Values.Any(unit => unit.Troops == 0)) return Unresolved();
        var result = PlayableTacticRuntime.TryApplySelectedNativeUseAtCurrentTick(current,
            new CurrentSelectedUse(this, pairIndex, activationTail));
        return result is null ? Unresolved() : new(OriginalAutomaticTacticPrefixOutcome.EffectsTailWaitingCommitted,
            result.State, selection, result);

        OriginalAutomaticTacticPrefixResult Unresolved() =>
            new(OriginalAutomaticTacticPrefixOutcome.CurrentInputsUnresolved, current, null, null);
    }

    private sealed class CurrentSelectedUse(OriginalNormalArmyCombatCurrentPhase phase, int index,
        IPlayableNativeTacticActivationTailSource tail) : IPlayableNativeSelectedTacticUseSource
    {
        public bool HasNativeActivationCommanderTail => tail.HasNativeActivationCommanderTail;
        public PlayableNativeSelectedTacticUse? ResolveSelectedUse(GameState state)
        {
            if (phase.ResolveStage6AutomaticSelection(state, index) is not { Selection:
                    { Status: OriginalAutomaticTacticSelectionStatus.Selected, SelectedCandidate: { } selected } } reached ||
                phase.captureDefinitions(state) is not { } definitions) return null;
            var context = phase.createContext(definitions);
            UnitState actor = state.Units[reached.Stage5.Pair.Attacker.ArmyId];
            if (context.ResolveAutomaticActivationWaiting(state, actor, selected) is not { } waiting ||
                context.ResolveAutomaticCapturedEffects(state, actor, selected) is not { } effects) return null;
            var profile = new PlayableTacticDefinition(selected.TacticSlot, waiting.Revision,
                PlayableTacticTargetRelation.Hostile, 0, selected.Program.Instructions.Max(item => item.Raw64), 0, [])
                { NativeActivationWaiting = waiting };
            return new(actor.Id, reached.Stage5.Pair.Target.ArmyId, profile, effects);
        }
        public OfficerState? ResolveNativeActivationCommanderTail(GameState state, UnitState actor,
            PlayableTacticDefinition definition) => tail.ResolveNativeActivationCommanderTail(state, actor, definition);
    }

    private static OriginalNormalArmyCombatPlan Copy(OriginalNormalArmyCombatPlan source) => source with {
        OrderedPairs = source.OrderedPairs.Select(pair => pair with {
            Attacker = pair.Attacker with { }, Target = pair.Target with { } }).ToArray()
    };
}
