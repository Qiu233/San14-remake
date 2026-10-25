namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TryPrepareNormalTurnStoryStep(GameState expectedOwner, ICampaignStoryPreparationRules rules,
        out CampaignStoryPreparationResult result)
    {
        result = new(null, null, "story.presentation_owner");
        if (_progress is not null || _normalPeriod is not { } pending ||
            !ReferenceEquals(pending.Candidate, expectedOwner) ||
            pending.Candidate.NativeNormalTurnContinuation is not { NextAction: 48, StoryPlayback: { } playback } turn ||
            playback.Story.NextStep >= playback.Story.Steps.Length)
            return false;
        var story = playback.Story;
        if (story.PreparedStep is { } cached)
        {
            result = new(cached, pending.Candidate.CampaignAppRandom32, null);
            return cached.Step == story.NextStep;
        }
        result = rules.PrepareNormalTurnStoryStep(pending.Candidate, story);
        if (result.UnresolvedOwner is not null || result.Prepared is not { } prepared || prepared.Step != story.NextStep)
            return false;
        if ((story.Steps[story.NextStep] is StoryMessage or StoryNarration) && prepared.Text is null)
        {
            result = result with { UnresolvedOwner = "story.prepared_text" };
            return false;
        }
        _normalPeriod = pending with { Candidate = pending.Candidate with
        {
            CampaignAppRandom32 = result.AppRandomState ?? pending.Candidate.CampaignAppRandom32,
            NativeNormalTurnContinuation = turn with
            { StoryPlayback = playback with { Story = story with { PreparedStep = prepared } } }
        } };
        return true;
    }

    public bool TryCompleteNormalTurnStoryStep(GameState expectedOwner, long sequence, int step) =>
        TryCompleteNormalTurnStoryStep(expectedOwner, new CampaignStoryStepReceipt(sequence, step));

    /// <summary>Receives one genuinely consumed story step from the exact
    /// normal-period presentation owner. Event completion remains with its manager.</summary>
    public bool TryCompleteNormalTurnStoryStep(GameState expectedOwner, CampaignStoryStepReceipt receipt)
    {
        if (_progress is not null || _normalPeriod is not { } pending ||
            !ReferenceEquals(pending.Candidate, expectedOwner) ||
            pending.Candidate.NativeNormalTurnContinuation is not { NextAction: 48, StoryPlayback: { } playback } turn ||
            playback.Story is not { } story || receipt.RequestSequence != story.Sequence ||
            receipt.Step != story.NextStep || story.NextStep >= story.Steps.Length)
            return false;
        if ((story.Steps[story.NextStep] is StoryMessage or StoryNarration) &&
            story.PreparedStep is not { Text: not null }) return false;
        _normalPeriod = pending with { Candidate = pending.Candidate with
        {
            NativeNormalTurnContinuation = turn with { StoryPlayback = playback with { Story = story.CompleteStep() } }
        } };
        return true;
    }
}
