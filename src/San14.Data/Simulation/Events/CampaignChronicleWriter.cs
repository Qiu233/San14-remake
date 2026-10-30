using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record CampaignChronicleCommand(int EventCode, int Family, int CampaignDay,
    string ForceName, int ColorIndex, uint EntryKind, ImmutableArray<string> Labels,
    ImmutableArray<int> HexIds, bool Primary = true, int AdditionalEvent = -1);

/// <summary>Writes the actual campaign chronology and its current map snapshots.
/// This is independent of the report ring and emits no presentation work.</summary>
internal sealed class CampaignChronicleWriter(CampaignHistoryWriter forceNames)
{
    internal GameState Write(GameState state, SourceWorldContext current, CampaignChronicleCommand command,
        bool? userSelectionServicePresent = null)
        => WriteTracked(state, current, command, captureSnapshot: true, userSelectionServicePresent).State;

    internal CampaignChronicleWriteResult WriteTracked(GameState state, SourceWorldContext current,
        CampaignChronicleCommand command, bool captureSnapshot, bool? userSelectionServicePresent)
    {
        //8C8D50 reads BC only when the independent selection service exists.
        //Legacy callers can still close the conjunction with a known -1.
        if (userSelectionServicePresent is true)
        {
            var selection = state.CampaignEventWorld?.CurrentUserIndex
                ?? throw Missing("current_user");
            if (selection != -1) return new(state, null);
        }
        else if (userSelectionServicePresent is null && state.CampaignEventWorld?.CurrentUserIndex != -1)
            throw Missing("task_scene_presence_for_current_user");
        CampaignChronicleEntryReference? written = null;
        //The recursive event row precedes even a main command with kind zero.
        //The same selection gate and the caller's snapshot timing apply to both.
        if (command.AdditionalEvent != -1)
        {
            var additional = new CampaignChronicleCommand(2001, command.Family, command.CampaignDay,
                "", 0, unchecked((uint)command.AdditionalEvent), ["", "", ""], command.HexIds,
                command.Primary);
            var appended = WriteTracked(state, current, additional, captureSnapshot, userSelectionServicePresent);
            state = appended.State;
            written = appended.Entry;
        }
        if (command.EntryKind == 0) return new(state, written);

        var chronology = state.CampaignChronicle ?? throw Missing("saved_chronology");
        var entries = command.Primary ? chronology.PrimaryEntries : chronology.ImportantEntries;
        int selected = Select(entries);
        var record = entries[selected];
        if ((uint)command.EventCode < 65536) record = record with { EventCode = (ushort)command.EventCode };
        record = record with
        {
            Family = (byte)Math.Clamp(command.Family, 0, 3),
            CampaignDay = (ushort)Math.Clamp(command.CampaignDay, 0, 65535),
            MainLabel = Copy(record.MainLabel, FirstUnit(command.ForceName)),
            ColorIndex = (byte)Math.Clamp(command.ColorIndex, 0, 60),
            EntryKind = command.EntryKind
        };
        if (command.Labels.IsDefault || command.Labels.Length != 3) throw Missing("command_labels");
        record = record with
        {
            Label0 = Copy(record.Label0, command.Labels[0]),
            Label1 = Copy(record.Label1, command.Labels[1]),
            Label2 = Copy(record.Label2, command.Labels[2])
        };
        if (command.HexIds.IsDefault || command.HexIds.Length != 4) throw Missing("command_hexes");
        var hexes = record.HexIds.ToBuilder();
        for (int i = 0; i < 4; i++)
            if (command.HexIds[i] != 48400) hexes[i] = (ushort)Math.Clamp(command.HexIds[i], 0, 48399);
        record = record with { HexIds = hexes.ToImmutable() };

        uint maximum = 0;
        Scan(chronology.PrimaryEntries, true);
        Scan(chronology.ImportantEntries, false);
        record = record with { Sequence = maximum == 48400 ? (ushort)48400 : (ushort)Math.Min(maximum + 1, 65535) };

        //A local pending record makes its new subtype visible to the scan
        //while retaining its old sequence. Current snapshot getters do not
        //read the chronology. A late failure never publishes a partial entry.
        if (captureSnapshot) record = Snapshot(state, current, record);
        chronology = command.Primary
            ? chronology with { PrimaryEntries = entries.SetItem(selected, record) }
            : chronology with { ImportantEntries = entries.SetItem(selected, record) };
        return new(state with { CampaignChronicle = chronology }, new(command.Primary, selected));

        void Scan(ImmutableArray<CampaignChronicleRecord> table, bool primary)
        {
            for (int i = 0; i < table.Length; i++)
            {
                var seen = primary == command.Primary && i == selected ? record : table[i];
                if (seen.EntryKind == 0) break;
                maximum = Math.Max(maximum, seen.Sequence);
            }
        }
    }

