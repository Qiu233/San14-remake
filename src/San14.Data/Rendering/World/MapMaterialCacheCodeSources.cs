using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data;

/// <summary>Type 0BD05B27's F4EA40 source parser and its four byte CPU records.
/// The source column named ShaderType is copied directly into byte0; it is not
/// translated through the separate keyed GBuffer configuration registry.
/// Supports the original unquoted ASCII decimal comma profile. The first
/// physical line is skipped by the native parser's initial row-index equality.
/// Empty/comment lines are skipped; three columns use native optional byte3=0.
/// Unsupported atoi inputs are rejected rather than assigned guessed values.</summary>
public static class MapMaterialCacheCodeSources
{
    public static IReadOnlyList<DeferredMaterialEntry> ReadSourceRecords(LoadedRdbAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        byte[] bytes = asset.Bytes.ToArray();
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(asset.DecodedSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original material ID table source bytes differ from their declared hash.");
        if (bytes.Length == 0 || bytes.Any(value => value > 0x7F || value == 0))
            throw new InvalidDataException("Original material ID table requires the supported nonempty ASCII CSV source.");
        string source = Encoding.ASCII.GetString(bytes);
        var records = new List<DeferredMaterialEntry>();
        string[] lines = source.Split('\n');
        for (int line = 1; line < lines.Length; line++)
        {
            string text = lines[line];
            if (text.EndsWith('\r')) text = text[..^1];
            if (text.Length == 0 || text[0] == ';') continue;
            if (text.Contains('\r') || text.Contains('"'))
                throw new InvalidDataException($"Original material ID table line {line + 1} has an unsupported token profile.");
            string[] values = text.Split(',');
            if (values.Length is < 3 or > 4)
                throw new InvalidDataException($"Original material ID table line {line + 1} requires three or four decimal fields.");
            byte first = ReadByte(values[0], line), rim = ReadByte(values[1], line), ramp = ReadByte(values[2], line);
            byte high = values.Length == 4 && values[3].Length != 0 ? ReadByte(values[3], line) : (byte)0;
            // F4EC65/6E/7F/7A write only R12B/R14B/SIL/DIL respectively.
            records.Add(new(first, rim, ramp, high));
        }
        if (records.Count == 0)
            throw new InvalidDataException("Original material ID table source has no complete native record.");
        return records.AsReadOnly();
    }

    private static byte ReadByte(string token, int line)
    {
        const NumberStyles profile = NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        if (!int.TryParse(token, profile, CultureInfo.InvariantCulture, out int value))
            throw new InvalidDataException($"Original material ID table line {line + 1} requires a supported int32 decimal token.");
        return unchecked((byte)value);
    }
}
