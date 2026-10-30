using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Explicit fresh3035A0 boundary. The complete working result is
    /// published only after each City's300FE0(1) and302F20 finish. Loading and
    /// ordinary page queries never invoke this initializer.</summary>
    public AreaAdministrationQuery<GameState> InitializeFreshCityMerchants(GameState state)
    {
        try
        {
            if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 })
                throw CityMerchantSource.Missing("fresh_lifecycle");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            if (!_baseCapacityInputs.BindCurrent(state, current)) throw CityMerchantSource.Missing("fresh_B8_owner");
            var working = state;
            foreach (var item in current.ReadRegisteredBases() ?? throw CityMerchantSource.Missing("fresh_Base80"))
            {
                var city = item ?? throw CityMerchantSource.Missing("fresh_Base_pointer");
                int link = CityMerchantSource.K(current.ReadBaseObjectLink(city), "fresh_Base4E");
                if (link >= 52) continue;
                int id = CityMerchantSource.K(city.PhysicalSlot, "fresh_City_pointer");
                var owner = ReadCityMerchantDomainOwner(working, city, link, id, "fresh");
                if (owner is not null)
                {
                    if (owner.MerchantFoodPerTenGold is not null) throw CityMerchantSource.Missing("fresh_already_initialized");
                    //226CD0's actual fresh constructor value. It is never a
                    //fallback for an unknown existing city or historical save.
                    working = working with { Cities = new(working.Cities) { [id] = owner with { MerchantFoodPerTenGold = 10 } } };
                }
                //3035A0 also visits the registered fallback City0. It has no
                //playable CityState, but its C9 writer and all13 App draws run
                //before City1. Never remove it by filtering native validity.
                working = RefreshCityMerchantAppearance(working, current, city, true);
                working = RefreshCityMerchantPrice(working, current, city);
            }
            return AreaAdministrationQuery<GameState>.Known(working);
        }
        catch (InvalidDataException missing) { return AreaAdministrationQuery<GameState>.Unknown(missing.Message); }
    }

    private GameState RefreshCityMerchantMonth(GameState state, SourceWorldContext current, BaseReference receiver)
    {
        int link = CityMerchantSource.K(current.ReadBaseObjectLink(receiver), "month_Base4E");
        if (link >= 52) return state;
        int id = CityMerchantSource.K(receiver.PhysicalSlot, "month_City_pointer");
        var owner = ReadCityMerchantDomainOwner(state, receiver, link, id, "month");
        //An unknown old playable City's quote is not initialized by loading.
        //City0 instead always runs the source writers when the Turn visits it.
        if (owner is not null && owner.MerchantFoodPerTenGold is null) return state;
        if (!_baseCapacityInputs.BindCurrent(state, current)) throw CityMerchantSource.Missing("month_B8_owner");
        state = RefreshCityMerchantAppearance(state, current, receiver, false);
        return RefreshCityMerchantPrice(state, current, receiver);
    }

    private static CityState? ReadCityMerchantDomainOwner(GameState state, BaseReference receiver,
        int link, int id, string operation)
    {
        if (receiver.Kind == BaseKind.City && id == 0 && link == 0) return null;
        if (receiver.Kind == BaseKind.City && id is >= 1 and <= 51 && state.Cities.TryGetValue(id, out var owner))
            return owner;
        throw CityMerchantSource.Missing(operation + "_City_owner_kind_" + receiver.Kind + "_physical_" + id + "_link_" + link);
    }

    private GameState RefreshCityMerchantAppearance(GameState state, SourceWorldContext current,
        BaseReference receiver, bool fresh)
    {
        int location = CityMerchantSource.K(current.ReadBaseObjectLink(receiver), "appearance_Base4E");
        if (location >= 52) return state;
        if (state.CampaignEventWorld?.CurrentUserIndex is not sbyte user) throw CityMerchantSource.Missing("appearance_App_user");
        if (user != -1) return state;
        var mode = current.ArmyReturnTransferMode ?? throw CityMerchantSource.Missing("appearance_570850");
        if (mode.Present && CityMerchantSource.K(mode.Value, "appearance_570850_value") != 0) return state;
        int id = CityMerchantSource.K(receiver.PhysicalSlot, "appearance_City_pointer");
        byte size = CityMerchantSource.K(current.ReadCitySizeB8(receiver), "appearance_SizeB8");
        var parameters = new CityMerchantRuleParameters(state, Seed.Language);
        int basis = parameters.Read("appearance-base");
        int chance = Math.Clamp(unchecked(size * parameters.Read("appearance-size-scale") + basis), 0, 100);
        var date = state.NativeMapSeason?.ThirdDate ?? throw CityMerchantSource.Missing("appearance_ThirdDate");
        int gold = CityMerchantSource.K(current.ReadBaseResource(receiver, BaseResourceKind.Gold), "appearance_Gold");
        int food = CityMerchantSource.K(current.ReadBaseResource(receiver, BaseResourceKind.Supplies), "appearance_Food");
        int troops = CityMerchantSource.K(current.ReadBaseResource(receiver, BaseResourceKind.Troops), "appearance_Ready3C");
        //The never-playable fallback receiver has no CityState overlay.
        //Its qword3C constructor clears40 and its static word2E is loaded from
        //City row0; native-invalid Base0 cannot acquire an active Governor or
        //wounded-troop writer. Ordinary Cities require their saved current fields.
        bool fallback = receiver.Kind == BaseKind.City && id == 0 && location == 0;
        int wounded = fallback ? 0 : CityMerchantSource.K(current.ReadArmyReturnBaseWounded40(receiver), "appearance_Wounded40");
        byte morale = CityMerchantSource.K(current.ReadBaseMorale44(receiver), "appearance_Morale44");
        ushort governor = fallback ? _definitions.ReadFreshReturnGovernor2E(0)
            : CityMerchantSource.K(current.ReadArmyReturnBaseGovernor2E(receiver), "appearance_Governor2E");
        int link = CityMerchantSource.K(current.ReadBaseObjectLink(receiver), "appearance_fresh_Base4E");
        var obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw CityMerchantSource.Missing("appearance_Object_pointer");
        int hp = CityMerchantSource.K(current.ReadObjectNativeValidity(obj), "appearance_Object_validity")
            ? CityMerchantSource.K(current.ReadObjectRawField14(obj), "appearance_Object14") : 6000;
        int sum = unchecked(gold + food + hp + morale + wounded + troops);
        var world = Context(current).ReadCurrentSamplerWorld(state) ?? throw CityMerchantSource.Missing("appearance_World450_45C");
        int draw = TacticEffectSourceSamplerMath.Sample(100, 1, unchecked((short)date.Year + date.Month), id, governor, sum, world);
        if (fresh)
        {
            var random = new SharedSourceRandom32(state.CampaignAppRandom32?.State ?? throw CityMerchantSource.Missing("appearance_App32"));
            draw = random.Next(100);
            state = state with { CampaignAppRandom32 = new(random.State) };
        }
        var owner = state.NativeBaseCaptureCurrent ?? throw CityMerchantSource.Missing("appearance_C9_owner");
        var city = owner.Cities.GetValueOrDefault(id) ?? throw CityMerchantSource.Missing("appearance_City_C9");
        byte before = city.C9 ?? throw CityMerchantSource.Missing("appearance_C9");
        byte after = (byte)((before & 0xFE) | (draw < chance ? 1 : 0));
        current.SetCaptureCityByteC9(id, after);
        return state with { NativeBaseCaptureCurrent = owner with { Cities = new(owner.Cities) { [id] = city with { C9 = after } } } };
    }
    //2FAC50 is a Turn writer, after Progress, over the actual manager100
    //chain. It clears the source used/turn bits; it is not a new campaign reset.
    private static GameState ResetCityMerchantPeopleForTurn(GameState state, SourceWorldContext current)
    {
        Dictionary<int, OfficerState>? people = null;
        foreach (int? item in current.ReadRegisteredPersonPhysicalSlots() ?? throw CityMerchantSource.Missing("Turn_Person100"))
        {
            int p = CityMerchantSource.K(item, "Turn_Person_pointer");
            ushort flags = CityMerchantSource.K(current.ReadArmyReturnPersonFlags196(p), "Turn_Person15E");
            ushort after = (ushort)(flags & 0x7FFC);
            current.WriteArmyReturnPersonFlags196(p, after);
            if (after == flags) continue;
            int id = CityMerchantSource.K(current.ReadPersonIdentity(p), "Turn_Person_identity");
            var person = state.Officers.GetValueOrDefault(id) ?? throw CityMerchantSource.Missing("Turn_Person_owner");
            people ??= new(state.Officers);
            people[id] = person with { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(person.Behavior, after) };
        }
        return people is null ? state : state with { Officers = people };
    }

    private static GameState ResetCityMerchantCityForTurn(GameState state, SourceWorldContext current, int id)
    {
        if (id >= 52) return state;
        var owner = state.NativeBaseCaptureCurrent ?? throw CityMerchantSource.Missing("Turn_C9_owner");
        var city = owner.Cities.GetValueOrDefault(id) ?? throw CityMerchantSource.Missing("Turn_City_C9");
        byte before = city.C9 ?? throw CityMerchantSource.Missing("Turn_C9");
        byte after = (byte)(before & 0xFB);
        current.SetCaptureCityByteC9(id, after);
        return state with { NativeBaseCaptureCurrent = owner with { Cities = new(owner.Cities) { [id] = city with { C9 = after } } } };
    }

    //302F20 consumes all twelve draws before selecting the current month.
    //Both editions use their independently extracted immutable parameters;
    //missing historical city quotes remain untouched by the caller above.
    private GameState RefreshCityMerchantPrice(GameState state, SourceWorldContext current, BaseReference receiver)
    {
        int location = CityMerchantSource.K(current.ReadBaseObjectLink(receiver), "price_Base4E");
        if (location >= 52) return state;
        if (state.CampaignEventWorld?.CurrentUserIndex is not sbyte user) throw CityMerchantSource.Missing("price_App_current_user");
        if (user != -1) return state;
        int id = CityMerchantSource.K(receiver.PhysicalSlot, "price_City_pointer");
        var city = ReadCityMerchantDomainOwner(state, receiver, location, id, "price");
        int month = state.NativeMapSeason?.ThirdDate.Month ?? throw CityMerchantSource.Missing("price_ThirdDate");
        var random = new SharedSourceRandom32(state.CampaignAppRandom32?.State ?? throw CityMerchantSource.Missing("price_App32"));
        var parameters = new CityMerchantRuleParameters(state, Seed.Language);
        int winter = parameters.Read("price-winter-base"), spring = parameters.Read("price-spring-base"),
            june = parameters.Read("price-june-base"), summer = parameters.Read("price-summer-base"),
            autumn = parameters.Read("price-autumn-base");
        int wide = parameters.Read("price-wide-random-bound"), narrow = parameters.Read("price-narrow-random-bound"),
            step = parameters.Read("price-random-step");
        int[] basis = [winter, winter, winter, spring, spring, june, summer, summer, summer, autumn, autumn, autumn];
        int[] bounds = [wide, narrow, narrow, wide, wide, wide, wide, narrow, narrow, wide, narrow, narrow];
        int[] monthly = new int[12];
        for (int n = 0; n < monthly.Length; n++) monthly[n] = unchecked(basis[n] + random.Next(bounds[n]) * step);
        int drought = 100, locust = 100;
        foreach (var area in current.ReadRegisteredAreaReferences() ?? throw CityMerchantSource.Missing("price_AreaB0"))
        {
            var entry = area ?? throw CityMerchantSource.Missing("price_Area_pointer");
            if (id is < 1 or > 51 || CityMerchantSource.K(current.ReadAreaCitySlot(entry), "price_Area35") != id) continue;
            byte flags = CityMerchantSource.K(current.ReadAreaFlags38(entry), "price_Area38");
            if ((flags & 4) != 0) drought = parameters.Read("price-drought-percent");
            if ((flags & 0x10) != 0) locust = parameters.Read("price-locust-percent");
        }
        int value = (int)((long)monthly[Math.Clamp(month - 1, 0, 11)] * drought * locust / 10000);
        //Fallback City0's C8 is only a local writer result: no playable city
        //or valid trade consumer reads it. Preserve all twelve draws without
        //inventing a DomainCity or aliasing that result to another City.
        return city is null ? state with { CampaignAppRandom32 = new(random.State) }
            : state with { CampaignAppRandom32 = new(random.State), Cities = new(state.Cities)
                { [id] = city with { MerchantFoodPerTenGold = (byte)Math.Clamp(value, 0, 255) } } };
    }
}
