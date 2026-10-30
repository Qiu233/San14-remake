using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using San14.Core;

namespace San14.Data.Formats;

/// <summary>The uncompressed EventSample locale archive: 100 serialized
/// headers, the fixed event bank, 1000 article headers and the article bank.
/// Object vtables, padding and the transient editor manager are not stored.</summary>
public static class EditedEventSampleReader
{
    private const int HeaderBytes = 253;
    private const int EventBankBytes = 512000;
    private const int MessageHeaderBytes = 10;
    private const int MessageBankBytes = 128000;
    private const int EventBankCount = HeaderBytes * EditedEventLibrary.EventCapacity;
    private const int EventBankStart = EventBankCount + 4;
    private const int MessageHeadersStart = EventBankStart + EventBankBytes;
    private const int MessageBankCount = MessageHeadersStart + MessageHeaderBytes * EditedEventLibrary.MessageCapacity;
    private const int MessageBankStart = MessageBankCount + 4;
    private static readonly Encoding Utf16 = new UnicodeEncoding(false, false, true);

    public static EditedEventSampleCatalog Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != MessageBankStart + MessageBankBytes)
            throw Invalid("archive_length");
        int eventUsed = I32(bytes, EventBankCount), messageUsed = I32(bytes, MessageBankCount);
        if (eventUsed is < 0 or > EventBankBytes || messageUsed is < 0 or > MessageBankBytes || (messageUsed & 1) != 0)
            throw Invalid("bank_length");
        var eventBank = bytes.Slice(EventBankStart, eventUsed);
        var messageBank = bytes.Slice(MessageBankStart, messageUsed);
        var messages = ImmutableArray.CreateBuilder<EditedEventMessage>();
        for (int slot = 0; slot < EditedEventLibrary.MessageCapacity; slot++)
        {
            var header = bytes.Slice(MessageHeadersStart + slot * MessageHeaderBytes, MessageHeaderBytes);
            int id = U16(header, 0), offset = I32(header, 2), length = I32(header, 6);
            if (id != slot && id != 10000 + slot) throw Invalid("message_identity");
            if (length == 0)
            {
                if (offset != -1) throw Invalid("empty_message");
                continue;
            }
            if (length < 1 || offset < 0 || (offset & 1) != 0 || (long)offset + 2L * length > messageBank.Length)
                throw Invalid("message_window");
            string text = Text(messageBank.Slice(offset, checked(length * 2)), true);
            if (text.Length == 0) throw Invalid("empty_message");
            messages.Add(new(slot, text));
        }
        var events = ImmutableArray.CreateBuilder<EditedEventDefinition>();
        for (int slot = 0; slot < EditedEventLibrary.EventCapacity; slot++)
        {
            var header = bytes.Slice(slot * HeaderBytes, HeaderBytes);
            if (U16(header, 0) != 1) throw Invalid("event_format");
            int id = U16(header, 2);
            if (id != slot && id != 5000 + slot) throw Invalid("event_identity");
            string name = Text(header.Slice(4, 26), false);
            int offset = I32(header, 214), conditionBytes = I32(header, 222);
            int contentBytes = I32(header, 236), resultBytes = I32(header, 240);
            if (name.Length == 0)
            {
                if (offset != -1 || conditionBytes != 0 || contentBytes != 0 || resultBytes != 0)
                    throw Invalid("empty_event");
                continue;
            }
            if (header[30] >= 5 || offset < 0 || (offset & 3) != 0 || conditionBytes <= 0 ||
                contentBytes <= 0 || contentBytes % (6 * 44) != 0 || resultBytes < 48 ||
                (long)offset + conditionBytes + contentBytes + resultBytes + 4 > eventBank.Length)
                throw Invalid("event_window");
            var conditions = Program(eventBank.Slice(offset, conditionBytes));
            var contents = ImmutableArray.CreateBuilder<EditedEventContent>();
            int contentStart = checked(offset + conditionBytes);
            for (int index = 0; index < contentBytes; index += 6 * 44)
            {
                var content = new EditedEventContent(Program(eventBank.Slice(contentStart + index, 6 * 44)));
                _ = EditedEventContentEditing.Read(content);
                contents.Add(content);
            }
            int resultStart = checked(contentStart + contentBytes);
            var results = Results(eventBank.Slice(resultStart, resultBytes));
            if (I32(eventBank, checked(resultStart + resultBytes)) != 0) throw Invalid("result_terminator");
            int flags = U16(header, 246);
            var value = new EditedEventDefinition(slot, name, Text(header.Slice(32, 182), false),
                header[30], (flags & 16) != 0, (flags & 4) != 0, (flags & 8) != 0,
                conditions, contents.ToImmutable(), results);
            value.Validate();
            // Structural compilation is a resource-load boundary, not a
            // campaign evaluation or a per-frame correctness scan.
            _ = EditedEventProgramCompiler.Compile(conditions, slot);
            _ = EditedEventProgramCompiler.Compile(results, slot);
            events.Add(value);
        }
        return new(new(events.ToImmutable(), messages.ToImmutable()));
    }

    private static EditedEventProgram Program(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length % 44 != 0) throw Invalid("statement_window");
        var result = ImmutableArray.CreateBuilder<EditedEventInstruction>(bytes.Length / 44);
        for (int index = 0; index < bytes.Length; index += 44)
        {
            var record = bytes.Slice(index, 44);
            var operands = ImmutableArray.CreateBuilder<int>(8);
            for (int operand = 0; operand < 8; operand++) operands.Add(I32(record, 12 + 4 * operand));
            var instruction = new EditedEventInstruction(I32(record, 8), operands.ToImmutable(),
                BinaryPrimitives.ReadInt16LittleEndian(record[4..]), BinaryPrimitives.ReadInt16LittleEndian(record[6..]));
            instruction.Validate();
            result.Add(instruction);
        }
        return new(result.ToImmutable());
    }

    private static EditedEventProgram Results(ReadOnlySpan<byte> bytes)
    {
        var result = ImmutableArray.CreateBuilder<EditedEventInstruction>();
        int cursor = 0;
        while (cursor < bytes.Length)
        {
            if (bytes.Length - cursor < 4) throw Invalid("result_group");
            int count = I32(bytes, cursor);
            cursor += 4;
            if (count <= 0 || 44L * count > bytes.Length - cursor) throw Invalid("result_group");
            int length = checked(44 * count);
            result.AddRange(Program(bytes.Slice(cursor, length)).Instructions);
            cursor += length;
        }
        return new(result.ToImmutable());
    }

    private static string Text(ReadOnlySpan<byte> bytes, bool packed)
    {
        int end = 0;
        while (end < bytes.Length && U16(bytes, end) != 0) end += 2;
        if (end == bytes.Length || (packed && end + 2 != bytes.Length)) throw Invalid("text_terminator");
        try { return Utf16.GetString(bytes[..end]); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("event_editor.samples.text_encoding", error); }
    }
    private static int I32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
    private static int U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static InvalidDataException Invalid(string reason) => new("event_editor.samples." + reason);
}
