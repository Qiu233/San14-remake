using San14.Core;

namespace San14.Data;

public enum TransportArmyFatalApplicationOutcome
{ CurrentInputsUnresolved, NativeTargetSkipped, Committed }
public sealed record TransportArmyFatalApplicationReceipt(int AttackerId, int TargetId,
    int ExternalRequestedLoss, NormalArmyApplicationBodyReceipt Body,
    NormalArmyForceResultApplication? ForceResult,
    OwnedArmyDestructionReceipt[] Destructions, int SurvivingAttackerTroops, string Revision);
public sealed record TransportArmyFatalApplicationResult(TransportArmyFatalApplicationOutcome Outcome,
    GameState State, TransportArmyFatalApplicationReceipt? Receipt, string? MissingOwner)
{
    public IReadOnlyList<PeriodReportEntry> Reports { get; init; } = [];
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public const string TransportFatalApplicationRevision = "ce-transport-external178EC0-1745B0-19-26-v1";

    /// <summary>One actual type1→type2 fatal external request, followed by
    /// source19 and26. Zero-ready/cargo/wounded/Force intermediate states stay
    /// private. The caller must not apply a dead transport's reverse request
    /// or keep the old prototype surviving quantities after this result.</summary>
    public TransportArmyFatalApplicationResult TryApplyExternalTransportArmyDestruction(GameState input,
        int attackerId, int targetId, int requestedLoss, TacticSourceSamplerWorld? sampler = null,
        NativeMapSeasonDate? operationThirdDate = null)
    {
        if (!input.Units.TryGetValue(attackerId, out var initialAttacker) ||
            !input.Units.TryGetValue(targetId, out var initialTarget) ||
            initialAttacker.NativeTacticRuntime?.Status10 != 1 || initialTarget.NativeTacticRuntime?.Status10 != 2 ||
            initialTarget.Mission?.Kind != UnitMissionKind.Transport || requestedLoss < initialTarget.Troops)
            return new(TransportArmyFatalApplicationOutcome.CurrentInputsUnresolved, input, null,
                "branch.external_transport_single_fatal_request");
        var result = ApplyExternalSingleArmyDestruction(input, attackerId, targetId, requestedLoss,
            sampler, operationThirdDate, "external_transport", TransportFatalApplicationRevision);
        var receipt = result.Receipt;
        return new(result.Outcome switch
        {
            SingleArmyFatalApplicationOutcome.Committed => TransportArmyFatalApplicationOutcome.Committed,
            SingleArmyFatalApplicationOutcome.NativeTargetSkipped => TransportArmyFatalApplicationOutcome.NativeTargetSkipped,
            _ => TransportArmyFatalApplicationOutcome.CurrentInputsUnresolved
        }, result.State, receipt is null ? null : new(receipt.AttackerId, receipt.TargetId, receipt.ExternalRequestedLoss,
            receipt.Body, receipt.ForceResult, receipt.Destructions, receipt.SurvivingAttackerTroops, receipt.Revision),
            result.MissingOwner) { Reports = result.Reports };
    }
}
