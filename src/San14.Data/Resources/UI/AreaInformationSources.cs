namespace San14.Data;

public sealed record AreaInformationTrait(int Node, int TraitId, string Name, byte Style);
public sealed record AreaInformationNeighbor(int Slot, int AreaId, DetailForceAppearance? Flag, string? UnresolvedOwner);

/// <summary>The ordinary Area default page, evaluated against the supplied current world.</summary>
public sealed record AreaInformationPage(int AreaId, string Name, IReadOnlyDictionary<int,string> Text)
{
    public string LayoutName => AreaInformationSources.LayoutName;
    public IReadOnlySet<int> HiddenNodes { get; init; } = new HashSet<int>();
    public IReadOnlyDictionary<int,int> TextStyles { get; init; } = new Dictionary<int,int>();
    public IReadOnlyDictionary<int,int> TextAlignments { get; init; } = new Dictionary<int,int>();
    public IReadOnlyDictionary<int,MapCornerColors> TextColors { get; init; } = new Dictionary<int,MapCornerColors>();
    public IReadOnlyDictionary<int,int> SpriteIds { get; init; } = new Dictionary<int,int>();
    public IReadOnlyDictionary<int,string> UnresolvedFields { get; init; } = new Dictionary<int,string>();
    public int? PortraitAppearanceId { get; init; }
    public DeploymentNarrationPortraitRoute? StandingPortrait { get; init; }
    public int? ForceId { get; init; }
    public AreaMarkerColor? ForceNameColor { get; init; }
    public DetailForceAppearance? ForceFlag { get; init; }
    public int? DistrictNumber { get; init; }
    public bool? NearbyEnemyPanelVisible { get; init; }
    public int? SelectionIconSpriteId { get; init; }
    public DetailMiniMapValues? MiniMap { get; init; }
    public IReadOnlyList<CityInformationGauge> Gauges { get; init; } = [];
    public IReadOnlyList<AreaInformationTrait> Traits { get; init; } = [];
    public IReadOnlyList<AreaInformationNeighbor> Neighbors { get; init; } = [];
    public string? UnresolvedOwner { get; init; }
}

public static class AreaInformationSources
{
    public const string LayoutName = "11_01_05_00_info_area0_cn";
    public const string NameplateLayoutName = "11_01_03_00_info_city_offset_00";
    public const int TitleId = 0x463;
    public const int BasicTabCaptionId = 0x3CD;
    public const int SelectionCaptionId = 0xAD;
    public const int NearbyEnemyCaptionId = 0x3D6;
    public static IReadOnlyList<int> AuthoredArtNodes { get; } = Array.AsReadOnly(new[]{67,70,73});
    public static IReadOnlyList<(int Node,int Message)> Captions { get; } = Array.AsReadOnly(new (int,int)[]
    {
        (13,0x1FD),(15,0x3D4),(17,0x1FE),(22,0x201),(30,0x2B3),(38,0x2B4),(46,0x2B5),
        (55,0x298),(59,0x253),(64,0x3FD),(77,0x254),(82,0x255),(87,0x256),(91,0x23D),
        (119,0x3D3),(126,0x613),(130,0x252),(102,0xAD),(108,0x893)
    });
}
