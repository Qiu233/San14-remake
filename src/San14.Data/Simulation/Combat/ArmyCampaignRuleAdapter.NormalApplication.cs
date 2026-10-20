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
        => TryApplyNativeDamageTargetBody(input, attackerId, targetId, requestedLoss, captureCurrent);

    //Field-object origin has a proved null Army/Base source. Only that caller
    //supplies its writable operation current and real zero-morale continuation.
    private NormalArmyApplicationBodyResult TryApplyNativeDamageTargetBody(GameState input,
        int? attackerId, int targetId, int requestedLoss,
        Func<GameState, TacticRemakeCurrentContext?> captureCurrent,
        SourceWorldContext? fieldObjectCurrent = null,
        Func<GameState, UnitState, int, (GameState State, ArmyMoraleAdjustmentReceipt Receipt)>? objectMorale = null,
        Func<TacticSourceSamplerWorld>? objectSampler = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        GameState working = fieldObjectCurrent is null ? GameStateCopy.Copy(input) : input;
        UnitState? attacker = null;
        if (attackerId is int actor)
        {
            if (!working.Units.TryGetValue(actor, out attacker) || !working.Units.ContainsKey(targetId) ||
                captureCurrent(working) is not { } current || current.ArmyValid(working, attacker) != true)
                return Unknown("attacker");
        }
        else if (fieldObjectCurrent is null || !working.Units.ContainsKey(targetId) || captureCurrent(working) is null)
            return Unknown("known-null-source");
        //178CD5..178D19: amount is the uncapped ordinary int32 request, kind0.
        PersonNormalArmyRewardResult reward = requestedLoss > 0 && attacker is not null
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
        var debit = fieldObjectCurrent is null
            ? NormalArmyCombatApplicationSourceMath.ApplyTroopDebit((ushort)target.Troops, requestedLoss)
            : NormalArmyCombatApplicationSourceMath.ApplyEnvironmentTroopDebit((ushort)target.Troops, requestedLoss);
        TransportArmyNormalCargoReceipt? transportCargo = null;
        if (transport)
        {
            var cargoCurrent = fieldObjectCurrent ?? CaptureCurrentDefinitions(working);
            if (cargoCurrent is null) return Unknown("transport-cargo-current");
            var cargo = fieldObjectCurrent is not null
                ? TransportArmyNormalCargoSource.ApplyFieldObject(working, cargoCurrent, target, debit.AppliedLoss)
                : TransportArmyNormalCargoSource.Apply(working, cargoCurrent, working.Units[attackerId!.Value], target, debit.AppliedLoss);
            if (cargo.State is not { } cargoState || cargo.Receipt is not { } cargoReceipt)
                return Unknown($"transport-cargo:{cargo.UnresolvedOwner}");
            working = cargoState; target = working.Units[targetId]; transportCargo = cargoReceipt;
        }
        target = target with { Troops = debit.Troops16After };
        working = Replace(working, target);
        SyncObjectNumbers(target);
        var parameters = fieldObjectCurrent is null
            ? NormalArmyCombatApplicationSourceMath.ReadCurrent(working, _normalApplicationSeed) : null;
        int? environmentCC = fieldObjectCurrent is null ? null :
            BattleRemakeCurrentSettings.ReadInt32(working, 0xCC)
                ?? throw TrapParameters.Missing("damage_wounded_CC");
        if (fieldObjectCurrent is null && parameters is null ||
            BattleRemakeCurrentSettings.ReadSelectorE58(working) is not int selector)
            return Unknown("application-parameters-or-settings");
        int settingsCC = environmentCC ?? 0; //Ordinary legacy unused formal remains unchanged.
        if (fieldObjectCurrent is null && selector != 0)
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
                woundedCurrent.ResolveNormalWoundedRatioInput(working, attacker is null ? null : working.Units[attacker.Id], target,
                    selector, difficulty) is not { } resolved ||
                (fieldObjectCurrent is null
                    ? NormalArmyCombatApplicationSourceMath.CalculateWoundedRatio(parameters!.CurrentValues, resolved)
                    : NormalArmyCombatApplicationSourceMath.CalculateCurrentWoundedRatio(resolved, ReadEnvironmentParameter)) is not float value)
                return Unknown("wounded-ratio");
            woundedInput = resolved; ratio = value;
        }
        var wounds = fieldObjectCurrent is null
            ? NormalArmyCombatApplicationSourceMath.ApplyWoundedChannel(parameters!.CurrentValues,
                (ushort)target.WoundedTroops!.Value, debit.AppliedLoss, ratio, selector, settingsCC, advanced)
            : NormalArmyCombatApplicationSourceMath.ApplyEnvironmentWoundedChannel(ReadEnvironmentParameter,
                (ushort)target.WoundedTroops!.Value, debit.AppliedLoss, ratio, selector, settingsCC, advanced,
                ReadEnvironmentSubtractionGate);
        if (wounds is null) return Unknown("wounded-writes");
        target = target with { WoundedTroops = wounds.Wounded18After };
        working = Replace(working, target);
        SyncObjectNumbers(target);
        if (fieldObjectCurrent is not null)
            fieldObjectCurrent.WriteArmyReturnWounded(target.NativeArmyOrigin!.NativeSlotIndex!.Value,
                checked((ushort)target.WoundedTroops!.Value));
        NormalArmyCombatMoraleReceipt? moraleLoss = null;
        ArmyMoraleAdjustmentReceipt? moraleWrite = null;
        //179186 skips this whole morale getter/calculation/callback for type2.
        if (!transport)
        {
            if (captureCurrent(working) is not { } moraleCurrent || ArmyMoraleSourceAdapter.Read(target) is not byte rawMorale ||
                moraleCurrent.ForceMoraleMaximum(working, target.ForceId) is not int moraleCap)
                return Unknown("morale-getter");
            if (Math.Min((int)rawMorale, moraleCap) > 0)
            {
                if (moraleCurrent.ReadNormalApplicationLeadership(working, target, fieldObjectCurrent is not null) is not int leadership)
                    return Unknown("morale-leadership-or-cycle-sampler");
                if (fieldObjectCurrent is null)
                {
                    if (moraleCurrent.ReadCurrentSamplerWorld(working) is not { } sampler)
                        return Unknown("morale-leadership-or-cycle-sampler");
                    moraleLoss = NormalArmyCombatApplicationSourceMath.CalculateMoraleLoss(parameters!.CurrentValues,
                        wounds.Wounded18After, debit.Troops16After, leadership, debit.AppliedLoss, sampler);
                }
                else moraleLoss = NormalArmyCombatApplicationSourceMath.CalculateEnvironmentMoraleLoss(ReadEnvironmentParameter,
                    wounds.Wounded18After, debit.Troops16After, leadership, debit.AppliedLoss,
                    () => objectSampler?.Invoke() ?? throw TrapParameters.Missing("damage_current_sampler"));
                if (moraleLoss.Loss > 0)
                {
                    if (fieldObjectCurrent is not null)
                    {
                        if (objectMorale is null) return Unknown("object-morale-continuation");
                        var adjusted = objectMorale(working, target, moraleLoss.NativeAdjustment);
                        working = adjusted.State; moraleWrite = adjusted.Receipt;
                    }
                    else
                    {
                        var adjustment = ApplyMoraleDuringArmyOperation(working, target.Id, moraleLoss.NativeAdjustment);
                        if (adjustment.Outcome != ArmyMoraleAdjustmentOutcome.Committed)
                            return Unknown($"morale-callback:{adjustment.Outcome}");
                        working = adjustment.State; moraleWrite = adjustment.Receipt;
                    }
                }
            }
        }
        //1791C3 reads2337C0 again after the actual2706A0 callback. The killed
        //flag belongs to that later read and live word16, not the old cap.
        if (captureCurrent(working) is not { } finalCurrent ||
            (fieldObjectCurrent is null ? finalCurrent.ArmyManagerExcluded(working, target.Id) :
                OwnedArmyDestructionCurrent.ReadArmyExcluded233560(working, fieldObjectCurrent,
                    target.NativeArmyOrigin!.NativeSlotIndex!.Value)) is not bool excludedAfter)
            return Unknown("target-manager-after-morale");
        var finalTarget = working.Units.GetValueOrDefault(target.Id);
        if (finalTarget is null && (fieldObjectCurrent is null || !excludedAfter)) return Unknown("target-image-after-morale");
        bool killed = !excludedAfter && finalTarget!.Troops == 0;
        var receipt = new NormalArmyApplicationBodyReceipt(requestedLoss, reward, debit,
            woundedInput, ratio, wounds, moraleLoss, moraleWrite, excludedAfter, killed,
            $"native-normal-178C20-prefix:{Revision}:{input.NativeClock!.UpdateOrdinal}") { TransportCargo = transportCargo };
        //A zero-troop Army cannot escape this prefix as a saved live Unit.
        //174310 death/capture/removal still needs its real producer.
        if (finalTarget?.Troops == 0)
        {
            return new(NormalArmyApplicationPrefixOutcome.ZeroTroopsCleanupRequired, input, receipt,
                "174310-zero-troops-cleanup") { PendingCleanupState = working };
        }
        return new(NormalArmyApplicationPrefixOutcome.AwaitingForceAndAftermath, working, receipt, null);

        int ReadEnvironmentParameter(string key) =>
            NormalArmyCombatApplicationSourceMath.ReadCurrentParameter(working, _normalApplicationSeed, key);
        bool ReadEnvironmentSubtractionGate()
        {
            int e58 = BattleRemakeCurrentSettings.ReadSelectorE58(working)
                ?? throw TrapParameters.Missing("damage_second_wounded_E58");
            //The native first CC getter is cached across the two E58 gates.
            if (e58 != 0 && settingsCC == 0) return true;
            bool enabled = BattleRemakeCurrentSettings.ReadAdvancedSettings(working)
                ?? throw TrapParameters.Missing("damage_second_advanced_enabled");
            return enabled && (BattleRemakeCurrentSettings.ReadAdvancedParameter(working, 7)
                ?? throw TrapParameters.Missing("damage_second_advanced7")) > 0;
        }
        void SyncObjectNumbers(UnitState image)
        {
            if (fieldObjectCurrent is null) return;
            int physical = image.NativeArmyOrigin!.NativeSlotIndex!.Value;
            var fields = fieldObjectCurrent.ReadArmyReturnResourceFields(new(physical))
                ?? throw TrapParameters.Missing("damage_current_resource_fields");
            fieldObjectCurrent.BindArmyReturnResourceFields(physical, fields with
                { Ready16 = checked((ushort)image.Troops), Wounded18 = checked((ushort)image.WoundedTroops!.Value) });
        }

        NormalArmyApplicationBodyResult Unknown(string phase) =>
            new(NormalArmyApplicationPrefixOutcome.CurrentInputsUnresolved, input, null, phase);
        NormalArmyApplicationBodyResult TargetSkip() => new(
            NormalArmyApplicationPrefixOutcome.NativeTargetSkipped, working,
            new(requestedLoss, reward, null, null, null, null, null, null, null, null, Revision), null);
    }

    private static GameState Replace(GameState state, UnitState target) =>
        state with { Units = new(state.Units) { [target.Id] = target } };
}
