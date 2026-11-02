using San14.Core;

namespace San14.Data;

public sealed record TransportArmyRouteReceipt(int ArmyPhysical, int? GoalHex,
    int[] PathHexes, ushort? NextHex48Write, ushort? Goal1F8Write, int ExpandedNodes, string SourceBranch);
public sealed record TransportArmyRouteResult(TransportArmyRouteReceipt? Receipt, string? UnresolvedOwner);

/// <summary>17A620's actual301DD0(Army,1,1,0), type2 transport with a Hex,
/// City or Gate target. This writes no gameplay position or route credit. Caller owns the
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
            var arena = state.ReadArmyLifecycle() ?? throw Missing("lifecycle");
            var slot = arena.Slots[physical];
            if (K(slot.Commander12, "Army12") == 0 || K(slot.Status10, "Army10") == 0)
                return Done(null, [], null, null, 0, "301DD0.invalid_Army");
            if (K(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "233560"))
                return Done(null, [], null, null, 0, "301DD0.excluded_Army");
            var (goal, kind, identity) = ReadTransportGoal(state, current, army);
            if (goal == Count) return Done(goal, [], null, null, 0, "301DD0.no_goal");
            if (K(army.NativeRouteHold56, "Army56") != 0) return Done(goal, [], null, null, 0, "301DD0.hold56");
            if (K(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical), "second233560"))
                return Done(goal, [], null, null, 0, "301DD0.second_excluded");
            ushort start = K(ArmyMarchSourceAdapter.ReadCurrentHex(state, army), "Army2A");
            if (!current.ContainsMapHex(start) || !current.ContainsMapHex(goal)) throw Missing("route_endpoint");
            int[] path = ArmyRouteSearch.Search(state, current, army, start, goal, kind, identity, null, out int expanded);
            ushort? next = path.Length > 1 ? ArmyMarchSourceAdapter.EncodeRouteHex(path[1]) : null;
            ushort future = path.Length > 4 ? ArmyMarchSourceAdapter.EncodeRouteHex(path[4]) : (ushort)Count;
            return Done(goal, path, next, future, expanded, path.Length == 0 ?
                "301DD0.mode1_no_path" : "301DD0.mode1_source_path");

            TransportArmyRouteResult Done(int? goalHex, int[] path, ushort? next, ushort? future,
                int expanded, string branch) => new(new(physical, goalHex, path, next, future, expanded, branch), null);
        }
        catch (ArmyRouteSearch.MissingOwner error) { return new(null, error.Message); }
        catch (MissingOwner error) { return new(null, error.Message); }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    internal static (int Goal, int[] Path, int Expanded) ReadCreationPath(GameState state,
        SourceWorldContext current, UnitState army)
    {
        var (goal, kind, identity) = ReadTransportGoal(state, current, army);
        ushort start = K(ArmyMarchSourceAdapter.ReadCurrentHex(state, army), "Army2A");
        if (!current.ContainsMapHex(start) || !current.ContainsMapHex(goal)) throw Missing("route_endpoint");
        int[] path = ArmyRouteSearch.Search(state, current, army, start, goal, kind, identity,
            army.NativeArmyOrigin?.NativeSlotIndex, out int expanded);
        return (goal, path, expanded);
    }

    private static (int Goal, byte Kind, ushort Identity) ReadTransportGoal(GameState state,
        SourceWorldContext current, UnitState army)
    {
        byte? action = ArmyConfigurationSourceAdapter.ReadAction(state, army);
        if (army.NativeTacticRuntime?.Status10 != 2 || action is not (1 or 2 or 3 or 11 or 12))
            throw Missing("type2_Action1_2_3_11_12_scope");
        if (army.TransportRoute is null ||
            army.OverrideTarget is not { Kind: ArmyOverrideTargetKind.None }) throw Missing("transport_waypoints_overrideFF");
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("Army_physical");
        var target = state.ReadArmyLifecycle() is { } lifecycle
            ? OwnedArmyLifecycleTargets.Read(state, lifecycle.Slots[physical].ReceiverId) ?? throw Missing("Army38_3A")
            : throw Missing("Army38_3A");
        if (target.Kind38 is not byte kind || target.Identity3A is not ushort identity) throw Missing("Army38_3A");
        if (!(action == 1 && kind == 9 || (action is 2 or 11) && kind == 5 || (action is 3 or 12) && kind == 6))
            throw Missing("ordinary_transport_action_target");
        int goal = TransportRouteSourceAdapter.ReadWaypoint(state, army, 0) ?? throw Missing("transport_first_waypoint");
        if (goal == Count)
        {
            if (kind == 9)
                goal = identity; //301DD0 reads the already captured Hex identity directly.
            else
            {
                var targetBase = kind == 5
                    ? K(current.ReadCityTableReference(identity <= 51 ? identity : 0), "goal_City_pointer")
                    : K(current.ReadGateTableReference(identity <= 10 ? identity : 0), "goal_Gate_pointer");
                if (K(current.ReadBaseNativeValidity(targetBase), "goal_Base_validity"))
                {
                    ushort link = K(current.ReadBaseObjectLink(targetBase), "goal_Base4E");
                    var obj = K(current.ReadObjectTableReference(link <= 3000 ? link : 0), "goal_Object_pointer");
                    if (K(current.ReadObjectNativeValidity(obj), "goal_Object_validity"))
                        goal = K(current.ReadObjectHexIndex(obj), "goal_Object10");
                }
            }
        }
        return (goal, kind, identity);
    }

    internal static int Distance(int first, int second) => ArmyRouteSearch.Distance(first, second);
    private static T K<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static T K<T>(T? value, string name) where T : class => value ?? throw Missing(name);
    private static MissingOwner Missing(string name) => new("current.transport_route_" + name);
    internal sealed class MissingOwner(string owner) : Exception(owner);
}
