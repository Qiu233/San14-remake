using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record G1ShaderMatrixBindingRow(int Index, int SourceOffset,
    IReadOnlyList<uint> Words, ReadOnlyMemory<byte> RawBytes);
public sealed class G1ShaderMatrixBindings
{
    internal G1ShaderMatrixBindings(DxbcRange sourceRange, IReadOnlyList<G1ShaderMatrixBindingRow> rows)
        => (SourceRange, Rows) = (sourceRange, rows);
    public DxbcRange SourceRange { get; }
    public IReadOnlyList<G1ShaderMatrixBindingRow> Rows { get; }
}

/// <summary>Reads the original 16-byte matrix binding rows used by 980CC0.
/// Header source+50 is the count and source+80 the table origin. The rows
/// select the separate renderer+13E8 dimension; no current row is assumed.</summary>
public static class G1ShaderMatrixBindingReader
{
    public static G1ShaderMatrixBindings Read(ReadOnlySpan<byte> source)
        => Read(source, G2ShaderContainerReader.Read(source));

    internal static G1ShaderMatrixBindings Read(ReadOnlySpan<byte> source, G2ShaderContainer container)
    {
        uint count = U32(source, 0x50), start = U32(source, 0x80);
        long length = (long)count * 16;
        if (start < container.MetadataRange.Offset || start > int.MaxValue || length > int.MaxValue ||
            (long)start + length > (long)container.MetadataRange.Offset + container.MetadataRange.Length)
            throw new InvalidDataException("Original G1S matrix binding table exceeds the complete source metadata.");
        var rows = new List<G1ShaderMatrixBindingRow>((int)count);
        for (int i = 0; i < count; i++)
        {
            int offset = checked((int)start + i * 16);
            ReadOnlySpan<byte> raw = source.Slice(offset, 16);
            var words = new uint[4];
            for (int word = 0; word < words.Length; word++) words[word] = U32(raw, word * 4);
            rows.Add(new(i, offset, Array.AsReadOnly(words), raw.ToArray()));
        }
        return new(new((int)start, (int)length), rows.AsReadOnly());
    }
    private static uint U32(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
}
