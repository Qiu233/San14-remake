using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState AppendDemolitionEffects(GameState state, SourceWorldContext current,
        ArmyUnitReference army, UnitState image, ArmyDemolitionNumberResult removal,
        NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> requests)
    {
        string definitionKey = MapObjectSourceDirectory.DefinitionKey(removal.DefinitionSlot);
        requests.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionVisual,
            removal.Receiver, removal.HexId, definitionKey) });
        requests.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionSound,
            removal.Receiver, removal.HexId, definitionKey) });
        if (DemolitionKnown(current.ReadArmyUnitNativeValidity(army), "speech_army_validity"))
        {
            if (state.PersonnelCommands is null) throw DemolitionMissing("speech_personnel_owner");
            PersonReturnCommands.BindCurrent(state, current);
            ushort commander = DemolitionKnown(current.ReadArmyUnitCommanderId(army), "speech_commander");
            var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                ?? throw DemolitionMissing("speech_person_pointer");
            int personPhysical = person.PhysicalSlot ?? throw DemolitionMissing("speech_person");
            int position = PersonReturnCommands.ResolvePersonPosition(state, current, personPhysical, moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
            // The position getter can initialize a journey deadline. Preserve
            // that real writer without publishing unrelated Person fields.
            state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
                ?? throw DemolitionMissing("speech_personnel_publication") };
            int officer = DemolitionKnown(current.ReadPersonIdentity(personPhysical), "speech_person_identity");
            requests.Add(new(new(CombatParticipantKind.Army, image.Id, officer, CombatSpeechEvent.ArmyDemolitionCompleted,
                position, CombatParticipantKind.FieldObject, removal.Receiver.Value, null, null, null), null));
        }
        // Category4 suppresses only text reporting, after all three prior intents.
        if (removal.Category != 4) AppendDemolitionReport(state, current, army, image, removal, moment, requests);
        return state;
    }

    private void AppendDemolitionReport(GameState state, SourceWorldContext current,
        ArmyUnitReference army, UnitState image, ArmyDemolitionNumberResult removal,
        NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> requests)
    {
        bool visible = ReadDemolitionAreaVisibility(state, current, removal.AreaId);
        if (!DemolitionKnown(current.ReadArmyUnitNativeValidity(army), "report_army_validity")) return;
        bool playerDistrict = IsPlayerRulerLedDistrict(state, current, image);
        if (!playerDistrict && !visible) return;
        int person = current.ResolveOfficerSource(image.OfficerId) ?? throw DemolitionMissing("report_person");
        int district; int speaker; CampaignReportEvent reportEvent;
        byte? choice = null;
        if (playerDistrict)
        {
            district = DemolitionKnown(current.ReadPersonDistrictSlot(person), "report_player_district");
            speaker = DemolitionKnown(current.ReadArmyUnitCommanderId(army), "report_player_speaker");
            choice = image.NativeCommandChoice41 ?? throw DemolitionMissing("report_command_choice");
            reportEvent = visible && DemolitionKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "report_action") == 10
                ? CampaignReportEvent.PlayerDemolitionVisible : CampaignReportEvent.PlayerDemolitionAcknowledged;
        }
        else
        {
            byte objectFaction = DemolitionKnown(current.ReadObjectForceSlot13(new(removal.Physical)), "report_object_faction");
            int selectedForce = objectFaction <= 51 ? objectFaction : 0;
            district = FirstForceDistrict(selectedForce);
            speaker = 5012;
            reportEvent = CampaignReportEvent.OtherArmyDemolitionObserved;
        }
        // The native report pool's App/ring admission remains an application
        // consumer. Capture its source-time arguments; never fabricate admission.
        int selectedDistrict = district <= 51 ? district : 0;
        bool districtValid = DemolitionKnown(current.ReadDistrictNativeValidity(selectedDistrict), "report_recipient_validity");
        byte forceSlot = districtValid ? DemolitionKnown(current.ReadDistrictForceId(selectedDistrict), "report_recipient_force") : (byte)0;
        int forcePhysical = forceSlot <= 51 ? forceSlot : 0;
        var force = current.ReadForceTableReference(forcePhysical) ?? throw DemolitionMissing("report_force_pointer");
        byte color = districtValid ? DemolitionKnown(current.ReadForceReportColor(force), "report_force_color") : (byte)60;
        byte kingdom = DemolitionKnown(current.ReadForceKingdomNameIndex(forcePhysical), "report_force_kingdom");
        int ruler = current.ReadForceRecord(forcePhysical)?.RawRuler10 ?? throw DemolitionMissing("report_force_ruler");
        string? rulerName = state.Officers.GetValueOrDefault(ruler)?.Name;
        string objectName = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions[removal.DefinitionSlot].Name;
        string armyName = state.Officers.GetValueOrDefault(image.OfficerId)?.Name ?? throw DemolitionMissing("report_army_name");
        requests.Add(new(null, new(district, removal.HexId, StatusCampaignDay(state, moment), speaker,
            armyName, objectName, CombatParticipantKind.FieldObject, removal.Receiver.Value,
            forcePhysical, color, kingdom, ruler)
        { Event = reportEvent, AreaId = removal.AreaId, CommandChoiceId = choice, RulerNameSnapshot = rulerName }));

        int FirstForceDistrict(int force)
        {
            if (force == 0) return 0;
            var selected = current.ReadForceRecord(force) ?? throw DemolitionMissing("report_original_force");
            ushort ruler = selected.RawRuler10;
            var order = current.ReadRegistryReferences(RegistryKind.District) ?? throw DemolitionMissing("report_complete_districts");
            foreach (var entry in order)
            {
                int physical = entry is { Kind: RegistryKind.District, PhysicalSlot: int at } ? at : throw DemolitionMissing("report_district_entry");
                if (DemolitionKnown(current.ReadDistrictForceId(physical), "report_district_filter_force") != force) continue;
                if (DemolitionKnown(current.ReadDistrictLeader(physical), "report_district_filter_leader") == ruler ||
                    DemolitionKnown(current.ReadDistrictNumber(physical), "report_district_filter_number") == 1) return physical;
            }
            return 0;
        }
    }

    private bool ReadDemolitionAreaVisibility(GameState state, SourceWorldContext current, int ordinal)
    {
        var area = current.ReadAreaTableReference(ordinal <= 500 ? ordinal : 0) ?? throw DemolitionMissing("report_area_pointer");
        if (!DemolitionKnown(current.ReadAreaNativeValidity(area), "report_area_validity")) return false;
        int physical = area.PhysicalSlot ?? throw DemolitionMissing("report_area_identity");
        int citySlot = DemolitionKnown(current.ReadAreaCitySlot(area), "report_current_city");
        if (ReadDemolitionCityVisibility(state, current, citySlot)) return true;
        int original = DemolitionKnown(current.ReadAreaValidityCode(area), "report_original_city");
        return ReadDemolitionCityVisibility(state, current, original);
    }

    private bool ReadDemolitionCityVisibility(GameState state, SourceWorldContext current, int ordinal)
    {
        var city = current.ReadCityTableReference(ordinal <= 51 ? ordinal : 0) ?? throw DemolitionMissing("report_city_pointer");
        if (!DemolitionKnown(current.ReadBaseNativeValidity(city), "report_city_validity")) return false;
        var district = current.ReadCityDistrictReference(city) ?? throw DemolitionMissing("report_city_district_pointer");
        if (!DemolitionKnown(current.ReadDistrictNativeValidity(district), "report_city_district_validity")) return false;
        int physical = district.PhysicalSlot ?? throw DemolitionMissing("report_city_district");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw DemolitionMissing("report_current_player");
        if (!DemolitionKnown(current.ReadForceNativeValidity(player), "report_player_validity")) return false;
        if (DemolitionKnown(current.ReadDistrictForceId(physical), "report_city_district_force") != player.PhysicalSlot) return false;
        ushort leader = DemolitionKnown(current.ReadDistrictLeader(physical), "report_city_district_leader");
        var person = current.ReadPersonLookupReference(leader <= 6000 ? leader : 0) ?? throw DemolitionMissing("report_city_leader_pointer");
        if (person.PhysicalSlot is not int personPhysical || !DemolitionKnown(current.ReadPersonNativeValidity(personPhysical), "report_city_leader_validity")) return false;
        return DemolitionKnown(current.ReadPersonStatus(personPhysical), "report_city_leader_status") == 1;
    }
}
