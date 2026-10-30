using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>327's actual age scalar after its resolved Person-validity
    /// gate. Shares the ordinary current age rule, including timeless modes.</summary>
    internal int ReadStoryPersonMessageAgeCurrent(GameState state, SourceWorldContext current,
        S14EventConditionPersonReference person) => PersonCurrentAgeSource.Read(state, current, person.PhysicalSlot);

    /// <summary>435's literal Doctrine-table selector. It is not a Person,
    /// Force or one-based display identity. Invalid receivers write known
    /// empty names; an omitted current definition remains unknown.</summary>
    internal string ReadStoryDoctrineMessageNameCurrent(GameState state, SourceWorldContext current, int raw)
    {
        //The native signed comparison would index before its pointer table
        //for negative operands. No safe resource profile assigns them a
        //meaning; reject at this input-format boundary instead of guessing.
        if (raw < 0) throw new InvalidDataException("story.doctrine-parameters.negative_table_operand");
        var zero = current.ReadStaticTableReference(0x78CF8, 0)
            ?? throw new InvalidDataException("story.doctrine-parameters.default_pointer");
        int? anchor = zero.PhysicalSlot;
        var selected = current.ReadStaticTableReference(0x78CF8, raw < 7 ? raw : 0)
            ?? throw new InvalidDataException("story.doctrine-parameters.table_pointer");
        if (selected.PhysicalSlot is not int physical) return string.Empty;
        //233F10: the default receiver is invalid before checking its name.
        //The wrapper retries it after an invalid selected receiver, and then
        //writes the actual empty string if that retry is also invalid.
        if (physical == anchor) return string.Empty;
        string name = state.Doctrines.GetValueOrDefault(physical)?.Name
            ?? throw new InvalidDataException("story.doctrine-parameters.current_definition");
        if (name.IndexOf('\0') is int terminator && terminator >= 0) name = name[..terminator];
        return name.Length > 62 ? string.Empty : name;
    }
}
