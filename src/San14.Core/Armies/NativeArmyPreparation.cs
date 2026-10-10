namespace San14.Core;

/// <summary>Current campaign morale after logistics. Null leaves that aftermath unknown.</summary>
public sealed record ArmyMoralePreparationResolution(int? CurrentMorale);
public sealed record NativeArmyRegionPreparationResolution(ushort? NativeSavedRegionHex152);
/// <summary>Completed regional statistics, staged before army region effects are evaluated.</summary>
public sealed record AreaStatisticsUpdate(AreaMapStatisticsState Statistics);

/// <summary>Explicit CTurnState step36 logistics result for an owned Army.
/// Null means a reached current query is unresolved.</summary>
public interface IPlayableNativeArmyPreparationRules
{
    bool? ResolveLogisticsConnection(GameState state, UnitState army);
    //An incomplete native aftermath cannot silently retain constructor cache
    //values. Null means the current trait cache becomes unavailable.
    bool? PreservesTraitRoster(GameState state, UnitState army) => null;
    ArmyMoralePreparationResolution? ResolveMoraleAfterLogistics(GameState state,
        UnitState army, bool connected) => null;
    ICampaignResourceState? ResolveResourceKnowledgeAfterLogistics(GameState state, UnitState army,
        ICampaignResourceState? stagedKnowledge, bool traitRosterPreserved) => stagedKnowledge;
    bool ProvidesGlobalRegionPreparation => false;
    AreaStatisticsUpdate? ResolveGlobalRegionPreparation(GameState state) => null;
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
        var morale = new Dictionary<int, int?>();
        ICampaignResourceState? resourceKnowledge = state.ResourceState;
        foreach (UnitState army in working.Units.Values.Where(unit => unit.NativeArmyOrigin is not null).OrderBy(unit => unit.Id))
        {
            bool result = rules.ResolveLogisticsConnection(working, army)
                ?? throw new InvalidDataException("Owned Army logistics preparation inputs are unresolved.");
            connected.Add(army.Id, result);
            traitsPreserved.Add(army.Id, rules.PreservesTraitRoster(working, army) == true);
            morale.Add(army.Id, rules.ResolveMoraleAfterLogistics(working, army, result)?.CurrentMorale);
            resourceKnowledge = rules.ResolveResourceKnowledgeAfterLogistics(working, army, resourceKnowledge, traitsPreserved[army.Id]);
        }
        var units = new Dictionary<int, UnitState>(state.Units);
        foreach ((int id, bool connection) in connected)
        {
            UnitState army = units[id];
            NativeArmyTacticRuntime runtime = army.NativeTacticRuntime!;
            units[id] = army with { NativeTacticRuntime = runtime with
                { Flags26 = (byte)((runtime.Flags26 & 0xFD) | (connection ? 2 : 0)) },
                MoraleState = army.MoraleState with { CurrentValue = morale[id] },
                TraitRoster = traitsPreserved[id] ? army.TraitRoster : null };
        }
        //37 first refreshes global Area68/78, then snapshots every Army152.
        //Both consume the completed36 state. Neither may commit a prefix.
        GameState prepared = state with { Units = units, ResourceState = resourceKnowledge };
        GameState afterLogistics = GameStateSnapshot.Detach(prepared);
        bool globalPrepared = rules.ProvidesGlobalRegionPreparation && state.NativeArmySimulation is not null;
        if (globalPrepared)
        {
            AreaStatisticsUpdate global = rules.ResolveGlobalRegionPreparation(afterLogistics)
                ?? throw new InvalidDataException("Global native region preparation inputs are unresolved.");
            //Only receipt fields are accepted. Rebuild from the original
            //snapshot and staged36 units, excluding incidental provider writes.
            afterLogistics = prepared with { RegionStatistics = global.Statistics };
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
            { Units = units, RegionStatistics = afterLogistics.RegionStatistics });
    }
}
