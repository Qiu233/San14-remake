using San14.Data.Formats;

namespace San14.Data;

public sealed record ItemEditorExternalImage(int ImageIndex, string Path, RgbaTexture? Texture, string? Error);

/// <summary>The original read-only ADD_PICTURE/EDIT_PICTURE library. A file
/// occurrence must exist; arbitrary external indices are never synthesized.</summary>
public static class ItemEditorExternalImages
{
    public static string OriginalDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "KoeiTecmo", "SAN14", "ADD_PICTURE", "EDIT_PICTURE");
    public static IReadOnlyList<ItemEditorExternalImage> Read(string directory)
    {
        var result = new List<ItemEditorExternalImage>();
        for (int slot = 0; slot <= 200; slot++)
        {
            string path = Path.Combine(directory, $"EDIT_PICTURE_{slot}.s14.zp");
            if (!File.Exists(path)) continue;
            try { result.Add(new(slot + 201, path, Decode(File.ReadAllBytes(path)), null)); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
            { result.Add(new(slot + 201, path, null, error.Message)); }
        }
        return result;
    }
    public static ItemEditorExternalImage ReadImage(int imageIndex)
    {
        if(imageIndex is <201 or >401) throw new ArgumentOutOfRangeException(nameof(imageIndex));
        string path=Path.Combine(OriginalDirectory,$"EDIT_PICTURE_{imageIndex-201}.s14.zp");
        try { return new(imageIndex,path,Decode(File.ReadAllBytes(path)),null); }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException)
        { return new(imageIndex,path,null,error.Message); }
    }
    public static RgbaTexture Decode(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith("zp1\0"u8)) bytes = Zp1Reader.Read(bytes).DecodedPayload;
        if (!bytes.AsSpan().StartsWith("GT1G"u8))
            throw new InvalidDataException("External item picture has an unsupported resource container.");
        return G1TextureReader.Decode(bytes);
    }
}
