using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Owns the ordinary Return manager0, finite Army stage13 and real19/26
    /// consumers. The caller still supplies every remaining reached stage;
    /// this adapter does not claim Base/Gate/transport or whole-world coverage.</summary>
    public IPlayableNativeArmyProgressStageRules CreateOrdinaryArmyCombatProgressStages(
        IPlayableNativeArmyProgressStageRules remainingStages,
        INormalArmyForceResultDefinitionQueries forceDefinitions)
    {
        ArgumentNullException.ThrowIfNull(remainingStages); ArgumentNullException.ThrowIfNull(forceDefinitions);
        return new OrdinaryArmyCombatStages(this, remainingStages, forceDefinitions);
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
            if (moment.SchedulerStageIndex == 0)
            {
                var returned = package.TryAdvancePersonReturnCommands(state, moment.PreWriterThirdDate);
                return returned.State is { } continued
                    ? new(continued, "native202360-owned-Return-manager8_22-1DC380-1EC8C0") : null;
            }
            if (moment.SchedulerStageIndex == 13 && moment.PreWriterThirdDate.Hour == 10)
            {
                currentPhase = package.CaptureNormalArmyCombatPhase(state, moment);
                return currentPhase?.TryCompleteOrdinaryPhase(state, forceDefinitions) is { } completed
                    ? new(completed, "native169CE0-ordinary-Army-numeric-stage13-deferred1745B0") : null;
            }
            if (moment.SchedulerStageIndex == 19)
            {
                var destroyed = new OwnedArmyDestructionStage(package).TryConsumePhase19(state,
                    currentPhase?.ReadOrdinaryCycleSampler(state), moment.PreWriterThirdDate);
                if (destroyed.State is not { } result) return null;
                var reports = destroyed.Receipts.Select(receipt => new NativeSubdayReport("field_destroyed",
                    result.NativeClock!.UpdateOrdinal, receipt.UnitId, receipt.ForceId, receipt.PhysicalSlot));
                result = result with { NativeSubdayReports = [.. result.NativeSubdayReports ?? [], .. reports] };
                return new(result, "native450830-deferred-Army259360-owned-business");
            }
            if (moment.SchedulerStageIndex == 26)
            {
                currentPhase = null;
                return package.ConsumeArmyLifecyclePending(state) is { } reset
                    ? new(reset, "native28E6D0-pending70-2FFA20-owned-header-reset") : null;
            }
            return remaining.AdvanceStage(state, moment);
        }
        public IReadOnlyList<int>? ReadCompleteWaitingArmyOrder(GameState state, NativeArmyProgressStageMoment moment) =>
            remaining.ReadCompleteWaitingArmyOrder(state, moment);
    }
}
