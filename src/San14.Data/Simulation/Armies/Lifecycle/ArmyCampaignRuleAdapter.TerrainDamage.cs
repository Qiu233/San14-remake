using San14.Core;

namespace San14.Data;

public sealed record TerrainDamageStageResult(GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The actual stage24 terrain manager in the ordinary owned
    /// application. Calendar and final transaction commit belong to the driver.</summary>
    public TerrainDamageStageResult TryAdvanceTerrainDamage(GameState state, NativeArmyProgressStageMoment moment)
    {
        if (moment.SchedulerStageIndex != (byte)NativeArmyProgressStage.TerrainDamage ||
            moment.Boundary != NativeArmyProgressStageBoundary.StageEntry ||
            moment.PreWriterThirdDate.Hour is < 0 or > 22 || (moment.PreWriterThirdDate.Hour & 1) != 0)
            throw new InvalidDataException("Terrain damage requires actual stage24 entry.");
        int halfHour = moment.PreWriterThirdDate.Hour / 2;
        if (halfHour <= 0 || (halfHour + 1) % 6 != 0) return new(state, null);
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            _baseCapacityInputs.BindCurrent(state, current);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            //This is the same ordinary standalone host profile used by the
            //owned22/23 adapters, not a classifier of foreign challenge state.
            current.ArmyReturnTransferMode = new(false, null);
            return new(new TerrainDamageApplication(this, state, current, moment).Run(), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
        catch (PersonArmyCaptureSource.MissingOwner error) { return new(null, error.Message); }
    }
}
