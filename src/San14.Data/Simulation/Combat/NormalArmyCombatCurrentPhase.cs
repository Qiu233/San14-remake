using San14.Core;

namespace San14.Data;

public enum NormalArmyCombatStage6Gate
{
    InputsUnresolved, TutorialControlSkipsTactic, NormalWordReachesSnapshotTroops, TacticProducerRequired
}

/// <summary>The actual stage1..5 Army pair receipt. Stage6's threshold is
/// descriptor.wordA from phase generation, not the target's later live troops.
/// This does not contain frame22, an application packet or a combat commit.</summary>
public sealed record NormalArmyCombatPairCurrentDamage(
    NormalArmyCombatPair Pair, BattleRemakeCurrentDamageReceipt Damage,
    NormalArmyCombatStage6Gate Stage6Gate);

/// <summary>Stage5's snapshot gate and the reached finite automatic selector.
/// No effects, tree/frame22, aftermath or combat application are committed.</summary>
public sealed record NormalArmyCombatPairAutomaticSelection(
    NormalArmyCombatPairCurrentDamage Stage5,
    AutomaticTacticCurrentSelection? Selection);

public enum AutomaticTacticPrefixOutcome { CurrentInputsUnresolved, NativeSkip, EffectsTailWaitingCommitted, AwaitingPlayerDecision }
public sealed record AutomaticTacticPrefixResult(AutomaticTacticPrefixOutcome Outcome,
    GameState State, NormalArmyCombatPairAutomaticSelection? Selection,
    PlayableNativeTacticActivationResult? Activation)
{
    public string? MissingOwner { get; init; }
    internal NormalArmyCombatTacticFrame22Receipt? ActualFrame22 { get; init; }
    internal bool? NativeFrame24Activated { get; init; }
    internal AutomaticTacticExecutionContinuation? PendingAutomaticExecution { get; init; }
}

/// <summary>Operation-local descriptor/cache owner for Army-only ordinary
/// combat. It is generated from one reached source capture. Each later pair
/// builds its attributes from fresh owned definitions and live Army quantities,
/// retaining the original complete count14 cache. No batch precomputation of
/// opposite-direction damage or inferred source callbacks is performed.</summary>
public sealed partial class NormalArmyCombatCurrentPhase
{
    private readonly NormalArmyCombatPlan plan;
    private readonly Dictionary<int, NormalArmyCombatDescriptor> descriptors;
    private readonly Dictionary<int, (int Officer, int Slot)> identities;
    private readonly NativeSubdayClock clock;
    private readonly NativeMapSeasonState season;
    private readonly Func<GameState, SourceWorldContext?> captureDefinitions;
    private readonly Func<SourceWorldContext, TacticRemakeCurrentContext> createContext;
    private readonly Func<TacticRemakeCurrentContext, SourceWorldContext,
        BattleRemakeCurrentRegionFactor> createCurrentRegion;
    private readonly Func<SourceWorldContext,
        IReadOnlyDictionary<int, NormalArmyCombatDescriptor>, BattleRemakeCurrentDamage> createDamage;
    private readonly IPlayableNativeTacticActivationTailSource? activationTail;
    private readonly ArmyCampaignRuleAdapter? currentApplication;
    private readonly SourceWorldContext? operationCurrent;
    private readonly NativeArmyCombatCycleSampler? operationSampler;
    private readonly List<CampaignEffectRequest>? operationEffects;

