using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>TC2C5360 / SC2C51E0. Count is the active prefix, including
    /// vacant entries. Only a matching raw commander reads its auxiliary time.</summary>
    private bool? ReadSelectedCommanderWaitingMode(GameState state, UnitState army,
        NativeMapSeasonDate? operationDate = null)
    {
        if (!Bound(state) || ArmyValid(state, army) is not bool valid) return null;
        if (!valid) return false;
        if (state.CommanderSelection?.ActiveCount is not int count) return null;
        if (count == 0) return false;
        if (state.CommanderSelection.Candidates is not { } entries || count > entries.Length ||
            ReadSelectionArmyCommander(state, army) is not ushort commander) return null;
        for (int index = 0; index < count; index++)
        {
            if (entries[index].OfficerId is not int selected || selected is < 0 or > ushort.MaxValue) return null;
            if (selected != commander) continue;
            if (entries[index].UninterpretedValue is not int previous ||
                state.CampaignEventWorld is not { } world || state.NativeMapSeason is not { } season ||
                Parameter(state, SimulationParameterKeys.Tactics.ManualInterventionMinimumPeriods) is not int minimum) return null;
            static int Period(NativeMapSeasonDate date) => unchecked(date.Year * 36 +
                (date.Month - 1) * 3 + (date.Day - 1) / 10);
            int current = unchecked(world.ElapsedPeriodOffset + (int)season.AdvancedPeriods +
                (operationDate is null ? 0 : Period(operationDate) - Period(season.ThirdDate)));
            return unchecked(current - previous) >= minimum;
        }
        return false;
    }

    /// <summary>17D6E0's reached player/selected-commander tail. A selected
    /// player Army that has reached the threshold requires commander flag20.</summary>
    private bool? ReadSelectedCommanderAutomaticGate(GameState state, UnitState army,
        NativeMapSeasonDate? operationDate = null)
    {
        if (_definitions.ReadCurrentPlayerForce() is not { } player ||
            _definitions.ReadForcePhysicalSlot(player) is not int playerId) return null;
        //The owned native creator and ArmyValid bind this force identity to
        //the actual commander/District. It is not a player/AI permission flag.
        if (ArmyValid(state, army) != true) return null;
        if (playerId != army.ForceId) return true;
        bool? selected = ReadSelectedCommanderWaitingMode(state, army, operationDate);
        if (selected is not true) return selected.HasValue ? true : null;
        if (ReadSelectionArmyCommander(state, army) is not ushort commander) return null;
        var person = commander <= 6000 ? _definitions.ReadPersonLookupReference(commander)
            : _definitions.ReadTransferPersonTableReference(0);
        if (person?.PhysicalSlot is not int physical ||
            _definitions.ReadArmyReturnPersonFlags196(physical) is not ushort flags) return null;
        return (flags & 0x20) != 0;
    }

    private ushort? ReadSelectionArmyCommander(GameState state, UnitState army)
    {
        if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical) return null;
        if (state.ReadArmyLifecycle()?.ReceiverSlots() is { } slots)
        {
            if ((uint)physical >= slots.Length || slots[physical].PhysicalSlot != physical ||
                slots[physical].ActiveUnitId != army.Id) return null;
            _definitions.BindUnspecifiedOwnedArmyQueryHeader(slots[physical]);
            return _definitions.ReadArmyUnitCommanderId(new(physical));
        }
        //The source creator clamps its actual packet commander to0..6000
        //and binds Unit.OfficerId to it. No independent arena is needed for
        //that existing constructed owner; legacy unknown origins fail ArmyValid.
        return army.OfficerId is >= 0 and <= 6000 ? (ushort)army.OfficerId : null;
    }

    public int? ReadSelectedCommanderWaitingRate(GameState state, UnitState unit, int nativeIndex)
    {
        if (nativeIndex == 0) return Parameter(state, SimulationParameterKeys.Tactics.OrdinaryWaitingRate);
        int? selector = BattleRemakeCurrentSettings.ReadSelectorE58(state);
        int? difficulty = selector is int value ? BattleRemakeCurrentSettings.ReadInt32(state,
            value == 0 ? 0x134 : 0x28) : null;
        if (difficulty is null) return null;
        return Parameter(state, (nativeIndex, difficulty > 2) switch
        {
            (1, false) => SimulationParameterKeys.Tactics.SelectedCommanderSecondaryWaitingRate,
            (2, false) => SimulationParameterKeys.Tactics.SelectedCommanderTertiaryWaitingRate,
            (1, true) => SimulationParameterKeys.Tactics.HighDifficultySelectedCommanderSecondaryWaitingRate,
            (2, true) => SimulationParameterKeys.Tactics.HighDifficultySelectedCommanderTertiaryWaitingRate,
            _ => throw new ArgumentOutOfRangeException(nameof(nativeIndex))
        });
    }
}
