using San14.Core;

namespace San14.Data;

/// <summary>335E90's fresh wandering-city pass, before its bloodline pass and
/// before credit initialization. Borrows current Person
/// identities and the campaign's actual shared application random stream.</summary>
internal static class NewGameWanderingCityInitialization
{
    internal static GameState Apply(GameState state, SourceWorldContext current)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(current);
        int selector = Need(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
        bool enabled = selector == 0
            ? Need(BattleRemakeCurrentSettings.ReadInt32(state, 0x1F4), "advanced_setting") == 1
            : Need(BattleRemakeCurrentSettings.ReadByte(state, 0x79), "ordinary_setting") != 0;
        if (!enabled) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.CampaignAppRandom32 is not { } randomOwner)
            throw Missing("fresh_application_random_owner");
        var random = new SharedSourceRandom32(randomOwner.State);
        Dictionary<int, OfficerState>? officers = null;
        var residues = CampaignSourceKnowledge.From(state.ResourceState);
        for (int slot = 0; slot < 1851; slot++)
        {
            var reference = current.ReadTransferPersonTableReference(slot) ?? throw Missing("person_table:" + slot);
            int physical = Need(reference.PhysicalSlot, "person_pointer:" + slot);
            ushort identity = Need(current.ReadPersonIdentity(physical), "person_identity:" + slot);
            // The source does not add a native-validity filter. It includes
            // actual historical/free/unborn statuses5/7/8 in physical order.
            if (identity is < 1 or > 3000) continue;
            byte status = Need(current.ReadPersonStatus(physical), "person_status:" + slot);
            if (status is not (5 or 7 or 8)) continue;
            if (current.ResolveOfficerSource(identity) != physical || !state.Officers.TryGetValue(identity, out var person) ||
                person.LocationState is not { } location)
                throw Missing("current_person_owner:" + identity);
            //3FCF40(0x2E)+1 consumes the actual App stream once per reached
            //person. The source's subsequent0..61/1061 clamps are inactive
            //within this proved1..46 draw range; no uniform61-city substitute.
            ushort city = checked((ushort)(random.Next(0x2E) + 1));
            current.WritePersonFirstLocation(physical, city);
            current.WritePersonSecondLocation(physical, city);
            officers ??= new(state.Officers);
            officers[identity] = person with
            {
                LocationId = city,
                LocationState = location with { Position = new(OfficerPositionKind.City, city) }
            };
            if (residues.Find(identity) is { LocationCode: not null } residue)
                residues = residues.With(new(identity, null, residue.DistrictCode));
        }
        return officers is null ? state : state with
        {
            Officers = officers, ResourceState = residues, CampaignAppRandom32 = new(random.State)
        };
    }

    private static T Need<T>(T? value, string input) where T : struct => value ?? throw Missing(input);
    private static InvalidDataException Missing(string input) => new("new-game.wandering-cities." + input);
}
