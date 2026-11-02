using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyPersonnelCreditReportsCurrent(GameState state, SourceWorldContext current,
        int forceId, IReadOnlyList<int> selectedOfficerIds, bool rankup, List<CampaignEffectRequest> sink,
        GameState sourceState, IReadOnlyList<int>? promotedIds = null,
        bool emitInitialSound = true, bool choiceOnly = false)
    {
        if (selectedOfficerIds.Count == 0) return state;
        int first = selectedOfficerIds[0];
        int ruler = PersonnelAdministrationRuler(sourceState, forceId);
        if (emitInitialSound) PersonnelAdministrationSound(sink, rankup ? 5 : 4);
        var line = FormatPersonnelAdministrationCurrent(state, current, 0x5C56,
            new(Person0: first, Person1: ruler));
        sink.Add(PersonnelAdministrationOfficerLine(current, sourceState, first, ruler, line));
        if (!rankup) PersonnelAdministrationSound(sink, 5);
        var notice = FormatPersonnelAdministrationCurrent(line.State, current,
            rankup ? choiceOnly ? selectedOfficerIds.Count == 1 ? 0x5C53 : 0x5C54
                : selectedOfficerIds.Count == 1 ? 0x5C51 : 0x5C52
                : selectedOfficerIds.Count == 1 ? 0x5C5E : 0x5C5F,
            new(Person0: first));
        PersonnelAdministrationAnnouncement(sink, notice.Text);
        if (promotedIds is not { Count: > 0 }) return notice.State;
        //334D80 consumes the operation's already sorted notification list.
        //It names no more than three people, while Integer0 retains its size.
        var promotion = FormatPersonnelAdministrationCurrent(notice.State, current,
            0x5D21 + Math.Min(promotedIds.Count - 1, 3),
            new(Integers: [promotedIds.Count], Person2: promotedIds[0],
                Person3: promotedIds.Count >= 2 ? promotedIds[1] : null,
                Person4: promotedIds.Count >= 3 ? promotedIds[2] : null));
        PersonnelAdministrationAnnouncement(sink, promotion.Text);
        return promotion.State;
    }

    internal GameState ApplyPersonnelRewardReportsCurrent(GameState state, SourceWorldContext current,
        int forceId, int representativeId, int selectedCount, List<CampaignEffectRequest> sink,
        GameState sourceState)
    {
        int ruler = PersonnelAdministrationRuler(sourceState, forceId);
        var line = FormatPersonnelAdministrationCurrent(state, current, 0x585D,
            new(Person0: representativeId, Person1: ruler));
        sink.Add(PersonnelAdministrationOfficerLine(current, sourceState, representativeId, ruler, line));
        var notice = FormatPersonnelAdministrationCurrent(line.State, current,
            selectedCount == 1 ? 0x5867 : 0x5868,
            new(Person0: representativeId, Integers: [selectedCount]));
        PersonnelAdministrationAnnouncement(sink, notice.Text);
        return notice.State;
    }

    internal GameState ApplyPersonnelBanishReportsCurrent(GameState state, SourceWorldContext current,
        int forceId, IReadOnlyList<int> selectedOfficerIds, bool firstWasCaptive,
        List<CampaignEffectRequest> sink, GameState sourceState)
    {
        if (selectedOfficerIds.Count == 0) return state;
        int first = selectedOfficerIds[0];
        int ruler = PersonnelAdministrationRuler(sourceState, forceId);
        var line = FormatPersonnelAdministrationCurrent(state, current, firstWasCaptive ? 0x5896 : 0x588C,
            new(Person0: first, Person1: ruler));
        sink.Add(PersonnelAdministrationOfficerLine(current, sourceState, first, ruler, line));
        int root = firstWasCaptive ? selectedOfficerIds.Count == 1 ? 0x5889 : 0x588B
            : selectedOfficerIds.Count == 1 ? 0x5888 : 0x588A;
        var notice = FormatPersonnelAdministrationCurrent(line.State, current, root,
            new(Person0: first, Integers: [selectedOfficerIds.Count]));
        PersonnelAdministrationAnnouncement(sink, notice.Text);
        return notice.State;
    }

    private HistoryTextResult FormatPersonnelAdministrationCurrent(GameState state,
        SourceWorldContext current, int root, HistoryMessageOperands operands)
        => FormatReportMessage(state, current, root, operands, ReportMessageOrigin.PersonnelAdministration,
            null, _personnelNarrationVoice ?? throw PersonnelAdministrationReportMissing("voice_settings"));

    private static int PersonnelAdministrationRuler(GameState state, int forceId)
        => state.Forces.GetValueOrDefault(forceId)?.RulerId
            ?? throw PersonnelAdministrationReportMissing("ruler");

    private static CampaignEffectRequest PersonnelAdministrationOfficerLine(SourceWorldContext current,
        GameState source, int speaker, int ruler, HistoryTextResult line)
    {
        if (line.Voices.Count > 1) throw PersonnelAdministrationReportMissing("multiple_voices");
        var context = new TalentRecruitConversationContext(speaker, ruler, Name(speaker), Name(ruler),
            Appearance(speaker), Appearance(ruler));
        return new(null, null)
        {
            TalentCommandPresentation = new(TalentCommandPresentationOperation.OfficerText,
                null, speaker, ruler, null, line.Text)
            { RecruitConversation = context, Voice = line.Voices.Count == 0 ? null : SaveVoice(line.Voices[0]) }
        };

        string Name(int id)
        {
            var person = source.Officers.GetValueOrDefault(id)
                ?? throw PersonnelAdministrationReportMissing("source_person");
            return (person.CurrentSurname ?? throw PersonnelAdministrationReportMissing("source_surname")) +
                (person.CurrentGivenName ?? throw PersonnelAdministrationReportMissing("source_given_name"));
        }
        ushort? Appearance(int id) => current.ReadPersonPortraitAppearance(current.ResolveOfficerSource(id)
            ?? throw PersonnelAdministrationReportMissing("source_person_pointer"));
    }

    private static void PersonnelAdministrationAnnouncement(List<CampaignEffectRequest> sink, string text)
        => sink.Add(new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.Announcement, null, null, null, null, text) });

    private static void PersonnelAdministrationSound(List<CampaignEffectRequest> sink, int sound)
        => sink.Add(new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.SoundCue, sound, null, null, null, null) });

    private static InvalidDataException PersonnelAdministrationReportMissing(string field)
        => new("personnel.administration.report." + field);
}
