namespace San14.Data.AI.Original;

/// <summary>The transport selectors' Area graph, distinct from the Army Hex route.
/// Search runs from the target, preserving source neighbor order for equal costs.</summary>
internal static class OriginalAiTransportAreaRoute
{
    internal static (bool Found, IReadOnlyList<int> Areas, int Cost) Resolve(
        SourceWorldContext current, int startAreaId, int targetAreaId, bool selectTransportRoute,
        Func<BaseReference, IReadOnlyList<BaseReference>>? readNeighborCities = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        CheckArea(startAreaId); CheckArea(targetAreaId);
        if (selectTransportRoute && readNeighborCities is null) throw Missing("City_neighbors22F430");
        //0B5FE0 returns before querying the graph;097730 likewise recognizes
        //its initial target node before expanding any neighbor inputs.
        if (startAreaId == targetAreaId) return (true, new[] { startAreaId }, 0);

        var graph = new Graph(current, selectTransportRoute, readNeighborCities);
        var parent = new Dictionary<int, int> { [targetAreaId] = -1 };
        var distance = new Dictionary<int, int> { [targetAreaId] = 0 };
        var open = new Queue<int>();
        open.Enqueue(targetAreaId);
        while (open.TryDequeue(out int at))
        {
            if (at == startAreaId)
            {
                var path = new List<int>();
                for (int next = at; next != -1; next = parent[next]) path.Add(next);
                return (true, path.ToArray(), distance[at]);
            }
            //097730 uses cost+1, with equal-priority insertion to the right
            //and no equal-cost parent replacement. FIFO is the same ordering
            //for this unit-weight graph; no heuristic or Hex cost belongs here.
            foreach (int next in graph.Neighbors(at))
            {
                if (distance.ContainsKey(next)) continue;
                parent.Add(next, at);
                distance.Add(next, unchecked(distance[at] + 1));
                open.Enqueue(next);
            }
        }
        //0B5FE0 exposes INT_MAX on an exhausted search, not a guessed route.
        return (false, Array.Empty<int>(), int.MaxValue);
    }

    private sealed class Graph(SourceWorldContext current, bool transport,
        Func<BaseReference, IReadOnlyList<BaseReference>>? readNeighborCities)
    {
        private readonly Dictionary<int, int[]> areaNeighbors = [];
        private readonly Dictionary<BaseReference, int[]> cityNeighbors = [];

        internal IReadOnlyList<int> Neighbors(int id)
        {
            if (areaNeighbors.TryGetValue(id, out var found)) return found;
            var area = current.ReadAreaTableReference(id)
                ?? throw Missing("Area_table_pointer");
            int[] neighbors;
            if (transport)
            {
                //097730's nonzero flag cell: Area35 -> City ->22F430 ->
                //229FD0 representative Area. It does not use Area56 here.
                int cityId = Known(current.ReadAreaCitySlot(area), "Area35");
                var city = current.ReadCityTableReference(cityId < 52 ? cityId : 0)
                    ?? throw Missing("Area35_City_pointer");
                neighbors = CityAreas(city);
            }
            else
            {
                //22F1C0 walks the eight serialized words in source order,
                //ignoring zero and native-invalid neighbors. No mask/Force gate.
                var values = new List<int>(8);
                for (int index = 0; index < 8; index++)
                {
                    ushort raw = Known(current.ReadAreaNeighborSlot(area, index), "Area56_neighbor_" + index);
                    if (raw == 0) continue;
                    var next = current.ReadAreaTableReference(raw < 501 ? raw : 0)
                        ?? throw Missing("neighbor_Area_pointer");
                    if (!Known(current.ReadAreaNativeValidity(next), "neighbor_Area_validity")) continue;
                    int physical = Known(next.PhysicalSlot, "neighbor_Area_identity");
                    CheckArea(physical);
                    values.Add(physical);
                }
                neighbors = values.ToArray();
            }
            areaNeighbors.Add(id, neighbors);
            return neighbors;
        }

        private int[] CityAreas(BaseReference city)
        {
            if (cityNeighbors.TryGetValue(city, out var found)) return found;
            //The operation's real22F430 query owns Gate traversal, ordered
            //pointer de-duplication and temporary-list lifecycle. Reuse its
            //existing per-plan cache instead of loading another resource view.
            var cities = readNeighborCities!(city) ?? throw Missing("neighbor_City_list");
            var values = new List<int>(cities.Count);
            foreach (var nextCity in cities)
            {
                if (nextCity is not { Kind: BaseKind.City, PhysicalSlot: not null })
                    throw Missing("neighbor_City_receiver");
                int physical = Known(CurrentGateMapQueries.FindArea(nextCity, current).PhysicalSlot,
                    "neighbor_City_representative_Area");
                CheckArea(physical);
                //The source adds these representative Area pointers with
                //no second native-validity filter after229FD0.
                values.Add(physical);
            }
            var result = values.ToArray();
            cityNeighbors.Add(city, result);
            return result;
        }
    }

    private static void CheckArea(int id)
    {
        if ((uint)id >= 501) throw Missing("Area_identity_range");
    }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("original_ai.transport_area_route." + field);
}
