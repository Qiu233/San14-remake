namespace San14.Data;

public sealed record CityGateMarkerGeometry(AreaMarkerRectangle Flag,
    AreaMarkerRectangle TextCell, AreaMarkerRectangle Plate,
    AreaMarkerRectangle Hit, float TextWidth);
public sealed record MapBarGeometry(AreaMarkerRectangle Background,
    AreaMarkerRectangle Fill, int SourceFillWidth, float ScaleX, float ScaleY);

/// <summary>The four actually selected CE city/gate children. Static geometry
/// includes the main root once; animation samples remain a separate consumer.</summary>
public sealed class CityGateMapMarkerSources
{
    public MapMarkerLayout Full { get; }
    public MapMarkerLayout Simple { get; }
    public MapMarkerLayout CityFlag { get; }
    public MapMarkerLayout Gate { get; }
    public MapMarkerLayout Bar { get; }
    public const float WorldHeightOffset = 1000;

    private CityGateMapMarkerSources(MapMarkerLayout full,
        MapMarkerLayout simple, MapMarkerLayout cityFlag, MapMarkerLayout gate,
        MapMarkerLayout bar)
        => (Full, Simple, CityFlag, Gate, Bar) = (full, simple, cityFlag, gate, bar);

    public static CityGateMapMarkerSources Load(GameResourceRepository repository) => new(
        MapMarkerLayout.Load(repository, "00_81_07_0100_city_flag", 0xEDC35713, "LCSK8500"),
        MapMarkerLayout.Load(repository, "00_81_07_0120_btn_city_offset_01", 0x6E6DB2D5, "LCSK8500"),
        MapMarkerLayout.Load(repository, "cmn_flag_force_kunigae", 0xC628C466, "LCSK9500"),
        MapMarkerLayout.Load(repository, "00_81_07_btn_gate_offset_01", 0x06A0C88D, "LCSK7500"),
        MapMarkerLayout.Load(repository, "cmn_bar", 0xAFC125D8, "LCSK4500"));

    public MapBarGeometry BarGeometry(int current, int capacity, AreaMarkerRectangle control,
        int? retainedSourceFillWidth = null)
    {
        // 85B2B0 leaves this UI child's current width untouched for cap<=0.
        // The HP caller, rather than the shared bar, supplies its6000 fallback.
        int width = capacity > 0
            ? (int)((long)Bar.Nodes[1].Value44 * Math.Clamp(current, 0, capacity) / capacity)
            : retainedSourceFillWidth ?? Bar.Nodes[2].Value44;
        float sx = control.Width / Bar.Nodes[0].Value44, sy = control.Height / Bar.Nodes[0].Value46;
        var fill = Bar.Nodes[2];
        return new(control, new(control.Left + fill.HeaderValues[0] * sx,
            control.Top - fill.HeaderValues[1] * sy, width * sx, fill.Value46 * sy), width, sx, sy);
    }

    // 1A8680 changes width while 932380/930FF0 preserve the old left edge.
    // UTF16 length here follows the current single-line source string route.
    public static CityGateMarkerGeometry SimpleGeometry(string text, bool numbers, bool gate)
    {
        float width = Math.Max(72, (int)(12f * (numbers ? text.Length : text.Length * 2)));
        return new(new(-20, gate ? -38 : -40, 40, gate ? 40 : 52),
            new(19, numbers ? -33 : -30, width, 24), new(1, -34, width + 36, 32),
            new(-18, -37, width + 55, 46), width);
    }

    public static int NormalMapSettledDisplayLevel(int sourceCameraLevel) => sourceCameraLevel switch
    {
        >= 0 and <= 3 => 0,
        4 => 1,
        5 or 6 => 2,
        _ => throw new InvalidDataException("Original city/gate label level is outside its seven source rows.")
    };

    /// <summary>19C660 selects a child tester;8734B0 first checks the main
    /// control itself. Its height stays52, width becomes tester.width. Both
    /// rectangles use the source inclusive tester, independent of alpha.</summary>
    public static AreaMarkerRectangle NormalHitRectangle(int display, string numbers, bool gate)
    {
        AreaMarkerRectangle tester = display switch
        {
            0 => new(-20, -48, 121, 78),
            1 => SimpleGeometry(numbers, true, gate).Hit,
            2 when gate => new(-20, -38, 40, 40),
            2 => new(-18, -37, 36, 46),
            _ => throw new InvalidDataException("Original city/gate label hit requested outside display0..2.")
        };
        float left = Math.Max(-20, tester.Left), top = Math.Max(-40, tester.Top);
        float right = Math.Min(-20 + tester.Width, tester.Left + tester.Width);
        float bottom = Math.Min(12, tester.Top + tester.Height);
        return new(left, top, right - left, bottom - top);
    }
}
