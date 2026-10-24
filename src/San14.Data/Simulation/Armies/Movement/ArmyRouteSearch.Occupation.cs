using San14.Core;

namespace San14.Data;

internal static partial class ArmyRouteSearch
{
    //2B1AF0's nonadjacent leg uses1845B0(mode1, flags21). Reuse the same
    //183C00 tree, scaled edge costs and source queue ordering. Bit20 omits
    //the occupied-Army surcharge; it does not forbid occupied cells.
    internal static int? OccupationPathCost(GameState state, SourceWorldContext current,
        UnitState army, int from, int to)
    {
        var query = new Queries(state, current, new LiveMovement(state, current, army),
            from, to, 9, checked((ushort)to), null, 0x21);
        var path = Search(query, out _, out int cost);
        return path.Length == 0 ? null : cost;
    }
}
