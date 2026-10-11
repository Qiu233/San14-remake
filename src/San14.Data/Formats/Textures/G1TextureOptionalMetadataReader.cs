using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

/// <summary>A detached typed record from the texture's declared optional pool.</summary>
public sealed class G1TextureOptionalRecord
{
    internal G1TextureOptionalRecord(ushort type, byte rawEndFlag, G1EncodedRange range, uint[] words)
    {
        Type = type; RawEndFlag = rawEndFlag; EncodedRange = range;
        Words = Array.AsReadOnly(words);
    }
    public ushort Type { get; }
    public byte RawEndFlag { get; }
    public G1EncodedRange EncodedRange { get; }
    public IReadOnlyList<uint> Words { get; }
}

public sealed class G1TextureImageOptionalMetadata
{
    internal G1TextureImageOptionalMetadata(int index, G1TextureOptionalRecord[] records)
    { ImageIndex = index; Records = Array.AsReadOnly(records); }
    public int ImageIndex { get; }
    public IReadOnlyList<G1TextureOptionalRecord> Records { get; }
    /// <summary>The original lookup returns the first matching typed record.</summary>
    public G1TextureOptionalRecord? Find(ushort type) => Records.FirstOrDefault(record => record.Type == type);
}

public sealed class G1TextureOptionalMetadata
{
    internal G1TextureOptionalMetadata(int bytes, G1EncodedRange pool, G1TextureImageOptionalMetadata[] images)
    { SourceByteLength = bytes; PoolRange = pool; Images = Array.AsReadOnly(images); }
    public int SourceByteLength { get; }
    public G1EncodedRange PoolRange { get; }
    public IReadOnlyList<G1TextureImageOptionalMetadata> Images { get; }
}

/// <summary>Reads the Windows G1T optional pool after the image directory.
/// Each image's base-header high nibble at byte 3 gives its record count.
/// A record contains ushort type, byte payload-word count, byte end flag,
/// then that many uint words. Native sets end flags from these counts after
/// loading; serialized end flags do not determine record ownership.</summary>
public static class G1TextureOptionalMetadataReader
{
    public static G1TextureOptionalMetadata Read(ReadOnlySpan<byte> source)
    {
        if (source.Length < 32 || !source[..4].SequenceEqual("GT1G"u8))
            throw Error("missing container header");
        string version = Encoding.ASCII.GetString(source.Slice(4, 4));
        uint platform = U32(source, 20);
        bool legacy = version == "0500" && platform == 0;
        if (!legacy && (version is not ("0600" or "1600" or "4600") || platform != 10))
            throw Error("unsupported container version or platform");
        int table = Number(source, 12), count = Number(source, 16);
        if (count < 1 || count > 10_000 || table < 28 || table > source.Length - 4L * count)
            throw Error("invalid image directory");
        int poolStart = checked(table + count * 4);
        int poolBytes = legacy ? 0 : Number(source, 24);
        if (poolBytes > source.Length - poolStart)
            throw Error("optional pool exceeds the container");
        int poolEnd = poolStart + poolBytes;
        var images = new G1TextureImageOptionalMetadata[count];
        int cursor = poolStart, previousStart = poolEnd;
        for (int image = 0; image < count; image++)
        {
            long start = table + (long)U32(source, table + image * 4);
            long end = image + 1 < count ? table + (long)U32(source, table + (image + 1) * 4) : source.Length;
            if (start < poolEnd || (image == 0 && start != poolEnd) ||
                (image > 0 && start <= previousStart) || end > source.Length || start > end - 8)
                throw Error("image boundary overlaps the directory, optional pool or another image");
            previousStart = (int)start;
            int records = source[(int)start + 3] >> 4;
            var values = new G1TextureOptionalRecord[records];
            for (int record = 0; record < records; record++)
            {
                if (cursor > poolEnd - 4) throw Error("typed record header exceeds the optional pool");
                ushort type = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(cursor, 2));
                int words = source[cursor + 2], bytes = 4 + words * 4;
                if (bytes > poolEnd - cursor) throw Error("typed record payload exceeds the optional pool");
                var payload = new uint[words];
                for (int word = 0; word < words; word++) payload[word] = U32(source, cursor + 4 + word * 4);
                values[record] = new(type, source[cursor + 3], new(cursor, bytes), payload);
                cursor += bytes;
            }
            images[image] = new(image, values);
        }
        if (cursor != poolEnd) throw Error("image record counts do not cover the optional pool");
        return new(source.Length, new(poolStart, poolBytes), images);
    }

    private static uint U32(ReadOnlySpan<byte> source, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
    private static int Number(ReadOnlySpan<byte> source, int offset)
    {
        uint value = U32(source, offset);
        return value <= int.MaxValue ? (int)value : throw Error("offset or length exceeds supported addressing");
    }
    private static InvalidDataException Error(string message) => new("G1T optional metadata: " + message + ".");
}
