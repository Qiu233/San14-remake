namespace San14.Data;

/// <summary>The three leaves of the ordinary scenery Hex command tree.
/// Eligibility is supplied by its retained business query.</summary>
public sealed record SceneryHexMenuItem(int Payload, string Caption, bool? Enabled,
    string? UnresolvedOwner = null);

public sealed record SceneryInvestmentColumnSource(int Id, int Width, int CaptionId);

/// <summary>Authored resource bindings shared by both investment entry paths.</summary>
public static class SceneryInvestmentSources
{
    public const string LayoutName = "06_15_03_scenic_spot_taishou_cn";
    public const int TitleId = 0xB1;
    public const int SelectionTitleId = 0x848;
    public static IReadOnlyList<(int Node, int Message)> Captions { get; } =
        Array.AsReadOnly(new (int, int)[] { (9,0x849),(24,0x478),(29,0x477),
            (37,0x84C),(44,0x411),(53,0x84B),(59,0x20C),(67,0x292) });
    public static IReadOnlyList<SceneryInvestmentColumnSource> Columns { get; } =
        Array.AsReadOnly(new SceneryInvestmentColumnSource[] {
            new(0,188,0xAD),new(2,76,0x207),new(1,132,0x202),new(5,160,0x1FD),
            new(6,160,0x84C),new(7,188,0x411),new(8,160,0x44F) });
    public static string ImagePack(int index) => $"texture_stl_scenery_{index:D3}";
    public static string ImageSprite(int index) => $"sti_scenery_{index:D3}";
    public static string? EffectSprite(int kind) => kind switch {
        1 => "000_cmn_icon_kane_fuchi", 2 => "000_cmn_icon_hyourou_fuchi",
        3 => "000_cmn_icon_heishi_fuchi", _ => null };
}
