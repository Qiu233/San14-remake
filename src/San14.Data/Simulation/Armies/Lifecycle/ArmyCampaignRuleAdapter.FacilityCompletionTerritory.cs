using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2E8320, only the first bit0 completion of definition3/4. The source
    //keeps the actual ring list alive while each185D00 changes the same world.
    private GameState ApplyFacilityCompletionTerritory(GameState state, SourceWorldContext current,
        ArmyUnitReference army, UnitState image, ObjectReference obj, ObjectDefinitionReference definition,
        FacilityOperation operation, List<CampaignEffectRequest> requests)
    {
        int kind = RepairNeed(definition.PhysicalSlot, "completion_territory_definition");
        if (kind is not (3 or 4)) throw RepairMissing("completion_territory_definition_scope");
        var range = current.ReadFacilityEffectDefinition(definition)
            ?? throw RepairMissing("completion_territory_range");
        int rawHex = RepairNeed(current.ReadObjectHexIndex(obj), "completion_territory_Object10");
        var center = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
            ?? throw RepairMissing("completion_territory_center");
        var pool = current.ReadOwnedCurrentListPool() ?? throw RepairMissing("completion_territory_pool");
        var candidates = current.CreateNearbyTemporary();
        GameState working;
        try
        {
            //0B9FE0 includes the actual advanced range setting.2A7EE0 emits
            //rings in linked order and does not append the center.
            NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, range.MaximumFor(state, kind), candidates);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            current.ArmyReturnTransferMode = ReadApplicationTransferModeCurrent();
            BindApplicationServiceCurrent(state, current);
            current.MaterializeFixedScenery(_landmarkDefinitions);
            for (int i = 0; i < 101; i++)
                current.SetSceneryFlags47(i, state.LandmarkInvestments?.GetValueOrDefault(i)?.Flags);
            current.BindFixedConstructionSceneryLinks();
            //The owned simulation has no original scenery-layout receiver.
            //The immutable publication below and existing territory requests
            //invalidate the real map; they do not acknowledge its playback.
            if (current.SceneryLayoutReader is null && current.CurrentSceneryLayout is null)
                current.CurrentSceneryLayout = new(null);
            working = new MonthlyCampaignMaintenanceSource(this, state, current, pool, borrowedEffects: requests)
                .ApplyFacilityCompletionTerritoryCurrent(army, image, center, range.Minimum,
                    candidates, operation.ReadDate());
        }
        finally { current.ClearNearbyTemporary(candidates, true); }
        return working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
    }

    private static bool FacilityCompletionTerritoryEligible(GameState state, SourceWorldContext current,
        UnitState image, HexReference candidate, int force)
    {
        //2C6A10's naval prefix precedes2C6AD0. Its flag1 then checks Army
        //occupancy before examining the resident Object; an occupied tile
        //must not demand fields of an otherwise irrelevant Object.
        var hex = OccupationHex(current, RepairNeed(candidate.PhysicalSlot, "completion_territory_candidate"));
        if (hex.PhysicalSlot is null) return false;
        byte surface = RepairNeed(current.ReadHexSurfaceCode(hex), "completion_territory_terrain");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
            ?? throw RepairMissing("completion_territory_terrain_pointer");
        if (terrain.PhysicalSlot is null) return false;
        if (terrain.PhysicalSlot == 24 && RepairNeed(ArmyConfigurationSourceAdapter.ReadNaval(state, image),
            "completion_territory_naval") is not (12 or 13 or 17 or 18)) return false;
        return GateCaptureTerritorySource.Eligible(state, current, hex, force, requireNoArmy: true);
    }

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        //Other callers keep their existing date. Only a reached facility
        //operation lends its actual pre-writer cut to Home/Scenery speech.
        private NativeMapSeasonDate? borrowedTerritoryDate;

        internal GameState ApplyFacilityCompletionTerritoryCurrent(ArmyUnitReference army, UnitState image,
            HexReference center, int minimum, HexListHandle candidates, NativeMapSeasonDate date)
        {
            borrowedTerritoryDate = date;
            int at = RepairNeed(center.PhysicalSlot, "completion_territory_center_identity");
            var node = current.ReadHexListHead(candidates) ?? throw RepairMissing("completion_territory_head");
            while (node.Identity.HasValue)
            {
                var hex = current.ReadHexListNodePayload(node) ?? throw RepairMissing("completion_territory_payload");
                int target = RepairNeed(hex.PhysicalSlot, "completion_territory_candidate_identity");
                if (ArmyRouteSearch.Distance(at, target) >= minimum)
                {
                    int force = current.ReadArmyUnitRawForceSlot(army);
                    //2C6A10(...,1,Army,0): retain its naval/occupancy/Object
                    //gate in that order. The
                    //185D00 wrapper subsequently repeats its own flag0 gate.
                    if (FacilityCompletionTerritoryEligible(state, current, image, hex, force) &&
                        RepairNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force,
                            RepairNeed(current.ReadHexForceSlot(hex), "completion_territory_Hex14")), "completion_territory_relation"))
                    {
                        //These Home and City reads occur anew for each
                        //accepted candidate, after preceding center transfers.
                        var home = current.FindArmyUnitCurrentBase(army);
                        int city = RepairNeed(CurrentGateMapQueries.FindCity(home, current).PhysicalSlot,
                            "completion_territory_Home_City");
                        ApplyFacilityCompletionHex(hex, force, city);
                    }
                }
                node = current.ReadHexListNodeNext(node) ?? throw RepairMissing("completion_territory_next");
            }
            return state;
        }

        private void ApplyFacilityCompletionHex(HexReference hex, int force, int city)
        {
            bool playerCenter = RepairNeed(current.ReadHexSurfaceCode(hex), "completion_territory_surface") is >= 15 and <= 18 &&
                force == RepairNeed((current.CurrentPlayerForce ?? throw RepairMissing("completion_territory_player")).PhysicalSlot,
                    "completion_territory_player_identity");
            if (playerCenter)
                state = package.PrepareApplicationServiceCurrent(state, current, profile => profile);
            //This is the existing complete185D00(flags1,1,0) writer: center
            //assignment, HomeArmy notifications, Scenery, raw Hex ownership,
            //Area/City/Person projections, map4/5 request and tutorial counter.
            RestoreMonthlyOccupation(hex, force, city, enqueueEncirclement: true,
                allowType3Override: false, showAppearance: true);
            if (playerCenter)
            {
                var profile = current.TransferService ?? throw RepairMissing("completion_territory_service");
                state = package.PrepareApplicationServiceCurrent(state, current, _ => profile);
            }
        }
    }
}
