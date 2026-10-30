using San14.Core;

namespace San14.Data;

public sealed record RegionEncirclementCandidate(int AreaId, bool Enclosed,
    int[] CapturedAreas, int[] BoundaryAreas, int[] VisitedAreas);
public sealed record RegionEncirclementEvaluation(int ForceId, int SeedAreaId,
    int RankId, bool RankAllowsEncirclement, RegionEncirclementCandidate[] Candidates);
public sealed record RegionEncirclementResult(GameState? State,
    RegionEncirclementEvaluation[] Evaluations, string? UnresolvedOwner)
{ public TransferServiceState? Service { get; init; } }

/// <summary>The candidate expansion and component walk used by the campaign
/// encirclement consumer. Each candidate has an independent traversal.</summary>
internal static class RegionEncirclementSource
{
    internal static int[] ExpandCandidates(GameState state, SourceWorldContext current, int seed, int force,
        (int AreaId, int ForceId)? assumedOwnership = null)
    {
        var q = new Queries(state, current, assumedOwnership);
        return q.Owner(seed) != force ? [] : q.Neighbors(seed).Where(id => (q.Flags(id) & 0x40) == 0 &&
            Known(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, q.Owner(id)), "relation")).ToArray();
    }

    internal static RegionEncirclementCandidate EvaluateCandidate(GameState state, SourceWorldContext current, int candidate, int force,
        (int AreaId, int ForceId)? assumedOwnership = null)
    {
        var q = new Queries(state, current, assumedOwnership);
        var captured = new List<int>(); var boundary = new List<int>();
        var visited = new HashSet<int>(); var visitOrder = new List<int>();
        bool enclosed = Visit(candidate);
        return new(candidate, enclosed, enclosed ? captured.ToArray() : [], enclosed ? boundary.ToArray() : [], visitOrder.ToArray());
        bool Visit(int area)
        {
            int owner = q.Owner(area);
            if (!visited.Add(area)) return true;
            visitOrder.Add(area);
            if (owner == force) { boundary.Add(area); return true; }
            if ((q.Flags(area) & 0x40) != 0) return false;
            foreach (int neighbor in q.Neighbors(area)) if (!Visit(neighbor)) return false;
            captured.Add(area); return true;
        }
    }

    internal static IEnumerable<int> ReadNeighbors(GameState state, SourceWorldContext current, int area)
        => new Queries(state, current, null).Neighbors(area);

    private sealed class Queries(GameState state, SourceWorldContext current,
        (int AreaId, int ForceId)? assumedOwnership)
    {
        internal AreaReference Area(int id) => current.ReadAreaTableReference(id is >= 0 and <= 500 ? id : 0)
            ?? throw Missing("area_pointer");
        internal int Owner(int id)
        {
            var area = Area(id);
            if (!Known(current.ReadAreaNativeValidity(area), "area_validity")) return 0;
            //42F7C0 evaluates one prospective border Area as owned by the
            //attacking Force. Actual capture consumers keep the default view.
            return assumedOwnership is { } assumed && assumed.AreaId == area.PhysicalSlot
                ? assumed.ForceId : Known(current.ReadAreaCurrentForceSlot(area), "area_owner");
        }
        internal byte Flags(int id) => Known(current.ReadAreaFlags38(Area(id)), "area_flags");
        internal IEnumerable<int> Neighbors(int id)
        {
            var area = Area(id);
            if (area.PhysicalSlot is not int physical || state.RegionStatistics is not { } statistics ||
                !statistics.Entries.TryGetValue(physical, out var entry))
                throw Missing("area_neighbor_mask");
            byte mask = entry.NeighborMask;
            for (int index = 0; index < 8; index++)
            {
                ushort raw = Known(current.ReadAreaNeighborSlot(area, index), "area_neighbor");
                if (raw == 0 || (mask & (1 << index)) == 0) continue;
                var neighbor = Area(raw <= 500 ? raw : 0);
                if (Known(current.ReadAreaNativeValidity(neighbor), "neighbor_validity"))
                    yield return neighbor.PhysicalSlot ?? throw Missing("neighbor_identity");
            }
        }
    }

    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.encirclement_" + field);
}
