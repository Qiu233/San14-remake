using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2725E0's reached2EA480(Army,0,1,0) withdrawal. This operation
    /// keeps its caller's current/pool and preserves construction selection.
    /// Source-time player reports use their actual roots and operands.</summary>
    private GameState ApplyAutomaticArmyWithdrawal(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation, bool reportReason = true)
    {
        int physical = image.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("Army_receiver");
        var actor = new ArmyUnitReference(physical);
        if (!K(current.ReadArmyUnitNativeValidity(actor), "Army_validity")) return state;
        if (K(ArmySavedCommandKnowledge.ReadControl(state, image), "control140") != 0)
            throw Missing("stored_command");
        ushort commander = K(current.ReadArmyUnitCommanderId(actor), "Commander12");
        var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
            ?? throw Missing("Commander_lookup");
        ushort home = person.PhysicalSlot is int slot && K(current.ReadPersonNativeValidity(slot), "Commander_validity")
            ? K(current.ReadPersonFirstLocation(slot), "Commander_E2") : (ushort)0;
        byte kind = home is >= 52 and <= 61 ? (byte)6 : (byte)5;
        ushort identity = kind == 6 ? (ushort)(home - 51) : home <= 51 ? home : (ushort)0;
        byte action = kind == 6 ? (byte)12 : (byte)11;
        var receiver = state.ReadArmyLifecycle()?.Receivers.FindUnit(image.Id) ?? throw Missing("Army_owner");
        var march = image.March ?? throw Missing("March_owner");
        var runtime = image.NativeTacticRuntime ?? throw Missing("runtime_owner");
        var changed = image with
        {
            FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(action),
            Mission = new(UnitMissionKind.Retreat), MilitaryPlan = null,
            March = march with { Target = ArmyMarchDestination.None,
                FollowingWaypoints = new([]), NextStep = ArmyMarchDestination.None },
            OverrideTarget = runtime.Abnormal25 == 0 ? ArmyOverrideTargetState.Cleared : image.OverrideTarget
        };
        var working = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(kind, identity));
        current.SetArmyLifecycleTargetFields(physical, new(kind, identity));
        working = working with { Units = new(working.Units) { [image.Id] = changed },
            ResourceState = ArmyConfigurationSourceAdapter.WriteAction(working.ResourceState, image.Id, action) };
        if (runtime.Abnormal25 == 0)
        {
            current.ClearArmyCommandOverride(physical);
            var lifecycle = working.ReadArmyLifecycle()!;
            working = working.WithArmyLifecycle(lifecycle with
            { Receivers = lifecycle.Receivers.With(receiver with { OverrideTarget = changed.OverrideTarget }) });
            working = ArmyOverrideTargetSourceAdapter.ReconcileOwner(working, changed);
        }
        //The real call is301DD0(mode0,refresh1). No player-command submitter,
        //ChangedList append, new pool import, or constructor defaults are used.
        working = FRoute(working, image.Id, true, current, false);
        changed = working.Units[image.Id];
        if (changed.March?.NextStep == ArmyMarchDestination.None)
        {
            working = HoldArmyPositionAfterCommand(working, current, changed, operation);
            changed = working.Units[image.Id];
            ushort at = K(ArmyMarchSourceAdapter.ReadCurrentHex(working, changed), "holding_Hex2A");
            changed = changed with { Mission = new(UnitMissionKind.Combat), DestinationHexId = at,
                MilitaryPlan = new([], 0, new("stay", new(DeploymentTargetKind.Hex, at)), Completed: true, RestingHexId: at) };
            working = working with { Units = new(working.Units) { [image.Id] = changed } };
            return IsPlayerRulerLedDistrict(working, current, changed)
                ? AppendAutomaticWithdrawalReport(working, current, changed, operation, 0x3231, null) : working;
        }
        ushort goal = K(OrdinaryArmyRouteSource.ReadCurrentTarget(working, changed, current), "current_target");
        if (goal >= 48400) throw Missing("route_without_target");
        changed = changed with { DestinationHexId = goal };
        working = working with { Units = new(working.Units) { [image.Id] = changed } };
        //2A1980's choice1 calls the same2EA480 with param3=0. The
        //no-route3231 above is independent of that optional reason report.
        if (!reportReason) return working;
        //233880 tests the actual commander's District owner against World3A.
        //It has no ruler/leader or District-validity precondition.
        byte raw = ReadFacilityHistoryDistrict(current, actor);
        var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
            ?? throw Missing("report_District_lookup");
        if (!BaseCaptureRewardSource.ReadPlayerDistrict(current, district, Missing)) return working;
        string? reason = null;
        var reachedRuntime = changed.NativeTacticRuntime ?? throw Missing("report_runtime");
        if ((reachedRuntime.Flags26 & 2) == 0) reason = ReadHistoryDataText(1, 0x26A);
        else
        {
            _baseCapacityInputs.BindCurrent(working, current);
            var resources = current.ReadArmyReturnResourceFields(actor) ?? throw Missing("report_resources");
            int upper = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(actor), current);
            int morale = Math.Min(upper, K(resources.Morale1A, "report_morale1A"));
            if (morale < ProgressLowMorale)
                reason = ReadHistoryDataText(1, 0x225) + ReadHistoryDataText(1, 0x24D);
            else if (K(resources.Ready16, "report_troops16") < ProgressLowTroops)
                reason = ReadHistoryDataText(1, 0x20E) + ReadHistoryDataText(1, 0x24D);
        }
        return AppendAutomaticWithdrawalReport(working, current, changed, operation, 0x3232, reason);

        static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
        static InvalidDataException Missing(string owner) => MaintenanceMissing("automatic_retreat." + owner);
    }

    private GameState AppendAutomaticWithdrawalReport(GameState state, SourceWorldContext current,
        UnitState image, FacilityOperation operation, int root, string? reason)
    {
        var actor = new ArmyUnitReference(image.NativeArmyOrigin?.NativeSlotIndex
            ?? throw MaintenanceMissing("automatic_retreat.report_Army_receiver"));
        var formatted = FormatHistoryReport(state, current, root, new(Army0: actor, String0: reason));
        var working = formatted.State;
        int hex = MaintenanceKnown(current.ReadArmyUnitRawHex2A(actor), "automatic_retreat.report_Hex2A");
        int day = operation.ReadCampaignDay(working);
        int district = ReadFacilityHistoryDistrict(current, actor);
        var effects = new List<CampaignEffectRequest>();
        //2EA967 passes the generic reporter5012, bit0=0 and bit4=1.
        //This is history/presentation metadata, not a commander identity.
        working = WriteHistory(working, current, new ReportCommand(5, district, hex, day, formatted.Text,
            CampaignHistoryWriter.Supplemental(working), 5012, MarkBit4: 1), effects);
        return effects.Count == 0 ? working : working with
        {
            CampaignEffectRequests = (working.CampaignEffectRequests
                ?? throw MaintenanceMissing("automatic_retreat.report_effect_owner"))
                .Append(CampaignEffectSource.ArmyCommandMaintenance, operation.Revision, effects)
        };
    }
}
