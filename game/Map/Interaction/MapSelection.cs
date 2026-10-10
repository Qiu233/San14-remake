using San14.Core;

namespace San14.Client.MapInteraction;

public sealed record MapSelectionLocation(int Id, string Name);
public sealed record MapSelectionUnit(int Id, string CommanderName, int Troops);
public sealed record MapSelectionStructure(string KindId, int Durability, int MaxDurability);

/// <summary>Values for the selected cell, independent of controls and the mutable campaign world.</summary>
public sealed record MapCellSelection(
    int HexId, string? AreaName, string? ForceName,
    IReadOnlyList<string> LocationNames, MapSelectionStructure? Structure,
    IReadOnlyList<MapSelectionUnit> Units,
    MapSelectionLocation? CityDetails, MapSelectionLocation? LocationCommand,
    MapSelectionLocation? AreaCommand)
{
    public MapSelectionUnit? SelectedUnit { get; init; }
    public int PlayerUnitCount { get; init; }
}

/// <summary>Owns map command selection. Metadata and rendering only mirror this state.</summary>
public sealed class MapSelection
{
    public MapCellSelection? Current { get; private set; }

    public void Clear() => Current = null;

    public MapCellSelection Select(GameState world, int hexId, int? playerForceId)
    {
        if (!world.Territory.TryGetValue(hexId, out TerritoryState? territory))
            throw new InvalidDataException($"Picked map cell {hexId} is absent from the current territory state.");

        var cities = world.Cities.Values.Where(city => city.HexId == hexId).ToArray();
        var gates = world.Gates.Values.Where(gate => gate.HexId == hexId).ToArray();
        var commands = cities.Where(city => city.ForceId == playerForceId)
            .Select(city => new MapSelectionLocation(city.Id, city.Name))
            .Concat(gates.Where(gate => gate.ForceId == playerForceId)
                .Select(gate => new MapSelectionLocation(gate.Id, gate.Name))).ToArray();
        MapSelectionLocation? areaCommand = territory.AreaId is int areaId &&
            world.Areas.TryGetValue(areaId, out AreaState? area) && area.ForceId == playerForceId &&
            world.Cities.TryGetValue(area.CityId, out CityState? areaCity) &&
            areaCity.ForceId == playerForceId && areaCity.AreaIds.Contains(areaId)
                ? new(areaId, area.Name) : null;
        var ownUnits = PlayerUnits(world, hexId, playerForceId);
        int? previousUnit = Current is { } previous && previous.HexId == hexId ? previous.SelectedUnit?.Id : null;
        var selectedUnit = ownUnits.FirstOrDefault(unit => unit.Id == previousUnit) ?? ownUnits.FirstOrDefault();
        var building = world.Structures?.Values.FirstOrDefault(item => item.HexId == hexId);

        Current = new(hexId,
            territory.AreaId is int namedArea ? world.Areas[namedArea].Name : null,
            territory.ForceId is int forceId ? world.Forces[forceId].Name : null,
            Array.AsReadOnly(cities.OrderBy(item => item.Id).Select(item => item.Name)
                .Concat(gates.OrderBy(item => item.Id).Select(item => item.Name)).ToArray()),
            building is null ? null : new(building.KindId, building.Durability, building.MaxDurability),
            Array.AsReadOnly(world.Units.Values.Where(unit => unit.HexId == hexId)
                .OrderBy(unit => unit.Id).Select(unit => Describe(world, unit)).ToArray()),
            cities.Length == 1 ? new(cities[0].Id, cities[0].Name) : null,
            commands.Length == 1 ? commands[0] : null, areaCommand)
            { SelectedUnit = selectedUnit, PlayerUnitCount = ownUnits.Length };
        return Current;
    }

    public MapSelectionUnit? CycleUnit(GameState world, int? playerForceId)
    {
        if (Current is null || playerForceId is null) return null;
        var units = PlayerUnits(world, Current.HexId, playerForceId);
        if (units.Length < 2) return null;
        int index = Array.FindIndex(units, unit => unit.Id == Current.SelectedUnit?.Id);
        var next = units[(index + 1) % units.Length];
        Current = Current with { SelectedUnit = next, PlayerUnitCount = units.Length };
        return next;
    }

    public MapSelectionUnit? SelectUnit(GameState world, int? playerForceId, int unitId)
    {
        if (Current is null || !world.Units.TryGetValue(unitId, out var unit) || unit.ForceId != playerForceId)
            return null;
        var selected = Describe(world, unit);
        Current = Current with { SelectedUnit = selected };
        return selected;
    }

    private static MapSelectionUnit[] PlayerUnits(GameState world, int hexId, int? playerForceId) =>
        world.Units.Values.Where(unit => unit.HexId == hexId && unit.ForceId == playerForceId)
            .OrderBy(unit => unit.Id).Select(unit => Describe(world, unit)).ToArray();

    private static MapSelectionUnit Describe(GameState world, UnitState unit) =>
        new(unit.Id, world.Officers[unit.OfficerId].Name, unit.Troops);
}
