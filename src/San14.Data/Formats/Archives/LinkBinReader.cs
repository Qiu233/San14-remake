using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record LinkBinEntry(int Index, uint Offset, uint Length)
{
    public bool HasPayload => Length != 0;
}

public sealed record LinkBinIndex(long ContainerLength, IReadOnlyList<LinkBinEntry> Entries);

/// <summary>The little-endian mode0 LinkBin count and absolute offset/length
/// table. Reading this index never loads or hashes the package payloads.</summary>
public static class LinkBinReader
{
    public static int IndexLength(ReadOnlySpan<byte> header)
    {
        if (header.Length < 4) throw new InvalidDataException("linkbin.header_truncated");
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (count is < 1 or > 1999) throw new InvalidDataException("linkbin.count_unsupported");
        return checked(4 + (int)count * 8);
    }

    public static LinkBinIndex ReadIndex(ReadOnlySpan<byte> bytes, long containerLength)
    {
        int size = IndexLength(bytes);
        if (bytes.Length != size || containerLength < size)
            throw new InvalidDataException("linkbin.index_truncated");
        var entries = new LinkBinEntry[(size - 4) / 8];
        for (int i = 0; i < entries.Length; i++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4 + i * 8, 4));
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8 + i * 8, 4));
            //An empty source slot remains empty. Shared payload spans are legal;
            //the format does not require a monotonic or nonoverlapping table.
            if (length != 0 && (offset < size || (ulong)offset + length > (ulong)containerLength))
                throw new InvalidDataException("linkbin.entry_bounds:" + i);
            entries[i] = new(i, offset, length);
        }
        return new(containerLength, Array.AsReadOnly(entries));
    }
}
