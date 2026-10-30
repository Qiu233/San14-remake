using San14.Core;

namespace San14.Data;

public sealed record FacilityEffectStageResult(GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual stage14 time gate, then one current Object manager and
    /// private list-pool capture. Support, damage, morale and abnormal channels
    /// retain their own reached handlers and explicit source inputs.</summary>
    public FacilityEffectStageResult TryAdvanceFacilityEffects(GameState state,
        NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.FacilityEffects ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Facility effects require reached stage14.");
        if (moment.PreWriterThirdDate.Hour / 2 != 5) return new(state, null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            _ = _baseCapacityInputs.BindCurrent(state, current);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            return new(FacilityEffectMilitaryMusicSource.Apply(state, current, moment,
                (image, requests) => new(this, image, current, moment, _repository, _scenario, requests),
                (image,army)=>CaptureFacilityPeriodicArmyViewCurrent(image,current,army)), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }
}
