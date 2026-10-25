using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //428D70→27F320. This scheduled gameplay count is not a consistency
    //check: the terrain denominator is shared by all promotion candidates.
    internal GameState ApplyNormalTurnForceRankMaintenanceCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        var application = ReadApplicationGameplay() ?? throw RankMaintenanceMissing("Application_context");
        bool tutorialPresent = RankMaintenanceNeed(application.TutorialServicePresent, "tutorial_service_presence");
        if (tutorialPresent && RankMaintenanceNeed(application.TutorialActive, "tutorial_active") &&
            RankMaintenanceNeed(application.SpecialModeServicePresent, "special_service_presence"))
            throw RankMaintenanceMissing("special_service_rank_suppression_bit20");
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw RankMaintenanceMissing("current_pool_binding");

        int total = 0;
        var owned = new int[52];
        for (int slot = 0; slot < 48400; slot++)
        {
            var hex = current.ReadHexTableReference(slot) ?? throw RankMaintenanceMissing("Hex_pointer");
            //CHex is native-valid for every non-null pointer, including Hex0.
            if (!hex.PhysicalSlot.HasValue) continue;
            int raw = RankMaintenanceNeed(current.ReadHexSurfaceCode(hex), "Hex10");
            var terrain = current.ReadSurfaceTypeTableReference(raw <= 30 ? raw : 0)
                ?? throw RankMaintenanceMissing("surface_type_pointer");
            if (RankMaintenanceNeed(current.ReadSurfaceTypeRawField2A(terrain), "surface_type2A") == 0) continue;
            int owner = RankMaintenanceNeed(current.ReadHexForceSlot(hex), "Hex14");
            if ((uint)owner >= owned.Length) throw RankMaintenanceMissing("Hex14_range");
            total = unchecked(total + 1);
            owned[owner] = unchecked(owned[owner] + 1);
        }

        foreach (var row in current.ReadRegistryReferences(RegistryKind.Force)
            ?? throw RankMaintenanceMissing("Force_registryC0"))
        {
            if (row is not { Kind: RegistryKind.Force, PhysicalSlot: int physical })
                throw RankMaintenanceMissing("Force_registry_payload");
            var force = new ForceReference(physical);
            if (!RankMaintenanceNeed(current.ReadForceNativeValidity(force), "Force_validity")) continue;
            int id = RankMaintenanceNeed(current.ReadForcePhysicalSlot(force), "Force60");
            if ((uint)id >= owned.Length) throw RankMaintenanceMissing("Force60_range");
            ApplyForce(force, id);
        }
        return state;

        void ApplyForce(ForceReference force, int id)
        {
            if (RankMaintenanceNeed(current.ReadForceKingdomNameIndex(id), "Force48") == 6 &&
                TalentForceIsPlayerCurrent(state, current, force)) return;
            int rank = RankMaintenanceNeed(current.ReadForceRankSlot47(id), "Force47");
            if (rank is 1 or 11 or 12) return;
            int next = rank - 1;
            var definition = current.ReadStaticTableReference(0x78490, next is >= 0 and < 13 ? next : 0)
                ?? throw RankMaintenanceMissing("next_rank_pointer");
            if (definition.PhysicalSlot is null or 0) return;
            int cities = RankMaintenanceNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current, force), "City_count22AB60");
            var states = current.CreateTemporaryHexList() ?? throw RankMaintenanceMissing("State_temporary_descriptor");
            try
            {
                int completeStates = CountCompleteStates(force, id, states);
                //The source uses float32, retaining IEEE NaN comparisons if
                //the actual terrain table contains no eligible cell.
                float share = (float)owned[id] / total * 100f;
                bool blocked = TalentForceIsPlayerCurrent(state, current, force);
                switch (rank)
                {
                    case 2:
                        blocked |= cities < 25;
                        int emperor = KingPromotionLookup(current,
                            RankMaintenanceNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654"));
                        bool present = RankMaintenanceNeed(current.ReadPersonNativeValidity(emperor), "emperor_validity");
                        //22D6C0 returns a Hex, whose14 is the occupying Force.
                        var position = PersonReturnCommands.ResolvePersonPosition(state, current, emperor,
                            () => state.NativeMapSeason?.ThirdDate ?? throw RankMaintenanceMissing("World34_date"));
                        if (present && (!position.PhysicalSlot.HasValue ||
                            RankMaintenanceNeed(current.ReadHexForceSlot(position), "emperor_Hex14") != id)) blocked = true;
                        break;
                    //Immutable TC promotion thresholds AA10..AB04.
                    case 3: blocked |= share < 40 || completeStates < 5 || cities < 15; break;
                    case 4: blocked |= share < 35 || completeStates < 4 || cities < 12; break;
                    case 5: blocked |= share < 25 || completeStates < 3 || cities < 10; break;
                    case 6: blocked |= share < 15 || completeStates < 2 || cities < 7; break;
                    case 7: blocked |= share < 10 || completeStates < 2 || cities < 5; break;
                    case 8: blocked |= share < 5 || completeStates < 1 || cities < 3; break;
                    case 9: blocked |= cities < 2 || completeStates < 1 && share < 2; break;
                    default: return;
                }
                if (blocked) return;
                //The shared2B5ED0 writer still has only a rank2 caller.
                //Do not change Rank47 without its loyalty, security,
                //experience, naming, report and emperor-removal semantics.
                throw RankMaintenanceMissing("2B5ED0_general_rank_promotion");
            }
            finally { current.ClearTemporaryHexList(states); current.ReleaseTemporaryHexList(states); }
        }

        int CountCompleteStates(ForceReference force, int forceId, HexListHandle output)
        {
            //2AB190 first allocates and fills2AAD90's City list even though
            //its subsequent ownership scan uses the registered City chain.
            current.ClearTemporaryHexList(output);
            if (!RankMaintenanceNeed(current.ReadForceNativeValidity(force), "State_count_Force_validity")) return 0;
            var cities = current.CreateTemporaryHexList() ?? throw RankMaintenanceMissing("City_temporary_descriptor");
            try
            {
                current.ClearTemporaryHexList(cities);
                for (int slot = 0; slot < 52; slot++)
                {
                    var city = current.ReadCityTableReference(slot) ?? throw RankMaintenanceMissing("City_table_pointer");
                    if (forceId is < 1 or > 51 ||
                        RankMaintenanceNeed(current.ReadBaseForceSlot(city), "City_Force") != forceId ||
                        !RankMaintenanceNeed(current.ReadBaseNativeValidity(city), "City_validity")) continue;
                    var node = current.AllocateRegistryNode(new(cities.Identity)) ?? throw RankMaintenanceMissing("City_temporary_node");
                    if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.City, city.PhysicalSlot));
                }
                for (int slot = 0; slot < 23; slot++)
                {
                    var province = current.ReadStaticTableReference(0xDC80, slot) ?? throw RankMaintenanceMissing("State_pointer");
                    if (province.PhysicalSlot is not int stateId || stateId == 0) continue;
                    int count = 0;
                    bool all = true;
                    foreach (var row in current.ReadRegistryReferences(RegistryKind.City)
                        ?? throw RankMaintenanceMissing("City_registry90"))
                    {
                        if (row is not { Kind: RegistryKind.City, PhysicalSlot: int physical })
                            throw RankMaintenanceMissing("City_registry_payload");
                        if (!RankMaintenanceNeed(current.ReadRegistryObjectNativeValidity(row), "registered_City_validity")) continue;
                        if (RankMaintenanceNeed(_growthQueries.ReadCityState(state, physical), "CityCE") != stateId) continue;
                        count = unchecked(count + 1);
                        if (RankMaintenanceNeed(current.ReadBaseForceSlot(new(BaseKind.City, physical)), "registered_City_Force") == forceId) continue;
                        all = false;
                        break;
                    }
                    if (count != 0 && all) _ = current.AppendReturnStateTemporary(output, stateId);
                }
                return RankMaintenanceNeed(current.ReadTemporaryHexListCount(output), "State_temporary_count");
            }
            finally { current.ClearTemporaryHexList(cities); current.ReleaseTemporaryHexList(cities); }
        }
    }

    private static T RankMaintenanceNeed<T>(T? value, string field) where T : struct =>
        value ?? throw RankMaintenanceMissing(field);
    private static InvalidDataException RankMaintenanceMissing(string field) => new("normal-turn.force-rank." + field);
}
