using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState CompleteMaximumLandmark(GameState state, SourceWorldContext current, SceneryReference landmark, int id,
        NativeArmyProgressStageMoment moment)
    {
        int rawHex = OccupationKnown(current.ReadSceneryHexIndex(landmark), "landmark_maximum_hex");
        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw OccupationMissing("landmark_maximum_hex_pointer");
        int owner = hex.PhysicalSlot is null ? 0 : OccupationKnown(current.ReadHexForceSlot(hex), "landmark_maximum_owner");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw OccupationMissing("landmark_maximum_player");
        if (OccupationKnown(current.ReadForceNativeValidity(player), "landmark_maximum_player_validity") &&
            OccupationKnown(current.ReadForcePhysicalSlot(player), "landmark_maximum_player_identity") == owner)
        {
            // The original evaluates the complete pool before selecting this
            // story. Earlier manager writes remain visible to later conditions.
            // Until all execution effects are supported, no scan prefix escapes.
            _ = ScanOrdinaryCampaignEventConditions(state, moment);
            throw OccupationMissing("landmark_maximum_event_execution_owner");
        }
        //The non-player branch does not execute or date a story.3E12A0 sets
        //only the independent event manager bit, even for a neutral landmark.
        var manager = state.CampaignEventEligibility ?? throw OccupationMissing("landmark_event_eligibility_owner");
        var blocked = (bool[])manager.Blocked.Clone();
        blocked[id + 338] = true;
        return ApplyMaximumLandmarkRewards(state with { CampaignEventEligibility = manager with { Blocked = blocked } }, current, landmark, id, owner);
    }

    private GameState ApplyMaximumLandmarkRewards(GameState state, SourceWorldContext current, SceneryReference landmark, int id, int owner)
    {
        var investment = state.LandmarkInvestments![id];
        byte flags = OccupationKnown(investment.Flags, "landmark_current_flags");
        flags |= 4;
        var working = state with { LandmarkInvestments = new(state.LandmarkInvestments) { [id] = investment with { Flags = flags } } };
        current.SetSceneryFlags47(id, flags);
        var force = current.ReadForceTableReference(owner < 52 ? owner : 0) ?? throw OccupationMissing("landmark_reward_force");
        if (!OccupationKnown(current.ReadForceNativeValidity(force), "landmark_reward_force_validity")) return working;
        int forceId = OccupationKnown(current.ReadForcePhysicalSlot(force), "landmark_reward_force_identity");
        byte kind = investment.Development!.EffectKind;
        if (kind == 1)
        {
            var areas = new Dictionary<int, AreaState>(working.Areas);
            foreach (var reference in current.ReadRegisteredAreaReferences() ?? throw OccupationMissing("landmark_reward_area_registry"))
            {
                if (reference is null) throw OccupationMissing("landmark_reward_area_pointer");
                //CForceAreaIterator filters by the representative hex owner.
                if (forceId is < 1 or > 51 || OccupationKnown(current.ReadAreaCurrentForceSlot(reference), "landmark_reward_area_force") != forceId ||
                    !OccupationKnown(current.ReadAreaNativeValidity(reference), "landmark_reward_area_validity")) continue;
                int areaId = OccupationKnown(reference.PhysicalSlot, "landmark_reward_area_identity");
                var old = areas.GetValueOrDefault(areaId) ?? throw OccupationMissing("landmark_reward_area");
                int amount = Math.Clamp(unchecked(OccupationKnown(old.Security, "landmark_reward_security") +
                    CounterParameter(state, SimulationParameterKeys.Landmark.CompletionSecurity)), 0, 100);
                if (forceId is < 46 or > 50) areas[areaId] = old with { Security = amount };
            }
            working = working with { Areas = areas };
        }
        else if (kind is 2 or 3)
        {
            var cityCurrent = CaptureOwnedArmyReturnWorld(working).Context;
            var cities = new Dictionary<int, CityState>(working.Cities);
            //CForceCityIterator is the physical City table, never the combined
            //Base registry or the domain's dictionary enumeration order.
            for (int i = 0; i < 52; i++)
            {
                var city = cityCurrent.ReadCityTableReference(i) ?? throw OccupationMissing("landmark_reward_city_pointer");
                if (forceId is < 1 or > 51 || OccupationKnown(cityCurrent.ReadBaseForceSlot(city), "landmark_reward_city_force") != forceId ||
                    !OccupationKnown(cityCurrent.ReadBaseNativeValidity(city), "landmark_reward_city_validity")) continue;
                int cityId = OccupationKnown(city.PhysicalSlot, "landmark_reward_city_identity");
                var old = cities.GetValueOrDefault(cityId) ?? throw OccupationMissing("landmark_reward_city");
                if (kind == 2)
                {
                    int link = OccupationKnown(cityCurrent.ReadBaseObjectLink(city), "landmark_reward_city_object");
                    var obj = cityCurrent.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw OccupationMissing("landmark_reward_object_pointer");
                    if (!OccupationKnown(cityCurrent.ReadObjectNativeValidity(obj), "landmark_reward_object_validity")) continue;
                    int previous = OccupationKnown(cityCurrent.ReadObjectRawField14(obj), "landmark_reward_durability");
                    int cap = BasePostLoad.ReadCapacities(city, cityCurrent).Field46;
                    int amount = unchecked(previous + CounterParameter(state, SimulationParameterKeys.Landmark.CompletionDurability));
                    cityCurrent.WriteObjectPostLoadField14(obj, amount < 0 ? 0 : Math.Min(amount, cap));
                    cities[cityId] = old with { Durability = OccupationKnown(cityCurrent.ReadObjectRawField14(obj), "landmark_reward_durability_after") };
                }
                else
                {
                    int cap = BasePostLoad.ReadMoraleUpper(force, cityCurrent);
                    int amount = unchecked(OccupationKnown(cityCurrent.ReadBaseMorale44(city), "landmark_reward_morale") +
                        CounterParameter(state, SimulationParameterKeys.Landmark.CompletionMorale));
                    BasePostLoad.WriteMorale(city, amount < 0 ? 0 : Math.Min(amount, cap), cityCurrent);
                    byte after = OccupationKnown(cityCurrent.ReadBaseMorale44(city), "landmark_reward_morale_after");
                    var runtime = old.NativeReturnRuntime ?? throw OccupationMissing("landmark_reward_city_runtime");
                    cities[cityId] = old with { Morale = after, NativeReturnRuntime = runtime with { Morale44 = after } };
                }
            }
            working = working with { Cities = cities };
        }
        //223D40's signed popularity is the existing force development owner.
        if (forceId is >= 1 and <= 45)
        {
            var development = new OwnedAreaDevelopmentContext(working, current, _normalAftermathInputs);
            int old = OccupationKnown(development.ReadForceDevelopmentWord1BE(force), "landmark_reward_popularity");
            development.WriteForceDevelopmentWord1BE(force, (short)Math.Clamp(unchecked(old +
                CounterParameter(working, SimulationParameterKeys.Landmark.CompletionPopularity)), -1000, 1000));
            working = development.State;
        }
        return working;
    }
}
