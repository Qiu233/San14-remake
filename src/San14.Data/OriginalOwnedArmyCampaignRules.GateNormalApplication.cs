using San14.Core;

namespace San14.Data;

public sealed record OriginalNormalGateApplicationReceipt(int GateId, int AttackerArmyId, int RequestedLoss,
    int AppliedLoss, OriginalPersonNormalArmyRewardResult AttackerReward,
    OriginalNormalCityCapacityWriteReceipt? TroopDebit, OriginalNormalCityWoundedReceipt? Wounded,
    OriginalNormalArmyCombatMoraleReceipt? MoraleLoss, OriginalNormalCityMoraleWriteReceipt? MoraleWrite,
    bool NativeReturn, string Revision);
public sealed record OriginalNormalGateApplicationResult(OriginalNormalCityApplicationOutcome Outcome,
    GameState State, OriginalNormalGateApplicationReceipt? Receipt, string? UnresolvedOwner);

public sealed partial class OriginalOwnedArmyCampaignRules
{
    /// <summary>Actual Army→Gate kind6 ordinary nonfatal178EC0 body. Gates
    /// retain signed3C/40 and byte44;17D260's adjacent distance is1. The shared
    /// Base body owns ordered reward, clamps and morale, never wall/capture.</summary>
    public OriginalNormalGateApplicationResult TryApplyNativeNormalGateApplication(GameState input,
        int attackerArmyId, int gateId, int requestedLoss,
        OriginalNativeArmyCombatCycleSampler? operationSampler = null)
    {
        var result = TryApplyNativeNormalBaseApplication(input, attackerArmyId, OriginalBaseKind.Gate,
            gateId, requestedLoss, operationSampler);
        var r = result.Receipt;
        return new(result.Outcome, result.State, r is null ? null : new(r.LocationId, r.AttackerArmyId,
            r.RequestedLoss, r.AppliedLoss, r.AttackerReward, r.TroopDebit, r.Wounded, r.MoraleLoss,
            r.MoraleWrite, r.NativeReturn, r.Revision), result.UnresolvedOwner);
    }
}
