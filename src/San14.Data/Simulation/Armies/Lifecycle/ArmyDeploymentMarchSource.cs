using San14.Core;

namespace San14.Data;

/// <summary>Independent new-player deployment fields before the creation
/// wrapper resolves its route. Other packet producers retain unknown inputs.</summary>
internal sealed record ArmyDeploymentMarchSource(ArmyMarchState March, ArmyTargetSourceFields Goal, byte Action,
    byte? RouteHold)
{
    internal static ArmyDeploymentMarchSource? Read(PreparedDeployment deployment, byte? commandControl,
        ArmyTargetSourceFields? normalTarget = null)
    {
        // The target dialog writes a Hex identity independently from its five
        // selected intermediate destinations. Do not use the playable adapter's
        // resolved destination, approach cell, or remaining computed path.
        if (deployment.Actions is not [var action] ||
            deployment.WaypointHexIds is not { Length: <= 5 } waypoints ||
            waypoints.Any(hex => hex is < 0 or >= 48400)) return null;
        ArmyTargetSourceFields goal;
        byte nativeAction;
        if (action is { ActionKey: "move" or "stay",
                Target: { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 } })
        {
            goal = new(9, (ushort)action.Target.Id);
            nativeAction = action.ActionKey == "move" ? (byte)1 : (byte)13;
        }
        else if (action.ActionKey is ("attack" or "enter") && normalTarget is { Kind38: 5 or 6 or 27, Identity3A: not null })
        {
            //Only the normal Make/Target committer supplies this actual
            //resolved receiver. A script or old save cannot acquire a target
            //by interpreting a logical City/Gate/Unit ID as a source ordinal.
            goal = normalTarget;
            if (action.ActionKey == "enter" && normalTarget.Kind38 == 27) return null;
            nativeAction = action.ActionKey == "enter"
                ? normalTarget.Kind38 == 5 ? (byte)2 : (byte)3
                : normalTarget.Kind38 == 5 ? (byte)4 : normalTarget.Kind38 == 6 ? (byte)5 : (byte)7;
        }
        else if (action is { ActionKey: "capture", Target.Kind: DeploymentTargetKind.Unit, StructureKindId: null,
                FalseReportDestinationHexId: >= 0 and < 48400 } && normalTarget is { Kind38: 27, Identity3A: not null })
        {
            goal = normalTarget;
            nativeAction = 17;
        }
        else if (action is { ActionKey: "occupy", Target.Kind: DeploymentTargetKind.Area, StructureKindId: null } &&
            normalTarget is { Kind38: 9, Identity3A: not null })
        {
            //7EB310 case7 stores the Area representative's actual Hex
            //independently from its typed Area objective and five points.
            goal = normalTarget;
            nativeAction = 14;
        }
        else if (action is { ActionKey: "fire", Target.Kind: DeploymentTargetKind.Hex, StructureKindId: null } &&
            normalTarget is { Kind38: 9, Identity3A: not null })
        {
            //The ordinary initial packet is16/9/physical; its independent
            //original press was checked before allocation, not a waypoint.
            goal = normalTarget;
            nativeAction = 16;
        }
        else if (action is { ActionKey: "build", Target.Kind: DeploymentTargetKind.Hex } &&
            FacilityConstructionIntent.IsDefinitionKey(action.StructureKindId) &&
            normalTarget is { Kind38: 9, Identity3A: not null })
        {
            //Only the normal committer supplies this actual construction Hex.
            //Selected definition/sites live on the real admitted receiver.
            goal = normalTarget;
            nativeAction = 8;
        }
        else if (action is { ActionKey: "repair" or "demolish", Target.Kind: DeploymentTargetKind.Structure } &&
            normalTarget is { Kind38: 11, Identity3A: not null })
        {
            //7EB310 stores the actual Object identity independently from the
            //selected five-point path; no post-creation command replacement.
            goal = normalTarget;
            nativeAction = action.ActionKey == "repair" ? (byte)9 : (byte)10;
        }
        else return null;

        // 2EE4B0 clears all five entries when the retained command-control
        // field is nonzero. Unknown control cannot establish the first entry.
        ArmyMarchDestination? first = commandControl is null ? null :
            commandControl != 0 || waypoints.Length == 0 ? ArmyMarchDestination.None :
            ArmyMarchDestination.AtHex(waypoints[0]);
        //The summary creates credit=0, hold=0 and next=BD10. Intermediate-point
        //insert/delete only changes the route group. Confirmation copies its
        //explicit points, excluding the start and independently selected final
        //target; it neither deduplicates nor rewrites those three fields.
        //1EC980 subsequently replaces next through actual path preparation.
        ArmyWaypointContinuation? following = commandControl is null ? null :
            new(commandControl != 0 ? [] : waypoints.Skip(1).Select(hex => (ArmyMarchDestination?)ArmyMarchDestination.AtHex(hex)).ToArray());
        // Original message tables identify action1 as advance and action13 as
        // stand by; both use the selected Hex, independently from waypoints.
        return new(new(0, first, ArmyMarchDestination.None) { FollowingWaypoints = following },
            goal, nativeAction, 0);
    }
}
