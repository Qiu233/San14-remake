using San14.Core;

namespace San14.Data;

public enum SingleArmyFatalApplicationOutcome
{ CurrentInputsUnresolved, NativeTargetSkipped, Committed }
public sealed record SingleArmyFatalApplicationReceipt(int AttackerId, int TargetId,
    int ExternalRequestedLoss, NormalArmyApplicationBodyReceipt Body,
    NormalArmyForceResultApplication? ForceResult,
    OwnedArmyDestructionReceipt[] Destructions, int SurvivingAttackerTroops, string Revision);
public sealed record SingleArmyFatalApplicationResult(SingleArmyFatalApplicationOutcome Outcome,
    GameState State, SingleArmyFatalApplicationReceipt? Receipt, string? MissingOwner)
{
    public IReadOnlyList<PeriodReportEntry> Reports { get; init; } = [];
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public const string SingleFatalApplicationRevision = "ce-single-army-external178EC0-1745B0-19-26-v1";

    /// <summary>One actual type1/2 Army caster and target, with an external
    /// fatal request. This does not choose native pairs or authorize a dead
    /// caster's reverse request. All zero-ready intermediates stay private.</summary>
    public SingleArmyFatalApplicationResult TryApplyExternalSingleArmyDestruction(GameState input,
        int attackerId, int targetId, int requestedLoss, TacticSourceSamplerWorld? sampler = null,
        NativeMapSeasonDate? operationThirdDate = null)
    {
        if (!input.Units.TryGetValue(attackerId, out var attacker) ||
            !input.Units.TryGetValue(targetId, out var target) || attackerId == targetId ||
            !Supported(attacker) || !Supported(target) || requestedLoss < target.Troops)
            return new(SingleArmyFatalApplicationOutcome.CurrentInputsUnresolved, input, null,
                "branch.external_single_army_fatal_request");
        return ApplyExternalSingleArmyDestruction(input, attackerId, targetId, requestedLoss,
            sampler, operationThirdDate, "external_single_army", SingleFatalApplicationRevision);

        static bool Supported(UnitState unit) => unit.NativeTacticRuntime?.Status10 == 1 ||
            (unit.NativeTacticRuntime?.Status10 == 2 && unit.Mission?.Kind == UnitMissionKind.Transport);
    }

    private SingleArmyFatalApplicationResult ApplyExternalSingleArmyDestruction(GameState input,
        int attackerId, int targetId, int requestedLoss, TacticSourceSamplerWorld? sampler,
        NativeMapSeasonDate? operationThirdDate, string scope, string revision)
    {
        var application = TryApplyNativeNormalApplicationBody(input, attackerId, targetId, requestedLoss,
            state => CaptureCurrentDefinitions(state) is { } current ? Context(current) : null);
        if (application.Outcome == NormalArmyApplicationPrefixOutcome.NativeTargetSkipped && application.Receipt is { } skipped)
            return new(SingleArmyFatalApplicationOutcome.NativeTargetSkipped, application.State,
                new(attackerId, targetId, requestedLoss, skipped, null, [],
                    application.State.Units[attackerId].Troops, revision), null);
        if (application.Outcome != NormalArmyApplicationPrefixOutcome.ZeroTroopsCleanupRequired ||
            application.PendingCleanupState is not { } draft || application.Receipt is not { Killed: true,
                TroopDebit: { } debit } receipt || !draft.Units.TryGetValue(attackerId, out var attacker) ||
            draft.Units.GetValueOrDefault(targetId) is not { Troops: 0 } target)
            return Unknown(application.MissingPhase ?? $"current.{scope}_actual_zero_body");
        if (target.NativeTacticRuntime?.Status10 == 2 &&
            (receipt.TransportCargo is null || target.NativeReturnResources is not { Gold5C: 0, Supplies60: 0 }))
            return Unknown($"current.{scope}_actual_zero_transport_cargo");
        var definitions = CaptureCurrentDefinitions(draft);
        if (definitions is null) return Unknown($"current.{scope}_Force_definitions");
        //178EC0 reaches257670 with killed=true before1745B0. Dead targets
        //do not enter the nonfatal9-slot personality callback.
        var force = TryApplyNativeForceResult(draft, attacker.ForceId, target.ForceId, debit.AppliedLoss, true,
            new NormalArmyForceResultDefinitions(definitions));
        if (force.Outcome == NormalArmyForceResultOutcome.CurrentInputsUnresolved)
            return Unknown($"current.{scope}_killed_Force_result");
        draft = force.State;
        int physical = target.NativeArmyOrigin!.NativeSlotIndex!.Value;
        int winnerPhysical = attacker.NativeArmyOrigin!.NativeSlotIndex!.Value;
        definitions = CaptureCurrentDefinitions(draft);
        if (definitions is null) return Unknown($"current.{scope}_death_definitions");
        var admitted = OwnedArmyDestructionCurrent.AdmitOrdinaryArmy1745B0(draft, definitions,
            physical, winnerPhysical, null);
        if (!admitted.IsPending || admitted.State is not { } queued)
            return Unknown(admitted.UnresolvedOwner ?? $"current.{scope}_death_queue");
        var destroyed = new OwnedArmyDestructionStage(this).TryConsumePhase19(queued, sampler, operationThirdDate);
        if (destroyed.State is not { } after19 || after19.Units.ContainsKey(targetId) ||
            !destroyed.Receipts.Any(x => x.UnitId == targetId))
            return Unknown(destroyed.UnresolvedOwner ?? $"current.{scope}_actual19");
        var cleaned = ConsumeArmyLifecyclePending(after19);
        if (cleaned is null || !cleaned.Units.TryGetValue(attackerId, out var survivor))
            return Unknown($"current.{scope}_actual26_or_survivor");
        StateValidator.Validate(cleaned);
        return new(SingleArmyFatalApplicationOutcome.Committed, cleaned,
            new(attackerId, targetId, requestedLoss, receipt, force, destroyed.Receipts.ToArray(),
                survivor.Troops, revision), null) { Reports = destroyed.Reports };

        SingleArmyFatalApplicationResult Unknown(string? owner) =>
            new(SingleArmyFatalApplicationOutcome.CurrentInputsUnresolved, input, null, owner);
    }
}
