using System.Buffers.Binary;
using System.Text;
using San14.Core;

namespace San14.Data;

/// <summary>The ordinary portable personality record, independently of the
/// application's versioned library. Portable IDs and the unused second text
/// buffer are discarded by the original importer.</summary>
public static class PersonalityEditorExchangeCodec
{
    private static readonly byte[] Signature = Encoding.ASCII.GetBytes("SAN14EDITPERSONALITY\0");
    public const int FileBytes = 91;
    public static bool HasSignature(ReadOnlySpan<byte> data) =>
        data.Length >= 28 && data[..Signature.Length].SequenceEqual(Signature);
    public static byte[] Write(CreatedPersonality value)
    {
        value.Validate();
        var bytes = new byte[FileBytes];
        Signature.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24, 4), 1);
        var row = bytes.AsSpan(28);
        row[0] = 2; // The base record always writes version two.
        // ID is zero and secondary text is empty in the portable copy.
        Encoding.Unicode.GetBytes(value.Name.AsSpan(), row.Slice(3, 12));
        row[47] = checked((byte)value.ConditionKind);
        row[48] = checked((byte)value.OptionKind);
        row[49] = checked((byte)value.Effect1Code);
        BinaryPrimitives.WriteInt16LittleEndian(row.Slice(50, 2), checked((short)value.Effect1Value));
        row[52] = checked((byte)value.Effect2Code);
        BinaryPrimitives.WriteInt16LittleEndian(row.Slice(53, 2), checked((short)value.Effect2Value));
        BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(55, 4), value.ConditionValue);
        BinaryPrimitives.WriteUInt32LittleEndian(row.Slice(59, 4), value.OptionValue);
        return bytes;
    }
    public static CreatedPersonality Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < FileBytes || !HasSignature(bytes))
            throw new InvalidDataException("Invalid portable personality signature or truncated record.");
        var row = bytes.Slice(28, 63);
        string name = Encoding.Unicode.GetString(row.Slice(3, 12));
        int terminator = name.IndexOf('\0');
        if (terminator >= 0) name = name[..terminator];
        // The actual import setter accepts a three-unit buffer including NUL.
        if (name.Length > 2) name = "";
        return new(256, name, Math.Min((int)row[47], 100),
            BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(55, 4)),
            Math.Min((int)row[48], 100), BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(59, 4)),
            Math.Min((int)row[49], 126), Math.Clamp((int)BinaryPrimitives.ReadInt16LittleEndian(row.Slice(50, 2)), -100, 2000),
            Math.Min((int)row[52], 126), Math.Clamp((int)BinaryPrimitives.ReadInt16LittleEndian(row.Slice(53, 2)), -100, 2000));
    }
}
