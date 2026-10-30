using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //The two ordered Area tables from the fifth Opening. Their City operands
    //are separate from the Force operands;185D00 owns any center reassignment.
    private static readonly int[] FifthOpeningPlayerAreas = [50, 48, 51, 44];
    private static readonly int[] FifthOpeningOtherAreas = [36, 37, 40, 41, 42];

    /// <summary>4BC370's complete domain preparation before narration178.
    /// The lesson host owns the surrounding UI, force overlays and camera.
    /// This borrows one normally initialized world and its actual list pool;
    /// no domestic command, alliance success or lesson completion is invented.</summary>
    internal GameState InitializeFifthTutorialOpeningCurrent(GameState state)
    {
        RequireSelectedTutorialControl(state);
        if (state.TutorialLessonProgress is not { CourseIndex: 4 })
            throw FifthOpeningMissing("selected_lesson");
        foreach (int flag in new[] { 78, 79, 81, 62 })
            state = WriteFirstTutorialControl(state, flag, true);

        var world = CaptureOwnedArmyReturnWorld(state);
        var current = world.Context;
        var pool = world.Pool;
        var city = current.ReadCityTableReference(7) ?? throw FifthOpeningMissing("City7_pointer");

        //Keep the native registry's order. OriginalCity34 is independent of
        //the mutable assigned City35 and is not inferred from a Core roster.
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Area)
            ?? throw FifthOpeningMissing("Area_registry"))
        {
            if (entry is not { Kind: RegistryKind.Area, PhysicalSlot: int physical })
                throw FifthOpeningMissing("Area_registry_payload");
            var area = current.ReadAreaTableReference(physical) ?? throw FifthOpeningMissing("registered_Area_pointer");
            if (FifthOpeningNeed(current.ReadAreaValidityCode(area), "original_City34") != 5) continue;
            var original = current.ReadCityTableReference(5) ?? throw FifthOpeningMissing("City5_pointer");
            int force = FifthOpeningNeed(current.ReadBaseForceSlot(original), "City5_current_Force");
            state = TransferArea(state, area, force, 5);
        }
        foreach (int physical in FifthOpeningPlayerAreas)
            state = TransferArea(state, Area(physical), 2, 7);
        foreach (int physical in FifthOpeningOtherAreas)
            state = TransferArea(state, Area(physical), 15, 6);

        //33E2E0 refreshes the latched Area counts here. It does not execute
        //the separate2EC4C0 Army-region snapshot from ordinary Turn maintenance.
        state = RefreshFifthOpeningAreaCounts(state, current);

        //These are six direct, directional Relations13 byte stores. EA's
        //alliance terms remain separate and no diplomatic command is submitted.
        state = AbsorptionWriteRelation(state, current, 15, 2, alliance: false, 200);
        state = AbsorptionWriteRelation(state, current, 2, 15, alliance: false, 200);
        state = AbsorptionWriteRelation(state, current, 2, 28, alliance: false, 0);
        state = AbsorptionWriteRelation(state, current, 28, 2, alliance: false, 0);
        state = AbsorptionWriteRelation(state, current, 2, 6, alliance: false, 0);
        state = AbsorptionWriteRelation(state, current, 6, 2, alliance: false, 0);

        //The source selects physical Person952 and asks its current Force.
        //The rank store neither infers that Force nor runs promotion reports.
        int person = FifthOpeningNeed((current.ReadTransferPersonTableReference(952)
            ?? throw FifthOpeningMissing("rank_Person_pointer")).PhysicalSlot, "rank_Person");
        if (FifthOpeningNeed(current.ReadPersonNativeValidity(person), "rank_Person_validity"))
        {
            var force = current.ReadPersonForce(person) ?? throw FifthOpeningMissing("rank_Force_pointer");
            if (FifthOpeningNeed(current.ReadForceNativeValidity(force), "rank_Force_validity"))
            {
                int physical = FifthOpeningNeed(force.PhysicalSlot, "rank_Force");
                var owner = state.Forces.GetValueOrDefault(physical) ?? throw FifthOpeningMissing("rank_Force_owner");
                var simulation = state.NativeArmySimulation ?? throw FifthOpeningMissing("rank_simulation");
                current.SetForceRankSlot47(physical, 9);
                state = state with
                {
                    Forces = new(state.Forces) { [physical] = owner with { RankId = 9 } },
                    NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                        { Int32Values = new(simulation.Parameters.Int32Values)
                            { [OwnedArmyMoraleWaveInputs.ForceRank47Key(physical)] = 9 } } }
                };
            }
        }

        foreach (int flag in new[] { 51, 52, 53 }) state = WriteFirstTutorialControl(state, flag, true);

        //The normal virtual98/A8 setters and vC8 troop bound remain in force.
        //10000 ready and100000 food are the extracted immutable lesson inputs.
        int gold = Bound(10000, BasePostLoad.ReadGoldCapacity(city, current));
        current.SetBaseResource(city, BaseResourceKind.Gold, gold);
        int troops = Bound(10000, BasePostLoad.ReadTroopCapacity(city, current));
        current.SetBaseResource(city, BaseResourceKind.Troops, troops);
        int food = Bound(100000, BasePostLoad.ReadSuppliesCapacity(city, current));
        current.SetBaseResource(city, BaseResourceKind.Supplies, food);
        var cityOwner = state.Cities.GetValueOrDefault(7) ?? throw FifthOpeningMissing("City7_owner");
        state = state with { Cities = new(state.Cities) { [7] = cityOwner with
            { Gold = gold, Troops = troops, Supplies = food } } };
        state = SetFirstTutorialCommandMask(state, 0);
        state = WriteFirstTutorialControl(state, 58, true);
        state = WriteFirstTutorialControl(state, 59, true);
        state = SynchronizeDiplomacyProjection(state);
        return state.WithArmyLifecycle((state.ReadArmyLifecycle() ?? throw FifthOpeningMissing("lifecycle")) with
            { Indexes = pool.ExportOwnedState() });

        AreaReference Area(int physical) => current.ReadAreaTableReference(physical)
            ?? throw FifthOpeningMissing("table_Area_pointer");
        GameState TransferArea(GameState latest, AreaReference area, int force, int cityId)
        {
            if (!FifthOpeningNeed(current.ReadAreaNativeValidity(area), "transfer_Area_validity")) return latest;
            int physical = FifthOpeningNeed(area.PhysicalSlot, "transfer_Area");
            var hexes = new List<int>();
            for (int hex = AreaHexIterator.FindNextIndex(0, physical, current); hex < 48400;
                 hex = AreaHexIterator.FindNextIndex(hex + 1, physical, current))
            {
                var pointer = current.ReadHexTableReference(hex) ?? throw FifthOpeningMissing("transfer_Hex_pointer");
                //567F50 avoids invoking185D00 for an already-owned tile.
                if (FifthOpeningNeed(current.ReadHexForceSlot(pointer), "transfer_Hex_Force") != force) hexes.Add(hex);
            }
            //The existing185D00 kernel retains eligibility, actual center
            //City changes, resident moves, encirclement/scenery side effects
            //and the tutorial transfer counter, with source flags0,1,0.
            return new MonthlyCampaignMaintenanceSource(this, latest, current, pool)
                .ApplyTutorialTerritory(hexes, force, cityId, showAppearance: false);
        }
        static int Bound(int requested, int maximum) => requested < 0 ? 0 : Math.Min(requested, maximum);
    }

    private static GameState RefreshFifthOpeningAreaCounts(GameState state, SourceWorldContext current)
    {
        var counts = new int[501];
        for (int physical = 0; physical < 48400; physical++)
        {
            var hex = current.ReadHexTableReference(physical) ?? throw FifthOpeningMissing("counts_Hex_pointer");
            int raw = FifthOpeningNeed(current.ReadHexAreaSlot(hex), "counts_Hex_Area");
            var area = current.ReadAreaTableReference(raw <= 500 ? raw : 0)
                ?? throw FifthOpeningMissing("counts_Area_pointer");
            if (!FifthOpeningNeed(current.ReadAreaNativeValidity(area), "counts_Area_validity")) continue;
            int force = FifthOpeningNeed(current.ReadAreaCurrentForceSlot(area), "counts_Area_Force");
            if (force is < 1 or > 51 || FifthOpeningNeed(current.ReadHexForceSlot(hex), "counts_Hex_Force") != force) continue;
            int index = FifthOpeningNeed(area.PhysicalSlot, "counts_Area");
            counts[index] = unchecked(counts[index] + 1);
        }
        var saved = state.RegionStatistics ?? throw FifthOpeningMissing("counts_saved_owner");
        var entries = new Dictionary<int, AreaMapStatisticsEntry>(saved.Entries);
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Area)
            ?? throw FifthOpeningMissing("counts_Area_registry"))
        {
            if (entry is not { Kind: RegistryKind.Area, PhysicalSlot: int physical })
                throw FifthOpeningMissing("counts_Area_payload");
            var area = current.ReadAreaTableReference(physical) ?? throw FifthOpeningMissing("counts_registered_Area");
            ushort count = AreaMapStatisticsInitialization.CountToNativeWord(counts[physical]);
            byte force = (byte)Math.Clamp((int)FifthOpeningNeed(current.ReadAreaCurrentForceSlot(area), "counts_snapshot_Force"), 0, 51);
            current.WriteAreaRawField68(area, count);
            current.WriteAreaRawField78(area, force);
            var previous = entries.GetValueOrDefault(physical) ?? throw FifthOpeningMissing("counts_saved_Area");
            entries[physical] = previous with
                { OwnedCellCount = count, ForceId = force == 0 ? null : force, IsOwnedCountCurrent = true };
        }
        return state with { RegionStatistics = saved with { Entries = entries } };
    }

    private static T FifthOpeningNeed<T>(T? value, string name) where T : struct => value ?? throw FifthOpeningMissing(name);
    private static InvalidDataException FifthOpeningMissing(string name) => new("tutorial.fifth_lesson.opening." + name);
}
