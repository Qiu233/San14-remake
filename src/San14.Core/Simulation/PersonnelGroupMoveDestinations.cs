namespace San14.Core;

/// <summary>The application's ten group-move selections. They are not campaign
/// state and loading an older world does not restore an earlier selection.</summary>
public sealed class PersonnelGroupMoveDestinations
{
    private readonly int?[] destinations = new int?[10];

    public int? GetDestination(int groupIndex) => destinations[CheckedIndex(groupIndex)];

    public void SetDestination(int groupIndex, int? locationId)
    {
        if (locationId is < 1 or > 61) throw new ArgumentOutOfRangeException(nameof(locationId));
        destinations[CheckedIndex(groupIndex)] = locationId;
    }

    public void Clear() => Array.Clear(destinations);

    private static int CheckedIndex(int groupIndex) => groupIndex is >= 0 and < 10
        ? groupIndex : throw new ArgumentOutOfRangeException(nameof(groupIndex));
}