    //83C100 retains the actual table slot across business execution. Other
    //business writers may reuse that slot; its identity is not a copied record.
    internal GameState RefreshStorySnapshots(GameState state, SourceWorldContext current,
        CampaignChronicleEntryReference entry)
    {
        var chronology = state.CampaignChronicle ?? throw Missing("saved_chronology");
        var entries = entry.Primary ? chronology.PrimaryEntries : chronology.ImportantEntries;
        if ((uint)entry.Slot >= (uint)entries.Length) throw Missing("story_entry_slot");
        if (entries[entry.Slot].EntryKind == 0) return state;
        entries = entries.SetItem(entry.Slot, Snapshot(state, current, entries[entry.Slot]));
        //8CE8B0 returns the previous physical slot, including table wraparound.
        int previous = entry.Slot == 0 ? entries.Length - 1 : entry.Slot - 1;
        if (entries[previous].EntryKind != 0 && entries[previous].EventCode == 2001)
            entries = entries.SetItem(previous, Snapshot(state, current, entries[previous]));
        return state with { CampaignChronicle = entry.Primary
            ? chronology with { PrimaryEntries = entries }
            : chronology with { ImportantEntries = entries } };
    }

    private static int Select(ImmutableArray<CampaignChronicleRecord> records)
    {
        if (records.IsDefaultOrEmpty) throw Missing("writable_entries");
        ushort firstDay = records[0].CampaignDay;
        for (int i = 0; i < records.Length; i++)
            if (records[i].EntryKind == 0 || records[i].CampaignDay < firstDay) return i;
        return 0;
    }

    internal CampaignChronicleRecord Snapshot(GameState state, SourceWorldContext current, CampaignChronicleRecord record)
    {
        var colors = record.AreaColors.ToBuilder();
        foreach (var area in current.ReadRegistryReferences(RegistryKind.Area) ?? throw Missing("Area_registry"))
        {
            if (area is null || area.Kind != RegistryKind.Area) throw Missing("Area_payload");
            if (!K(current.ReadRegistryObjectNativeValidity(area), "Area_validity")) continue;
            var pointer = new AreaReference(area.PhysicalSlot);
            byte rawForce = K(current.ReadAreaCurrentForceSlot(pointer), "Area_current_Hex_force");
            var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw Missing("Area_Force_lookup");
            byte color = K(current.ReadForceReportColor(force), "Area_current_Force_color");
            uint ordinal = unchecked((uint)K(current.ReadAreaPhysicalSlot(pointer), "Area_ordinal"));
            if (ordinal < 501) colors[(int)ordinal] = Math.Min(color, (byte)60);
        }
        var names = record.CityForceShortNames.ToBuilder();
        foreach (var city in current.ReadRegistryReferences(RegistryKind.City) ?? throw Missing("City_registry"))
        {
            if (city is null || city.Kind != RegistryKind.City) throw Missing("City_payload");
            if (!K(current.ReadRegistryObjectNativeValidity(city), "City_validity")) continue;
            int rawForce = K(current.ReadBaseForceSlot(new(BaseKind.City, city.PhysicalSlot)), "City_current_Force");
            var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0) ?? throw Missing("City_Force_lookup");
            string shortName = forceNames.ShortName(state, current, force);
            uint ordinal = unchecked((uint)K(city.PhysicalSlot, "City_ordinal"));
            if (ordinal < 52) names[(int)ordinal] = Copy(names[(int)ordinal], shortName);
        }
        return record with { AreaColors = colors.ToImmutable(), CityForceShortNames = names.ToImmutable() };
    }

    private static string FirstUnit(string text) => text.Length == 0 || text[0] == '\0' ? "" : text[..1];
    private static ImmutableArray<ushort> Copy(ImmutableArray<ushort> window, string text)
    {
        var result = window.ToBuilder();
        for (int i = 0; i < result.Count; i++)
        {
            ushort value = i < text.Length ? text[i] : (ushort)0;
            result[i] = value;
            if (value == 0) break;
        }
        return result.ToImmutable();
    }
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("campaign-chronicle." + field);
}

internal sealed record CampaignChronicleWriteResult(GameState State, CampaignChronicleEntryReference? Entry);
