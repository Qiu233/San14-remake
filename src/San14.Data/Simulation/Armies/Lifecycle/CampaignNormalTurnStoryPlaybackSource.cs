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
        long sequence;
        if (state.NativeNormalTurnContinuation?.StoryPlayback is { } pending)
        {
            RequireStorySource(pending.Story);
            if (pending.Story.NextStep < pending.Story.Steps.Length) return state;
            if (pending.IsReturningCamera)
                return state with { NativeNormalTurnContinuation = null };
            state = CompleteNormalTurnStoryEventCurrent(state, current, pending.Story.Source.EventId, pending.Date);
            BindApplicationServiceCurrent(state, current);
            //Occurrence/date and the independent local event history are
            //distinct. Only the actually consumed event reaches this tail.
            if (!PostEconomyNeed(ReadApplicationInformationRestriction(state), "event_selected_user") &&
                PostEconomyNeed(current.ReadTransferServiceEligibility(), "event_profile_eligibility"))
            {
                state = PrepareApplicationServiceCurrent(state, current, profile =>
                {
                    var application = ReadApplicationGameplay() ?? throw PostEconomyMissing("event_application_context");
                    var prepared = CampaignEventService.PrepareEventCompletion(new(current, current,
                        id => ReadCampaignServiceRecordKind(_campaignEvents.Value.Document, id),
                        application.UserSelectionServicePresent, state.CampaignEventWorld?.CurrentUserIndex),
                        profile, pending.Story.Source.EventId);
                    return prepared.Service ?? throw new InvalidDataException(prepared.UnresolvedInput
                        ?? "normal.turn.event_profile_preparation");
                });
            }
            remaining = pending.RemainingEventIds;
            date = pending.Date;
            camera = pending.ReturnCamera;
            sequence = checked(pending.Story.Sequence + 1);
            if (remaining.IsEmpty && camera is not null)
            {
                var restore = pending.Story with { Sequence = sequence,
                    Steps = [new StoryRestoreCampaignCamera(camera)], NextStep = 0, PreparedStep = null,
                    //3D8850 returns from each playback owner's573900 wait
                    //before its batch camera restoration.83B4F0 has already
                    //closed that event's background/talk; no old scene or log
                    //is part of the one-millisecond restoration request.
                    Scene = CampaignStoryScene.Empty, TextLog = ImmutableArray<CampaignStoryPreparedText>.Empty };
                return state with { NativeNormalTurnContinuation = state.NativeNormalTurnContinuation! with
                {
                    Grouping = grouping,
                    StoryPlayback = new(restore, remaining, date, camera) { IsReturningCamera = true }
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
            StoryPlayback = new(story, remaining.RemoveAt(0), date, camera)
        } };
    }
}
