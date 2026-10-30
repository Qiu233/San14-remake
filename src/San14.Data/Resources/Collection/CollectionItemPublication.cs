namespace San14.Data;

/// <summary>A cell in the title's immutable item directory. Sorting is supplied
/// separately from the formatted value; a missing source key is not a name sort.</summary>
public sealed record CollectionItemCell(int ColumnId, string? Text, CollectionSortValue SortValue)
{
    public string? UnresolvedOwner { get; init; }
}

public sealed record CollectionItemRow(int ItemId,
    IReadOnlyDictionary<int, CollectionItemCell> Cells);

public sealed record CollectionItemTab(int Id, string Caption,
    IReadOnlyList<CollectionColumn> Columns);

/// <summary>The title's DLC gate is independent of a campaign or discovery
/// profile. Both facts must come from the application content owner.</summary>
public sealed record CollectionItemContentAvailability(bool Owned, bool Available);

public sealed record CollectionItemUnresolvedCandidate(int ItemId, string UnresolvedOwner);

public sealed record CollectionItemDetail(int ItemId, string Name, string TypeName,
    int Value, bool? Transferable, string Ability, string Effect, string Description,
    CollectionImageRoute? Image)
{
    public bool CanAppreciatePoem { get; init; }
    /// <summary>Actual caption and value controls in the dedicated item body.
    /// These indices stay at this UI publication boundary, outside the model.</summary>
    public IReadOnlyDictionary<int, string> DetailFields { get; init; } =
        new Dictionary<int, string>();
    public IReadOnlyList<string> UnresolvedOwners { get; init; } = [];
}
