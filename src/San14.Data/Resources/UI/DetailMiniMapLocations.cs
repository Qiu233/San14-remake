namespace San14.Data;

/// <summary>Offline-extracted CE small-map positions, independent of current
/// selected-object positions. See DETAIL_MINIMAP_SOURCE_20261018.md.</summary>
public static class DetailMiniMapLocations
{
    private static readonly int[] Cities =
    [
        2841, 3907, 3215, 7807, 7634, 10905, 11373, 10971, 18225, 17102,
        22195, 23037, 28327, 14882, 17946, 21899, 25873, 16379, 21655, 15468,
        24053, 10610, 14542, 6386, 27251, 30351, 35846, 32935, 41543, 45678,
        47814, 29396, 26947, 29578, 33544, 38188, 37265, 43243, 42770, 31089,
        20728, 25778, 35467, 31691, 42488, 47091, 48400, 48400, 48400, 48400, 48400
    ];
    private static readonly int[] Gates =
        [11548, 15950, 16138, 16367, 20323, 19178, 21808, 24241, 27745, 29494];

    public static int ReadHex(int locationId) => locationId switch
    {
        >= 1 and <= 51 => Cities[locationId - 1],
        >= 52 and <= 61 => Gates[locationId - 52],
        _ => throw new ArgumentOutOfRangeException(nameof(locationId))
    };
}
