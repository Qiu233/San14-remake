using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Installs the campaign-owned definitions into the same table read by
/// normal trait conditions, commander effects and skill consumers.</summary>
public static class PersonalityEditorRuntime
{
    public static LoadedScenario PrepareFresh(LoadedScenario source, PersonalityEditorLibrary library)
    {
        library.Validate();
        return source with { InitialCreatedPersonalities = library.Created };
    }

    /// <summary>Title officer editors need the same selectable definitions as
    /// fresh initialization. Original resource provenance is retained; only
    /// owned row buffers are composed, without changing the repository cache.</summary>
    public static LoadedPersonalities Compose(LoadedPersonalities source, PersonalityEditorLibrary library)
    {
        library.Validate();
        if (library.Created.Length == 0) return source;
        var current = new SourceWorldContext();
        current.InitializePersonalityStorage(); current.MaterializeFixedPersonalities(source);
        Apply(library.Created, current);
        var rows = source.Catalog.Rows.ToArray();
        foreach (var definition in library.Created)
        {
            var bytes = rows[definition.Id].RawBytes.ToArray();
            foreach (var field in S14PersonalityReader.FixedFields)
                for (int index = 0; index < field.Bytes; index++)
                    bytes[field.SourceOffset + index] = current.ReadPersonalityByte(new(definition.Id), field.ObjectOffset + index)
                        ?? throw new InvalidDataException("Composed personality definition field is unavailable.");
            rows[definition.Id] = rows[definition.Id] with { RawBytes = bytes };
        }
        return source with { Catalog = source.Catalog with { Rows = Array.AsReadOnly(rows) } };
    }
    /// <summary>Only the unused fresh world adopts the current title archive.
    /// Loaded campaigns retain their saved definition snapshot.</summary>
    public static GameState InitializeFresh(GameState state, PersonalityEditorLibrary library)
    {
        library.Validate();
        if (state.Phase != TurnPhase.Initializing || state.Units.Count != 0 ||
            state.NativeClock is { } clock && (clock.UpdateOrdinal != 0 || clock.UpdatesIntoPeriod != 0))
            throw new InvalidDataException("Personality adoption requires the unused fresh world.");
        return state with { CreatedPersonalities = library.Created };
    }

    public static void Apply(ImmutableArray<CreatedPersonality>? definitions, SourceWorldContext current)
    {
        // An old save has no title archive snapshot. Do not fill that unknown
        // owner from today's title library or rewrite its resource table.
        if (definitions is null) return;
        foreach (var definition in definitions.Value)
        {
            var source = new CustomPersonalityReference(definition.Id - 256);
            current.SetCustomPersonalityRecord(new(definition.Id - 256, 1, checked((ushort)definition.Id),
                definition.Name, checked((uint)definition.ConditionKind), definition.ConditionValue,
                checked((uint)definition.OptionKind), checked((byte)definition.Effect1Code),
                checked((short)definition.Effect1Value), checked((byte)definition.Effect2Code),
                checked((short)definition.Effect2Value)));
            _ = PersonalityManager.Copy(source, definition.Id, current);
        }
    }
}
