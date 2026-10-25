using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The actual mixed combat descriptors captured before this phase
/// changed live troops, morale, receivers or ownership.</summary>
public sealed record NormalCombatParticipantSnapshot(int Kind, int PhysicalSlot, int? UnitId,
    byte Morale, ushort Troops, int HexId, int ForceSlot, byte AcceptedCount);

public sealed record NormalCombatPairSnapshot(int GenerationOrdinal,
    NormalCombatParticipantSnapshot Actor, NormalCombatParticipantSnapshot Target);

public sealed record NormalCombatArmySnapshot(int ArmyId, ushort NativePoolSlot, int OfficerId,
    byte CappedMorale, ushort Troops, int HexId, int ForceId, byte AcceptedCount);

/// <summary>The immutable ordered phase and its current pair's unapplied tail.
/// The same world's ArmyDefeatContinuation owns the suspended destruction;
/// no native context, callback, registry arena or regenerated plan is saved.</summary>
public sealed record NormalCombatImmediateContinuation(
    ImmutableArray<NormalCombatParticipantSnapshot> Actors,
    ImmutableArray<NormalCombatPairSnapshot> OrderedPairs,
    ImmutableDictionary<int, NormalCombatArmySnapshot> ArmyDescriptors,
    int PairIndex, ushort ReportWord20, int SampleDay, int SampleSalt)
{
    public AutomaticTacticExecutionContinuation? AutomaticExecution { get; init; }
    public BaseAutomaticTacticExecutionContinuation? BaseAutomaticExecution { get; init; }
}

public sealed partial record GameState
{
    public NormalCombatImmediateContinuation? NormalCombatImmediateContinuation { get; init; }
}

internal static class NormalCombatImmediateContinuationValidation
{
    //Collection shape/ranges are admitted once by the save decoder. Normal
    //lifecycle checks only inspect the retained cursor and current position.
    internal static void Validate(GameState state)
    {
        if (state.NormalCombatImmediateContinuation is not { } value)
        {
            if (state.NativePeriodContinuation?.Cursor is { StageIndex: 13 })
                throw new InvalidDataException("The immediate combat phase has no retained snapshot.");
            return;
        }
        if (state.NativePeriodContinuation?.Cursor is not { StageIndex: 13 } ||
            state.ArmyDefeatContinuation is not { SettlementIndex: -1 } ||
            value.Actors.IsDefault || value.OrderedPairs.IsDefaultOrEmpty || value.ArmyDescriptors is null ||
            value.PairIndex < 0 || value.PairIndex >= value.OrderedPairs.Length)
            throw new InvalidDataException("The immediate combat continuation has no matching phase or current pair.");
        if (value.AutomaticExecution is not null && value.BaseAutomaticExecution is not null)
            throw new InvalidDataException("A combat instruction cannot have both an Army and a Base caster.");
        if (value.BaseAutomaticExecution is { } baseAutomatic)
        {
            if (value.OrderedPairs[value.PairIndex].Actor.Kind is not (5 or 6) ||
                baseAutomatic.CasterBaseLocationId is < 1 or > 61 || baseAutomatic.GovernorOfficerId is < 0 or > 6000 ||
                baseAutomatic.SourceForceId is < 0 or > 51 || baseAutomatic.SourceHexId is < 0 or > 48400 ||
                baseAutomatic.TacticId is < 0 or > 200 || baseAutomatic.Instructions.IsDefault ||
                baseAutomatic.InstructionIndex < 0 || baseAutomatic.InstructionIndex >= baseAutomatic.Instructions.Length ||
                baseAutomatic.Instructions[baseAutomatic.InstructionIndex] is not { } instruction ||
                instruction.Index != baseAutomatic.InstructionIndex || instruction.OrderedHexIds.IsDefault ||
                baseAutomatic.TargetIndex < 0 || baseAutomatic.TargetIndex >= instruction.OrderedHexIds.Length ||
                baseAutomatic.TargetUnitId <= 0 || baseAutomatic.TargetArmyPhysicalSlot < 0 ||
                baseAutomatic.TargetReceiverId.Value <= 0 || baseAutomatic.TargetHexId is < 0 or > 48400 ||
                instruction.Opcode is not (1 or 16) || (instruction.Opcode == 1) != (baseAutomatic.TroopPrefix is not null) ||
                baseAutomatic.DamageContributions.IsDefault || baseAutomatic.Details.IsDefault ||
                baseAutomatic.FirstInstructionResults.IsDefault || baseAutomatic.SecondInstructionResults.IsDefault ||
                baseAutomatic.ArmyPresentationEffects.IsDefault)
                throw new InvalidDataException("The Base automatic tactic has no matching instruction or target cursor.");
        }
        if (value.AutomaticExecution is { } automatic)
        {
            if (automatic.CasterUnitId <= 0 || automatic.TacticId is < 0 or > 200 ||
                automatic.Instructions.IsDefault || automatic.InstructionIndex < 0 ||
                automatic.InstructionIndex >= automatic.Instructions.Length ||
                automatic.Instructions[automatic.InstructionIndex] is not { } instruction ||
                instruction.Index != automatic.InstructionIndex || instruction.OrderedHexIds.IsDefault ||
                automatic.TargetIndex < 0 || automatic.TargetIndex >= instruction.OrderedHexIds.Length ||
                automatic.TargetUnitId <= 0 || automatic.TargetArmyPhysicalSlot < 0 || automatic.TargetReceiverId.Value <= 0 ||
                automatic.TargetHexId is < 0 or > 48400 ||
                instruction.Opcode is not (1 or 16) ||
                (instruction.Opcode == 1) != (automatic.TroopPrefix is not null) ||
                (instruction.Opcode == 16) != (automatic.MoralePrefix is not null) ||
                automatic.DamageContributions.IsDefault || automatic.Details.IsDefault ||
                automatic.FirstInstructionResults.IsDefault || automatic.SecondInstructionResults.IsDefault ||
                automatic.ArmyPresentationEffects.IsDefault)
                throw new InvalidDataException("The automatic tactic continuation has no matching instruction or target cursor.");
            if (automatic.LinkedExecution is { } linked && (linked.RemainingActors.IsDefault ||
                linked.CompletedActors.IsDefault || linked.RemainingActors.Length + linked.CompletedActors.Length > 10))
                throw new InvalidDataException("The automatic tactic linked actor continuation is incomplete.");
        }
    }
}
