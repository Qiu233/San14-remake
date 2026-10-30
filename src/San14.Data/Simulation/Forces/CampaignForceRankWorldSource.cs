using San14.Core;

namespace San14.Data;

internal sealed record ForceRankWorldContext(int ForcePhysicalSlot, int Rank, int RulerPhysicalSlot,
    int EmperorPhysicalSlot, bool SelfProclaimed, int ImperialOwnerForceSlot);

public sealed partial class ArmyCampaignRuleAdapter
{
    //Shared actual2B5ED0 prefix. A legitimate native rejection is null;
    //unknown current inputs retain their concrete owner.
    private ForceRankWorldContext? ReadForceRankWorldContext(GameState state, SourceWorldContext current,
        WorldIndexContext pool, ForceReference force, int rank)
    {
        if (!KingPromotionNeed(current.ReadForceNativeValidity(force), "Force_validity")) return null;
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw KingPromotionMissing("current_pool_binding");
        int forceId = KingPromotionNeed(current.ReadForcePhysicalSlot(force), "Force60");
        if (rank < 0) throw KingPromotionMissing("negative_Rank_table_selector");
        var definition = current.ReadStaticTableReference(0x78490, rank < 13 ? rank : 0)
            ?? throw KingPromotionMissing("rank_definition_pointer");
        if (definition.PhysicalSlot is null or 0) return null;
        if (!state.ForceRanks.ContainsKey(definition.PhysicalSlot.Value)) throw KingPromotionMissing("rank_definition");
        int ruler = KingPromotionLookup(current,
            KingPromotionNeed(current.ReadForceRecord(forceId)?.RawRuler10, "Force_ruler10"));
        if (!KingPromotionNeed(current.ReadPersonNativeValidity(ruler), "ruler_validity")) return null;
        int title = KingPromotionNeed(current.ReadForceKingdomNameIndex(forceId), "Force48");
        if (title is 6 or 44 or 45) return null;

        ushort emperorRaw = KingPromotionNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654");
        int emperor = KingPromotionLookup(current, emperorRaw);
        bool emperorPresent = KingPromotionNeed(current.ReadPersonNativeValidity(emperor), "emperor_validity");
        bool selfProclaimed = !emperorPresent;
        if (emperorPresent && rank == 1)
        {
            var position = PersonReturnCommands.ResolvePersonPosition(state, current, emperor,
                () => state.NativeMapSeason?.ThirdDate ?? throw KingPromotionMissing("World34_date"));
            //An invalid current Hex retains the source's imperial flag0.
            if (position.PhysicalSlot.HasValue)
                selfProclaimed = KingPromotionNeed(current.ReadHexForceSlot(position), "emperor_Hex14") != forceId;
        }
        //32D280 is the owner of the emperor's actual E4 City, including the
        //native Force0 fallback. It is independent of the selected player.
        int emperorCity = 0;
        if (emperorPresent)
        {
            var location = PersonPositionRepair.DecodeSecondLocation(
                KingPromotionNeed(current.ReadPersonSecondLocation(emperor), "emperor_E4"));
            if (location.Kind == 5) emperorCity = location.Index < 52 ? location.Index : 0;
        }
        var seat = current.ReadCityTableReference(emperorCity) ?? throw KingPromotionMissing("emperor_City_pointer");
        int imperialOwner = KingPromotionNeed(current.ReadBaseNativeValidity(seat), "emperor_City_validity")
            ? KingPromotionNeed(current.ReadBaseForceSlot(seat), "emperor_City_Force")
            : KingPromotionNeed((current.ReadForceTableReference(0) ?? throw KingPromotionMissing("Force0_pointer")).PhysicalSlot, "Force0_physical");
        return new(forceId, rank, ruler, emperor, selfProclaimed, imperialOwner);
    }

