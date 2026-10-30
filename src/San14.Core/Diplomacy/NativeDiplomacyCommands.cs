using System.Collections.Immutable;

namespace San14.Core;

/// <summary>Signed final transfers: positive gives to the recipient, negative
/// asks from it. The submitted escrow is separately required to be nonnegative.</summary>
public sealed record DiplomacyOffer(int Gold, int Supplies, int? ItemId = null, int? AreaId = null);

public enum DiplomaticNegotiationStage
{
    RecipientDecision = 2, SenderPersuasion = 6, RecipientCounterOffer = 8, SenderFinalOffer = 10
}

//The reached negotiation window is independent of the personnel journey cursor.
//Each expense is an actual consumed District military-intelligence amount.
public sealed record DiplomaticNegotiationState(DiplomaticNegotiationStage Stage,
    DiplomacyOffer Proposal, int Probability, bool CanEditConditions, bool? ForcedResult,
    int SenderFirstExpense = 0, int RecipientExpense = 0, int SenderFinalExpense = 0,
    bool PersuasionLocked = false);

public sealed record PersonnelDiplomacyTask(int Id, int DispatchSlot, int[] MemberIds,
    DiplomaticMissionKind Kind, int NativeKind, int ForceId, int SourceCityId, int SourceDistrictId,
    int TargetForceId, int TargetRulerOfficerId, int TargetCapitalCityId,
    int AllianceMonths, int? TargetCityId, DiplomacyOffer InitialOffer,
    int StartHexId, int EndHexId, NativeMapSeasonDate StartDate, NativeMapSeasonDate DueDate,
    bool Started, bool NeedsRefresh, bool Cancelled, bool AwaitingNegotiation,
    bool IsRetiring, bool IsAssociated)
{
    // The source order is retained while normal arrival pruning removes ineligible captives.
    public ImmutableArray<int> RequestedCaptiveOfficerIds { get; init; } = [];
    public DiplomacyOffer RequestedOffer { get; init; } = new(0, 0);
    // Null preserves an unknown legacy dispatch forecast; -1 is the actual no-override result.
    public int? AdvisorOutcomeAtDispatch { get; init; }
    public NativeMapSeasonDate? NegotiationDate { get; init; }
    public DiplomaticNegotiationState? Negotiation { get; init; }
}

public sealed record DiplomacyNegotiationRequest(int TaskId, int ForceId, int TargetForceId,
    int ExecutorOfficerId, DiplomaticMissionKind Kind, DiplomacyOffer InitialOffer,
    int AllianceMonths, int? TargetCityId, NativeMapSeasonDate ArrivalDate)
{
    public DiplomaticNegotiationState? Negotiation { get; init; }
    public ImmutableArray<int> RequestedCaptiveOfficerIds { get; init; } = [];
    public DiplomacyOffer RequestedOffer { get; init; } = new(0, 0);
}

/// <summary>A real player decision, not an animation acknowledgement.</summary>
public sealed record ConfirmDiplomaticNegotiationOrder(int ForceId, int TaskId,
    DiplomacyOffer Offer, bool Accepted) : GameOrder(ForceId)
{
    //Null means no additional persuasion was requested. Zero is an explicit selection.
    public int? MilitaryIntelligence { get; init; }
}

public interface IPlayableDiplomacyCommandRules : IDiplomacyPolicy
{
    CommandResult SubmitDiplomacy(GameState state, ForceState force,
        PrepareDiplomaticOrder order, CommandContext context);
    CommandResult ConfirmDiplomacy(GameState state, ForceState force,
        ConfirmDiplomaticNegotiationOrder order, CommandContext context);
    GameState AdvanceDiplomacyPeriod(GameState state, int nextPeriodNumber);
}

public sealed record DiplomacyCommandAvailability(IReadOnlyList<int> ExecutorOfficerIds,
    IReadOnlyList<int> TargetForceIds, bool? CanSubmit, string? MissingRuleKey, int? CommandBooks);
