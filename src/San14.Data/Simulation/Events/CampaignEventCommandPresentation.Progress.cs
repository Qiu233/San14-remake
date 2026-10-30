using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Mutually exclusive original row progress and cooldown text. A null
/// visibility is unresolved, not a fabricated zero-condition or cooldown result.</summary>
public sealed record CampaignEventCommandRowProgress(bool? ShowProgress,
    int BarNumerator, int BarDenominator, string Caption, string CountText,
    NativeMapSeasonDate? CooldownUntil, string? CooldownText, string? UnresolvedOwner);

public static partial class CampaignEventCommandPresentation
{
    internal static CampaignEventCommandRowProgress ReadProgress(S14EventDocument document,
        S14EventRecord record, CampaignEventCommandRow row, GameState state, MessageDocument data)
    {
        try
        {
            long slot = (long)record.Id - document.NativeBaseId;
            var operations = slot >= 0 && slot < document.ConditionStreams.Count
                ? document.ConditionStreams[(int)slot].Operations : Array.Empty<S14EventOperation>();
            foreach (var operation in operations)
            {
                // This UI consumer deliberately scans raw resource order; it
                // does not apply condition VM admission or comparison modes.
                if (operation.Code != 10 || Argument(operation, 0) != record.Id) continue;
                if (row.Occurred is null) return Unknown("world_occurrence");
                if (!row.Occurred.Value) continue;
                var dates = state.CampaignEvents?.OccurrenceDates;
                if (dates is null || (uint)record.Id >= (uint)dates.Length || dates[record.Id] is not { } occurred)
                    return Unknown("occurrence_date");
                if (state.NativeMapSeason?.ThirdDate is not { } now) return Unknown("current_date");
                int threshold = Argument(operation, 2);
                int elapsedDays = unchecked(((now.Year - occurred.Year) * 12 + now.Month - occurred.Month) * 30 + now.Day - occurred.Day);
                if (elapsedDays >= threshold) continue;
                // Preserve the original caller's separate units: its active
                // test compares days directly, but its end date adds 240 hours
                // per operand. Do not substitute the condition VM's /10 test.
                var due = AddCooldownHours(occurred, unchecked(threshold * 240));
                string label = Data(0x2AB), month = Data(0x2AC);
                string period = Data(due.Day < 10 ? 0x2AD : due.Day < 20 ? 0x2AE : 0x2AF);
                string dateText = string.Create(CultureInfo.InvariantCulture, $"{due.Year,3}{label}{due.Month,2}{month}{period}");
                return new(false, 0, 1, "", "", due, dateText, null);
            }
            if (row.Status is null) return Unknown("eligibility_status");
            bool show = (row.Repeatable || (int)row.Status.Value != 3) && (int)row.Status.Value != 2;
            if (!show) return new(false, 0, 1, "", "", null, null, null);
            int total = row.EvaluatedConditions, successful = row.SuccessfulConditions;
            return new(true, total == 0 ? 0 : successful, total == 0 ? 1 : total,
                Data(0x49E), FormatCount(Data(0x325), successful, total), null, null, null);
        }
        catch (InvalidDataException error)
        { return new(null, 0, 1, "", "", null, null, error.Message); }

        CampaignEventCommandRowProgress Unknown(string field) => new(null, 0, 1, "", "", null, null,
            "campaign_event.presentation.progress." + field);
        string Data(int index)
        {
            if (data.Sections.Count <= 1 || (uint)index >= (uint)data.Sections[1].Records.Count ||
                data.Sections[1].Records[index].Text is not string value)
                throw new InvalidDataException("campaign_event.presentation.progress.data1." + index.ToString("X", CultureInfo.InvariantCulture));
            return value;
        }
    }

    private static int Argument(S14EventOperation operation, int index)
        => BinaryPrimitives.ReadInt32LittleEndian(operation.RawBytes.AsSpan(0xC + index * 4, 4));

    private static NativeMapSeasonDate AddCooldownHours(CampaignEventOccurrenceDate date, int hours)
    {
        // Same signed-hour calendar normalization as the existing native date
        // consumer; all arithmetic is local, without advancing the world.
        int value = unchecked(hours + (date.Day - 31 + (date.Month + date.Year * 12) * 30) * 24);
        short year = unchecked((short)((short)(value / 8640) - (short)(value / -2030043136)));
        value = Math.Max(0, unchecked(value - year * 8640));
        sbyte month = unchecked((sbyte)((sbyte)(value / 720) - (sbyte)(value / -1275068416)));
        value = Math.Max(0, unchecked(value - month * 720));
        sbyte day = unchecked((sbyte)(value / 24));
        value = Math.Max(0, unchecked(value - day * 24));
        return new(year, unchecked((sbyte)(month + 1)), unchecked((sbyte)(day + 1)), unchecked((sbyte)value));
    }

    private static string FormatCount(string template, int successful, int total)
    {
        var result = new StringBuilder();
        int argument = 0;
        for (int i = 0; i < template.Length; i++)
        {
            if (template[i] != '%') { result.Append(template[i]); continue; }
            if (++i < template.Length && template[i] == '%') { result.Append('%'); continue; }
            int width = 0;
            while (i < template.Length && template[i] is >= '0' and <= '9')
                width = checked(width * 10 + template[i++] - '0');
            if (i >= template.Length || template[i] != 'd' || argument >= 2)
                throw new InvalidDataException("campaign_event.presentation.progress.count_format");
            string value = (argument++ == 0 ? successful : total).ToString(CultureInfo.InvariantCulture);
            result.Append(value.PadLeft(width));
        }
        if (argument != 2) throw new InvalidDataException("campaign_event.presentation.progress.count_arguments");
        return result.ToString();
    }
}
