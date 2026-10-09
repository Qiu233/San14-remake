using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace San14.Data.Formats;

public sealed record S14DoctrineDefinition(int Slot, string Name, string SecondaryText,
    string Description, byte[] RawTailBytes, byte[] RawBytes);

public sealed record S14DoctrineCatalog(uint FormatMarker, int RecordOffset, int RecordStride,
    string DecodedSha256, IReadOnlyList<S14DoctrineDefinition> Doctrines);

public sealed record S14CharacterDoctrineReference(int RecordSlot, ushort CharacterId,
    ushort RawDoctrineWord, byte[] RawFieldBytes)
{
    /// <summary>The first source byte; absent in older serialized reference models.</summary>
    public byte? RawDoctrineId { get; init; }
}

public sealed record S14CharacterDoctrineTable(int RecordOffset, int RecordStride,
    int DoctrineFieldOffset, IReadOnlyList<S14CharacterDoctrineReference> Characters);

/// <summary>
/// Reads the inspected fixed-data doctrine records, including unnamed source slots.
/// The twelve tail bytes remain uninterpreted: these definitions alone establish
/// neither a ruler's doctrine nor department capacity, policy cost or expansion rules.
/// </summary>
public static class S14DoctrineReader
{
    private const int Count = 11, Stride = 102;
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);

    /// <summary>
    /// Reads the first source byte independently of its unnamed neighboring byte.
    /// CE SC's CPersonData serializer handles the two bytes separately; the other
    /// supported profiles use the independently inspected corresponding record slots.
    /// The two-byte window and its word view remain diagnostics, not type references.
    /// </summary>
    public static S14CharacterDoctrineTable ReadCharacterReferences(S14Document scenario, string locale)
    {
        S14CharacterTable table = S14ScenarioTables.ReadCharacterNames(scenario, locale);
        IReadOnlyList<S14CharacterIdentity> identities = S14ScenarioTables.ReadCharacterIdentities(scenario, locale);
        int field = (scenario.FormatMarker == 0x33 ? 321 : 330) - (locale == "TC" ? 56 : 0);
        var references = new S14CharacterDoctrineReference[table.Names.Count];
        for (int slot = 0; slot < references.Length; slot++)
        {
            ReadOnlySpan<byte> bytes = scenario.DecodedPayload.AsSpan(table.RecordOffset + slot * table.RecordStride + field, 2);
            references[slot] = new S14CharacterDoctrineReference(slot, identities[slot].CharacterId,
                BinaryPrimitives.ReadUInt16LittleEndian(bytes), bytes.ToArray())
            {
                RawDoctrineId = bytes[0]
            };
        }
        return new S14CharacterDoctrineTable(table.RecordOffset, table.RecordStride, field, Array.AsReadOnly(references));
    }

    public static S14DoctrineCatalog Read(S14Document fixedData)
    {
        int start = fixedData.FormatMarker switch
        {
            0x33 => 237274,
            0x5C => 238438,
            0x66 => 238708,
            _ => throw new InvalidDataException("Unsupported S14 edition for doctrine definitions.")
        };
        ReadOnlySpan<byte> bytes = fixedData.DecodedPayload;
        RequireHeader(bytes, start - 8, 17, Count);
        RequireHeader(bytes, start + Count * Stride, 18, 121);
        var doctrines = new S14DoctrineDefinition[Count];
        for (int slot = 0; slot < doctrines.Length; slot++)
        {
            ReadOnlySpan<byte> row = bytes.Slice(start + slot * Stride, Stride);
            doctrines[slot] = new S14DoctrineDefinition(slot,
                ReadText(row[..8], slot, "name"), ReadText(row.Slice(8, 20), slot, "secondary text"),
                ReadText(row.Slice(28, 62), slot, "description"), row[90..].ToArray(), row.ToArray());
        }
        return new S14DoctrineCatalog(fixedData.FormatMarker, start, Stride,
            Convert.ToHexString(SHA256.HashData(bytes)), Array.AsReadOnly(doctrines));
    }

    private static void RequireHeader(ReadOnlySpan<byte> bytes, int offset, int section, int count)
    {
        if (offset < 0 || offset > bytes.Length - 8 ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4)) != section ||
            BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4, 4)) != count)
            throw new InvalidDataException($"S14 doctrine section {section} boundary or count differs.");
    }

    private static string ReadText(ReadOnlySpan<byte> bytes, int slot, string field)
    {
        string value;
        try { value = StrictUtf16.GetString(bytes); }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException($"S14 doctrine record {slot} {field} has invalid UTF-16LE.", error);
        }
        int end = value.IndexOf('\0');
        if (end < 0) return value;
        if (value.AsSpan(end).IndexOfAnyExcept('\0') >= 0)
            throw new InvalidDataException($"S14 doctrine record {slot} {field} has nonzero text after its terminator.");
        return value[..end];
    }
}
