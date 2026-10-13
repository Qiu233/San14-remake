using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public enum DxbcSignatureKind { Input, Output, PatchConstants }
public sealed record DxbcSignatureString(string Text, DxbcRange EncodedRange);
public sealed record DxbcSignatureParameter(int Index, DxbcRange RecordRange, DxbcSignatureString SemanticName,
    uint SemanticIndex, uint RawSystemValue, uint ComponentType, uint Register,
    byte Mask, byte ReadWriteMask, ushort RawMaskPadding, uint? StreamIndex);
public sealed record DxbcSignaturePart(int DirectoryIndex, string FourCc, DxbcSignatureKind Kind,
    DxbcRange PayloadRangeInProgram, DxbcRange HeaderRange, DxbcRange RecordTableRange,
    IReadOnlyList<DxbcSignatureParameter> Parameters, IReadOnlyList<DxbcRange> UninterpretedRanges);
public sealed record DxbcProgramSignatures(int ProgramByteLength, IReadOnlyList<DxbcSignaturePart> Parts);

/// <summary>
/// Reads the installed ISGN/OSGN/PCSG 24-byte and OSG5 28-byte signature profiles.
/// Inner ranges are relative to their signature payload; raw system-value codes
/// and mask bytes are retained, without D3DReflect's output-name normalization.
/// Stage linkage, vertex-buffer binding and shader execution are separate concerns.
/// </summary>
public static class DxbcSignatureReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static DxbcProgramSignatures Read(ReadOnlySpan<byte> program)
    {
        DxbcContainer container = DxbcContainerReader.Read(program);
        var parts = new List<DxbcSignaturePart>();
        foreach (DxbcPart part in container.Parts)
        {
            if (part.FourCc is "ISG1" or "OSG1" or "PSG1")
                throw Error(part.FourCc, "minimum-precision signature profile has not been verified for installed sources");
            if (part.FourCc is not ("ISGN" or "OSGN" or "PCSG" or "OSG5")) continue;
            parts.Add(ReadPart(part, part.PayloadRange.Slice(program)));
        }
        return new(program.Length, parts.AsReadOnly());
    }

    private static DxbcSignaturePart ReadPart(DxbcPart part, ReadOnlySpan<byte> data)
    {
        string context = $"{part.FourCc} directory entry {part.DirectoryIndex}";
        if (data.Length < 8) throw Error(context, "truncated header");
        int count = Number(data, 0, context), headerSize = Number(data, 4, context);
        int stride = part.FourCc == "OSG5" ? 28 : 24;
        if (headerSize < 8 || headerSize > data.Length || headerSize % 4 != 0 ||
            count > (data.Length - headerSize) / stride)
            throw Error(context, "invalid header or parameter table boundaries");
        var header = new DxbcRange(0, headerSize);
        var table = new DxbcRange(headerSize, count * stride);
        int stringStart = headerSize + table.Length;
        int textBudget = data.Length;
        var strings = new Dictionary<int, DxbcSignatureString>();
        var parameters = new List<DxbcSignatureParameter>(count);
        for (int index = 0; index < count; index++)
        {
            int row = headerSize + index * stride, fields = row + (stride == 28 ? 4 : 0);
            uint? stream = stride == 28 ? UInt(data, row) : null;
            DxbcSignatureString name = ReadString(data, UInt(data, fields), stringStart, strings, ref textBudget, context);
            parameters.Add(new(index, new(row, stride), name,
                UInt(data, fields + 4), UInt(data, fields + 8), UInt(data, fields + 12), UInt(data, fields + 16),
                data[fields + 20], data[fields + 21], BinaryPrimitives.ReadUInt16LittleEndian(data[(fields + 22)..]), stream));
        }
        var gaps = new List<DxbcRange>();
        int cursor = stringStart;
        foreach (DxbcRange range in strings.Values.Select(name => name.EncodedRange).OrderBy(range => range.Offset))
        {
            if (range.Offset > cursor) gaps.Add(new(cursor, range.Offset - cursor));
            cursor = Math.Max(cursor, range.Offset + range.Length);
        }
        if (cursor < data.Length) gaps.Add(new(cursor, data.Length - cursor));
        DxbcSignatureKind kind = part.FourCc switch
        {
            "ISGN" => DxbcSignatureKind.Input,
            "PCSG" => DxbcSignatureKind.PatchConstants,
            _ => DxbcSignatureKind.Output
        };
        return new(part.DirectoryIndex, part.FourCc, kind, part.PayloadRange, header, table,
            parameters.AsReadOnly(), gaps.AsReadOnly());
    }

    private static DxbcSignatureString ReadString(ReadOnlySpan<byte> data, uint rawOffset, int minimum,
        Dictionary<int, DxbcSignatureString> strings, ref int budget, string context)
    {
        if (rawOffset < minimum || rawOffset >= data.Length)
            throw Error(context, $"semantic name offset {rawOffset} intersects structural data or exceeds payload");
        int offset = (int)rawOffset;
        if (strings.TryGetValue(offset, out DxbcSignatureString? known)) return known;
        int limit = Math.Min(data.Length - offset, budget);
        int length = data.Slice(offset, limit).IndexOf((byte)0);
        if (length < 0) throw Error(context, $"semantic name at {offset} is unterminated or exceeds decoded text budget");
        string text;
        try { text = StrictUtf8.GetString(data.Slice(offset, length)); }
        catch (DecoderFallbackException error)
        { throw new InvalidDataException($"DXBC signature {context}: semantic name at {offset} is not valid UTF-8.", error); }
        budget -= length + 1;
        var result = new DxbcSignatureString(text, new(offset, length + 1));
        strings.Add(offset, result);
        return result;
    }
    private static uint UInt(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static int Number(ReadOnlySpan<byte> bytes, int offset, string context)
    {
        uint value = UInt(bytes, offset);
        return value <= int.MaxValue ? (int)value : throw Error(context, $"field at {offset} exceeds supported range");
    }
    private static InvalidDataException Error(string context, string message) => new($"DXBC signature {context}: {message}.");
}
