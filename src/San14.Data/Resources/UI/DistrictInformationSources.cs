namespace San14.Data;

/// <summary>One ordinary District default page, from the supplied current world.</summary>
public sealed record DistrictInformationPage(int DistrictId,string Name,IReadOnlyDictionary<int,string> Text)
{
    public string LayoutName => DistrictInformationSources.LayoutName;
    public IReadOnlySet<int> HiddenNodes { get; init; } = new HashSet<int>();
    public IReadOnlyDictionary<int,int> TextStyles { get; init; } = new Dictionary<int,int>();
    public IReadOnlyDictionary<int,MapCornerColors> TextColors { get; init; } = new Dictionary<int,MapCornerColors>();
    public IReadOnlyDictionary<int,string> UnresolvedFields { get; init; } = new Dictionary<int,string>();
    public int? LeaderId { get; init; }
    public int? PortraitAppearanceId { get; init; }
    public DeploymentNarrationPortraitRoute? StandingPortrait { get; init; }
    public int? ForceId { get; init; }
    public int? DistrictNumber { get; init; }
    public int? NameIconSpriteId { get; init; }
    public DetailForceAppearance? ForceFlag { get; init; }
    public DetailMiniMapValues? MiniMap { get; init; }
    public string? UnresolvedOwner { get; init; }
}

public static class DistrictInformationSources
{
    public const string LayoutName="11_01_06_00_info_gundan0_cn";
    public const string NameplateLayoutName="11_01_06_00_info_gundan_offset_00";
    public const int TitleId=0x465;
    public const int BasicTabCaptionId=0x3CD;
    public static IReadOnlyList<(int Node,int Message)> Captions { get; }=Array.AsReadOnly(new (int,int)[]
    {
        (18,0x207),(47,0x20D),(52,0x255),(36,0x254),(31,0x20C),(57,0x3F4),(41,0x3F2),
        (13,0x214),(23,0x218),(63,0x20E),(68,0x256),(73,0x226),(81,0x21F),(86,0x299),
        (91,0x544),(98,0x8A4),(103,0x8A5)
    });
}
