using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The normal event manager's selected batch. Completing a displayed
/// step does not mark an event as occurred or rerun its selection prefix.</summary>
public sealed record NormalTurnStoryPlayback(CampaignStoryRequest Story,
    ImmutableArray<int> RemainingEventIds, CampaignEventOccurrenceDate Date,
    CampaignCameraSnapshot? ReturnCamera)
{
    public bool IsReturningCamera { get; init; }

    internal NormalTurnStoryPlayback Copy() => this with
    {
        Story = Story.Copy()
    };

    internal void Validate()
    {
        if (Story is null || RemainingEventIds.IsDefault || RemainingEventIds.Any(id => id is < 0 or >= 1024) ||
            Date is null || Date.Year > 350 || Date.Month is < 1 or > 12 || Date.Day is < 1 or > 30)
            throw new InvalidDataException("The normal Turn story batch is incomplete.");
        Story.Validate();
        if (IsReturningCamera && (!RemainingEventIds.IsEmpty || Story.Steps.Length != 1 ||
            Story.Steps[0] is not StoryRestoreCampaignCamera))
            throw new InvalidDataException("The normal Turn camera return is incomplete.");
    }
}
