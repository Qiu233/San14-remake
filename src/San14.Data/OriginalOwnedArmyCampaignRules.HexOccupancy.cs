using San14.Core;

namespace San14.Data;

public sealed partial class OriginalOwnedArmyCampaignRules : IPlayableArmyHexOccupancyRules
{
    /// <summary>A private same-current draft for one already decided ordinary
    /// traversal. Only saved Hex1C writes are published after every edge succeeds.</summary>
    public PlayableArmyHexOccupancyResult TryApplyTraversal(GameState state, UnitState actor,
        ReadOnlySpan<int> traversed)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(actor);
        if (traversed.IsEmpty) return new(state);
        if (state.NativeArmyLifecycle is not { } arena)
            return Unknown("current.position_commit_saved_Hex1C_owner");
        if (actor.NativeArmyOrigin?.NativeSlotIndex is not int physical || physical is < 1 or > 500)
            return Unknown("current.position_commit_Army_physical1_500");
        ushort? nativeFrom = actor.NativeMarch?.CurrentHex2A ?? actor.NativeCurrentHex2A;
        if (nativeFrom is not ushort from || from != actor.HexId)
            return Unknown("current.position_commit_Army2A_projection");
        var current = CaptureCurrentDefinitions(state);
        if (current is null) return Unknown("current.position_commit_definitions");
        var entry = current.ReadHexTableReference(from);
        if (entry?.PhysicalSlot is not int entryPhysical)
            return Unknown("current.position_commit_old_Hex_pointer");
        current.BindArmyMapOccupancy(state);
        if (current.ReadHexRaw1C(entry) is null)
            return Unknown("current.position_commit_old_Hex_signed1C");
        int edgeFrom = from;
        foreach (int edgeTo in traversed)
        {
            if (OriginalArmyDirectionSource.ReadAdjacentDirection36(edgeFrom, edgeTo) is null)
                return Unknown("branch.position_commit_nonadjacent_traversal");
            var applied = OriginalArmyCurrentHexOccupancySource.TryApplyCurrent(current, physical, edgeFrom, edgeTo);
            if (applied.Receipt is not { } receipt) return Unknown(applied.UnresolvedOwner!);
            edgeFrom = edgeTo;
        }
        return new(state with { ArmyMapOccupancy = current.ExportArmyMapOccupancy() });

        static PlayableArmyHexOccupancyResult Unknown(string owner) => new(null, owner);
    }
}
