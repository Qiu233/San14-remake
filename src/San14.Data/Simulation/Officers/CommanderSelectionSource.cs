using San14.Core;

namespace San14.Data;

public sealed record CommanderSelectionRemovalReceipt(int PersonPhysical, ushort PersonId,
    int ActiveCount, int[] ClearedOrdinals);
public sealed record CommanderSelectionRemovalResult(GameState? State,
    CommanderSelectionRemovalReceipt? Receipt, string? UnresolvedOwner);

/// <summary>Adapts source officer lookup and validity to the remake's ordered
/// commander selection. Original presentation objects are not gameplay owners.</summary>
public static class CommanderSelectionSource
{
    // Only source allocation has this storage limit. Saved domain selections
    // can have larger capacity without changing the removal algorithm.
    private const int SourceCandidateCapacity = 5;
    private const int SourceOfficerLookupCapacity = 6001;

    public static GameState InstallFresh(GameState state)
    {
        if (state.CommanderSelection is not { ActiveCount: 0, Candidates: null } || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 })
            throw new InvalidDataException("Commander selection storage requires an unused fresh selection.");
        return state with { CommanderSelection = new(0,
            Enumerable.Repeat(CommanderSelectionCandidate.Cleared, SourceCandidateCapacity).ToArray()) };
    }

    public static CommanderSelectionRemovalResult RemoveCurrentOfficer(GameState state,
        SourceWorldContext current, int personPhysical)
    {
        try
        {
            if (!Get(current.ReadPersonNativeValidity(personPhysical), "commander-selection.officer-validity"))
                return new(state, null, null);
            ushort id = Get(current.ReadPersonIdentity(personPhysical), "commander-selection.officer-identity");
            CommanderSelectionState? selection = state.CommanderSelection;
            int count = Get(selection?.ActiveCount, "commander-selection.active-count");
            // Empty selections never read candidate storage, including when
            // a migrated save has a count but no candidate knowledge.
            if (count == 0) return new(state, new(personPhysical, id, count, []), null);
            if (selection?.Candidates is not { } candidates)
                throw Missing("commander-selection.candidates");
            var cleared = new List<int>();
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int selected = Get(candidates[ordinal].OfficerId, "commander-selection.candidate-officer");
                // The installed source's identity word cannot encode an
                // extended domain identity; it remains unresolved here.
                if (selected is < 0 or > ushort.MaxValue)
                    throw Missing("commander-selection.source-officer-encoding");
                var pointer = selected < SourceOfficerLookupCapacity
                    ? current.ReadPersonLookupReference(selected) : current.ReadTransferPersonTableReference(0);
                if (pointer is null) throw Missing("commander-selection.candidate-reference");
                if (pointer.PhysicalSlot is not int physical ||
                    !Get(current.ReadPersonNativeValidity(physical), "commander-selection.candidate-validity")) continue;
                if (Get(current.ReadPersonIdentity(physical), "commander-selection.candidate-identity") == id)
                    cleared.Add(ordinal);
            }
            return new(state with { CommanderSelection = selection!.ClearAt(cleared) },
                new(personPhysical, id, count, cleared.ToArray()), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    private static T Get<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}
