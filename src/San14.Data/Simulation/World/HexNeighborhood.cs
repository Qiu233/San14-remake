namespace San14.Data;

/// <summary>
/// The native 2A6D30 radius1 call used by 3306E0. Geometry is confined to the
/// inspected original Data profile; independent Core maps do not inherit it.
/// </summary>
public static class HexNeighborhood
{
    // Two x-parity rows, directions1..6.
    private static readonly (int X, int Y)[][] Directions =
    [
        [(-1, 0), (0, -1), (1, 0), (-1, 1), (0, 1), (1, 1)],
        [(-1, -1), (0, -1), (1, -1), (-1, 0), (0, 1), (1, 0)]
    ];
    // Walk order; candidates exclude the center.
    private static readonly int[] WalkOrder = [2, 3, 6, 5, 4, 1];

    public static IReadOnlyList<int> RadiusOneCandidates(int hexPhysicalSlot) =>
        RadiusOneCandidates(hexPhysicalSlot, SourceWorldContext.OriginalMapWidth, SourceWorldContext.OriginalMapHeight);

    internal static IReadOnlyList<int> RadiusOneCandidates(int hexPhysicalSlot, int width, int height)
    {
        if (width < 1 || height < 1 || (uint)hexPhysicalSlot >= (long)width * height)
            throw new InvalidDataException("Hex neighborhood center is outside the current map.");
        int x = hexPhysicalSlot % width, y = hexPhysicalSlot / width;
        Step(4);
        var result = new List<int>(6);
        foreach (int direction in WalkOrder)
        {
            Step(direction);
            if ((uint)x < (uint)width && (uint)y < (uint)height) result.Add(y * width + x);
        }
        return result.AsReadOnly();

        void Step(int direction)
        {
            // Native abs(x)%2 is recomputed after every step, including outside the map.
            (int dx, int dy) = Directions[Math.Abs(x) % 2][direction - 1];
            x += dx; y += dy;
        }
    }
}
