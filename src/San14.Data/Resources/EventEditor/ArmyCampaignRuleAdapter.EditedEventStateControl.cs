using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IEditedEventPostResultRules
{
    public EditedEventPostResultContinuationResult AdvanceEditedEventPostResult(GameState candidate,
        EditedEventPlayback playback)
    {
        try
        {
            var actual = Read(candidate);
            if (actual.Phase != EditedEventPlaybackPhase.PostResult || actual.Summary is null)
                throw StoryStateControlLifecycleMissing("edited_post_result_phase");
            var progress = actual.StateControl ?? throw StoryStateControlLifecycleMissing("edited_owner");
            var current = CaptureCurrentCampaignEventQueries(candidate);
            var pool = current.OwnedIndexes ?? throw StoryStateControlLifecycleMissing("edited_current_pool");
            _baseCapacityInputs.BindCurrent(candidate, current);
            var result = AdvanceStoryStateControlCurrent(candidate, current, pool,
                owner => Read(owner).StateControl ?? throw StoryStateControlLifecycleMissing("edited_owner"),
                (owner, control) => owner with { EditedEventPlayback = Read(owner) with { StateControl = control } });
            //Only the shared source gate can finish without completing its
            //progress: actual ReturnTitle before any province operation.
            return new(result.State, result.ReadyToExit, null)
            { BypassedForReturnTitle = result.ReadyToExit && !Read(result.State).StateControl.IsCompleted };
        }
        catch (InvalidDataException error) { return new(null, false, error.Message); }

        EditedEventPlayback Read(GameState owner)
        {
            var actual = owner.EditedEventPlayback;
            if (actual is null || actual.Source != playback.Source || actual.Story.Sequence != playback.Story.Sequence ||
                actual.Story.ContentRevision != playback.Story.ContentRevision ||
                actual.Source.ResourceUri != "san14://user/edited-events")
                throw StoryStateControlLifecycleMissing("edited_invocation_owner");
            return actual;
        }
    }
}