    private NormalArmyCombatCurrentPhase(GameState initial, NormalArmyCombatPlan ownedPlan,
        Dictionary<int, NormalArmyCombatDescriptor> cache,
        Func<GameState, SourceWorldContext?> capture,
        Func<SourceWorldContext, TacticRemakeCurrentContext> contextFactory,
        Func<TacticRemakeCurrentContext, SourceWorldContext,
            BattleRemakeCurrentRegionFactor> regionFactory,
        Func<SourceWorldContext, IReadOnlyDictionary<int, NormalArmyCombatDescriptor>,
            BattleRemakeCurrentDamage> damageFactory,
        IPlayableNativeTacticActivationTailSource? currentActivationTail,
        ArmyCampaignRuleAdapter? currentApplicationSource, SourceWorldContext? borrowedCurrent,
        NativeArmyCombatCycleSampler? currentSampler, List<CampaignEffectRequest>? currentEffects,
        IReadOnlyDictionary<int, (int Officer, int Slot)>? retainedIdentities)
    {
        plan = Copy(ownedPlan); descriptors = cache;
        identities = retainedIdentities is not null ? new(retainedIdentities) : cache.Keys.ToDictionary(id => id,
            id => (initial.Units[id].OfficerId, initial.Units[id].NativeArmyOrigin!.NativeSlotIndex!.Value));
        clock = initial.NativeClock!; season = initial.NativeMapSeason!;
        captureDefinitions = capture; createDamage = damageFactory;
        createContext = contextFactory; createCurrentRegion = regionFactory;
        activationTail = currentActivationTail;
        currentApplication = currentApplicationSource; operationCurrent = borrowedCurrent;
        operationSampler = currentSampler; operationEffects = currentEffects;
    }

    public NormalArmyCombatPlan Plan => Copy(plan);

