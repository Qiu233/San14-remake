using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private bool HasCreatedEventResults => _createdEventWorkspace is { } owner &&
        IsCreatedEventPlaybackCurrent(owner) &&
        (owner.Session.PendingEditedEventPlayback is { Phase: EditedEventPlaybackPhase.Results or EditedEventPlaybackPhase.PostResult } ||
            owner.Session.PendingEditedEventPlayback is { Phase: EditedEventPlaybackPhase.Summary } && owner.Summary is null);

    private bool TryResumeCreatedEventResults()
    {
        if (!HasCreatedEventResults || _createdEventWorkspace is not { } owner) return false;
        if (_advancingPeriod || HasEnvironmentSettings || HasCampaignSavePage || HasMapSystemMenu) return true;
        if (!EnsureCreatedEventMedia(owner)) return true;
        if (HasRulerDecision)
        {
            if (IsRulerDecisionCurrent) return true;
            CloseRulerDecision();
        }
        // The actual prefix/outbox has priority over the decision. Receipts
        // change the world owner, and the next reached business slice resumes
        // through this same edited-result cursor, never a ten-day advance.
        if (TryOpenPersonnelPresentation()) return true;
        var world = owner.Session.World;
        if (PersonnelPresentationQueue.Next(world) is not null) return true;
        if (owner.Session.PlayerForceId is int player &&
            TryOpenRulerDecision(owner.Session, world, owner.Composition, owner.Map, player)) return true;
        if (ReferenceEquals(owner.ResultAttemptWorld, world)) return true;
        try
        {
            if (owner.Session.PendingEditedEventPlayback is { Phase: EditedEventPlaybackPhase.PostResult } progress)
            {
                owner.ResultAttemptWorld = world;
                if (!owner.Session.TryAdvanceEditedEventPostResult(world, progress, owner.Rules!, out string? missing))
                    throw new InvalidDataException(missing ?? "edited_event.post_result.advance");
                if (owner.Session.PendingEditedEventPlayback is null) CompleteCreatedEventWorkspace(owner);
                else
                {
                    owner.Composition.PublishApplicationServices(owner.Session.World);
                    owner.Map.RefreshPlayableWorld(owner.Session.World);
                    RefreshPlayControls(); TryOpenPersonnelPresentation();
                }
            }
            else if (owner.Session.PendingEditedEventPlayback is { Phase: EditedEventPlaybackPhase.Summary } summary)
                OpenCreatedEventSummary(owner, summary);
            else FinishCreatedEventPlayback(owner);
        }
        catch (Exception error) { FailCreatedEventPlayback(owner, error); }
        return true;
    }
}
