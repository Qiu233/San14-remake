using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

/// <summary>A source scalar and its destination in the inspected CE serializer.</summary>
public sealed record S14ScenarioSettingsField(int Index, int Offset,
    int NativeMemberOffset, int Width, uint RawValue)
{
    public int SignedValue => Width == 1 ? unchecked((sbyte)RawValue) : unchecked((int)RawValue);
}

/// <summary>Static scenario settings, before the selected new-game options are restored.</summary>
public sealed record S14ScenarioSettingsDocument(int Offset, byte[] RawBytes,
    string RawSha256, IReadOnlyList<S14ScenarioSettingsField> Fields)
{
    public bool FinalPlayerOptionsResolved => false;

    public S14ScenarioSettingsField FieldAtNativeMember(int memberOffset) =>
        Fields.Single(field => field.NativeMemberOffset == memberOffset);
}

/// <summary>
/// Reads the complete 1645-byte settings block serialized by the inspected CE
/// CEnvironment slot 1. Native offsets describe this source adapter only; they
/// are not settings IDs or a layout for the independent simulation state.
/// Unknown values retain both unsigned and signed views without enum coercion.
/// </summary>
public static class S14ScenarioSettingsReader
{
    public const int SerializedBytes = 1645;
    public const int ScalarFieldCount = 49;
    public const int ColumnCount = 61;
    public const int RowCount = 6;
    public const int FieldCount = ScalarFieldCount + ColumnCount * RowCount;

    private static readonly (int Member, int Width)[] ScalarProfile =
    [
        (0x28, 4), (0x2C, 4), (0x30, 4), (0x88, 1), (0x78, 1),
        (0x79, 1), (0x80, 4), (0x3C, 4), (0x40, 4), (0x7B, 1), (0x84, 4),
        // 3E4DB0 reads four adjacent dwords at this point in the stream.
        (0x90, 4), (0x94, 4), (0x98, 4), (0x9C, 4),
        (0xA0, 4), (0xA4, 4), (0xA8, 4), (0xAC, 1),
        (0xB8, 4), (0xBC, 4), (0xC0, 4), (0xC4, 4), (0xC8, 4),
        (0xCC, 4), (0xD0, 4), (0xD4, 4), (0xD8, 4), (0xDC, 4),
        (0xE0, 4), (0xE4, 4), (0xE8, 4), (0xEC, 4),
        (0xF8, 4), (0xFC, 4), (0x100, 4), (0x104, 4), (0x108, 4),
        (0x10C, 4), (0x110, 4),
        (0x114, 4), (0x118, 4), (0x11C, 4), (0x120, 4), (0x124, 4),
        (0x128, 4), (0x12C, 4), (0x130, 4), (0xE58, 4)
    ];

    /// <param name="settings">Exactly one CE settings block, excluding section markers.</param>
    /// <param name="offset">The block's absolute offset in its decoded source.</param>
    public static S14ScenarioSettingsDocument Read(ReadOnlySpan<byte> settings, int offset = 0)
    {
        if (settings.Length != SerializedBytes)
            throw new InvalidDataException($"CE scenario settings require exactly {SerializedBytes} bytes.");
        if (offset < 0 || offset > int.MaxValue - SerializedBytes)
            throw new InvalidDataException("CE scenario settings have an invalid source window.");

        var fields = new S14ScenarioSettingsField[FieldCount];
        int cursor = 0;
        int index = 0;
        foreach ((int member, int width) in ScalarProfile)
        {
            fields[index] = Take(settings, offset, member, width, ref cursor, index);
            index++;
        }

        // The stream visits each of six rows in turn. Members are column arrays
        // with a 24-byte stride; the 35 direct reads and 26 helper reads each
        // consume one dword, not a whole column array.
        for (int row = 0; row < RowCount; row++)
            for (int column = 0; column < ColumnCount; column++)
            {
                fields[index] = Take(settings, offset, 0x134 + column * 24 + row * 4,
                    sizeof(uint), ref cursor, index);
                index++;
            }

        if (cursor != SerializedBytes || index != FieldCount)
            throw new InvalidDataException("CE settings profile does not cover the complete source block.");
        return new(offset, settings.ToArray(), Convert.ToHexString(SHA256.HashData(settings)),
            Array.AsReadOnly(fields));
    }

    private static S14ScenarioSettingsField Take(ReadOnlySpan<byte> source, int offset,
        int member, int width, ref int cursor, int index)
    {
        uint value = width == 1 ? source[cursor]
            : BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(cursor, sizeof(uint)));
        var field = new S14ScenarioSettingsField(index, offset + cursor, member, width, value);
        cursor += width;
        return field;
    }
}
