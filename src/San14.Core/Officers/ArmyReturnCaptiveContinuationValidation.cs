namespace San14.Core;

internal static partial class ArmyReturnCaptiveContinuationValidation
{
    private static void ValidateHeldSuffix(GameState state, ArmyReturnCaptiveContinuation value)
    {
        if (!Enum.IsDefined(value.Caller) || value.Caller == ArmyReturnCaptiveCaller.Unspecified ||
            value.RequestedOfficerIds.IsDefault ||
            value.RequestedOfficerIds.Distinct().Count() != value.RequestedOfficerIds.Length ||
            value.RequestedOfficerIds.Any(id => !value.OriginalOfficerIds.Contains(id)) ||
            value.ActiveBatchOfficerIds.Any(id => !value.RequestedOfficerIds.Contains(id)) ||
            value.FirstResultQueued && value.FirstResultPresentation is null ||
            value.Phase == ArmyReturnCaptivePhase.ApplyingBatch ||
            value.Phase == ArmyReturnCaptivePhase.AwaitingPersonDeath &&
                (state.PersonDeathContinuation is null || value.ActiveDisposition != ArmyReturnCaptiveDisposition.Execute ||
                 value.ActiveBatchIndex >= value.ActiveBatchOfficerIds.Length ||
                 value.ActiveBatchOfficerIds[value.ActiveBatchIndex] != value.AwaitingPersonDeathOfficerId) ||
            value.Phase is (ArmyReturnCaptivePhase.AwaitingIntroduction or ArmyReturnCaptivePhase.AwaitingSelection or
                ArmyReturnCaptivePhase.ReadyToFinishReturn) &&
                (value.ActiveDisposition is not null || !value.RequestedOfficerIds.IsEmpty ||
                 !value.ActiveBatchOfficerIds.IsEmpty || value.ActiveBatchIndex != 0 ||
                 value.FirstResultPresentation is not null || value.FirstResultQueued)) throw Invalid();
        if (value.FirstResultPresentation is { } result &&
            (result.Operation != TalentCommandPresentationOperation.OfficerText ||
             result.OfficerId is not int officer || !value.OriginalOfficerIds.Contains(officer) ||
             string.IsNullOrEmpty(result.TextAtSource) || result.SoundId is not null ||
             result.Speaker is { } speaker && (speaker.OfficerId != officer || speaker.NameAtSource is null) ||
             result.Voice is { } voice && (voice.SpeakerId != officer || voice.VoiceId is not (35 or 40 or 45) || voice.VoiceType >= 26 ||
                 !Enum.IsDefined(voice.Language) || voice.VariantIndex is < 0 or > 4 || voice.SoundId == 0)))
            throw Invalid();
        if ((value.Caller == ArmyReturnCaptiveCaller.MovementArrival) != (value.ArrivalTail is not null) ||
            (value.Caller == ArmyReturnCaptiveCaller.BaseCapture) != (value.CaptureTail is not null) ||
            (value.Caller == ArmyReturnCaptiveCaller.CampaignWithdrawal) != (value.CampaignTail is not null))
            throw Invalid();
        if (value.ArrivalTail is { } arrival &&
            (arrival.Moment is null || arrival.Moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.Movement ||
             arrival.Moment.CalendarCycleOrdinal != value.SourceRevision || arrival.Moment.PreWriterThirdDate != value.SourceDate ||
             arrival.DestinationLocationId != value.Destination.Id)) throw Invalid();
        if (value.CaptureTail is { } capture &&
            (capture.Operation is not { } operation || capture.Request != operation.Request ||
             capture.Request.WinnerUnitId != value.ArmyUnitId || capture.Request.Target != value.Destination ||
             operation.WinnerReceiverId != value.BatchId || operation.WinnerArmyPhysicalSlot != value.ArmyPhysicalSlot ||
             capture.WinningForceId != value.CapturingForceId || operation.WinnerForceId != capture.WinningForceId ||
             operation.WinnerDistrictId != capture.WinningDistrictId || capture.SettlementIndex < -1 ||
             capture.BeforeStateCount < 0 || capture.BeforeRegionCount < 0 || capture.StatePhysicalSlots.IsDefault ||
             capture.RegionPhysicalSlots.IsDefault || capture.StatePhysicalSlots.Any(slot => slot < 0) ||
             capture.RegionPhysicalSlots.Any(slot => slot < 0))) throw Invalid();
    }
}
