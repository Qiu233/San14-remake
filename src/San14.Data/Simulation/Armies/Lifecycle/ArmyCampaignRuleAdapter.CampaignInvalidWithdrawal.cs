using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //2EA480(Army,0,0,0), after the caller has established an invalid Army
    //header. The receiver can still be on manager60 with a pending image.
    private GameState ApplyInvalidCampaignArmyWithdrawalCurrent(GameState state,
        SourceWorldContext current, UnitState image, FacilityOperation operation)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex
            ?? throw CampaignRetreatMissing("invalid_withdraw_Army_receiver");
        var army = new ArmyUnitReference(physical);
        var runtime = image.NativeTacticRuntime
            ?? throw CampaignRetreatMissing("invalid_withdraw_runtime");

        //22A630 only supplies the destination for the validity-guarded37,
        //38/3A writes. Invalid headers retain action, target and all five
        //points, regardless of140. Clearing3C/3E is instead guarded only by25.
        bool clearOverride = runtime.Abnormal25 == 0;
        var changed = image with
        {
            OverrideTarget = clearOverride ? ArmyOverrideTargetState.Cleared : image.OverrideTarget,
            March = (image.March ?? throw CampaignRetreatMissing("invalid_withdraw_March")) with
                { NextStep = ArmyMarchDestination.None }
        };
        var arena = state.ReadArmyLifecycle() ?? throw CampaignRetreatMissing("invalid_withdraw_lifecycle");
        var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId)
            ?? throw CampaignRetreatMissing("invalid_withdraw_receiver_owner");
        if (clearOverride)
        {
            current.ClearArmyCommandOverride(physical);
            receiver = receiver with { OverrideTarget = ArmyOverrideTargetState.Cleared };
        }
        GameState working;
        if (receiver.ActiveUnitId == image.Id && state.Units.ContainsKey(image.Id))
            working = (state with { Units = new(state.Units) { [image.Id] = changed } })
                .WithArmyLifecycle(arena with { Receivers = arena.Receivers.With(receiver) });
        else
        {
            if (receiver.PendingImage?.Id != image.Id)
                throw CampaignRetreatMissing("invalid_withdraw_pending_image");
            working = state.WithArmyLifecycle(arena with
                { Receivers = arena.Receivers.With(receiver with { PendingImage = changed }) });
        }
        if (clearOverride) working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);

        //301DD0(mode0,refresh1) returns at its first32FB90 check, before
        //233560, goal selection, temporary allocation or any cached1F8 write.
        //Consequently the unconditional48=None above remains authoritative.
        byte status = CK(current.ReadArmyReturnUnitStatus10(army), "invalid_withdraw_Army10");
        if (status != 1)
            return StopTransportRoute(working, current, changed, operation, maintenanceCaller: false);

        //304A20 has the same validity guards on action/points/main target,
        //but reads2A and clears48 unconditionally. Its optional3D speech
        //consumer1B4AB0 independently rejects this invalid Army.
        _ = CK(current.ReadArmyUnitRawHex2A(army), "invalid_withdraw_hold_Army2A");
        return IsPlayerRulerLedDistrict(working, current, changed)
            ? AppendAutomaticWithdrawalReport(working, current, changed, operation, 0x3231, null)
            : working;
    }
}
