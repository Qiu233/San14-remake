using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

public sealed record S14PersonalitySourceRow(int Slot, byte[] RawBytes);
public sealed record S14PersonalityCatalog(bool FixedData, int RecordOffset, int RecordStride,
    string DecodedSha256, IReadOnlyList<S14PersonalitySourceRow> Rows);
public sealed record S14PersonalityField(int ObjectOffset, int SourceOffset, int Bytes);

/// <summary>CE officer trait definition and static state source windows.</summary>
public static class S14PersonalityReader
{
    public const int Count=356, FixedStart=263629, FixedStride=170, StateStride=60;
    public static IReadOnlyList<S14PersonalityField> FixedFields { get; } = Array.AsReadOnly(new S14PersonalityField[]
    {
        new(0x10,0,12),new(0x1C,12,32),new(0x3C,44,102),new(0xA2,146,2),
        new(0xA4,148,1),new(0xAC,149,1),new(0xB4,150,2),new(0xB6,152,1),
        new(0xB8,153,2),new(0xBA,155,1),new(0xBC,156,2),new(0xBE,158,1),new(0xBF,159,1),
        new(0xA8,160,4),new(0xB0,164,4),new(0xC0,168,1),new(0xE0,169,1)
    });
    public static IReadOnlyList<S14PersonalityField> StateFields { get; } = Array.AsReadOnly(new S14PersonalityField[]
    {
        new(0x10,0,12),new(0x1C,12,32),new(0xA4,44,1),new(0xAC,45,1),
        new(0xA8,46,4),new(0xB0,50,4),new(0xB6,54,1),new(0xB8,55,2),new(0xBA,57,1),new(0xBC,58,2)
    });

    public static S14PersonalityCatalog ReadFixed(S14Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ReadOnlySpan<byte> source=document.DecodedPayload;
        int end=FixedStart+Count*FixedStride;
        if(document.FormatMarker!=0x66 || source.Length<end+8 ||
            I32(source,0)!=0x66 || I32(source,FixedStart-8)!=22 || I32(source,FixedStart-4)!=Count ||
            I32(source,end)!=23 || I32(source,end+4)!=501)
            throw new InvalidDataException("Officer trait definitions require the supported CE section22/356/170-byte fixed profile.");
        return Catalog(true,FixedStart,FixedStride,Convert.ToHexString(SHA256.HashData(source)),source.Slice(FixedStart,Count*FixedStride));
    }
    public static S14PersonalityCatalog ReadState(S14ScenarioWorldDocument world)
    {
        ArgumentNullException.ThrowIfNull(world);
        S14ScenarioStateTable[] tables=world.SourceBoundary?.StateTables.Where(table=>table.Section==22).ToArray()
            ?? throw new InvalidDataException("Officer trait state requires the forward scenario boundary.");
        if(tables.Length!=1 || tables[0] is not { RawCount:Count,RecordBytes:StateStride } table ||
            table.RawBytes.Length!=Count*StateStride || table.RawSha256!=Convert.ToHexString(SHA256.HashData(table.RawBytes)))
            throw new InvalidDataException("Officer trait state section22 requires356 complete60-byte source rows.");
        return Catalog(false,table.RecordOffset,StateStride,world.DecodedSha256,table.RawBytes);
    }
    private static S14PersonalityCatalog Catalog(bool fixedData,int offset,int stride,string sha,ReadOnlySpan<byte> source)
    {
        var rows=new S14PersonalitySourceRow[Count];
        for(int slot=0;slot<rows.Length;slot++)rows[slot]=new(slot,source.Slice(slot*stride,stride).ToArray());
        return new(fixedData,offset,stride,sha,Array.AsReadOnly(rows));
    }
    private static int I32(ReadOnlySpan<byte> source,int offset)=>BinaryPrimitives.ReadInt32LittleEndian(source[offset..]);
}
