namespace San14.Core;

public sealed partial class GameSimulation
{
    public bool TryPrepareLegacyStoryStep(GameState expectedOwner, ICampaignStoryPreparationRules rules,
        out CampaignStoryPreparationResult result)
    {
        result = new(null, null, "legacy.story.presentation_owner");
        if (_progress?.Story is not { ContinueAfterPresentation: false } pending ||
            !ReferenceEquals(pending.Candidate, expectedOwner) ||
            pending.Story.NextStep >= pending.Story.Steps.Length) return false;
        var story = pending.Story;
        if (story.PreparedStep is { } cached)
        {
            result = new(cached, pending.Candidate.CampaignAppRandom32, null);
            return cached.Step == story.NextStep;
        }
        result = rules.PrepareNormalTurnStoryStep(pending.Candidate, story);
        if (result.UnresolvedOwner is not null || result.Prepared is not { } prepared ||
            prepared.Step != story.NextStep) return false;
        if (story.Steps[story.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage && prepared.Text is null)
        {
            result = result with { UnresolvedOwner = "legacy.story.prepared_text" };
            return false;
        }
        _progress = new(pending with
        {
            Candidate = pending.Candidate with { CampaignAppRandom32 = result.AppRandomState ?? pending.Candidate.CampaignAppRandom32 },
            Story = story with { PreparedStep = prepared }
        }, null);
        return true;
    }

    public bool TryCompleteLegacyStoryStep(GameState expectedOwner, CampaignStoryStepReceipt receipt)
    {
        if (_progress?.Story is not { ContinueAfterPresentation: false } pending ||
            !ReferenceEquals(pending.Candidate, expectedOwner) || receipt.RequestSequence != pending.Story.Sequence ||
            receipt.Step != pending.Story.NextStep || pending.Story.NextStep >= pending.Story.Steps.Length) return false;
        var story = pending.Story;
        if (story.Steps[story.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage &&
            story.PreparedStep is not { Text: not null }) return false;
        var completed = story.CompleteStep(receipt);
        _progress = new(pending with { Story = completed,
            ContinueAfterPresentation = completed.NextStep == completed.Steps.Length }, null);
        return true;
    }
}
