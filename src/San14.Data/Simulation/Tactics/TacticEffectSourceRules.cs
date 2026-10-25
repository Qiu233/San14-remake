using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current usage values after the native gate and cooldown calculation
/// and an explicit conversion to the independent period scheduler. Null usage
/// is unknown. Consumable costs must be independently resolved by the context.</summary>
public sealed record TacticEffectUsageSource(int CooldownPeriods,
    bool? NoConsumableCostsRequired, TacticUseEligibility Eligibility, string Revision)
{
    public NativeTacticWaitingAssignment? NativeActivationWaiting { get; init; }
    public TacticPrimaryGeometrySource? PrimaryGeometry { get; init; }
}

public sealed record TacticPrimaryGeometrySource(PlayableTacticTargetRelation Relation,
    int RangeHexes, string Revision);
/// <summary>The complete ordered Army list produced by one native instruction's
/// target builder. Base absence must be resolved from current targets, never from
/// a program name or omission from Core's unit dictionary.</summary>
public sealed record TacticInstructionTargetsSource(int[] OrderedArmyIds,
    bool NoSelectedBaseTargets, ulong RandomState);

/// <summary>Current original queries required by the implemented Army effects.
/// Implementations read the same owned current definition context as skill queries;
/// no executable initial global is used as a current value by this adapter.</summary>
public interface ITacticEffectSourceContext
{
    string Revision { get; }
    /// <summary>An installed native selector's null result means a reached
    /// current query is unknown, and must not enter the older singleton adapter.</summary>
    bool ProvidesNativeInstructionTargets => false;
    S14TacticEffectProgram? ReadCurrentProgram(GameState state, TacticSourceRow source);
    byte? ReadCurrentTargetFlags(GameState state, TacticSourceRow source);
    TacticEffectUsageSource? ResolveUsage(GameState state, UnitState? caster,
        TacticSourceRow source, S14TacticEffectProgram program);
    TacticMoraleIncreaseSourceInput? ResolveArmyMoraleIncrease(GameState state,
        UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction);
    IReadOnlyList<int>? ReadCurrentAbnormalUpperBounds(GameState state,
        UnitState caster, UnitState target);
    ulong? FinishArmyAbnormalClear(GameState state, UnitState caster, UnitState target,
        TacticSourceRow source, S14TacticEffectInstruction instruction) => null;
    TacticInstructionTargetsSource? ResolveInstructionTargets(GameState state,
        UnitState caster, UnitState primaryTarget, TacticSourceRow source,
        S14TacticEffectInstruction instruction) => null;
    TacticUseEligibility? ResolvePrimaryTargetEligibility(GameState state, UnitState caster,
        UnitState primaryTarget, TacticSourceRow source) => null;
    int? ReadArmyHealingSelectionCapacity(GameState state, UnitState target) => null;
    TacticEffectQueryResult<TacticHealingSourceInput>? ResolveArmyHealing(GameState state,
        UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction) => null;
    TacticEffectQueryResult<TacticAbnormalStrengthSourceInput>? ResolveArmyAbnormalStrength(
        GameState state, UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction) => null;
    TacticEffectQueryResult<TacticAbnormalSourceInput>? ResolveArmyAbnormalStatus(
        GameState state, UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction, int nativeIndex, int requestedStrength) => null;
    ulong? FinishArmyAbnormalAdjustment(GameState state, UnitState caster, UnitState target,
        TacticSourceRow source, S14TacticEffectInstruction instruction,
        int nativeIndex, TacticAbnormalSourceAdjustment adjustment) => null;
    int? ReadCurrentArmyAbnormalUpperBound(GameState state, UnitState target, int nativeIndex) => null;
    TacticEffectQueryResult<TacticCapturedOwnerSource>? ResolveArmyAbnormalCapturedOwner(
        GameState state, UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction) => null;
    ulong? FinishArmyAbnormalStatus(GameState state, UnitState caster, UnitState target,
        TacticSourceRow source, S14TacticEffectInstruction instruction,
        int nativeIndex, TacticAbnormalSourceResult result) => null;
}

