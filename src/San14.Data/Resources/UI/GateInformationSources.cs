namespace San14.Data;

public sealed record GateInformationGauge(int Node, int Current, int Maximum, int Style);

public sealed record GateInformationPage(int GateId, string Name, IReadOnlyDictionary<int, string> Text)
{
    public string LayoutName => GateInformationSources.LayoutName;
    public IReadOnlySet<int> HiddenNodes { get; init; } = new HashSet<int>();
    public IReadOnlyDictionary<int, int> TextStyles { get; init; } = new Dictionary<int, int>();
    //930D10 selects71-row corner colors, not78-row text presets.
    public IReadOnlyDictionary<int, MapCornerColors> TextColors { get; init; } = new Dictionary<int, MapCornerColors>();
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
    public IReadOnlyList<GateInformationGauge> Gauges { get; init; } = [];
    //The source uses the same common tactic marker as the City page.
    public CityInformationTactic? Tactic { get; init; }
    public string? UnresolvedOwner { get; init; }
}

public static class GateInformationSources
{
    public const string LayoutName = "11_01_07_00_sekisho0_pk";
    public const int TitleId = 0x466;
    public const int BasicTabCaptionId = 0x3CD;
    public const int OfficersCaptionId = 0x211;
    public const int NearbyEnemyCaptionId = 0x3D6;
    public static IReadOnlyList<(int Node, int Message)> Captions { get; } =
        Array.AsReadOnly(new (int, int)[] { (21,0x1FF),(23,0x200),(26,0x201),
            (66,0x25C),(16,0x1FE),(33,0x20B),(12,0x1FD),(49,0x217),
            (38,0x225),(14,0x215),(44,0x218),(54,0x3EC),(59,0x25E),
            (76,0x20E),(85,0x234),(81,0x226),(61,0x469),(68,0x469) });
}
