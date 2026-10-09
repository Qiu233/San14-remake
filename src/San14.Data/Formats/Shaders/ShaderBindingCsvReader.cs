using System.Text;

namespace San14.Data.Formats;

public sealed record ShaderBindingRow(int Index, int SourceOffset, int SourceByteLength,
    string Identifier, uint IdentifierHash, string ObjectName, uint ObjectNameHash,
    string ResourceName, uint ResourceNameHash, byte[] RawBytes);

public sealed record ShaderBindingCatalog(byte[] RawHeader,
    IReadOnlyList<ShaderBindingRow> Rows)
{
    public ShaderBindingRow RequireIdentifier(uint identifierHash)
    {
        ShaderBindingRow[] found = Rows.Where(row => row.IdentifierHash == identifierHash).ToArray();
        if (found.Length != 1)
            throw new InvalidDataException($"Original shader binding identifier {identifierHash:X8} requires one exact row; found {found.Length}.");
        return found[0];
    }
}

/// <summary>Reads the installed three-column shader binding CSV without choosing a default shader.
/// Object and resource names may be aliases; every source row and its bytes are retained.</summary>
public static class ShaderBindingCsvReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const string Header = "CE1ResourceShaderStandardID, id at ObjectDB, id at ResourceDB";

    public static ShaderBindingCatalog Read(ReadOnlySpan<byte> bytes)
    {
        int position = 0;
        byte[] header = ReadLine(bytes, ref position);
        if (Text(header) != Header)
            throw new InvalidDataException("Unsupported original shader binding CSV header.");
        var rows = new List<ShaderBindingRow>();
        var identifiers = new HashSet<uint>();
        while (position < bytes.Length)
        {
            int start = position;
            byte[] raw = ReadLine(bytes, ref position);
            string[] fields = Text(raw).Split(',');
            if (fields.Length != 3 || fields.Any(field => field.Length == 0 ||
                field != field.Trim() || field.IndexOfAny(['\0', '"', '\r', '\n']) >= 0))
                throw new InvalidDataException($"Unsupported original shader binding CSV row {rows.Count}: expected three nonempty unquoted fields.");
            uint identifier = ModelShaderIdentifierHash.Compute(fields[0]);
            if (!identifiers.Add(identifier))
                throw new InvalidDataException($"Ambiguous original shader binding identifier {identifier:X8} at row {rows.Count}.");
            rows.Add(new(rows.Count, start, position - start, fields[0], identifier,
                fields[1], ResourceNameHash.Compute(fields[1]),
                fields[2], ResourceNameHash.Compute(fields[2]), raw));
        }
        if (rows.Count == 0) throw new InvalidDataException("Original shader binding CSV has no data rows.");
        return new(header, rows.AsReadOnly());
    }

    private static byte[] ReadLine(ReadOnlySpan<byte> bytes, ref int position)
    {
        int length = bytes[position..].IndexOf((byte)'\n');
        if (length < 1 || bytes[position + length - 1] != '\r')
            throw new InvalidDataException("Original shader binding CSV requires complete CRLF lines.");
        byte[] raw = bytes.Slice(position, length - 1).ToArray();
        position += length + 1;
        return raw;
    }
    private static string Text(byte[] bytes)
    {
        try { return StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException error)
        { throw new InvalidDataException("Original shader binding CSV is not valid UTF-8.", error); }
    }
}
