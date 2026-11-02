namespace San14.Core;

public sealed partial class GameSimulation
{
    private CampaignPendingOperation? _progress;
    public bool HasPendingOperation => _progress is not null || _normalPeriod is not null || State.EditedEventPlayback is not null || State.CampaignEventCommandPlayback is not null || State.AutomaticEmperorCapture is not null;
    public CampaignProgressOperation? PendingProgress => _progress?.Story?.Copy();
    public GameState? PendingProgressCandidate => _progress?.Story?.Candidate;
    public CampaignArmyProgressOperation? PendingArmyProgress => _progress?.ArmyProgress?.Copy();
    public GameState? PendingArmyProgressCandidate => _progress?.ArmyProgress?.Candidate;
    public CampaignStoryRequest? PendingLegacyStory => _progress?.Story is { ContinueAfterPresentation: false } story &&
        story.Story.NextStep < story.Story.Steps.Length ? story.Story : null;
    public bool CanRetryLegacyProgress => _progress?.ArmyProgress is { } armyProgress ? armyProgress.CanContinue :
        _progress?.ManualTactic is { } manual
        ? (manual.ContinueAfterPresentation || manual.RequiresAutomaticManualTacticContinuation) &&
            manual.PendingCapturedRulerTreatment is null && manual.PendingRulerSuccession is null &&
            !manual.IsAwaitingArmyReturnCaptiveReply
        : _progress?.Story?.ContinueAfterPresentation == true;
    public ManualTacticProgressOperation? PendingManualTactic => _progress?.ManualTactic?.Copy();
    /// <summary>Borrowed read-only candidate for exact-owner presentation transactions.</summary>
    public GameState? PendingManualTacticCandidate => _progress?.ManualTactic?.Candidate;
    public long LastStorySequence { get; private set; }
    private void RequireNoProgress()
    { if (HasPendingOperation) throw new InvalidOperationException("The campaign is awaiting its current continuation."); }
    public void RestoreProgress(CampaignProgressOperation? progress, long lastSequence, ManualTacticProgressOperation? manualTactic = null)
        => RestoreProgress(progress, lastSequence, manualTactic, null);

