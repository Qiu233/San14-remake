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
                state = AppendFacilityRepairReport(state, current, army, image, numbers, commander, moment,
                    CampaignReportEvent.PlayerFacilityConstructionCompleted, requests);
            }
            if (numbers.DefinitionIdentity is 3 or 4)
                throw RepairMissing("first_completion_ordered_territory_tail");
        }
        else if (IsPlayerRulerLedDistrict(state, current, image))
            state = AppendFacilityRepairReport(state, current, army, image, numbers, commander, moment,
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

    private GameState AppendFacilityRepairReport(GameState state, SourceWorldContext current,
        ArmyUnitReference army, UnitState image, FacilityRepairNumberResult numbers, ushort commander,
        NativeArmyProgressStageMoment moment, CampaignReportEvent reportEvent, List<CampaignEffectRequest> requests)
    {
        int root = reportEvent switch
        {
            CampaignReportEvent.PlayerFacilityConstructionCompleted => 0xC358,
            CampaignReportEvent.PlayerFacilityRepairCompleted => 0xC359,
            _ => throw RepairMissing("report_source_variant")
        };
        string definitionName = _repository.LoadObjectDefinitions(_scenario.FixedDataSource.SourcePath)
            .Catalog.Definitions[numbers.DefinitionIdentity].Name;
        byte choice = image.NativeCommandChoice41 ?? throw RepairMissing("report_command_choice");
        string choiceName = ReadHistoryChoiceName(state, choice);
        var formatted = FormatHistoryReport(state, current, root,
            new HistoryMessageOperands(Army0: army, String0: definitionName, String1: choiceName));
        state = formatted.State;
        //The raw commander was captured by2E8320 before its completion
        //branch. Do not replace it with a fresh post-format Person query.
        int day = StatusCampaignDay(state, moment);
        int hex = RepairNeed(current.ReadObjectHexIndex(new(numbers.Physical)), "report_current_Object10");
        int district = ReadFacilityHistoryDistrict(current, army);
        return WriteHistory(state, current, new ReportCommand(5, district, hex, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(state), commander, MarkBit4: 1), requests);
    }

    private static GameState AppendFacilityRepairBatch(GameState state,
        NativeArmyProgressStageMoment moment, List<CampaignEffectRequest> requests) =>
        state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw RepairMissing("effect_owner"))
            .Append(CampaignEffectSource.ArmyFacilityRepair, moment.CalendarCycleOrdinal, requests) };
}
