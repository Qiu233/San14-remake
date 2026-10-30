namespace San14.Data;

/// <summary>One retained ordinary Scenery detail, expanded from its source-time
/// current. Text keys are the authored value-node identities.</summary>
public sealed record SceneryInformationPage(int SceneryId, string Name,
    IReadOnlyDictionary<int,string> Text)
{
    public int? ImageIndex { get; init; }
    public int? EffectKind { get; init; }
    public int? ForceColorIndex { get; init; }
    public AreaMarkerColor? ForceNameColor { get; init; }
    public int? ProgressCurrent { get; init; }
    public int? ProgressMaximum { get; init; }
    public bool? UseAlternateStatePanel { get; init; }
    public DetailMiniMapValues? MiniMap { get; init; }
    public string? UnresolvedOwner { get; init; }
}

public static class SceneryInformationSources
{
    public const string LayoutName="11_01_13_00_info_scenic_cn";
    public const int TitleId=0x85F;
    public static IReadOnlyList<(int Node,int Message)> Captions { get; } =
        Array.AsReadOnly(new (int,int)[] { (15,0x1FD),(19,0x201),(22,0x202),
            (29,0x842),(39,0x411),(44,0x44F),(49,0x44F),(55,0x400) });
}
