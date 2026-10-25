using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private CampaignStoryBindings? normalTurnStoryBindings;

    /// <summary>The host supplies its actual camera, audio and application
    /// inputs before the private worker starts. No presentation is acknowledged.</summary>
    public void ConfigureNormalTurnStoryBindings(CampaignStoryBindings? bindings)
        => normalTurnStoryBindings = bindings;

    private GameState ApplyNormalTurnStoryPlaybackCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, uint restoreApp, CampaignGroupingCheckpoint grouping)
    {
        var bindings = normalTurnStoryBindings;
        ImmutableArray<int> remaining;
        CampaignEventOccurrenceDate date;
        CampaignCameraSnapshot? camera;
        CampaignServiceChanges changes;
        long sequence;
        if (state.NativeNormalTurnContinuation?.StoryPlayback is { } pending)
        {
            RequireStorySource(pending.Story);
            if (pending.Story.NextStep < pending.Story.Steps.Length) return state;
            if (pending.IsReturningCamera)
                return state with { NativeNormalTurnContinuation = null };
            state = CompleteNormalTurnStoryEventCurrent(state, current, pending.Story.Source.EventId, pending.Date);
            //The original3F0750 also publishes an independent local-profile
            //event bit when its eligibility gate is true. A world occurrence
            //is not a fabricated acknowledgement of that separate service.
            if (!PostEconomyNeed(ReadApplicationInformationRestriction(state), "event_selected_user") &&
                PostEconomyNeed(current.ReadTransferServiceEligibility(), "event_profile_eligibility"))
                throw PostEconomyMissing("event_local_profile_publication");
            remaining = pending.RemainingEventIds;
            date = pending.Date;
            camera = pending.ReturnCamera;
            changes = pending.ServiceChanges;
            sequence = checked(pending.Story.Sequence + 1);
            if (remaining.IsEmpty && camera is not null)
            {
                var restore = pending.Story with { Sequence = sequence,
                    Steps = [new StoryRestoreCampaignCamera(camera)], NextStep = 0, PreparedStep = null };
                return state with { NativeNormalTurnContinuation = state.NativeNormalTurnContinuation! with
                {
                    Grouping = grouping,
                    StoryPlayback = new(restore, remaining, date, camera)
                        { ServiceChanges = changes, IsReturningCamera = true }
                } };
            }
        }
        else
        {
            var prepared = PrepareNormalTurnStoryEventsCurrent(state, current, pool, bindings);
            state = prepared.State;
            if (prepared.EventIds.Length == 0) return state;
            remaining = prepared.EventIds.ToImmutableArray();
            date = prepared.Date;
            //3D8850 preserves the campaign camera around its entire selected
            //batch. An absent host input cannot manufacture a focus snapshot.
            camera = (bindings ?? throw PostEconomyMissing("event_playback_bindings")).CaptureCamera();
            changes = CampaignServiceChanges.Empty;
            sequence = 1;
        }

        if (remaining.IsEmpty)
            return state with { NativeNormalTurnContinuation = null };
        if (bindings is null) throw PostEconomyMissing("event_playback_bindings");
        int eventId = remaining[0];
        var story = PrepareNormalTurnStoryEventCurrent(state, current, pool, eventId, bindings, sequence);
        var saved = state.NativeNormalTurnContinuation ?? new(48, restoreApp, ImmutableArray<int>.Empty, null, grouping);
        return state with { NativeNormalTurnContinuation = saved with
        {
            NextAction = 48,
            Grouping = grouping,
            StoryPlayback = new(story, remaining.RemoveAt(0), date, camera) { ServiceChanges = changes }
        } };
    }
}
