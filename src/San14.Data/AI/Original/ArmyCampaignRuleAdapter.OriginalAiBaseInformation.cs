using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal BaseInformationValue ReadOriginalAiFootholdInformationCurrent(GameState state,
        SourceWorldContext current, OriginalAiActor actor, BaseReference receiver, int mode)
    {
        BaseInformationValue Number(int value) => new BaseInformationInteger(value);
        BaseReference City() => CurrentGateMapQueries.FindCity(receiver, current);
        int Location() => BaseInfoNeed(current.ReadBaseObjectLink(receiver), "Base4E");
        int Currency(BaseResourceKind kind) => BaseInfoNeed(current.ReadBaseResource(
            receiver.Kind == BaseKind.Gate ? City() : receiver, kind), "Base_currency_" + kind);
        BaseInformationValue People(IEnumerable<int> values) => new BaseInformationEntities(WorldEntityKind.Officer,
            values.Select(person => BaseInfoPersonEntity(current, BaseInfoNeed(current.ReadPersonIdentity(person), "Person10"))).ToArray());
        switch (mode)
        {
            case 0: return Number(Location());
            case 1:
                return new BaseInformationHex(new(BaseInfoNeed(current.FindBaseHex(receiver)?.PhysicalSlot, "Base_Hex_pointer")));
            case 2: return new BaseInformationEntity(BaseInfoPersonEntity(current,
                BaseInfoNeed(current.ReadArmyReturnBaseGovernor2E(receiver), "Base_governor2E")));
            case 3:
            {
                int raw = BaseInfoNeed(current.ReadBaseForceSlot(receiver), "Base_Force");
                var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw BaseInfoMissing("Base_Force_pointer");
                return new BaseInformationEntity(new(WorldEntityKind.Force, BaseInfoNeed(force.PhysicalSlot, "Base_Force_identity")));
            }
            case 4:
                return new BaseInformationEntity(new(WorldEntityKind.District,
                    BaseInfoNeed(current.ReadBaseDistrictReference(receiver)?.PhysicalSlot, "Base_District_pointer")));
            case 5: return Number(BaseInfoNeed(current.ReadCitySizeB8(City()), "mapped_City_sizeB8"));
            case 6: return Number(BasePostLoad.ReadGoldCapacity(receiver.Kind == BaseKind.Gate ? City() : receiver, current));
            case 7: return Number(BasePostLoad.ReadSuppliesCapacity(receiver.Kind == BaseKind.Gate ? City() : receiver, current));
            case 8: return Number(BasePostLoad.ReadTroopCapacity(receiver, current));
            case 9 or 10:
            {
                int stock = Currency(mode == 9 ? BaseResourceKind.Gold : BaseResourceKind.Supplies);
                int preference = TroopsInfoActorWord(state, current, actor, mode == 9 ? 0x170 : 0x172);
                int factor = BaseInfoParameter(state, "base-currency-preservation-percent");
                return Number(unchecked(stock - unchecked(unchecked(stock * factor) * preference) / 10000));
            }
            case 11:
            {
                int stock = BaseInfoNeed(current.ReadBaseResource(receiver, BaseResourceKind.Troops), "Base_troops3C");
                int preference = TroopsInfoActorWord(state, current, actor, 0x16A);
                int factor = BaseInfoParameter(state, "base-ready-personality-percent");
                return Number(unchecked(stock + unchecked(unchecked(factor * preference) * stock) / 10000));
            }
            case 12: return Number(BaseInfoNeed(current.ReadArmyReturnBaseWounded40(receiver), "Base_wounded40"));
            case 22: return Number(BaseInfoNeed(current.ReadBaseMorale44(receiver), "Base_morale44"));
            case 23:
            {
                int raw = BaseInfoNeed(current.ReadBaseObjectLink(City()), "mapped_City_Object4E");
                var obj = current.ReadObjectTableReference(raw < 3001 ? raw : 0) ?? throw BaseInfoMissing("City_Object_pointer");
                return Number(BaseInfoNeed(current.ReadObjectNativeValidity(obj), "City_Object_validity")
                    ? BaseInfoNeed(current.ReadObjectRawField14(obj), "City_Object_durability14") : 6000);
            }
            case 26:
                return new BaseInformationEntities(WorldEntityKind.City,
                    AiForceInformationNeighbors(current, receiver).Select(at => AiForceInformationBaseEntity(current, at)).ToArray());
            case 27:
            {
                int district = BaseInfoNeed(current.ReadBaseDistrictSlot(receiver), "Base_District");
                return People(EnumerateBaseResidents(current, checked((ushort)Location()), BaseInfoMissing)
                    .Where(person => BaseInfoNeed(current.ReadPersonDistrictSlot(person), "resident_DistrictE0") == district));
            }
            case 29:
            {
                int city = BaseInfoNeed(City().PhysicalSlot, "mapped_City_identity");
                int force = BaseInfoNeed(current.ReadBaseForceSlot(receiver), "Base_Force");
                var result = new List<OriginalAiEntity>();
                foreach (var item in current.ReadRegisteredAreaReferences() ?? throw BaseInfoMissing("Area_registryB0"))
                {
                    var area = item ?? throw BaseInfoMissing("Area_registry_pointer");
                    if (city is < 1 or > 51 || BaseInfoNeed(current.ReadAreaCitySlot(area), "Area_City35") != city) continue;
                    if (BaseInfoNeed(current.ReadAreaCurrentForceSlot(area), "Area_Force") == force)
                        result.Add(new(WorldEntityKind.Area, BaseInfoNeed(area.PhysicalSlot, "Area_identity")));
                }
                return new BaseInformationEntities(WorldEntityKind.Area, result);
            }
            case 31:
                return new BaseInformationEntities(WorldEntityKind.Army, BaseInfoArmies(state, current, receiver, null).ToArray());
            case 32: return People(BaseInfoAvailablePeople(state, current, receiver));
            case 34: return Number(Currency(BaseResourceKind.Gold));
            case 35: return Number(Currency(BaseResourceKind.Supplies));
            case 36: return Number(BaseInfoNeed(current.ReadBaseResource(receiver, BaseResourceKind.Troops), "Base_troops3C"));
            case 37:
            {
                int raw = BaseInfoNeed(current.ReadBaseForceSlot(receiver), "morale_Base_Force");
                var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw BaseInfoMissing("morale_Force_pointer");
                return Number(BasePostLoad.ReadMoraleUpper(force, current));
            }
            case 38 or 39:
            {
                var memory = state.OriginalAiStrategyMemory?.Footholds.GetValueOrDefault(Location())
                    ?? throw BaseInfoMissing("Foothold_planning_owner");
                return Number(BaseInfoNeed(mode == 38 ? memory.FoodNeed : memory.GoldNeed,
                    mode == 38 ? "Foothold_FoodNeed94" : "Foothold_GoldNeed98"));
            }
            case 41:
            {
                //229EC0 returns the actual Area pointer cached by the crisis
                //route producer. A missing cache is unknown, not a null Area.
                var memory = state.OriginalAiStrategyMemory?.Footholds.GetValueOrDefault(Location())
                    ?? throw BaseInfoMissing("Foothold_planning_owner");
                int area = BaseInfoNeed(memory.FriendlyRouteAreaId, "Foothold_friendly_route_Area");
                return new BaseInformationEntity(area == 0 ? null : new(WorldEntityKind.Area, area));
            }
            case 43:
            {
                int province = BaseInfoNeed(_growthQueries.ReadCityState(state,
                    BaseInfoNeed(City().PhysicalSlot, "region_City_identity")), "City_provinceCE");
                province = province < 23 ? province : 0;
                int region = BaseInfoNeed(BaseCaptureRewardInputs.ReadStateRegion(state, province), "Province_region30");
                return new BaseInformationRegion(region < 18 ? region : 0);
            }
            case 44:
            {
                var result = new List<OriginalAiEntity>();
                var city = City();
                int id = BaseInfoNeed(city.PhysicalSlot, "investment_City_identity");
                int force = BaseInfoNeed(current.ReadBaseForceSlot(city), "investment_City_Force");
                //The fixed 101-receiver table is different from the scenery registry.
                for (int slot = 0; slot < 101; slot++)
                {
                    var at = current.ReadSceneryTableReference(slot) ?? throw BaseInfoMissing("investment_Scenery_table");
                    if (!BaseInfoNeed(current.ReadSceneryNativeValidity(at), "investment_Scenery_validity") ||
                        (BaseInfoNeed(current.ReadSceneryFlags47(at), "investment_Scenery_flags47") & 1) == 0) continue;
                    if (InvestmentCity(current, at) != id || BaseInfoNeed(current.ReadConditionSceneryForceSlot(at), "investment_Scenery_Force") != force) continue;
                    int actual = BaseInfoNeed(at.PhysicalSlot, "investment_Scenery_identity");
                    var owner = InvestmentOwner(state, actual);
                    var growth = owner.Development ?? throw BaseInfoMissing("investment_Scenery_development");
                    if (owner.Phase == 1 && growth.Level is >= 1 and <= 5 && owner.InvestmentDays == 0 && owner.CompletionDay < 1)
                        result.Add(new(WorldEntityKind.Landmark, actual));
                }
                return new BaseInformationEntities(WorldEntityKind.Landmark, result);
            }
            case 45:
            {
                int raw = BaseInfoNeed(current.ReadBaseForceSlot(receiver), "forecast_Force");
                var force = current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw BaseInfoMissing("forecast_Force_pointer");
                return BaseInfoNeed(current.ReadForceNativeValidity(force), "forecast_Force_validity")
                    ? Number(BaseInfoForecastPersons(state, current, receiver)) : new BaseInformationFailure();
            }
            default: throw BaseInfoMissing("Foothold_selector_" + mode);
        }
    }

    private IEnumerable<int> BaseInfoAvailablePeople(GameState state, SourceWorldContext current, BaseReference home)
    {
        ushort location = BaseInfoNeed(current.ReadBaseObjectLink(home), "available_Base4E");
        foreach (int person in EnumerateBaseResidents(current, location, BaseInfoMissing))
        {
            if ((BaseInfoNeed(current.ReadPersonResetWord1FF(person), "available_Person1C7") & 255) != 0 ||
                BaseInfoNeed(current.ReadPersonSecondLocation(person), "available_PersonE4") != location ||
                (BaseInfoNeed(current.ReadOwnedReturnPersonFlags(person), "available_Person15E") & 1) != 0) continue;
            var linked = current.ReadPersonLinkedEntity(person) ?? throw BaseInfoMissing("available_linked_command");
            if (linked.Identity.HasValue) continue;
            //2A0BC0 removes only critical illness in a safe home and rulers
            //whose Force has no other ordinary non4000 serving person.
            if (BaseInfoNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "available_health161") == 3 && !BaseInfoNearbyEnemy(state, home)) continue;
            if (BaseInfoNeed(current.ReadPersonStatus(person), "available_PersonE6") == 1)
            {
                int force = BaseInfoNeed(current.ReadPersonOldForceSlot(person), "available_ruler_Force");
                bool successor = false;
                foreach (int other in NormalTurnServingPeople(current, force, 0, true))
                    if (BaseInfoNeed(current.ReadPersonStatus(other), "available_other_status") != 1 &&
                        !DuelAftermathSpecialPerson(current, other) &&
                        (BaseInfoNeed(current.ReadArmyReturnPersonFlags196(other), "available_other_flags") & 0x4000) == 0)
                    { successor = true; break; }
                if (!successor) continue;
            }
            //The final source pass excludes an actual valid associated task.
            if (BaseInfoNeed(current.ReadLinkedEntityNativeValidity(linked), "available_linked_validity")) continue;
            yield return person;
        }
    }

    private int BaseInfoForecastPersons(GameState state, SourceWorldContext current, BaseReference home)
    {
        int force = BaseInfoNeed(current.ReadBaseForceSlot(home), "forecast_Force");
        ushort location = BaseInfoNeed(current.ReadBaseObjectLink(home), "forecast_Base4E");
        int district = BaseInfoNeed(current.ReadBaseDistrictSlot(home), "forecast_Base_District");
        int count = EnumerateBaseResidents(current, location, BaseInfoMissing)
            .Count(person => BaseInfoNeed(current.ReadPersonDistrictSlot(person), "forecast_PersonE0") == district);
        var associated = new PersonReturnGovernorQueries(state, _governorInputs, current);
        foreach (int person in NormalTurnServingPeople(current, force, 0, true))
        {
            var linked = current.ReadPersonLinkedEntity(person) ?? throw BaseInfoMissing("forecast_Person_associated");
            if (!BaseInfoNeed(current.ReadLinkedEntityNativeValidity(linked), "forecast_associated_validity")) continue;
            if (BaseInfoNeed(associated.ReadCurrentAssociatedVirtual98(linked), "forecast_associated_kind") != 1) continue;
            var manager = current.ReadOwnedPersonCommandManager() ?? throw BaseInfoMissing("forecast_personnel_manager");
            var task = manager.Returns.GetValueOrDefault(BaseInfoNeed(linked.Identity, "forecast_associated_identity"))
                ?? throw BaseInfoMissing("forecast_reassignment_task");
            var destination = task.Destination;
            BaseReference? goal = destination is null ? null : BaseLookup.Find(destination.BaseId, current);
            bool goalValid = goal is not null && BaseInfoNeed(current.ReadBaseNativeValidity(goal), "forecast_destination_validity");
            var origin = BaseLookup.Find(BaseInfoNeed(current.ReadPersonFirstLocation(person), "forecast_PersonE2"), current);
            if (origin == home) count = unchecked(count - 1);
            else if (goalValid && goal == home) count = unchecked(count + 1);
        }
        return count;
    }

    private static bool BaseInfoNearbyEnemy(GameState state, BaseReference home)
    {
        int slot = BaseInfoNeed(home.PhysicalSlot, "nearby_Base_pointer");
        bool? value = home.Kind == BaseKind.City ? slot == 0 ? state.UnassignedCityHasNearbyEnemyArmy :
            state.Cities.GetValueOrDefault(slot)?.HasNearbyEnemyArmy : slot == 0 ? state.UnassignedGateHasNearbyEnemyArmy :
            state.Gates.GetValueOrDefault(slot + 51)?.HasNearbyEnemyArmy;
        return BaseInfoNeed(value, "Base_nearby_Army67");
    }

    private IEnumerable<OriginalAiEntity> BaseInfoArmies(GameState state, SourceWorldContext current, BaseReference? home, int? district)
    {
        int location = home is null ? 0 : BaseInfoNeed(current.ReadBaseObjectLink(home), "Army_list_Home4E");
        if (home is not null ? location is < 1 or > 61 : district is < 1 or > 51) yield break;
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw BaseInfoMissing("Army_registry60"))
        {
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int slot }) throw BaseInfoMissing("Army_registry_payload");
            var army = new ArmyUnitReference(slot);
            if (district.HasValue && ReadFacilityHistoryDistrict(current, army) != district.Value) continue;
            if (BaseInfoNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, slot), "Army_excluded233560")) continue;
            if (home is not null)
            {
                int person = TroopsInfoPerson(current, BaseInfoNeed(current.ReadArmyUnitCommanderId(army), "Army_Commander12"));
                int at = BaseInfoNeed(current.ReadPersonNativeValidity(person), "Army_Commander_validity")
                    ? BaseInfoNeed(current.ReadPersonFirstLocation(person), "Army_Commander_HomeE2") : 0;
                if (at != location) continue;
            }
            yield return TroopsInfoArmyEntity(state, army);
        }
    }

    private static OriginalAiEntity BaseInfoPersonEntity(SourceWorldContext current, int raw)
    {
        int physical = TroopsInfoPerson(current, raw);
        return new(WorldEntityKind.Officer, BaseInfoNeed(current.ReadPersonIdentity(physical), "Person_identity"));
    }
    private static int BaseInfoParameter(GameState state, string name) => state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue("original-ai." + name, out int value) == true
        ? value : throw BaseInfoMissing("parameter." + name);
    private static T BaseInfoNeed<T>(T? value, string field) where T : struct => value ?? throw BaseInfoMissing(field);
    private static InvalidDataException BaseInfoMissing(string field) => new("original_ai.base_information." + field);
}
