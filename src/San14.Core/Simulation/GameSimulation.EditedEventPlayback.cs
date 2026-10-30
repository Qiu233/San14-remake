namespace San14.Core;

public sealed partial class GameSimulation
{
    /// <summary>Borrowed exact owner for the manual edited-event UI.</summary>
    public EditedEventPlayback? PendingEditedEventPlayback => State.EditedEventPlayback;

    public bool TryBeginEditedEventPlayback(GameState expectedOwner, int slot,
        IEditedEventPlaybackRules rules, out string? unresolvedOwner,
        CampaignAppRandomState? appRandomSnapshot = null)
    {
        unresolvedOwner = "edited_event.playback_owner";
        if (HasPendingOperation || !ReferenceEquals(State, expectedOwner) || slot is < 0 or >= 100 ||
            State.Phase != TurnPhase.Strategy || State.NativePlayableTerminal is not null ||
            State.CampaignTurnStop is not null || State.EndingProgress is not null) return false;
        long sequence = checked(LastStorySequence + 1);
        var source = appRandomSnapshot is null ? State : State with { CampaignAppRandom32 = appRandomSnapshot };
        var result = rules.PrepareEditedEventPlayback(GameStateCopy.Copy(source), slot, sequence);
        unresolvedOwner = result.UnresolvedOwner;
        if (unresolvedOwner is not null) return false;
        if (result.Story is not { } story || result.State is not { } preparedState)
        { unresolvedOwner = "edited_event.prepared_playback"; return false; }
        if (story.Sequence != sequence)
        { unresolvedOwner = "edited_event.story_sequence"; return false; }
        var playback = new EditedEventPlayback(slot, story.Source, story,
            story.Steps is { Length: 0 } ? EditedEventPlaybackPhase.Results : EditedEventPlaybackPhase.Presentation)
            { StateControl = CampaignStoryStateControlProgress.Fresh };
        EditedEventPlaybackValidation.Validate(playback);
        CheckAuxiliaryTransition(State, preparedState);
        State = preparedState with { EditedEventPlayback = playback };
        LastStorySequence = sequence;
        return true;
    }

