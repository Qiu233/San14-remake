using System.Text.Json.Serialization;
namespace San14.Core;

public enum ManualTacticStagePhase { EnterStage, AfterIntervention }
/// <summary>Only the reached stage6 transaction may hold this continuation.
/// Its ordinary committed world remains unpublished while effects are pending.</summary>
public sealed record ManualTacticProgressOperation(string Revision,
    CampaignProgressRequest Request, int BlockIndex, GameState Candidate,
    CampaignOperationWorldContext Context, NativeNormalArmyCombatCalendarState Calendar,
    ManualTacticStagePhase StagePhase, TacticPresentationReceipt? Presentation,
    long LastStorySequence, SavedArmyCombatCapture[] Captures,
    CampaignGroupingCheckpoint Grouping, CampaignServiceChanges ServiceChanges)
{
    [JsonRequired] public bool ContinueAfterPresentation { get; init; }
    [JsonIgnore] public CapturedRulerTreatmentRequest? PendingCapturedRulerTreatment =>
        Candidate.ArmyDefeatContinuation?.PendingCapturedRulerTreatment ?? Candidate.BaseCaptureContinuation?.PendingCapturedRulerTreatment;
    [JsonIgnore] public RulerSuccessionRequest? PendingRulerSuccession =>
        Candidate.PersonDeathContinuation?.PendingSuccession;
    [JsonIgnore] public ArmyReturnCaptiveContinuation? PendingArmyReturnCaptives =>
        Candidate.ArmyReturnCaptiveContinuation;
    [JsonIgnore] public bool IsAwaitingArmyReturnCaptiveReply => PendingArmyReturnCaptives is { Phase:
        not ArmyReturnCaptivePhase.ReadyToFinishReturn };
    [JsonIgnore] public bool RequiresAutomaticManualTacticContinuation =>
        Candidate.ManualTacticExecutionContinuation is not null &&
        PendingCapturedRulerTreatment is null && PendingRulerSuccession is null && !IsAwaitingArmyReturnCaptiveReply;
    [JsonIgnore] public TacticPresentationRequest? CurrentPresentation => Presentation is not { } key ? null :
        ManualTacticInterventionState.CopyRequest((key.Kind == TacticPresentationKind.UniqueDirection
            ? Candidate.ManualTacticIntervention!.Directions : Candidate.ManualTacticIntervention!.Effects)[0]);
    public ManualTacticProgressOperation Copy() => this with
    {
        Candidate = GameStateCopy.Copy(Candidate),
        Captures = [], // Old diagnostic snapshots have no runtime consumer.
        Grouping = Grouping with { Roots = Grouping.Roots.ToArray() }, ServiceChanges = ServiceChanges.Copy()
    };
    public void Validate(GameState committed)
    {
        if (string.IsNullOrWhiteSpace(Revision) || Request is null || Candidate is null || Context is null || Calendar is null ||
            Captures is null || Grouping is null || ServiceChanges is null ||
            Request.BlockCount is < 1 or > 40 || BlockIndex < 0 || BlockIndex >= Request.BlockCount ||
            Context.Moment.SchedulerStageIndex != 6 || !Enum.IsDefined(StagePhase) ||
            committed.NativeMapSeason is null || Request.StartCalendarCycleOrdinal != committed.NativeMapSeason.AdvancedCalendarSteps ||
            Context.BlockStartSeason.AdvancedCalendarSteps != Request.StartCalendarCycleOrdinal + 3L * BlockIndex ||
            Calendar.NextCalendarCycleOrdinal != Context.BlockStartSeason.AdvancedCalendarSteps || LastStorySequence < 0 ||
            Grouping.Roots is not { Length: 1503 } || Grouping.Roots.Any(x => x > 500) ||
            !ContinueAfterPresentation && Presentation is null && Candidate.ManualTacticExecutionContinuation is null ||
            ContinueAfterPresentation && Presentation is not null ||
            Candidate.ManualTacticExecutionContinuation is not null && Presentation is not null ||
            ContinueAfterPresentation && (PendingCapturedRulerTreatment is not null || PendingRulerSuccession is not null ||
                IsAwaitingArmyReturnCaptiveReply))
            throw new InvalidDataException("Manual tactic continuation is incomplete.");
        ArmyReturnCaptiveContinuationValidation.Validate(Candidate);
        Request.ValidateNormalPeriod(committed);
        GameEngine.ValidateWorldIdentity(committed, Candidate); Context.Validate(Candidate);
        ServiceChanges.Validate();
        ManualTacticInterventionState.Validate(Candidate.ManualTacticIntervention);
        var manager = Candidate.ManualTacticIntervention ?? throw new InvalidDataException("Manual tactic manager is unknown.");
        if (manager.LastPresentationSequence > LastStorySequence)
            throw new InvalidDataException("Manual tactic continuation lost its campaign presentation sequence.");
        if (Presentation is { } shown)
        {
            var queue = shown.Kind == TacticPresentationKind.UniqueDirection ? manager.Directions : manager.Effects;
            if (queue.Length == 0 || queue[0].Sequence != shown.Sequence || queue[0].Kind != shown.Kind)
                throw new InvalidDataException("Manual tactic continuation does not name a waiting manager head.");
        }
        long previous = Request.StartCalendarCycleOrdinal - 1;
        foreach (var capture in Captures)
        {
            if (capture is null || capture.Context.Moment.SchedulerStageIndex != 13 ||
                capture.Context.Moment.CalendarCycleOrdinal <= previous ||
                capture.Context.Moment.CalendarCycleOrdinal >= Context.Moment.CalendarCycleOrdinal)
                throw new InvalidDataException("Combat captures do not precede the manual tactic wait.");
            capture.Context.Validate(capture.State); GameEngine.ValidateWorldIdentity(committed, capture.State);
            previous = capture.Context.Moment.CalendarCycleOrdinal;
        }
    }
}

/// <summary>One exclusive transaction slot, with two named continuation kinds.</summary>
internal sealed record CampaignPendingOperation(CampaignProgressOperation? Story, ManualTacticProgressOperation? ManualTactic,
    CampaignArmyProgressOperation? ArmyProgress = null)
{
    public void Validate(GameState world)
    {
        if ((Story is null ? 0 : 1) + (ManualTactic is null ? 0 : 1) + (ArmyProgress is null ? 0 : 1) != 1)
            throw new InvalidDataException("Campaign continuation requires exactly one named operation.");
        Story?.Validate(world); ManualTactic?.Validate(world); ArmyProgress?.Validate(world);
    }
}
public interface ICampaignManualTacticProgressRules : ICampaignProgressRules
{
    CampaignProgressTransition ResumeManualTactic(ManualTacticProgressOperation pending, TacticPresentationReceipt receipt);
    CampaignProgressTransition RetryManualTactic(ManualTacticProgressOperation pending);
}
