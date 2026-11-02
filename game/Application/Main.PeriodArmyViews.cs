using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    // Checkpoint owns the stopped worker's candidate for this synchronous
    // initialization. No modal, extra GPU frame or later world override is
    // needed before returning the canonical receipt to that same operation.
    private GameState PresentPeriodArmyViews(CampaignPeriodPlayback playback, MapSceneView? map, GameState candidate)
    {
        if (ArmyViewPresentationPlaybackRules.Next(candidate) is null) return candidate;
        if (!ReferenceEquals(_periodPlayback, playback) || !ReferenceEquals(playback.Candidate, candidate) ||
            map is null || !ReferenceEquals(_residentMap, map))
            throw new InvalidOperationException("period.army-view.borrow_owner");
        GameState? traitSource = null;
        while (playback.Candidate is { } owner && ArmyViewPresentationPlaybackRules.Next(owner) is { } head)
        {
            bool accepted;
            string? missing;
            if (head.Request.ArmyDeparture is { } departure)
            {
                traitSource = null;
                accepted = map.TryPresentArmyDeparturePresentation(owner, departure, out missing, playback);
            }
            else
            {
                traitSource ??= owner;
                accepted = map.TryRefreshArmyTraitView(traitSource, head.Request.ArmyTraitViewRefresh!.ArmyId,
                    out missing, playback);
            }
            if (!accepted) throw new InvalidDataException(missing ?? "period.army-view.source_consumer");
            int armyId = head.Request.ArmyDeparture?.ArmyAtSource.Id ?? head.Request.ArmyTraitViewRefresh!.ArmyId;
            //Only the actual initialized label owner supplies this fact. A
            //captured departure of an already retired/reused receiver cannot
            //make the current worker's different receiver view available.
            if (map.CaptureArmyTraitSpeechView(armyId) is { } speechView)
                playback.PublishInitializedArmySpeechView(owner, speechView);
            if (!playback.TryCompleteArmyViewPresentation(owner, head))
                throw new InvalidOperationException("period.army-view.completion_owner");
        }
        return playback.Candidate ?? throw new InvalidOperationException("period.army-view.borrow_released");
    }
}
