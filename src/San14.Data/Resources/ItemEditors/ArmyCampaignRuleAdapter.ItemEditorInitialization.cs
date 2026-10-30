using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>5329A0's two actual world flags. Custody, definitions and CRT
    /// were already applied at the detached new-game source boundary.</summary>
    public GameState FinalizeItemEditorsCurrent(GameState state)
    {
        if (_scenario.InitialItemEditorState is not { } initial) return state;
        if (state.NativeClock is not { UpdateOrdinal:0,UpdatesIntoPeriod:0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } native ||
            !native.Parameters.Int32Values.TryGetValue(OwnedArmyReturnServiceInputs.Flags16A8Key, out int raw))
            throw new InvalidDataException("Item editor initialization requires its unused fresh world flags.");
        uint flags = unchecked((uint)raw) & ~0x400040u;
        if (initial.Selection.HistoricalIds.Length > 0) flags |= 0x40;
        if (Enumerable.Range(152,50).Any(id => state.ItemDefinitions.GetValueOrDefault(id)?.Appearance is { Kind: not 0 }))
            flags |= 0x400000;
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with
            { Int32Values = new(native.Parameters.Int32Values) { [OwnedArmyReturnServiceInputs.Flags16A8Key] = unchecked((int)flags) } } } };
    }
}