    internal static NormalArmyCombatCurrentPhase? Capture(GameState state,
        NormalArmyCombatPhaseMoment moment,
        Func<SourceWorldContext, TacticRemakeCurrentContext> createContext,
        Func<GameState, SourceWorldContext?> capture,
        Func<SourceWorldContext, IReadOnlyDictionary<int, NormalArmyCombatDescriptor>,
            BattleRemakeCurrentDamage> damageFactory,
        Func<TacticRemakeCurrentContext, SourceWorldContext,
            BattleRemakeCurrentRegionFactor> regionFactory,
        string language, string executable, string revision,
        IPlayableNativeTacticActivationTailSource? currentActivationTail = null,
        ArmyCampaignRuleAdapter? currentApplicationSource = null, SourceWorldContext? borrowedCurrent = null,
        NormalArmyCombatPlan? currentPlan = null,
        IReadOnlyDictionary<int, NormalArmyCombatDescriptor>? currentDescriptors = null,
        NativeArmyCombatCycleSampler? currentSampler = null, List<CampaignEffectRequest>? currentEffects = null,
        IReadOnlyDictionary<int, (int Officer, int Slot)>? retainedIdentities = null)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(moment);
        if (!NormalArmyCombatSourcePlanner.IsDueAtSourceCalendarPhase(moment)) return null;
        if (state.NativeClock is null || state.NativeMapSeason is null || capture(state) is not { } definitions)
            return null;
        var queries = new NormalArmyCombatCurrentQueries(createContext(definitions));
        var planner = new NormalArmyCombatSourcePlanner(queries, language, executable, revision);
        if ((currentPlan ?? planner.Resolve(state, moment)) is not { } resolved) return null;
        var cache = currentDescriptors is null ? new Dictionary<int, NormalArmyCombatDescriptor>() : new(currentDescriptors);
        if (currentDescriptors is null)
            foreach (UnitState army in state.Units.Values)
            {
                if (queries.ReadDescriptor(state, army) is not { } descriptor) return null;
                cache.Add(army.Id, descriptor with { });
            }
        return new(state, resolved, cache, capture, createContext, regionFactory, damageFactory,
            currentActivationTail, currentApplicationSource, borrowedCurrent, currentSampler, currentEffects, retainedIdentities);
    }

    /// <summary>Read the pair only when it is actually reached. The caller owns
    /// earlier pair application and the complete stage6..8 transactions. Unknown
    /// native inputs reject this receipt without changing state or random stream.</summary>
    public NormalArmyCombatPairCurrentDamage? ResolveStage5(GameState current, int pairIndex)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (pairIndex < 0 || pairIndex >= plan.OrderedPairs.Length)
            throw new ArgumentOutOfRangeException(nameof(pairIndex));
        if (current.Date != clock.AnchorDate || current.NativeClock != clock || current.NativeMapSeason != season ||
            current.ContentHashes.GetValueOrDefault(plan.Moment.SourceScenarioUri) != plan.Moment.SourceScenarioSha256)
            return null;
        var pair = plan.OrderedPairs[pairIndex];
        if (!current.Units.TryGetValue(pair.Attacker.ArmyId, out var attacker) ||
            !current.Units.TryGetValue(pair.Target.ArmyId, out var target) ||
            !identities.TryGetValue(attacker.Id, out var attackerIdentity) || attacker.OfficerId != attackerIdentity.Officer ||
            attacker.NativeArmyOrigin?.NativeSlotIndex != attackerIdentity.Slot ||
            !identities.TryGetValue(target.Id, out var targetIdentity) || target.OfficerId != targetIdentity.Officer ||
            target.NativeArmyOrigin?.NativeSlotIndex != targetIdentity.Slot ||
            captureDefinitions(current) is not { } definitions ||
            createDamage(definitions, descriptors).Resolve(current, attacker, target) is not { } damage)
            return null;
        bool? tutorial = TacticRemakeCurrentBattleControl.Read(current, 0x48);
        var gate = tutorial switch {
            null => NormalArmyCombatStage6Gate.InputsUnresolved,
            true => NormalArmyCombatStage6Gate.TutorialControlSkipsTactic,
            false when damage.NativeFrameWord20 >= pair.Target.TroopsA => NormalArmyCombatStage6Gate.NormalWordReachesSnapshotTroops,
            _ => NormalArmyCombatStage6Gate.TacticProducerRequired
        };
        return new(pair with { Attacker = pair.Attacker with { }, Target = pair.Target with { } }, damage, gate);
    }

    /// <summary>Continue the same phase cache through stage6 selection. Known
    /// tutorial/unsigned snapshot-troop skips do not query player/rate. A reached
    /// automatic selector uses current quantities without rescanning stage5's
    /// captured primary count or replacing descriptor.TroopsA by live troops.</summary>
    public NormalArmyCombatPairAutomaticSelection? ResolveStage6AutomaticSelection(GameState current, int pairIndex)
        => ResolveStage6AutomaticSelection(current, pairIndex, out _);

    private NormalArmyCombatPairAutomaticSelection? ResolveStage6AutomaticSelection(GameState current,
        int pairIndex, out string? missingOwner)
    {
        missingOwner = null;
        var stage5 = ResolveStage5(current, pairIndex);
        if (stage5 is null)
        { missingOwner = "automatic-prefix.stage5_current_damage"; return null; }
        if (stage5.Stage6Gate == NormalArmyCombatStage6Gate.InputsUnresolved)
        { missingOwner = "automatic-prefix.stage6_tutorial_control48"; return null; }
        if (stage5.Stage6Gate != NormalArmyCombatStage6Gate.TacticProducerRequired)
            return new(stage5, null);
        if (captureDefinitions(current) is not { } definitions)
        { missingOwner = "automatic-prefix.current_definitions"; return null; }
        var context = createContext(definitions);
        var selection = context.ResolveAutomaticCurrentSelection(current, stage5.Pair,
            createCurrentRegion(context, definitions), out missingOwner, plan.Moment.WorldThirdDate);
        return selection is null ? null : new(stage5, selection);
    }

    /// <summary>The reached current pair's automatic non-damage prefix only.
    /// It cannot complete normal debit/aftermath, a manager tree or frame24.
    /// Only the actual hour10 factory installs the necessary operation owner.
    /// The caller must apply ordinary damage before advancing the next pair.</summary>
    public AutomaticTacticPrefixResult TryApplyAutomaticTacticPrefix(GameState current, int pairIndex)
    {
        if (activationTail is null) return Unresolved("automatic-prefix.activation_tail_owner");
        var selection = ResolveStage6AutomaticSelection(current, pairIndex, out var missingOwner);
        if (selection is null) return Unresolved(missingOwner ?? "automatic-prefix.selection_inputs");
        if (selection.Selection?.Status == AutomaticTacticSelectionStatus.MultiCandidateScoringRequired)
            return Unresolved("automatic-prefix.multi_candidate_scoring", selection);
        if (selection.Selection?.Status != AutomaticTacticSelectionStatus.Selected)
            return new(AutomaticTacticPrefixOutcome.NativeSkip, current, selection, null);
        if (operationCurrent is not null)
        {
            if (currentApplication is null || operationSampler is null || operationEffects is null ||
                selection.Selection?.SelectedCandidate is not { HexInstructions: { } hexVectors } selected ||
                captureDefinitions(current) is not { } definitions)
                return Unresolved("automatic-prefix.borrowed_activation_owners", selection);
            var target = selection.Stage5.Pair.Target;
            var primary = new NormalCombatParticipant(27, target.NativePoolSlot, target.ArmyId,
                target.CappedMorale8, target.TroopsA, target.NativeHexC, target.NativeForce10, target.AcceptedPrimaryCount14);
            try
            {
                var applied = currentApplication.ApplyBorrowedSelectedAutomatic(current, operationCurrent,
                    createContext(definitions), selection.Stage5.Pair.Attacker.ArmyId, primary,
                    new(selected.TacticSlot, selected.Program, hexVectors), operationSampler,
                    plan.Moment.WorldThirdDate, operationEffects, plan.Moment.CalendarCycleOrdinal);
                if (applied.Continuation is { } pending)
                    return new(AutomaticTacticPrefixOutcome.AwaitingPlayerDecision, applied.State, selection, null)
                    { PendingAutomaticExecution = pending };
                var actor = applied.State.Units[selection.Stage5.Pair.Attacker.ArmyId];
                var activation = new PlayableNativeTacticActivationResult(applied.State, applied.Reports,
                    [new("tactic_used", actor.ForceId, null, actor.OfficerId, selected.TacticSlot,
                        actor.HexId, target.NativeHexC)]);
                return new(AutomaticTacticPrefixOutcome.EffectsTailWaitingCommitted, applied.State, selection, activation)
                { ActualFrame22 = applied.Frame22, NativeFrame24Activated = true };
            }
            catch (InvalidDataException error) { return Unresolved(error.Message, selection); }
        }
        //The existing public activation writer publishes a complete client
        //world. Its deferred-zero callback scope is not implemented yet.
        if (current.Units.Values.Any(unit => unit.Troops == 0))
            return Unresolved("automatic-prefix.standalone_zero_troops_cleanup", selection);
        var result = PlayableTacticRuntime.TryApplySelectedNativeUseAtCurrentTick(current,
            new CurrentSelectedUse(this, pairIndex, activationTail));
        return result is null ? Unresolved("automatic-prefix.standalone_activation", selection)
            : new(AutomaticTacticPrefixOutcome.EffectsTailWaitingCommitted, result.State, selection, result);

        AutomaticTacticPrefixResult Unresolved(string owner, NormalArmyCombatPairAutomaticSelection? reached = null) =>
            new(AutomaticTacticPrefixOutcome.CurrentInputsUnresolved, current, reached, null) { MissingOwner = owner };
    }

    private sealed class CurrentSelectedUse(NormalArmyCombatCurrentPhase phase, int index,
        IPlayableNativeTacticActivationTailSource tail) : IPlayableNativeSelectedTacticUseSource
    {
        public bool HasNativeActivationCommanderTail => tail.HasNativeActivationCommanderTail;
        public PlayableNativeSelectedTacticUse? ResolveSelectedUse(GameState state)
        {
            if (phase.ResolveStage6AutomaticSelection(state, index) is not { Selection:
                    { Status: AutomaticTacticSelectionStatus.Selected, SelectedCandidate: { } selected } } reached ||
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

    private static NormalArmyCombatPlan Copy(NormalArmyCombatPlan source) => source with {
        OrderedPairs = source.OrderedPairs.Select(pair => pair with {
            Attacker = pair.Attacker with { }, Target = pair.Target with { } }).ToArray()
    };
}
