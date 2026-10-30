using San14.Data;

/// <summary>The caller's stable selection values and current scroll preference.</summary>
internal sealed record InformationDetailSelectionContext(
    IReadOnlyList<InformationDetailSelectionItem> Items, Func<int> ScrollSpeed)
{
    internal bool? InitialNameSortAscending { get; init; }
}

internal sealed record OfficerDetailSelectionReturn(IReadOnlyList<int> OrderedIds, int SelectedId,
    bool? NameSortAscending);

internal sealed record InformationDetailSelectionReturn(IReadOnlyList<int> OrderedIds, int SelectedId,
    bool? NameSortAscending);
