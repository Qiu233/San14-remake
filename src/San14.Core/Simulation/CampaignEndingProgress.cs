using System.Collections.Immutable;

namespace San14.Core;

public enum CampaignEndingPresentationKind
{ Opening, AbdicationChoice, KingdomNameChoice, OfficerModal, Movie, CompletionNotices, Exit }

public sealed record CampaignEndingKingdomChoice(int Id, string Name)
{
    public string Description { get; init; } = string.Empty;
}
public sealed record CampaignEndingCompletionNotice(string TextAtSource, int? SoundId = null);

public sealed record CampaignEndingPresentation(long Sequence, CampaignEndingPresentationKind Kind,
    string? TextAtSource = null)
{
    public int? ForceId { get; init; }
    public int? RulerOfficerId { get; init; }
    public NativeMapSeasonDate? Date { get; init; }
    public TalentOfficerPresentationContext? Speaker { get; init; }
    public ImmutableArray<CampaignEndingKingdomChoice> KingdomChoices { get; init; } = [];
    public int? MovieId { get; init; }
    public ImmutableArray<CampaignEndingCompletionNotice> Notices { get; init; } = [];
}

/// <summary>The admitted Ending's current source stage and its actual awaited
/// presentation. Script text uses the same once-prepared cache as other stories.</summary>
public sealed record CampaignEndingProgress(bool FromEvent900, int Stage, int Substep, long NextSequence)
{
    public bool DeclinedAbdication { get; init; }
    public bool? AbdicationAccepted { get; init; }
    public int? ChosenKingdomId { get; init; }
    public CampaignEndingPresentation? Pending { get; init; }
    public CampaignStoryRequest? Story { get; init; }
    public bool Completed { get; init; }
}

public sealed record CampaignEndingPreparationResult(GameState? State, string? UnresolvedOwner = null);

public interface ICampaignEndingRules
{
    //The supplied world is borrowed read-only. Return only the new Ending,
    //App/profile facts and the actual business writes reached by this stage.
    CampaignEndingPreparationResult PrepareCampaignEnding(GameState candidate, bool fromEvent900 = false);
    CampaignEndingPreparationResult CompleteCampaignEndingPresentation(GameState candidate,
        CampaignEndingProgress progress, long sequence, bool? abdicationAccepted, int? kingdomId);
    CampaignEndingPreparationResult CompleteCampaignEndingStory(GameState candidate, CampaignEndingProgress progress);
}

internal static class CampaignEndingProgressValidation
{
    internal static void Validate(CampaignEndingProgress? progress)
    {
        if (progress is null) return;
        if (progress.Stage is < 0 or > 15 || progress.Substep is < 0 or > 4 || progress.NextSequence < 1 ||
            progress.ChosenKingdomId is < 0 || progress.Pending is not null && progress.Story is not null ||
            progress.Completed && (progress.Stage != 15 || progress.Pending is not null || progress.Story is not null))
            throw new InvalidDataException("Ending progress has an invalid stage or awaited presentation.");
        if (progress.Story is { } story)
        {
            story.Validate();
            if (story.Sequence >= progress.NextSequence) throw new InvalidDataException("Ending story sequence is outside its owner.");
        }
        if (progress.Pending is not { } pending) return;
        if (!Enum.IsDefined(pending.Kind) || pending.Sequence < 1 || pending.Sequence >= progress.NextSequence ||
            pending.ForceId is < 0 or > 51 || pending.RulerOfficerId is < 0 or > 6000 ||
            pending.KingdomChoices.IsDefault || pending.Notices.IsDefault ||
            pending.Kind != CampaignEndingPresentationKind.KingdomNameChoice && !pending.KingdomChoices.IsEmpty ||
            pending.Kind != CampaignEndingPresentationKind.CompletionNotices && !pending.Notices.IsEmpty ||
            pending.Kind != CampaignEndingPresentationKind.Movie && pending.MovieId is not null ||
            pending.Kind != CampaignEndingPresentationKind.OfficerModal && pending.Speaker is not null)
            throw new InvalidDataException("Ending presentation has an invalid source snapshot.");
        bool valid = pending.Kind switch
        {
            CampaignEndingPresentationKind.Opening => pending.TextAtSource is not null && pending.ForceId is not null && pending.Date is not null,
            CampaignEndingPresentationKind.AbdicationChoice => pending.TextAtSource is not null,
            CampaignEndingPresentationKind.KingdomNameChoice => !pending.KingdomChoices.IsEmpty &&
                pending.KingdomChoices.All(choice => choice is not null && choice.Id >= 0 && choice.Name is not null && choice.Description is not null) &&
                pending.KingdomChoices.Select(choice => choice.Id).Distinct().Count() == pending.KingdomChoices.Length,
            CampaignEndingPresentationKind.OfficerModal => pending.TextAtSource is not null && pending.Speaker is { NameAtSource: not null, OfficerId: >= 0 and <= 6000 },
            CampaignEndingPresentationKind.Movie => pending.MovieId == 45,
            CampaignEndingPresentationKind.CompletionNotices => pending.Notices.All(notice => notice is not null && notice.TextAtSource is not null && notice.SoundId is null or 5),
            CampaignEndingPresentationKind.Exit => true,
            _ => false
        };
        if (!valid) throw new InvalidDataException("Ending presentation is missing its actual text, choice, actor or media.");
    }
}

public sealed partial record GameState
{
    public CampaignEndingProgress? EndingProgress { get; init; }
}
