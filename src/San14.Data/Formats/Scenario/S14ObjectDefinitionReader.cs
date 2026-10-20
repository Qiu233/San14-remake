using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14FacilityEffectChannel(byte Effect, byte Amount, byte Secondary);
public sealed record S14ObjectDefinition(int Slot, string Name, byte RawField2C, byte[] RawBytes)
{
    public ushort BaseDurability => BinaryPrimitives.ReadUInt16LittleEndian(RawBytes.AsSpan(29, 2));
    public ushort BaseGoldCost => BinaryPrimitives.ReadUInt16LittleEndian(RawBytes.AsSpan(31, 2));
    public ushort RequiredTrait => BinaryPrimitives.ReadUInt16LittleEndian(RawBytes.AsSpan(33, 2));
    public byte RequiredPolicyEffect => RawBytes[35];
    public byte ExclusionRadius => RawBytes[36];
    public uint ConstructionTerrainMask => BinaryPrimitives.ReadUInt32LittleEndian(RawBytes.AsSpan(46, 4));
    public byte TriggerRange => RawBytes[50];
    public byte RequiredPolicyLevel => RawBytes[51];
    public S14FacilityEffectChannel FirstChannel { get; init; } = new(0, 0, 0);
    public S14FacilityEffectChannel SecondChannel { get; init; } = new(0, 0, 0);
    public byte MinimumRange { get; init; }
    public byte MaximumRange { get; init; }
}
public sealed record S14ObjectDefinitionCatalog(uint FormatMarker, int RecordOffset,
    int RecordStride, string DecodedSha256, IReadOnlyList<S14ObjectDefinition> Definitions);

/// <summary>CE fixed section9, current definition table73958. Retains complete
/// raw rows; decodes the name, category and two facility-effect channels/ranges.</summary>
public static class S14ObjectDefinitionReader
{
    public const int Count = 31;
    public const int RecordStart = 183710;
    public const int RecordStride = 53;
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);

    public static S14ObjectDefinitionCatalog ReadFixed(S14Document fixedData)
    {
        ArgumentNullException.ThrowIfNull(fixedData);
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        if (fixedData.FormatMarker != 0x66 || bytes.Length < 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes) != fixedData.FormatMarker)
            throw new InvalidDataException("Object definitions require the inspected CE fixed-data profile.");
        Header(bytes, RecordStart - 8, 9, Count);
        Header(bytes, RecordStart + Count * RecordStride, 10, 1851);
        var definitions = new S14ObjectDefinition[Count];
        for (int slot = 0; slot < Count; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(RecordStart + slot * RecordStride, RecordStride);
            string name;
            try { name = StrictUtf16.GetString(row[..10]); }
            catch (DecoderFallbackException error)
            { throw new InvalidDataException($"Object definition {slot} has invalid UTF16 text.", error); }
            int end = name.IndexOf('\0');
            if (end >= 0) name = name[..end];
            definitions[slot] = new(slot, name, row[28], row.ToArray())
            {
                FirstChannel = new(row[37], row[38], row[39]),
                SecondChannel = new(row[40], row[41], row[42]),
                MinimumRange = row[43], MaximumRange = row[44]
            };
        }
        return new(fixedData.FormatMarker, RecordStart, RecordStride,
            Convert.ToHexString(SHA256.HashData(bytes)), Array.AsReadOnly(definitions));
    }

    private static void Header(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"Object definition fixed section {section} boundary or count differs.");
    }
}
