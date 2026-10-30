using System.Collections.Immutable;

namespace San14.Core;

public sealed record StrategicPoisonEmperorContinuation(int TaskId, int TargetCityPhysicalSlot,
    int ExecutorPhysicalSlot, int OwnForcePhysicalSlot, int PreviousForcePhysicalSlot,
    NativeMapSeasonDate CompletionDate, int ResultCode);
public sealed record EditedEventForceAbsorptionTail(int OriginalForcePhysicalSlot,
    int RecipientForcePhysicalSlot, bool RestoreRecordingGate);
public sealed record EditedEventResultCursor(ImmutableArray<CampaignStoryDispatchFrame> Frames,
    int SuccessCount, int EvaluatedCount, bool RootFold);

public sealed partial record GameState
{
    public StrategicPoisonEmperorContinuation? StrategicPoisonEmperorContinuation { get; init; }
    public EditedEventForceAbsorptionTail? EditedEventForceAbsorptionTail { get; init; }
    public EditedEventResultCursor? EditedEventResultCursor { get; init; }
}
