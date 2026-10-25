using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal sealed record StrategicIncitementArmyResult(GameState State, bool Created, int BandForceId);

    // The great-success branch of Incitement selects a special District before
    // gathering cells. Its Army creator is distinct from ordinary deployment.
    internal StrategicIncitementArmyResult ApplyStrategicIncitementArmyCurrent(GameState state,
        SourceWorldContext current, int areaPhysical, int executorPhysical)
    {
        var source = new StrategicIncitementArmyCurrent(this, state, current, areaPhysical);
        int district = source.SelectDistrict(executorPhysical);
        int bandForce = source.DistrictForce(district);
        if (!source.HasSpawnCell()) return new(state, false, bandForce);
        if (!source.HasArmyReceiver()) return new(state, false, bandForce);

        // The next original operation generates a special Person, including
        // its first App draw, before shuffling cells or configuring the Army.
        // Ordinary ArmyConstruction would require an existing serving Person
        // and would publish different status, formation, route and group data.
        // Keep the unsupported producer at its actual reached boundary; neither
        // downgrade a real success nor consume an invented random sequence.
        throw StrategyMissing("incitement_special_Person_creation_4033_4100_and_Army_registry_route_group_admission");
    }

    private sealed class StrategicIncitementArmyCurrent(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, int area)
    {
        internal int SelectDistrict(int executor)
        {
            int rawCity = StrategyNeed(current.ReadAreaCitySlot(new(area)), "incitement_Area35");
            var city = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0)
                ?? throw StrategyMissing("incitement_City_pointer");
            int cityPhysical = StrategyNeed(city.PhysicalSlot, "incitement_City_physical");
            int province = StrategyNeed(package._growthQueries.ReadCityState(state, cityPhysical), "incitement_City_provinceCE");
            int district = 51;
            // All five tests run in source order; a later qualifying trait wins.
            if (Trait(executor, 83) != 0 && (province is 1 or 3 or 4 || rawCity is 9 or 11)) district = 46;
            if (Trait(executor, 84) != 0 && (province is 2 or 7 or 8 or 9 || rawCity is 10 or 33 or 34)) district = 47;
            if (Trait(executor, 85) != 0 && (province is 10 or 11 || rawCity is 41 or 42)) district = 48;
            if (Trait(executor, 86) != 0 && (province is 6 or 12 or 13 || rawCity == 32)) district = 49;
            if (Trait(executor, 87) != 0 && (province is 15 or 17 || rawCity is 35 or 40 or 43 or 44)) district = 50;
            if (district == 51) return district;

            int rawForce = StrategyNeed(current.ReadAreaCurrentForceSlot(new(area)), "incitement_Area_Force");
            var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
                ?? throw StrategyMissing("incitement_target_Force_pointer");
            int owner = StrategyNeed(current.ReadForcePhysicalSlot(force), "incitement_target_Force_physical");
            // Offline source data gives the friendship cutoff as 70.
            if (Relation(owner, district) >= StrategicParameter(state, "incitement.foreign-friendship-cutoff", 70) ||
                Flags(DistrictForce(district)) != 0 || CityHasTrait(city, district - 46 + 83)) return 51;
            return district;
        }

        internal int DistrictForce(int district)
        {
            int raw = StrategyNeed(current.ReadDistrictForceId(district), "incitement_special_District10");
            var force = current.ReadForceTableReference(raw < 52 ? raw : 0)
                ?? throw StrategyMissing("incitement_special_Force_pointer");
            return StrategyNeed(current.ReadForcePhysicalSlot(force), "incitement_special_Force_physical");
        }

        private bool CityHasTrait(BaseReference city, int effect)
        {
            if (!StrategyNeed(current.ReadBaseNativeValidity(city), "incitement_protected_City_validity") ||
                StrategyNeed(current.ReadBaseForceSlot(city), "incitement_protected_City_Force") == 0) return false;
            int home = StrategyNeed(current.ReadBaseObjectLink(city), "incitement_protected_City4E");
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw StrategyMissing("incitement_Person100"))
            {
                int person = StrategyNeed(entry, "incitement_protected_Person_pointer");
                if (StrategyNeed(current.ReadPersonStatus(person), "incitement_protected_Person_status") is < 1 or > 4 ||
                    StrategyNeed(current.ReadPersonFirstLocation(person), "incitement_protected_Person_home") != home ||
                    PersonCurrentBusySource.Read(current, person, StrategyMissing)) continue;
                if (Trait(person, effect) != 0) return true;
            }
            return false;
        }

        // 2A8C30 appends through the current temporary-list allocator. A failed
        // append really leaves no node, so allocation suppression can yield an
        // empty list even when geometric eligibility succeeds.
        internal bool HasSpawnCell()
        {
            var list = current.CreateTemporaryHexList() ?? throw StrategyMissing("incitement_spawn_temporary_list");
            bool any = false;
            try
            {
                for (int cursor = AreaHexIterator.FindNextIndex(0, area, current); cursor < 48400;
                    cursor = AreaHexIterator.FindNextIndex(cursor + 1, area, current))
                {
                    var hex = current.ReadHexTableReference(cursor) ?? throw StrategyMissing("incitement_spawn_Hex_pointer");
                    if (!hex.PhysicalSlot.HasValue) continue;
                    int surface = StrategyNeed(current.ReadHexSurfaceCode(hex), "incitement_spawn_Hex10");
                    var terrain = current.ReadSurfaceTypeTableReference(surface < 31 ? surface : 0)
                        ?? throw StrategyMissing("incitement_spawn_Terrain_pointer");
                    if (!terrain.PhysicalSlot.HasValue ||
                        StrategyNeed(current.ReadSurfaceTypeRawField2A(terrain), "incitement_spawn_Terrain2A") == 0) continue;
                    int army = unchecked((short)StrategyNeed(current.ReadHexRaw1C(hex), "incitement_spawn_Hex1C"));
                    if (army is >= 1 and <= 500) continue;
                    var obj = FacilityConstructionLegality.ObjectAt(current, hex);
                    int objectPhysical = StrategyNeed(obj.PhysicalSlot, "incitement_spawn_Object_physical");
                    if (objectPhysical is >= 52 and <= 61)
                    {
                        if (StrategyNeed(current.ReadObjectHexIndex(obj), "incitement_spawn_Gate_Hex10") != cursor) continue;
                        int otherForce = StrategyNeed(current.ReadHexForceSlot(hex), "incitement_spawn_Gate_Hex14");
                        if (StrategyNeed(ArmyLogisticsPreparation.ReadNativeRelationGate(state, 0, otherForce), "incitement_spawn_Gate_relation")) continue;
                    }
                    else
                    {
                        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
                        if (StrategyNeed(current.ReadObjectDefinitionValidity(definition), "incitement_spawn_definition_validity") &&
                            StrategyNeed(current.ReadObjectDefinitionRaw2C(definition), "incitement_spawn_definition2C") != 4) continue;
                    }
                    int terrainPhysical = StrategyNeed(terrain.PhysicalSlot, "incitement_spawn_Terrain_physical");
                    if ((StrategyNeed(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, terrainPhysical), "incitement_spawn_Terrain2C") & 4) == 0 ||
                        StrategyNeed(current.ReadCaptureHexWord16(cursor) ?? state.NativeBaseCaptureCurrent?.HexWords16[cursor], "incitement_spawn_Hex16") != 0 ||
                        StrategyNeed(current.ReadObjectNativeValidity(obj), "incitement_spawn_Object_validity") || army >= 1) continue;
                    var node = current.AllocateHexListNode(list);
                    if (node.Identity is not int identity) continue;
                    current.WriteCurrentListPayload(identity, cursor);
                    any = true;
                }
                return any;
            }
            finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        }

        // 2D3410 scans all 501 table pointers, including inactive receivers;
        // registry-only enumeration cannot find a free receiver.
        internal bool HasArmyReceiver()
        {
            ArmyUnitReference? first = null;
            int count = 0;
            for (int slot = 0; slot <= 500; slot++)
            {
                var army = current.ReadArmyUnitTableReference(slot) ?? throw StrategyMissing("incitement_Army_table_pointer");
                if (StrategyNeed(army.PhysicalSlot, "incitement_Army_physical") == 0) continue;
                if (!StrategyNeed(current.ReadArmyUnitNativeValidity(army), "incitement_Army_validity"))
                { first ??= army; continue; }
                var force = current.ReadArmyUnitForceReference(army) ?? throw StrategyMissing("incitement_Army_Force_pointer");
                if (StrategyNeed(current.ReadForcePhysicalSlot(force), "incitement_Army_Force_physical") is >= 46 and <= 51) count++;
            }
            return count < StrategicParameter(state, "incitement.special-army-capacity", 10) && first is not null &&
                !StrategyNeed(current.ReadArmyUnitNativeValidity(first), "incitement_free_Army_validity");
        }

        private int Trait(int person, int effect) => StrategyNeed(TacticRemakeCurrentTraits.PersonContribution(current, person, effect), "incitement_Person_trait_" + effect);
        private byte Flags(int force)
        {
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true)
                return StrategyNeed(reset[0x194], "incitement_reset_Force194");
            return StrategyNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.Flags194, "incitement_Force194");
        }
        private byte Relation(int force, int other)
        {
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true)
                return StrategyNeed(reset[0x13 + other], "incitement_reset_relation13");
            var values = force == 0 ? state.NativeForceUnregisterCurrent?.PlaceholderRelations13 :
                state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.NormalAftermath?.Relations13;
            return values is { Length: 52 } ? values[other] : throw StrategyMissing("incitement_Force_relations13");
        }
    }
}
