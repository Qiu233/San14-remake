using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static CommandResult? OCReadInitialOccupationTarget(SourceWorldContext current,
        DeploymentAction action, out ArmyCommandTarget? result)
    {
        result = null;
        if (action.Target is not { Kind: DeploymentTargetKind.Area, Id: >= 1 and <= 500 })
            return CommandResult.Reject("The selected initial occupation area is invalid.", "deployment.occupation_target");
        var area = new AreaReference(action.Target.Id);
        if (!OCKnown(current.ReadAreaNativeValidity(area), "initial_occupation_Area_validity"))
            return CommandResult.Reject("The selected occupation area is no longer valid.", "deployment.occupation_target");
        //7EB310 case7:22A030 identifies the actual Area,03F430 reads its
        //representative through HexTable, and22DEF0 supplies physical9/3A.
        ushort raw = OCKnown(current.ReadAreaHexIndex(area), "initial_occupation_Area_representative");
        var center = current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw OCMissing("initial_occupation_Hex_pointer");
        int goal = OCKnown(center.PhysicalSlot, "initial_occupation_Hex_identity");
        result = new(14, new(9, checked((ushort)goal)), goal);
        return null;
    }

    private CommandResult? OCValidateInitialOccupationTargets(GameState state, SourceWorldContext current,
        int force, IReadOnlyList<DeploymentUnitRequest> rows, IReadOnlyDictionary<int, ArmyCommandTarget> targets)
    {
        foreach (var group in rows.Where(row => row.Actions is [{ ActionKey: "occupy" }])
            .GroupBy(row => row.Actions[0].Target.Id))
        {
            var officers = group.Select(row => row.OfficerId).ToArray();
            int goal = targets[officers[0]].GoalHex;
            //Like the existing formal Army occupation command, this reader
            //does not interpret its representative as a new map press. Only
            //the explicit Area predicate is read; no menu/route is rebuilt.
            var reader = new UnitTargetMenuReader(this, state, officers, goal, force, new(false, null), current);
            if (!reader.ReadDeploymentOccupationCommand(group.Key))
                return CommandResult.Reject("These commanders cannot occupy the selected area.", "deployment.occupation_target");
        }
        return null;
    }

    private sealed partial class UnitTargetMenuReader
    {
        private void AddInitialOccupation()
        {
            int ordinal = options.Count;
            int? goal = null;
            Add("occupy", 0x111, 0x56A7, AreaIncluded,
                () => InitialAction("occupy", new(DeploymentTargetKind.Area,
                    Need(TargetArea().PhysicalSlot, "initial_occupation_Area_identity")), out goal));
            if (options.Count > ordinal && options[ordinal].Action is not null)
                options[ordinal] = options[ordinal] with { GoalHexId = goal };
        }

        internal bool ReadDeploymentOccupationCommand(int expectedAreaPhysical)
        {
            foreach (int id in selectedOfficerIds)
            {
                int person = Need(current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot,
                    "initial_occupation_Person_pointer");
                if (!Need(current.ReadPersonNativeValidity(person), "initial_occupation_Person_validity") ||
                    current.ReadPersonForce(person) != own) return false;
            }
            RequireTutorialOwner();
            return AreaIncluded(new AreaReference(expectedAreaPhysical));
        }
    }
}
