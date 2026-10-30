namespace San14.Data;

public sealed record CityInformationGauge(int Node, int Current, int Maximum, int Style);
public sealed record CityInformationDutyPortrait(int Node, int? OfficerId, OfficerPortraitRoute? Portrait,
    string? UnresolvedOwner);
public sealed record CityInformationTactic(int Node, int? TacticId, string Name, int IconSpriteId);

/// <summary>One ordinary City default page. Text keys are actual Type2 nodes;
/// composite controls and their source identities have separate typed fields.</summary>
public sealed record CityInformationPage(int CityId, string Name, IReadOnlyDictionary<int, string> Text)
{
    public string LayoutName => CityInformationSources.LayoutName;
    public IReadOnlySet<int> HiddenNodes { get; init; } = new HashSet<int>();
    public IReadOnlyDictionary<int, int> TextStyles { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, MapCornerColors> TextColors { get; init; } = new Dictionary<int, MapCornerColors>();
    public IReadOnlyDictionary<int, int> SpriteIds { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, string> UnresolvedFields { get; init; } = new Dictionary<int, string>();
    public int? PortraitAppearanceId { get; init; }
    public OfficerPortraitRoute? Portrait { get; init; }
    public DeploymentNarrationPortraitRoute? StandingPortrait { get; init; }
    public int? ForceId { get; init; }
    public int? ForceColorIndex { get; init; }
    public AreaMarkerColor? ForceNameColor { get; init; }
    public DetailForceAppearance? ForceFlag { get; init; }
    public int? DistrictNumber { get; init; }
    public bool? NearbyEnemyPanelVisible { get; init; }
    public DetailMiniMapValues? MiniMap { get; init; }
    public IReadOnlyList<CityInformationGauge> Gauges { get; init; } = [];
    public CityInformationDutyPortrait? Recruiter { get; init; }
    public CityInformationDutyPortrait? Trainer { get; init; }
    public CityInformationTactic? Tactic { get; init; }
    public bool ReportContext { get; init; }
    public string? UnresolvedOwner { get; init; }
}

public static class CityInformationSources
{
    public const string LayoutName = "11_01_03_00_info_city0_pk_cn";
    public const int TitleId = 0x464;
    public const int BasicTabCaptionId = 0x3CD;
    public const int OfficersCaptionId = 0x211;
    public const int NearbyEnemyCaptionId = 0x3D6;
    public static IReadOnlyList<(int Node, int Message)> Captions { get; } = Array.AsReadOnly(new (int, int)[]
    {
        (13,0x1FD),(15,0x215),(17,0x1FE),(22,0x1FF),(24,0x200),
        (32,0x20B),(39,0x225),(47,0x218),(52,0x217),(57,0x3EC),
        (62,0x202),(67,0x5EA),(72,0x3ED),(77,0x25E),(84,0x25C),
        (94,0x20C),(99,0x254),(104,0x3F2),(110,0x20D),(115,0x255),
        (120,0x3F4),(126,0x20E),(133,0x256),(138,0x226),(145,0xAD),
        (151,0x893),(165,0x3EE),(173,0x3EF),(177,0x234),(182,0x2B0),
        (187,0x6F1),(192,0x6F2)
    });
}
