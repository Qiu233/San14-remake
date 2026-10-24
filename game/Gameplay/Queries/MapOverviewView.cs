using San14.Core;
using San14.Data.Formats;

namespace San14.Client.Gameplay;

internal readonly record struct MapOverviewOwnership(int? AreaId, int? ForceId);
internal readonly record struct MapOverviewMarker(int HexId, int? ForceId);
internal sealed record MapOverviewView(MapOverviewContent Content, bool UsesPlayableRules,
    IReadOnlyList<MapOverviewOwnership>? Ownership, IReadOnlyList<MapOverviewMarker> Gates,
    IReadOnlyList<MapOverviewMarker> Cities, IReadOnlyList<MapOverviewMarker> Units)
{
    public static MapOverviewView Capture(MapOverviewContent content, GameState? world)
    {
        if (world is null) return new(content, false, null, [], [], []);
        return new(content, world.RulesetId == PlayableSkeleton.RulesetId,
            Array.AsReadOnly(content.Cells.Select(cell =>
            {
                TerritoryState tile = world.Territory[cell.Id];
                return new MapOverviewOwnership(tile.AreaId, tile.ForceId);
            }).ToArray()),
            Array.AsReadOnly(world.Gates.Values.Where(gate => gate.HexId is not null)
                .Select(gate => new MapOverviewMarker(gate.HexId!.Value, null)).ToArray()),
            Array.AsReadOnly(world.Cities.Values.Where(city => city.HexId is not null)
                .Select(city => new MapOverviewMarker(city.HexId!.Value, city.ForceId)).ToArray()),
            Array.AsReadOnly(world.Units.Values.OrderBy(unit => unit.Id)
                .Select(unit => new MapOverviewMarker(unit.HexId, unit.ForceId)).ToArray()));
    }

    public static string DescribeTile(MapOverviewContent content, int hexId, GameState? world, PreviewText text)
    {
        MapCell cell = content.Cells[hexId];
        string? locationName = world?.Cities.Values.FirstOrDefault(city => city.HexId == hexId)?.Name ??
            world?.Gates.Values.FirstOrDefault(gate => gate.HexId == hexId)?.Name;
        string location = locationName is null ? "" : $"{text.MapSeparator}{text.Location} {locationName}";
        string region = "";
        if (world is not null)
        {
            TerritoryState tile = world.Territory[hexId];
            string? areaName = tile.AreaId is int areaId ? world.Areas.GetValueOrDefault(areaId)?.Name : null;
            string? forceName = tile.ForceId is int forceId ? world.Forces.GetValueOrDefault(forceId)?.Name : null;
            string owner = tile.ForceId is null ? text.Neutral : forceName ?? tile.ForceId.Value.ToString();
            region = $"{text.MapSeparator}{text.Region} {areaName ?? (tile.AreaId ?? 0).ToString()}" +
                $"{text.MapSeparator}{text.Force} {owner}";
        }
        return $"{text.Cell} {hexId} ({hexId % content.Width}, {hexId / content.Width})" +
            $"{text.MapSeparator}{text.Surface} {cell.SurfaceCode}{text.MapSeparator}" +
            $"{text.RawFields} {cell.Field1:X8} / {cell.Field2:X8} / {cell.Field3:X8}{region}{location}";
    }
}
