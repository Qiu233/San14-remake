using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<ChronologyInformationPage> PrepareChronologyInformationPage(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var saved = state.CampaignChronicle ?? throw ChronologyMissing("saved_records");
            if (saved.PrimaryEntries.IsDefault || saved.ImportantEntries.IsDefault) throw ChronologyMissing("record_tables");
            var records = saved.PrimaryEntries.Select((record, index) => (Record: record, Important: false, Slot: index))
                .Concat(saved.ImportantEntries.Select((record, index) => (Record: record, Important: true, Slot: index)))
                .Where(value => value.Record.EntryKind != 0).ToArray();
            // Native8BCFC0: sequence, day, important flag first, then native allocation ordinal.
            // Equal flags across the two separate storage blocks lack an owned allocation order.
            bool ambiguous = records.GroupBy(value => (value.Record.Sequence, value.Record.CampaignDay, value.Record.EntryFlags & 1))
                .Any(group => group.Select(value => value.Important).Distinct().Count() > 1);
            var ordered = records.OrderBy(value => value.Record.Sequence).ThenBy(value => value.Record.CampaignDay)
                .ThenByDescending(value => value.Record.EntryFlags & 1).ThenBy(value => value.Important).ThenBy(value => value.Slot);
            var rows = ImmutableArray.CreateBuilder<ChronologyInformationRow>();
            var vm = new NarrationResourceMessages(_repository.LoadMessages(Seed.Language, MessageKind.Game).Messages);
            foreach (var value in ordered)
            {
                var record = value.Record;
                var issues = new Dictionary<string, string>(StringComparer.Ordinal);
                string? Read(string field, Func<string> produce)
                {
                    try { return produce(); }
                    catch (InvalidDataException error) { issues[field] = error.Message; return null; }
                }
                string? year = null, month = null, period = null;
                if (state.NativeMapSeason?.SecondDate is { } epoch)
                {
                    int ordinal = unchecked(PersonReturnCommands.CalendarOrdinal(epoch) + record.CampaignDay);
                    short y = unchecked((short)(ordinal / 360));
                    int remainder = Math.Max(0, unchecked(ordinal - y * 360));
                    int m = remainder / 30 + 1, day = remainder % 30 + 1;
                    year = Read("year", () => FacilityListFormat(ReadHistoryDataText(1, 0x569), (int)y));
                    month = Read("month", () => FacilityListFormat(ReadHistoryDataText(1, 0x611), m));
                    period = Read("period", () => ReadHistoryDataText(1, day < 11 ? 0x2AD : day <= 20 ? 0x2AE : 0x2AF));
                }
                else issues["date"] = ChronologyMissing("date_epoch").Message;
                string? narrative = Read("narrative", () =>
                {
                    if (record.EventCode == 2001)
                    {
                        if (ReadApplicationInformationRestriction(state) is not false) throw ChronologyMissing("task_scene_event_message_owner");
                        var source = _campaignEvents.Value.Document;
                        long index = (long)record.EntryKind - source.NativeBaseId;
                        if (index < 0 || index >= source.Events.Count) throw ChronologyMissing("event_template_identity");
                        return ReadHistoryDataText(3, checked(source.Events[(int)index].Id * 4 + 1));
                    }
                    if (record.EntryKind > int.MaxValue) throw ChronologyMissing("message_identity");
                    return vm.ResolveChronologyInformationNarrative((int)record.EntryKind,
                        ChronologyWindow(record.Label0), ChronologyWindow(record.Label1), ChronologyWindow(record.Label2));
                });
                rows.Add(new(value.Important, value.Slot, record.EventCode, record.Family, record.CampaignDay,
                    record.Sequence, record.EntryFlags, ChronologyWindow(record.MainLabel), record.ColorIndex,
                    year, month, period, narrative, narrative is null ? null : ChronologyPlainHeader(narrative),
                    new(record.AreaColors, record.CityForceShortNames, record.HexIds), issues));
            }
            return AreaAdministrationQuery<ChronologyInformationPage>.Known(new(ReadHistoryDataText(1, 0x7F),
                ReadHistoryDataText(1, 0x2E5), ReadHistoryDataText(1, 0x2E6), rows.ToImmutable(),
                ambiguous ? ChronologyMissing("cross_storage_native_allocation_sort").Message : null));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<ChronologyInformationPage>.Unknown(error.Message); }
    }
    private static string ChronologyWindow(ImmutableArray<ushort> units)
    {
        if (units.IsDefault) throw ChronologyMissing("text_window");
        int length = 0; while (length < units.Length && units[length] != 0) length++;
        return new string(units.Take(length).Select(unit => (char)unit).ToArray());
    }
    private static string ChronologyPlainHeader(string text)
    {
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < text.Length;)
        {
            if (text[i] != '^') { result.Append(text[i++]); continue; }
            i = Math.Min(text.Length, i + 3);
            if (i < text.Length && text[i] == '~')
            {
                i++; while (i < text.Length && text[i] != '~') i++;
                if (i < text.Length) i++;
            }
        }
        return result.ToString();
    }
    private static InvalidDataException ChronologyMissing(string field) => new("information.chronology." + field);
}
