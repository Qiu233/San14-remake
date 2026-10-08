using San14.Core;
using San14.Data;

/// <summary>One immutable ownership publication. Values are source report
/// color indices0..59 or the known no-tint60, not native palette slots.</summary>
public sealed class OriginalD3D11TerritoryPublication
{
    internal byte[] NativeColorIndices { get; }
    internal float[] SourceMapColors { get; }
    public ReadOnlyMemory<byte> ColorIndices => NativeColorIndices;
    public ulong Revision { get; }
    internal OriginalD3D11TerritoryPublication(byte[] colors, ulong revision, float[] mapColors)
        => (NativeColorIndices, Revision, SourceMapColors) = (colors, revision, mapColors);
}

/// <summary>Loaded source colors and land eligibility are retained once.
/// Core worlds/dictionaries are immutable by contract; normal camera requests
/// reuse this48KB publication without serializing or hashing the world.</summary>
public sealed class OriginalD3D11TerritoryPublisher
{
    public const int CellCount = 48400;
    public const byte NoTint = 60;
    private readonly bool[] _eligible;
    private readonly Dictionary<int, byte?> _colors;
    private readonly float[] _mapColors;
    private GameState? _lastWorld;
    private object? _lastTerritory, _lastForces;
    private OriginalD3D11TerritoryPublication? _current;
    private ulong _revision;

    public OriginalD3D11TerritoryPublisher(ScenarioCityPreview scenario, LoadedMap map,
        LoadedTerrainTypes terrain, GameState initialWorld, OriginalCeForceMapPalette palette)
    {
        ArgumentNullException.ThrowIfNull(scenario); ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(terrain); ArgumentNullException.ThrowIfNull(initialWorld);
        ArgumentNullException.ThrowIfNull(palette);
        // Loading boundary only. The loaded source providers already performed
        // file validation; these identities bind the owner to its real map.
        if (initialWorld.ScenarioKey != GameResourceId.File(scenario.SourcePath).ToString() ||
            initialWorld.MapSha256 != map.Source.Sha256 || initialWorld.MapWidth != 220 || initialWorld.MapHeight != 220 || palette.Locale != scenario.Locale)
            throw new InvalidDataException("D3D11 ownership source belongs to another scenario/map.");
        _eligible = OriginalTerritoryOwnership.ReadSourceTintEligibility(map, terrain);
        _colors = [];
        foreach (var force in scenario.Forces)
            if (force.Slot < 0 || !_colors.TryAdd(force.Slot, force.RawReportColor49))
                throw new InvalidDataException("D3D11 ownership source has invalid/duplicate force slots.");
        _mapColors = new float[60 * 4];
        for (int raw = 0; raw < 60; raw++)
        {
            var color = palette.MapColor(raw); int at = raw * 4;
            _mapColors[at] = color.RedFloat; _mapColors[at + 1] = color.GreenFloat;
            _mapColors[at + 2] = color.BlueFloat; _mapColors[at + 3] = color.AlphaFloat;
        }
    }

    /// <summary>Capture on the world's owner thread. A newly accepted territory
    /// or force dictionary produces one publication; camera-only requests reuse
    /// the same object. Unknown current force colors have no presentation fallback.</summary>
    public OriginalD3D11TerritoryPublication Capture(GameState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (_current is not null && (ReferenceEquals(world, _lastWorld) ||
            ReferenceEquals(world.Territory, _lastTerritory) && ReferenceEquals(world.Forces, _lastForces)))
        {
            _lastWorld = world; return _current;
        }
        if (world.MapWidth != 220 || world.MapHeight != 220 || world.Territory.Count != CellCount)
            throw new InvalidDataException("D3D11 ownership requires the complete220x220 current territory.");
        byte[] colors = new byte[CellCount]; Array.Fill(colors, NoTint);
        for (int id = 0; id < CellCount; id++)
        {
            if (!world.Territory.TryGetValue(id, out var cell) || cell.Id != id)
                throw new InvalidDataException($"Current ownership cell{id} is missing or has a different physical ID.");
            if (!_eligible[id] || cell.ForceId is null) continue;
            int force = cell.ForceId.Value;
            if (!world.Forces.ContainsKey(force) || !_colors.TryGetValue(force, out byte? color) || color is null)
                throw new InvalidDataException($"Current force{force} has no resolved source RawReportColor49.");
            if (color > NoTint) throw new InvalidDataException($"Current force{force} has unsupported source color{color}.");
            colors[id] = color.Value;
        }
        var current = new OriginalD3D11TerritoryPublication(colors, ++_revision, _mapColors);
        _lastWorld = world; _lastTerritory = world.Territory; _lastForces = world.Forces; return _current = current;
    }
}
