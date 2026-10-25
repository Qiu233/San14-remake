using System.Collections.Immutable;

namespace San14.Core;

public sealed record CampaignStoryPreparedText(string SpeakerName, string Text,
    ImmutableArray<string> Pages, TalentOfficerPresentationContext? Speaker,
    ImmutableArray<TalentNarrationVoiceRequest> Voices);
public sealed record CampaignStoryPreparedStep(int Step, CampaignStoryPreparedText? Text);
public sealed record CampaignStoryPreparationResult(CampaignStoryPreparedStep? Prepared,
    CampaignAppRandomState? AppRandomState, string? UnresolvedOwner);

public interface ICampaignStoryPreparationRules
{
    CampaignStoryPreparationResult PrepareNormalTurnStoryStep(GameState candidate, CampaignStoryRequest story);
}

internal static class CampaignStoryPreparationValidation
{
    internal static bool Speaker(TalentOfficerPresentationContext? speaker) => speaker is
        { OfficerId: >= 0 and <= 6000, NameAtSource: not null };

    internal static void ValidateText(CampaignStoryPreparedText? text)
    {
        if (text is null) return;
        if (text.SpeakerName is null || text.Text is null || text.Pages.IsDefault || text.Pages.Any(page => page is null) ||
            text.Speaker is { } speaker && !Speaker(speaker) || text.Voices.IsDefault ||
            text.Voices.Any(voice => voice is null || voice.SpeakerId is < 0 or > 6000 || voice.VoiceId < 0 ||
                voice.VariantIndex < 0 || !Enum.IsDefined(voice.Language)))
            throw new InvalidDataException("Prepared story text is incomplete.");
    }
}
