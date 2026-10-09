namespace San14.Data;

/// <summary>Grouping radii and hex traversal order implemented by the project.</summary>
public sealed class ArmyMapGroupingProfile
{
    private readonly int[] _radii, _moves, _order;
    private ArmyMapGroupingProfile(int[] radii, int[] moves, int[] order)
        => (_radii, _moves, _order) = (radii, moves, order);

    public static ArmyMapGroupingProfile Create()
    {
        return new([3,3,3],
            [-1,0, 0,-1, 1,0, -1,1, 0,1, 1,1,
             -1,-1, 0,-1, 1,-1, -1,0, 0,1, 1,0],
            [2,3,6,5,4,1]);
    }

    public int Radius(int index) => _radii[index];
    public (int X, int Y) Direction(int parity, int direction)
    {
        int at = parity * 12 + (direction - 1) * 2;
        return (_moves[at], _moves[at + 1]);
    }
    public IReadOnlyList<int> RingSideOrder => Array.AsReadOnly(_order);

    public IReadOnlyList<int> EnumerateNeighbors(int centerPhysical, int index)
    {
        var result = new List<int>();
        int centerX = centerPhysical % 220, centerY = centerPhysical / 220;
        for (int ring = 1; ring <= _radii[index]; ring++)
        {
            int x = centerX, y = centerY;
            for (int step = 0; step < ring; step++) Move(4);
            foreach (int direction in _order)
                for (int step = 0; step < ring; step++)
                {
                    Move(direction); //The original appends after moving, even the closing step.
                    if ((uint)x < 220 && (uint)y < 220) result.Add(y * 220 + x);
                }
            void Move(int direction)
            {
                int parity = (x % 2 + 2) % 2;
                (int dx, int dy) = Direction(parity, direction);
                x += dx; y += dy;
            }
        }
        return result.AsReadOnly();
    }
}
