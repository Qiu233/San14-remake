using San14.Core;
using San14.Data;

internal sealed partial class DeploymentTargetDraft
{
    //7CA9A0 has already written these real packet actions. The source-filter
    //route is not a unit route: resolve each actual saved segment here.
    private void InitializeGeneralTargets(IReadOnlyDictionary<int,int> goals)
    {
        if (goals.Count != order.Count || order.Any(officer => !goals.ContainsKey(officer)))
            throw new ArgumentException("Initial general targets must cover the prepared rows.", nameof(goals));
        foreach (int officer in order)
        {
            RequireHex(goals[officer]);
            var request = rows[officer].Request;
            if (request.Actions.Count != 1 || request.WaypointHexIds.Count != 0)
                throw new InvalidDataException("A general entry requires its actual initial action and empty waypoint slots.");
            progress[officer] = progress[officer] with { HasResolvedTarget = true };
        }
        groups = Normalize(order.Select(officer => new DeploymentTargetRouteGroup(
            [officer], [starts[officer], goals[officer]], null) { Segments = [null] }));
        groups = groups.Select(EnsureSegments).ToArray();
        foreach (var group in groups) Integrate(group);
    }
}
