namespace San14.Data;

/// <summary>One actual caller-provided detail-selection slot. Repeated
/// identities remain separate slots; an empty caption is known source text.</summary>
public sealed record InformationDetailSelectionItem(int Id,string? Caption,string? UnresolvedOwner)
{
    public int? IconSpriteId { get; init; }
    public string? SortText { get; init; }
    public int? SortForceId { get; init; }
    public int? SortDistrictNumber { get; init; }
    public bool? SortForceIsPlayer { get; init; }
    public string? SortUnresolvedOwner { get; init; }
}
