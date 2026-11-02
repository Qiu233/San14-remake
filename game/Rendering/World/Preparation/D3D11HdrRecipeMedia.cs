using System.Text;

/// <summary>One prepared recipe's named byte payloads. The output belongs to
/// the current opening, and Native imports it once before creating GPU owners.</summary>
public sealed class D3D11HdrRecipeMedia(string directory)
{
    private readonly SortedDictionary<string, byte[]> files = new(StringComparer.Ordinal);

    public void WriteAllBytes(string name, ReadOnlySpan<byte> bytes) => files[name] = bytes.ToArray();
    public void WriteAllText(string name, string text) => files[name] = Encoding.UTF8.GetBytes(text);
    public void WriteAllLines(string name, IEnumerable<string> lines)
        => WriteAllText(name, string.Concat(lines.Select(line => line + Environment.NewLine)));
    public void Remove(string name) => files.Remove(name);

    public void Commit()
    {
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, "media.bin"));
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(0x314D5248u); // HRM1: versioned named recipe media.
        writer.Write(files.Count);
        foreach (var (name, bytes) in files)
        {
            byte[] encodedName = Encoding.UTF8.GetBytes(name);
            writer.Write(encodedName.Length);
            writer.Write(encodedName);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
