using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //One reached settlement owns the pre-transfer report operands. They are
    //ordinary local values, not a new saved task or a replayed source result.
    internal GameState CompleteDemandSurrenderCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, bool accepted, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        DemandSurrenderReportSnapshot report;
        if (state.DemandSurrenderCompletionTail is { } resumed)
        {
            if (resumed.Task.Id != task.Id) throw DiplomacyMissing("surrender_completion_owner");
            task = resumed.Task; accepted = resumed.Accepted; date = resumed.OperationDate; report = resumed.Report;
            state = ResumeDemandSurrenderAbsorptionCurrent(state, current);
            if (HasPendingForceAbsorptionCurrent(state)) return state;
        }
        else
        {
            report = ReadDemandSurrenderHistoryCurrent(state, current, task);
            state = ApplyDemandSurrenderNumericCurrent(state, current, task, task.InitialOffer, accepted, effects, date);
            if (HasPendingForceAbsorptionCurrent(state))
                return state with { DemandSurrenderCompletionTail = new(task, accepted, date, report) };
        }
        state = WriteDemandSurrenderHistoryCurrent(state, current, task, accepted, date, effects, report);
        int actor = DiplomacyLookupPerson(current, task.MemberIds[0]);
        state = ApplyTalentCommonRewardsCurrent(state, current, actor, 7, accepted ? 1 : 2);
        state = SetDiplomacyTaskCurrent(state, current, task with { AwaitingNegotiation = false });
        return FinishDiplomacyReturnCurrent(state, current, task, date) with { DemandSurrenderCompletionTail = null };
    }

    //8232C0. No signed transfer or escrow belongs to this command.
    internal GameState ApplyDemandSurrenderNumericCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, DiplomacyOffer offer, bool accepted,
        List<CampaignEffectRequest> effects, NativeMapSeasonDate? date = null)
    {
        if (state.DemandSurrenderAbsorptionTail is { } resumed)
        {
            if (resumed.Task.Id != task.Id) throw DiplomacyMissing("surrender_absorption_owner");
            return ResumeDemandSurrenderAbsorptionCurrent(state, current);
        }
        if (offer != new DiplomacyOffer(0, 0) || task.InitialOffer != new DiplomacyOffer(0, 0))
            throw DiplomacyCommandSource.Invalid("surrender_has_no_settlement_offer");
        if (!accepted)
            return ApplyDiplomacyRelationCurrent(state, current, task.ForceId, task.TargetForceId, -20, effects);
        //27A6B0 precedes all absorption. Its terminal branch is32BD90,
        //implemented by the common force lifecycle owner, never a false ACK.
        var terminal = TryApplyDemandSurrenderTerminalCurrent(state, current, task.TargetForceId, task.ForceId, effects);
        if (terminal is not null) return terminal;
        var pool = current.ReadOwnedCurrentListPool() ?? throw DiplomacyMissing("surrender_settlement_pool");
        var target = DiplomacyForce(current, task.TargetForceId);
        var own = DiplomacyForce(current, task.ForceId);
        int ruler = DiplomacyRulerId(current, own);
        int targetHex = current.ReadBaseCurrentHexIndex(new DiplomacyCommandSource(state, current).Capital(task.TargetForceId));
        bool ownPlayer = TalentForceIsPlayerCurrent(state, current, own);
        int? chronicleSlot = null;
        //8232C0 writes CHistoryTableManager event96/family1/kind17 before
        //2A2760. This is independent of the later ordinary history ring.
        if ((state.CampaignChronicle ?? throw DiplomacyMissing("surrender_chronicle_owner")).RecordingSuppression is not int suppression)
            throw DiplomacyMissing("surrender_chronicle_suppression");
        if (suppression == 0)
        {
            var before = state.CampaignChronicle.PrimaryEntries;
            string Name(OfficerState person, bool full) => full ? person.Name :
                person.CurrentSurname ?? throw DiplomacyMissing("surrender_chronicle_surname");
            state = new CampaignChronicleWriter(HistoryWriter).Write(state, current,
                new(96, 1, (ReadArmyDestructionSampler(state, current) ?? throw DiplomacyMissing("surrender_chronicle_day")).Day450,
                    HistoryWriter.CustomFirstName(state, current, own, Name),
                    DNeed(current.ReadForceReportColor(own), "surrender_chronicle_color"), 17,
                    ImmutableArray.Create(SurrenderForceName(state, current, target),
                        state.Officers.GetValueOrDefault(ruler)?.Name ?? throw DiplomacyMissing("surrender_chronicle_ruler"), string.Empty),
                    ImmutableArray.Create(targetHex, 48400, 48400, 48400)),
                ReadApplicationGameplay()?.UserSelectionServicePresent);
            //The writer may be blocked by the real user-selection service.
            //Keep only the actual changed entry for the later8ED160 refresh.
            var after = state.CampaignChronicle!.PrimaryEntries;
            for (int slot = 0; slot < before.Length; slot++)
                if (!ReferenceEquals(before[slot], after[slot])) { chronicleSlot = slot; break; }
        }
        state = AppendDiplomacyEffects(state, effects);
        effects.Clear();
        state = ApplyNormalTurnForceAbsorptionCurrent(state, current, pool, target, own, suppressEliminationPresentation: false);
        if (HasPendingForceAbsorptionCurrent(state))
            return state with { DemandSurrenderAbsorptionTail = new(task, ownPlayer, chronicleSlot) };
        return CompleteDemandSurrenderChronicleCurrent(state, current, ownPlayer, chronicleSlot);
    }

    private GameState ResumeDemandSurrenderAbsorptionCurrent(GameState state, SourceWorldContext current)
    {
        var tail = state.DemandSurrenderAbsorptionTail ?? throw DiplomacyMissing("surrender_absorption_tail");
        if (HasPendingForceAbsorptionCurrent(state))
            state = ResumeNormalTurnForceAbsorptionCurrent(state, current,
                current.ReadOwnedCurrentListPool() ?? throw DiplomacyMissing("surrender_resume_pool"));
        if (HasPendingForceAbsorptionCurrent(state)) return state;
        return CompleteDemandSurrenderChronicleCurrent(state, current, tail.OwnPlayer, tail.ChronicleSlot)
            with { DemandSurrenderAbsorptionTail = null };
    }

    private GameState CompleteDemandSurrenderChronicleCurrent(GameState state, SourceWorldContext current,
        bool ownPlayer, int? chronicleSlot)
    {
        if (ownPlayer && chronicleSlot is int written)
        {
            var chronology = state.CampaignChronicle ?? throw DiplomacyMissing("surrender_chronicle_after_absorption");
            var record = new CampaignChronicleWriter(HistoryWriter).Snapshot(state, current, chronology.PrimaryEntries[written]);
            state = state with { CampaignChronicle = chronology with { PrimaryEntries = chronology.PrimaryEntries.SetItem(written, record) } };
        }
        return state;
    }

    private DemandSurrenderReportSnapshot ReadDemandSurrenderHistoryCurrent(GameState state,
        SourceWorldContext current, PersonnelDiplomacyTask task)
    {
        int actor = DiplomacyReportActor(current, task);
        var own = current.ReadPersonForce(actor) ?? throw DiplomacyMissing("surrender_report_own");
        var target = DiplomacyForce(current, task.TargetForceId);
        var primary = ForcePrimaryDistrictSource.Resolve(current, target);
        var pool = current.ReadOwnedCurrentListPool() ?? throw DiplomacyMissing("surrender_report_pool");
        var selection = new PersonDeathHistorySelection(state, current, pool,
            new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language)), _governorInputs,
            (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect));
        int speaker = DNeed(selection.Select(primary).PhysicalSlot, "surrender_report_speaker");
        return new(DNeed(current.ReadPersonIdentity(actor), "surrender_report_actor_ID"),
            DiplomacyRulerId(current, own), DiplomacyRulerId(current, target),
            DNeed(current.ReadPersonDistrictSlot(actor), "surrender_report_actor_District"),
            DNeed(primary.PhysicalSlot, "surrender_report_target_District"),
            DNeed(current.ReadPersonIdentity(speaker), "surrender_report_speaker_ID"),
            ReadGrowthReportHex(current, new DiplomacyCommandSource(state, current).Capital(task.TargetForceId)),
            TalentForceIsPlayerCurrent(state, current, own), TalentForceIsPlayerCurrent(state, current, target),
            SurrenderForceName(state, current, target));
    }

    private string SurrenderForceName(GameState state, SourceWorldContext current, ForceReference force)
        => HistoryWriter.FallbackName(state, current, force, (person, full) => full
            ? HeadHistory(person.CurrentSurname ?? throw DiplomacyMissing("surrender_name_surname")) +
              HeadHistory(person.CurrentGivenName ?? throw DiplomacyMissing("surrender_name_given"))
            : HeadHistory(person.CurrentSurname ?? throw DiplomacyMissing("surrender_name_short")));

    private GameState WriteDemandSurrenderHistoryCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, bool accepted, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects, DemandSurrenderReportSnapshot report)
    {
        if (!accepted && !report.OwnPlayer && !report.TargetPlayer) return state;
        int sound = accepted ? report.OwnPlayer ? 5 : report.TargetPlayer ? 7 : 8 : 6;
        var pair = new HistoryMessageOperands(Person0: report.OwnRuler, Person1: report.TargetRuler);
        var announce = FormatDiplomacyReportCurrent(state, current, accepted ? 0x3175 : 0x3181,
            accepted ? new(Person0: report.OwnRuler, String0: report.TargetName) : pair);
        state = announce.State;
        DiplomacyPresent(effects, TalentCommandPresentationOperation.SoundCue, null, sound: sound);
        DiplomacyPresent(effects, TalentCommandPresentationOperation.Announcement, announce.Text);
        string? main, detail;
        int district = report.ActorDistrict, payload = report.Actor, category = 3, mark3 = 0;
        if (!accepted)
        {
            var text = FormatDiplomacyReportCurrent(state, current, 0x3182, pair);
            state = text.State; main = text.Text;
            if (report.OwnPlayer)
            {
                text = FormatDiplomacyReportCurrent(state, current, 0x3183,
                    new(Person0: report.Actor, Person1: report.OwnRuler, Person2: report.TargetRuler));
                state = text.State; detail = text.Text;
            }
            else { detail = CampaignHistoryWriter.Supplemental(state); payload = report.TargetSpeaker; district = report.TargetDistrict; }
        }
        else
        {
            var text = FormatDiplomacyReportCurrent(state, current, 0x3176,
                new(String0: report.TargetName, Integers: [date.Year, date.Month]));
            state = text.State;
            DiplomacyPresent(effects, TalentCommandPresentationOperation.Announcement, text.Text);
            text = FormatDiplomacyReportCurrent(state, current, 0x3177,
                new(Person0: report.OwnRuler, String0: report.TargetName));
            state = text.State; main = text.Text;
            if (report.OwnPlayer)
            {
                text = FormatDiplomacyReportCurrent(state, current, 0x3178,
                    new(Person0: report.Actor, Person1: report.OwnRuler, String0: report.TargetName));
                state = text.State; detail = text.Text;
            }
            else if (report.TargetPlayer) return state;
            else
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw DiplomacyMissing("surrender_report_observer");
                var primary = ForcePrimaryDistrictSource.Resolve(current, player);
                if (!DNeed(current.ReadDistrictNativeValidity(primary), "surrender_observer_District_validity")) return state;
                var pool = current.ReadOwnedCurrentListPool() ?? throw DiplomacyMissing("surrender_observer_pool");
                var selection = new PersonDeathHistorySelection(state, current, pool,
                    new(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language)), _governorInputs,
                    (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect));
                payload = DNeed(current.ReadPersonIdentity(DNeed(selection.Select(primary).PhysicalSlot,
                    "surrender_observer_speaker")), "surrender_observer_speaker_ID");
                text = FormatDiplomacyReportCurrent(state, current, 0x50EB,
                    new(Person0: payload, Person1: DiplomacyRulerId(current, player), Force0: task.ForceId,
                        String0: report.TargetName));
                state = text.State; detail = text.Text; category = 1; mark3 = 1;
            }
        }
        int day = (ReadArmyDestructionSampler(state, current) ?? throw DiplomacyMissing("surrender_report_day")).Day450;
        return WriteHistory(state, current, new(category, district, accepted ? 0 : report.TargetHex, day,
            main, detail, payload, MarkBit0: 1, MarkBit3: mark3, MarkBit4: accepted ? 0 : 1), effects);
    }
}
