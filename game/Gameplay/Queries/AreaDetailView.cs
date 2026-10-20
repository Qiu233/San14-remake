using System.Globalization;
using San14.Core;
using San14.Data;

namespace San14.Client.Gameplay;

internal enum AreaOwnershipCoverage { BelowHalf, Partial, Complete }
internal enum AreaPlaceKind { City, Gate, Port }

internal sealed record AreaDetailView(DetailContent Content, int AreaId, AreaOwnershipCoverage? OwnershipCoverage)
{
    internal AreaPlaceKind? PlaceKind { get; init; }
    internal DetailForceAppearance? ForceAppearance { get; init; }
    internal IReadOnlyList<DetailForceAppearance?> NeighborFlags { get; init; } = [];
    internal DetailMiniMapValues? MiniMap { get; init; }

    internal static AreaDetailView Create(GameState world, ScenarioCityPreview snapshot, int areaId, PreviewText text,
        CeAreaDetailQueries queries, byte? representativeSurfaceCode = null,
        IReadOnlyDictionary<int, DetailForceAppearance?>? forceAppearances = null, DetailMiniMapValues? miniMap = null)
    {
        AreaState area = world.Areas[areaId];
        CityState city = world.Cities[area.CityId];
        string unknown = text.Get("detail_unknown");
        int? overseer = area.Administration?.OverseerId;
        CeAreaDetailValues? current = queries.Read(world, areaId);
        DetailForceAppearance? forceAppearance = current?.CurrentForceId is int currentForce
            ? forceAppearances?.GetValueOrDefault(currentForce) : null;
        var neighborFlags = new DetailForceAppearance?[8];
        var values = new Dictionary<string, string>
        {
            ["area"] = area.Name, ["city"] = city.Name,
            ["force"] = forceAppearance?.DisplayText ?? unknown,
            ["overseer"] = overseer is int person ? world.Officers[person].Name : area.Administration is null ? unknown : "",
            ["district"] = city.DistrictId is int districtId ? text.DistrictNumber(world.Districts[districtId].Number) : unknown,
            ["security"] = area.Security?.ToString(CultureInfo.InvariantCulture) ?? unknown,
            ["securityCapacity"] = "100",
            ["population"] = Number(current?.Population), ["goldIncome"] = unknown, ["suppliesIncome"] = unknown,
            ["troopIncome"] = unknown, ["landmarks"] = unknown, ["goldEffect"] = unknown,
            ["suppliesEffect"] = unknown, ["troopEffect"] = unknown,
            ["owned"] = current?.HasValidOwner == false ? "" : Number(current?.OwnedCells),
            ["ownedSlash"] = current?.HasValidOwner == false ? "" : "/",
            ["total"] = current?.HasValidOwner == false ? "" : Number(current?.TerrainCells),
            ["constructionLimit"] = unknown
        };
        foreach (var (development, display) in new[] { ("commerce", current?.Commerce), ("agriculture", current?.Agriculture), ("barracks", current?.Barracks) })
        {
            values[development + "Level"] = display is null ? unknown : $"Lv{display.Level}";
            values[development + "Progress"] = Number(display?.Progress);
            values[development + "Capacity"] = display?.ShowProgressSeparator == false ? "" : Number(display?.RequiredProgress);
            values[development + "Slash"] = display?.ShowProgressSeparator == false ? "" : "/";
        }
        for (int i = 0; i < 8; i++)
        {
            AreaState? neighbor = area.AdjacentAreaIds is { } adjacent && i < adjacent.Length ? world.Areas[adjacent[i]] : null;
            values[$"neighbor{i}Name"] = neighbor?.Name ?? "";
            var neighborValues = neighbor is null ? null : queries.Read(world, neighbor.Id);
            bool neutral = neighborValues?.HasValidOwner == false;
            neighborFlags[i] = neighborValues?.CurrentForceId is int neighborForce
                ? forceAppearances?.GetValueOrDefault(neighborForce) : null;
            values[$"neighbor{i}Owned"] = neighbor is null || neutral ? "" : Number(neighborValues?.OwnedCells);
            values[$"neighbor{i}Total"] = neighbor is null || neutral ? "" : Number(neighborValues?.TerrainCells);
            values[$"neighbor{i}Slash"] = neighbor is null || neutral ? "" : "/";
        }
        int? portrait = snapshot.Characters.FirstOrDefault(p => p.CharacterId != 0 && p.CharacterId == overseer)?.AppearanceCode;
        AreaOwnershipCoverage? coverage = current?.HasValidOwner == true &&
            current.OwnedCells is int owned && current.TerrainCells is int total
            ? (float)owned / total < .5f ? AreaOwnershipCoverage.BelowHalf :
                owned == total ? AreaOwnershipCoverage.Complete : AreaOwnershipCoverage.Partial
            : null;
        return new(new(values, portrait, area.Name), areaId, coverage)
        {
            PlaceKind = representativeSurfaceCode switch
                { 16 => AreaPlaceKind.City, 17 => AreaPlaceKind.Gate, 18 => AreaPlaceKind.Port, _ => null },
            ForceAppearance = forceAppearance, NeighborFlags = Array.AsReadOnly(neighborFlags), MiniMap = miniMap
        };
        string Number(int? number) => number?.ToString(CultureInfo.InvariantCulture) ?? unknown;
    }
}
