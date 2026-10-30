namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TryPrepareCampaignEventCommandStep(GameState expectedOwner, ICampaignStoryPreparationRules rules,
        out CampaignStoryPreparationResult result)
    {
        result = new(null, null, "story.presentation_owner");
        if (!EventCommandOwner(expectedOwner) || State.CampaignEventCommandPlayback?.Invocation is not { } playback ||
            playback.Story.NextStep >= playback.Story.Steps.Length)
            return false;
        var story = playback.Story;
        if (story.PreparedStep is { } cached)
        {
            result = new(cached, State.CampaignAppRandom32, null);
            return cached.Step == story.NextStep;
        }
        result = rules.PrepareNormalTurnStoryStep(State, story);
        if (result.UnresolvedOwner is not null || result.Prepared is not { } prepared || prepared.Step != story.NextStep)
            return false;
        if ((story.Steps[story.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage) && prepared.Text is null)
        {
            result = result with { UnresolvedOwner = "story.prepared_text" };
            return false;
        }
        State = State with { CampaignAppRandom32 = result.AppRandomState ?? State.CampaignAppRandom32,
            CampaignEventCommandPlayback = State.CampaignEventCommandPlayback! with
            { Invocation = playback with { Story = story with { PreparedStep = prepared } } } };
        return true;
    }

    public bool TryCompleteCampaignEventCommandStep(GameState expectedOwner, long sequence, int step) =>
        TryCompleteCampaignEventCommandStep(expectedOwner, new CampaignStoryStepReceipt(sequence, step));

    /// <summary>Receives one genuinely consumed story step from the exact
    /// ordinary-command presentation owner. Event completion remains with its manager.</summary>
    public bool TryCompleteCampaignEventCommandStep(GameState expectedOwner, CampaignStoryStepReceipt receipt)
    {
        if (!EventCommandOwner(expectedOwner) || State.CampaignEventCommandPlayback?.Invocation is not { } playback ||
            playback.Story is not { } story || receipt.RequestSequence != story.Sequence ||
            receipt.Step != story.NextStep || story.NextStep >= story.Steps.Length)
            return false;
        if ((story.Steps[story.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage) &&
            story.PreparedStep is not { Text: not null }) return false;
        StoreEventCommandInvocation(playback with { Story = story.CompleteStep(receipt) });
        return true;
    }
}
