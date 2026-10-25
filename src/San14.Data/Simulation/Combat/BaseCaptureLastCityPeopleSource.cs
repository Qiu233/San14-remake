using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2851B0 last-City noncaptured loop: the actual2EACB0 recipient is the
    //winning Force (TC287BDF moves it into RDX), followed by265250(null,null).
    internal GameState ApplyLastCityNoncapturedPersonCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int personPhysical,
        ForceReference winner, NativeMapSeasonDate? sourceDate,
        Func<TacticSourceSamplerWorld> readSampler, List<CampaignEffectRequest> effects)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw LastCityPersonMissing("current_pool_binding");
        if (LastCityPersonNeed(current.ReadPersonNativeValidity(personPhysical), "item_Person_validity") &&
            LastCityPersonNeed(current.ReadForceNativeValidity(winner), "item_recipient_Force_validity"))
        {
            int ruler = TalentRulerCurrent(current, winner);
            if (ruler is >= 1 and <= 6000)
                state = TransferLastCityPersonItemsCurrent(state, current, personPhysical, (ushort)ruler);
        }
        int Day() => (ReadArmyDestructionSampler(state, current)
            ?? throw LastCityPersonMissing("history_World450")).Day450;
        return ApplyCaptiveFreedomCurrent(state, current, pool, personPhysical, sourceDate,
            Day, readSampler, effects);
    }

    //2EACB0 writes only flag3 and holder4A. It does not acquire/register an
    //item, change its status/location, or transfer protected bit0 equipment.
    private static GameState TransferLastCityPersonItemsCurrent(GameState state,
        SourceWorldContext current, int person, ushort recipient)
    {
        int id = LastCityPersonNeed(current.ReadPersonIdentity(person), "items_Person10");
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Item) ?? throw LastCityPersonMissing("items_descriptor");
        var node = current.ReadRegistryHead(descriptor) ?? throw LastCityPersonMissing("items_head");
        Dictionary<int, ItemState>? changed = null;
        while (node.Identity.HasValue)
        {
            var item = current.ReadRegistryNodePayload(node) ?? throw LastCityPersonMissing("items_payload");
            if (item is not { Kind: RegistryKind.Item, PhysicalSlot: int physical })
                throw LastCityPersonMissing("items_pointer");
            if (id is >= 1 and <= 6000 && LastCityPersonNeed(current.ReadCaptureItemOwner4A(physical), "items_4A") == id)
            {
                var owner = (changed ?? state.Items)?.GetValueOrDefault(physical) ?? throw LastCityPersonMissing("items_owner");
                byte flags = LastCityPersonNeed(owner.Lifecycle?.RetainedFlags, "items_4F");
                if ((flags & 1) == 0)
                {
                    current.SetCaptureItemOwner4A(physical, recipient);
                    (changed ??= new(state.Items ?? throw LastCityPersonMissing("items_inventory")))[physical] = owner with
                    { Holder = new(recipient), Lifecycle = owner.Lifecycle! with { RetainedFlags = (byte)(flags & 0xF7) } };
                }
            }
            node = current.ReadRegistryNodeNext(node) ?? throw LastCityPersonMissing("items_next");
        }
        return changed is null ? state : state with { Items = changed };
    }
    //287960's two advantage slots can recall foreign auxiliary personnel.
    //The ordinary empty/mixed-owner branches have no writer. A real
    //nonempty allocation feeds the shared2CD1C0 recall operation.
    internal GameState ApplyLastCitySpecialPersonnelReturnCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference target, int oldForce,
        NativeMapSeasonDate? date, List<CampaignEffectRequest> effects)
    {
        var city = CurrentGateMapQueries.FindCity(target, current); //22A940.
        int cityId = LastCityPersonNeed(city.PhysicalSlot, "special_City_pointer");
        int provinceRaw = LastCityPersonNeed(_growthQueries.ReadCityState(state, cityId), "special_CityCE");
        var province = current.ReadStaticTableReference(0xDC80, provinceRaw < 23 ? provinceRaw : 0)
            ?? throw LastCityPersonMissing("special_Province_pointer");
        int provinceId = LastCityPersonNeed(province.PhysicalSlot, "special_Province_identity");
        if (provinceId == 0) return state;
        int advantageRaw = LastCityPersonNeed(_growthQueries.ReadStateAdvantage(state, provinceId), "special_Province30");
        var advantage = current.ReadStaticTableReference(0x85990, advantageRaw < 18 ? advantageRaw : 0)
            ?? throw LastCityPersonMissing("special_Advantage_pointer");
        int advantageId = LastCityPersonNeed(advantage.PhysicalSlot, "special_Advantage_identity");
        if (advantageId == 0) return state;
        for (int index = 0; index < 2; index++)
        {
            if (LastCityPersonNeed(_growthQueries.ReadAdvantageEffect(state, advantageId, index), "special_Advantage_effect") != 1) continue;
            string magnitudeKey = PersonGrowthCurrentQueries.MagnitudeKey(advantageId, index);
            var parameters = state.NativeArmySimulation?.Parameters ?? throw LastCityPersonMissing("special_parameters");
            if (parameters.Float32Values.ContainsKey(magnitudeKey) ||
                !parameters.Int32Values.TryGetValue(magnitudeKey, out int raw) || (uint)raw > 255)
                throw LastCityPersonMissing("special_Advantage_magnitude");
            var force = current.ReadForceTableReference(raw < 52 ? raw : 0)
                ?? throw LastCityPersonMissing("special_Force_pointer");
            if (!LastCityPersonNeed(current.ReadForceNativeValidity(force), "special_Force_validity")) continue;
            int forceId = LastCityPersonNeed(current.ReadForcePhysicalSlot(force), "special_Force60");
            var temporary = current.CreateTemporaryHexList() ?? throw LastCityPersonMissing("special_temporary");
            try
            {
                current.ClearTemporaryHexList(temporary);
                //2B0290's selected ten of each twenty-person native family,
                //in source lookup order. A failed append remains suppressed.
                if (forceId is >= 46 and <= 50)
                    for (int lookup = 4101; lookup <= 4200; lookup++)
                    {
                        if ((lookup - 4101) % 20 < 10) continue;
                        int physical = LastCityPersonNeed(current.ReadPersonLookupReference(lookup)?.PhysicalSlot, "special_Person_lookup");
                        if (!LastCityPersonNeed(current.ReadPersonNativeValidity(physical), "special_Person_validity") ||
                            LastCityPersonNeed(current.ReadPersonStatus(physical), "special_PersonE6") is < 1 or > 4) continue;
                        int id = LastCityPersonNeed(current.ReadPersonIdentity(physical), "special_Person10");
                        int family = id is >= 4101 and <= 4200 ? 46 + (id - 4101) / 20 :
                            id is >= 4001 and <= 4030 ? 46 + (id - 4001) / 6 : 0;
                        if (family == forceId) current.AppendPersonReturnTemporary(temporary, physical);
                    }
                bool allOldForce = true;
                var item = current.ReadRegistryHead(new(temporary.Identity)) ?? throw LastCityPersonMissing("special_head");
                while (item.Identity.HasValue)
                {
                    var person = current.ReadRegistryNodePayload(item) ?? throw LastCityPersonMissing("special_payload");
                    if (person is not { Kind: RegistryKind.Person, PhysicalSlot: int physical }) throw LastCityPersonMissing("special_Person_pointer");
                    if (LastCityPersonNeed(current.ReadPersonOldForceSlot(physical), "special_Person_Force") != oldForce)
                    { allOldForce = false; break; }
                    item = current.ReadRegistryNodeNext(item) ?? throw LastCityPersonMissing("special_next");
                }
                if (allOldForce && LastCityPersonNeed(current.ReadTemporaryHexListCount(temporary), "special_count") != 0)
                {
                    //287B24..287B67: suppress final primary maintenance only
                    //for the captured Base's valid current Force with at most one City.
                    int rawCaptor = LastCityPersonNeed(current.ReadBaseForceSlot(target), "special_captor_Force");
                    var captor = current.ReadForceTableReference(rawCaptor < 52 ? rawCaptor : 0)
                        ?? throw LastCityPersonMissing("special_captor_Force_pointer");
                    bool suppressPrimary = LastCityPersonNeed(current.ReadForceNativeValidity(captor), "special_captor_validity") &&
                        LastCityPersonNeed(PersonArmyCaptureSource.ReadOwnedCityCount(current, captor), "special_captor_City_count") <= 1;
                    state = ApplySpecialPersonnelRecallCurrent(state, current, pool, temporary,
                        conditional: false, requireValidOriginalForce: true, suppressPrimary, date, effects);
                }
            }
            finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
        }
        return state;
    }

    private static T LastCityPersonNeed<T>(T? value, string field) where T : struct => value ?? throw LastCityPersonMissing(field);
    private static InvalidDataException LastCityPersonMissing(string field) => new("Base-capture.last-City-Person." + field);
}
