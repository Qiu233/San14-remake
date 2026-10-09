using San14.Core;

namespace San14.Data;

public sealed record TransportArmyRouteReceipt(int ArmyPhysical, int? GoalHex,
    int[] PathHexes, ushort? NextHex48Write, ushort? Goal1F8Write, int ExpandedNodes, string SourceBranch);
public sealed record TransportArmyRouteResult(TransportArmyRouteReceipt? Receipt, string? UnresolvedOwner);

/// <summary>17A620's actual301DD0(Army,1,1,0), ordinary type2 Action2/City
/// target. This writes no gameplay position or route credit. Caller owns the
/// writable current/pool and publishes the returned scalar writes together.
/// The original diagnostic trace UI is absent from this independent client.</summary>
public static partial class TransportArmyRouteSource
{
    public const string Revision = "san14:ce-type2-route-301DD0-mode1-20261008";
    private const int Count = 48400;

    public static TransportArmyRouteResult Rewrite(GameState state,
        SourceWorldContext current, UnitState army)
    {
        try
        {
            int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("Army_physical");
            var arena = state.NativeArmyLifecycle ?? throw Missing("lifecycle");
            var slot = arena.Slots[physical];
            if (K(slot.Commander12, "Army12") == 0 || K(slot.Status10, "Army10") == 0)
                return Done(null, [], null, null, 0, "301DD0.invalid_Army");
            if (K(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "233560"))
                return Done(null, [], null, null, 0, "301DD0.excluded_Army");
            var (goal, kind, identity) = ReadCityGoal(state, current, army);
            if (goal == Count) return Done(goal, [], null, null, 0, "301DD0.no_goal");
            if (K(army.NativeRouteHold56, "Army56") != 0) return Done(goal, [], null, null, 0, "301DD0.hold56");
            if (K(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "second233560"))
                return Done(goal, [], null, null, 0, "301DD0.second_excluded");
            ushort start = K(army.NativeMarch?.CurrentHex2A ?? army.NativeCurrentHex2A, "Army2A");
            if (start >= Count || goal is < 0 or >= Count) throw Missing("route_endpoint");
            var query = new Queries(state, current, army, start, goal, kind, identity);
            int[] path = Search(query, out int expanded);
            ushort? next = path.Length > 1 ? (ushort)Math.Clamp(path[1], 0, Count) : null;
            ushort future = path.Length > 4 ? unchecked((ushort)path[4]) : (ushort)Count;
            return Done(goal, path, next, future, expanded, path.Length == 0 ?
                "301DD0.mode1_no_path" : "301DD0.mode1_source_path");

            TransportArmyRouteResult Done(int? goalHex, int[] path, ushort? next, ushort? future,
                int expanded, string branch) => new(new(physical, goalHex, path, next, future, expanded, branch), null);
        }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    internal static (int Goal, int[] Path, int Expanded) ReadCreationPath(GameState state,
        SourceWorldContext current, UnitState army)
    {
        var (goal, kind, identity) = ReadCityGoal(state, current, army);
        ushort start = K(army.NativeMarch?.CurrentHex2A ?? army.NativeCurrentHex2A, "Army2A");
        if (start >= Count || goal is < 0 or >= Count) throw Missing("route_endpoint");
        int[] path = Search(new Queries(state, current, army, start, goal, kind, identity), out int expanded);
        return (goal, path, expanded);
    }

    private static (int Goal, byte Kind, ushort Identity) ReadCityGoal(GameState state,
        SourceWorldContext current, UnitState army)
    {
        if (army.NativeTacticRuntime?.Status10 != 2 || army.NativeArmyConfiguration?.Action37 != 2)
            throw Missing("ordinary_type2_Action2_scope");
        if (army.NativeTransport is not { Waypoints2C.Length: 5 } transport ||
            army.OverrideTarget is not { Kind: ArmyOverrideTargetKind.None }) throw Missing("transport_waypoints_overrideFF");
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("Army_physical");
        var target = state.NativeArmyLifecycle is { } lifecycle
            ? OwnedArmyLifecycleTargets.Read(state, lifecycle.Slots[physical].ReceiverId) ?? throw Missing("Army38_3A")
            : throw Missing("Army38_3A");
        if (target.Kind38 is not byte kind || target.Identity3A is not ushort identity) throw Missing("Army38_3A");
        if (kind != 5) throw Missing("ordinary_target_City_scope");
        int goal = transport.Waypoints2C[0];
        if (goal == Count)
        {
            var city = K(current.ReadCityTableReference(identity <= 51 ? identity : 0), "goal_City_pointer");
            if (K(current.ReadBaseNativeValidity(city), "goal_City_validity"))
            {
                ushort link = K(current.ReadBaseObjectLink(city), "goal_Base4E");
                var obj = K(current.ReadObjectTableReference(link <= 3000 ? link : 0), "goal_Object_pointer");
                if (K(current.ReadObjectNativeValidity(obj), "goal_Object_validity"))
                    goal = K(current.ReadObjectHexIndex(obj), "goal_Object10");
            }
        }
        return (goal, kind, identity);
    }

    private static int[] Search(Queries query, out int expanded)
    {
        expanded = 0;
        if (!query.AdmitGoal()) return [];
        float baseCost = query.TerrainCost(query.Surface(2));
        float mobility = query.Mobility(query.LandFormation, 2);
        float heuristicDenominator = baseCost <= mobility ? baseCost : mobility;
        var nodes = new Node?[Count];
        Node? root = new(query.Goal, 0, null) { Priority = InitialPriority(query.Goal, query.Start), Open = true };
        nodes[query.Goal] = root;
        while (root is not null)
        {
            Node at = Pop(ref root); expanded++;
            if (at.Hex == query.Start)
            {
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
                    if (!query.Passable(neighbor) || query.HostileAtHex(neighbor)) continue;
                    if (query.BlockedScene(neighbor)) continue;
                    cost = unchecked(cost + query.OccupiedCost(neighbor));
                }
                cost = NativeInt((float)cost / query.TerrainMobility(neighbor));
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
    private static MissingOwner Missing(string name) => new("current.transport_route_" + name);
    internal sealed class MissingOwner(string owner) : Exception(owner);
}
