namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Prepares one actual Ending wait on the committed world. A
    /// normal-period transaction must publish its terminal result first.</summary>
    public bool TryPrepareEndingStep(GameState expectedOwner, ICampaignEndingRules rules,
        out CampaignEndingProgress? prepared, bool fromEvent900 = false) =>
        TryPrepareEndingStep(expectedOwner, rules, out prepared, out _, fromEvent900);

    public bool TryPrepareEndingStep(GameState expectedOwner, ICampaignEndingRules rules,
        out CampaignEndingProgress? prepared, out string? unresolvedOwner, bool fromEvent900 = false)
    {
        prepared = null;
        unresolvedOwner = "ending.preparation_owner";
        if (!EndingOwner(expectedOwner)) return false;
        var progress = State.EndingProgress;
        if (progress is { Story: { } story } && story.NextStep == story.Steps.Length)
        {
            if (!AdoptEndingTransition(rules.CompleteCampaignEndingStory(State, progress), out unresolvedOwner)) return false;
            progress = State.EndingProgress;
        }
        if (progress is { Pending: not null } || progress is { Story: not null } || progress is { Completed: true })
        { prepared = progress; unresolvedOwner = null; return true; }
        if (!AdoptEndingTransition(rules.PrepareCampaignEnding(State, fromEvent900), out unresolvedOwner, requireProgress: false)) return false;
        prepared = State.EndingProgress;
        return true; //Known source refusal has no progress; it still publishes its actual admission result.
    }

    public bool TryCompleteEndingPresentation(GameState expectedOwner, CampaignEndingProgress expectedProgress,
        long sequence, ICampaignEndingRules rules, out string? unresolvedOwner)
    {
        unresolvedOwner = "ending.presentation_owner";
        if (!EndingHead(expectedOwner, expectedProgress, sequence, out var presentation) ||
            presentation.Kind is CampaignEndingPresentationKind.AbdicationChoice or CampaignEndingPresentationKind.KingdomNameChoice) return false;
        return AdoptEndingTransition(rules.CompleteCampaignEndingPresentation(State, expectedProgress, sequence, null, null), out unresolvedOwner);
    }

    public bool TryConfirmEndingAbdication(GameState expectedOwner, CampaignEndingProgress expectedProgress,
        long sequence, bool accepted, ICampaignEndingRules rules, out string? unresolvedOwner)
    {
        unresolvedOwner = "ending.abdication_owner";
        return EndingHead(expectedOwner, expectedProgress, sequence, out var presentation) &&
            presentation.Kind == CampaignEndingPresentationKind.AbdicationChoice &&
            AdoptEndingTransition(rules.CompleteCampaignEndingPresentation(State, expectedProgress, sequence, accepted, null), out unresolvedOwner);
    }

    public bool TryConfirmEndingKingdom(GameState expectedOwner, CampaignEndingProgress expectedProgress,
        long sequence, int kingdomId, ICampaignEndingRules rules, out string? unresolvedOwner)
    {
        unresolvedOwner = "ending.kingdom_owner";
        return EndingHead(expectedOwner, expectedProgress, sequence, out var presentation) &&
            presentation.Kind == CampaignEndingPresentationKind.KingdomNameChoice &&
            presentation.KingdomChoices.Any(choice => choice.Id == kingdomId) &&
            AdoptEndingTransition(rules.CompleteCampaignEndingPresentation(State, expectedProgress, sequence, null, kingdomId), out unresolvedOwner);
    }

    public bool TryCancelEndingKingdom(GameState expectedOwner, CampaignEndingProgress expectedProgress,
        long sequence, ICampaignEndingRules rules, out string? unresolvedOwner)
    {
        unresolvedOwner = "ending.kingdom_owner";
        return EndingHead(expectedOwner, expectedProgress, sequence, out var presentation) &&
            presentation.Kind == CampaignEndingPresentationKind.KingdomNameChoice &&
            AdoptEndingTransition(rules.CompleteCampaignEndingPresentation(State, expectedProgress, sequence, null, null), out unresolvedOwner);
    }

    public bool TryPrepareEndingStoryStep(GameState expectedOwner, ICampaignStoryPreparationRules rules,
        out CampaignStoryPreparationResult result)
    {
        result = new(null, null, "ending.story_owner");
        if (!EndingOwner(expectedOwner) || State.EndingProgress is not { Pending: null, Story: { } story } ending ||
            story.NextStep >= story.Steps.Length) return false;
        if (story.PreparedStep is { } cached)
        { result = new(cached, State.CampaignAppRandom32, null); return cached.Step == story.NextStep; }
        result = rules.PrepareNormalTurnStoryStep(State, story);
        if (result.UnresolvedOwner is not null || result.Prepared is not { } prepared || prepared.Step != story.NextStep ||
            story.Steps[story.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage && prepared.Text is null) return false;
        State = State with { CampaignAppRandom32 = result.AppRandomState ?? State.CampaignAppRandom32,
            EndingProgress = ending with { Story = story with { PreparedStep = prepared } } };
        return true;
    }

    public bool TryCompleteEndingStoryStep(GameState expectedOwner, CampaignEndingProgress expectedProgress,
        CampaignStoryStepReceipt receipt)
    {
        if (!EndingOwner(expectedOwner) || !ReferenceEquals(State.EndingProgress, expectedProgress) ||
            expectedProgress is not { Pending: null, Story: { } story } || receipt.RequestSequence != story.Sequence ||
            receipt.Step != story.NextStep || story.NextStep >= story.Steps.Length ||
            story.Steps[story.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage && story.PreparedStep is not { Text: not null }) return false;
        State = State with { EndingProgress = expectedProgress with { Story = story.CompleteStep(receipt) } };
        return true;
    }

    private bool EndingOwner(GameState expectedOwner) => _normalPeriod is null && _progress is null &&
        State.EditedEventPlayback is null && ReferenceEquals(State, expectedOwner);
    private bool EndingHead(GameState expectedOwner, CampaignEndingProgress expectedProgress, long sequence,
        out CampaignEndingPresentation presentation)
    {
        presentation = expectedProgress.Pending!;
        return EndingOwner(expectedOwner) && ReferenceEquals(State.EndingProgress, expectedProgress) &&
            !expectedProgress.Completed && expectedProgress.Story is null && presentation is not null && presentation.Sequence == sequence;
    }
    private bool AdoptEndingTransition(CampaignEndingPreparationResult result, out string? unresolvedOwner, bool requireProgress = true)
    {
        unresolvedOwner = result.UnresolvedOwner;
        if (unresolvedOwner is not null || result.State is not { } next) return false;
        if (next.EndingProgress is null && (requireProgress || next.CampaignTurnStop?.EndingRequested != false))
        { unresolvedOwner = "ending.prepared_progress"; return false; }
        GameEngine.ValidateWorldIdentity(State, next);
        if (next.Date != State.Date || next.PeriodNumber != State.PeriodNumber || next.Phase != State.Phase || next.NativeClock != State.NativeClock)
            throw new InvalidDataException("An Ending presentation advanced the campaign simulation.");
        CampaignEndingProgressValidation.Validate(next.EndingProgress);
        State = next;
        return true;
    }
}
