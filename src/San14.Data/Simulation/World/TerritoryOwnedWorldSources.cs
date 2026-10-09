using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using San14.Core;

namespace San14.Data;

public enum TerritoryForceColorOrigin { ScenarioSource, CurrentOverride, UnresolvedCurrentForce }

/// <summary>A null current color explicitly suppresses the scenario color for this force.</summary>
public sealed record TerritoryForceColorOverride(int ForceId, byte? CurrentColorIndex, string Provenance);
public sealed record TerritorySourceForceColor(int ForceId, byte? RawReportColor49);
public sealed record TerritoryForceColorResolution(int ForceId, byte? ColorIndex,
    TerritoryForceColorOrigin Origin, string Provenance, bool CurrentForcePresent)
{
    // Native 60 is a known no-tint sentinel; larger bytes need a separate source rule.
    public bool IsSupportedPaletteIndex => ColorIndex is <= 60;
}

/// <summary>Source ownership is evidence only; CurrentForceId is the captured world owner.</summary>
public sealed record TerritoryCityOwnership(int CityId, bool CurrentPresent,
    string? CurrentName, int? CurrentForceId, int? CurrentHexId, bool SourcePresent,
    string? SourceName, int? SourceForceId, int? SourceHexId);

public sealed record TerritoryOwnedSourceIdentity(string ScenarioKey, int ScenarioNumber,
    string Locale, string ScenarioResourceUri, string ScenarioSha256, string DecodedScenarioSha256,
    string FixedDataResourceUri, string FixedDataSha256, string MapResourceUri, string MapSha256);

/// <summary>The provider's constructor allocation has seven zero layers. Only layer0 is
/// supplied by current Core ownership; later native overlay producers remain separate.</summary>
public sealed record TerritoryLayerScope(int LayerCount, int CurrentOwnershipLayer,
    bool ConstructorInitialSevenLayers, bool NativeOverlayUpdatesProvided);

/// <summary>A detached render input. Each world copy is independently mutable; mutations
/// to the owner or an earlier copy cannot change this snapshot.</summary>
public sealed class TerritoryOwnedWorldSnapshot
{
    private readonly byte[] _worldJson;
    public TerritoryOwnedSourceIdentity SourceIdentity { get; }
    public TerritoryStyleSelection Style { get; }
    public long StyleInvalidationGeneration { get; }
    public string WorldSnapshotSha256 { get; }
    public IReadOnlyDictionary<int, TerritorySourceForceColor> SourceForceColors { get; }
    public IReadOnlyDictionary<int, TerritoryForceColorResolution> ForceColors { get; }
    public IReadOnlyDictionary<int, byte> SourceForceColorIndices { get; }
    public IReadOnlyList<TerritoryCityOwnership> CityOwnership { get; }
    public IReadOnlyList<int> UnknownOwnedForceIds { get; }
    public IReadOnlyList<int> UnsupportedOwnedForceColorIds { get; }
    public TerritoryLayerScope LayerScope { get; } = new(7, 0, true, false);

    internal TerritoryOwnedWorldSnapshot(byte[] worldJson, TerritoryOwnedSourceIdentity identity,
        TerritoryStyleSelection style, long generation,
        Dictionary<int, TerritorySourceForceColor> sourceColors,
        Dictionary<int, TerritoryForceColorResolution> colors, Dictionary<int, byte> indices,
        TerritoryCityOwnership[] cities, int[] unknown, int[] unsupported)
    {
        _worldJson = worldJson;
        SourceIdentity = identity;
        Style = style;
        StyleInvalidationGeneration = generation;
        WorldSnapshotSha256 = Convert.ToHexString(SHA256.HashData(worldJson));
        SourceForceColors = new ReadOnlyDictionary<int, TerritorySourceForceColor>(sourceColors);
        ForceColors = new ReadOnlyDictionary<int, TerritoryForceColorResolution>(colors);
        SourceForceColorIndices = new ReadOnlyDictionary<int, byte>(indices);
        CityOwnership = Array.AsReadOnly(cities);
        UnknownOwnedForceIds = Array.AsReadOnly(unknown);
        UnsupportedOwnedForceColorIds = Array.AsReadOnly(unsupported);
    }

    public GameState CreateWorldCopy() => JsonSerializer.Deserialize<GameState>(_worldJson)
        ?? throw new InvalidDataException("The captured territory world could not be restored.");
}

