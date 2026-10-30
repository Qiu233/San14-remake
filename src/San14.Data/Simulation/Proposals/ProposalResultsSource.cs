using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    private readonly List<ProposalResultRow> resultRows = [];
    private bool collectResults;

    internal void BeginResults(bool enabled) { resultRows.Clear(); collectResults = enabled; }
    internal void AddResult(string name, string before, string after, int? personPhysical = null)
    {
        if (!collectResults || resultRows.Count >= 12) return;
        TalentOfficerPresentationContext? person = null;
        if (personPhysical is int physical)
        {
            int id = PersonIdentity(physical);
            person = new(id, Package.ProposalPersonName(State, id), Current.ReadPersonPortraitAppearance(physical));
        }
        resultRows.Add(new(name, before, after, person));
    }

    internal void FlushResults()
    {
        if (collectResults && resultRows.Count != 0)
            AppendPresentation([new(null, null) { ProposalResultTable = new(ForceId, resultRows.ToArray()) }]);
        resultRows.Clear(); collectResults = false;
    }

    private HistoryTextResult FormatProposal(int root, HistoryMessageOperands operands)
    {
        var result = Package.FormatProposalReportCurrent(State, Current, root, operands);
        State = result.State;
        random.InstallCommittedState(State.CampaignAppRandom32?.State ?? throw Missing("text_App_owner"));
        return result;
    }

    internal void Speech(ProposalEntry entry, int root, HistoryMessageOperands operands)
    {
        var text = FormatProposal(root, operands);
        AppendPresentation([ArmyCampaignRuleAdapter.CreateProposalPresentation(Current, State,
            entry.ProposerPersonId, text)]);
    }

    internal void History(ProposalEntry entry, int root, HistoryMessageOperands operands, int hex)
    {
        var text = FormatProposal(root, operands);
        int person = PersonPhysical(entry.ProposerPersonId);
        int district = K(Current.ReadPersonDistrictSlot(person), "history_District");
        int day = (Package.ReadArmyDestructionSampler(State, Current) ?? throw Missing("history_day")).Day450;
        if (hex == 48400) hex = TargetHex(entry);
        List<CampaignEffectRequest> effects = [];
        State = Package.WriteHistory(State, Current, new(0, district, hex, day, text.Text,
            CampaignHistoryWriter.Supplemental(State), entry.ProposerPersonId, MarkBit0: 0, MarkBit4: 1), effects);
        AppendPresentation(effects);
    }

    internal void Report(ProposalEntry entry, int speechRoot, HistoryMessageOperands speech,
        int historyRoot, HistoryMessageOperands history, int hex)
    {
        Speech(entry, speechRoot, speech);
        History(entry, historyRoot, history, hex);
    }

    internal void Announcement(int root, HistoryMessageOperands operands)
    {
        var text = FormatProposal(root, operands);
        AppendPresentation([new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) }]);
    }
    internal void EnterScene(int kind) => AppendPresentation([new(null, null) { MonthlyMaintenance = new(
        MonthlyMaintenanceOperation.PersonnelSceneEnter, kind, null, null) }]);
    internal void ExitScene() => AppendPresentation([new(null, null) { MonthlyMaintenance = new(
        MonthlyMaintenanceOperation.PersonnelSceneExit, null, null, null) }]);
    internal void ShowOfficerDetail(int officerId) => AppendPresentation([
        new(null, null) { ProposalOfficerDetail = new(officerId) }]);
    internal void Sound(int sound) => AppendPresentation([new(null, null) { TalentCommandPresentation = new(
        TalentCommandPresentationOperation.SoundCue, sound, null, null, null, null) }]);

    private void AppendPresentation(IReadOnlyList<CampaignEffectRequest> effects)
    {
        if (effects.Count == 0) return;
        var owner = State.CampaignEffectRequests ?? throw Missing("presentation_owner");
        long revision = State.NativeClock?.UpdateOrdinal ?? throw Missing("presentation_revision");
        State = State with { CampaignEffectRequests = owner.Append(CampaignEffectSource.ProposalCommands, revision, effects) };
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //441480/453790's six actual proposal EventView stills: prisoner release9,
    //exploit/requisition10, security sabotage32, healing35, banquet44,
    //and Base endurance repair54. Actor/target and numerical results belong
    //to the separately queued source-time narration, not this static still.
    public AreaAdministrationQuery<FinalOrderIllustrationRoute?> QueryProposalScene(GameState state, MonthlyMaintenanceRequest request)
        => request.Operation switch
        {
            MonthlyMaintenanceOperation.PersonnelSceneEnter when request.EventKind is 9 or 10 or 32 or 35 or 44 or 54
                => AreaAdministrationQuery<FinalOrderIllustrationRoute?>.Known(FinalOrderIllustration(request.EventKind.Value)),
            MonthlyMaintenanceOperation.PersonnelSceneExit when request.EventKind is null
                => AreaAdministrationQuery<FinalOrderIllustrationRoute?>.Known(null),
            _ => AreaAdministrationQuery<FinalOrderIllustrationRoute?>.Unknown("proposal.scene_request")
        };
    public AreaAdministrationQuery<OfficerPortraitRoute> QueryProposalPortrait(GameState state, TalentOfficerPresentationContext person)
    {
        try
        {
            var current = CaptureProposalCurrent(state);
            var route = ReadOfficerPortraitAppearance(state, current, _definitions, person.OfficerId,
                person.PortraitAppearanceIndex ?? throw new InvalidDataException("proposal.source_appearance"), ArmyMapUiSources.Create());
            return AreaAdministrationQuery<OfficerPortraitRoute>.Known(route);
        }
        catch (InvalidDataException missing) { return AreaAdministrationQuery<OfficerPortraitRoute>.Unknown(missing.Message); }
    }
    internal static CampaignEffectRequest CreateProposalPresentation(SourceWorldContext current,
        GameState state, int speaker, HistoryTextResult text) => CreateSingleOfficerPresentation(current,
            state, speaker, text, TalentCommandPresentationOperation.ConditionalOfficerText, "proposal");
    internal string ProposalPersonName(GameState state, int id)
    {
        var person = state.Officers.GetValueOrDefault(id) ?? throw new InvalidDataException("proposal.Person_owner");
        return (person.CurrentSurname ?? throw new InvalidDataException("proposal.Person_surname")) +
            (person.CurrentGivenName ?? throw new InvalidDataException("proposal.Person_given_name"));
    }
}
