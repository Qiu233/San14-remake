using San14.Core;

namespace San14.Data;

public sealed record FireDamageStageResult(GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState WriteFireDamageBaseWounded(GameState state, SourceWorldContext current,
        BaseReference target, int requestedValue)
    {
        int capacity = BasePostLoad.ReadTroopCapacity(target, current);
        current.SetArmyReturnBaseWounded40(target, requestedValue < 0 ? 0 : Math.Min(requestedValue, capacity));
        return PublishCurrentBaseCombatQuantities(state, current, target, false, true, false);
    }
    /// <summary>Complete reached stage23. The caller owns calendar advancement;
    /// no remainder stage or application presentation is acknowledged here.</summary>
    public FireDamageStageResult TryAdvanceFireDamage(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.FireDamage ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Fire damage requires actual stage23 entry.");
        int halfHour = moment.PreWriterThirdDate.Hour / 2;
        if (halfHour <= 0 || (halfHour + 1) % 6 != 0) return new(state, null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            CaptureBaseThreatInputs.BindCurrent(state, current);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            current.ArmyReturnTransferMode = new(false, null);
            return new(new FireDamageApplication(this, state, current, moment, _normalAftermathInputs).Run(), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
        catch (PersonArmyCaptureSource.MissingOwner error) { return new(null, error.Message); }
    }
}
