using System.Buffers.Binary;
using System.Security.Cryptography;

namespace San14.Data.Formats;

public sealed record S14EpisodeDefinition(int Slot, ushort PersonId, ushort RulerId,
    IReadOnlyList<ushort> SpeakerPersonIds, IReadOnlyList<int> MessageIndices, byte[] RawBytes);

public sealed record S14EpisodeCatalog(uint FormatMarker, int RecordOffset, int RecordStride,
    string TableSha256, IReadOnlyList<S14EpisodeDefinition> Episodes);

/// <summary>Immutable CE fixed-data Episode definitions. Scenario section40
/// contains no additional serialized Episode state. This reader does not
/// select an Episode, promote a person or acknowledge its presentation.</summary>
public static class S14EpisodeReader
{
    private const int HeaderOffset = 361394, Count = 31, Stride = 34;

    public static S14EpisodeCatalog Read(S14Document fixedData)
    {
        ArgumentNullException.ThrowIfNull(fixedData);
        ReadOnlySpan<byte> source = fixedData.DecodedPayload;
        const int start = HeaderOffset + 8, end = start + Count * Stride;
        if (fixedData.FormatMarker != 0x66 || source.Length < end + 8 ||
            BinaryPrimitives.ReadUInt32LittleEndian(source) != fixedData.FormatMarker ||
            BinaryPrimitives.ReadInt32LittleEndian(source.Slice(HeaderOffset, 4)) != 40 ||
            BinaryPrimitives.ReadInt32LittleEndian(source.Slice(HeaderOffset + 4, 4)) != Count ||
            BinaryPrimitives.ReadInt32LittleEndian(source.Slice(end, 4)) != 41 ||
            BinaryPrimitives.ReadInt32LittleEndian(source.Slice(end + 4, 4)) != 80)
            throw new InvalidDataException("Episode definitions require the CE fixed section40/31/34-byte profile.");
        var episodes = new S14EpisodeDefinition[Count];
        for (int slot = 0; slot < episodes.Length; slot++)
        {
            ReadOnlySpan<byte> row = source.Slice(start + slot * Stride, Stride);
            var speakers = new ushort[5];
            var messages = new int[5];
            for (int line = 0; line < speakers.Length; line++)
            {
                speakers[line] = BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(4 + line * 2, 2));
                messages[line] = BinaryPrimitives.ReadInt32LittleEndian(row.Slice(14 + line * 4, 4));
            }
            episodes[slot] = new(slot, BinaryPrimitives.ReadUInt16LittleEndian(row),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(2, 2)),
                Array.AsReadOnly(speakers), Array.AsReadOnly(messages), row.ToArray());
        }
        return new(fixedData.FormatMarker, start, Stride,
            Convert.ToHexString(SHA256.HashData(source.Slice(start, Count * Stride))),
            Array.AsReadOnly(episodes));
    }
}
