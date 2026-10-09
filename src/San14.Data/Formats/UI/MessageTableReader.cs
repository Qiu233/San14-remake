using System.Buffers.Binary;
using System.Text;

namespace San14.Data.Formats;

public enum MessageKind { Data, Game }

public sealed record MessageLiteral(int Offset, int ByteLength, string Text);
public sealed record MessageUninterpretedRange(int Offset, byte[] RawBytes);
public sealed record MessageRecord(int Index, int SourceOffset, byte[] RawBytes,
    string? Text, IReadOnlyList<MessageLiteral> Literals,
    IReadOnlyList<MessageUninterpretedRange> UninterpretedRanges);
public sealed record MessageSection(int Index, int SourceOffset, int Length,
    byte[] HeaderBytes, IReadOnlyList<uint> RecordOffsets,
    IReadOnlyList<MessageRecord> Records);
public sealed record MessageDocument(MessageKind Kind, byte[] DecodedPayload,
    IReadOnlyList<MessageSection> Sections);

/// <summary>
/// Reads msgdata's UTF-16 tables and msggame's indexed bytecode records. Game literal
/// spans are exposed separately; control bytes retain their source positions and are
/// not evaluated or silently removed to produce a supposedly complete message.
/// </summary>
public static class MessageTableReader
{
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);
    private static ReadOnlySpan<byte> LiteralStart => [7, 7, 1];
    private static ReadOnlySpan<byte> LiteralEnd => [7, 7, 2];

    public static MessageDocument Read(ReadOnlySpan<byte> decoded, MessageKind kind)
    {
        if (kind is not (MessageKind.Data or MessageKind.Game))
            throw new ArgumentOutOfRangeException(nameof(kind));
        var root = ReadDirectory(decoded, 0);
        if (root.Count != (kind == MessageKind.Data ? 1 : 10))
            throw new InvalidDataException($"Original {kind} message directory has an unsupported section count.");
        IReadOnlyList<DirectoryEntry> entries = root;
        if (kind == MessageKind.Data)
        {
            DirectoryEntry parent = root[0];
            entries = ReadDirectory(decoded.Slice(parent.Offset, parent.Length), parent.Offset);
            if (entries.Count != 4)
                throw new InvalidDataException("Original data message directory must contain four text tables.");
        }
        var sections = new List<MessageSection>(entries.Count);
        foreach (DirectoryEntry entry in entries)
        {
            ReadOnlySpan<byte> bytes = decoded.Slice(entry.Offset, entry.Length);
            int prefix = kind == MessageKind.Data ? 24 : 0;
            int count;
            int indexStart;
            if (kind == MessageKind.Data)
            {
                if (bytes.Length < 28 || U32(bytes, 0) != 0x00134C58 || U32(bytes, 12) != 1 ||
                    U32(bytes, 16) != 24 || U32(bytes, 20) != 0xFFFFFF00)
                    throw new InvalidDataException($"Data message section {entry.Index} has an unsupported XL header.");
                uint firstOffset = U32(bytes, prefix);
                if (firstOffset == 0 || firstOffset % 4 != 0 || firstOffset > bytes.Length - prefix)
                    throw new InvalidDataException($"Data message section {entry.Index} has an invalid index size.");
                count = (int)(firstOffset / 4);
                // These tagged fields are retained verbatim; only their observed count/length bits are checked.
                if (U32(bytes, 8) != 0x40000u + count || (U32(bytes, 4) & 0xFFFF) != (uint)(bytes.Length & 0xFFFF))
                    throw new InvalidDataException($"Data message section {entry.Index} tagged count/length differs.");
                indexStart = prefix;
            }
            else
            {
                if (bytes.Length < 8 || U32(bytes, 0) == 0 || U32(bytes, 0) > (bytes.Length - 4) / 4)
                    throw new InvalidDataException($"Game message section {entry.Index} has an invalid record count.");
                count = (int)U32(bytes, 0);
                indexStart = 4;
            }
            int indexEnd = indexStart + count * 4;
            var offsets = new uint[count];
            for (int index = 0; index < count; index++)
            {
                uint offset = U32(bytes, indexStart + index * 4);
                if (offset > bytes.Length - prefix || offset < indexEnd - prefix ||
                    (index == 0 && offset != indexEnd - prefix) ||
                    (index > 0 && offset <= offsets[index - 1]))
                    throw new InvalidDataException($"Message section {entry.Index} record {index} offset is invalid.");
                offsets[index] = offset;
            }
            var records = new List<MessageRecord>(count);
            for (int index = 0; index < count; index++)
            {
                int start = prefix + (int)offsets[index];
                int end = index + 1 < count ? prefix + (int)offsets[index + 1] : bytes.Length;
                byte[] raw = bytes.Slice(start, end - start).ToArray();
                string? text = null;
                IReadOnlyList<MessageLiteral> literals;
                IReadOnlyList<MessageUninterpretedRange> uninterpreted;
                if (kind == MessageKind.Data)
                {
                    if (raw.Length < 4 || raw.Length % 2 != 0 || raw.AsSpan(raw.Length - 4).IndexOfAnyExcept((byte)0) >= 0)
                        throw new InvalidDataException($"Data message section {entry.Index} record {index} has no double UTF-16 terminator.");
                    text = DecodeUtf16(raw, entry.Index, index).TrimEnd('\0');
                    if (text.Contains('\0'))
                        throw new InvalidDataException($"Data message section {entry.Index} record {index} has embedded null characters.");
                    literals = [new MessageLiteral(0, StrictUtf16.GetByteCount(text), text)];
                    uninterpreted = [];
                }
                else
                {
                    (literals, uninterpreted) = ReadGameLiterals(raw, entry.Index, index);
                }
                records.Add(new MessageRecord(index, entry.Offset + start, raw, text, literals, uninterpreted));
            }
            sections.Add(new MessageSection(entry.Index, entry.Offset, entry.Length,
                bytes[..indexStart].ToArray(), offsets, records));
        }
        return new MessageDocument(kind, decoded.ToArray(), sections);
    }

    private static (IReadOnlyList<MessageLiteral>, IReadOnlyList<MessageUninterpretedRange>)
        ReadGameLiterals(byte[] bytes, int section, int index)
    {
        var literals = new List<MessageLiteral>();
        var ranges = new List<MessageUninterpretedRange>();
        int position = 0;
        while (position < bytes.Length)
        {
            int relative = bytes.AsSpan(position).IndexOf(LiteralStart);
            if (relative < 0) break;
            int marker = position + relative, start = marker + LiteralStart.Length, end = start;
            if (marker > position)
                ranges.Add(new MessageUninterpretedRange(position, bytes.AsSpan(position, marker - position).ToArray()));
            while (end <= bytes.Length - LiteralEnd.Length &&
                !bytes.AsSpan(end, LiteralEnd.Length).SequenceEqual(LiteralEnd)) end += 2;
            if (end > bytes.Length - LiteralEnd.Length)
                throw new InvalidDataException($"Game message section {section} record {index} literal has no aligned end marker.");
            string text = DecodeUtf16(bytes.AsSpan(start, end - start), section, index);
            literals.Add(new MessageLiteral(start, end - start, text));
            // Delimiters are retained as uninterpreted bytes as well; their evaluation is not implemented.
            ranges.Add(new MessageUninterpretedRange(marker, LiteralStart.ToArray()));
            ranges.Add(new MessageUninterpretedRange(end, LiteralEnd.ToArray()));
            position = end + LiteralEnd.Length;
        }
        if (position < bytes.Length)
            ranges.Add(new MessageUninterpretedRange(position, bytes.AsSpan(position).ToArray()));
        return (literals, ranges);
    }

    private sealed record DirectoryEntry(int Index, int Offset, int Length);

    private static IReadOnlyList<DirectoryEntry> ReadDirectory(ReadOnlySpan<byte> bytes, int origin)
    {
        if (bytes.Length < 4 || U32(bytes, 0) == 0 || U32(bytes, 0) > (bytes.Length - 4) / 8)
            throw new InvalidDataException("Message directory count is invalid.");
        int count = (int)U32(bytes, 0), expected = 4 + count * 8;
        var entries = new List<DirectoryEntry>(count);
        for (int index = 0; index < count; index++)
        {
            uint offset = U32(bytes, 4 + index * 8), length = U32(bytes, 8 + index * 8);
            if (offset != expected || length == 0 || length > bytes.Length - expected)
                throw new InvalidDataException($"Message directory entry {index} has an invalid offset or length.");
            int end = expected + (int)length;
            long aligned = ((long)end + 3) / 4 * 4;
            if (aligned > bytes.Length || bytes.Slice(end, (int)aligned - end).IndexOfAnyExcept((byte)0) >= 0)
                throw new InvalidDataException($"Message directory entry {index} has missing or nonzero padding.");
            entries.Add(new DirectoryEntry(index, origin + expected, (int)length));
            expected = (int)aligned;
        }
        if (expected != bytes.Length)
            throw new InvalidDataException("Message directory contains unindexed trailing bytes.");
        return entries;
    }

    private static string DecodeUtf16(ReadOnlySpan<byte> bytes, int section, int index)
    {
        try { return StrictUtf16.GetString(bytes); }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException($"Message section {section} record {index} contains invalid UTF-16LE.", error);
        }
    }

    private static uint U32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
}
