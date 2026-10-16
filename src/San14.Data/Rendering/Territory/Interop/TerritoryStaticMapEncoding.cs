namespace San14.Data;

/// <summary>ZSV2 is the native static-map import boundary. It contains no
/// ownership layer or allocated palette slots.</summary>
public static class TerritoryStaticMapEncoding
{
    public const string NativeDirectory = "territory-zone-source-input";
    public const uint Magic = 0x3256535A;
    public const int EncodedLength = 4 + TerritoryStaticMapInputs.CellCount * 5 + TerritoryStaticMapInputs.AreaCount;

    public static void WriteNativeInputs(string directory, TerritoryStaticMapInputs source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Directory.CreateDirectory(directory);
        using var output = new BinaryWriter(File.Create(Path.Combine(directory, "profile.bin")));
        output.Write(Magic);
        foreach (ushort flags in source.TerrainFlags) output.Write(flags);
        output.Write(source.SurfaceCodes);
        foreach (ushort area in source.AreaSlots) output.Write(area);
        output.Write(source.ValidAreas);
    }
}
