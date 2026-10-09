namespace San14.Core;

/// <summary>Immutable map facts supplied by a source adapter; no original file format enters Core.</summary>
public interface IHexTravelMap
{
    string MapSha256 { get; }
    int Width { get; }
    int Height { get; }
    IReadOnlyList<int> RadiusOneCandidates(int hexId);
    byte SurfaceCode(int hexId);
    int? AreaId(int hexId);
}

/// <summary>Temporary route policy for the explicitly provisional playable ruleset.</summary>
public sealed class PlayableLandRoutes(IHexTravelMap map)
{
    public const int StepsPerPeriod = 6;
    private readonly IHexTravelMap _map = map ?? throw new ArgumentNullException(nameof(map));

    public void ValidateWorld(GameState state)
    {
        if (state.MapWidth != _map.Width || state.MapHeight != _map.Height ||
            !state.MapSha256.Equals(_map.MapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Movement map does not match the current world source.");
    }

    // For this temporary ruleset, only cells with a source area can be entered.
    // Source area zero does not establish original impassability; the original
    // terrain/formation policy must replace this decision later.
    public bool CanEnter(int hexId) => hexId >= 0 && hexId < checked(_map.Width * _map.Height) &&
        _map.AreaId(hexId) is not null;

    /// <summary>Topological hex radius, independent of movement passability.</summary>
    public bool WithinRadius(int origin, int target, int radius)
    {
        if (origin == target) return true;
        if (radius < 1) return false;
        var visited = new HashSet<int> { origin };
        var frontier = new List<int> { origin };
        for (int depth = 0; depth < radius; depth++)
        {
            var next = new List<int>();
            foreach (int hex in frontier)
                foreach (int neighbor in _map.RadiusOneCandidates(hex))
                    if (visited.Add(neighbor))
                    {
                        if (neighbor == target) return true;
                        next.Add(neighbor);
                    }
            frontier = next;
        }
        return false;
    }

    /// <summary>Deterministic shortest route in the supplied candidate topology, excluding start.</summary>
    public int[] Route(int startHexId, int destinationHexId)
    {
        int length = checked(_map.Width * _map.Height);
        if (startHexId < 0 || startHexId >= length || !CanEnter(startHexId) || !CanEnter(destinationHexId))
            return [];
        if (startHexId == destinationHexId) return [];
        var previous = new int[length];
        Array.Fill(previous, -1);
        var queue = new Queue<int>();
        previous[startHexId] = startHexId;
        queue.Enqueue(startHexId);
        while (queue.Count > 0 && previous[destinationHexId] < 0)
        {
            int from = queue.Dequeue();
            foreach (int neighbor in _map.RadiusOneCandidates(from))
            {
                if (neighbor < 0 || neighbor >= length || previous[neighbor] >= 0 || !CanEnter(neighbor)) continue;
                previous[neighbor] = from;
                queue.Enqueue(neighbor);
                if (neighbor == destinationHexId) break;
            }
        }
        if (previous[destinationHexId] < 0) return [];
        var reversed = new List<int>();
        for (int cell = destinationHexId; cell != startHexId; cell = previous[cell]) reversed.Add(cell);
        reversed.Reverse();
        return reversed.ToArray();
    }

    /// <summary>Shortest route to the first matching reachable cell in source candidate order.</summary>
    public int[] RouteToNearest(int startHexId, Func<int, bool> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);
        int length = checked(_map.Width * _map.Height);
        if (!CanEnter(startHexId)) return [];
        var previous = new int[length];
        Array.Fill(previous, -1);
        var queue = new Queue<int>();
        previous[startHexId] = startHexId;
        queue.Enqueue(startHexId);
        while (queue.Count > 0)
        {
            int from = queue.Dequeue();
            foreach (int neighbor in _map.RadiusOneCandidates(from))
            {
                if (neighbor < 0 || neighbor >= length || previous[neighbor] >= 0 ||
                    !CanEnter(neighbor)) continue;
                previous[neighbor] = from;
                if (matches(neighbor))
                {
                    var reversed = new List<int>();
                    for (int cell = neighbor; cell != startHexId; cell = previous[cell])
                        reversed.Add(cell);
                    reversed.Reverse();
                    return reversed.ToArray();
                }
                queue.Enqueue(neighbor);
            }
        }
        return [];
    }

    /// <summary>Area-local breadth-first order for provisional gradual occupation.</summary>
    public int[] NearestAreaCells(int centerHexId, int areaId,
        Func<int, bool> matches, int maximum)
    {
        ArgumentNullException.ThrowIfNull(matches);
        if (maximum < 1 || !CanEnter(centerHexId) || _map.AreaId(centerHexId) != areaId)
            return [];
        var selected = new List<int>(maximum);
        var visited = new HashSet<int> { centerHexId };
        var queue = new Queue<int>();
        queue.Enqueue(centerHexId);
        while (queue.Count > 0 && selected.Count < maximum)
        {
            int hex = queue.Dequeue();
            if (matches(hex)) selected.Add(hex);
            foreach (int neighbor in _map.RadiusOneCandidates(hex))
                if (neighbor >= 0 && neighbor < checked(_map.Width * _map.Height) &&
                    _map.AreaId(neighbor) == areaId && visited.Add(neighbor))
                    queue.Enqueue(neighbor);
        }
        return selected.ToArray();
    }

    /// <summary>Shortest distance under a caller-supplied land permission rule.</summary>
    public int? DistanceThrough(int startHexId, int destinationHexId,
        Func<int, bool> mayUse)
    {
        ArgumentNullException.ThrowIfNull(mayUse);
        int length = checked(_map.Width * _map.Height);
        if (!CanEnter(startHexId) || !CanEnter(destinationHexId) ||
            !mayUse(startHexId) || !mayUse(destinationHexId))
            return null;
        if (startHexId == destinationHexId) return 0;
        var distance = new int[length];
        Array.Fill(distance, -1);
        var queue = new Queue<int>();
        distance[startHexId] = 0;
        queue.Enqueue(startHexId);
        while (queue.Count > 0)
        {
            int from = queue.Dequeue();
            foreach (int neighbor in _map.RadiusOneCandidates(from))
            {
                if (neighbor < 0 || neighbor >= length || distance[neighbor] >= 0 ||
                    !CanEnter(neighbor) || !mayUse(neighbor)) continue;
                int nextDistance = distance[from] + 1;
                if (neighbor == destinationHexId) return nextDistance;
                distance[neighbor] = nextDistance;
                queue.Enqueue(neighbor);
            }
        }
        return null;
    }
}
