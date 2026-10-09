using San14.Core;

namespace San14.Data;

public enum NormalArmyApplicationPrefixOutcome
{
    CurrentInputsUnresolved, NativeTargetSkipped, AwaitingForceAndAftermath, ZeroTroopsCleanupRequired
}

/// <summary>Actual178C20 writes through1791DB, before the Force callback.
/// These quantities are not a complete application/death/report receipt.</summary>
public sealed record NormalArmyApplicationPrefixReceipt(
    NormalArmyCombatRequestedLossReceipt Request,
    PersonNormalArmyRewardResult AttackerReward,
    NormalArmyCombatTroopDebitReceipt? TroopDebit,
    NormalArmyCombatWoundedRatioInput? WoundedInput,
    float? WoundedRatio, NormalArmyCombatWoundedReceipt? Wounded,
    NormalArmyCombatMoraleReceipt? MoraleLoss,
    ArmyMoraleAdjustmentReceipt? MoraleAdjustment,
    bool? ExcludedAfterAdjustment, bool? Killed, string Revision)
{
    public TransportArmyNormalCargoReceipt? TransportCargo { get; init; }
}

public sealed record NormalArmyApplicationPrefixResult(
    NormalArmyApplicationPrefixOutcome Outcome, GameState State,
    NormalArmyApplicationPrefixReceipt? Receipt, string? MissingPhase)
{
    // Private operation continuation, never a successful saved/world result.
    // TC1745B0 enqueues; stages19/26 perform the actual later destruction.
    internal GameState? PendingCleanupState { get; init; }
}

/// <summary>Reached178EC0 quantity writes, independent of the caller's
/// CAttackUtil frame. A period request is never labelled frame20 or22.</summary>
public sealed record NormalArmyApplicationBodyReceipt(
    int RequestedLoss, PersonNormalArmyRewardResult AttackerReward,
    NormalArmyCombatTroopDebitReceipt? TroopDebit,
    NormalArmyCombatWoundedRatioInput? WoundedInput,
    float? WoundedRatio, NormalArmyCombatWoundedReceipt? Wounded,
    NormalArmyCombatMoraleReceipt? MoraleLoss,
    ArmyMoraleAdjustmentReceipt? MoraleAdjustment,
    bool? ExcludedAfterAdjustment, bool? Killed, string Revision)
{
    public TransportArmyNormalCargoReceipt? TransportCargo { get; init; }
}

