using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState AppendDemolitionEffects(GameState state, SourceWorldContext current,
        ArmyUnitReference army, UnitState image, ArmyDemolitionNumberResult removal,
        Func<NativeMapSeasonDate> readDate, Func<GameState, int> readCampaignDay,
        List<CampaignEffectRequest> requests, Action<GameState>? publishReached = null)
    {
        string definitionKey = MapObjectSourceDirectory.DefinitionKey(removal.DefinitionSlot);
        requests.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionVisual,
            removal.Receiver, removal.HexId, definitionKey) });
        publishReached?.Invoke(state);
        requests.Add(new(null, null) { MapObjectPresentation = new(MapObjectPresentationEvent.DestructionSound,
            removal.Receiver, removal.HexId, definitionKey) });
        publishReached?.Invoke(state);
        if (DemolitionKnown(current.ReadArmyUnitNativeValidity(army), "speech_army_validity"))
        {
            if (state.PersonnelCommands is null) throw DemolitionMissing("speech_personnel_owner");
            PersonReturnCommands.BindCurrent(state, current);
            ushort commander = DemolitionKnown(current.ReadArmyUnitCommanderId(army), "speech_commander");
            var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
                ?? throw DemolitionMissing("speech_person_pointer");
            int personPhysical = person.PhysicalSlot ?? throw DemolitionMissing("speech_person");
            int position;
            try
            {
                position = PersonReturnCommands.ResolvePersonPosition(state, current, personPhysical, readDate()).PhysicalSlot ?? 48400;
            }
            finally
            {
                // A later position dependency may fail after EnsureArrivalDate.
                // Preserve only this reached command owner, not other Person fields.
                state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
                    ?? throw DemolitionMissing("speech_personnel_publication") };
                publishReached?.Invoke(state);
            }
            int officer = DemolitionKnown(current.ReadPersonIdentity(personPhysical), "speech_person_identity");
            state = QueueArmyMapSpeechCurrent(state, current, image.Id, officer, 56, position);
            publishReached?.Invoke(state);
        }
        // Category4 suppresses only text reporting, after all three prior intents.
        if (removal.Category != 4) state = AppendDemolitionReport(state, current, army, image, removal,
            readCampaignDay, requests, publishReached);
        return state;
    }

    private GameState AppendDemolitionReport(GameState state, SourceWorldContext current,
        ArmyUnitReference army, UnitState image, ArmyDemolitionNumberResult removal,
        Func<GameState, int> readCampaignDay, List<CampaignEffectRequest> requests,
        Action<GameState>? publishReached = null)
    {
        byte objectFaction = DemolitionKnown(current.ReadObjectForceSlot13(new(removal.Physical)), "report_object_faction");
        var objectForce = current.ReadForceTableReference(objectFaction <= 51 ? objectFaction : 0)
            ?? throw DemolitionMissing("report_object_force_pointer");
        bool visible = ReadDemolitionAreaVisibility(state, current, removal.AreaId);
        if (!DemolitionKnown(current.ReadArmyUnitNativeValidity(army), "report_army_validity")) return state;
        bool playerDistrict = IsPlayerRulerLedDistrict(state, current, image);
        if (!playerDistrict && !visible) return state;
        int district, payload, root;
        string objectName;
        string? choiceName = null;
        if (playerDistrict)
        {
            district = ReadFacilityHistoryDistrict(current, army);
            objectName = ReadFacilityArrivalObjectName(state, current, new(removal.Physical));
            byte choice = image.NativeCommandChoice41 ?? throw DemolitionMissing("report_command_choice");
            //294FD0 uses119 for choice1 and11A for every other byte.
            choiceName = ReadHistoryDataText(0, choice == 1 ? 0x119 : 0x11A);
            root = visible && DemolitionKnown(ArmyConfigurationSourceAdapter.ReadAction(state, image), "report_action") == 10
                ? 0x5F7B : 0x5F7C;
            payload = DemolitionKnown(current.ReadArmyUnitCommanderId(army), "report_player_speaker");
        }
        else
        {
            objectName = ReadFacilityArrivalObjectName(state, current, new(removal.Physical));
            district = FirstForceDistrict(objectForce);
            payload = 5012;
            root = 0x5F7D;
        }
        var formatted = FormatReportMessage(state, current, root,
            new HistoryMessageOperands(Army0: army, Area0: removal.AreaId, String0: objectName, String1: choiceName),
            ReportMessageOrigin.SavedHistory, publishReached);
        state = formatted.State;
        publishReached?.Invoke(state);
        //The selected District and payload precede the formatter. Only450
        //and the retained original Hex pointer's22DEF0 are read afterwards.
        int day = readCampaignDay(state);
        int hex = DemolitionKnown(current.ReadHexPhysicalSlot(new(removal.HexId)), "report_retained_Hex_identity");
        state = WriteHistory(state, current, new ReportCommand(5, district, hex, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(state), payload, MarkBit4: 1), requests);
        publishReached?.Invoke(state);
        return state;

        int FirstForceDistrict(ForceReference force)
        {
            foreach (var entry in current.ReadArmyReturnRegisteredDistricts() ?? throw DemolitionMissing("report_complete_districts"))
            {
                int physical = entry?.PhysicalSlot ?? throw DemolitionMissing("report_district_entry");
                int identity = DemolitionKnown(current.ReadForcePhysicalSlot(force), "report_original_force_identity");
                if (identity is < 1 or > 51 ||
                    DemolitionKnown(current.ReadDistrictForceId(physical), "report_district_filter_force") != identity) continue;
                ushort leader = DemolitionKnown(current.ReadDistrictLeader(physical), "report_district_filter_leader");
                ushort ruler = DemolitionKnown(current.ReadMoraleWaveRuler22EAC0(force), "report_original_force");
                if (leader == ruler || DemolitionKnown(current.ReadDistrictNumber(physical), "report_district_filter_number") == 1)
                    return DemolitionKnown(current.ReadDistrictPhysicalSlot(entry!), "report_primary_district_identity");
            }
            var fallback = current.ReadDistrictTableReference(0) ?? throw DemolitionMissing("report_primary_district_zero_lookup");
            return DemolitionKnown(current.ReadDistrictPhysicalSlot(fallback), "report_primary_district_zero_identity");
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
