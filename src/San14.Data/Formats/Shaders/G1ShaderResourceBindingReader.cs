using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record G1ShaderResourceBinding(int Index, int SourceOffset,
    short PrimaryEncoded, short SecondarySamplerEncoded, short SlotIndex,
    short RawAuxiliary, uint NameOffset, string Name, DxbcRange NameRange, ReadOnlyMemory<byte> RawBytes);

public sealed record G1ShaderResourceBindings(G2ShaderCombination Combination,
    DxbcRange SourceRange, ushort RawHeaderWord2, IReadOnlyList<G1ShaderResourceBinding> Entries);

/// <summary>Native 97FAC0's 4-byte header and 12-byte resource-name records,
/// selected by an explicit original combination. Names use Module+30's source origin.
/// This preserves encoded arguments; it does not submit resources or infer stage masks.</summary>
public static class G1ShaderResourceBindingReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static G1ShaderResourceBindings Read(ReadOnlySpan<byte> source, int combinationIndex)
        => Read(source, G2ShaderContainerReader.Read(source), combinationIndex);

    internal static G1ShaderResourceBindings Read(ReadOnlySpan<byte> source,
        G2ShaderContainer container, int combinationIndex)
    {
        G2ShaderCombinations combinations = G2ShaderCombinationReader.Read(source, container);
        if ((uint)combinationIndex >= combinations.Rows.Count)
            throw new InvalidDataException("Original resource binding requires a valid explicit combination index.");
        return Read(source, container, combinations.Rows[combinationIndex]);
    }

    internal static G1ShaderResourceBindings Read(ReadOnlySpan<byte> source,
        G2ShaderContainer container, G2ShaderCombination combination)
    {
        if (combination.RawValue4 > int.MaxValue)
            throw new InvalidDataException("Original resource-name table exceeds supported source addressing.");
        int start = (int)combination.RawValue4;
        InMetadata(container, start, 4);
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(start, 2));
        ushort rawHeaderWord2 = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(start + 2, 2));
        int bytes = 4 + count * 12;
        InMetadata(container, start, bytes);
        var entries = new List<G1ShaderResourceBinding>(count);
        int metadataEnd = checked(container.MetadataRange.Offset + container.MetadataRange.Length);
        for (int i = 0; i < count; i++)
        {
            int at = start + 4 + i * 12;
            ReadOnlySpan<byte> raw = source.Slice(at, 12);
            uint nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(raw.Slice(8, 4));
            if (nameOffset > int.MaxValue)
                throw new InvalidDataException("Original resource name exceeds supported source addressing.");
            int nameAt = (int)nameOffset;
            InMetadata(container, nameAt, 1);
            int length = source.Slice(nameAt, metadataEnd - nameAt).IndexOf((byte)0);
            if (length < 0)
                throw new InvalidDataException("Original resource name lacks its native NUL terminator within metadata.");
            string name;
            try { name = StrictUtf8.GetString(source.Slice(nameAt, length)); }
            catch (DecoderFallbackException error)
            { throw new InvalidDataException("Original resource name is not valid UTF-8.", error); }
            entries.Add(new(i, at, BinaryPrimitives.ReadInt16LittleEndian(raw),
                BinaryPrimitives.ReadInt16LittleEndian(raw.Slice(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(raw.Slice(4, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(raw.Slice(6, 2)), nameOffset, name, new(nameAt, length + 1), raw.ToArray()));
        }
        return new(combination, new(start, bytes), rawHeaderWord2, entries.AsReadOnly());
    }

    private static void InMetadata(G2ShaderContainer container, int offset, int bytes)
    {
        if (offset < container.MetadataRange.Offset || bytes < 0 ||
            (long)offset + bytes > (long)container.MetadataRange.Offset + container.MetadataRange.Length)
            throw new InvalidDataException("Original resource binding range lies outside the decoded metadata.");
    }
}
