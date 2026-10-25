using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2D4480 keeps its existing rank2 NPC contract;58 shares the same writer.
    internal GameState ApplyNormalTurnCoalitionKingPromotionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference force)
        => ApplyNormalTurnForceRankPromotionCurrent(state, current, pool, force, 2);

    //2B5ED0's NPC path. The caller owns the current world and primary pool;
    //the operation neither captures nor exports that pool.
    internal GameState ApplyNormalTurnForceRankPromotionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference force, int rank,
        int kingdomNameChoice = 0)
    {
        if (!KingPromotionNeed(current.ReadForceNativeValidity(force), "Force_validity")) return state;
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw KingPromotionMissing("current_pool_binding");
        int forceId = KingPromotionNeed(current.ReadForcePhysicalSlot(force), "Force60");
        if (TalentForceIsPlayerCurrent(state, current, force)) throw KingPromotionMissing("NPC_scope");
        if (rank is < 1 or > 8) throw KingPromotionMissing("NPC_rank_scope");
        var definition = current.ReadStaticTableReference(0x78490, rank)
            ?? throw KingPromotionMissing("rank_definition_pointer");
        if (definition.PhysicalSlot is null or 0) return state;
        if (!state.ForceRanks.ContainsKey(definition.PhysicalSlot.Value)) throw KingPromotionMissing("rank_definition");
        int ruler = KingPromotionLookup(current,
            KingPromotionNeed(current.ReadForceRecord(forceId)?.RawRuler10, "Force_ruler10"));
        if (!KingPromotionNeed(current.ReadPersonNativeValidity(ruler), "ruler_validity")) return state;
        int title = KingPromotionNeed(current.ReadForceKingdomNameIndex(forceId), "Force48");
        if (title is 6 or 44 or 45) return state;

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
        if (rank == 1)
        {
            var named = PrepareNormalTurnEmpireNameCurrent(state, current, pool, force, kingdomNameChoice);
            state = named.State;
            if (!named.Valid) return state;
        }
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
                _ => selfProclaimed ? sameDoctrine ? 2 : 1 : sameDoctrine ? 3 : 2
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
            if (gain >= 0)
            {
                _force164Inputs.BindCurrent(state, current);
                int before = KingPromotionNeed(current.ReadForce164(force), "Force164");
                int oldLevel = KingPromotionNeed(OwnedForce164Inputs.ReadLevel(state, before), "experience_old_level");
                int after = Math.Clamp(unchecked(before + gain), 0, 9999999);
                int newLevel = KingPromotionNeed(OwnedForce164Inputs.ReadLevel(state, after), "experience_new_level");
                var packet = new ArmyDestructionForce164Packet(current, force, requested: gain, before: before,
                    after: after, oldLevel: oldLevel, newLevel: newLevel);
                ArmyDestructionForce164.ApplyContext(packet);
                state = ArmyDestructionForce164.PublishCurrent(state, packet);
            }
        }

        //2EE410 is player-only World165D=min(RankB3,5), so the guaranteed
        //NPC branch has no store. It does not assign or rearrange offices.
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
        return ApplyNormalTurnRankPromotionReportsCurrent(state, current, pool, force, rank, emperor, selfProclaimed);
    }

    private static bool KingPromotionProtectedKin(GameState state, SourceWorldContext current, int person, int ruler)
    {
        //2C9FA0 sworn group,2C8E90 spouse, and2C8E40 parents in both
        //directions are separate predicates; siblings and likes do not enter.
        int personId = KingPromotionNeed(current.ReadPersonIdentity(person), "kin_Person10");
        int rulerId = KingPromotionNeed(current.ReadPersonIdentity(ruler), "kin_ruler10");
        ushort group = OfficeWord(state, person, 0xBC);
        if (group != 0 && group == OfficeWord(state, ruler, 0xBC)) return true;
        if (OfficeWord(state, person, 0xBA) is var spouse && spouse != 0 && spouse == rulerId) return true;
        return Parent(person, rulerId) || Parent(ruler, personId);
        bool Parent(int child, int parent) => OfficeWord(state, child, 0xB4) is var father && father != 0 && father == parent ||
            OfficeWord(state, child, 0xB6) is var mother && mother != 0 && mother == parent;
    }

    private static int KingPromotionDoctrine(GameState state, SourceWorldContext current, int person)
    {
        int id = KingPromotionNeed(current.ReadPersonIdentity(person), "doctrine_Person10");
        var owner = state.Officers.GetValueOrDefault(id) ?? throw KingPromotionMissing("doctrine_Person_owner");
        if (current.ResolveOfficerSource(id) != person) throw KingPromotionMissing("doctrine_Person_binding");
        int raw = owner.DoctrineId ?? owner.AbilityState?.ObservedDoctrineId ?? throw KingPromotionMissing("Person14C");
        return (uint)raw <= 255 ? raw : throw KingPromotionMissing("Person14C_width");
    }

    private static int KingPromotionLookup(SourceWorldContext current, int raw) =>
        KingPromotionNeed((raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?.PhysicalSlot, "Person_lookup_pointer");
    private static T KingPromotionNeed<T>(T? value, string field) where T : struct => value ?? throw KingPromotionMissing(field);
    private static InvalidDataException KingPromotionMissing(string field) => new("normal-turn.coalition.king-promotion." + field);
}
