using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState MaintainFalseReportArmyCommand(GameState state, SourceWorldContext current,
        UnitState image, ArmyReceiverState receiver, FacilityOperation operation, int force, ushort at)
    {
        var target = OwnedArmyLifecycleTargets.Read(state, receiver.ReceiverId)
            ?? throw MaintenanceMissing("false_report_target");
        if (target is not { Kind38: 27, Identity3A: ushort raw })
            throw MaintenanceMissing("false_report_parent_Army");
        var parent = current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0)
            ?? throw MaintenanceMissing("false_report_parent_pointer");
        int physical = MaintenanceKnown(parent.PhysicalSlot, "false_report_parent_identity");
        var owner = state.ReadArmyLifecycle()!.ReceiverSlots()[physical];
        var parentImage = owner.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : owner.PendingImage;
        var runtime = image.NativeTacticRuntime ?? throw MaintenanceMissing("false_report_runtime");
        //The main27 retreat prefix precedes the action17 switch and does not
        //test the target's native validity. C35E is separate from2A11A0(0).
        if ((runtime.Flags26 & 8) == 0 &&
            MaintenanceKnown(ArmyOverrideTargetSourceAdapter.Read(state, image).Kind3C, "false_report_override3C") == 255 &&
            MaintenanceKnown(ArmyConfigurationSourceAdapter.ReadAction(state, parentImage), "false_report_parent_action37") is 11 or 12)
        {
            var stopped = IsPlayerRulerLedDistrict(state, current, image)
                ? AddMaintenanceReport(state, current, image, operation, 0xC35E, parent) : state;
            return FinishNoPathArmyCommand(stopped, current, image, operation);
        }
        //Actual2725E0 case17: a still-valid purpose Object, raw main3A in
        //1..500, target control0, enemy relation and at most two Area links
        //retain this command. The purpose need not still be our active Trap.
        int parentRaw = MaintenanceKnown(current.ReadArmyUnitRawHex2A(parent), "false_report_parent_position");
        var parentHex = current.ReadHexTableReference(parentRaw < 48400 ? parentRaw : 0)
            ?? throw MaintenanceMissing("false_report_parent_Hex");
        ushort purpose = ArmyMarchSourceAdapter.EncodeDestination(receiver.FalseReportDestination)
            ?? throw MaintenanceMissing("false_report_purpose");
        var purposeHex = current.ReadHexTableReference(purpose < 48400 ? purpose : 0)
            ?? throw MaintenanceMissing("false_report_purpose_Hex");
        var obj = FacilityConstructionLegality.ObjectAt(current, purposeHex);
        if (MaintenanceKnown(current.ReadObjectNativeValidity(obj), "false_report_purpose_validity") && raw is >= 1 and <= 500)
        {
            byte control = owner.SavedCommand?.Control ?? parentImage?.NativeCommandControl140
                ?? owner.CommandControl140 ?? throw MaintenanceMissing("false_report_parent_control140");
            if (control == 0 && MaintenanceKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
                current.ReadArmyUnitRawForceSlot(new(image.NativeArmyOrigin!.NativeSlotIndex)),
                current.ReadArmyUnitRawForceSlot(parent)), "false_report_parent_relation"))
            {
                int start = MaintenanceKnown(current.ReadHexAreaSlot(parentHex), "false_report_parent_Area_link");
                int end = MaintenanceKnown(current.ReadHexAreaSlot(purposeHex), "false_report_purpose_Area_link");
                var from = current.ReadAreaTableReference(start <= 500 ? start : 0)
                    ?? throw MaintenanceMissing("false_report_parent_Area_pointer");
                var to = current.ReadAreaTableReference(end <= 500 ? end : 0)
                    ?? throw MaintenanceMissing("false_report_purpose_Area_pointer");
                if (from == to) return ContinueRoute();
                var neighbors = ReadFalseReportAreaNeighbors(current, from);
                if (neighbors.Contains(to)) return ContinueRoute();
                foreach (var next in neighbors)
                    if (ReadFalseReportAreaNeighbors(current, next).Contains(to)) return ContinueRoute();
            }
        }
        return FinishInvalidArmyCommand(state, current, image, operation);

        //The source's matching branch breaks the action switch, then still
        //executes the ordinary Next48/waypoint/no-route suffix.
        GameState ContinueRoute() => CompleteCommandRouteTail(state, current, image, operation, 17, force, at);
    }
}
