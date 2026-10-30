namespace San14.Core;

public sealed partial class GameSimulation
{
    public CampaignEventCommandPlayback? PendingCampaignEventCommand => State.CampaignEventCommandPlayback;

    public bool TryBeginCampaignEventCommand(GameState expectedOwner, int eventId,
        ICampaignEventCommandRules rules, out string? unresolvedOwner, CampaignAppRandomState? appSnapshot = null)
    {
        unresolvedOwner = "event_command.owner";
        if (HasPendingOperation || !ReferenceEquals(State, expectedOwner) || eventId is < 0 or >= 1024 ||
            State.Phase != TurnPhase.Strategy || State.NativePlayableTerminal is not null ||
            State.CampaignTurnStop is not null || State.EndingProgress is not null) return false;
        long sequence = checked(LastStorySequence + 1);
        var source = appSnapshot is null ? State : State with { CampaignAppRandom32 = appSnapshot };
        var prepared = rules.PrepareCampaignEventCommand(GameStateCopy.Copy(source), eventId, sequence);
        unresolvedOwner = prepared.UnresolvedOwner;
        if (unresolvedOwner is not null) return false;
        if (prepared.State is not { CampaignEventCommandPlayback: { } playback } next ||
            playback.Invocation.Story.Sequence != sequence || playback.Invocation.Story.Source.EventId != eventId ||
            playback.Invocation.Phase != NormalTurnStoryResultPhase.Entry)
        { unresolvedOwner = "event_command.prepared_entry"; return false; }
        CheckAuxiliaryTransition(State, next);
        playback.Validate(); State = next; LastStorySequence = sequence; return true;
    }

    public bool TryContinueCampaignEventCommand(GameState expectedOwner,
        ICampaignEventCommandRules rules, out string? unresolvedOwner)
    {
        unresolvedOwner = "event_command.continuation_owner";
        if (!EventCommandOwner(expectedOwner) || State.CampaignEventCommandPlayback is not { } command || !command.RequiresAutomaticContinuation(State) ||
            State.PersonDeathContinuation?.PendingSuccession is not null ||
            State.AutomaticEmperorCapture?.PendingDecision is not null ||
            AutomaticEmperorCaptureLifecycle.IsPresentationPending(State)) return false;
        var candidate = GameStateCopy.Copy(State);
        var result = rules.ContinueCampaignEventCommand(candidate, candidate.CampaignEventCommandPlayback!);
        unresolvedOwner = result.UnresolvedOwner;
        if (unresolvedOwner is not null) return false;
        if (result.State is not { } next) { unresolvedOwner = "event_command.continued_state"; return false; }
        GameEngine.ValidateWorldIdentity(State, next);
        if (next.Phase != State.Phase || next.Date != State.Date || next.PeriodNumber != State.PeriodNumber || next.NativeClock != State.NativeClock)
            throw new InvalidDataException("An event command changed the period boundary.");
        if (next.CampaignEventCommandPlayback is { } pending)
        {
            if (pending.Invocation.Story.Sequence != command.Invocation.Story.Sequence ||
                pending.Invocation.Story.Source != command.Invocation.Story.Source ||
                pending.Invocation.Story.ContentRevision != command.Invocation.Story.ContentRevision)
                throw new InvalidDataException("An event command replaced its active source.");
        }
        State = next; return true;
    }

    private bool EventCommandOwner(GameState expectedOwner) => _progress is null && _normalPeriod is null &&
        ReferenceEquals(State, expectedOwner) && State.Phase == TurnPhase.Strategy && State.CampaignEventCommandPlayback is not null;

    private void StoreEventCommandInvocation(NormalTurnStoryPlayback invocation) => State = State with
    { CampaignEventCommandPlayback = State.CampaignEventCommandPlayback! with { Invocation = invocation } };
}
