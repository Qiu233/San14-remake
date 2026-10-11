namespace San14.Data;

/// <summary>The selected named renderer's construction inputs. Device target
/// dimensions and GPU completion are separate from this CPU resource owner.</summary>
public static class NamedViewRendererEncoding
{
    public const string NativeFilename = "scene-renderer.bin";
    public const int ByteLength = 28;

    public static byte[] Write(NamedViewRendererResources resources, uint viewIndex)
    {
        using var stream = new MemoryStream(ByteLength);
        using var writer = new BinaryWriter(stream);
        writer.Write(0x3143524Eu); // NRC1: named renderer construction.
        writer.Write(viewIndex);
        writer.Write(resources.ViewObjectId); writer.Write(resources.RendererObjectId);
        writer.Write(resources.DeclaredGrid.Columns); writer.Write(resources.DeclaredGrid.Rows);
        writer.Write(resources.DimensionOverride);
        return stream.ToArray();
    }

    public static void WriteNativeInputs(string directory, NamedViewRendererResources resources, uint viewIndex)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, NativeFilename), Write(resources, viewIndex));
    }
}
