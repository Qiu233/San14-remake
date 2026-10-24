using San14.Core;

namespace San14.Data;

/// <summary>Source 183C00 mode1, one target, default descriptor. Shared by
/// ordinary and transport writers; their distinct hostile-terrain gates remain explicit.</summary>
internal static partial class ArmyRouteSearch
{
    private const int Count = 48400;
    internal static int[] Search(GameState state, SourceWorldContext current, UnitState army, int start, int goal, byte kind, ushort identity,
        int? registeredSearchArmyPhysical, out int expanded, byte searchFlags = 1)
    {
        var motion = new LiveMovement(state, current, army);
        return Search(new Queries(state, current, motion, start, goal, kind, identity, registeredSearchArmyPhysical, searchFlags), out expanded);
    }

    internal static int[] Search(Queries query, out int expanded)
        => Search(query, out expanded, out _);

    internal static int[] Search(Queries query, out int expanded, out int totalCost, bool hasArmyDescriptor = true)
    {
        expanded = 0; totalCost = 0;
        if (!query.AdmitGoal()) return [];
        float baseCost = query.TerrainCost(query.Surface(2));
        float mobility = hasArmyDescriptor ? query.Mobility(query.LandFormation, 2) : baseCost;
        float heuristicDenominator = baseCost <= mobility ? baseCost : mobility;
        var nodes = new Node?[Count];
        Node? root = new(query.Goal, 0, null) { Priority = InitialPriority(query.Goal, query.Start), Open = true };
        nodes[query.Goal] = root;
        while (root is not null)
        {
            Node at = Pop(ref root); expanded++;
            if (at.Hex == query.Start)
            {
                totalCost = at.Cost;
                var result = new List<int>();
                for (Node? next = at; next is not null; next = next.Parent) result.Add(next.Hex);
                return result.ToArray();
            }
            for (int direction = 1; direction <= 6; direction++)
            {
                int neighbor = PersonReturnPathSource.Neighbor(at.Hex, direction);
                if (neighbor == Count || !query.HexPresent(neighbor)) continue;
                int cost = query.BaseEdge(neighbor);
                if (neighbor != query.Start)
                {
                    if (!query.Passable(neighbor) || query.AvoidHostile && query.HostileAtHex(neighbor)) continue;
                    if (query.BlockedScene(neighbor)) continue;
                    if (query.ForbidsOccupiedNonTarget && neighbor != query.Goal && query.Occupied(neighbor)) continue;
                    cost = unchecked(cost + query.OccupiedCost(neighbor));
                }
                cost = query.ScaleEdgeCost(neighbor, cost);
                cost = unchecked(at.Cost + cost);
                var next = nodes[neighbor];
                if (next is not null && (next.Open || next.Closed) && cost >= next.Cost) continue;
                if (next is null) { next = new(neighbor, cost, at); nodes[neighbor] = next; }
                else
                {
                    if (next.Open) Remove(ref root, next); //18B020 while old priority is still present.
                    next.Cost = cost; next.Parent = at; next.Closed = false;
                }
                next.Priority = unchecked(cost + NativeInt((float)Distance(neighbor, query.Start) *
                    (baseCost / heuristicDenominator)));
                Insert(ref root, next); next.Open = true;
            }
            at.Open = false; at.Closed = true;
        }
        return [];
    }

    private sealed class Node(int hex, int cost, Node? parent)
    {
        internal readonly int Hex = hex;
        internal int Cost = cost, Priority;
        internal Node? Parent = parent, Left, Right;
        internal bool Open, Closed;
    }
    private static void Insert(ref Node? root, Node next)
    {
        if (root is null) { root = next; return; }
        Node at = root;
        while (true)
        {
            if (next.Priority < at.Priority) { if (at.Left is null) { at.Left = next; return; } at = at.Left; }
            else { if (at.Right is null) { at.Right = next; return; } at = at.Right; }
        }
    }
    private static Node Pop(ref Node? root)
    {
        Node at = root!;
        if (at.Left is null) { root = at.Right; at.Right = null; return at; }
        Node parent = at;
        while (at.Left is not null) { parent = at; at = at.Left; }
        parent.Left = at.Right; at.Right = null; return at;
    }
    private static void Remove(ref Node? root, Node target)
    {
        if (ReferenceEquals(root, target)) { root = Replacement(target); return; }
        Node at = root!;
        while (true)
        {
            bool left = target.Priority < at.Priority;
            Node child = (left ? at.Left : at.Right)!;
            if (ReferenceEquals(child, target))
            { if (left) at.Left = Replacement(target); else at.Right = Replacement(target); return; }
            at = child;
        }
    }
    private static Node? Replacement(Node target)
    {
        Node? replacement = target.Left;
        if (replacement is null) replacement = target.Right;
        else { Node last = replacement; while (last.Right is not null) last = last.Right; last.Right = target.Right; }
        target.Left = target.Right = null; return replacement;
    }
    private static int InitialPriority(int first, int second)
    {
        //2AF2C0 returns column and row. 18B160 uses two abs differences,
        //whereas later priority updates use actual2AAC90 hex distance.
        int x1 = first % 220, x2 = second % 220;
        return Math.Abs(x1 - x2) + Math.Abs(first / 220 - second / 220);
    }
    internal static int Distance(int first, int second)
    {
        int x1 = first % 220, x2 = second % 220;
        int dx = Math.Abs(x2 - x1), dy = Math.Abs(2 * (second / 220 - first / 220) -
            (x1 % 2 == 0 ? 1 : 0) + (x2 % 2 == 0 ? 1 : 0));
        return dy < dx ? dx : dx + (dy - dx) / 2;
    }
    private static int NativeInt(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f
        ? int.MinValue : (int)value;
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static T K<T>(T? value, string name) where T : class => value ?? throw Missing(name);
    private static MissingOwner Missing(string name) => new("current.army_route_" + name);
    internal sealed class MissingOwner(string owner) : Exception(owner);
}
