using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal HistoryTextResult FormatDoctrineResearchReport(GameState state, SourceWorldContext current,
        int root, int rulerId) => FormatReportMessage(state, current, root,
            new(Person0: rulerId), ReportMessageOrigin.DoctrineResearch, null,
            _personnelNarrationVoice ?? throw new InvalidDataException("doctrine.research.voice_settings"));

    internal static CampaignEffectRequest CreateDoctrineResearchPresentation(SourceWorldContext current,
        GameState sourceState, int rulerId, HistoryTextResult formatted) =>
        CreateSingleOfficerPresentation(current, sourceState, rulerId, formatted,
            TalentCommandPresentationOperation.OfficerText, "doctrine.research");

    private static CampaignEffectRequest CreateSingleOfficerPresentation(SourceWorldContext current,
        GameState sourceState, int rulerId, HistoryTextResult formatted,
        TalentCommandPresentationOperation operation, string missingPrefix)
    {
        if (formatted.Voices.Count > 1)
            throw new InvalidDataException(missingPrefix + ".multiple_voices");
        var lookup = current.ReadPersonLookupReference(rulerId) ??
            throw new InvalidDataException(missingPrefix + ".speaker_lookup");
        int physical = lookup.PhysicalSlot ?? throw new InvalidDataException(missingPrefix + ".speaker_pointer");
        int identity = current.ReadPersonIdentity(physical) ?? throw new InvalidDataException(missingPrefix + ".speaker_identity");
        var person = sourceState.Officers.GetValueOrDefault(identity) ??
            throw new InvalidDataException(missingPrefix + ".speaker_owner");
        string surname = person.CurrentSurname ?? throw new InvalidDataException(missingPrefix + ".speaker_surname");
        string given = person.CurrentGivenName ?? throw new InvalidDataException(missingPrefix + ".speaker_given_name");
        return new(null, null)
        {
            TalentCommandPresentation = new(operation,
                null, rulerId, null, null, formatted.Text)
            {
                Speaker = new(rulerId, surname + given, current.ReadPersonPortraitAppearance(physical)),
                Voice = formatted.Voices.Count == 0 ? null : SaveVoice(formatted.Voices[0])
            }
        };
    }
}
