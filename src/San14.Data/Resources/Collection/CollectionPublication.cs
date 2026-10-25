namespace San14.Data;

/// <summary>The original list's typed sort value. Unknown keys do not prevent
/// displaying a resource row and must not be replaced by locale name sorting.</summary>
public sealed record CollectionSortValue(string? Text = null, int? Number = null,
    string? UnresolvedOwner = null);

public sealed record CollectionColumn(int Id, string Caption, int Width, bool CanSort);
public sealed record CollectionImageRoute(string TexturePack, string SpriteName, int Kind, int Index);

public sealed record CollectionPoemRow(int ItemId, string Name, string Author, string Era,
    CollectionSortValue NameSortKey);

public sealed record CollectionPoemDetail(int ItemId, string Name, string Author, string Era,
    string Description, CollectionImageRoute Image);

public sealed record CollectionSceneryRow(int SceneryId, string Name, string CityName,
    string AreaName, int HexId, CollectionSortValue NameSortKey,
    CollectionSortValue CitySortKey, CollectionSortValue AreaSortKey);

public sealed record CollectionSceneryDetail(int SceneryId, string Name, string CityName,
    string AreaName, int HexId, string Description, CollectionImageRoute Image)
{
    public DetailMiniMapValues? MiniMap { get; init; }
    public string? MiniMapUnresolvedOwner { get; init; }
}
