using System.Collections.Immutable;
using System.Globalization;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed record IncomeReportDetailsColumn(int ColumnId, int Width,
    int CaptionMessageId, string Caption)
{
    public bool CanSort { get; init; }
    public bool? InitialSortAscending { get; init; }
    public string? SortUnresolvedOwner { get; init; }
}

public sealed record IncomeReportDetailsCell(int ColumnId, string? Text,
    int? Number, string? UnresolvedOwner);

public sealed record IncomeReportCityDetailsRow(int CityIndex, string Name,
    ImmutableArray<IncomeReportDetailsCell> Cells, bool ResourceShortage,
    bool? CanInspect, string? UnresolvedOwner)
{
    internal ImmutableDictionary<int, IncomeReportDetailsSortKey> SortKeys { get; init; } =
        ImmutableDictionary<int, IncomeReportDetailsSortKey>.Empty;
    internal ImmutableDictionary<int, string> SortIssues { get; init; } = ImmutableDictionary<int, string>.Empty;
}

public sealed record IncomeReportCityDetails(string Title, int TitleMessageId,
    string TabCaption, int TabCaptionMessageId,
    ImmutableArray<IncomeReportDetailsColumn> Columns,
    ImmutableArray<IncomeReportCityDetailsRow> Rows, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    // The report-specific kind5 preset has one tab. These are the actual
    // column descriptors, rather than the ordinary City information preset.
    private static readonly (int Id, int Width, int Caption)[] IncomeDetailsColumns =
    [
        (0, 76, 0x207), (5, 104, 0x20C), (35, 104, 0x254),
        (36, 104, 0x3F2), (6, 118, 0x20D), (38, 132, 0x255),
        (39, 132, 0x3F4), (7, 104, 0x20E), (41, 132, 0x256),
    ];

    /// <summary>
    /// The income-detail City list from 673100. Reads the retained report current;
    /// it neither predicts income nor captures a replacement world.
    /// </summary>
    public IncomeReportCityDetails ReadIncomeReportDetails(CampaignIncomeReportPublication report,
        DistrictReference district)
    {
        ArgumentNullException.ThrowIfNull(report);
        var current = report.Current;
        var state = report.World;
        int districtId = IncomeNeed(district.PhysicalSlot, "details_District_pointer");
        var data = _repository.LoadMessages(Seed.Language, MessageKind.Data).Messages.Sections[1].Records;
        string Message(int id)
        {
            if ((uint)id >= (uint)data.Count || data[id].Text is not string text)
                throw IncomeMissing("details_Data1_" + id.ToString("X", CultureInfo.InvariantCulture));
            return text;
        }

        var columns = IncomeDetailsColumns.Select(column =>
            new IncomeReportDetailsColumn(column.Id, column.Width, column.Caption, Message(column.Caption))
            {
                // The report uses the same actual City descriptors: name
                // flags1 starts forward; these eight numeric flags20000002
                // start reversed. No initial column sort is requested here.
                CanSort = true,
                InitialSortAscending = column.Id == 0,
            }).ToImmutableArray();
        string absent = Message(0x353);
        var shortages = new List<IncomeReportCityDetailsRow>();
        var ordinary = new List<IncomeReportCityDetailsRow>();

        //224300 is the fixed City table iterator. Its District membership uses
        //the actual receiver, not dictionary order or all Force-owned cities.
        for (int slot = 0; slot < 52; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw IncomeMissing("details_City_table");
            if (districtId is < 1 or > 51 ||
                IncomeNeed(current.ReadCityDistrictReference(city)?.PhysicalSlot,
                    "details_City_District") != districtId) continue;
            int cityId = IncomeNeed(city.PhysicalSlot, "details_City_pointer");
            var owner = state.Cities.GetValueOrDefault(cityId)
                ?? throw IncomeMissing("details_City_owner");
            var earned = owner.TurnIncomeTotals ?? throw IncomeMissing("details_City_D0_D4_D8");
            int goldExpense = IncomeNeed(current.ReadBaseReturnField50(city), "details_Base50");
            int foodExpense = IncomeNeed(owner.PeriodFoodExpenditure, "details_Base54");
            bool shortage = earned.Gold < goldExpense || earned.Food < foodExpense;
            var cells = ImmutableArray.CreateBuilder<IncomeReportDetailsCell>(IncomeDetailsColumns.Length);
            var issues = new List<string>();
            var sortKeys = ImmutableDictionary.CreateBuilder<int, IncomeReportDetailsSortKey>();
            var sortIssues = ImmutableDictionary.CreateBuilder<int, string>();

            foreach (var column in IncomeDetailsColumns)
            {
                try
                {
                    if (column.Id == 0)
                    {
                        //35E9F0 returns the current City's literal name at12.
                        cells.Add(new(0, owner.Name, null, null));
                        try
                        {
                            //34C5A0 reads the fixed secondary text, not the
                            //mutable visible City name and not host culture.
                            sortKeys.Add(0, IncomeReportDetailsSortKey.Name(ReadCityDetailNameSortKeyCurrent(current, cityId)));
                        }
                        catch (InvalidDataException error) { sortIssues.Add(0, error.Message); }
                        continue;
                    }
                    if (column.Id is 5 or 6)
                    {
                        int force = IncomeNeed(current.ReadBaseForceSlot(city), "details_City_Force");
                        //232C60 suppresses only these stock cells for Force46..50.
                        if (force is >= 46 and <= 50)
                        {
                            cells.Add(new(column.Id, absent, null, null));
                            //The independent key getter returns native tag2;
                            //this is a known inapplicable value, not missing.
                            sortKeys.Add(column.Id, IncomeReportDetailsSortKey.Inapplicable());
                            continue;
                        }
                    }
                    int number = column.Id switch
                    {
                        5 => IncomeNeed(current.ReadBaseResource(city, BaseResourceKind.Gold), "details_Base34"),
                        35 => earned.Gold,
                        36 => goldExpense,
                        6 => IncomeNeed(current.ReadBaseResource(city, BaseResourceKind.Supplies), "details_Base38"),
                        38 => earned.Food,
                        39 => foodExpense,
                        7 => IncomeNeed(current.ReadBaseResource(city, BaseResourceKind.Troops), "details_Base3C"),
                        41 => earned.RecruitedTroops,
                        _ => throw IncomeMissing("details_column"),
                    };
                    //Every numeric getter uses the literal signed "%d" format.
                    cells.Add(new(column.Id, number.ToString(CultureInfo.InvariantCulture), number, null));
                    sortKeys.Add(column.Id, IncomeReportDetailsSortKey.Numeric(number));
                }
                catch (InvalidDataException error)
                {
                    issues.Add(error.Message);
                    sortIssues.Add(column.Id, error.Message);
                    cells.Add(new(column.Id, null, null, error.Message));
                }
            }

            bool? canInspect = current.ReadBaseNativeValidity(city);
            if (!canInspect.HasValue) issues.Add("income-report.details.City_inspect_validity");
            //22DC80 is the City table physical index. It is also the domain
            //CityId consumed by the actual City-detail command, not Base4E.
            var row = new IncomeReportCityDetailsRow(cityId, owner.Name, cells.ToImmutable(),
                shortage, canInspect, issues.Count == 0 ? null : string.Join("; ", issues.Distinct()))
            { SortKeys = sortKeys.ToImmutable(), SortIssues = sortIssues.ToImmutable() };
            (shortage ? shortages : ordinary).Add(row);
        }

        //673740 sorts each partition independently by22DC80 descending. The
        //strict comparison leaves equal identities and repeated entries stable.
        var rows = shortages.OrderByDescending(row => row.CityIndex)
            .Concat(ordinary.OrderByDescending(row => row.CityIndex)).ToImmutableArray();
        columns = columns.Select(column =>
        {
            string? missing = rows.Select(row => row.SortIssues.GetValueOrDefault(column.ColumnId))
                .FirstOrDefault(issue => issue is not null);
            return column with { CanSort = missing is null, SortUnresolvedOwner = missing };
        }).ToImmutableArray();
        return new(Message(0x5F4), 0x5F4, Message(0x3DA), 0x3DA, columns, rows, null);
    }
}
