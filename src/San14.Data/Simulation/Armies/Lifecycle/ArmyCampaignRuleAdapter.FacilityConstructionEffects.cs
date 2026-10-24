using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState AppendFacilityConstructionMarchTail(GameState state, SourceWorldContext current,
        UnitState image, NativeArmyProgressStageMoment? moment)
    {
        bool playerDistrict = IsPlayerRulerLedDistrict(state, current, image);
        byte action = K(ArmyConfigurationSourceAdapter.ReadAction(state, image), "arrival_action");
        if (!playerDistrict || action != 9) return state;
        if (image.OverrideTarget?.Kind != ArmyOverrideTargetKind.None) throw Missing("arrival_override_Army_target");
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("arrival_Army_identity");
        var fields = current.ReadArmyLifecycleTargetFields(physical) ?? throw Missing("arrival_target_fields");
        int goal = DemolitionGoal(current, K(fields.Kind38, "arrival_target_kind"), K(fields.Identity3A, "arrival_target_identity"));
        if (ArmyRouteSearch.Distance(goal, image.HexId) != 1) return state;
        if (ConstructionWaypoints(image).Any(value => value < 48400)) return state;
        var runtime = image.NativeTacticRuntime ?? throw Missing("arrival_runtime");
        if (runtime.Abnormal23 != 0 || runtime.Abnormal25 != 0 || runtime.Abnormal24 != 0 || (runtime.Flags26 & 1) != 0) return state;
        var site = current.ReadHexTableReference(goal < 48400 ? goal : 0) ?? throw Missing("arrival_Hex_pointer");
        if (site.PhysicalSlot is null) return state;
        var obj = FacilityConstructionLegality.ObjectAt(current, site);
        if (!K(current.ReadObjectNativeValidity(obj), "arrival_Object_validity")) return state;
        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
        if (!IsSupported(definition.PhysicalSlot)) throw Missing("arrival_Object_name_consumer_outside_supported_facilities");
        if (moment is null) throw Missing("arrival_source_moment");
        var requests = new List<CampaignEffectRequest>();
        state = AppendConstructionReport(state, current, image, obj, moment,
            CampaignReportEvent.PlayerFacilityRepairStarted, requests, goal);
        return state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("arrival_effect_owner"))
            .Append(CampaignEffectSource.ArmyFacilityConstruction, moment.CalendarCycleOrdinal, requests) };
    }

    private static bool ConstructionModelIsRequired(GameState state, SourceWorldContext current, ObjectReference obj)
    {
        if (!K(current.ReadObjectNativeValidity(obj), "model_Object_validity")) return false;
        var fields = current.ReadPhysicalObject(K(obj.PhysicalSlot, "model_Object_pointer")) ?? throw Missing("model_Object_fields");
        if (fields.RawField12 is 1 or 2) return false;
        int raw = K(current.ReadObjectForceSlot13(obj), "model_Object_Force");
        var force = current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("model_Force_pointer");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("model_player_pointer");
        int playerId = K(player.PhysicalSlot, "model_player_identity");
        int forceId = K(force.PhysicalSlot, "model_Force_identity");
        if (!IsSupported(fields.RawField12)) throw Missing("model_definition_consumer");
        //2C4830: own traps are visible immediately, without fabricating
        //completion or a diplomacy value. Non-traps share the ordinary tail.
        if (forceId == playerId) return true;
        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
        if (!K(current.ReadObjectDefinitionValidity(definition), "model_definition_validity") ||
            K(current.ReadObjectDefinitionRaw2C(definition), "model_definition_category") != 4) return true;
        if (!K(current.ReadForceNativeValidity(player), "model_player_validity")) return false;
        byte[]? relations = playerId == 0 ? state.NativeForceUnregisterCurrent?.PlaceholderRelationsEA
            : state.NativeArmySimulation?.Forces.GetValueOrDefault(playerId)?.RelationsEA;
        if (relations is not { Length: 52 } || (uint)forceId >= 52) throw Missing("model_player_relation_EA");
        return (relations[forceId] & 0x7f) != 0;
    }

    private GameState ApplyConstructionSiteOwnership(GameState state, SourceWorldContext current,
        ArmyUnitReference army, HexReference site, MapObjectReceiverId receiver, string definitionKey,
        List<CampaignEffectRequest> requests)
    {
        //288Bxx reads both the current Home and its City identity before the
        //185D00 call, even when the wrapper will return without a write.
        var home = current.FindArmyUnitCurrentBase(army);
        var homeCity = CurrentGateMapQueries.FindCity(home, current);
        _ = K(homeCity.PhysicalSlot, "ownership_Home_City_identity");
        int force = current.ReadArmyUnitRawForceSlot(army);
        if (!GateCaptureTerritorySource.Eligible(state, current, site, force, requireNoArmy: false)) return state;
        if (K(current.ReadHexForceSlot(site), "ownership_previous_Force") == force) return state;
        if (force is < 1 or > 45) throw Missing("ownership_special_Force_tail");
        int surface = K(current.ReadHexSurfaceCode(site), "ownership_surface");
        if (surface is >= 15 and <= 18) throw Missing("ownership_center_tail_outside_supported_facilities_terrain");
        //The same normal numeric application lifetime used by ordinary
        //occupation has no original scenery-view or tutorial-mode receiver.
        //Keep the logical visual/sound requests instead of claiming playback.
        current.CurrentSceneryLayout = new(null);
        current.ArmyReturnTransferMode = new(false, null);
        current.AssignHexForce(site, force);
        int physical = K(site.PhysicalSlot, "ownership_Hex_identity");
        if (!state.Territory.TryGetValue(physical, out var before)) throw Missing("ownership_canonical_Hex");
        var working = state with { Territory = new(state.Territory) { [physical] = before with { ForceId = force } } };
        var selected = current.ReadForceTableReference(K(current.ReadHexForceSlot(site), "ownership_written_Force"))
            ?? throw Missing("ownership_written_Force_pointer");
        if (K(current.ReadForceNativeValidity(selected), "ownership_written_Force_validity"))
        {
            requests.Add(new(null, null) { FacilityConstructionPresentation = new(
                FacilityConstructionPresentationOperation.ShowSiteOwnershipChanged, receiver, physical, definitionKey, force) });
            requests.Add(new(null, null) { FacilityConstructionPresentation = new(
                FacilityConstructionPresentationOperation.PlaySiteOwnershipChangedSound, receiver, physical, definitionKey, force) });
        }
        return working;
    }

    private static GameState AppendConstructionStartSpeech(GameState state, SourceWorldContext current,
        ArmyUnitReference army, UnitState image, ObjectReference obj, FacilityOperation operation,
        List<CampaignEffectRequest> requests)
    {
        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
        if (!RepairActorForceIsPlayer(state, current, army) && K(current.ReadObjectDefinitionRaw2C(definition), "speech_category") == 4) return state;
        if (!K(current.ReadArmyUnitNativeValidity(army), "speech_Army_validity")) return state;
        if (state.PersonnelCommands is null) throw Missing("speech_personnel_owner");
        PersonReturnCommands.BindCurrent(state, current);
        int commander = K(current.ReadArmyUnitCommanderId(army), "speech_commander");
        int person = K((current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
            ?? throw Missing("speech_Person_pointer")).PhysicalSlot, "speech_Person_identity");
        int position = PersonReturnCommands.ResolvePersonPosition(state, current, person, operation.ReadDate()).PhysicalSlot ?? 48400;
        state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw Missing("speech_personnel_publication") };
        int officer = K(current.ReadPersonIdentity(person), "speech_Person_character_identity");
        var receiver = current.CurrentMapObjectDirectory!.ReceiverAtPhysical(K(obj.PhysicalSlot, "speech_Object_identity"));
        requests.Add(new(new(CombatParticipantKind.Army, image.Id, officer,
            CombatSpeechEvent.ArmyFacilityConstructionStarted, position, CombatParticipantKind.FieldObject,
            receiver.Value, null, null, null), null));
        return state;
    }

    private GameState AppendConstructionReport(GameState state, SourceWorldContext current, UnitState image,
        ObjectReference obj, FacilityOperation operation, CampaignReportEvent reportEvent,
        List<CampaignEffectRequest> requests, int? retainedGoal = null)
    {
        bool arrival = reportEvent switch
        {
            CampaignReportEvent.PlayerFacilityConstructionStarted => false,
            CampaignReportEvent.PlayerFacilityRepairStarted => true,
            _ => throw Missing("report_source_variant")
        };
        var army = new ArmyUnitReference(image.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("report_Army_identity"));
        string targetName;
        if (arrival) targetName = ReadFacilityArrivalObjectName(state, current, obj);
        else
        {
            var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
            int kind = K(definition.PhysicalSlot, "report_definition_identity");
            targetName = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions[kind].Name;
        }
        var formatted = FormatHistoryReport(state, current, arrival ? 0xC357 : 0xC356,
            new HistoryMessageOperands(Army0: army, String0: targetName));
        state = formatted.State;
        //Fresh writer metadata follows main formatting. C357 keeps the
        //source's effective goal instead of substituting Object10/Army2A.
        int commander = K(current.ReadArmyUnitCommanderId(army), "report_commander");
        int day = operation.ReadCampaignDay(state);
        int hex = arrival ? retainedGoal ?? throw Missing("report_retained_goal") :
            K(current.ReadObjectHexIndex(obj), "report_Object_Hex");
        int district = ReadFacilityHistoryDistrict(current, army);
        return WriteHistory(state, current, new ReportCommand(5, district, hex, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(state), commander, MarkBit4: 1), requests);
    }

    //Complete22EFD0, message-field-source01/TC-22EFD0.cpp. This is
    //the actual Object12 lookup, including Definition0 and its validity gate.
    internal string ReadFacilityArrivalObjectName(GameState state, SourceWorldContext current, ObjectReference obj)
    {
        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
        if (!K(current.ReadObjectDefinitionValidity(definition), "report_Object_name_definition_validity"))
            return CampaignHistoryWriter.Supplemental(state) ?? throw Missing("report_Object_name_fallback");
        int physical = K(definition.PhysicalSlot, "report_Object_name_definition_pointer");
        return _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions[physical].Name;
    }

    private static byte ReadFacilityHistoryDistrict(SourceWorldContext current, ArmyUnitReference army)
    {
        ushort id = K(current.ReadArmyUnitCommanderId(army), "report_District_Commander12");
        var reference = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("report_District_Person_lookup");
        if (reference.PhysicalSlot is not int person) return 0;
        return K(current.ReadPersonNativeValidity(person), "report_District_Person_validity")
            ? K(current.ReadPersonDistrictSlot(person), "report_District_current_E0") : (byte)0;
    }
}
