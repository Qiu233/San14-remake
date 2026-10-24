namespace San14.Data;

/// <summary>The complete source roster passed by the ordinary new-game force
/// page to its single-selection picker. Rows preserves that roster's order;
/// NativeSortedRows is unavailable until the generic list's initial column-sort
/// inputs are known. A missing sort/display field does not remove a source row.
/// Row ForceId is the actual Force receiver accepted by the formal SelectForce
/// callback; RulerForceId belongs to the separate avatar callback.</summary>
public sealed record NewGameForcePickerPreview(
    IReadOnlyList<NewGameForcePreview> Rows,
    IReadOnlyList<NewGameForcePreview>? NativeSortedRows,
    int? InitialSelectedForceId,
    string? SortUnresolvedInput,
    IReadOnlyList<string> UnresolvedInputs);
