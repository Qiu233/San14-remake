using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Reads source programs from the explicitly constructed and loaded
/// shared original definition context. Usage conversion and final morale queries
/// are independent explicit callbacks; omitted queries remain unknown.</summary>
public sealed class OriginalTacticEffectSourceOwnedContext : IOriginalTacticEffectSourceContext
{
    private readonly OriginalEventInitializationContext _current;
    private readonly Func<GameState, UnitState?, OriginalTacticSourceRow,
        S14TacticEffectProgram, OriginalTacticEffectUsageSource?> _usage;
    private readonly Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
        S14TacticEffectInstruction, OriginalTacticMoraleIncreaseSourceInput?>? _morale;
    private readonly OriginalTacticEffectSourceCurrentQueries? _queries;
    public string Revision { get; }
    public bool ProvidesNativeInstructionTargets => _queries?.InstructionTargets is not null;

    public OriginalTacticEffectSourceOwnedContext(OriginalEventInitializationContext current,
        string revision, Func<GameState, UnitState?, OriginalTacticSourceRow,
            S14TacticEffectProgram, OriginalTacticEffectUsageSource?> usage,
        Func<GameState, UnitState, UnitState, OriginalTacticSourceRow,
            S14TacticEffectInstruction, OriginalTacticMoraleIncreaseSourceInput?>? morale = null,
        OriginalTacticEffectSourceCurrentQueries? queries = null)
    {
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _usage = usage ?? throw new ArgumentNullException(nameof(usage));
        if (string.IsNullOrWhiteSpace(revision))
            throw new InvalidDataException("Current tactic owned context revision is missing.");
        Revision = revision;
        _morale = morale;
        _queries = queries;
    }

    public S14TacticEffectProgram? ReadCurrentProgram(GameState state, OriginalTacticSourceRow source)
    {
        OriginalTacticReference? receiver = Receiver(source);
        if (receiver is null) return null;
        try
        {
            return OriginalTacticEffectSourceProgram.ReadCurrent(source.Slot, receiver, _current);
        }
        catch (InvalidDataException error) when (error.Message.StartsWith("Original ", StringComparison.Ordinal) &&
            error.Message.EndsWith(" is unknown.", StringComparison.Ordinal))
        {
            // The query boundary identifies missing current reads. Structural
            // contradictions keep throwing instead of being reclassified as unknown.
            return null;
        }
    }

    public byte? ReadCurrentTargetFlags(GameState state, OriginalTacticSourceRow source)
    {
        OriginalTacticReference? receiver = Receiver(source);
        return receiver is null ? null : _current.ReadSkillTacticFlags(receiver);
    }

    /// <summary>SC173B13 reads native definition+42 directly. Preserve the
    /// current table alias and owned raw fields; the immutable fixed row cannot
    /// substitute for a later current definition write.</summary>
    public ushort? ReadCurrentWaitingContribution(OriginalTacticSourceRow source)
    {
        OriginalTacticReference? receiver = Receiver(source);
        if (receiver is null || _current.ReadSkillTacticRawByte(receiver, 0x42) is not byte low ||
            _current.ReadSkillTacticRawByte(receiver, 0x43) is not byte high) return null;
        return (ushort)(low | (high << 8));
    }

    public OriginalTacticEffectUsageSource? ResolveUsage(GameState state, UnitState? caster,
        OriginalTacticSourceRow source, S14TacticEffectProgram program) =>
        _usage(state, caster, source, program);

    public OriginalTacticMoraleIncreaseSourceInput? ResolveArmyMoraleIncrease(GameState state,
        UnitState caster, UnitState target, OriginalTacticSourceRow source,
        S14TacticEffectInstruction instruction) => _morale?.Invoke(state, caster, target, source, instruction);

    public IReadOnlyList<int>? ReadCurrentAbnormalUpperBounds(GameState state,
        UnitState caster, UnitState target)
    {
        var bounds = new int[3];
        for (int index = 0; index < bounds.Length; index++)
        {
            if (_current.ReadTacticAbnormalUpperBound(index) is not int value) return null;
            bounds[index] = value;
        }
        return Array.AsReadOnly(bounds);
    }

