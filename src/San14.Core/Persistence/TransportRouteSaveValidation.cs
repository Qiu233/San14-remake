namespace San14.Core;

internal static class TransportRouteSaveValidation
{
    public static void Validate(GameState state)
    {
        foreach (var unit in state.Units.Values.Concat(state.ArmyLifecycle?.Receivers.Entries
            .Where(receiver => receiver.PendingImage is not null).Select(receiver => receiver.PendingImage!) ?? [])
            .Concat(state.ForceDefeats.Values.SelectMany(defeat => defeat.WithdrawnUnits)))
        {
            if (unit.TransportRoute is not { } route) continue;
            if (unit.Mission?.Kind != UnitMissionKind.Transport)
                throw new InvalidDataException("Saved transport route has no transport mission.");
            foreach (var waypoint in route.Waypoints)
                if (waypoint is not null && (!Enum.IsDefined(waypoint.Kind) ||
                    waypoint.Kind == ArmyMarchDestinationKind.None && waypoint.HexId is not null ||
                    waypoint.Kind == ArmyMarchDestinationKind.Hex && waypoint.HexId is not (>= 0)))
                    throw new InvalidDataException("Saved transport waypoint is invalid.");
        }
    }
}
