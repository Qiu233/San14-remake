using San14.Core;

namespace San14.Data;

public sealed record OrdinaryArmyRouteReceipt(int ArmyPhysical, int? GoalHex, int[] PathHexes,
    ushort? NextHexWrite, ushort? CachedGoalWrite, int ExpandedNodes, string SourceBranch);
public sealed record OrdinaryArmyRouteResult(OrdinaryArmyRouteReceipt? Receipt, string? UnresolvedOwner);

/// <summary>The ordinary move/stay Hex branch of 1EC980 and 301DD0, using
/// the actual mode1 path search. It never supplies credit or a provisional route.</summary>
public static class OrdinaryArmyRouteSource
{
    private const int Count = ArmyMarchSourceAdapter.SourceHexCount;

    public static ushort? ReadCurrentTarget(GameState state, UnitState army)
    {
        ushort? first = ArmyMarchSourceAdapter.EncodeDestination(army.March?.Target);
        if (first is null) return null;
        var temporary = ArmyOverrideTargetSourceAdapter.Read(state, army);
        if (temporary.Kind3C is not byte kind) return null;
        if (kind == 255)
        {
            if (first != Count) return first;
            if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical || state.ReadArmyLifecycle() is not { } directArena) return null;
            var target = OwnedArmyLifecycleTargets.Read(state, directArena.Slots[physical].ReceiverId);
            if (target is { Kind38: 9, Identity3A: ushort hex }) return hex;
            if (target is not { Kind38: 11, Identity3A: ushort objectLookup } ||
                (state.ResourceState as CampaignSourceKnowledge)?.MapObjectSource is not { } objects ||
                objects.LookupPhysicalSlots[objectLookup <= 3000 ? objectLookup : 0] is not int objectPhysical)
                return null;
            var receiver = state.MapObjects?.Find(objects.PhysicalReceivers[objectPhysical]);
            //2B5610 reads Object10 without a native-validity gate.
            return receiver?.Pose?.Kind switch
            {
                MapObjectPoseKind.Hex => checked((ushort)receiver.Pose.HexId!.Value),
                MapObjectPoseKind.None => (ushort)Count,
                MapObjectPoseKind.Unresolved => objects.UnmappedCoordinates.TryGetValue(objectPhysical, out ushort rawPosition) ? rawPosition : null,
                _ => null
            };
        }
        if (kind != 27 || temporary.Identity3E is not ushort raw) return null;
        if (state.ReadArmyLifecycle() is not { } arena) return null;
        var slot = arena.ReceiverSlots()[raw <= 500 ? raw : 0];
        if (slot.Commander12 is not ushort commander) return null;
        if (commander == 0) return first;
        if (slot.Status10 is not byte status) return null;
        if (status == 0) return first;
        var selected = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
        return selected is null ? null : ArmyMarchSourceAdapter.ReadCurrentHex(state, selected);
    }

    //18A240 selects the copied Army's3C/3E when set, independently of
    //2B54A0's resolved/fallback Hex. Other copied fields keep their owners.
    internal static (byte Kind, ushort Identity)? ReadEffectiveSearchTarget(GameState state, UnitState army)
    {
        var temporary = ArmyOverrideTargetSourceAdapter.Read(state, army);
        if (temporary.Kind3C is not byte kind) return null;
        if (kind != 255) return temporary.Identity3E is ushort id ? (kind, id) : null;
        if (army.NativeArmyOrigin?.NativeSlotIndex is not int physical || state.ReadArmyLifecycle() is not { } arena) return null;
        var main = OwnedArmyLifecycleTargets.Read(state, arena.Slots[physical].ReceiverId);
        return main is { Kind38: byte mainKind, Identity3A: ushort raw } ? (mainKind, raw) : null;
    }

    public static OrdinaryArmyRouteResult PrepareCreation(GameState state, SourceWorldContext current, UnitState army)
        => Resolve(state, current, army, true, true);

    /// <summary>301DD0(mode1, refresh, no alternate mode): cached target is
    /// retained until reached unless refresh is explicitly requested by caller.</summary>
    public static OrdinaryArmyRouteResult Rewrite(GameState state, SourceWorldContext current, UnitState army,
        bool refresh = true) => Resolve(state, current, army, false, refresh);

    private static OrdinaryArmyRouteResult Resolve(GameState state, SourceWorldContext current, UnitState army,
        bool creation, bool refresh)
    {
        try
        {
            int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("Army_physical");
            var arena = state.ReadArmyLifecycle() ?? throw Missing("lifecycle");
            var slot = arena.Slots[physical];
            if (slot.Commander12 is not ushort commander || slot.Status10 is not byte status) throw Missing("validity");
            if (commander == 0 || status == 0) return Done(null, [], null, null, 0, "invalid_Army");
            if (status != 1 || ArmyConfigurationSourceAdapter.ReadAction(state, army) is not (1 or 8 or 9 or 13) ||
                army.OverrideTarget?.Kind is not (ArmyOverrideTargetKind.None or ArmyOverrideTargetKind.ArmyReceiver)) throw Missing("ordinary_move_stay_scope");
            if (creation && ArmyConfigurationSourceAdapter.ReadAction(state, army) is not (1 or 13))
                throw Missing("creation_move_stay_scope");
            if (creation && ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army) is not (>= 1 and <= 18))
                throw Missing("creation_mode1_formation");
            if (!creation)
            {
                bool excluded = OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical) ?? throw Missing("233560");
                if (excluded) return Done(null, [], null, null, 0, "excluded_Army");
            }
            ushort start = ArmyMarchSourceAdapter.ReadCurrentHex(state, army) ?? throw Missing("Army2A");
            ushort goal;
            bool freshGoal = creation || refresh;
            if (!freshGoal)
            {
                goal = army.NativeRouteGoal1F8 ?? throw Missing("cached_goal");
                freshGoal = goal == Count || goal == start;
            }
            else goal = Count;
            if (freshGoal) goal = ReadCurrentTarget(state, army) ?? throw Missing("current_target");
            if (goal == Count) return Done(goal, [], null, null, 0, "no_goal");
            if (!creation)
            {
                byte hold = army.NativeRouteHold56 ?? throw Missing("route_hold");
                if (hold != 0) return Done(goal, [], null, null, 0, "hold");
            }
            if (start >= Count || goal >= Count) throw Missing("route_endpoint");
            var descriptorTarget = ReadEffectiveSearchTarget(state, army) ?? throw Missing("effective_search_target");
            if (army.OverrideTarget?.Kind == ArmyOverrideTargetKind.None && descriptorTarget.Kind is not (9 or 11))
                throw Missing("Hex_target_fields");
            var temporary = current.CreateTemporaryHexList() ?? throw Missing("descriptor");
            int[] path;
            int expanded;
            try
            {
                current.ClearTemporaryHexList(temporary);
                path = ArmyRouteSearch.Search(state, current, army, start, goal, descriptorTarget.Kind, descriptorTarget.Identity,
                    creation ? physical : null, out expanded);
            }
            finally { current.ClearTemporaryHexList(temporary); current.ReleaseTemporaryHexList(temporary); }
            int index = 1;
            if (creation && path.Length != 0)
            {
                var hex = current.ReadHexTableReference(start) ?? throw Missing("start_Hex");
                if (hex.PhysicalSlot is null) return Done(goal, path, null, null, expanded, "invalid_start_Hex");
                ushort raw = current.ReadHexObjectLink(hex) ?? throw Missing("start_Hex_object");
                var city = current.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw Missing("start_City");
                index = (current.ReadBaseNativeValidity(city) ?? throw Missing("start_City_validity")) ? 2 : 1;
            }
            ushort? next = path.Length > index ? (ushort)Math.Clamp(path[index], 0, Count) : null;
            ushort? cached = !creation && freshGoal ? path.Length > 4 ? (ushort)path[4] : (ushort)Count : null;
            return Done(goal, path, next, cached, expanded, path.Length == 0 ? "no_path" : "path");

            OrdinaryArmyRouteResult Done(int? goalHex, int[] path, ushort? next, ushort? cached, int expanded, string branch)
                => new(new(physical, goalHex, path, next, cached, expanded, (creation ? "1EC980." : "301DD0.") + branch), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
        catch (ArmyRouteSearch.MissingOwner error) { return new(null, error.Message); }
    }

    private static InvalidDataException Missing(string owner) => new("current.ordinary_route_" + owner);
}