/// <summary>Joins presentation source colors with an explicitly owned current world.
/// Capture on the world's owner thread before handing the result to an async renderer.</summary>
public static class TerritoryOwnedWorldSources
{
    public static TerritoryOwnedWorldSnapshot Capture(GameState ownedWorld,
        LoadedScenario scenario, LoadedMap map, TerritoryStyleSelection explicitSelection,
        long generation, IReadOnlyList<TerritoryForceColorOverride>? currentOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(ownedWorld);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(explicitSelection);
        _ = TerritoryStyleSources.CacheZoomValue(explicitSelection);

        // The same detached object supplies validation, metadata and renderer bytes.
        byte[] worldJson = JsonSerializer.SerializeToUtf8Bytes(ownedWorld);
        GameState world = JsonSerializer.Deserialize<GameState>(worldJson)
            ?? throw new InvalidDataException("The owned territory world could not be captured.");
        TerritoryOwnedSourceIdentity identity = Validate(world, scenario, map);
        var sourceColors = new Dictionary<int, TerritorySourceForceColor>();
        var colors = new Dictionary<int, TerritoryForceColorResolution>();
        var indices = new Dictionary<int, byte>();
        foreach (ForcePreviewRecord force in scenario.Snapshot.Forces)
        {
            if (force is null || force.Slot < 0 || !sourceColors.TryAdd(force.Slot, new(force.Slot, force.RawReportColor49)))
                throw new InvalidDataException("Invalid or duplicate source force slot.");
            if (force.Slot == 0) continue;
            colors.Add(force.Slot, new(force.Slot, force.RawReportColor49,
                TerritoryForceColorOrigin.ScenarioSource,
                $"{identity.ScenarioResourceUri}#force/{force.Slot}/RawReportColor49",
                world.Forces.ContainsKey(force.Slot)));
            if (force.RawReportColor49 is byte color) indices.Add(force.Slot, color);
        }
        foreach (int forceId in world.Forces.Keys)
            colors.TryAdd(forceId, new(forceId, null, TerritoryForceColorOrigin.UnresolvedCurrentForce,
                "current-force-color-unprovided", true));
        var overridden = new HashSet<int>();
        foreach (TerritoryForceColorOverride value in currentOverrides ?? [])
        {
            if (value is null || value.ForceId <= 0 || !world.Forces.ContainsKey(value.ForceId) ||
                !overridden.Add(value.ForceId) || string.IsNullOrWhiteSpace(value.Provenance))
                throw new InvalidDataException("Current force colors require unique current force IDs and explicit provenance.");
            colors[value.ForceId] = new(value.ForceId, value.CurrentColorIndex,
                TerritoryForceColorOrigin.CurrentOverride, value.Provenance, true);
            if (value.CurrentColorIndex is byte color) indices[value.ForceId] = color;
            else indices.Remove(value.ForceId);
        }

        Dictionary<int, CityPreviewRecord> sourceCities = [];
        foreach (CityPreviewRecord city in scenario.Snapshot.Cities)
            if (city is null || city.Slot < 0 || !sourceCities.TryAdd(city.Slot, city))
                throw new InvalidDataException("Invalid or duplicate source city slot.");
        TerritoryCityOwnership[] cities = sourceCities.Keys.Concat(world.Cities.Keys)
            .Where(id => id != 0).Distinct().Order().Select(id =>
            {
                world.Cities.TryGetValue(id, out CityState? current);
                sourceCities.TryGetValue(id, out CityPreviewRecord? source);
                return new TerritoryCityOwnership(id, current is not null, current?.Name,
                    current?.ForceId, current?.HexId, source is not null, source?.Name,
                    source is null || source.ForceSlot == 0 ? null : source.ForceSlot, source?.HexId);
            }).ToArray();
        int[] ownedForceIds = world.Territory.Values.Where(row => row.ForceId.HasValue)
            .Select(row => row.ForceId!.Value).Distinct().Order().ToArray();
        return new(worldJson, identity, explicitSelection with { }, generation, sourceColors, colors, indices, cities,
            ownedForceIds.Where(id => colors[id].ColorIndex is null).ToArray(),
            ownedForceIds.Where(id => colors[id].ColorIndex is > 60).ToArray());
    }

    /// <summary>Explicit discrete source zoom; every caller-selected map control is retained.</summary>
    public static TerritoryStyleSelection WithDiscreteSourceZoom(TerritoryStyleSources styles,
        TerritoryStyleSelection explicitSelection, int nativeBand)
    {
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(explicitSelection);
        _ = TerritoryStyleSources.CacheZoomValue(explicitSelection);
        return explicitSelection with { ZoomBand = nativeBand, InterpolationPercent = 0,
            ZoomValue = styles.ZoomAnchor(nativeBand) };
    }

