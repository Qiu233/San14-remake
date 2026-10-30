using System.Collections.Immutable;

namespace San14.Core;

public sealed record EmperorCaptureDecisionRequest(int CapturingForceId, int CityId,
    int EmperorId, string TextAtSource);

public sealed record AutomaticEmperorCaptureContext(int CityPhysicalSlot, int CityLocationId,
    int EmperorPhysicalSlot, int EmperorId, int NewForcePhysicalSlot, int RulerPhysicalSlot,
    int RulerId, int? ExcludedForcePhysicalSlot, bool NewForceIsPlayer, int KingdomName);

public enum AutomaticEmperorCapturePhase
{
    AwaitingChoice, Opening, ReceptionSceneEnter, ReceptionFirstNarration,
    ReceptionSecondNarration, ReceptionSceneExit, AbolitionNarration,
    ReportsAndLoyalty, ApplyLoyaltyAndPrepareDiplomacy, ApplyDiplomacy, Completed
}

/// <summary>One reached automatic emperor operation. Each phase records the
/// next business segment; queued presentation must be consumed before it runs.</summary>
public sealed record AutomaticEmperorCaptureProgress(AutomaticEmperorCaptureContext Context,
    AutomaticEmperorCapturePhase Phase, int? SelectedMode, string? ChoiceText)
{
    public ImmutableArray<int> LoyaltyPeople { get; init; } = [];
    public ImmutableArray<int> UnfavorableForces { get; init; } = [];
    public ImmutableArray<int> FavorableForces { get; init; } = [];
    public EmperorCaptureDecisionRequest? PendingDecision { get; init; }
    public bool NeedsPresentationReceipt { get; init; }
    public long? AwaitingEffectBatchSequence { get; init; }
}

public sealed record CityCaptureEmperorTail(int SettlementIndex,
    BaseCaptureOperationContext Operation, int CapturedOldDistrictPhysicalSlot,
    ImmutableArray<int> RelatedAreaPhysicalSlots, ImmutableArray<int> StatePhysicalSlots,
    ImmutableArray<int> RegionPhysicalSlots, int BeforeStateCount, int BeforeRegionCount,
    ImmutableArray<int> CapturedPersonPhysicalSlots, ImmutableArray<int> NoncapturedPersonPhysicalSlots,
    ImmutableArray<int> SpecialPersonPhysicalSlots, ImmutableArray<int> ChangedForcePersonPhysicalSlots);

public sealed record BaseWinnerEmperorTail(BaseCaptureRequest Request,
    int CapturedOldDistrictPhysicalSlot, int OldForcePhysicalSlot, int NewForcePhysicalSlot,
    int DestinationDistrictPhysicalSlot, NativeMapSeasonDate? OperationDate,
    int? SampleDay, int? SampleSalt, ImmutableArray<int> RelatedAreaPhysicalSlots,
    ImmutableArray<int> StatePhysicalSlots, ImmutableArray<int> RegionPhysicalSlots,
    int BeforeStateCount, int BeforeRegionCount, ImmutableArray<int> CapturedPersonPhysicalSlots,
    ImmutableArray<int> NoncapturedPersonPhysicalSlots, ImmutableArray<int> SpecialPersonPhysicalSlots,
    ImmutableArray<int> ChangedForcePersonPhysicalSlots)
{
    public int SettlementIndex { get; init; } = -1;
}

public sealed record ForceTransferEmperorTail(int CityPhysicalSlot, int OldForcePhysicalSlot,
    int NewForcePhysicalSlot, int DestinationDistrictPhysicalSlot,
    int CapturedOldDistrictPhysicalSlot, NativeMapSeasonDate? OperationDate,
    ImmutableArray<int> RelatedAreaPhysicalSlots);

public sealed record StoryForceTransferContinuation(int Code, int TargetCityPhysicalSlot,
    int PreviousForcePhysicalSlot, int? ExecutorPhysicalSlot, int? DestinationDistrictPhysicalSlot,
    ImmutableArray<int> CityPhysicalSlots, int CityIndex, bool ManagerPresent);

public sealed record ForceAbsorptionContinuation(int OriginalForcePhysicalSlot,
    int RecipientForcePhysicalSlot, int PrimaryDistrictPhysicalSlot, int RecipientRulerId,
    NativeMapSeasonDate OperationDate, bool SuppressEliminationPresentation,
    ImmutableArray<int> DistrictPhysicalSlots, ImmutableArray<SettlementBase> ProcessedBases,
    ImmutableArray<SettlementBase> RemainingBases, int BaseIndex);

public sealed partial record GameState
{
    public AutomaticEmperorCaptureProgress? AutomaticEmperorCapture { get; init; }
    public StoryForceTransferContinuation? StoryForceTransferContinuation { get; init; }
    public ForceAbsorptionContinuation? ForceAbsorptionContinuation { get; init; }
    public CityCaptureEmperorTail? CityCaptureEmperorTail { get; init; }
    public BaseWinnerEmperorTail? BaseWinnerEmperorTail { get; init; }
    public ForceTransferEmperorTail? ForceTransferEmperorTail { get; init; }
}
