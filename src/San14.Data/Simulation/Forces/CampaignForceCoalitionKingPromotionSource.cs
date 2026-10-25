using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2B5ED0 with rank=2, reached by2D4480 only for an ordinary NPC.
    //The caller owns the current world and the primary pool throughout the
    //coalition transaction; this leaf neither captures nor exports that pool.
    internal GameState ApplyNormalTurnCoalitionKingPromotionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, ForceReference force)
    {
        if (!KingPromotionNeed(current.ReadForceNativeValidity(force), "Force_validity")) return state;
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw KingPromotionMissing("current_pool_binding");
        int forceId = KingPromotionNeed(current.ReadForcePhysicalSlot(force), "Force60");
        if (TalentForceIsPlayerCurrent(state, current, force)) throw KingPromotionMissing("NPC_scope");
        //The selected resource owns the real rank definition, independently
        //of the force's old rank. Rank2 is the native-valid King object.
        if (!state.ForceRanks.ContainsKey(2)) throw KingPromotionMissing("King_rank_definition");
        int ruler = KingPromotionLookup(current,
            KingPromotionNeed(current.ReadForceRecord(forceId)?.RawRuler10, "Force_ruler10"));
        if (!KingPromotionNeed(current.ReadPersonNativeValidity(ruler), "ruler_validity")) return state;
        int title = KingPromotionNeed(current.ReadForceKingdomNameIndex(forceId), "Force48");
        if (title is 6 or 44 or 45) return state;

        ushort emperorRaw = KingPromotionNeed(CityCaptureTerritoryInputs.ReadEmperor1654(state), "World1654");
        int emperor = KingPromotionLookup(current, emperorRaw);
        bool emperorPresent = KingPromotionNeed(current.ReadPersonNativeValidity(emperor), "emperor_validity");
        //For rank2, presence alone selects the imperial-grant loyalty table.
        //The emperor's owning force is queried only by the rank1 branch.
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
            //Immutable TC1A4A8E0/8EC/8F8/904: granted same/different=9/6,
            //self-proclaimed same/different=6/3. These are source data, not
            //a selected-scenario observation or a cached execution result.
            int increase = emperorPresent ? sameDoctrine ? 9 : 6 : sameDoctrine ? 6 : 3;
            byte after = (byte)Math.Min(100, unchecked(loyalty + increase));
            if (KingPromotionNeed(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)),
                "Person15E_bit13")) after = 255;
            current.SetPersonDestructionLoyaltyE8(person, after);
            state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
        }

        //The original nested ForceCity/CityArea iterators increase Area54
        //security, not Army morale. City table order and Area registry order
        //are retained, including repeated current pointer-table aliases.
        for (int citySlot = 0; citySlot < 52; citySlot++)
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
                int after = Math.Clamp(unchecked(security + 10), 0, 100);
                var areaOwner = current.ReadForceTableReference(areaForce <= 51 ? areaForce : 0)
                    ?? throw KingPromotionMissing("Area_Force_pointer");
                int ordinal = KingPromotionNeed(current.ReadForcePhysicalSlot(areaOwner), "Area_Force60");
                if (ordinal is >= 46 and <= 50) continue;
                state = state with { Areas = new(state.Areas) { [physical] = owner with { Security = after } } };
            }
        }

        //TC1A49048/490B4/49BB8/49794:5000,1,1,10. The real ruler's
        //nine personality slots and current state advantages supply the terms.
        int trait = KingPromotionNeed(TacticRemakeCurrentTraits.CommanderEffectSum(current, ruler, 0x4C), "ruler_effect4C");
        var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 6);
        int effect6 = KingPromotionNeed(advantage.Value, advantage.UnresolvedOwner ?? "Force_effect6");
        int gain = unchecked(5000 + unchecked(trait * 5000) / 100 + unchecked(effect6 * 5000) / 100);
        if (KingPromotionNeed(current.ReadForceNativeValidity(force), "experience_Force_validity") &&
            (KingPromotionNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.Flags12, "Force12") & 8) != 0)
            gain = unchecked(gain + 500);
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

        //2EE410 is player-only World165D=min(RankB3,5), so the guaranteed
        //NPC branch has no store. It does not assign or rearrange offices.
        var forceOwner = state.Forces.GetValueOrDefault(forceId) ?? throw KingPromotionMissing("Force_owner");
        var simulation = state.NativeArmySimulation ?? throw KingPromotionMissing("simulation_owner");
        current.SetForceRankSlot47(forceId, 2);
        state = state with
        {
            Forces = new(state.Forces) { [forceId] = forceOwner with { RankId = 2 } },
            NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
            { Int32Values = new(simulation.Parameters.Int32Values) { [OwnedArmyMoraleWaveInputs.ForceRank47Key(forceId)] = 2 } } }
        };
        //223D40 adds the immutable TC1A49850 value10 to signed Force1BE.
        state = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(state, current, forceId, 10);
        return ApplyCoalitionKingPromotionHistoryCurrent(state, current, force, emperor, emperorPresent);
    }

    private GameState ApplyCoalitionKingPromotionHistoryCurrent(GameState state, SourceWorldContext current,
        ForceReference force, int emperor, bool emperorPresent)
    {
        //An NPC uses the actual World9DF8 lookup5012 rather than choosing a
        //new advisor. An invalid speaker suppresses the report after effects.
        int speaker = KingPromotionLookup(current, 5012);
        if (!KingPromotionNeed(current.ReadPersonNativeValidity(speaker), "history_speaker_validity")) return state;
        int speakerId = KingPromotionNeed(current.ReadPersonIdentity(speaker), "history_speaker10");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw KingPromotionMissing("history_player_Force");
        int forceId = KingPromotionNeed(current.ReadForcePhysicalSlot(force), "history_Force60");
        int playerRuler = TalentRulerCurrent(current, player), ruler = TalentRulerCurrent(current, force);
        int? emperorId = emperorPresent ? KingPromotionNeed(current.ReadPersonIdentity(emperor), "history_emperor10") : null;
        var main = FormatNormalTurnReport(state, current, emperorPresent ? 0x2C68 : 0x2C69,
            new(Force0: forceId, Force1: forceId, Person0: emperorId ?? 0));
        var detail = FormatNormalTurnReport(main.State, current, emperorPresent ? 0x5376 : 0x537C,
            new(Person0: speakerId, Person1: playerRuler, Person2: emperorId ?? ruler,
                Person3: emperorPresent ? ruler : null, Force0: forceId));
        int day = (ReadArmyDestructionSampler(detail.State, current) ?? throw KingPromotionMissing("history_World450")).Day450;
        int district = KingPromotionNeed(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "history_District60");
        var effects = new List<CampaignEffectRequest>();
        state = WriteHistory(detail.State, current,
            new(1, district, 0, day, main.Text, detail.Text, speakerId, MarkBit0: 1), effects);
        if (effects.Count == 0) return state;
        long revision = state.NativeMapSeason?.AdvancedCalendarSteps ?? throw KingPromotionMissing("history_revision");
        var pending = state.CampaignEffectRequests ?? throw KingPromotionMissing("history_pending_owner");
        return state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.MonthlyMaintenance, revision, effects) };
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
