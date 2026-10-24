using San14.Core;

namespace San14.Data;

public sealed record NormalGateApplicationReceipt(int GateId, int AttackerArmyId, int RequestedLoss,
    int AppliedLoss, PersonNormalArmyRewardResult AttackerReward,
    NormalCityCapacityWriteReceipt? TroopDebit, NormalCityWoundedReceipt? Wounded,
    NormalArmyCombatMoraleReceipt? MoraleLoss, NormalCityMoraleWriteReceipt? MoraleWrite,
    bool NativeReturn, string Revision);
public sealed record NormalGateApplicationResult(NormalCityApplicationOutcome Outcome,
    GameState State, NormalGateApplicationReceipt? Receipt, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Actual Army→Gate kind6 ordinary nonfatal178EC0 body. Gates
    /// retain signed3C/40 and byte44;17D260's adjacent distance is1. The shared
    /// Base body owns ordered reward, clamps and morale, never wall/capture.</summary>
    public NormalGateApplicationResult TryApplyNativeNormalGateApplication(GameState input,
        int attackerArmyId, int gateId, int requestedLoss,
        NativeArmyCombatCycleSampler? operationSampler = null)
    {
        var result = TryApplyNativeNormalBaseApplication(input, attackerArmyId, BaseKind.Gate,
            gateId, requestedLoss, operationSampler);
        var r = result.Receipt;
        return new(result.Outcome, result.State, r is null ? null : new(r.LocationId, r.AttackerArmyId,
            r.RequestedLoss, r.AppliedLoss, r.AttackerReward, r.TroopDebit, r.Wounded, r.MoraleLoss,
            r.MoraleWrite, r.NativeReturn, r.Revision), result.UnresolvedOwner);
    }

    /// <summary>Borrow the enclosing ordinary operation's current and pool;
    /// unresolved inputs discard that entire operation, not only this body.</summary>
    internal NormalGateApplicationResult ApplyBorrowedNormalGate(GameState input,
        int attackerArmyId, int gateId, int requestedLoss,
        NativeArmyCombatCycleSampler? operationSampler, SourceWorldContext current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var result = TryApplyNativeNormalBaseApplication(input, attackerArmyId, BaseKind.Gate,
            gateId, requestedLoss, operationSampler, operationCurrent: current);
        var r = result.Receipt;
        return new(result.Outcome, result.State, r is null ? null : new(r.LocationId, r.AttackerArmyId,
            r.RequestedLoss, r.AppliedLoss, r.AttackerReward, r.TroopDebit, r.Wounded, r.MoraleLoss,
            r.MoraleWrite, r.NativeReturn, r.Revision), result.UnresolvedOwner);
    }
}
