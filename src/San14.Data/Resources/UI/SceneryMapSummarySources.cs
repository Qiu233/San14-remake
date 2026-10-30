namespace San14.Data;

/// <summary>The actual compact map summary, independent of the full Scenery detail dialog.</summary>
public sealed record SceneryMapSummaryPage(int SceneryId, bool? ReceiverValid,
    IReadOnlyDictionary<int, string> Text)
{
    public int? ImageIndex { get; init; }
    public int? EffectKind { get; init; }
    public bool? EffectVisible { get; init; }
    public int? ForceColorIndex { get; init; }
    public AreaMarkerColor? ForceNameColor { get; init; }
    public bool? MaximumLevel { get; init; }
    public int? ProgressCurrent { get; init; }
    public int? ProgressMaximum { get; init; }
    public string? UnresolvedOwner { get; init; }
}

public static class SceneryMapSummarySources
{
    public const string LayoutName = "00_82_13_sum_meishou_cn";
    public const int Slot = 15;
    public const int MountX = 24;
    public const int MountY = 284;
    public static IReadOnlyList<(int Node, int Message)> Captions { get; } =
        Array.AsReadOnly(new (int, int)[] { (17, 0x201), (21, 0x202),
            (27, 0x842), (35, 0x411), (39, 0x44F) });

    //925FD0 indexes with floor(00ECB0), and compares elapsed strictly greater.
    public static int ShowDelayMilliseconds(int sourceZoomBand) => sourceZoomBand switch
    {
        >= 0 and <= 3 => 275,
        4 or 5 => 480,
        6 => 250,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceZoomBand))
    };
}
