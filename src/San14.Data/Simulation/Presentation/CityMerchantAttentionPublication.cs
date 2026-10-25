using San14.Core;

namespace San14.Data;

public sealed record CityMerchantAttention(int CityId, bool? Attention, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>446260's merchant attention, polled by1ABE20. The host
    /// supplies the actual4466C0 UI gate (UserStrategy stage2 and not6E8).
    /// An absent UI owner is unknown, rather than a disabled merchant.</summary>
    public AreaAdministrationQuery<IReadOnlyList<CityMerchantAttention>> QueryCityMerchantAttention(
        GameState state, bool? userStrategyAttentionEnabled)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (userStrategyAttentionEnabled is null)
            return AreaAdministrationQuery<IReadOnlyList<CityMerchantAttention>>.Unknown(
                "city.merchant-attention.UserStrategy_stage2_and_not_busy");
        if (!userStrategyAttentionEnabled.Value)
            return AreaAdministrationQuery<IReadOnlyList<CityMerchantAttention>>.Known(
                state.Cities.Keys.Order().Select(id => new CityMerchantAttention(id, false, null)).ToArray());
        try
        {
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            current.RestoreOwnedArmyReturnSlots(state, state.ReadArmyLifecycle()
                ?? throw MerchantAttentionMissing("army_lifecycle"));
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            var reader = new MerchantAttentionReader(this, state, current);
            return AreaAdministrationQuery<IReadOnlyList<CityMerchantAttention>>.Known(
                state.Cities.Keys.Order().Select(reader.Read).ToArray());
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<CityMerchantAttention>>.Unknown(error.Message); }
    }

    private sealed class MerchantAttentionReader(ArmyCampaignRuleAdapter owner,
        GameState state, SourceWorldContext current)
    {
        private bool capacityBound;

        internal CityMerchantAttention Read(int cityId)
        {
            try { return new(cityId, Available(cityId), null); }
            catch (InvalidDataException error) { return new(cityId, null, error.Message); }
        }

        private bool Available(int cityId)
        {
            var city = current.ReadCityTableReference(cityId) ?? throw Missing("City_pointer");
            var district = current.ReadCityDistrictReference(city) ?? throw Missing("City_District_pointer");
            //233940→233A90: player-owned District whose actual leader is
            //a valid ruler, not every District belonging to the player Force.
            if (!BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, Missing)) return false;
            if (!Need(current.ReadBaseNativeValidity(city), "City_validity") ||
                !Need(current.ReadDistrictNativeValidity(district), "District_validity")) return false;
            if (Need(current.ReadBaseObjectLink(city), "City4E") is >= 52 and <= 61) return false;
            int districtId = Need(district.PhysicalSlot, "District_physical");
            int books = state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining ?? throw Missing("District_books14");
            if ((uint)books > byte.MaxValue) throw Missing("District_books14_byte");
            if (books == 0) return false;
            byte flags = state.NativeBaseCaptureCurrent?.Cities.GetValueOrDefault(cityId)?.C9 ?? throw Missing("CityC9");
            if ((flags & 1) == 0) return false;
            int gold = Need(current.ReadBaseResource(city, BaseResourceKind.Gold), "City_Gold34");
            int food = Need(current.ReadBaseResource(city, BaseResourceKind.Supplies), "City_Food38");
            if (!capacityBound)
            {
                if (!owner._baseCapacityInputs.BindCurrent(state, current)) throw Missing("current_Base_capacity");
                capacityBound = true;
            }
            //The attention uses raw positive stock and free capacity. It
            //does NOT quote a price, buy/sell amount, or reserve more books.
            bool canBuy = gold > 0 && food < BasePostLoad.ReadSuppliesCapacity(city, current);
            bool canSell = gold < BasePostLoad.ReadGoldCapacity(city, current) && food > 0;
            if (!canBuy && !canSell) return false;
            return HasExecutor(city, districtId) && (flags & 4) == 0;
        }

        //6D99B0's same-current predicate. Its order is DistrictPersonIterator
        //mode0 (status1..4), then validity/special/busy/association/Army/health,
        //then actual Home City or a registered Gate governed by that City.
        //Only list nonemptiness is consumed by446260: no ability/name/card
        //calculation and no second capture is necessary.
        private bool HasExecutor(BaseReference city, int district)
        {
            int link = Need(current.ReadBaseObjectLink(city), "executor_City4E");
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person_registry100"))
            {
                int person = Need(entry, "Person_pointer");
                if (Need(current.ReadPersonDistrictSlot(person), "Person_E0") != district ||
                    Need(current.ReadPersonStatus(person), "Person_E6") is < 1 or > 4) continue;
                if (!Need(current.ReadPersonNativeValidity(person), "Person_validity")) continue;
                int id = Need(current.ReadPersonIdentity(person), "Person_identity10");
                if (Special(person, id) || (Need(current.ReadArmyReturnPersonFlags196(person), "Person_flags15E") & 1) != 0) continue;
                var association = current.ReadPersonLinkedEntity(person) ?? throw Missing("Person_association");
                if (Need(current.ReadLinkedEntityNativeValidity(association), "Person_association_validity")) continue;
                if (Need(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, person), "Person_commander_Army")) continue;
                if (Need(OfficerHealthSourceAdapter.ReadByte(current, person), "Person_health161") is 2 or 3) continue;
                int home = Need(current.ReadPersonFirstLocation(person), "Person_HomeE2");
                if (home == link) return true;
                if (link >= 52) continue;
                //22D110 scans the registered Gate chain, compares actual
                //Gate→City pointer, then compares that Gate's Base4E.
                foreach (var gate in current.ReadArmyReturnRegisteredGates() ?? throw Missing("Gate_registryA0"))
                {
                    if (gate is null) throw Missing("Gate_registry_payload");
                    if (CurrentGateMapQueries.FindCity(gate, current) != city) continue;
                    if (Need(current.ReadBaseObjectLink(gate), "Gate4E") == home) return true;
                }
            }
            return false;
        }

        private bool Special(int person, int id)
        {
            //2C7250's actual five native ID ranges, then233240(mode0).
            if (!new[] { 4111, 4131, 4151, 4171, 4191 }.Any(first => unchecked((uint)(id - first)) <= 9)) return false;
            int home = Need(current.ReadPersonFirstLocation(person), "special_HomeE2");
            if (home is < 1 or > 61) return false;
            var force = current.ReadPersonForce(person) ?? throw Missing("special_Force");
            return Need(current.ReadForcePhysicalSlot(force), "special_Force_ordinal") is not (>= 46 and <= 50);
        }

        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => MerchantAttentionMissing(field);
    }

    private static InvalidDataException MerchantAttentionMissing(string field) => new("city.merchant-attention." + field);
}
