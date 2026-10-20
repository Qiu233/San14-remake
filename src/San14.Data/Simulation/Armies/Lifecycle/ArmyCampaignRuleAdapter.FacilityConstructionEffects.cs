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
        if (definition.PhysicalSlot != 7) throw Missing("arrival_Object_name_consumer_outside_kind7");
        if (moment is null) throw Missing("arrival_source_moment");
        var requests = new List<CampaignEffectRequest>();
        AppendConstructionReport(state, current, image, obj, moment,
            CampaignReportEvent.PlayerFacilityRepairStarted, requests);
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
        _ = K(player.PhysicalSlot, "model_player_identity");
        _ = K(force.PhysicalSlot, "model_Force_identity");
        //The actor consumer is kind7, category2.2C4830's category4
        //diplomacy visibility branch is never substituted with a visible flag.
        if (fields.RawField12 != 7) throw Missing("model_definition_consumer");
        return true;
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
        if (surface is >= 15 and <= 18) throw Missing("ownership_center_tail_outside_kind7_terrain");
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
        ArmyUnitReference army, UnitState image, ObjectReference obj, NativeArmyProgressStageMoment moment,
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
        int position = PersonReturnCommands.ResolvePersonPosition(state, current, person, moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
        state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw Missing("speech_personnel_publication") };
        int officer = K(current.ReadPersonIdentity(person), "speech_Person_character_identity");
        var receiver = current.CurrentMapObjectDirectory!.ReceiverAtPhysical(K(obj.PhysicalSlot, "speech_Object_identity"));
        requests.Add(new(new(CombatParticipantKind.Army, image.Id, officer,
            CombatSpeechEvent.ArmyFacilityConstructionStarted, position, CombatParticipantKind.FieldObject,
            receiver.Value, null, null, null), null));
        return state;
    }

    private void AppendConstructionReport(GameState state, SourceWorldContext current, UnitState image,
        ObjectReference obj, NativeArmyProgressStageMoment moment, CampaignReportEvent reportEvent,
        List<CampaignEffectRequest> requests)
    {
        int person = current.ResolveOfficerSource(image.OfficerId) ?? throw Missing("report_Person_pointer");
        int district = K(current.ReadPersonDistrictSlot(person), "report_District");
        int selected = district <= 51 ? district : 0;
        bool valid = K(current.ReadDistrictNativeValidity(selected), "report_District_validity");
        byte rawForce = valid ? K(current.ReadDistrictForceId(selected), "report_District_Force") : (byte)0;
        int physical = rawForce <= 51 ? rawForce : 0;
        var force = current.ReadForceTableReference(physical) ?? throw Missing("report_Force_pointer");
        byte color = valid ? K(current.ReadForceReportColor(force), "report_Force_color") : (byte)60;
        byte kingdom = K(current.ReadForceKingdomNameIndex(physical), "report_kingdom");
        int ruler = current.ReadForceRecord(physical)?.RawRuler10 ?? throw Missing("report_ruler");
        string actorName = state.Officers.GetValueOrDefault(image.OfficerId)?.Name ?? throw Missing("report_actor_name");
        var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
        int kind = K(definition.PhysicalSlot, "report_definition_identity");
        string targetName = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath).Catalog.Definitions[kind].Name;
        var receiver = current.CurrentMapObjectDirectory!.ReceiverAtPhysical(K(obj.PhysicalSlot, "report_Object_identity"));
        int hex = K(current.ReadObjectHexIndex(obj), "report_Object_Hex");
        int commander = K(current.ReadArmyUnitCommanderId(new(image.NativeArmyOrigin!.NativeSlotIndex)), "report_commander");
        requests.Add(new(null, new(district, hex, StatusCampaignDay(state, moment), commander,
            actorName, targetName, CombatParticipantKind.FieldObject, receiver.Value, physical, color, kingdom, ruler)
        { Event = reportEvent, RulerNameSnapshot = state.Officers.GetValueOrDefault(ruler)?.Name }));
    }
}
