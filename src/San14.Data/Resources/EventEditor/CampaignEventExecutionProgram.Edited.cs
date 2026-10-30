using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class CampaignEventExecutionProgram
{
    public bool IsEditedProgram { get; private init; }
    /// <summary>Uses a prepared project-owned editor program directly. It is
    /// not represented as an original resource directory or reparsed S14 file.</summary>
    public static CampaignEventExecutionProgram ReadEditedProgram(EventSourceReference source,
        S14EventConditionProgram program)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(program);
        PlayableEventStateValidation.ValidateSource(source);
        if (source.DirectoryIndex is < 0 or >= 100 || source.EventId != source.DirectoryIndex ||
            program.StreamIndex != source.DirectoryIndex)
            throw new InvalidDataException("Edited event program identity differs from its library slot.");
        return new(source, program) { IsEditedProgram = true };
    }
}
