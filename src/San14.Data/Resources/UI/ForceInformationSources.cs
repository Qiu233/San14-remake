namespace San14.Data;

public sealed record ForceInformationRelation(string Text, int Value, int Style);

/// <summary>One ordinary Force default page. Text keys name authored Type2
/// nodes; the independent portrait, nameplate, relation marker and map are typed.</summary>
public sealed record ForceInformationPage(int ForceId, string Name, IReadOnlyDictionary<int, string> Text)
{
    public string LayoutName => ForceInformationSources.LayoutName;
    public IReadOnlySet<int> HiddenNodes { get; init; } = new HashSet<int>();
    public IReadOnlyDictionary<int, int> TextStyles { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, MapCornerColors> TextColors { get; init; } = new Dictionary<int, MapCornerColors>();
    public IReadOnlyDictionary<int, string> UnresolvedFields { get; init; } = new Dictionary<int, string>();
    public int? RulerId { get; init; }
    public int? PortraitAppearanceId { get; init; }
    public DeploymentNarrationPortraitRoute? StandingPortrait { get; init; }
    public DetailForceNameplate? Nameplate { get; init; }
    public DetailMiniMapValues? MiniMap { get; init; }
    public ForceInformationRelation? Relation { get; init; }
    public string? UnresolvedOwner { get; init; }
}

public static class ForceInformationSources
{
    public const string LayoutName = "11_01_01_00_info_seiryoku0_cn";
    public const string NameplateLayoutName = "11_01_01_00_info_seiryoku_offset_00";
    public const int TitleId = 0x45C;
    public const int BasicTabCaptionId = 0x3CD;
    public static IReadOnlyList<(int Node, int Message)> Captions { get; } = Array.AsReadOnly(new (int, int)[]
    {
        (13,0x3C8),(15,0x21B),(17,0x3ED),(23,0x1FE),(28,0x207),(33,0x218),(38,0x244),
        (68,0x20C),(73,0x254),(78,0x3F2),(83,0x3F3),(89,0x20D),(94,0x255),(99,0x3F4),
        (104,0x3F5),(110,0x20E),(115,0x256),(120,0x226),(127,0x413),(132,0x289),
        (137,0x414),(143,0x415),(148,0x416),(153,0x417),(158,0x418),(164,0x293),
        (169,0x419),(174,0x41A),(43,0x2DD),(50,0x2DE),(55,0x83E),(60,0x83F),
        (181,0x835),(186,0x836)
    });
}
