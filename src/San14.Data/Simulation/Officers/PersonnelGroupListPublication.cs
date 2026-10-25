namespace San14.Data;

public sealed record PersonnelGroupPersonCell(int ColumnId, string? Text, int? Number,
    int? SortNumber, string? SortKey, bool NotApplicable, string? UnresolvedOwner)
{
    public string? SortUnresolvedOwner { get; init; }
    public int? SortGroup { get; init; }
}
public sealed record PersonnelGroupPersonListRow(int OfficerId, IReadOnlyList<PersonnelGroupPersonCell> Cells);
public sealed record PersonnelGroupPersonPage(int PageIndex, IReadOnlyList<int> ColumnIds,
    IReadOnlyList<PersonnelGroupPersonListRow> Rows);
public sealed record PersonnelGroupDestinationListRow(int LocationId, IReadOnlyList<PersonnelGroupPersonCell> Cells);
public sealed record PersonnelGroupDestinationPage(IReadOnlyList<int> ColumnIds,
    IReadOnlyList<PersonnelGroupDestinationListRow> Rows);

internal static class PersonnelGroupListProfiles
{
    internal static readonly int[][] Person =
    [
        [0,185,2,3,4,5,35,37,86,88,34],
        [0,5,8,9,10,11,12,18,20],
        [0,1,2,3,4,5,35,6,21,22,23,7],
        [0,40,41,42,43,44,45,46,47,48,49,50],
        [0,51,52,53,54,55], [0,56,57,58,59,60], [0,61,62,63,64,65],
        [0,194,193,196,197,198],
        [0,8,9,10,11,12,35,208,209,36,132,133,134,139],
        [0,37,86,88,34,204]
    ];
    internal static readonly int[] Destination = [0,1,2,3,4,5,7];
}
