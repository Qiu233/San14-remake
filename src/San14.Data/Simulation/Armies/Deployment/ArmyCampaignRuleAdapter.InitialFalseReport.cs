using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static CommandResult? OCReadInitialFalseReportTarget(GameState state, SourceWorldContext current,
        int commanderPhysical, DeploymentAction action, out ArmyCommandTarget? result)
    {
        result = null;
        if (action.Target.Kind != DeploymentTargetKind.Unit || !state.Units.TryGetValue(action.Target.Id, out var target))
            return CommandResult.Reject("The false-report target Army is unavailable.", "deployment.false_report_target");
        if (action.FalseReportDestinationHexId is not (>= 0 and < 48400))
            throw OCMissing("initial_false_report_destination");
        var own = current.ReadPersonForce(commanderPhysical) ?? throw OCMissing("false_report_Commander_Force");
        var army = new ArmyUnitReference(target.NativeArmyOrigin?.NativeSlotIndex ?? throw OCMissing("false_report_Army_receiver"));
        if (!FalseReportTargetAllowed(state, current, own, army))
            return CommandResult.Reject("The selected Army cannot receive a false report.", "deployment.false_report_target");
        int raw = FRKnown(current.ReadArmyUnitRawHex2A(army), "initial_target_Hex2A");
        var at = current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw OCMissing("false_report_target_Hex_pointer");
        int goal = FRKnown(at.PhysicalSlot, "initial_target_Hex_identity");
        result = new(17, new(27, checked((ushort)FRKnown(army.PhysicalSlot, "initial_target_Army_identity"))), goal);
        return null;
    }

    private static CommandResult? OCValidateInitialFalseReportTargets(GameState state, SourceWorldContext current,
        int forceId, IReadOnlyList<DeploymentUnitRequest> rows)
    {
        var own = new ForceReference(forceId);
        foreach (var group in rows.Where(row => row.Actions is [{ ActionKey: "capture" }])
            .GroupBy(row => row.Actions[0].Target.Id))
        {
            var target = state.Units.GetValueOrDefault(group.Key) ?? throw OCMissing("false_report_target");
            var army = new ArmyUnitReference(target.NativeArmyOrigin?.NativeSlotIndex ?? throw OCMissing("false_report_Army_receiver"));
            var destinations = ReadFalseReportDestinations(current, own, army);
            foreach (var row in group)
                if (!destinations.Any(value => value.HexId == row.Actions[0].FalseReportDestinationHexId))
                    return CommandResult.Reject("The inducement destination is no longer available.", "deployment.false_report_destination");
        }
        return null;
    }

    private static GameState OCBindInitialFalseReportDestination(GameState state, int armyId, DeploymentUnitRequest row)
    {
        if (row.Actions is not [{ ActionKey: "capture", FalseReportDestinationHexId: int destination }]) return state;
        var lifecycle = state.ReadArmyLifecycle() ?? throw OCMissing("false_report_created_lifecycle");
        var receiver = lifecycle.Receivers.FindUnit(armyId) ?? throw OCMissing("false_report_created_receiver");
        return state.WithArmyLifecycle(lifecycle with { Receivers = lifecycle.Receivers.With(receiver with
        { FalseReportDestination = ArmyMarchDestination.AtHex(destination) }) });
    }

    private sealed partial class UnitTargetMenuReader
    {
        private void AddInitialFalseReport()
        {
            Add("capture", 0x114, 0x56C1, CaptureIncluded, () =>
            {
                //Prototype opens the real destination subpage. Its accepted
                //Trap Hex is filled there, before any formal deployment call.
                foreach (int id in selectedOfficerIds)
                {
                    int person = Need(current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot, "false_report_Person_pointer");
                    if (!Need(current.ReadPersonNativeValidity(person), "false_report_Person_validity") ||
                        current.ReadPersonForce(person) != own) throw Missing("false_report_Person_Force");
                }
                return new("capture", AttackTarget());
            }, presentationMissing: "unit.target_menu.false_report_probability");
        }
    }
}
