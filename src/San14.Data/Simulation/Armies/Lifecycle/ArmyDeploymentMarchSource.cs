using San14.Core;

namespace San14.Data;

/// <summary>Independent new-player deployment fields before the creation
/// wrapper resolves its route. Other packet producers retain unknown inputs.</summary>
internal sealed record ArmyDeploymentMarchSource(ArmyMarchState March, ArmyTargetSourceFields Goal, byte Action,
    byte? RouteHold)
{
    internal static ArmyDeploymentMarchSource? Read(PreparedDeployment deployment, byte? commandControl)
    {
        // The target dialog writes a Hex identity independently from its five
        // selected intermediate destinations. Do not use the playable adapter's
        // resolved destination, approach cell, or remaining computed path.
        if (deployment.Actions is not [{ ActionKey: "move" or "stay",
                Target: { Kind: DeploymentTargetKind.Hex, Id: >= 0 and < 48400 } } action] ||
            deployment.WaypointHexIds is not { Length: <= 5 } waypoints ||
            waypoints.Any(hex => hex is < 0 or >= 48400)) return null;

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
            new(9, (ushort)action.Target.Id), action.ActionKey == "move" ? (byte)1 : (byte)13, 0);
    }
}
