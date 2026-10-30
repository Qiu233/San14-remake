namespace San14.Data;

internal sealed record DefinitionInformationColumnSource(int ColumnId, int Width, int Flags,
    int CaptionId, int SortMode, bool Visible);
internal sealed record DefinitionInformationGroupSource(int CaptionId, int[] ColumnIds);
internal sealed record DefinitionInformationProfile(int TitleCaptionId,
    IReadOnlyDictionary<int, DefinitionInformationColumnSource> Rows,
    IReadOnlyList<DefinitionInformationGroupSource> Groups, bool HasOfficerAction);