    //Naming is complete before entry. The caller owns the subsequent real
    //player message barrier and only then invokes the shared history tail.
    private GameState ApplyForceRankWorldCurrent(GameState state, SourceWorldContext current,
        ForceRankWorldContext context, List<CampaignEffectRequest> effects)
    {
        int forceId = context.ForcePhysicalSlot, rank = context.Rank, ruler = context.RulerPhysicalSlot;
        int imperialOwner = context.ImperialOwnerForceSlot;
        bool selfProclaimed = context.SelfProclaimed;
        var force = new ForceReference(forceId);
        int rulerId = KingPromotionNeed(current.ReadPersonIdentity(ruler), "ruler_identity");
        foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw KingPromotionMissing("Person100"))
        {
            int person = KingPromotionNeed(entry, "Person100_payload");
            //2349D0(filter0) retains the raw force and status1..4 predicates.
            if (KingPromotionNeed(current.ReadPersonOldForceSlot(person), "Person_Force") != forceId ||
                KingPromotionNeed(current.ReadPersonStatus(person), "PersonE6") is < 1 or > 4) continue;
            if (!KingPromotionNeed(current.ReadPersonNativeValidity(person), "Person_validity")) continue;
            int personId = KingPromotionNeed(current.ReadPersonIdentity(person), "Person10");
            if (personId == rulerId || KingPromotionProtectedKin(state, current, person, ruler)) continue;
            byte loyalty = KingPromotionNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person,
                _governorInputs), "PersonE8");
            if (loyalty > 100) continue;
            bool sameDoctrine = KingPromotionDoctrine(state, current, person) == KingPromotionDoctrine(state, current, ruler);
            //Immutable TC A884..A934: each family is granted same/different,
            //then self-proclaimed same/different (305660's true selector).
            int increase = rank switch
            {
                1 => selfProclaimed ? sameDoctrine ? 8 : 4 : sameDoctrine ? 12 : 8,
                2 => selfProclaimed ? sameDoctrine ? 6 : 3 : sameDoctrine ? 9 : 6,
                3 => selfProclaimed ? sameDoctrine ? 4 : 2 : sameDoctrine ? 6 : 4,
                >= 4 and <= 8 => selfProclaimed ? sameDoctrine ? 2 : 1 : sameDoctrine ? 3 : 2,
                _ => 0
            };
            byte after = (byte)Math.Min(100, unchecked(loyalty + increase));
            if (KingPromotionNeed(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)),
                "Person15E_bit13")) after = 255;
            current.SetPersonDestructionLoyaltyE8(person, after);
            state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
        }

        //The original nested ForceCity/CityArea iterators increase Area54
        //security, not Army morale. City table order and Area registry order
        //are retained, including repeated current pointer-table aliases.
        //TC A94C/A958/A964:5/10/15; emperor's city owner gets +10
        //only when becoming emperor. Lower ranks do not query Area54.
        int securityIncrease = rank switch { 1 => 15 + (imperialOwner == forceId ? 10 : 0), 2 => 10, 3 => 5, _ => 0 };
        for (int citySlot = 0; securityIncrease > 0 && citySlot < 52; citySlot++)
        {
            var city = current.ReadCityTableReference(citySlot) ?? throw KingPromotionMissing("City_table_pointer");
            if (KingPromotionNeed(current.ReadBaseForceSlot(city), "City_Force") != forceId) continue;
            if (!KingPromotionNeed(current.ReadBaseNativeValidity(city), "City_validity")) continue;
            int cityPhysical = KingPromotionNeed(city.PhysicalSlot, "City_physical");
            foreach (var entry in current.ReadRegisteredAreaReferences() ?? throw KingPromotionMissing("AreaB0"))
            {
                var area = entry ?? throw KingPromotionMissing("AreaB0_payload");
                if (cityPhysical is < 1 or > 51 ||
                    KingPromotionNeed(current.ReadAreaCitySlot(area), "Area35") != cityPhysical) continue;
                int cityForce = KingPromotionNeed(current.ReadBaseForceSlot(city), "City_current_Force");
                int areaForce = KingPromotionNeed(current.ReadAreaCurrentForceSlot(area), "Area_current_Force");
                if (areaForce != cityForce || !KingPromotionNeed(current.ReadAreaNativeValidity(area), "Area_validity")) continue;
                int physical = KingPromotionNeed(area.PhysicalSlot, "Area_physical");
                var owner = state.Areas.GetValueOrDefault(physical) ?? throw KingPromotionMissing("Area_owner");
                int security = KingPromotionNeed(owner.Security, "Area54");
                //1DE490 clamps first, then suppresses the actual store for
                //the five special forces, using the fresh representative hex.
                int after = Math.Clamp(unchecked(security + securityIncrease), 0, 100);
                var areaOwner = current.ReadForceTableReference(areaForce <= 51 ? areaForce : 0)
                    ?? throw KingPromotionMissing("Area_Force_pointer");
                int ordinal = KingPromotionNeed(current.ReadForcePhysicalSlot(areaOwner), "Area_Force60");
                if (ordinal is >= 46 and <= 50) continue;
                state = state with { Areas = new(state.Areas) { [physical] = owner with { Security = after } } };
            }
        }

        //TC49054/49048 base=10000/5000, only for emperor/king.
        int experienceBase = rank switch { 1 => 10000, 2 => 5000, _ => 0 };
        if (experienceBase > 0)
        {
            int trait = KingPromotionNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, ruler, 0x4C), "ruler_effect4C");
            var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 6);
            int effect6 = KingPromotionNeed(advantage.Value, advantage.UnresolvedOwner ?? "Force_effect6");
            int gain = unchecked(experienceBase + unchecked(trait * experienceBase) / 100 + unchecked(effect6 * experienceBase) / 100);
            if (KingPromotionNeed(current.ReadForceNativeValidity(force), "experience_Force_validity") &&
                (KingPromotionNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.Flags12, "Force12") & 8) != 0)
                gain = unchecked(gain + experienceBase * 10 / 100);
            state = ApplyAdministrationForceExperienceCurrent(state, current, force, gain, effects);
        }

        //2EE410 uses the requested Rank before Force47 is stored.
        state = ApplyPlayerCommanderCapacityForRankCurrent(state, current, force, rank);
        var forceOwner = state.Forces.GetValueOrDefault(forceId) ?? throw KingPromotionMissing("Force_owner");
        var simulation = state.NativeArmySimulation ?? throw KingPromotionMissing("simulation_owner");
        current.SetForceRankSlot47(forceId, (byte)rank);
        state = state with
        {
            Forces = new(state.Forces) { [forceId] = forceOwner with { RankId = rank } },
            NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
            { Int32Values = new(simulation.Parameters.Int32Values) { [OwnedArmyMoraleWaveInputs.ForceRank47Key(forceId)] = rank } } }
        };
        //223D40 adds the immutable TC1A49850 value10 to signed Force1BE.
        state = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(state, current, forceId, 10);
        return state;
    }
}
