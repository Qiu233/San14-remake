using San14.Core;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //275960 ->1C2130 after creating a wall/chain: allocate a connecting
    //sub-object for the created and adjacent Objects with the same raw kind.
    private static void AppendAdjacentConstructionConnections(SourceWorldContext current,
        ObjectReference obj, List<CampaignEffectRequest> requests)
    {
        var source = current.ReadPhysicalObject(K(obj.PhysicalSlot, "adjacent_source_pointer"))
            ?? throw Missing("adjacent_source_fields");
        if (source.RawField12 is not (9 or 10 or 16)) return;
        int center = K(current.ReadObjectHexIndex(obj), "adjacent_source_Hex10");
        var created = current.CurrentMapObjectDirectory!.ReceiverAtPhysical(K(obj.PhysicalSlot, "adjacent_source_identity"));
        foreach (int hexId in TacticRemakeCurrentContext.NativeHexOrder(center, 1).Skip(1))
        {
            var hex = current.ReadHexTableReference(hexId) ?? throw Missing("adjacent_Hex_pointer");
            var neighbor = FacilityConstructionLegality.ObjectAt(current, hex);
            if (!K(current.ReadObjectNativeValidity(neighbor), "adjacent_Object_validity")) continue;
            int physical = K(neighbor.PhysicalSlot, "adjacent_Object_pointer");
            var fields = current.ReadPhysicalObject(physical) ?? throw Missing("adjacent_Object_fields");
            if (fields.RawField12 != source.RawField12) continue;
            var receiver = current.CurrentMapObjectDirectory!.ReceiverAtPhysical(physical);
            requests.Add(new(null, null) { FacilityConstructionPresentation = new(
                FacilityConstructionPresentationOperation.ConnectAdjacentFacility, created, center,
                MapObjectSourceDirectory.DefinitionKey(source.RawField12), null)
                { AdjacentReceiverId = receiver } });
        }
    }

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
        ArmyUnitReference army, HexReference site, FacilityOperation operation,
        List<CampaignEffectRequest> requests)
    {
        //2888B0 reads Home, its actual City identity and the Army raw Force
        //before185D00's own eligibility and unchanged-owner early returns.
        var home = current.FindArmyUnitCurrentBase(army);
        var homeCity = CurrentGateMapQueries.FindCity(home, current);
        int city = K(homeCity.PhysicalSlot, "ownership_Home_City_identity");
        int force = current.ReadArmyUnitRawForceSlot(army);
        if (!GateCaptureTerritorySource.Eligible(state, current, site, force, requireNoArmy: false)) return state;
        if (K(current.ReadHexForceSlot(site), "ownership_previous_Force") == force) return state;
        //The construction current already owns its fixed Scenery links and
        //live flags. Borrow the same pool and application lifetime; do not
        //materialize another world or replace its existing layout receiver.
        current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
        current.ArmyReturnTransferMode = ReadApplicationTransferModeCurrent();
        BindApplicationServiceCurrent(state, current);
        if (current.SceneryLayoutReader is null && current.CurrentSceneryLayout is null)
            current.CurrentSceneryLayout = new(null);
        bool playerCenter = K(current.ReadHexSurfaceCode(site), "ownership_surface") is >= 15 and <= 18 &&
            force == K((current.CurrentPlayerForce ?? throw Missing("ownership_player_pointer")).PhysicalSlot,
                "ownership_player_identity");
        var working = playerCenter ? PrepareApplicationServiceCurrent(state, current, profile => profile) : state;
        //The common185D00(flags1,1,0) kernel owns center/Scenery notices,
        //special-Force retention/neutralization, projections and map4/5 work.
        working = TransferTerritoryHexCurrent(working, current, site, force, city,
            enqueueEncirclement: true, allowType3Override: false, showAppearance: true,
            date: operation.ReadDate(), borrowedEffects: requests).State;
        if (!playerCenter) return working;
        var service = current.TransferService ?? throw Missing("ownership_service");
        return PrepareApplicationServiceCurrent(working, current, _ => service);
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
            receiver.Value, null, null, null)
            { FacilityArmySlot=K(army.PhysicalSlot,"speech_Army_packet_identity") }, null));
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
