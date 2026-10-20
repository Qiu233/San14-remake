using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // The operation retains logical source-time arguments. The application
    // still owns optional View slots, real pose, playback and report admission.
    private GameState AppendFacilityRepairEffects(GameState state, SourceWorldContext current,
        ArmyUnitReference army, UnitState image, FacilityRepairNumberResult numbers, ushort commander,
        NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> requests)
    {
        Presentation(FacilityRepairPresentationOperation.EnsureRepairLoop);
        if (!numbers.ReachedCapacity) return state;
        Presentation(FacilityRepairPresentationOperation.FinishRepairLoop);
        if (numbers.FirstCompletion)
        {
            Presentation(FacilityRepairPresentationOperation.RefreshFacilityCompletionModel);
            if (RepairActorForceIsPlayer(state, current, army) || numbers.Category != 4)
            {
                if (state.PersonnelCommands is null) throw RepairMissing("speech_personnel_owner");
                PersonReturnCommands.BindCurrent(state, current);
                var person = (commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0))
                    ?? throw RepairMissing("speech_person_pointer");
                int physical = RepairNeed(person.PhysicalSlot, "speech_person_identity");
                int position = PersonReturnCommands.ResolvePersonPosition(state, current, physical,
                    moment.PreWriterThirdDate).PhysicalSlot ?? 48400;
                state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
                    ?? throw RepairMissing("speech_personnel_publication") };
                int officer = RepairNeed(current.ReadPersonIdentity(physical), "speech_person_identity");
                requests.Add(new(new(CombatParticipantKind.Army, image.Id, officer,
                    CombatSpeechEvent.ArmyFacilityCompleted, position,
                    CombatParticipantKind.FieldObject, numbers.Receiver.Value, null, null, null), null));
            }
            Presentation(FacilityRepairPresentationOperation.FacilityCompletionVisual, commander);
            Presentation(FacilityRepairPresentationOperation.FacilityCompletionSound);
            if (numbers.DefinitionIdentity is 7 or 8)
                Presentation(FacilityRepairPresentationOperation.EnsureFacilityAmbientLoop);
            if (IsPlayerRulerLedDistrict(state, current, image))
            {
                // Army44's independent owner is not reconstructed from a
                // route, override target, current position or command memory.
                if (numbers.Category == 3) throw RepairMissing("first_report_army44_owner");
                AppendFacilityRepairReport(state, current, army, image, numbers, commander, moment,
                    CampaignReportEvent.PlayerFacilityConstructionCompleted, requests);
            }
            if (numbers.DefinitionIdentity is 3 or 4)
                throw RepairMissing("first_completion_ordered_territory_tail");
        }
        else if (IsPlayerRulerLedDistrict(state, current, image))
            AppendFacilityRepairReport(state, current, army, image, numbers, commander, moment,
                CampaignReportEvent.PlayerFacilityRepairCompleted, requests);
        return state;

        void Presentation(FacilityRepairPresentationOperation operation, int? rawCommander = null) =>
            requests.Add(new(null, null) { FacilityRepairPresentation = new(operation,
                numbers.Receiver, numbers.HexId, MapObjectSourceDirectory.DefinitionKey(numbers.DefinitionIdentity), rawCommander) });
    }

    private static bool RepairActorForceIsPlayer(GameState state, SourceWorldContext current, ArmyUnitReference army)
    {
        var force = current.ReadArmyUnitForceReference(army);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw RepairMissing("speech_current_player");
        return RepairNeed(current.ReadForceNativeValidity(player), "speech_player_validity") &&
            RepairNeed(force.PhysicalSlot, "speech_actor_force_identity") == player.PhysicalSlot;
    }

    private void AppendFacilityRepairReport(GameState state, SourceWorldContext current,
        ArmyUnitReference army, UnitState image, FacilityRepairNumberResult numbers, ushort commander,
        NativeArmyProgressStageMoment moment, CampaignReportEvent reportEvent, List<CampaignEffectRequest> requests)
    {
        int person = current.ResolveOfficerSource(image.OfficerId) ?? throw RepairMissing("report_person");
        int district = RepairNeed(current.ReadPersonDistrictSlot(person), "report_district");
        byte choice = image.NativeCommandChoice41 ?? throw RepairMissing("report_command_choice");
        int selected = district <= 51 ? district : 0;
        bool valid = RepairNeed(current.ReadDistrictNativeValidity(selected), "report_district_validity");
        byte rawForce = valid ? RepairNeed(current.ReadDistrictForceId(selected), "report_district_force") : (byte)0;
        int forcePhysical = rawForce <= 51 ? rawForce : 0;
        var force = current.ReadForceTableReference(forcePhysical) ?? throw RepairMissing("report_force_pointer");
        byte color = valid ? RepairNeed(current.ReadForceReportColor(force), "report_force_color") : (byte)60;
        byte kingdom = RepairNeed(current.ReadForceKingdomNameIndex(forcePhysical), "report_kingdom");
        int ruler = current.ReadForceRecord(forcePhysical)?.RawRuler10 ?? throw RepairMissing("report_ruler");
        string actorName = state.Officers.GetValueOrDefault(image.OfficerId)?.Name ?? throw RepairMissing("report_actor_name");
        string definitionName = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath)
            .Catalog.Definitions[numbers.DefinitionIdentity].Name;
        // These are the real formatter arguments, including the current
        // command choice identity. No fixed "build" text or City name is used.
        requests.Add(new(null, new(district, numbers.HexId, StatusCampaignDay(state, moment), commander,
            actorName, definitionName, CombatParticipantKind.FieldObject, numbers.Receiver.Value,
            forcePhysical, color, kingdom, ruler)
        {
            Event = reportEvent, CommandChoiceId = choice,
            RulerNameSnapshot = state.Officers.GetValueOrDefault(ruler)?.Name
        }));
    }

    private static GameState AppendFacilityRepairBatch(GameState state,
        NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> requests) =>
        state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw RepairMissing("effect_owner"))
            .Append(CampaignEffectSource.ArmyFacilityRepair, moment.CalendarCycleOrdinal, requests) };
}
