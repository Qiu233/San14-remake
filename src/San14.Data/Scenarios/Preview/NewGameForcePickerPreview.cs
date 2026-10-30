namespace San14.Data;

/// <summary>The complete source roster and authored seven-group Force selector.
/// Rows retains the caller's original occurrences; NativeSortedRows is its
/// stable selected-first order. Pages preserves SourceIndex through that order
/// and shares the ordinary Cell/Sort algorithms over one clockless capture.
/// Missing runtime fields remain explicit per-cell display/sort unknowns.
/// ForceId is the real picker receiver, independent of avatar RulerForceId.</summary>
public sealed record NewGameForcePickerPreview(
    IReadOnlyList<NewGameForcePreview> Rows,
    IReadOnlyList<NewGameForcePreview>? NativeSortedRows,
    int? InitialSelectedForceId,
    string? SortUnresolvedInput,
    IReadOnlyList<string> UnresolvedInputs,
    ForceInformationPageSet Pages);
