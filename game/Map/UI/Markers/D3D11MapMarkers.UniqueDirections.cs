using San14.Data;

internal partial class D3D11MapMarkers
{
    private readonly Dictionary<OrdinaryTacticViewIdentity, int> directionSuppression = [];

    internal void ChangeDirectionSuppression(IEnumerable<OrdinaryTacticViewIdentity> targets, int delta)
    {
        //Each source descriptor holds its own increment, including duplicates.
        foreach (var target in targets)
        {
            int count = Math.Clamp(directionSuppression.GetValueOrDefault(target) + delta, 0, byte.MaxValue);
            if (count <= 0) directionSuppression.Remove(target); else directionSuppression[target] = count;
        }
        _armyLabels?.SetDirectionSuppression(directionSuppression);
        QueueMarkerRedraw();
    }

    private bool DirectionSuppresses(Marker marker) => marker.Kind != Kind.Area &&
        directionSuppression.ContainsKey(new(marker.Kind == Kind.City ? OrdinaryTacticViewKind.City : OrdinaryTacticViewKind.Gate, marker.Id));
}
