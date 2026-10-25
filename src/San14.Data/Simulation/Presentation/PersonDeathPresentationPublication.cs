using San14.Core;

namespace San14.Data;

/// <summary>295EE0's ordered event work. Text/actor/voice are the source-time
/// publication, not a second formatting or a query of the now-dead officer.</summary>
public sealed record PersonDeathPresentationPublication(PersonDeathReportOperation Operation,
    FinalOrderIllustrationRoute? Illustration, string? AnimationName, int? SoundId,
    string? Text, IReadOnlyList<string> Pages, TalentOfficerPresentationContext? Speaker,
    TalentNarrationVoiceRequest? Voice, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<PersonDeathPresentationPublication> QueryPersonDeathPresentation(
        GameState state, PersonDeathReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        var operation = request.Operation;
        if (operation == PersonDeathReportOperation.EventEnter && request.EventKind is 4 or 5 or 6)
            return Known(FinalOrderIllustration(request.EventKind.Value), "stl_event_in");
        if (operation == PersonDeathReportOperation.EventExit && request.EventKind is null)
            return Known(animation: "stl_event_out");
        if (operation == PersonDeathReportOperation.ControllerQueueEvent && request.EventKind == 22)
            //405E40(manager,22,0), ordinary SE. It is not EventView kind22.
            return Known(sound: 22);
        if ((operation is PersonDeathReportOperation.SpeakerText or PersonDeathReportOperation.NarrationText) &&
            request.TextAtSource is { } text)
            return AreaAdministrationQuery<PersonDeathPresentationPublication>.Known(new(operation, null, null, null,
                text, SplitStoryPages(text), request.Speaker, request.Voice,
                operation == PersonDeathReportOperation.SpeakerText && request.Speaker is null
                    ? "person-death.presentation.source_speaker" : null));
        return AreaAdministrationQuery<PersonDeathPresentationPublication>.Unknown("person-death.presentation.request");

        AreaAdministrationQuery<PersonDeathPresentationPublication> Known(
            FinalOrderIllustrationRoute? illustration = null, string? animation = null, int? sound = null)
            => AreaAdministrationQuery<PersonDeathPresentationPublication>.Known(new(operation,
                illustration, animation, sound, null, [], null, null, null));
    }
}
