using System.Security.Cryptography;

namespace San14.Data.Formats;

/// <summary>
/// An explicitly supported CE execution stream. Parsing the complete stream
/// rejects unknown instructions before an execution provider can make writes.
/// The shared dispatcher is reused with its original per-operation gates.
/// </summary>
public sealed record S14EventExecutionProgram(S14EventConditionProgram DispatchProgram,
    string StreamSha256, string? ResourceSha256 = null, int? EventId = null)
{
    public static S14EventExecutionProgram Read(S14EventStream stream)
    {
        S14EventConditionProgram program = S14EventConditionProgram.Read(stream);
        foreach (S14ConditionInstruction operation in program.Operations)
            if (!S14AreaEventExecutionRuntime.IsSupported(operation.Code))
                throw new InvalidDataException($"Execution stream {stream.Index}, operation {operation.Index}, " +
                    $"offset {operation.Offset}: no executable binding for code {operation.Code}.");
        return new(program, Convert.ToHexString(SHA256.HashData(
            program.Operations.SelectMany(operation => operation.RawBytes).ToArray())));
    }

    /// <summary>Directory lookup is eventId minus the resource's native base ID.</summary>
    public static S14EventExecutionProgram ReadEvent(S14EventDocument document, int eventId)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.RawBytes is null || document.Events is null)
            throw new InvalidDataException("Event resource bytes or records are unknown.");
        S14EventDocument canonical = S14EventReader.Read(document.RawBytes);
        if (canonical.SourceSha256 != document.SourceSha256 ||
            canonical.NativeBaseId != document.NativeBaseId ||
            canonical.Events.Count != document.Events.Count)
            throw new InvalidDataException("Event execution document disagrees with its source bytes.");
        long index = (long)eventId - canonical.NativeBaseId;
        if (index < 0 || index >= canonical.ExecutionStreams.Count)
            throw new InvalidDataException($"Event ID {eventId} is outside the native execution directory.");
        return Read(canonical.ExecutionStreams[(int)index]) with
        { ResourceSha256 = canonical.SourceSha256, EventId = eventId };
    }
}