    public void RestoreProgress(CampaignProgressOperation? progress, long lastSequence,
        ManualTacticProgressOperation? manualTactic, CampaignArmyProgressOperation? armyProgress)
    {
        // A manual editor Story is already owned by the restored world. The
        // Session may restore its sequence, but cannot attach a second owner.
        if (_progress is not null || _normalPeriod is not null ||
            (State.EditedEventPlayback is not null || State.CampaignEventCommandPlayback is not null) && (progress is not null || manualTactic is not null || armyProgress is not null))
            throw new InvalidOperationException("The campaign already has a continuation owner.");
        if ((progress is null ? 0 : 1) + (manualTactic is null ? 0 : 1) + (armyProgress is null ? 0 : 1) > 1)
            throw new InvalidDataException("Saved campaign continuation is not exclusive.");
        if (lastSequence < 0 || progress is not null && progress.LastStorySequence != lastSequence ||
            armyProgress is not null && armyProgress.LastStorySequence != lastSequence)
            throw new InvalidDataException("Saved story sequence is inconsistent.");
        // Old session metadata can carry diagnostic combat snapshots. They
        // are deliberately retired, not reconstructed or validated as gameplay.
        if (progress is not null) progress = progress with { Captures = [] };
        if (manualTactic is not null) manualTactic = manualTactic with { Captures = [] };
        if (armyProgress is not null) armyProgress = armyProgress with { Captures = [] };
        progress?.Validate(State);
        if (progress is not null)
        {
            StateValidator.Validate(progress.Candidate, progress.Context);
        }
        if (manualTactic is not null)
        {
            if (progress is not null || manualTactic.LastStorySequence != lastSequence) throw new InvalidDataException("Saved campaign operation is not exclusive.");
            manualTactic.Validate(State); StateValidator.Validate(manualTactic.Candidate, manualTactic.Context);
        }
        if (armyProgress is not null)
        {
            armyProgress.Validate(State);
            StateValidator.Validate(armyProgress.Candidate, armyProgress.Context);
        }
        _progress = progress is not null ? new(progress.Copy(), null) : manualTactic is not null
            ? new(null, manualTactic.Copy()) : armyProgress is not null ? new(null, null, armyProgress.Copy()) : null;
        LastStorySequence = Math.Max(lastSequence, Math.Max(
            State.EditedEventPlayback?.Story.Sequence ?? 0,
            State.CampaignEventCommandPlayback?.Invocation.Story.Sequence ?? 0));
    }
    public CampaignProgressTransition BeginProgress(ICampaignProgressRules rules, CampaignProgressRequest request,
        ICommandPlanner? planner = null)
    {
        if (State.ArmyReturnCaptiveContinuation is not null)
            throw new InvalidOperationException("The army return is awaiting captive treatment.");
        RequireNoProgress();
        if (State.Phase != TurnPhase.Strategy || State.NativePlayableTerminal is not null || State.CampaignTurnStop is not null)
            throw new InvalidOperationException("Native campaign progress requires an active strategy world.");
        request.ValidateNormalPeriod(State);
        var planning = new CommandPlanningContext(GameStateCopy.Copy(State));
        planner?.Plan(planning);
        CheckAuxiliaryTransition(State, planning.Current);
        return Admit(rules, rules.Begin(planning.Current, request, checked(LastStorySequence + 1)), request);
    }
    public CampaignProgressTransition ResumeStory(ICampaignProgressRules rules, CampaignStoryStepReceipt receipt)
    {
        var pending = _progress?.Story ?? throw new InvalidOperationException("There is no pending campaign story.");
        if (pending.ContinueAfterPresentation || receipt.RequestSequence != pending.Story.Sequence || receipt.Step != pending.Story.NextStep)
            throw new InvalidDataException("Story acknowledgement is stale or does not name the waiting step.");
        return Admit(rules, rules.Resume(pending.Copy(), receipt), pending.Request);
    }
    public CampaignProgressTransition RetryProgress(ICampaignProgressRules rules)
    {
        if (_progress?.ArmyProgress is { } armyProgress)
        {
            if (!armyProgress.CanContinue || rules is not ICampaignArmyProgressRules source)
                throw new InvalidOperationException("Movement continuation needs its actual reply or rules.");
            return Admit(rules, source.RetryArmy(armyProgress.Copy()), armyProgress.Request);
        }
        if (_progress?.ManualTactic is { } manual)
        {
            if ((!manual.ContinueAfterPresentation && !manual.RequiresAutomaticManualTacticContinuation) ||
                manual.PendingCapturedRulerTreatment is not null || manual.PendingRulerSuccession is not null ||
                manual.IsAwaitingArmyReturnCaptiveReply ||
                rules is not ICampaignManualTacticProgressRules source)
                throw new InvalidOperationException("Manual tactic continuation needs its actual reply or rules.");
            return Admit(rules, source.RetryManualTactic(manual.Copy()), manual.Request);
        }
        var pending = _progress?.Story ?? throw new InvalidOperationException("There is no pending campaign continuation.");
        if (!pending.ContinueAfterPresentation) throw new InvalidOperationException("The current story step still needs presentation.");
        return Admit(rules, rules.Retry(pending.Copy()), pending.Request);
    }
    public CampaignProgressTransition ResumeManualTactic(ICampaignManualTacticProgressRules rules, TacticPresentationReceipt receipt)
    {
        var pending = _progress?.ManualTactic ?? throw new InvalidOperationException("There is no pending manual tactic.");
        if (receipt.Kind == TacticPresentationKind.OrdinaryEffects)
            throw new InvalidOperationException("Ordinary tactics require their actual start, result and retirement transactions.");
        if (pending.ContinueAfterPresentation || pending.Presentation is not { } current ||
            current.Sequence != receipt.Sequence || current.Kind != receipt.Kind)
            throw new InvalidDataException("Manual tactic receipt is stale or does not name the waiting head.");
        return Admit(rules, rules.ResumeManualTactic(pending.Copy(), receipt), pending.Request);
    }
    public void AbortProgress() => _progress = null;
    private CampaignProgressTransition Admit(ICampaignProgressRules rules, CampaignProgressTransition result, CampaignProgressRequest request)
    {
        request.ValidateNormalPeriod(State);
        if (!Enum.IsDefined(result.Outcome) ||
            (result.Pending is null ? 0 : 1) + (result.ManualTactic is null ? 0 : 1) + (result.ArmyProgress is null ? 0 : 1) > 1)
            throw new InvalidDataException("Unknown or nonexclusive progress result.");
        if (result.ArmyProgress is { } armyProgress)
        {
            if (result.State is not null || result.Outcome is not (CampaignProgressOutcome.AwaitingArmyProgress or CampaignProgressOutcome.Unresolved) ||
                (result.Outcome == CampaignProgressOutcome.Unresolved ? string.IsNullOrWhiteSpace(result.UnresolvedOwner) :
                    result.UnresolvedOwner is not null))
                throw new InvalidDataException("Movement transition fabricated publication or input completion.");
            armyProgress.Validate(State);
            if (armyProgress.LastStorySequence < LastStorySequence)
                throw new InvalidDataException("Movement rolled back its presentation sequence.");
            _progress = new(null, null, armyProgress.Copy()); LastStorySequence = armyProgress.LastStorySequence;
            return result with { ArmyProgress = _progress.ArmyProgress!.Copy() };
        }
        if (result.Outcome == CampaignProgressOutcome.AwaitingArmyProgress)
            throw new InvalidDataException("Movement wait has no continuation owner.");
        if (result.ManualTactic is { } manual)
        {
            if (result.State is not null || result.Outcome is not (CampaignProgressOutcome.AwaitingManualTactic or CampaignProgressOutcome.Unresolved) ||
                (result.Outcome == CampaignProgressOutcome.Unresolved ? !manual.ContinueAfterPresentation || string.IsNullOrWhiteSpace(result.UnresolvedOwner) : manual.ContinueAfterPresentation || result.UnresolvedOwner is not null))
                throw new InvalidDataException("Manual tactic transition fabricated publication or a reply.");
            manual.Validate(State);
            if (manual.LastStorySequence < LastStorySequence) throw new InvalidDataException("Manual tactic rolled back story sequence.");
            _progress = new(null, manual.Copy()); LastStorySequence = manual.LastStorySequence;
            return result with { ManualTactic = _progress.ManualTactic!.Copy() };
        }
        if (result.Outcome == CampaignProgressOutcome.AwaitingManualTactic) throw new InvalidDataException("Manual tactic wait has no owner.");
        if (result.Outcome == CampaignProgressOutcome.Unresolved)
        {
            if (result.State is not null || string.IsNullOrWhiteSpace(result.UnresolvedOwner))
                throw new InvalidDataException("Unresolved progress published a partial result.");
            if (result.Pending is { } accepted)
            {
                accepted.Validate(State);
                if (accepted.LastStorySequence < LastStorySequence) throw new InvalidDataException("A continuation rolled back its story sequence.");
                if (!accepted.ContinueAfterPresentation) throw new InvalidDataException("Unresolved progress fabricated a presentation wait.");
                _progress = new(accepted.Copy(), null); LastStorySequence = accepted.LastStorySequence;
            }
            return result;
        }
        if (result.Outcome == CampaignProgressOutcome.AwaitingStory)
        {
            if (result.State is not null || result.Pending is not { } pending || result.UnresolvedOwner is not null)
                throw new InvalidDataException("Pending progress lacks its private continuation.");
            pending.Validate(State); _progress = new(pending.Copy(), null); LastStorySequence = pending.LastStorySequence;
            return result with { Pending = _progress.Story!.Copy() };
        }
        if (result.State is not { } world || result.Pending is not null || result.UnresolvedOwner is not null)
            throw new InvalidDataException("Committed progress lacks its complete world.");
        if (request.NormalPeriod is not null)
            throw new InvalidDataException("normal.period.completion_owner_not_supplied_by_finite_prefix");
        GameEngine.ValidateWorldIdentity(State, world);
        if (world.Date != State.Date || world.PeriodNumber != State.PeriodNumber || world.Phase != State.Phase)
            throw new InvalidDataException("Native block progress performed the separate period finalizer.");
        var owned = GameStateCopy.Copy(world);
        rules.Publish(owned, result.ServiceChanges, result.Grouping);
        State = owned; _progress = null;
        LastStorySequence = Math.Max(LastStorySequence, result.LastStorySequence);
        return result with { State = State };
    }
}