    /// <summary>Known shader mask values2/4 only; other mask bits and provider flags are retained.</summary>
    public static TerritoryStyleSelection WithFillAndBorderVisibility(
        TerritoryStyleSelection explicitSelection, bool fillVisible, bool borderVisible)
    {
        ArgumentNullException.ThrowIfNull(explicitSelection);
        _ = TerritoryStyleSources.CacheZoomValue(explicitSelection);
        return explicitSelection with { ScreenMapMask = (explicitSelection.ScreenMapMask & ~6u) |
            (fillVisible ? 2u : 0u) | (borderVisible ? 4u : 0u) };
    }

    private static TerritoryOwnedSourceIdentity Validate(GameState world, LoadedScenario scenario, LoadedMap map)
    {
        if (world.ContentHashes is null || world.Forces is null || world.Cities is null || world.Territory is null ||
            scenario.Metadata is null || scenario.Snapshot is null || scenario.ScenarioSource is null ||
            scenario.FixedDataSource is null || scenario.Snapshot.Forces is null || scenario.Snapshot.Cities is null ||
            map.Map is null || map.Source is null || map.Map.Cells is null)
            throw new InvalidDataException("Incomplete owned territory source inputs.");
        string scenarioKey = GameResourceId.File(scenario.Metadata.SourcePath).ToString();
        if (scenario.Snapshot.SourcePath != scenario.Metadata.SourcePath ||
            scenario.ScenarioSource.SourcePath != scenario.Metadata.SourcePath ||
            scenario.Snapshot.FixedDataPath != scenario.FixedDataSource.SourcePath ||
            !StringComparer.OrdinalIgnoreCase.Equals(scenario.Snapshot.DecodedSha256, scenario.Metadata.DecodedSha256) ||
            world.ScenarioKey != scenarioKey)
            throw new InvalidDataException("Owned territory and loaded scenario identities differ.");
        ValidateHash(scenario.Metadata.DecodedSha256);
        foreach (SourceProvenance source in new[] { scenario.ScenarioSource, scenario.FixedDataSource, map.Source })
        {
            ValidateHash(source.Sha256);
            if (source.ResourceUri != GameResourceId.File(source.SourcePath).ToString() ||
                !world.ContentHashes.TryGetValue(source.ResourceUri, out string? expected) ||
                !StringComparer.OrdinalIgnoreCase.Equals(expected, source.Sha256))
                throw new InvalidDataException("Owned territory content hashes differ from the loaded scenario, fixed data or map.");
        }
        if (world.MapSha256 != map.Source.Sha256 || world.MapWidth != 220 || world.MapHeight != 220 ||
            map.Map.Width != 220 || map.Map.Height != 220 || map.Map.Cells.Length != 48400 ||
            world.Territory.Count != 48400)
            throw new InvalidDataException("Owned territory requires the matching complete original 220x220 map.");
        foreach ((int id, ForceState force) in world.Forces)
            if (force is null || id <= 0 || force.Id != id)
                throw new InvalidDataException("Invalid current force identity in territory world.");
        for (int id = 0; id < 48400; id++)
        {
            if (map.Map.Cells[id].Id != id || !world.Territory.TryGetValue(id, out TerritoryState? row) ||
                row is null || row.Id != id || row.ForceId is int forceId && !world.Forces.ContainsKey(forceId))
                throw new InvalidDataException("Current territory has a missing cell, invalid identity or unavailable current force.");
        }
        foreach ((int id, CityState city) in world.Cities)
            if (city is null || id <= 0 || city.Id != id ||
                city.ForceId is int forceId && !world.Forces.ContainsKey(forceId) ||
                city.HexId is int hexId && (uint)hexId >= 48400)
                throw new InvalidDataException("Invalid current city ownership or map position.");
        return new(scenarioKey, scenario.Snapshot.Number, scenario.Snapshot.Locale,
            scenario.ScenarioSource.ResourceUri, scenario.ScenarioSource.Sha256, scenario.Metadata.DecodedSha256,
            scenario.FixedDataSource.ResourceUri, scenario.FixedDataSource.Sha256, map.Source.ResourceUri, map.Source.Sha256);
    }

    private static void ValidateHash(string sha256)
    {
        if (sha256 is null || sha256.Length != 64 || sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException("Territory source identity requires a SHA256 digest.");
    }
}