    public OriginalTacticInstructionTargetsSource? ResolveInstructionTargets(GameState state,
        UnitState caster, UnitState primaryTarget, OriginalTacticSourceRow source,
        S14TacticEffectInstruction instruction) =>
        _queries?.InstructionTargets?.Invoke(state, caster, primaryTarget, source, instruction);
    public TacticUseEligibility? ResolvePrimaryTargetEligibility(GameState state, UnitState caster,
        UnitState primaryTarget, OriginalTacticSourceRow source) =>
        _queries?.PrimaryTargetEligibility?.Invoke(state, caster, primaryTarget, source);
    public int? ReadArmyHealingSelectionCapacity(GameState state, UnitState target) =>
        _queries?.HealingSelectionCapacity?.Invoke(state, target);
    public ulong? FinishArmyAbnormalClear(GameState state, UnitState caster, UnitState target,
        OriginalTacticSourceRow source, S14TacticEffectInstruction instruction) =>
        _queries?.FinishArmyAbnormalClear?.Invoke(state, caster, target, source, instruction);
    public OriginalTacticEffectQueryResult<OriginalTacticHealingSourceInput>? ResolveArmyHealing(
        GameState state, UnitState caster, UnitState target, OriginalTacticSourceRow source,
        S14TacticEffectInstruction instruction) =>
        _queries?.ArmyHealing?.Invoke(state, caster, target, source, instruction);
    public OriginalTacticEffectQueryResult<OriginalTacticAbnormalStrengthSourceInput>? ResolveArmyAbnormalStrength(
        GameState state, UnitState caster, UnitState target, OriginalTacticSourceRow source,
        S14TacticEffectInstruction instruction) =>
        _queries?.ArmyAbnormalStrength?.Invoke(state, caster, target, source, instruction);
    public OriginalTacticEffectQueryResult<OriginalTacticAbnormalSourceInput>? ResolveArmyAbnormalStatus(
        GameState state, UnitState caster, UnitState target, OriginalTacticSourceRow source,
        S14TacticEffectInstruction instruction, int nativeIndex, int requestedStrength) =>
        _queries?.ArmyAbnormalStatus?.Invoke(state, caster, target, source, instruction, nativeIndex, requestedStrength);
    public ulong? FinishArmyAbnormalStatus(GameState state, UnitState caster, UnitState target,
        OriginalTacticSourceRow source, S14TacticEffectInstruction instruction,
        int nativeIndex, OriginalTacticAbnormalSourceResult result) =>
        _queries?.FinishArmyAbnormalStatus?.Invoke(state, caster, target, source, instruction, nativeIndex, result);
    public ulong? FinishArmyAbnormalAdjustment(GameState state, UnitState caster, UnitState target,
        OriginalTacticSourceRow source, S14TacticEffectInstruction instruction,
        int nativeIndex, OriginalTacticAbnormalSourceAdjustment adjustment) =>
        _queries?.FinishArmyAbnormalAdjustment?.Invoke(state, caster, target, source, instruction, nativeIndex, adjustment);
    public int? ReadCurrentArmyAbnormalUpperBound(GameState state, UnitState target, int nativeIndex) =>
        _current.ReadTacticAbnormalUpperBound(nativeIndex);
    public OriginalTacticEffectQueryResult<OriginalTacticCapturedOwnerSource>? ResolveArmyAbnormalCapturedOwner(
        GameState state, UnitState caster, UnitState target, OriginalTacticSourceRow source,
        S14TacticEffectInstruction instruction) =>
        _queries?.ArmyAbnormalCapturedOwner?.Invoke(state, caster, target, source, instruction);

    private OriginalTacticReference? Receiver(OriginalTacticSourceRow source)
    {
        if (_current.SkillDefinitionSource is not { } provenance ||
            provenance.ResourceUri != source.Source.ResourceUri ||
            !provenance.Sha256.Equals(source.Source.Sha256, StringComparison.OrdinalIgnoreCase) ||
            _current.SkillDefinitionDecodedSha256 != source.DecodedSha256)
            return null;
        OriginalTacticReference? receiver = _current.ReadSkillTacticReference(source.Slot);
        return receiver is not null && _current.ReadSkillTacticValidity(receiver) == true ? receiver : null;
    }
}
