using System.Buffers.Binary;

namespace San14.Data.Formats;

public sealed record TaskGraphEndRegisterSource(int SourceOffset, uint FactoryClassId,
    uint DeclaredArgumentCount, IReadOnlyList<uint> ConnectedNodeIndices,
    uint ViewObjectNameHash, bool HasExplicitViewObjectNameHash, int? ViewKeySourceOffset,
    int ParsedPrefixBytes, byte[] RawBytes)
{
    public int UninterpretedTrailingBytes => RawBytes.Length - ParsedPrefixBytes;
}

/// <summary>Read an already identified original EndRegisterPrimitive call-metadata fragment.
/// This is not a TaskGraph file detector, execution graph or active-View selector.</summary>
public static class TaskGraphEndRegisterReader
{
    public const uint FactoryClassId = 0x80D359AE;
    public const uint ViewObjectNameHashProperty = 0x72DB7EBD;

    /// <param name="metadata">Bytes beginning at the original call metadata M.
    /// Preserve any trailing bytes; the caller owns the module and fragment boundaries.</param>
    /// <param name="sourceOffset">The fragment's explicit byte offset in its parent source.</param>
    public static TaskGraphEndRegisterSource Read(ReadOnlySpan<byte> metadata, int sourceOffset = 0) =>
        Read(TaskGraphCallReader.Read(metadata, sourceOffset));

    public static TaskGraphEndRegisterSource Read(TaskGraphCallMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.FactoryClassId != FactoryClassId)
            throw new InvalidDataException("Call metadata is not the original EndRegisterPrimitive factory.");
        // The native copy takes the first exact packed-word/name match.
        // High raw count bits can alias the packed descriptor; match the
        // generated complete word, rather than raw type/count separately.
        TaskGraphArgument? field = metadata.Arguments.FirstOrDefault(a =>
            a.DescriptorWord == 0x05000001 && a.NameHash == ViewObjectNameHashProperty);
        uint key = 0;
        if (field is not null)
        {
            if (field.RawValue.Length != 4)
                throw new InvalidDataException("Original EndRegister scalar literal does not contain exactly four bytes.");
            key = BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        return new(metadata.SourceOffset, metadata.FactoryClassId, (uint)metadata.Arguments.Count,
            Array.AsReadOnly(metadata.ConnectedNodeIndices.ToArray()), key, field is not null,
            field?.LiteralSourceOffset, metadata.ParsedPrefixBytes, (byte[])metadata.RawBytes.Clone());
    }
}
