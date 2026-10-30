using System.Collections.Immutable;

namespace San14.Core;

public enum CapturedRulerDisposition { Execute, Release }
public sealed record CapturedRulerTreatmentRequest(int OfficerId, int CapturingForceId,
    int CapturingRulerId, bool CanExecute, string TextAtSource);
public sealed record ConfirmCapturedRulerTreatmentOrder(int ForceId, int OfficerId,
    CapturedRulerDisposition Disposition) : GameOrder(ForceId);

public enum ArmyDefeatResumePoint { AfterRulerTreatment, AfterPersonDeath, AfterSpecialCaptureAnnouncement }
/// <summary>The already executed defeat prefix is retained in the candidate.
/// These captured operands resume only the remaining settlement tail.</summary>
public sealed record ArmyDefeatContinuation(int SettlementIndex, UnitDefeatRequest Defeat,
    int ArmyPhysicalSlot, ArmyReceiverId ReceiverId, int VictimForceId, int CommanderOfficerId,
    int HomeCityId, int HexId, int? WinnerArmyPhysicalSlot, int? WinnerOfficerId,
    SettlementBase? WinnerBase, int? WinnerForceId, int? SampleDay, int? SampleSalt,
    ArmyDefeatResumePoint ResumePoint, int CaptureImmunity)
{
    public CapturedRulerTreatmentRequest? PendingCapturedRulerTreatment { get; init; }
    public CapturedRulerDisposition? ChosenDisposition { get; init; }
    public bool DeathReturnedEarly { get; init; }
    public HiredArmyCaptureProgress? HiredCapture { get; init; }
    /// <summary>The actual captured Base, including a Gate's location identity.
    /// HomeCityId remains only the report's current City context.</summary>
    public SettlementBase? VictimHome { get; init; }
    public bool SpecialCommanderReclaim { get; init; }
    public bool? ReclaimDefeatedCommander { get; init; }
}

public sealed record RulerSuccessionRequest(int ForceId, int DeceasedOfficerId,
    ImmutableArray<int> CandidateOfficerIds, string PromptAtSource, string EmptyChoiceWarningAtSource);
public sealed record ConfirmRulerSuccessionOrder(int ForceId, int OfficerId) : GameOrder(ForceId);

public enum PersonDeathBaseKind { City, Gate }
public sealed record PersonDeathBaseContext(PersonDeathBaseKind Kind, int? PhysicalSlot);
/// <summary>Captured references needed after the actual successor selection.
/// Death reports, random draws and the completed prefix are never replayed.</summary>
public sealed record PersonDeathContinuation(int OfficerId, int PersonPhysical, int? KillerPhysical,
    int Cause, bool RetireAssociatedArmy, bool EmitReports, bool EmitHistory, bool SuppressReports,
    PersonDeathBaseContext? OriginalBase, PersonDeathBaseContext? OriginalCity,
    int? OriginalHexPhysical, int? OriginalForcePhysical, byte OriginalStatus, int OriginalOffice,
    int? OriginalSwornPersonPhysical, int OriginalMeritStage, int? AssociatedCommandId,
    RulerSuccessionRequest PendingSuccession);

public interface INormalPeriodCapturedRulerRules
{
    CommandResult ConfirmCapturedRulerTreatment(GameState state,
        ConfirmCapturedRulerTreatmentOrder order, CommandContext context);
}
public interface IRulerSuccessionRules
{
    CommandResult ConfirmRulerSuccession(GameState state,
        ConfirmRulerSuccessionOrder order, CommandContext context);
}

public sealed class RulerDecisionOrders(IOrderRules next, INormalPeriodCapturedRulerRules? capturedRules,
    IRulerSuccessionRules? successionRules) : IOrderRules
{
    public CommandResult Apply(GameState state, ForceState force, GameOrder order, CommandContext context)
    {
        if (order is ConfirmRulerSuccessionOrder succession)
        {
            if (context.DelegatedDistrictId is not null || state.PersonDeathContinuation?.PendingSuccession is not { } request ||
                request.ForceId != force.Id || !request.CandidateOfficerIds.Contains(succession.OfficerId))
                return CommandResult.Reject("This successor is not awaiting selection.", "ruler.succession_pending");
            return successionRules?.ConfirmRulerSuccession(state, succession, context) ??
                CommandResult.RuleUnavailable("ruler.succession_rules", "Ruler succession rules are unavailable.");
        }
        if (order is not ConfirmCapturedRulerTreatmentOrder treatment) return next.Apply(state, force, order, context);
        if (context.DelegatedDistrictId is not null || state.PersonDeathContinuation is not null ||
            (state.ArmyDefeatContinuation?.PendingCapturedRulerTreatment ??
                state.BaseCaptureContinuation?.PendingCapturedRulerTreatment) is not { } captured ||
            captured.CapturingForceId != force.Id || captured.OfficerId != treatment.OfficerId ||
            !Enum.IsDefined(treatment.Disposition) || treatment.Disposition == CapturedRulerDisposition.Execute && !captured.CanExecute)
            return CommandResult.Reject("This captive ruler is not awaiting this decision.", "ruler.treatment_pending");
        return capturedRules?.ConfirmCapturedRulerTreatment(state, treatment, context) ??
            CommandResult.RuleUnavailable("ruler.treatment_rules", "Captured ruler treatment rules are unavailable.");
    }
}

public sealed partial record GameState
{
    public ArmyDefeatContinuation? ArmyDefeatContinuation { get; init; }
    public PersonDeathContinuation? PersonDeathContinuation { get; init; }
}

