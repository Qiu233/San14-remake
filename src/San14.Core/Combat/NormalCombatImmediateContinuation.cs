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
    int PairIndex, ushort ReportWord20, int SampleDay, int SampleSalt);

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
    }
}
