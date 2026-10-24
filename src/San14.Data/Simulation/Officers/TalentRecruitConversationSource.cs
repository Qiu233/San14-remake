using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private NarrationVoiceSettings? _personnelNarrationVoice;

    /// <summary>Application preferences for source-time dialogue preparation.
    /// The campaign never reads application settings or the original process.</summary>
    public void ConfigurePersonnelNarrationVoice(NarrationVoiceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _personnelNarrationVoice = settings;
    }

    //6C5F50/72BD80/735B50: format while both actors still have their pre-join
    //relationships. UI consumption only displays these already selected lines.
    private GameState AppendTalentRecruitConversationCurrent(GameState state, SourceWorldContext current,
        int executor, int target, bool succeeded, List<CampaignEffectRequest> effects)
    {
        var force = current.ReadPersonForce(executor) ?? throw TalentCommandMissing("conversation_executor_Force");
        //72BD80 enters state5 directly for an executor outside the player Force.
        if (!TalentForceIsPlayerCurrent(state, current, force)) return state;
        int executorId = TalentNeed(current.ReadPersonIdentity(executor), "conversation_executor10");
        int targetId = TalentNeed(current.ReadPersonIdentity(target), "conversation_target10");
        int rulerId = TalentRulerCurrent(current, force);
        var actors = new TalentRecruitConversationContext(executorId, targetId,
            Name(executorId), Name(targetId), current.ReadPersonPortraitAppearance(executor),
            current.ReadPersonPortraitAppearance(target));
        var settings = _personnelNarrationVoice ?? throw TalentCommandMissing("conversation_voice_settings");
        var opening = FormatReportMessage(state, current, 0xC366,
            new(Person0: executorId, Person1: targetId, Person2: rulerId), ReportMessageOrigin.Talent, null, settings);
        effects.Add(Line(opening, executorId));
        //The second VM runs after the opening, with reversed Person0/1 and
        //the same executor Force ruler. Voice40 retains its real branch/draws.
        var answer = FormatReportMessage(opening.State, current, succeeded ? 0xC3B6 : 0xC3C5,
            new(Person0: targetId, Person1: executorId, Person2: rulerId), ReportMessageOrigin.Talent, null, settings);
        effects.Add(new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.SoundCue, succeeded ? 5 : 6, null, null, null, null) });
        effects.Add(Line(answer, targetId));
        return answer.State;

        string Name(int id)
        {
            var officer = state.Officers.GetValueOrDefault(id) ?? throw TalentCommandMissing("conversation_Person_owner");
            return (officer.CurrentSurname ?? throw TalentCommandMissing("conversation_surname")) +
                (officer.CurrentGivenName ?? throw TalentCommandMissing("conversation_given_name"));
        }

        CampaignEffectRequest Line(HistoryTextResult formatted, int speaker)
        {
            //The actual three-root program has one optional Voice40 request
            //on its answer. Do not silently discard a future program's audio.
            if (formatted.Voices.Count > 1) throw TalentCommandMissing("conversation_multiple_voice_requests");
            TalentNarrationVoiceRequest? voice = formatted.Voices.Count == 0 ? null : SaveVoice(formatted.Voices[0]);
            return new(null, null) { TalentCommandPresentation = new(
                TalentCommandPresentationOperation.OfficerText, null, speaker, targetId, succeeded, formatted.Text)
            { RecruitConversation = actors, Voice = voice } };
        }
    }

    private static TalentNarrationVoiceRequest SaveVoice(NarrationVoiceRequest voice) => new(
        voice.SpeakerId, voice.VoiceType, voice.VoiceId, voice.Language switch
        {
            NarrationVoiceLanguage.Japanese => PersonnelVoiceLanguage.Japanese,
            NarrationVoiceLanguage.Chinese => PersonnelVoiceLanguage.Chinese,
            _ => throw TalentCommandMissing("conversation_voice_language")
        }, voice.VariantIndex, voice.SoundId);
}