internal static class RulerDecisionContinuationValidation
{
    internal static void Validate(GameState state)
    {
        NormalCombatImmediateContinuationValidation.Validate(state);
        DuelAftermathContinuationValidation.Validate(state);
        BaseCaptureContinuationValidation.Validate(state);
        ManualTacticExecutionContinuationValidation.Validate(state);
        if (state.ArmyDefeatContinuation is { } defeat)
        {
            bool immediate = state.NativePeriodContinuation?.Cursor is { StageIndex: 13 } &&
                state.NormalCombatImmediateContinuation is not null ||
                state.NativePeriodContinuation?.Cursor is { StageIndex: 5 } && state.DuelAftermathContinuation is not null ||
                state.ManualTacticExecutionContinuation is not null;
            if ((immediate ? defeat.SettlementIndex != -1 : defeat.SettlementIndex < 0) ||
                defeat.Defeat is null || defeat.Defeat.VictimUnitId <= 0 ||
                defeat.Defeat.WinnerUnitId is <= 0 || !Enum.IsDefined(defeat.Defeat.Cause) ||
                !Settlement(defeat.Defeat.WinnerBase) || !Settlement(defeat.WinnerBase) || defeat.ArmyPhysicalSlot < 0 ||
                defeat.ReceiverId.Value <= 0 || defeat.VictimForceId is < 0 or > 51 ||
                defeat.CommanderOfficerId is < 0 or > 6000 ||
                (defeat.VictimHome is null ? !defeat.SpecialCommanderReclaim || defeat.HomeCityId != 0 :
                    !ValidDefeatHome(defeat.VictimHome) || defeat.HomeCityId is < 1 or > 51) ||
                (defeat.ResumePoint == ArmyDefeatResumePoint.AfterPersonDeath
                    ? defeat.ReclaimDefeatedCommander is not null : defeat.ReclaimDefeatedCommander is null) ||
                defeat.ReclaimDefeatedCommander == true && (!defeat.SpecialCommanderReclaim || defeat.Defeat.Cause == UnitDefeatCause.DuelDeath) ||
                defeat.SpecialCommanderReclaim && defeat.VictimForceId is < 46 or > 51 ||
                defeat.VictimForceId == 51 && !defeat.SpecialCommanderReclaim ||
                defeat.VictimHome is { Kind: SettlementBaseKind.City } cityHome && cityHome.Id != defeat.HomeCityId ||
                defeat.HexId is < 0 or > 48400 || defeat.WinnerArmyPhysicalSlot is < 0 ||
                defeat.WinnerOfficerId is < 0 or > 6000 || defeat.WinnerForceId is < 0 or > 51 ||
                (defeat.SampleDay is null) != (defeat.SampleSalt is null) || !Enum.IsDefined(defeat.ResumePoint) ||
                defeat.ChosenDisposition is { } disposition && !Enum.IsDefined(disposition) ||
                (defeat.ResumePoint == ArmyDefeatResumePoint.AfterSpecialCaptureAnnouncement
                    ? defeat.HiredCapture is not { AnnouncementBatchSequence: > 0 } hired ||
                        !Enum.IsDefined(hired.Phase) || defeat.PendingCapturedRulerTreatment is not null ||
                        defeat.ChosenDisposition is not null || defeat.ReclaimDefeatedCommander != false
                    : defeat.HiredCapture is not null))
                throw new InvalidDataException("Invalid saved army defeat continuation.");
            if (defeat.PendingCapturedRulerTreatment is { } request &&
                (request.OfficerId is < 1 or > 6000 || request.CapturingForceId is < 1 or > 45 ||
                 request.CapturingRulerId is < 1 or > 6000 || request.TextAtSource is null ||
                 defeat.ResumePoint != ArmyDefeatResumePoint.AfterRulerTreatment || defeat.ChosenDisposition is not null))
                throw new InvalidDataException("Invalid saved captive ruler decision.");
        }
        if (state.PersonDeathContinuation is not { } death) return;
        var succession = death.PendingSuccession;
        if (death.OfficerId is < 0 or > 6000 || death.PersonPhysical is < 0 or > 6000 ||
            death.KillerPhysical is < 0 or > 6000 || death.OriginalHexPhysical is < 0 or > 48400 ||
            death.OriginalForcePhysical is < 0 or > 51 || death.OriginalSwornPersonPhysical is < 0 or > 6000 ||
            !Base(death.OriginalBase) || !Base(death.OriginalCity) || succession is null ||
            succession.ForceId is < 0 or > 51 || succession.DeceasedOfficerId != death.OfficerId ||
            succession.CandidateOfficerIds.IsDefaultOrEmpty || succession.CandidateOfficerIds.Any(id => id is < 1 or > 6000) ||
            succession.PromptAtSource is null || succession.EmptyChoiceWarningAtSource is null)
            throw new InvalidDataException("Invalid saved ruler succession continuation.");
    }

    private static bool ValidDefeatHome(SettlementBase? value) => value is
        { Kind: SettlementBaseKind.City, Id: >= 1 and <= 51 } or
        { Kind: SettlementBaseKind.Gate, Id: >= 52 and <= 61 };

    private static bool Base(PersonDeathBaseContext? value) => value is null ||
        Enum.IsDefined(value.Kind) && (value.PhysicalSlot is null or >= 0);

    private static bool Settlement(SettlementBase? value) => value is null ||
        Enum.IsDefined(value.Kind) && value.Id >= 0;
}