    public bool TryPrepareEditedEventPlaybackStep(GameState expectedOwner,
        IEditedEventPlaybackRules rules, out CampaignStoryPreparationResult result)
    {
        result = new(null, null, "edited_event.story_owner");
        if (!EditedEventOwner(expectedOwner) || State.EditedEventPlayback is not
            { Phase: EditedEventPlaybackPhase.Presentation } playback) return false;
        var story = playback.Story;
        if (story.NextStep >= story.Steps.Length) return false;
        if (story.PreparedStep is { } cached)
        { result = new(cached, State.CampaignAppRandom32, null); return cached.Step == story.NextStep; }
        result = rules.PrepareEditedEventPlaybackStep(State, playback);
        if (result.UnresolvedOwner is not null || result.Prepared is not { } prepared ||
            prepared.Step != story.NextStep ||
            (story.Steps[story.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage) != (prepared.Text is not null)) return false;
        CampaignStoryPreparationValidation.ValidateText(prepared.Text);
        State = State with { CampaignAppRandom32 = result.AppRandomState ?? State.CampaignAppRandom32,
            EditedEventPlayback = playback with { Story = story with { PreparedStep = prepared } } };
        return true;
    }

    /// <summary>Consumes one actually finished step, never an entire script.
    /// Reaching the final receipt only admits the separate results phase.</summary>
    public bool TryCompleteEditedEventPlaybackStep(GameState expectedOwner,
        EditedEventPlayback expectedProgress, CampaignStoryStepReceipt receipt)
    {
        if (!EditedEventOwner(expectedOwner) || !ReferenceEquals(State.EditedEventPlayback, expectedProgress) ||
            expectedProgress.Phase != EditedEventPlaybackPhase.Presentation) return false;
        var story = expectedProgress.Story;
        if (receipt.RequestSequence != story.Sequence || receipt.Step != story.NextStep ||
            story.NextStep >= story.Steps.Length || story.Steps[story.NextStep] is StoryMessage or StoryNarration or StoryFormatOnlyMessage &&
            story.PreparedStep is not { Text: not null }) return false;
        var next = story.CompleteStep(receipt);
        State = State with { EditedEventPlayback = expectedProgress with { Story = next,
            Phase = next.NextStep == next.Steps.Length ? EditedEventPlaybackPhase.Results : EditedEventPlaybackPhase.Presentation } };
        return true;
    }

    public bool TryFinishEditedEventPlayback(GameState expectedOwner,
        EditedEventPlayback expectedProgress, IEditedEventPlaybackRules rules, out string? unresolvedOwner)
    {
        unresolvedOwner = "edited_event.results_owner";
        if (!EditedEventOwner(expectedOwner) || !ReferenceEquals(State.EditedEventPlayback, expectedProgress) ||
            expectedProgress.Phase != EditedEventPlaybackPhase.Results ||
            expectedProgress.Story.NextStep != expectedProgress.Story.Steps.Length ||
            State.AutomaticEmperorCapture?.PendingDecision is not null ||
            AutomaticEmperorCaptureLifecycle.IsPresentationPending(State)) return false;
        // Only the business tail detaches a world. Failed handlers cannot
        // mutate the visible owner or replay any accepted presentation prefix.
        var working = GameStateCopy.Copy(State);
        var result = rules.CompleteEditedEventPlayback(working, working.EditedEventPlayback!);
        unresolvedOwner = result.UnresolvedOwner;
        if (unresolvedOwner is not null) return false;
        if (result.State is not { } next) { unresolvedOwner = "edited_event.completed_results"; return false; }
        GameEngine.ValidateWorldIdentity(State, next);
        if (next.Phase != State.Phase || next.Date != State.Date ||
            next.PeriodNumber != State.PeriodNumber || next.NativeClock != State.NativeClock)
            throw new InvalidDataException("Manual edited-event results changed the campaign clock or phase.");
        var active = RequireEditedEventSuccessor(next, expectedProgress, EditedEventPlaybackPhase.Results);
        if (result.PendingBusiness)
        {
            if (next.EditedEventResultCursor is null)
                throw new InvalidDataException("An edited event business suspension lost its real result cursor.");
            State = next;
            return true;
        }
        if (next.EditedEventResultCursor is not null)
            throw new InvalidDataException("Completed edited-event results still have a business cursor.");
        if (result.Summary is { } summary)
        {
            var pending = active with { Phase = EditedEventPlaybackPhase.Summary, Summary = summary };
            EditedEventPlaybackValidation.ValidateSummary(summary);
            State = next with { EditedEventPlayback = pending };
        }
        else
        {
            //The source skips its result page only while actually returning
            //to the title. Data has resolved that branch; empty text is a page.
            if (!TryRecordEditedEventOccurrence(next, expectedProgress.Slot, out var finished))
            { unresolvedOwner = "edited_event.world_occurrence_owner"; return false; }
            State = finished! with { EditedEventPlayback = null };
        }
        return true;
    }

    /// <summary>Only a real result-page confirmation consumes this saved
    /// phase. It neither compiles the title archive nor executes results again.</summary>
    public bool TryCompleteEditedEventSummary(GameState expectedOwner,
        EditedEventPlayback expectedProgress, out string? unresolvedOwner)
    {
        unresolvedOwner = "edited_event.summary_owner";
        if (!EditedEventOwner(expectedOwner) || !ReferenceEquals(State.EditedEventPlayback, expectedProgress) ||
            expectedProgress.Phase != EditedEventPlaybackPhase.Summary || expectedProgress.Summary is null) return false;
        if (State.EditedEventResultCursor is not null)
            throw new InvalidDataException("Edited-event summary confirmation still has a business cursor.");
        State = State with { EditedEventPlayback = expectedProgress with { Phase = EditedEventPlaybackPhase.PostResult } };
        unresolvedOwner = null;
        return true;
    }

    public bool TryAdvanceEditedEventPostResult(GameState expectedOwner, EditedEventPlayback expectedProgress,
        IEditedEventPostResultRules rules, out string? unresolvedOwner)
    {
        unresolvedOwner = "edited_event.post_result_owner";
        if (!EditedEventOwner(expectedOwner) || !ReferenceEquals(State.EditedEventPlayback, expectedProgress) ||
            expectedProgress.Phase != EditedEventPlaybackPhase.PostResult || expectedProgress.Summary is null ||
            State.EditedEventResultCursor is not null || State.PersonDeathContinuation?.PendingSuccession is not null ||
            State.AutomaticEmperorCapture?.PendingDecision is not null ||
            AutomaticEmperorCaptureLifecycle.IsPresentationPending(State)) return false;
        if (CampaignStoryStateControlProgress.IsAwaitingPresentation(State))
        { unresolvedOwner = null; return true; }
        var working = GameStateCopy.Copy(State);
        var result = rules.AdvanceEditedEventPostResult(working, working.EditedEventPlayback!);
        unresolvedOwner = result.UnresolvedOwner;
        if (unresolvedOwner is not null) return false;
        if (result.State is not { } next)
        { unresolvedOwner = "edited_event.post_result_state"; return false; }
        GameEngine.ValidateWorldIdentity(State, next);
        if (next.Phase != State.Phase || next.Date != State.Date ||
            next.PeriodNumber != State.PeriodNumber || next.NativeClock != State.NativeClock)
            throw new InvalidDataException("Edited-event post-Result work changed the campaign clock or phase.");
        var active = RequireEditedEventSuccessor(next, expectedProgress, EditedEventPlaybackPhase.PostResult);
        if (next.EditedEventResultCursor is not null)
            throw new InvalidDataException("Edited-event post-Result continuation returned a business cursor.");
        if (result.BypassedForReturnTitle && (!result.IsCompleted || active.StateControl != expectedProgress.StateControl ||
            active.StateControl.NextOperation != 0 || active.StateControl.PendingNotice is not null ||
            active.StateControl.AwaitingPresentationBatch is not null))
            throw new InvalidDataException("Edited-event return-to-title bypass does not belong to the untouched tail.");
        if (!result.IsCompleted)
        { State = next; return true; }
        if (!result.BypassedForReturnTitle && !active.StateControl.IsCompleted)
            throw new InvalidDataException("Edited-event province-control work has not actually completed.");
        if (!TryRecordEditedEventOccurrence(next, active.Slot, out var completed))
        { unresolvedOwner = "edited_event.world_occurrence_owner"; return false; }
        State = completed! with { EditedEventPlayback = null };
        return true;
    }

    private static EditedEventPlayback RequireEditedEventSuccessor(GameState state,
        EditedEventPlayback previous, EditedEventPlaybackPhase phase)
    {
        if (state.EditedEventPlayback is not { } active || active.Phase != phase ||
            active.Slot != previous.Slot || active.Source != previous.Source ||
            active.Story.Sequence != previous.Story.Sequence || active.Story.Source != previous.Story.Source ||
            active.Story.ContentRevision != previous.Story.ContentRevision)
            throw new InvalidDataException("Edited-event continuation replaced its actual invocation.");
        EditedEventPlaybackValidation.Validate(active);
        return active;
    }

    private static bool TryRecordEditedEventOccurrence(GameState state, int slot, out GameState? completed)
    {
        completed = null;
        if (slot is < 0 or >= 100 || state.CampaignEvents is not { Occurred.Length: 1024 } history) return false;
        var occurred = history.Occurred.ToArray();
        occurred[800 + slot] = true;
        completed = state with { CampaignEvents = history with { Occurred = occurred } };
        return true;
    }

    private bool EditedEventOwner(GameState expectedOwner) =>
        _normalPeriod is null && _progress is null && ReferenceEquals(State, expectedOwner) &&
        State.Phase == TurnPhase.Strategy && State.EditedEventPlayback is not null;
}
