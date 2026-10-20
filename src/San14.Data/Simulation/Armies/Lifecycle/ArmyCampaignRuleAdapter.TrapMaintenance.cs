using San14.Core;

namespace San14.Data;

public sealed record FacilityTrapStageResult(GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The complete reached22 manager. No independent hourly gate
    /// is added to the actual scheduler call. Unknown rolls back this capture.</summary>
    public FacilityTrapStageResult TryAdvanceFacilityTraps(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.FacilityTraps ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Trap maintenance requires reached stage22 entry.");
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            CaptureBaseThreatInputs.BindCurrent(state, current);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            current.ArmyReturnTransferMode = new(false, null);
            return new(new TrapEffectApplication(this, state, current, moment, _repository, _scenario).Run(), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
        catch (PersonArmyCaptureSource.MissingOwner error) { return new(null, error.Message); }
    }
}
