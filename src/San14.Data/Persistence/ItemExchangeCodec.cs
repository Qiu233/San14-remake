using System.Text;
using San14.Core;

namespace San14.Data;

public sealed record ItemExchangeRecord(string Name, string Description, ItemEditorValues Values);

/// <summary>The original item exchange wire; it is separate from the title
/// library archive and cannot assign an item to a campaign or library slot.</summary>
public static class ItemExchangeCodec
{
    private static readonly byte[] Signature = Encoding.ASCII.GetBytes("SAN14EDITITEM\0\0\0");
    private static readonly UnicodeEncoding Utf16 = new(false, false, true);
    public const int CurrentLength = 204;

    public static byte[] Write(CreatedItem item, int? portableImageIndex = null)
    {
        item.Validate();
        int image = portableImageIndex ?? (item.Values.ImageIndex < 201 ? item.Values.ImageIndex
            : throw new InvalidDataException("External item artwork requires the actual source replacement image."));
        if (image is < 0 or > 456) throw new InvalidDataException("Portable item image is out of range.");
        using var stream = new MemoryStream(CurrentLength);
        using var writer = new BinaryWriter(stream, Utf16, true);
        writer.Write(Signature); writer.Write(1u);
        writer.Write((byte)2); writer.Write((byte)0); // Current record version; portable id.
        writer.Write((byte)(image >= 201 ? 0x10 : 0));
        writer.Write((byte)item.Values.Kind); writer.Write((byte)item.Values.AbilityKind);
        writer.Write((byte)item.Values.AbilityValue); writer.Write((byte)item.Values.EffectKind);
        writer.Write((byte)item.Values.EffectValue); writer.Write((byte)item.Values.Worth);
        writer.Write((byte)(image >= 201 ? image - 201 : image));
        Text(writer, item.Name, 7); Text(writer, "", 19); Text(writer, item.Description, 61);
        return stream.ToArray();
    }

    /// <summary>210E40 normalizes the candidate, discards its original id and
    /// secondary reading, and finally clears the external image-bank bit.</summary>
    public static ItemExchangeRecord Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 21 || !data[..16].SequenceEqual(Signature))
            throw new InvalidDataException("Invalid item exchange signature.");
        int version = data[20];
        if (version > 2) throw new InvalidDataException("Unsupported item exchange record version.");
        int length = version >= 2 ? CurrentLength : CurrentLength - 1;
        if (data.Length < length) throw new InvalidDataException("Truncated item exchange record.");
        using var stream = new MemoryStream(data[..length].ToArray(), false);
        using var reader = new BinaryReader(stream, Utf16, true);
        stream.Position = 21;
        reader.ReadByte(); // Portable identity is not adopted.
        if (version >= 2) reader.ReadByte(); // Final import clears the external bank bit.
        var values = new ItemEditorValues(Math.Min(reader.ReadByte(), (byte)26),
            Math.Min(reader.ReadByte(), (byte)5), Math.Min(reader.ReadByte(), (byte)10),
            Math.Min(reader.ReadByte(), (byte)2), Math.Min(reader.ReadByte(), (byte)10),
            Math.Min(reader.ReadByte(), (byte)50), reader.ReadByte());
        string name = Text(reader, 7);
        Text(reader, 19); // The original preview/adoption does not copy this reading.
        string description = Text(reader, 61);
        return new(name, description, values);
    }

    public static bool HasSignature(ReadOnlySpan<byte> data) => data.Length >= 16 && data[..16].SequenceEqual(Signature);
    private static void Text(BinaryWriter writer, string text, int units)
    {
        byte[] bytes = Utf16.GetBytes(text);
        if (text.Contains('\0') || bytes.Length >= units * 2) throw new InvalidDataException("Portable item text exceeds its source window.");
        writer.Write(bytes); writer.Write(new byte[units * 2 - bytes.Length]);
    }
    private static string Text(BinaryReader reader, int units)
    {
        byte[] bytes = reader.ReadBytes(units * 2);
        if (bytes.Length != units * 2) throw new InvalidDataException("Truncated item exchange text.");
        string value = Utf16.GetString(bytes);
        int end = value.IndexOf('\0');
        if (end < 0) throw new InvalidDataException("Item exchange text lacks its source terminator.");
        return value[..end];
    }
}