/// <summary>Connects complete Army programs0/15/19/20/23 to the Core production
/// provider using explicit current targets, permissions, factors and RNG. One
/// unsupported instruction rejects the entire declaration before queuing.</summary>
public sealed class TacticEffectSourceRules : ITacticRuleSource, IPlayableTacticUseSource
{
    private readonly TacticSourceCatalog _catalog;
    private readonly ITacticEffectSourceContext _context;
    private readonly PlayableLandRoutes _routes;
    private readonly IPlayableNativeSubdayRules? _nativeSubday;

    public TacticEffectSourceRules(TacticSourceCatalog catalog,
        ITacticEffectSourceContext context, PlayableLandRoutes routes,
        IPlayableNativeSubdayRules? nativeSubday = null)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _nativeSubday = nativeSubday;
        if (string.IsNullOrWhiteSpace(context.Revision))
            throw new InvalidDataException("Current original tactic context revision is missing.");
    }

    public PlayableTacticDefinition? Resolve(GameState state, int tacticId) =>
        ResolveDeclaration(state, null, tacticId);

    public PlayableTacticDefinition? ResolveForCaster(GameState state, UnitState caster, int tacticId) =>
        ResolveDeclaration(state, caster, tacticId);

    private PlayableTacticDefinition? ResolveDeclaration(GameState state, UnitState? caster, int tacticId)
    {
        TacticSourceRow? source = Source(state, tacticId);
        if (source is null) return null;
        S14TacticEffectProgram? program = _context.ReadCurrentProgram(state, source);
        if (!Supported(tacticId, program) || _context.ReadCurrentTargetFlags(state, source) is null) return null;
        TacticEffectUsageSource? usage = _context.ResolveUsage(state, caster, source, program!);
        if (!KnownUsage(usage)) return null;
        int radius = program!.Instructions.Max(instruction => (int)instruction.Raw64);
        bool selectedPrimary = program.Instructions.Any(instruction => instruction.Opcode == 23);
        TacticPrimaryGeometrySource? geometry = usage!.PrimaryGeometry;
        if (selectedPrimary && (geometry is null || geometry.RangeHexes < 0 ||
            geometry.Relation != PlayableTacticTargetRelation.Hostile || string.IsNullOrWhiteSpace(geometry.Revision))) return null;
        //23 stores raw native durations whose expiration requires the bound
        //native clock/updater. A period-only cooldown cannot advance them.
        if (selectedPrimary && usage.NativeActivationWaiting is null) return null;
        if (usage!.NativeActivationWaiting is not null &&
            (state.NativeClock is null || caster?.NativeTacticRuntime is null || _nativeSubday is null)) return null;
        return new(tacticId, Revision(source, program, usage!),
            selectedPrimary ? geometry!.Relation : PlayableTacticTargetRelation.Self,
            selectedPrimary ? geometry!.RangeHexes : 0, radius, usage.CooldownPeriods, [])
        { NativeActivationWaiting = usage.NativeActivationWaiting };
    }

    public TacticUseEligibility CanUse(GameState state, UnitState caster,
        PlayableTacticDefinition definition)
    {
        TacticSourceRow? source = Source(state, definition.TacticId);
        if (source is null) return new(null, "tactic.source_revision", "tactic.source_revision");
        S14TacticEffectProgram? program = _context.ReadCurrentProgram(state, source);
        if (!Supported(definition.TacticId, program) || _context.ReadCurrentTargetFlags(state, source) is null)
            return new(null, "tactic.program_unresolved", $"tactic.program.{definition.TacticId}");
        TacticEffectUsageSource? usage = _context.ResolveUsage(state, caster, source, program!);
        if (!KnownUsage(usage)) return new(null, "tactic.usage_unresolved", "tactic.current_usage");
        if (Revision(source, program!, usage!) != definition.ProfileRevision)
            return new(false, "tactic.profile_changed");
        if (usage!.Eligibility.Allowed != true) return usage.Eligibility;
        return usage.Eligibility;
    }

    public bool UsesSourceEffectOrder => true;

    public TacticUseEligibility CanUseTarget(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes)
    {
        TacticUseEligibility usage = CanUse(state, caster, definition);
        if (usage.Allowed != true) return usage;
        TacticSourceRow source = Source(state, definition.TacticId)!;
        if (definition.TargetRelation == PlayableTacticTargetRelation.Self && primaryTarget.Id != caster.Id)
            return new(false, "tactic.target_relation");
        if (definition.TargetRelation != PlayableTacticTargetRelation.Self)
        {
            TacticUseEligibility? primary = _context.ResolvePrimaryTargetEligibility(state, caster, primaryTarget, source);
            if (primary is null) return new(null, "tactic.native_target_unresolved", "tactic.current_primary_target");
            if (primary.Value.Allowed != true) return primary.Value;
        }
        //172C10 dispatches the complete effect chain at173B0E before assigning
        //new waiting bytes at173C92.16D660 observes the former21/22 counters.
        return ResolveUse(state, caster, primaryTarget, definition, routes) is null
            ? new(null, "tactic.effect_inputs_unresolved", "tactic.current_effect_inputs") : usage;
    }

    /// <summary>Convenience union for caster-centered programs. Mixed programs
    /// use ResolveTargetIds with their explicit selected primary target.</summary>
    public int[] ResolveArmyTargetIds(GameState state, UnitState caster,
        PlayableTacticDefinition definition, PlayableLandRoutes routes)
        => ResolveTargetIds(state, caster, caster, definition, routes);

    public int[] ResolveTargetIds(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes)
    {
        if (definition.TargetRelation == PlayableTacticTargetRelation.Self && primaryTarget.Id != caster.Id)
            throw new InvalidDataException("Original caster-centered tactic chose a different primary target.");
        TacticSourceRow source = Source(state, definition.TacticId)
            ?? throw new InvalidDataException("Current original tactic source changed before targeting.");
        S14TacticEffectProgram program = _context.ReadCurrentProgram(state, source)
            ?? throw new InvalidDataException("Current original tactic program is unknown.");
        TacticInstructionTargetsSource[] selections = SelectInstructions(state, caster,
            primaryTarget, source, program, routes)
            ?? throw new InvalidDataException("Current original tactic selectors are unresolved.");
        return selections.SelectMany(selection => selection.OrderedArmyIds).Distinct().Order().ToArray();
    }

    public TacticUseSourceResolution? ResolveUse(GameState state, UnitState caster, UnitState primaryTarget,
        PlayableTacticDefinition definition, PlayableLandRoutes routes)
    {
        TacticSourceRow? source = Source(state, definition.TacticId);
        if (source is null) return null;
        S14TacticEffectProgram? program = _context.ReadCurrentProgram(state, source);
        if (!Supported(definition.TacticId, program)) return null;
        TacticInstructionTargetsSource[]? selections = SelectInstructions(state, caster,
            primaryTarget, source, program!, routes);
        if (selections is null) return null;
        return ResolveInstructionUse(state, caster, source, program!, selections);
    }

    /// <summary>Automatic172C10 consumes the two vectors created before
    /// effects. This entry shares only the effect dispatcher, never manual
    /// primary geometry/usage or a second selection around live centers.</summary>
    public TacticUseSourceResolution? ResolveCapturedInstructionUse(GameState state, UnitState caster,
        int tacticSlot, S14TacticEffectProgram program, TacticInstructionTargetsSource[] selections)
    {
        TacticSourceRow? source = Source(state, tacticSlot);
        if (source is null || !Supported(tacticSlot, program) || selections.Length != 2) return null;
        foreach (var selection in selections)
        {
            if (selection is null || !selection.NoSelectedBaseTargets || selection.OrderedArmyIds is null)
                return null;
            if (selection.OrderedArmyIds.Distinct().Count() != selection.OrderedArmyIds.Length ||
                selection.OrderedArmyIds.Any(id => !state.Units.ContainsKey(id)))
                throw new InvalidDataException("Captured native instruction vector has an invalid Army identity.");
        }
        return ResolveInstructionUse(state, caster, source, program, selections);
    }

    private TacticUseSourceResolution? ResolveInstructionUse(GameState state, UnitState caster,
        TacticSourceRow source, S14TacticEffectProgram program,
        TacticInstructionTargetsSource[] selections)
    {
        var steps = new List<TacticSourceEffectStep>();
        GameState staged = state with { RandomState = selections[^1].RandomState };
        foreach (S14TacticEffectInstruction instruction in program!.Instructions)
            foreach (int targetId in selections[instruction.Index].OrderedArmyIds)
            {
                UnitState target = staged.Units[targetId];
                UnitState acting = staged.Units.GetValueOrDefault(caster.Id) ?? caster;
                TacticTargetResolution? result = ResolveInstruction(staged, acting, target, source, instruction);
                if (result is null) return null;
                steps.Add(new(targetId, result.Effects, result.RandomState)
                { ResourceStateAfter = result.ResourceStateAfter, NativeResultAmount = result.NativeResultAmount });
                foreach (PlayableTacticEffect effect in result.Effects) target = ApplyReceipt(target, effect);
                staged = Stage(staged, target) with { RandomState = result.RandomState,
                    ResourceState = result.ResourceStateAfter ?? staged.ResourceState };
            }
        return new(steps.ToArray(), staged.RandomState);
    }

    public NativeSubdayUnitResolution? AdvanceUnit(GameState state, UnitState unit,
        NativeSubdayMoment moment) => _nativeSubday?.AdvanceUnit(state, unit, moment);

    public TacticTargetResolution ResolveEffects(GameState state, UnitState caster,
        UnitState target, PlayableTacticDefinition definition)
    {
        TacticSourceRow source = Source(state, definition.TacticId)
            ?? throw new InvalidDataException("Current original tactic source changed during settlement.");
        S14TacticEffectProgram program = _context.ReadCurrentProgram(state, source)
            ?? throw new InvalidDataException("Current original tactic program is unknown.");
        if (!Supported(definition.TacticId, program) || !ArmyOnly(state, source) ||
            program.Instructions.Any(instruction => instruction.Opcode == 23))
            throw new InvalidDataException("One original tactic instruction is unsupported.");
        return TryResolveEffects(state, caster, target, source, program)
            ?? throw new InvalidDataException("Current original tactic target input is unresolved.");
    }

    private TacticTargetResolution? TryResolveEffects(GameState state, UnitState caster,
        UnitState target, TacticSourceRow source, S14TacticEffectProgram program)
    {
        var effects = new List<PlayableTacticEffect>();
        UnitState simulatedTarget = target;
        GameState simulatedState = state;
        foreach (S14TacticEffectInstruction instruction in program.Instructions)
        {
            if (!Selected(state, caster, target, instruction, _routes)) continue;
            TacticTargetResolution? result = ResolveInstruction(simulatedState,
                simulatedState.Units.GetValueOrDefault(caster.Id) ?? caster, simulatedTarget, source, instruction);
            if (result is null) return null;
            effects.AddRange(result.Effects);
            foreach (PlayableTacticEffect effect in result.Effects) simulatedTarget = ApplyReceipt(simulatedTarget, effect);
            simulatedState = Stage(simulatedState, simulatedTarget) with { RandomState = result.RandomState,
                ResourceState = result.ResourceStateAfter ?? simulatedState.ResourceState };
        }
        return new(effects.ToArray(), simulatedState.RandomState) { ResourceStateAfter = simulatedState.ResourceState };
    }

    //The automatic Hex walker already owns selection and the real source
    //program. Share this dispatcher without constructing a fake primary Army
    //or invoking manual geometry, whole-world detachment or a second selector.
    internal TacticTargetResolution? ResolveCapturedInstructionTarget(GameState state,
        UnitState caster, UnitState target, TacticSourceRow source,
        S14TacticEffectInstruction instruction,
        TacticEffectQueryResult<TacticAbnormalStrengthSourceInput>? qualifiedAbnormalStrength = null) =>
        ResolveInstruction(state, caster, target, source, instruction, qualifiedAbnormalStrength);

    //1782F0 reads/writes only its actual Army recipient. The source actor is
    //not an operand; Base actors supply the same target-only marker callback.
    internal TacticTargetResolution? ResolveCapturedArmyClearTarget(GameState state, UnitState target,
        Func<GameState, UnitState, ulong?> finish, Func<IReadOnlyList<int>?>? readLegacyBounds = null)
    {
        UnitState current = target;
        GameState staged = state;
        NativeTacticAbnormalAssignment? clear = null;
        if (current.NativeTacticRuntime is { } native)
        {
            //1780CF resets the former positive25 owner before its three
            //fresh cap reads, then writes each counter before the next read.
            ArmyOverrideTargetState? ownerWrite = native.Abnormal25 > 0 ? ArmyOverrideTargetState.Cleared : null;
            native = native with { ProvocationOwner = native.Abnormal25 > 0 ? null : native.ProvocationOwner };
            current = ArmyOverrideTargetWrites.Apply(current with { NativeTacticRuntime = native }, ownerWrite);
            staged = Stage(staged, current);
            for (int index = 0; index < 3; index++)
            {
                int? bound = _context.ReadCurrentArmyAbnormalUpperBound(staged, current, index);
                if (bound is null) return null;
                byte value = bound < 0 ? unchecked((byte)bound.Value) : (byte)0;
                native = index switch { 0 => native with { Abnormal23 = value },
                    1 => native with { Abnormal24 = value }, _ => native with { Abnormal25 = value } };
                current = current with { NativeTacticRuntime = native };
                staged = Stage(staged, current);
            }
            clear = new(native.Abnormal23, native.Abnormal24, native.Abnormal25, native.ProvocationOwner)
                { OverrideTarget = ownerWrite };
        }
        else
        {
            IReadOnlyList<int>? bounds = readLegacyBounds?.Invoke();
            if (bounds is not { Count: 3 } || bounds.Any(bound => bound < 0)) return null;
        }
        var effect = new PlayableTacticEffect(PlayableTacticEffectKind.ClearAbnormalStatuses, 0)
            { NativeAbnormalAfter = clear };
        current = ApplyReceipt(current, effect);
        staged = Stage(staged, current);
        //The native walker rereads this target-list node's Hex identity
        //and appends its result marker at179F20. An omitted current marker
        //receipt remains unknown; the pure native append has no RNG draw.
        ulong? random = finish(staged, current);
        return random is null ? null : new([effect], random.Value)
            { ResourceStateAfter = ArmyOverrideTargetSourceAdapter.ReconcileOwner(staged, current).ResourceState };
    }

    private TacticTargetResolution? ResolveInstruction(GameState state, UnitState caster, UnitState target,
        TacticSourceRow source, S14TacticEffectInstruction instruction,
        TacticEffectQueryResult<TacticAbnormalStrengthSourceInput>? qualifiedAbnormalStrength = null)
    {
        if (instruction.Opcode == 15)
        {
            if ((target.NativeArmyOrigin is not null || target.MoraleState.CurrentValue is not null) && ArmyMoraleSourceAdapter.Read(target) is null) return null;
            TacticMoraleIncreaseSourceInput? input = _context.ResolveArmyMoraleIncrease(
                state, caster, target, source, instruction);
            if (input is null) return null;
            int current = ArmyMoraleSourceAdapter.Read(target) ?? target.Morale;
            if (input.Scale.EffectValue != instruction.EffectValue || input.CurrentMorale != current)
                throw new InvalidDataException("Current original morale input differs from its target or program.");
            int after = TacticEffectSourceMath.ArmyMoraleAfter(input);
            // The shared aftermath destroys zero-morale Armies; its source
            // counterpart must be bound before that native branch can execute.
            if (target.MoraleState.CurrentValue is not null)
            {
                if (after is < 1 or > byte.MaxValue) return null;
                return new([new(PlayableTacticEffectKind.NativeMoraleChange, checked(after - current))
                    { CurrentMoraleAfter = after }], state.RandomState);
            }
            if (after is < 1 or > 100) return null;
            return new([new(PlayableTacticEffectKind.MoraleChange, checked(after - target.Morale))], state.RandomState);
        }
        if (instruction.Opcode == 19)
            return ResolveCapturedArmyClearTarget(state, target,
                (staged, current) => _context.FinishArmyAbnormalClear(staged, caster, current, source, instruction),
                () => _context.ReadCurrentAbnormalUpperBounds(state, caster, target));
        if (instruction.Opcode == 20)
        {
            if (target.WoundedTroops is null) return null;
            TacticEffectQueryResult<TacticHealingSourceInput>? query =
                _context.ResolveArmyHealing(state, caster, target, source, instruction);
            if (query is null) return null;
            TacticHealingSourceInput input = query.Value;
            if (input.Scale.EffectValue != instruction.EffectValue || input.CurrentTroops != target.Troops ||
                input.WoundedTroops != target.WoundedTroops)
                throw new InvalidDataException("Current original healing input differs from its target or program.");
            // The inspected all-Army route's wounded writer uses literal20000.
            // A preceding Base branch can change its carried bound and is not
            // part of this complete Army profile.
            if (input.CurrentWoundedWriteMaximum != 20000) return null;
            TacticHealingSourceResult after = TacticEffectSourceMath.ArmyHealing(input);
            if (after.TroopsAfter < 1) return null;
            return new([new(PlayableTacticEffectKind.ArmyHealing, checked(after.TroopsAfter - target.Troops))
                { HealingResult = new(after.TroopsAfter, after.WoundedAfter) }], query.RandomState)
                { NativeResultAmount = after.RequestedRecovery };
        }
        if (instruction.Opcode == 23)
        {
            if (target.NativeTacticRuntime is not { } native) return null;
            TacticEffectQueryResult<TacticAbnormalStrengthSourceInput>? query =
                qualifiedAbnormalStrength ?? _context.ResolveArmyAbnormalStrength(state, caster, target, source, instruction);
            if (query is null) return null;
            if (query.Value.Scale.EffectValue != instruction.EffectValue)
                throw new InvalidDataException("Current original abnormal strength differs from its program.");
            if (!query.Value.NativeEffectGateAllowed) return new([], query.RandomState);
            int strength = TacticEffectSourceMath.Opcode23RequestedStrength(query.Value.Scale,
                query.Value.ResolvedSpecialPercent);
            GameState staged = state with { RandomState = query.RandomState };
            UnitState current = target;
            var effects = new List<PlayableTacticEffect>();
            for (int index = 0; index < 3; index++)
            {
                TacticEffectQueryResult<TacticAbnormalSourceInput>? statusQuery =
                    _context.ResolveArmyAbnormalStatus(staged, caster, current, source, instruction, index, strength);
                if (statusQuery is null) return null;
                TacticAbnormalSourceInput input = statusQuery.Value;
                if (input.NativeIndex != index || input.RequestedStrength != strength ||
                    input.CurrentCounter != Counter(current.NativeTacticRuntime!, index))
                    throw new InvalidDataException("Current original abnormal input differs from its target or instruction.");
                staged = staged with { RandomState = statusQuery.RandomState };
                TacticAbnormalSourceAdjustment? adjustment = TacticEffectSourceAbnormal.ResolveAdjustment(input);
                if (adjustment is null) return null;
                //2EEC50 returns immediately for an invalid current receiver;
                //none of its notification/cap/owner callbacks is reached.
                if (!input.NativeArmyValid) continue;
                if (adjustment.CounterWritten)
                {
                    if (adjustment.HasTraitAdjustment)
                    {
                        ulong? afterAdjustment = _context.FinishArmyAbnormalAdjustment(staged, caster, current,
                            source, instruction, index, adjustment);
                        if (afterAdjustment is null) return null;
                        staged = staged with { RandomState = afterAdjustment.Value };
                    }
                    int? bound = _context.ReadCurrentArmyAbnormalUpperBound(staged, current, index);
                    if (bound is null) return null;
                    // First stage only the byte write. Positive Provoked then
                    // reaches16E880 on the captured source Hex, after this write.
                    input = input with { CurrentUpperBound = bound, CapturedOwner = new(false, null) };
                }
                TacticAbnormalSourceResult? result = TacticEffectSourceAbnormal.Resolve(input,
                    current.NativeTacticRuntime!.ProvocationOwner);
                if (result is null) return null;
                NativeArmyTacticRuntime before = current.NativeTacticRuntime!;
                NativeTacticAbnormalAssignment receipt = new(index == 0 ? result.CounterAfter : before.Abnormal23,
                    index == 1 ? result.CounterAfter : before.Abnormal24,
                    index == 2 ? result.CounterAfter : before.Abnormal25, result.OwnerAfter);
                PlayableTacticEffect effect = new(PlayableTacticEffectKind.NativeAbnormalChange,
                    result.CounterWritten ? result.CounterAfter - input.CurrentCounter : 0)
                    { NativeAbnormalAfter = receipt };
                current = ApplyReceipt(current, effect);
                staged = Stage(staged, current);
                if (index == 2 && result.CounterWritten && result.AdjustedStrength > 0)
                {
                    TacticEffectQueryResult<TacticCapturedOwnerSource>? capture =
                        _context.ResolveArmyAbnormalCapturedOwner(staged, caster, current, source, instruction);
                    if (capture is null) return null;
                    if (capture.Value.HasEntity && capture.Value.OverrideTarget is not { Kind: not null, Destination: not null }) return null;
                    result = TacticEffectSourceAbnormal.Resolve(input with { CapturedOwner = capture.Value },
                        before.ProvocationOwner)!;
                    receipt = receipt with { ProvocationOwner = result.OwnerAfter,
                        OverrideTarget = capture.Value.HasEntity ? capture.Value.OverrideTarget : null };
                    effect = effect with { NativeAbnormalAfter = receipt };
                    current = ApplyReceipt(current, effect);
                    staged = Stage(staged, current) with { RandomState = capture.RandomState };
                    if (capture.Value.SourceResidue is { } residue)
                        staged = staged with { ResourceState = CampaignSourceKnowledge.From(staged.ResourceState).WithArmyOverride(residue) };
                }
                effects.Add(effect);
                ulong? random = _context.FinishArmyAbnormalStatus(staged, caster, current, source, instruction, index, result);
                if (random is null) return null;
                staged = staged with { RandomState = random.Value };
            }
            return new(effects.ToArray(), staged.RandomState)
                { ResourceStateAfter = ArmyOverrideTargetSourceAdapter.ReconcileOwner(staged, current).ResourceState };
        }
        return instruction.Opcode == 0 ? new([], state.RandomState) : null;
    }

    private TacticInstructionTargetsSource[]? SelectInstructions(GameState state, UnitState caster,
        UnitState primary, TacticSourceRow source, S14TacticEffectProgram program, PlayableLandRoutes routes)
    {
        routes.ValidateWorld(state);
        if (!Supported(source.Slot, program) || _context.ReadCurrentTargetFlags(state, source) is not byte flags) return null;
        var selections = new TacticInstructionTargetsSource[2];
        GameState staged = state;
        foreach (S14TacticEffectInstruction instruction in program.Instructions)
        {
            TacticInstructionTargetsSource? selection = instruction.Opcode == 0
                ? new([], true, staged.RandomState)
                : _context.ResolveInstructionTargets(staged, caster, primary, source, instruction);
            if (selection is null && !_context.ProvidesNativeInstructionTargets &&
                (flags & 1) == 0 && instruction.Opcode is 15 or 19)
            {
                int[] ids = staged.Units.Values.Where(target => target.ForceId == caster.ForceId &&
                    Selected(staged, caster, target, instruction, routes)).Select(target => target.Id).ToArray();
                // A singleton has no unresolved native vector order. Multiple
                // candidates require the actual current source selector vector.
                if (ids.Length <= 1) selection = new(ids, true, staged.RandomState);
            }
            if (selection is null || !selection.NoSelectedBaseTargets) return null;
            if (selection.OrderedArmyIds is null || selection.OrderedArmyIds.Distinct().Count() != selection.OrderedArmyIds.Length)
                throw new InvalidDataException("Original tactic selector returned an invalid native target vector.");
            foreach (int id in selection.OrderedArmyIds)
            {
                if (!staged.Units.TryGetValue(id, out UnitState? target) || target.Id != id || target.Troops <= 0 ||
                    target.Morale <= 0 || target.HexId < 0 || target.HexId >= checked(state.MapWidth * state.MapHeight))
                    throw new InvalidDataException("Original tactic selected an unresolved Army identity.");
                UnitState center = instruction.Opcode == 23 ? primary : caster;
                if (!routes.WithinRadius(center.HexId, target.HexId, instruction.Raw64) ||
                    (instruction.Opcode is 15 or 19 or 20 && target.ForceId != caster.ForceId) ||
                    (instruction.Opcode == 23 && target.ForceId == caster.ForceId) ||
                    (instruction.Opcode == 19 && !Abnormal(target)))
                    throw new InvalidDataException("Original tactic selected an Army outside its reached source branch.");
                if (instruction.Opcode == 20)
                {
                    int? capacity = _context.ReadArmyHealingSelectionCapacity(staged, target);
                    if (target.WoundedTroops is null || capacity is null) return null;
                    if (target.WoundedTroops <= 0 || target.Troops >= capacity)
                        throw new InvalidDataException("Original healing selector included an ineligible Army.");
                }
            }
            selections[instruction.Index] = selection;
            staged = staged with { RandomState = selection.RandomState };
        }
        return selections;
    }

    internal static UnitState ApplyReceipt(UnitState target, PlayableTacticEffect effect)
    {
        if (effect.Kind == PlayableTacticEffectKind.MoraleChange)
            target = target with { MoraleState = target.MoraleState with { ProvisionalValue = checked(target.Morale + effect.Amount) } };
        if (effect.CurrentMoraleAfter is int morale)
            target = target with { MoraleState = target.MoraleState with { CurrentValue = morale } };
        if (effect.HealingResult is { } healing)
            target = target with { Troops = healing.TroopsAfter, WoundedTroops = healing.WoundedAfter };
        if (effect.NativeAbnormalAfter is { } after && target.NativeTacticRuntime is { } native)
            target = ArmyOverrideTargetWrites.Apply(target with { NativeTacticRuntime = native with
                { Abnormal23 = after.Abnormal23, Abnormal24 = after.Abnormal24, Abnormal25 = after.Abnormal25,
                    ProvocationOwner = after.ProvocationOwner } }, after.OverrideTarget);
        if (effect.Kind == PlayableTacticEffectKind.ClearAbnormalStatuses && target.TacticRuntime is { } runtime)
            target = target with { TacticRuntime = runtime with { Statuses = runtime.Statuses.Where(status =>
                status.Kind is not (PlayableTacticEffectKind.Confused or PlayableTacticEffectKind.Immobilized or
                    PlayableTacticEffectKind.Provoked)).ToArray() } };
        return target;
    }

    private static byte Counter(NativeArmyTacticRuntime runtime, int index) => index switch
    { 0 => runtime.Abnormal23, 1 => runtime.Abnormal24, _ => runtime.Abnormal25 };
    private static GameState Stage(GameState state, UnitState unit) => state with
    { Units = new(state.Units) { [unit.Id] = unit } };

    private TacticSourceRow? Source(GameState state, int tacticId) =>
        tacticId > 0 && tacticId < _catalog.Rows.Count &&
        state.ContentHashes.TryGetValue(_catalog.Source.ResourceUri, out string? hash) &&
        hash.Equals(_catalog.Source.Sha256, StringComparison.OrdinalIgnoreCase)
            ? _catalog.Rows[tacticId] : null;

    private static bool KnownUsage(TacticEffectUsageSource? usage) =>
        usage is { CooldownPeriods: >= 0, NoConsumableCostsRequired: true } &&
        !string.IsNullOrWhiteSpace(usage.Revision);

    private bool ArmyOnly(GameState state, TacticSourceRow source) =>
        _context.ReadCurrentTargetFlags(state, source) is byte flags && (flags & 1) == 0;

    private static bool Supported(int tacticId, S14TacticEffectProgram? program) =>
        program is not null && program.TacticSlot == tacticId &&
        program.Instructions is { Count: 2 } &&
        program.Instructions.Select(instruction => instruction.Index).SequenceEqual(new[] { 0, 1 }) &&
        program.Instructions.Any(instruction => instruction.Opcode != 0) &&
        program.Instructions.All(instruction => instruction.Opcode is 0 or 15 or 19 or 20 or 23);

    private string Revision(TacticSourceRow source, S14TacticEffectProgram program,
        TacticEffectUsageSource usage)
    {
        string native = usage.NativeActivationWaiting is { } waiting
            ? $":native-wait{waiting.Waiting20},{waiting.Waiting21},{waiting.Waiting22}:{waiting.Revision}" : "";
        string geometry = usage.PrimaryGeometry is { } primary
            ? $":primary{primary.Relation},{primary.RangeHexes}:{primary.Revision}" : "";
        return $"original-ce-tactic:{_context.Revision}:{source.Source.Sha256}:{source.DecodedSha256}:{program.Sha256}:{usage.Revision}:cooldown{usage.CooldownPeriods}{native}{geometry}";
    }

    private static bool Selected(GameState state, UnitState caster, UnitState target,
        S14TacticEffectInstruction instruction, PlayableLandRoutes routes) =>
        instruction.Opcode != 0 && routes.WithinRadius(caster.HexId, target.HexId,
            Math.Min(instruction.Raw64, checked(state.MapWidth * state.MapHeight) - 1)) &&
        (instruction.Opcode != 19 || Abnormal(target));

    private static bool Abnormal(UnitState target) => target.NativeTacticRuntime is { } native
        ? native.Abnormal23 > 0 || native.Abnormal24 > 0 || native.Abnormal25 > 0
        : target.TacticRuntime?.Statuses.Any(status => status.Kind is
            PlayableTacticEffectKind.Immobilized or PlayableTacticEffectKind.Confused or
            PlayableTacticEffectKind.Provoked) == true;
}
