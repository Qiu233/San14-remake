using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The SC BadWord resource's actual first-column byte patterns.
/// The second column belongs to replacement; it is parsed but not queried here.</summary>
internal sealed class PersonnelHireNameFilterSource
{
    //SC309F30 and30A940 use this immediate fixed owner capacity.
    //It is independent of the XL resource's declared row count.
    private const int UsedRows = 25289;
    private readonly byte[][] patterns;
    private static readonly Encoding NameEncoding = CreateEncoding();

    private PersonnelHireNameFilterSource(byte[][] patterns) => this.patterns = patterns;

    internal bool Contains(string name)
    {
        //SC152050 selects the Simplified Chinese CRT locale and code page936.
        //33F1A0 converts the generated UTF16 full name before308740/30A940.
        byte[] encoded;
        try { encoded = NameEncoding.GetBytes(name); }
        catch (EncoderFallbackException error)
        {
            //Every literal part in the installed SC make-name catalogue is
            //representable. An incompatible replacement is not a '?' name.
            throw new InvalidDataException("personnel.hire.name_filter.encoding", error);
        }
        ReadOnlySpan<byte> text = encoded.AsSpan(0, Math.Min(1024, encoded.Length));
        foreach (byte[] pattern in patterns)
            if (text.IndexOf(pattern) >= 0) return true;
        return false;
    }

    internal static PersonnelHireNameFilterSource Read(ReadOnlySpan<byte> source)
    {
        byte[] payload = Zp1Reader.Read(source).DecodedPayload;
        ReadOnlySpan<byte> bytes = payload;
        const int prefix = 24, columns = 2;
        if (bytes.Length < prefix || U32(bytes, 0) != 0x00134C58 ||
            U16(bytes, 4) != (bytes.Length & 0xFFFF) || U16(bytes, 6) != columns ||
            U16(bytes, 10) != columns * sizeof(uint) || U32(bytes, 12) != 0 ||
            U32(bytes, 16) != prefix || U32(bytes, 20) != 0xFFFF0000u)
            throw Missing("XL_header");
        //XL+8 is the resource's UInt16 row count; +A is its row stride.
        //The native fixed-count loop does not subtract1 from that row count.
        int rows = U16(bytes, 8);
        int indexBytes = checked(rows * columns * sizeof(uint));
        if (rows < UsedRows || bytes.Length < prefix + indexBytes)
            throw Missing("XL_rows");

        var patterns = new byte[UsedRows][];
        int previous = -1;
        for (int cell = 0; cell < rows * columns; cell++)
        {
            uint relative = U32(bytes, prefix + cell * sizeof(uint));
            if (relative < indexBytes || relative > bytes.Length - prefix ||
                relative % 2 != 0 || relative <= previous || cell == 0 && relative != indexBytes)
                throw Missing("XL_cell_offset");
            int start = prefix + (int)relative;
            int end = cell + 1 < rows * columns
                ? checked(prefix + (int)U32(bytes, prefix + (cell + 1) * sizeof(uint))) : bytes.Length;
            if (end < start || end > bytes.Length) throw Missing("XL_cell_bounds");
            ReadOnlySpan<byte> raw = bytes.Slice(start, end - start);
            int terminator = raw.IndexOf((byte)0);
            if (terminator < 0 || raw[terminator..].IndexOfAnyExcept((byte)0) >= 0)
                throw Missing("XL_cell_terminator");
            //309F30 copies up to129 bytes, then explicitly terminates byte128.
            //It consumes the first25289 rows regardless of additional resource rows.
            if (cell % columns == 0 && cell / columns < UsedRows)
                patterns[cell / columns] = raw[..Math.Min(128, terminator)].ToArray();
            previous = (int)relative;
        }
        return new(patterns);
    }

    private static Encoding CreateEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    private static ushort U16(ReadOnlySpan<byte> bytes, int at) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(at, sizeof(ushort)));
    private static uint U32(ReadOnlySpan<byte> bytes, int at) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at, sizeof(uint)));
    private static InvalidDataException Missing(string field) => new("personnel.hire.name_filter." + field);
}

public sealed partial class GameResourceRepository
{
    internal PersonnelHireNameFilterSource LoadPersonnelHireNameFilter()
    {
        var (bytes, source) = ReadSource("0001_SC/BadWord.bin");
        try { return PersonnelHireNameFilterSource.Read(bytes); }
        catch (InvalidDataException error)
        {
            throw new InvalidDataException($"Original personnel name filter failed to load: {source.ResourceUri} ({source.PhysicalPath}). {error.Message}", error);
        }
    }
}
