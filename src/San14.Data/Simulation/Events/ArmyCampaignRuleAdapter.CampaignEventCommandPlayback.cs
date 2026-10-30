using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : ICampaignEventCommandRules
{
    public CampaignEventCommandTransition PrepareCampaignEventCommand(GameState candidate, int eventId, long sequence)
    {
        try
        {
            var current = CaptureWritableTransportRouteCurrent(candidate);
            int flags = RequireCampaignEventCommandSelectionCurrent(candidate, current, eventId);
            candidate = PublishCampaignEventCommandEligibilityCurrent(candidate, current);
            var day = candidate.NativeMapSeason?.ThirdDate ?? throw EventCommandMissing("calendar");
            var date = new CampaignEventOccurrenceDate(checked((ushort)day.Year), checked((byte)day.Month), checked((byte)day.Day));
            var story = PrepareNormalTurnStoryEntryCurrent(eventId, sequence);
            //Actual main-thread audio/visual entry precedes even business-only scripts.
            var invocation = new NormalTurnStoryPlayback(story, ImmutableArray<int>.Empty, date, null)
            { Phase = NormalTurnStoryResultPhase.Entry };
            return new(candidate with { CampaignEventCommandPlayback = new(invocation, flags) }, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    public CampaignEventCommandTransition ContinueCampaignEventCommand(GameState candidate, CampaignEventCommandPlayback playback)
    {
        try
        {
            if (!ReferenceEquals(candidate.CampaignEventCommandPlayback, playback)) throw EventCommandMissing("current_owner");
            if (!playback.RequiresAutomaticContinuation(candidate) || candidate.PersonDeathContinuation?.PendingSuccession is not null ||
                candidate.AutomaticEmperorCapture?.PendingDecision is not null ||
                AutomaticEmperorCaptureLifecycle.IsPresentationPending(candidate))
                throw EventCommandMissing("pending_presentation");
            var bindings = normalTurnStoryBindings ?? throw EventCommandMissing("presentation_bindings");
            var current = CaptureWritableTransportRouteCurrent(candidate);
            var pool = current.ReadOwnedCurrentListPool() ?? throw EventCommandMissing("current_pool");
            BindNormalTurnPersonnelCurrent(candidate, current);
            var state = candidate;
            var invocation = playback.Invocation;
            RequireStorySource(invocation.Story);
            int eventId = invocation.Story.Source.EventId;
            if (invocation.Phase == NormalTurnStoryResultPhase.Entered)
            {
                _ = invocation.AudioReturn ?? throw EventCommandMissing("entry_audio_snapshot");
                var chronicle = BeginStoryChronicleCurrent(state, current, eventId);
                state = chronicle.State;
                invocation = invocation with { ChronicleProgress = chronicle.Progress,
                    StateControl = CampaignStoryStateControlProgress.Fresh };
                state = state with { CampaignEventCommandPlayback = playback with { Invocation = invocation } };
                var started = PrepareNormalTurnStoryEventCurrent(state, current, pool, eventId, bindings, invocation.Story.Sequence);
                state = started.State;
                invocation = ReadStoryStateControlPlayback(state, invocation, manual: true);
                invocation = invocation with { Story = started.Story, ExecutionCursor = started.Cursor, Outcomes = started.Outcomes,
                    Phase = NormalTurnStoryResultPhase.Presentation };
                state = StoreStoryStateControlPlayback(state, invocation, invocation, manual: true);
                if (MustYield()) return Pause();
            }
            if (invocation.ExecutionCursor is { IsCompleted: false } cursor)
            {
                var next = PrepareNormalTurnStoryEventCurrent(state, current, pool, eventId, bindings,
                    invocation.Story.Sequence, invocation.Story, cursor, invocation.Outcomes);
                state = next.State;
                invocation = ReadStoryStateControlPlayback(state, invocation, manual: true);
                invocation = invocation with { Story = next.Story, ExecutionCursor = next.Cursor, Outcomes = next.Outcomes };
                state = StoreStoryStateControlPlayback(state, invocation, invocation, manual: true);
                if (MustYield()) return Pause();
            }
            if (invocation.Phase == NormalTurnStoryResultPhase.Presentation)
            {
                var chronicle = FinishStoryChronicleCurrent(state, current, eventId,
                    invocation.ExecutionCursor, invocation.ChronicleProgress);
                state = chronicle.State;
                invocation = ReadStoryStateControlPlayback(state, invocation, manual: true) with { ChronicleProgress = chronicle.Progress };
                state = StoreStoryStateControlPlayback(state, invocation, invocation, manual: true);
                state = CampaignHistoryPlaybackControl.Exit(state);
            }
            if (invocation.Phase == NormalTurnStoryResultPhase.Presentation && (playback.Flags & 1) != 0 &&
                !PostEconomyNeed(ReadApplicationReturnToTitlePending(), "event_command_return_title"))
            {
                var result = PrepareNormalTurnStoryResultCurrent(state, current, invocation.Story.Source,
                    invocation.Outcomes ?? throw EventCommandMissing("result_outcomes"));
                if (result.Summary is null) throw EventCommandMissing(result.UnresolvedOwner ?? "result_summary");
                state = result.State;
                invocation = invocation with { Outcomes = result.Outcomes, ResultSummary = result.Summary, Phase = NormalTurnStoryResultPhase.Result };
                return Pause();
            }
            if (invocation.Phase != NormalTurnStoryResultPhase.Exited)
            {
                if ((playback.Flags & 1) == 0)
                {
                    //83C100 nests all three province passes inside its real
                    //Result block. The flags2/event900 caller skips that tail.
                    state = BypassStoryStateControlCurrent(state, invocation, manual: true);
                    invocation = ReadStoryStateControlPlayback(state, invocation, manual: true);
                }
                else
                {
                    if (invocation.Phase != NormalTurnStoryResultPhase.PostResult &&
                        (invocation.StateControl?.HasChanges == false ||
                        !PostEconomyNeed(ReadApplicationReturnToTitlePending(), "event_command_post_result_return_title")))
                    {
                        invocation = invocation with { Phase = NormalTurnStoryResultPhase.PostResult };
                        state = StoreStoryStateControlPlayback(state, invocation, invocation, manual: true);
                    }
                    var control = AdvanceStoryStateControlCurrent(state, current, pool, invocation, manual: true);
                    state = control.State;
                    invocation = ReadStoryStateControlPlayback(state, invocation, manual: true);
                    if (!control.ReadyToExit) return Pause();
                }
                _ = invocation.AudioReturn ?? throw EventCommandMissing("exit_audio_snapshot");
                invocation = invocation with { Phase = NormalTurnStoryResultPhase.Exiting };
                return Pause();
            }
            //Only return from the actual CEventPlaybackState reaches occurrence,
            //date/profile, Ack reset and the command's next catalogue refresh.
            state = state with { CampaignEventCommandPlayback = null };
            state = CompleteCampaignEventCommandCurrent(state, current, eventId, invocation.Date);
            return new(PublishNormalTurnCurrent(state, current, pool), null);

            bool MustYield() => invocation.Story.NextStep < invocation.Story.Steps.Length ||
                invocation.ExecutionCursor?.ForceRankContinuation is { IsAwaitingInput: true } || state.PersonDeathContinuation is not null ||
                state.AutomaticEmperorCapture is not null || state.StoryForceTransferContinuation is not null ||
                state.StoryForceAbsorptionTail is not null;
            CampaignEventCommandTransition Pause() => new(PublishNormalTurnCurrent(
                StoreStoryStateControlPlayback(state, invocation, invocation, manual: true), current, pool), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static InvalidDataException EventCommandMissing(string field) => new("event_command." + field);
}
