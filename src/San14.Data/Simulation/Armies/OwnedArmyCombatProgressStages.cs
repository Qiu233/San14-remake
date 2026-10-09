using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Owns the ordinary Return manager0, finite Army stage13 and real19/26
    /// consumers. The caller still supplies every remaining reached stage;
    /// this adapter does not claim Base/Gate/transport or whole-world coverage.</summary>
    public IPlayableNativeArmyProgressStageSource CreateOrdinaryArmyCombatProgressStages(
        IPlayableNativeArmyProgressStageSource remainingStages,
        INormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        ArgumentNullException.ThrowIfNull(remainingStages); ArgumentNullException.ThrowIfNull(forceDefinitions);
        return new OrdinaryArmyCombatStageSource(this, remainingStages, forceDefinitions);
    }

    private sealed class OrdinaryArmyCombatStageSource(ArmyCampaignRuleAdapter package,
        IPlayableNativeArmyProgressStageSource remaining,
        INormalArmyForceResultDefinitionQueries forceDefinitions) : IPlayableNativeArmyProgressStageSource
    {
        public IPlayableNativeArmyProgressStageRules BeginOperation() =>
            new OrdinaryArmyCombatStages(package, remaining.BeginOperation(), forceDefinitions);
    }

    private sealed class OrdinaryArmyCombatStages(ArmyCampaignRuleAdapter package,
        IPlayableNativeArmyProgressStageRules remaining,
        INormalArmyForceResultDefinitionQueries forceDefinitions) : IPlayableNativeArmyProgressStageRules
    {
        private NormalArmyCombatCurrentPhase? currentPhase;
        public NativeArmyProgressStageResolution? AdvanceStage(GameState state, NativeArmyProgressStageMoment moment)
        {
            if (moment.Boundary != NativeArmyProgressStageBoundary.StageEntry)
                return remaining.AdvanceStage(state, moment);
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.PersonnelReturn)
            {
                var returned = package.TryAdvancePersonReturnCommands(state, moment.PreWriterThirdDate);
                return returned.State is { } continued
                    ? new(continued, "army.progress.personnel-return") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Combat && moment.PreWriterThirdDate.Hour == 10)
            {
                currentPhase = package.CaptureNormalArmyCombatPhase(state, moment);
                return currentPhase?.TryCompleteOrdinaryPhase(state, forceDefinitions) is { } completed
                    ? new(completed, "army.progress.ordinary-combat") : null;
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.Defeat)
            {
                var destroyed = new OwnedArmyDestructionStage(package).TryConsumePhase19(state,
                    currentPhase?.ReadOrdinaryCycleSampler(state), moment.PreWriterThirdDate);
                if (destroyed.State is not { } result) return null;
                var reports = destroyed.Receipts.Select(receipt => new NativeSubdayReport("field_destroyed",
                    result.NativeClock!.UpdateOrdinal, receipt.UnitId, receipt.ForceId, receipt.PhysicalSlot));
                result = result with { NativeSubdayReports = [.. result.NativeSubdayReports ?? [], .. reports] };
                return new(result, "army.progress.deferred-defeat");
            }
            if (moment.SchedulerStageIndex == (byte)NativeArmyProgressStage.ReleaseRetired)
            {
                currentPhase = null;
                return package.ConsumeArmyLifecyclePending(state) is { } reset
                    ? new(reset, "army.progress.release-retired") : null;
            }
            return remaining.AdvanceStage(state, moment);
        }
        public IReadOnlyList<int>? ReadCompleteWaitingArmyOrder(GameState state, NativeArmyProgressStageMoment moment) =>
            remaining.ReadCompleteWaitingArmyOrder(state, moment);
    }
}
