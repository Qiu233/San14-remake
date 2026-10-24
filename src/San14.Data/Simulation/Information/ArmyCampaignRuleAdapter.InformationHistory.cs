using System.Collections.Immutable;
using System.Globalization;
using San14.Core;

namespace San14.Data;

/// <summary>Source-time report fields in the original oldest-to-newest list
/// order. Text retains the expanded colour controls for the existing UI formatter.</summary>
public sealed record InformationHistoryRow(int Slot, string? YearText, string? MonthText,
    string? PeriodText, bool ShowDate, string Text, byte ColorIndex, string ForceShortName);

public sealed record InformationHistoryPage(string Title, ImmutableArray<InformationHistoryRow> Rows,
    string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    internal InformationHistoryPage ReadInformationHistoryPage(GameState state)
    {
        string title = ReadHistoryDataText(1, 0x5BB);
        if (state.CampaignHistory is not { } history)
            return new(title, [], "information.history.saved_ring");
        if (history.Records.IsDefault || history.Records.Length != CampaignHistoryState.Capacity ||
            history.Cursor >= CampaignHistoryState.Capacity)
            return new(title, [], "information.history.ring_shape");

        var rows = ImmutableArray.CreateBuilder<InformationHistoryRow>();
        string? issue = null;
        int previousPeriod = -1;
        int first = (history.Cursor + 1) % CampaignHistoryState.Capacity;
        for (int ordinal = 0; ordinal < CampaignHistoryState.Capacity; ordinal++)
        {
            int slot = (first + ordinal) % CampaignHistoryState.Capacity;
            var record = history.Records[slot];
            string text = HistoryWindowText(record.Text);
            if (text.Length == 0) continue;
            string? yearText = null, monthText = null, periodText = null;
            bool showDate = false;
            if (state.NativeMapSeason?.SecondDate is { } epoch)
            {
                //32C0F0(World2E) + record.day; the source uses 360-day years
                //and30-day months. No current date or scenario fallback enters.
                int day = unchecked(PersonReturnCommands.CalendarOrdinal(epoch) + record.CampaignDay);
                short year = unchecked((short)(day / 360));
                int remainder = Math.Max(0, unchecked(day - year * 360));
                int month = remainder / 30 + 1;
                int date = remainder % 30 + 1;
                int period = (date - 1) / 10;
                //8D9FF0 compares only the period ordinal of adjacent rows,
                //rather than adding a different year/month grouping rule.
                showDate = period != previousPeriod;
                previousPeriod = period;
                yearText = year.ToString(CultureInfo.InvariantCulture) + ReadHistoryDataText(1, 0x2AB);
                monthText = month.ToString(CultureInfo.InvariantCulture) + ReadHistoryDataText(1, 0x2AC);
                periodText = ReadHistoryDataText(1, 0x2AD + period);
            }
            else issue = "information.history.date_epoch";
            rows.Add(new(slot, yearText, monthText, periodText, showDate, text,
                record.ColorIndex, HistoryWindowText(record.ForceShortName)));
        }
        return new(title, rows.ToImmutable(), issue);
    }

    private static string HistoryWindowText(ImmutableArray<ushort> window)
    {
        if (window.IsDefault) throw new InvalidDataException("information.history.text_window");
        int count = 0;
        while (count < window.Length && window[count] != 0) count++;
        var chars = new char[count];
        for (int index = 0; index < count; index++) chars[index] = (char)window[index];
        //These are UTF16 code units, not byte-encoded resource text. Preserve
        //surrogates and source ^02/^05/^00 controls; rendering consumes them.
        return new(chars);
    }
}
