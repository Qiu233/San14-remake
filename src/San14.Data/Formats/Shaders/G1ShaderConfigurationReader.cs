using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public sealed record G1ShaderConfigurationParameter(int Index, int SourceOffset, uint NameHash, uint RawValue);
public sealed record G1ShaderConfigurationRecord(int Index, int SourceOffset, uint NameHash,
    uint RawSecondWord, string Name, DxbcRange NameRange, IReadOnlyList<G1ShaderConfigurationParameter> Parameters)
{
    /// <summary>Native lookup uses the first matching parameter, preserving duplicate rows.</summary>
    public G1ShaderConfigurationParameter? Find(uint hash) => Parameters.FirstOrDefault(p => p.NameHash == hash);
}
public sealed record G1ShaderConfigurations(DxbcRange DirectoryRange,
    IReadOnlyList<G1ShaderConfigurationRecord> Records);

/// <summary>Source configuration directory used by the original module's named
/// factory and GBufferLayout lookup. A source record is not a created runtime plugin.</summary>
public static class G1ShaderConfigurationReader
{
    public static G1ShaderConfigurations Read(ReadOnlySpan<byte> source)
        => Read(source, G2ShaderContainerReader.Read(source));

    public static G1ShaderConfigurations Read(ReadOnlySpan<byte> source, G2ShaderContainer container)
    {
        try { return ReadCore(source, container); }
        catch (OverflowException error) { throw new InvalidDataException("Shader configuration range exceeds supported addressing.", error); }
    }

    private static G1ShaderConfigurations ReadCore(ReadOnlySpan<byte> source, G2ShaderContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        if (source.Length != container.SourceByteLength || source.Length < 256 ||
            !source[..256].SequenceEqual(container.RawHeader))
            throw new InvalidDataException("Shader configuration source differs from its container header.");
        int count = Number(source, 0x70), table = Number(source, 0xA0);
        void Range(int at, int bytes) {
            if (at < container.MetadataRange.Offset || bytes < 0 ||
                (long)at + bytes > (long)container.MetadataRange.Offset + container.MetadataRange.Length)
                throw new InvalidDataException("Shader configuration range is outside the original metadata.");
        }
        if (count > 4096) throw new InvalidDataException("Shader configuration directory exceeds the supported record cap.");
        Range(table, checked(count * 4));
        var rows = new List<G1ShaderConfigurationRecord>(); var occupied = new List<DxbcRange>();
        if (count > 0) occupied.Add(new(table, count * 4));
        for (int i = 0; i < count; i++)
        {
            int at = checked(table + Number(source, table + i * 4));
            Range(at, 12);
            uint hash = U32(source, at), second = U32(source, at + 4);
            int parameters = Number(source, at + 8);
            if (parameters > 8192) throw new InvalidDataException("Shader configuration parameter count exceeds the supported cap.");
            int nameAt = checked(at + 12 + parameters * 8);
            Range(at, checked(12 + parameters * 8)); Range(nameAt, 1);
            int available = Math.Min(1024, container.MetadataRange.Offset + container.MetadataRange.Length - nameAt);
            int length = source.Slice(nameAt, available).IndexOf((byte)0);
            if (length < 1 || source.Slice(nameAt, length).ContainsAnyExceptInRange((byte)32, (byte)126))
                throw new InvalidDataException("Shader configuration name is not a bounded native ASCII string.");
            uint actualHash = 0;
            foreach (byte b in source.Slice(nameAt, length)) actualHash = unchecked(actualHash * 27 + (uint)(sbyte)b);
            if (actualHash != hash) throw new InvalidDataException("Shader configuration name hash differs from its native lookup key.");
            var fields = new List<G1ShaderConfigurationParameter>();
            for (int p = 0; p < parameters; p++) {
                int field = at + 12 + p * 8;
                fields.Add(new(p, field, U32(source, field), U32(source, field + 4)));
            }
            occupied.Add(new(at, checked(nameAt + length + 1 - at)));
            rows.Add(new(i, at, hash, second, Encoding.ASCII.GetString(source.Slice(nameAt, length)),
                new(nameAt, length + 1), fields.AsReadOnly()));
        }
        // Distinct records may share a source body; retain ordered aliases.
        var distinct = occupied.Distinct().OrderBy(r => r.Offset).ToArray();
        for (int i = 1; i < distinct.Length; i++)
            if ((long)distinct[i - 1].Offset + distinct[i - 1].Length > distinct[i].Offset)
                throw new InvalidDataException("Shader configuration directory or distinct records overlap.");
        return new(new(table, checked(count * 4)), rows.AsReadOnly());
    }

    private static uint U32(ReadOnlySpan<byte> source, int at) => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(at, 4));
    private static int Number(ReadOnlySpan<byte> source, int at) {
        uint value = U32(source, at);
        return value <= int.MaxValue ? (int)value : throw new InvalidDataException("Shader configuration offset/count exceeds supported addressing.");
    }
}
