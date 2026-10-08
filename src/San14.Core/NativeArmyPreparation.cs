namespace San14.Core;

/// <summary>Independent current Army byte1A after the logistics operation.
/// A null value explicitly leaves the reached native aftermath unknown.</summary>
public sealed record NativeArmyMoralePreparationResolution(byte? NativeMorale1A);
public sealed record NativeArmyRegionPreparationResolution(ushort? NativeSavedRegionHex152);
/// <summary>Global step37 writes to the existing native current parameter
/// owner. This receipt cannot replace source identity, floats or other state.</summary>
public sealed record NativeArmyGlobalRegionPreparationResolution(Dictionary<string, int> Int32Values);

/// <summary>Explicit CTurnState step36 logistics result for an owned Army.
/// Null means a reached current query is unresolved.</summary>
public interface IPlayableNativeArmyPreparationRules
{
    bool? ResolveLogisticsConnection(GameState state, UnitState army);
    //An incomplete native aftermath cannot silently retain constructor cache
    //values. Null means the current trait cache becomes unavailable.
    bool? PreservesTraitSlots64(GameState state, UnitState army) => null;
    NativeArmyMoralePreparationResolution? ResolveMoraleAfterLogistics(GameState state,
        UnitState army, bool connected) => null;
    byte? ResolveFlags150AfterLogistics(GameState state, UnitState army) => null;
    bool ProvidesFlags150Preparation => false;
    bool ProvidesGlobalRegionPreparation => false;
    NativeArmyGlobalRegionPreparationResolution? ResolveGlobalRegionPreparation(GameState state) => null;
    /// <summary>Separate CTurnState step37, after all step36 receipts are
    /// staged. This is not a movement or subday-update callback.</summary>
    NativeArmyRegionPreparationResolution? ResolveSavedRegionAfterPreparation(GameState state, UnitState army) => null;
}

public static class NativeArmyPreparation
{
    /// <summary>Preflights all source-created Armies, then commits mask02 and
    /// independent native aftermath receipts. Fresh creation never enables02.</summary>
    public static GameState PrepareAfterDeployment(GameState state, IPlayableNativeArmyPreparationRules? rules)
    {
        if (rules is null) return state;
        NativeSubdayStateValidation.Validate(state);
        GameState working = GameStateSnapshot.Detach(state);
        var connected = new Dictionary<int, bool>();
        var traitsPreserved = new Dictionary<int, bool>();
        var morale = new Dictionary<int, byte?>();
        var flags150 = state.NativeArmyLifecycle?.ArmyFlags150?.ToArray();
        foreach (UnitState army in working.Units.Values.Where(unit => unit.NativeArmyOrigin is not null).OrderBy(unit => unit.Id))
        {
            bool result = rules.ResolveLogisticsConnection(working, army)
                ?? throw new InvalidDataException("Owned Army logistics preparation inputs are unresolved.");
            connected.Add(army.Id, result);
            traitsPreserved.Add(army.Id, rules.PreservesTraitSlots64(working, army) == true);
            morale.Add(army.Id, rules.ResolveMoraleAfterLogistics(working, army, result)?.NativeMorale1A);
            if (rules.ProvidesFlags150Preparation && flags150 is not null && army.NativeArmyOrigin?.NativeSlotIndex is int physical)
                flags150[physical] = rules.ResolveFlags150AfterLogistics(working, army);
        }
        var units = new Dictionary<int, UnitState>(state.Units);
        foreach ((int id, bool connection) in connected)
        {
            UnitState army = units[id];
            NativeArmyTacticRuntime runtime = army.NativeTacticRuntime!;
            units[id] = army with { NativeTacticRuntime = runtime with
                { Flags26 = (byte)((runtime.Flags26 & 0xFD) | (connection ? 2 : 0)) },
                NativeMorale1A = morale[id],
                NativeArmyConfiguration = army.NativeArmyConfiguration! with
                    { TraitSlots64 = traitsPreserved[id] ? army.NativeArmyConfiguration!.TraitSlots64 : null } };
        }
        //37 first refreshes global Area68/78, then snapshots every Army152.
        //Both consume the completed36 state. Neither may commit a prefix.
        GameState prepared = state with { Units = units, NativeArmyLifecycle = state.NativeArmyLifecycle is { } lifecycle
            ? lifecycle with { ArmyFlags150 = flags150 } : null };
        GameState afterLogistics = GameStateSnapshot.Detach(prepared);
        bool globalPrepared = rules.ProvidesGlobalRegionPreparation && state.NativeArmySimulation is not null;
        if (globalPrepared)
        {
            if (state.NativeArmySimulation is not { } simulation)
                throw new InvalidDataException("Global native region preparation has no current parameter owner.");
            NativeArmyGlobalRegionPreparationResolution global = rules.ResolveGlobalRegionPreparation(afterLogistics)
                ?? throw new InvalidDataException("Global native region preparation inputs are unresolved.");
            if (global.Int32Values is null || global.Int32Values.Keys.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Global native region preparation returned malformed current fields.");
            var parameters = simulation.Parameters with { Int32Values = new(global.Int32Values, global.Int32Values.Comparer) };
            //Only receipt fields are accepted. Rebuild from the original
            //snapshot and staged36 units, excluding incidental provider writes.
            afterLogistics = GameStateSnapshot.Detach(prepared with { Units = units,
                NativeArmySimulation = simulation with { Parameters = parameters } });
            NativeArmySimulationStateValidation.Validate(afterLogistics);
        }
        var regions = new Dictionary<int, ushort?>();
        foreach (int id in connected.Keys.Order())
        {
            ushort? region = rules.ResolveSavedRegionAfterPreparation(afterLogistics, afterLogistics.Units[id])?.NativeSavedRegionHex152;
            if (region is > 48400) throw new InvalidDataException("Native saved-region preparation receipt is outside its source map.");
            regions.Add(id, region);
        }
        foreach ((int id, ushort? region) in regions)
            units[id] = units[id] with { NativeSavedRegionHex152 = region };
        return connected.Count == 0 && !globalPrepared ? state : GameStateSnapshot.Detach(prepared with
            { Units = units, NativeArmySimulation = afterLogistics.NativeArmySimulation });
    }
}
