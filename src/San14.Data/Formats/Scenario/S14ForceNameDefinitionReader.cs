using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

public sealed record S14ForceNameDefinition(int PhysicalSlot, int SourceOffset,
    string ShortName, string Text18, string Text26, byte Field8C, byte Field8D,
    byte Field8E, byte[] RawBytes)
{
    // Current CKingdomData2344E0 computes its object-array slot. It does not
    // test the name contents: all nonzero physical rows are valid.
    public bool NativeValidity => PhysicalSlot != 0;
}

public sealed record S14ForceNameDefinitionCatalog(int RecordOffset, int RecordStride,
    IReadOnlyList<S14ForceNameDefinition> Definitions, string RowsSha256);

/// <summary>Current CE fixed section14, read by CKingdomData slot6
/// SC23ECE0/TC23EA60. Its independent scenario slot5 consumes zero bytes.
/// Four raw UTF16 units map to native+10; no locale or ruler text is inferred.</summary>
public static class S14ForceNameDefinitionReader
{
    public const int Count = 101;
    public const int RecordOffset = 0x33BD3;
    public const int RecordStride = 127;

    public static S14ForceNameDefinitionCatalog Read(S14Document fixedData)
    {
        ArgumentNullException.ThrowIfNull(fixedData);
        ReadOnlySpan<byte> source = fixedData.DecodedPayload;
        int end = checked(RecordOffset + Count * RecordStride);
        if (fixedData.FormatMarker != 0x66 || source.Length < end + 8 ||
            I(source, RecordOffset - 8) != 14 || I(source, RecordOffset - 4) != Count ||
            I(source, end) != 15 || I(source, end + 4) != 13)
            throw new InvalidDataException("Army flag custom names require the complete CE fixed section14/count101 and its marker15/count13 boundary.");
        var definitions = new S14ForceNameDefinition[Count];
        for (int slot = 0; slot < Count; slot++)
        {
            int at = RecordOffset + slot * RecordStride;
            ReadOnlySpan<byte> row = source.Slice(at, RecordStride);
            definitions[slot] = new(slot, at, ReadLiteralUnits(row[..8]), ReadLiteralUnits(row.Slice(8, 14)),
                ReadLiteralUnits(row.Slice(22, 102)), row[124], row[125], row[126], row.ToArray());
        }
        return new(RecordOffset, RecordStride, Array.AsReadOnly(definitions),
            Convert.ToHexString(SHA256.HashData(source.Slice(RecordOffset, Count * RecordStride))));
    }

    private static int I(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(at, 4));
    internal static string ReadLiteralUnits(ReadOnlySpan<byte> bytes)
    {
        // Preserve individual source units, including an unpaired surrogate.
        // Native fixed readers copy words; the getter stops at the first zero.
        var units = new char[bytes.Length / 2]; int count = 0;
        for (; count < units.Length; count++)
        {
            ushort unit = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(count * 2, 2));
            if (unit == 0) break;
            units[count] = (char)unit;
        }
        return new string(units, 0, count);
    }
}
