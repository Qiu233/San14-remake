using San14.Core;

namespace San14.Data;

/// <summary>This independent client owns no original CGameState/TaskScene
/// candidate view. The absence affects only the original898C30 presentation
/// call; it never bypasses the current World candidate scalar writes.</summary>
public sealed class OriginalPersonCaptureCandidatesPresentationAbsent
{
    public static OriginalPersonCaptureCandidatesPresentationAbsent IndependentClient { get; } = new();
    private OriginalPersonCaptureCandidatesPresentationAbsent() { }
}
public sealed record OriginalPersonCaptureWorldCandidatesReceipt(int PersonPhysical, ushort PersonId,
    byte Count165DBeforeAndAfter, int[] ClearedOrdinals, int SkippedPresentationRefreshCount);
public sealed record OriginalPersonCaptureWorldCandidatesResult(GameState? State,
    OriginalPersonCaptureWorldCandidatesReceipt? Receipt, string? UnresolvedOwner);

/// <summary>326230's actual10/20-byte reset windows and29CCB0's current
/// selected-Person removal. The same saved owner is reusable after capture,
/// save/load, and later actual candidate selection writes.</summary>
public static class OriginalPersonCaptureWorldCandidates
{
    public const string Revision = "san14:ce-owned-person-capture-world-candidates-20261007";
    public static GameState InstallFresh(GameState state)
    {
        if (state.NativePersonCaptureWorldCandidates is not null || state.Units.Count != 0 ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeArmySimulation?.World.SelectedCommanderCount165D is not 0)
            throw new InvalidDataException("World candidate reset may only be installed once on its unused fresh owner.");
        var result = state with { NativePersonCaptureWorldCandidates = new(Revision, new ushort?[5], new int?[5]) };
        Array.Fill(result.NativePersonCaptureWorldCandidates.PersonIds165E, (ushort?)0);
        Array.Fill(result.NativePersonCaptureWorldCandidates.Values1668, (int?)0);
        NativePersonCaptureWorldCandidatesStateValidation.Validate(result); return result;
    }

    public static OriginalPersonCaptureWorldCandidatesResult ApplyCurrent(GameState state,
        OriginalEventInitializationContext current, int personPhysical,
        OriginalPersonCaptureCandidatesPresentationAbsent presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        try
        {
            if (!Get(current.ReadPersonNativeValidity(personPhysical), "current.29CCB0_Person_validity"))
                return new(state, null, null);
            ushort id = Get(current.ReadPersonIdentity(personPhysical), "current.29CCB0_Person10");
            byte count = Get(state.NativeArmySimulation?.World.SelectedCommanderCount165D, "current.World165D");
            if (count > 5) throw Missing("current.World165D_exceeds_actual_five_slots");
            //The actual count0 branch never consumes the candidate arrays.
            if (count == 0) return new(state, new(personPhysical, id, count, [], 0), null);
            var owner = state.NativePersonCaptureWorldCandidates;
            if (owner is not { Revision: Revision, PersonIds165E.Length: 5, Values1668.Length: 5 })
                throw Missing("current.World165E_1668_candidates_owner");
            var people = owner.PersonIds165E.ToArray(); var values = owner.Values1668.ToArray(); var cleared = new List<int>();
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                ushort selected = Get(people[ordinal], "current.World165E_selected_Person_ID");
                var pointer = selected < 6001 ? current.ReadPersonLookupReference(selected) : current.ReadTransferPersonTableReference(0);
                if (pointer is null) throw Missing("current.32ECD0_selected_Person_ID_pointer");
                if (pointer.PhysicalSlot is not int physical || !Get(current.ReadPersonNativeValidity(physical), "current.selected_Person_validity")) continue;
                if (Get(current.ReadPersonIdentity(physical), "current.selected_Person10") != id) continue;
                //No compaction and no count decrement. Source writes these
                //two scalars even if previous values were unknown.
                people[ordinal] = 0; values[ordinal] = 0; cleared.Add(ordinal);
            }
            var result = state with { NativePersonCaptureWorldCandidates = owner with
                { PersonIds165E = people, Values1668 = values } };
            return new(result, new(personPhysical, id, count, cleared.ToArray(), cleared.Count), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
    private static T Get<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}
