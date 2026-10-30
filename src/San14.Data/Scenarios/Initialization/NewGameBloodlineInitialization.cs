using San14.Core;

namespace San14.Data;

/// <summary>The ordinary fresh nonhistorical bloodline transformation. The
/// caller runs this after wandering-city initialization and relation allocation.
/// It borrows the same current Person table and never installs a saved owner.</summary>
internal static class NewGameBloodlineInitialization
{
    internal static GameState Apply(GameState state, SourceWorldContext current)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
        int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
        int mode = Need(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x2FC : 0xDC), "bloodline_setting");
        if (mode != 1) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation ||
            simulation.Parameters.Int32Values.GetValueOrDefault(OwnedPersonRelationInputs.VersionKey) != 1)
            throw Missing("fresh_relation_owner");

        // 335E90 first collects the physical pointer table, not the live
        // registration chain or a logical-ID sort. The preceding city rule
        // can change E2, so candidates must read this operation's current home.
        var candidates = new List<(int Physical, ushort Identity)>();
        for (int slot = 0; slot < 1851; slot++)
        {
            var reference = current.ReadTransferPersonTableReference(slot) ?? throw Missing("person_table:" + slot);
            int physical = Need(reference.PhysicalSlot, "person_pointer:" + slot);
            ushort identity = Need(current.ReadPersonIdentity(physical), "person_identity:" + slot);
            if (identity is < 1 or > 1000) continue;
            ushort? home = current.ReadPersonFirstLocation(physical);
            if (home is not (>= 1 and <= 61))
            {
                byte? status = current.ReadPersonStatus(physical);
                if (status is not (7 or 8))
                {
                    if (!home.HasValue || !status.HasValue) throw Missing("person_home_or_status:" + slot);
                    continue;
                }
            }
            candidates.Add((physical, identity));
        }

        Dictionary<string, int>? values = null;
        Dictionary<int, OfficerState>? officers = null;
        foreach (var candidate in candidates)
        {
            // 2E90B0 independently rejects invalid receivers after collecting
            // the list. Spouse, sworn-brother, affinity and like/dislike fields
            // are not bloodline fields and are not written by this source.
            if (!Need(current.ReadPersonNativeValidity(candidate.Physical), "person_validity:" + candidate.Physical)) continue;
            if (current.ResolveOfficerSource(candidate.Identity) != candidate.Physical ||
                !state.Officers.TryGetValue(candidate.Identity, out var person))
                throw Missing("current_person_owner:" + candidate.Identity);
            values ??= new(simulation.Parameters.Int32Values);
            officers ??= new(state.Officers);
            values[OwnedPersonRelationInputs.PersonFieldKey(candidate.Physical, 0xB4)] = 0;
            values[OwnedPersonRelationInputs.PersonFieldKey(candidate.Physical, 0xB6)] = 0;
            values[OwnedPersonRelationInputs.PersonFieldKey(candidate.Physical, 0xB2)] = candidate.Identity;
            officers[candidate.Identity] = person with { FamilyGeneration = 1 };
        }
        if (values is null) return state;
        return state with
        {
            Officers = officers!,
            NativeArmySimulation = simulation with { Parameters = simulation.Parameters with { Int32Values = values } }
        };
    }

    private static T Need<T>(T? value, string input) where T : struct => value ?? throw Missing(input);
    private static InvalidDataException Missing(string input) => new("new-game.bloodline." + input);
}