internal sealed record NormalArmyApplicationBodyResult(
    NormalArmyApplicationPrefixOutcome Outcome, GameState State,
    NormalArmyApplicationBodyReceipt? Receipt, string? MissingPhase)
{
    internal GameState? PendingCleanupState { get; init; }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One ordinary Army→Army application prefix. The
    /// source commander reward precedes target cap; every other read uses the
    /// draft after earlier actual writes. Unknown callbacks publish nothing.
    /// The caller still owns Force/personality/report/cleanup and174C20.</summary>
    internal NormalArmyApplicationPrefixResult TryApplyNativeNormalApplicationPrefix(GameState input,
        NormalArmyCombatPair pair, NormalArmyCombatRequestedLossReceipt request,
        Func<GameState, TacticRemakeCurrentContext?> captureCurrent)
    {
        var body = TryApplyNativeNormalApplicationBody(input, pair.Attacker.ArmyId, pair.Target.ArmyId,
            request.RequestedLoss, captureCurrent);
        var r = body.Receipt;
        return new(body.Outcome, body.State, r is null ? null : new(request, r.AttackerReward,
            r.TroopDebit, r.WoundedInput, r.WoundedRatio, r.Wounded, r.MoraleLoss,
            r.MoraleAdjustment, r.ExcludedAfterAdjustment, r.Killed, r.Revision) { TransportCargo = r.TransportCargo }, body.MissingPhase)
            { PendingCleanupState = body.PendingCleanupState };
    }

    //The phase wrapper retains its actual frame receipt. The normal period
    //wrapper supplies only its external gross request to this same body.
    internal NormalArmyApplicationBodyResult TryApplyNativeNormalApplicationBody(GameState input,
        int attackerId, int targetId, int requestedLoss,
        Func<GameState, TacticRemakeCurrentContext?> captureCurrent)
    {
        ArgumentNullException.ThrowIfNull(input);
        GameState working = GameStateCopy.Copy(input);
        if (!working.Units.TryGetValue(attackerId, out UnitState? attacker) ||
            !working.Units.ContainsKey(targetId) || captureCurrent(working) is not { } current ||
            current.ArmyValid(working, attacker) != true)
            return Unknown("attacker");
        //178CD5..178D19: amount is the uncapped ordinary int32 request, kind0.
        PersonNormalArmyRewardResult reward = requestedLoss > 0
            ? TryApplyNativeNormalPersonRewardDuringArmyOperation(working, attacker.OfficerId, 0, requestedLoss)
            : new(PersonNormalArmyRewardOutcome.NativeSkip, working, null, null);
        if (reward.Outcome == PersonNormalArmyRewardOutcome.CurrentInputsUnresolved)
            return Unknown($"attacker-reward:{reward.MissingPhase}");
        working = reward.State;
        if (captureCurrent(working) is not { } afterReward ||
            !working.Units.TryGetValue(targetId, out UnitState? target) ||
            afterReward.ArmyValid(working, target) is not bool targetValid)
            return Unknown("target-validity");
        if (!targetValid) return TargetSkip();
        if (afterReward.ArmyManagerExcluded(working, target.Id) is not bool excluded)
            return Unknown("target-manager");
        if (excluded) return TargetSkip();
        bool transport = target.NativeTacticRuntime!.Status10 == 2;
        if (target.Troops is < 0 or > ushort.MaxValue || target.WoundedTroops is not >= 0 or > ushort.MaxValue)
            return Unknown("army-words16-18");
        var debit = NormalArmyCombatApplicationSourceMath.ApplyTroopDebit((ushort)target.Troops, requestedLoss);
        TransportArmyNormalCargoReceipt? transportCargo = null;
        if (transport)
        {
            var cargoCurrent = CaptureCurrentDefinitions(working);
            if (cargoCurrent is null) return Unknown("transport-cargo-current");
            var cargo = TransportArmyNormalCargoSource.Apply(working, cargoCurrent,
                working.Units[attackerId], target, debit.AppliedLoss);
            if (cargo.State is not { } cargoState || cargo.Receipt is not { } cargoReceipt)
                return Unknown($"transport-cargo:{cargo.UnresolvedOwner}");
            working = cargoState; target = working.Units[targetId]; transportCargo = cargoReceipt;
        }
        target = target with { Troops = debit.Troops16After };
        working = Replace(working, target);
        if (NormalArmyCombatApplicationSourceMath.ReadCurrent(working, _normalApplicationSeed) is not { } parameters ||
            BattleRemakeCurrentSettings.ReadSelectorE58(working) is not int selector)
            return Unknown("application-parameters-or-settings");
        int settingsCC = 0; //Unused by the advanced helper; no current0 inferred.
        if (selector != 0)
        {
            if (BattleRemakeCurrentSettings.ReadInt32(working, 0xCC) is not int cc)
                return Unknown("application-parameters-or-settings");
            settingsCC = cc;
        }
        int? advanced = selector == 0 ? BattleRemakeCurrentSettings.ReadAdvancedParameter(working, 7) : null;
        if (selector == 0 && advanced is null) return Unknown("advanced-wounded-percent7");
        bool channel = selector != 0 ? settingsCC == 0 : advanced > 0;
        NormalArmyCombatWoundedRatioInput? woundedInput = null;
        float? ratio = null;
        if (channel)
        {
            int difficulty = 0; //17CFD0 advanced mode has no difficulty term.
            if (selector != 0)
            {
                if (BattleRemakeCurrentSettings.ReadInt32(working, 0x28) is not int currentDifficulty)
                    return Unknown("wounded-difficulty");
                difficulty = currentDifficulty;
            }
            if (captureCurrent(working) is not { } woundedCurrent ||
                woundedCurrent.ResolveNormalWoundedRatioInput(working, working.Units[attacker.Id], target,
                    selector, difficulty) is not { } resolved ||
                NormalArmyCombatApplicationSourceMath.CalculateWoundedRatio(parameters.CurrentValues, resolved) is not float value)
                return Unknown("wounded-ratio");
            woundedInput = resolved; ratio = value;
        }
        var wounds = NormalArmyCombatApplicationSourceMath.ApplyWoundedChannel(parameters.CurrentValues,
            (ushort)target.WoundedTroops!.Value, debit.AppliedLoss, ratio, selector, settingsCC, advanced);
        if (wounds is null) return Unknown("wounded-writes");
        target = target with { WoundedTroops = wounds.Wounded18After };
        working = Replace(working, target);
        NormalArmyCombatMoraleReceipt? moraleLoss = null;
        ArmyMoraleAdjustmentReceipt? moraleWrite = null;
        //179186 skips this whole morale getter/calculation/callback for type2.
        if (!transport)
        {
            if (captureCurrent(working) is not { } moraleCurrent || target.NativeMorale1A is not byte rawMorale ||
                moraleCurrent.ForceMoraleMaximum(working, target.ForceId) is not int moraleCap)
                return Unknown("morale-getter");
            if (Math.Min((int)rawMorale, moraleCap) > 0)
            {
                if (moraleCurrent.ReadNormalApplicationLeadership(working, target) is not int leadership ||
                    moraleCurrent.ReadCurrentSamplerWorld(working) is not { } sampler)
                    return Unknown("morale-leadership-or-cycle-sampler");
                moraleLoss = NormalArmyCombatApplicationSourceMath.CalculateMoraleLoss(parameters.CurrentValues,
                    wounds.Wounded18After, debit.Troops16After, leadership, debit.AppliedLoss, sampler);
                if (moraleLoss.Loss > 0)
                {
                    var adjustment = ApplyMoraleDuringArmyOperation(working, target.Id, moraleLoss.NativeAdjustment);
                    if (adjustment.Outcome != ArmyMoraleAdjustmentOutcome.Committed)
                        return Unknown($"morale-callback:{adjustment.Outcome}");
                    working = adjustment.State; moraleWrite = adjustment.Receipt;
                }
            }
        }
        //1791C3 reads2337C0 again after the actual2706A0 callback. The killed
        //flag belongs to that later read and live word16, not the old cap.
        if (captureCurrent(working) is not { } finalCurrent ||
            finalCurrent.ArmyManagerExcluded(working, target.Id) is not bool excludedAfter)
            return Unknown("target-manager-after-morale");
        bool killed = !excludedAfter && working.Units[target.Id].Troops == 0;
        var receipt = new NormalArmyApplicationBodyReceipt(requestedLoss, reward, debit,
            woundedInput, ratio, wounds, moraleLoss, moraleWrite, excludedAfter, killed,
            $"native-normal-178C20-prefix:{Revision}:{input.NativeClock!.UpdateOrdinal}") { TransportCargo = transportCargo };
        //A zero-troop Army cannot escape this prefix as a saved live Unit.
        //174310 death/capture/removal still needs its real producer.
        if (working.Units[target.Id].Troops == 0)
        {
            return new(NormalArmyApplicationPrefixOutcome.ZeroTroopsCleanupRequired, input, receipt,
                "174310-zero-troops-cleanup") { PendingCleanupState = working };
        }
        return new(NormalArmyApplicationPrefixOutcome.AwaitingForceAndAftermath, working, receipt, null);

        NormalArmyApplicationBodyResult Unknown(string phase) =>
            new(NormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved, input, null, phase);
        NormalArmyApplicationBodyResult TargetSkip() => new(
            NormalArmyApplicationPrefixOutcome.NativeTargetSkipped, working,
            new(requestedLoss, reward, null, null, null, null, null, null, null, null, Revision), null);
    }

    private static GameState Replace(GameState state, UnitState target) =>
        state with { Units = new(state.Units) { [target.Id] = target } };
}
