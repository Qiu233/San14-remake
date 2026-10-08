using San14.Data;

/// <summary>CE C460's two LandscapePalette MaterialNameHash stores followed
/// by the ordinary first-name texture lookup. C080 placement/biome changes are
/// separate; this producer does not change or invent any control texel.</summary>
public sealed class OriginalD3D11TerrainSeasonPalette
{
    private readonly IReadOnlyList<TerrainDetailPaletteEntry>[] _seasons;
    public LoadedTerrainParameterResources Source { get; }
    public const string SourceEvidence = "CE typed 140000000.cpp C460..C5F2; BA94C0/BC0130 LandscapePalette24; " +
        "docs/.local/ce-palette-gpu-construction-20261001/REPORT.md sections2/3";

    public OriginalD3D11TerrainSeasonPalette(LoadedTerrainParameterResources actualCurrentLandscape)
    {
        Source = actualCurrentLandscape;
        _seasons = new IReadOnlyList<TerrainDetailPaletteEntry>[4];
        for (int season = 0; season < 4; season++)
        {
            var fields = actualCurrentLandscape.PaletteEntries.ToArray();
            var names = season < 2 ? new[] { "MC52_Juicy_Stage_Terrain_detail_field_01",
                "MC52_Juicy_Stage_Terrain_detail_forest_01" } : new[] {
                "MC52_Juicy_Stage_Terrain_detail_field_01a", "MC52_Juicy_Stage_Terrain_detail_forest_02" };
            int[] indices = [10, 4];
            for (int i = 0; i < indices.Length; i++)
            {
                int at = indices[i];
                if (at < fields.Length) fields[at] = fields[at] with {
                    MaterialNameHash = OriginalResourceNameHash.Compute(names[i]) };
            }
            _seasons[season] = Array.AsReadOnly(fields.Select(row => new TerrainDetailPaletteEntry(row,
                First(actualCurrentLandscape.Identifiers, row.MaterialNameHash))).ToArray());
        }
    }
    private static uint First(IReadOnlyList<uint> identifiers, uint name)
    { for (int i = 0; i < identifiers.Count; i++) if (identifiers[i] == name) return (uint)i; return uint.MaxValue; }
    public IReadOnlyList<TerrainDetailPaletteEntry> ForSeason(int seasonIndex)
        => (uint)seasonIndex < 4 ? _seasons[seasonIndex] : throw new ArgumentOutOfRangeException(nameof(seasonIndex));

    /// <summary>Four current lookup-index prefixes. Other five palette columns
    /// remain exact original values, including all native padding/tail bytes.</summary>
    public void WriteNativeInputs(string directory)
    {
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, "season-palette.bin")));
        writer.Write(4u); writer.Write((uint)Source.PaletteEntries.Count);
        for (int season = 0; season < 4; season++)
        { writer.Write((uint)season); foreach (var row in _seasons[season]) writer.Write(row.DetailTextureNameIndex); }
    }
}
