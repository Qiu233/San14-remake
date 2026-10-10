using System.Collections.ObjectModel;
using Godot;
using San14.Core;
using San14.Data;

/// <summary>One scenario's immutable territory and marker colors, outside campaign state.</summary>
internal sealed class MapForcePresentation
{
    public const int MarkerColorSelector = 2;
    private const int OrdinaryColorCount = 60;
    public CeForceMapPalette Palette { get; }
    public IReadOnlyDictionary<int, Color> MapColors { get; }
    public IReadOnlyDictionary<int, Color> MarkerColors { get; }

    private MapForcePresentation(CeForceMapPalette palette, Dictionary<int, Color> mapColors,
        Dictionary<int, Color> markerColors)
    {
        Palette = palette;
        MapColors = new ReadOnlyDictionary<int, Color>(mapColors);
        MarkerColors = new ReadOnlyDictionary<int, Color>(markerColors);
    }

    public static MapForcePresentation Load(ScenarioCityPreview scenario, string locale)
    {
        if (!scenario.Locale.Equals(locale, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Scenario and displayed source locale differ.");
        CeForceMapPalette palette = CeForceMapPalette.LoadCurrent(locale);
        var colors = new Dictionary<int, Color>();
        var markers = new Dictionary<int, Color>();
        foreach (ForcePreviewRecord force in scenario.Forces)
        {
            if (force.RawReportColor49 is not byte colorIndex || colorIndex >= OrdinaryColorCount) continue;
            CeForceMapColor color = palette.MapColor(colorIndex);
            if (!colors.TryAdd(force.Slot, new(color.RedFloat, color.GreenFloat, color.BlueFloat, color.AlphaFloat)))
                throw new InvalidDataException($"Duplicate source force slot {force.Slot}.");
            CeForceFirstTableColor marker = palette.FirstTableColor(colorIndex, MarkerColorSelector);
            markers.Add(force.Slot, new(marker.Red / 255f, marker.Green / 255f, marker.Blue / 255f, marker.Alpha / 255f));
        }
        return new(palette, colors, markers);
    }
}

/// <summary>Building display names resolved from the campaign's fixed-data resource.</summary>
internal sealed class MapStructureCatalog
{
    public IReadOnlyDictionary<string, string> Names { get; }
    public string SourceUri { get; }
    public string SourceSha256 { get; }

    private MapStructureCatalog(Dictionary<string, string> names, string sourceUri, string sourceSha256)
    {
        Names = new ReadOnlyDictionary<string, string>(names);
        (SourceUri, SourceSha256) = (sourceUri, sourceSha256);
    }

    public static MapStructureCatalog Load(GameResourceRepository repository, ScenarioCityPreview scenario, GameState world)
    {
        LoadedObjectDefinitions loaded = repository.LoadObjectDefinitions(scenario.FixedDataPath);
        if (!world.ContentHashes.TryGetValue(loaded.Source.ResourceUri, out string? expectedSha) ||
            !expectedSha.Equals(loaded.Source.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Displayed building names and saved fixed-data source differ.");
        var names = loaded.Catalog.Definitions.Where(row => !string.IsNullOrWhiteSpace(row.Name))
            .ToDictionary(row => $"san14:object/{row.Slot}", row => row.Name);
        return new(names, loaded.Source.ResourceUri, loaded.Source.Sha256);
    }
}
