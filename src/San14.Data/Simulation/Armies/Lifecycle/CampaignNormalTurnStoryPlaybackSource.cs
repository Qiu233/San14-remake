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
        while (true)
        {
            if (CampaignStoryStateControlProgress.IsAwaitingPresentation(state)) return state;
            if (state.PersonDeathContinuation is not null || state.AutomaticEmperorCapture?.PendingDecision is not null ||
                AutomaticEmperorCaptureLifecycle.IsPresentationPending(state)) return state;
            var bindings = normalTurnStoryBindings;
            ImmutableArray<int> remaining;
            CampaignEventOccurrenceDate date;
            CampaignCameraSnapshot? camera;
            long sequence;
            if (state.NativeNormalTurnContinuation?.StoryPlayback is { } pending)
            {
                RequireStorySource(pending.Story);
                if (pending.ExecutionCursor?.ForceRankContinuation is { IsAwaitingInput: true }) return state;
                if (pending.Phase is NormalTurnStoryResultPhase.Entry or NormalTurnStoryResultPhase.Result or NormalTurnStoryResultPhase.Exiting)
                    return state;
                if (pending.Phase == NormalTurnStoryResultPhase.Entered)
                {
                    _ = pending.AudioReturn ?? throw PostEconomyMissing("event_audio_entry_snapshot");
                    var chronicle = BeginStoryChronicleCurrent(state, current, pending.Story.Source.EventId);
                    state = chronicle.State;
                    pending = pending with { ChronicleProgress = chronicle.Progress,
                        StateControl = CampaignStoryStateControlProgress.Fresh };
                    state = state with { NativeNormalTurnContinuation = state.NativeNormalTurnContinuation! with { StoryPlayback = pending } };
                    var started = PrepareNormalTurnStoryEventCurrent(state, current, pool, pending.Story.Source.EventId,
                        bindings ?? throw PostEconomyMissing("event_playback_bindings"), pending.Story.Sequence);
                    state = started.State;
                    pending = ReadStoryStateControlPlayback(state, pending, manual: false);
                    pending = pending with { Story = started.Story, ExecutionCursor = started.Cursor, Outcomes = started.Outcomes,
                        Phase = NormalTurnStoryResultPhase.Presentation };
                    state = state with { NativeNormalTurnContinuation = state.NativeNormalTurnContinuation! with { StoryPlayback = pending } };
                    if (state.AutomaticEmperorCapture is not null || state.StoryForceTransferContinuation is not null || state.StoryForceAbsorptionTail is not null || state.PersonDeathContinuation is not null || pending.ExecutionCursor?.ForceRankContinuation is { IsAwaitingInput: true }) return state;
                }
                if (pending.Story.NextStep < pending.Story.Steps.Length) return state;
                if (pending.ExecutionCursor is { IsCompleted: false } cursor)
                {
                    var next = PrepareNormalTurnStoryEventCurrent(state, current, pool, pending.Story.Source.EventId,
                        bindings ?? throw PostEconomyMissing("event_playback_bindings"), pending.Story.Sequence, pending.Story, cursor, pending.Outcomes);
                    state = next.State;
                    pending = ReadStoryStateControlPlayback(state, pending, manual: false);
                    pending = pending with { Story = next.Story, ExecutionCursor = next.Cursor, Outcomes = next.Outcomes };
                    state = state with { NativeNormalTurnContinuation = state.NativeNormalTurnContinuation! with { StoryPlayback = pending } };
                    if (pending.Story.NextStep < pending.Story.Steps.Length || state.AutomaticEmperorCapture is not null || state.StoryForceTransferContinuation is not null || state.StoryForceAbsorptionTail is not null || state.PersonDeathContinuation is not null ||
                        pending.ExecutionCursor?.ForceRankContinuation is { IsAwaitingInput: true }) return state;
                    //A real business-only tail proceeds directly into its manager;
                    //there is no invented wait, presentation receipt or extra turn.
                }
                if (pending.Phase == NormalTurnStoryResultPhase.Presentation)
                {
                    if (!pending.IsReturningCamera)
                    {
                        var chronicle = FinishStoryChronicleCurrent(state, current, pending.Story.Source.EventId,
                            pending.ExecutionCursor, pending.ChronicleProgress);
                        state = chronicle.State;
                        pending = ReadStoryStateControlPlayback(state, pending, manual: false) with { ChronicleProgress = chronicle.Progress };
                        state = StoreStoryStateControlPlayback(state, pending, pending, manual: false);
                    }
                    state = CampaignHistoryPlaybackControl.Exit(state);
                }
                if (pending.IsReturningCamera)
                    return state with { NativeNormalTurnContinuation = null };
                if (pending.Phase == NormalTurnStoryResultPhase.Presentation &&
                    !PostEconomyNeed(ReadApplicationReturnToTitlePending(), "event_result_return_to_title"))
                {
                    //83C100 opens ResultState after this same invocation. No
                    //second execution stream or reconstructed world delta exists.
                    var result = PrepareNormalTurnStoryResultCurrent(state, current, pending.Story.Source,
                        pending.Outcomes ?? throw PostEconomyMissing("event_result_legacy_outcomes"));
                    if (result.Summary is null)
                        throw PostEconomyMissing(result.UnresolvedOwner ?? "event_result_preparation");
                    state = result.State;
                    pending = pending with { Outcomes = result.Outcomes, ResultSummary = result.Summary,
                        Phase = NormalTurnStoryResultPhase.Result };
                    return state with { NativeNormalTurnContinuation = state.NativeNormalTurnContinuation! with { StoryPlayback = pending } };
                }
                if (pending.Phase != NormalTurnStoryResultPhase.Exited)
                {
                    if (pending.Phase != NormalTurnStoryResultPhase.PostResult &&
                        (pending.StateControl?.HasChanges == false ||
                        !PostEconomyNeed(ReadApplicationReturnToTitlePending(), "event_post_result_return_title")))
                    {
                        pending = pending with { Phase = NormalTurnStoryResultPhase.PostResult };
                        state = StoreStoryStateControlPlayback(state, pending, pending, manual: false);
                    }
                    var control = AdvanceStoryStateControlCurrent(state, current, pool, pending, manual: false);
                    state = control.State;
                    pending = ReadStoryStateControlPlayback(state, pending, manual: false);
                    if (!control.ReadyToExit) return state;
                    _ = pending.AudioReturn ?? throw PostEconomyMissing("event_audio_legacy_return_snapshot");
                    //8345D0 retires the visual owner; it is not occurrence.
                    //The main thread then completes audio/attachments/wait/fade.
                    //3D8850 records occurrence only after that real return.
                    return state with { NativeNormalTurnContinuation = state.NativeNormalTurnContinuation! with
                    { StoryPlayback = pending with { Phase = NormalTurnStoryResultPhase.Exiting } } };
                }
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
                if (pending.Story.Source.EventId == 900)
                {
                    //83BE74 requests the special Ending only after the actual
                    //event playback returned. It is not an ordinary conquest win.
                    var ending = PrepareCampaignEnding(state, fromEvent900: true);
                    state = ending.State ?? throw PostEconomyMissing(ending.UnresolvedOwner ?? "event900_ending");
                    if (state.EndingProgress is { FromEvent900: true })
                        return state with { NativeNormalTurnContinuation = null,
                            CampaignTurnStop = new(CampaignTurnStopReason.EndingFromEvent900, null, true) };
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
            var story = PrepareNormalTurnStoryEntryCurrent(eventId, sequence);
            var saved = state.NativeNormalTurnContinuation ?? new(48, restoreApp, ImmutableArray<int>.Empty, null, grouping);
            //The audio owner captures/enters on the main thread before any
            //business, including events whose program has no visible messages.
            return state with { NativeNormalTurnContinuation = saved with
            {
                NextAction = 48,
                Grouping = grouping,
                StoryPlayback = new(story, remaining.RemoveAt(0), date, camera) { Phase = NormalTurnStoryResultPhase.Entry }
            } };
        }

    }
}
