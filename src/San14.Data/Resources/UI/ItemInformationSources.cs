namespace San14.Data;

/// <summary>The ordinary current-world Item body. This is not the title's
/// Collection item page, which has neither a current holder nor a Force.</summary>
public sealed record ItemInformationPage(int ItemId, string Name, IReadOnlyDictionary<int, string> Text)
{
    public string LayoutName => ItemInformationSources.LayoutName;
    public IReadOnlyDictionary<int, string> UnresolvedFields { get; init; } = new Dictionary<int, string>();
    public int? HolderOfficerId { get; init; }
    public bool? HolderValid { get; init; }
    public int? HolderPortraitAppearanceId { get; init; }
    public OfficerPortraitRoute? HolderPortrait { get; init; }
    public int? ForceId { get; init; }
    public int? ForceColorIndex { get; init; }
    public bool? ForceFlagValid { get; init; }
    public DetailForceAppearance? ForceFlag { get; init; }
    /// <summary>The saved effective image index, including the imported-image
    /// extension. Zero is a known empty image; null is missing current data.</summary>
    public int? ImageIndex { get; init; }
    public bool? CanAppreciatePoem { get; init; }
}

public static class ItemInformationSources
{
    public const string LayoutName = "11_01_08_00_item_cn";
    public const int TitleId = 0x467;
    public const int BasicTabCaptionId = 0x3CD;
    public const int SelectionCaptionId = 0x246;
    public const int SelectionWidth = 188;
    public const int PoemCaptionId = 0xAE;
    public const int InvalidHolderAppearanceId = 0x896;
    public static IReadOnlyList<(int Node, int Message)> Captions { get; } =
        Array.AsReadOnly(new (int, int)[] { (16,0x424), (21,0x401), (26,0x423),
            (31,0x426), (36,0x452), (41,0x411), (46,0x400), (52,0x425) });
}
