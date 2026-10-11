namespace San14.Data;

/// <summary>Cache transport for the water grid constructor, separate from its
/// resource declaration and from current wave/surface state.</summary>
public static class OceanGridSettingsEncoding
{
    public const string NativeDirectory = "named-renderer-input";
    public const string NativeFilename = "ocean-grid.bin";

    public static void WriteNativeInputs(string directory, OceanGridSettings grid)
    {
        Directory.CreateDirectory(directory);
        using var writer = new BinaryWriter(File.Create(Path.Combine(directory, NativeFilename)));
        writer.Write(0x3152474Fu); // OGR1
        writer.Write(grid.Columns); writer.Write(grid.Rows);
        writer.Write(grid.Size.X); writer.Write(grid.Size.Y);
        writer.Write(grid.Cycle); writer.Write(grid.DeclaredLod); writer.Write(grid.MaximumDistance);
    }
}
