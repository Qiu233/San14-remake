using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

internal sealed record OriginalAiInspectPreparation(OriginalAiCommandPreparation Command,
    IReadOnlyList<(OriginalAiEntity Entity, int Score)> Evaluations);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal OriginalAiCommandPreparation PrepareOriginalAiSpyAwayCurrent(GameState state,
        SourceWorldContext current, int targetForceId, int officerId, int duration)
    {
        if (Seed.Language != "TC") throw AiIntelligenceMissionMissing("SC_source_contract");
        int person = IntelligenceMissionPersonCurrent(current, officerId);
        int district = IntelligenceActorDistrict(current, person);
        //053C90 reads PersonE0's books before1F01E0 validates the packet.
        int cost = StrategicParameter(state, "intelligence.away-node-book", 1);
        if (IntelligenceNeed(current.ReadDistrictRawByte(district, 0x14), "away_node_books") < cost)
            return new(false, null);
        if (!IntelligenceNeed(current.ReadPersonNativeValidity(person), "away_packet_Person_validity")) return new(false, null);
        var target = current.ReadForceTableReference((uint)targetForceId < 52 ? targetForceId : 0)
            ?? throw AiIntelligenceMissionMissing("away_target_Force_pointer");
        if (!IntelligenceNeed(current.ReadForceNativeValidity(target), "away_packet_Force_validity") ||
            duration is not (5 or 10 or 15)) return new(false, null);
        if (!IntelligenceNeed(current.ReadDistrictNativeValidity(district), "away_District_validity"))
            throw AiIntelligenceMissionMissing("mission_district_receiver_contract");
        return PrepareDistrictIntelligenceMissionCurrent(state, current, district,
            targetForceId, officerId, duration, PersonnelIntelligenceKind.Away);
    }

    internal OriginalAiInspectPreparation PrepareOriginalAiInspectCurrent(GameState state,
        SourceWorldContext current, int district)
    {
        var evaluations = new List<(OriginalAiEntity Entity, int Score)>();
        OriginalAiInspectPreparation Complete(OriginalAiCommandPreparation command) => new(command, evaluations.AsReadOnly());
        if (Seed.Language != "TC") throw AiIntelligenceMissionMissing("SC_source_contract");
        if (!IntelligenceNeed(current.ReadDistrictNativeValidity(district), "inspect_actor_District_validity"))
            return Complete(new(false, null));
        //02D0C0's low-budget branch is a successful source no-op.
        if (ReadDiplomacyDistrictBudget(current, district) < 500) return Complete(new(true, null));
        BindIntelligencePageCurrent(state, current);
        var tasks = state.PersonnelCommands?.Intelligences ?? throw AiIntelligenceMissionMissing("inspect_task_owner");
        var peopleRegistry = current.ReadRegisteredPersonPhysicalSlots()
            ?? throw AiIntelligenceMissionMissing("inspect_World100");
        var cities = new List<(BaseReference City, int Score)>();
        //224300/334590 traverse the complete physical City table0..51.
        //Their only iterator gate is virtual78==selected District physical.
        for (int physical = 0; physical < 52; physical++)
        {
            var city = current.ReadCityTableReference(physical) ?? throw AiIntelligenceMissionMissing("inspect_City_pointer");
            if (IntelligenceNeed(current.ReadBaseDistrictSlot(city), "inspect_City_District") != district) continue;
            int force = IntelligenceNeed(current.ReadBaseForceSlot(city), "inspect_City_Force");
            bool hasInspect = IntelligenceNeed(current.ReadBaseNativeValidity(city), "inspect_task_City_validity") &&
                tasks.Values.Any(task => task.Kind == PersonnelIntelligenceKind.Inspect && task.TargetId == physical &&
                    task.ForceId == force && CheckIntelligenceJourneyCurrent(state, current, task) == IntelligenceJourneyDisposition.Continue);
            if (hasInspect) continue;
            var context = ReadOriginalAiInspectCityContextCurrent(state, current, city);
            int score = context.Threat == 1 ? 80 : context.Threat == 2 ? 120 : 0;
            int residents = 0;
            int location = IntelligenceNeed(current.ReadBaseObjectLink(city), "inspect_City4E");
            foreach (int? entry in peopleRegistry)
            {
                int person = IntelligenceNeed(entry, "inspect_resident_pointer");
                if (location is < 1 or > 61 || IntelligenceNeed(current.ReadPersonFirstLocation(person), "inspect_resident_E2") != location) continue;
                if (IntelligenceNeed(current.ReadPersonStatus(person), "inspect_resident_E6") is >= 1 and <= 4) residents++;
            }
            if (residents < 10) score = unchecked(score + 60);
            int governor = IntelligenceMissionPersonCurrent(current,
                IntelligenceNeed(current.ReadArmyReturnBaseGovernor2E(city), "inspect_City2E"));
            if (IntelligenceNeed(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, governor, _governorInputs), "inspect_GovernorE8") <= 89)
                score = unchecked(score + 80);
            if (context.AverageAreaOrder <= 89) score = unchecked(score + 120);
            int identity = IntelligenceNeed(city.PhysicalSlot, "inspect_City_identity");
            score = unchecked(score * 100 + identity);
            cities.Add((city, score));
            evaluations.Add((new(WorldEntityKind.City, identity), score));
        }
        //The source requires MORE than one candidate City, after scoring all.
        if (cities.Count <= 1) return Complete(new(true, null));
        var target = cities.OrderByDescending(row => row.Score).First().City;
        if (!IntelligenceNeed(current.ReadBaseNativeValidity(target), "inspect_selected_City_validity")) return Complete(new(false, null));
        int targetId = IntelligenceNeed(target.PhysicalSlot, "inspect_target_identity");
        var candidates = new List<(int Person, int Identity, int Score)>();
        var date = state.NativeMapSeason?.ThirdDate ?? throw AiIntelligenceMissionMissing("inspect_date");
        foreach (int? entry in peopleRegistry)
        {
            int person = IntelligenceNeed(entry, "inspect_Person_pointer");
            if (IntelligenceNeed(current.ReadPersonDistrictSlot(person), "inspect_PersonE0") != district ||
                IntelligenceNeed(current.ReadPersonStatus(person), "inspect_PersonE6") is < 1 or > 4) continue;
            if (IntelligenceNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "inspect_Person161") >= 2 ||
                (IntelligenceNeed(current.ReadArmyReturnPersonFlags196(person), "inspect_Person15E") & 1) != 0 ||
                PersonCurrentBusySource.Read(current, person, AiIntelligenceMissionMissing) || DuelAftermathSpecialPerson(current, person)) continue;
            if (!IntelligenceNeed(current.ReadPersonNativeValidity(person), "inspect_candidate_validity")) return Complete(new(false, null));
            int id = IntelligenceNeed(current.ReadPersonIdentity(person), "inspect_Person10");
            int days = StrategicCommandTravelSource.Resolve(state, current, person,
                new(StrategicCommandTargetKind.City, targetId), null, date).TravelDays;
            int intelligence = ReadOriginalAiCityDutyAbility(state, current, id, 2);
            int score = unchecked(unchecked(unchecked(intelligence * 13) / 10 - days / 2) * 10000 + id);
            candidates.Add((person, id, score));
            evaluations.Add((new(WorldEntityKind.Officer, id), score));
        }
        //One candidate is scored but does not dispatch; zero is also Success.
        if (candidates.Count <= 1) return Complete(new(true, null));
        var selected = candidates.OrderByDescending(row => row.Score).First();
        if (!IntelligenceNeed(current.ReadPersonNativeValidity(selected.Person), "inspect_selected_Person_validity")) return Complete(new(false, null));
        int targetDistrict = IntelligenceNeed(current.ReadBaseDistrictSlot(target), "inspect_target_District");
        var sampler = ReadArmyDestructionSampler(state, current) ?? throw AiIntelligenceMissionMissing("inspect_World450_45C");
        //3FCA40 function1 is the pure World day/salt sampler, not App RNG.
        int roll = TacticEffectSourceSamplerMath.Sample(100, 1, targetDistrict, district, selected.Identity, 0, sampler);
        int level = roll < 80 ? 10 : roll < 92 ? 20 : 30;
        return Complete(PrepareDistrictIntelligenceMissionCurrent(state, current, district,
            targetId, selected.Identity, level, PersonnelIntelligenceKind.Inspect));
    }

    private OriginalAiCommandPreparation PrepareDistrictIntelligenceMissionCurrent(GameState state,
        SourceWorldContext current, int district, int targetId, int officerId, int option, PersonnelIntelligenceKind kind)
    {
        if (!ValidateDistrictIntelligenceMissionPacketCurrent(state, current, district, targetId, officerId, option, kind, out int force))
            return new(false, null);
        return kind == PersonnelIntelligenceKind.Away
            ? new(true, new DispatchIntelligenceOrder(force, targetId, officerId, option) { DistrictId = district })
            : new(true, new InspectIntelligenceOrder(force, targetId, officerId, option) { DistrictId = district });
    }

    private static InvalidDataException AiIntelligenceMissionMissing(string field) => new("original_ai.intelligence_mission." + field);
}
