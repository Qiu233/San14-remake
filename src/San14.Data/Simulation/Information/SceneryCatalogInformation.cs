namespace San14.Data;

public sealed record SceneryCatalogInformation(string Title, string NameCaption,
    string CityCaption, string AreaCaption, string DescriptionCaption,
    CollectionSceneryDetail Detail, string? UnresolvedOwner);
