namespace San14.Core;

public sealed record MonthlyIncomeReportEntry(int CityId, int ForceId, int Period, MonthlyIncomeQuote Quote);

public sealed partial record GameState
{
    // Null preserves legacy/source uncertainty. A completed playable period may
    // have a known empty set because its provider is explicitly provisional.
    public MonthlyIncomeReportEntry[]? MonthlyIncomeReports { get; init; }
}

public static class EconomyReportStateValidation
{
    public static void Validate(GameState state)
    {
        foreach (AreaState area in state.Areas.Values)
        {
            if (area.EconomySource is not { } source) continue;
            if (source.RawField44 is < 0 or > ushort.MaxValue || source.RawField46 is < 0 or > ushort.MaxValue ||
                source.RawField52 is < 0 or > ushort.MaxValue || !Hash(source.FixedDataSha256) || !Hash(source.ScenarioSha256) ||
                !Hex(source.FixedRemainingBytesHex, 52) || !Hex(source.ScenarioRemainingBytesHex, 8))
                throw new InvalidDataException($"Area {area.Id} economy source is incomplete or invalid.");
            byte[] fixedBytes = Convert.FromHexString(source.FixedRemainingBytesHex);
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(fixedBytes.AsSpan(2, 2)) != source.RawField44 ||
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(fixedBytes.AsSpan(4, 2)) != source.RawField46)
                throw new InvalidDataException($"Area {area.Id} economy source words differ from its retained bytes.");
            if (source.ScenarioSourceBytesHex is { } scenarioHex)
            {
                if (!Hex(scenarioHex, 16)) throw new InvalidDataException($"Area {area.Id} scenario economy source is invalid.");
                byte[] bytes = Convert.FromHexString(scenarioHex);
                if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes) != source.RawField52 ||
                    Convert.ToHexString(bytes.AsSpan(4, 4)) != source.ScenarioRemainingBytesHex.ToUpperInvariant())
                    throw new InvalidDataException($"Area {area.Id} scenario economy fields differ from its retained bytes.");
            }
        }
        if (state.MonthlyIncomeReports is null) return;
        var cities = new HashSet<int>();
        foreach (MonthlyIncomeReportEntry report in state.MonthlyIncomeReports)
        {
            if (report is null || report.Quote is null || !state.Cities.ContainsKey(report.CityId) ||
                !state.Forces.ContainsKey(report.ForceId) || report.Period != state.PeriodNumber || !cities.Add(report.CityId))
                throw new InvalidDataException("Monthly income report has invalid period or world references.");
            PlayableMonthlyIncome.ValidateQuote(report.Quote);
            if (report.Quote.CityId is int quotedCity && quotedCity != report.CityId)
                throw new InvalidDataException("Monthly income quote belongs to another city.");
            if (report.Quote.SettlementMonth is int month && month != state.Date.Month)
                throw new InvalidDataException("Monthly income quote belongs to another settlement month.");
            if (report.Quote.Areas.Any(area => !state.Areas.ContainsKey(area.AreaId)))
                throw new InvalidDataException("Monthly income receipt refers to an unknown area.");
        }
    }
    private static bool Hash(string? value) => Hex(value, 64);
    private static bool Hex(string? value, int length) => value is not null && value.Length == length && value.All(Uri.IsHexDigit);
}
