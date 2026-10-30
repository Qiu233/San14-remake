using System.Collections.Immutable;

namespace San14.Data;

// Accepted report values only. A header comparison never queries the World,
// replays income, captures another current or formats a replacement report.
internal sealed record IncomeReportDetailsSortKey(string? Text, int Number, bool NotApplicable)
{
    internal static IncomeReportDetailsSortKey Name(string value) => new(value, 0, false);
    internal static IncomeReportDetailsSortKey Numeric(int value) => new(null, value, false);
    internal static IncomeReportDetailsSortKey Inapplicable() => new(null, 0, true);

    internal int CompareTo(IncomeReportDetailsSortKey other)
    {
        //The common key comparator orders native tag2 after numeric tag0;
        //reversing the actual operands also reverses that known empty group.
        if (NotApplicable != other.NotApplicable) return NotApplicable ? 1 : -1;
        if (NotApplicable) return 0;
        if (Text is not null && other.Text is not null) return PersonNameOrder.Compare(Text, other.Text);
        if (Text is not null || other.Text is not null)
            throw new InvalidDataException("income-report.details.sort_key_kind");
        return Number.CompareTo(other.Number);
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Sort the actual retained report rows. Equal keys preserve the
    /// caller's current row order, including the original shortage partition
    /// on first use. Later column clicks operate on the latest returned order.</summary>
    public AreaAdministrationQuery<IncomeReportCityDetails> SortIncomeReportDetails(
        IncomeReportCityDetails publication, int columnId, bool ascending)
    {
        ArgumentNullException.ThrowIfNull(publication);
        var column = publication.Columns.FirstOrDefault(value => value.ColumnId == columnId);
        if (column is not { CanSort: true })
            return AreaAdministrationQuery<IncomeReportCityDetails>.Unknown(column?.SortUnresolvedOwner
                ?? "income-report.details.sort_column");
        try
        {
            foreach (var row in publication.Rows)
                if (!row.SortKeys.ContainsKey(columnId))
                    throw new InvalidDataException(row.SortIssues.GetValueOrDefault(columnId)
                        ?? "income-report.details.sort_key");
            var comparer = Comparer<IncomeReportCityDetailsRow>.Create((left, right) => ascending
                ? left.SortKeys[columnId].CompareTo(right.SortKeys[columnId])
                : right.SortKeys[columnId].CompareTo(left.SortKeys[columnId]));
            var rows = publication.Rows.OrderBy(row => row, comparer).ToImmutableArray();
            return AreaAdministrationQuery<IncomeReportCityDetails>.Known(publication with { Rows = rows });
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IncomeReportCityDetails>.Unknown(error.Message); }
    }
}
