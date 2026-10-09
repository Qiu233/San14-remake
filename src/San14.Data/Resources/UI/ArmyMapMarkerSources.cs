using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct ArmySimpleMarkerGeometry(AreaMarkerRectangle Flag,
    AreaMarkerRectangle ChildHit);
public sealed record ArmyNormalMarkerGeometry(AreaMarkerRectangle FaceBase,
    AreaMarkerRectangle FaceFill, AreaMarkerRectangle Portrait,
    AreaMarkerRectangle PortraitMask, AreaMarkerRectangle NumberPanel,
    AreaMarkerRectangle NumberCell, AreaMarkerRectangle Icon,
    AreaMarkerRectangle Morale, AreaMarkerRectangle ChildHit);
public sealed record ArmyGroupMarkerGeometry(AreaMarkerRectangle FaceBase,
    AreaMarkerRectangle FaceFill, AreaMarkerRectangle Portrait,
    AreaMarkerRectangle ChildHit);
public readonly record struct ArmyMapHitRectangle(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public ArmyMapHitRectangle At(int x, int y) => this with { Left = Left + x, Top = Top + y };
}
public readonly record struct ArmyMapHitGeometry(ArmyMapHitRectangle Own,
    ArmyMapHitRectangle Tester)
{
    //8734B0 requires both controls;86FFE0 includes all four endpoints.
    //No alpha condition, or empty-test excluding a degenerate source edge.
    public bool Contains(int x, int y) => x >= Math.Max(Own.Left, Tester.Left) &&
        y >= Math.Max(Own.Top, Tester.Top) && x <= Math.Min(Own.Right, Tester.Right) &&
        y <= Math.Min(Own.Bottom, Tester.Bottom);
    public ArmyMapHitGeometry At(int x, int y) => new(Own.At(x, y), Tester.At(x, y));
}

/// <summary>Actual Army map child resources and ordinary settled geometry.
/// Shared source bytes do not imply shared runtime visibility or pose owners.</summary>
public sealed class ArmyMapMarkerSources
{
    public MapMarkerLayout TopAndUi { get; }
    public MapMarkerLayout SingleFlag { get; }
    public MapMarkerLayout GroupFlag { get; }
    public MapMarkerLayout Normal { get; }
    public MapMarkerLayout Grouping { get; }
    public MapMarkerLayout UnitAction { get; }
    public MapMarkerLayout Face { get; }

    public ArmyNormalMarkerGeometry NormalRestGeometry()
    {
        // 92F0A0/1BC4E0 cancel the nested Face offset; the Face root returns
        // to Normal's original replacement node5 left/top (Int32 setters).
        var faceSlot = RestRectangle(Normal, 5);
        var faceOffset = new Vector2((int)faceSlot.Left, (int)faceSlot.Top);
        var bar = RestRectangle(TopAndUi, 0x57);
        var normalSlot = RestRectangle(TopAndUi, 0x1B);
        return new(Offset(RestRectangle(Face, 4), faceOffset),
            Offset(RestRectangle(Face, 5), faceOffset),
            Offset(RestRectangle(Face, 7), faceOffset),
            Offset(RestRectangle(Face, 8), faceOffset),
            RestRectangle(Normal, 0x29), RestRectangle(Normal, 0x32),
            RestRectangle(TopAndUi, 0x51),
            new((int)bar.Left - (int)normalSlot.Left, (int)bar.Top - (int)normalSlot.Top, bar.Width, bar.Height),
            RestRectangle(Normal, 3));
    }

    public ArmyGroupMarkerGeometry GroupRestGeometry() => new(
        RestRectangle(Grouping, 4), RestRectangle(Grouping, 5),
        RestRectangle(Grouping, 0x14), RestRectangle(Grouping, 1));

    public ArmyMapHitGeometry SettledHitGeometry(bool group, bool simple)
    {
        //1AA0B0 copies the UI control's size to the parent. The replacement
        //Grouping face quad is not its own mouse rectangle.
        var ui = IntegerRectangle(RestRectangle(TopAndUi, 0));
        var own = new ArmyMapHitRectangle(0, 0, ui.Width, ui.Height);
        if (simple)
            return new(own, IntegerRectangle(RestRectangle(group ? GroupFlag : SingleFlag, 1)));
        if (group) return new(own, IntegerRectangle(RestRectangle(Grouping, 1)));
        //19E470 starts at Normal node0, expands only left/top/right for the
        //actual substituted UnitAction face node1, and retains Root bottom.
        var normal = IntegerRectangle(RestRectangle(Normal, 0));
        var faceSlot = IntegerRectangle(RestRectangle(Normal, 5));
        var face = IntegerRectangle(RestRectangle(Face, 1)).At(faceSlot.Left, faceSlot.Top);
        int left = Math.Min(normal.Left, face.Left), top = Math.Min(normal.Top, face.Top);
        return new(own, new(left, top, Math.Max(normal.Right, face.Right) - left, normal.Bottom - top));
    }

    private static ArmyMapHitRectangle IntegerRectangle(AreaMarkerRectangle rectangle)
        => new((int)rectangle.Left, (int)rectangle.Top, (int)rectangle.Width, (int)rectangle.Height);

    // Restricted to these normal-rest planar nodes. Ancestor pivots affect
    // their own quads, not the origin of their children (AC8F90).
    private static AreaMarkerRectangle RestRectangle(MapMarkerLayout layout, int index)
    {
        var chain = new Stack<LcskNodeRecord>();
        for (LcskNodeRecord? node = layout.Nodes[index]; node is not null;
            node = node.ParentIndex is int parent ? layout.Nodes[parent] : null) chain.Push(node);
        Vector2 position = Vector2.Zero, scale = Vector2.One;
        while (chain.TryPop(out var node))
        {
            if (node.HeaderValues[3] != 0 || node.HeaderValues[4] != 0 || node.HeaderValues[5] != 0 ||
                node.ParentIndex is not null && (node.Value48 >> 8) != 0x11)
                throw new NotSupportedException($"Army normal-rest transform requires its source planar/center-anchor branch: {layout.Name}/{node.Index}.");
            position += new Vector2(node.HeaderValues[0], -node.HeaderValues[1]) * scale;
            scale *= new Vector2(node.HeaderValues[7], node.HeaderValues[8]);
        }
        var selected = layout.Nodes[index];
        Vector2 size = new Vector2(selected.Value44, selected.Value46) * scale;
        int pivot = selected.Value48 & 255;
        position -= size * new Vector2((pivot >> 4) * .5f, (pivot & 15) * .5f);
        return new(position.X, position.Y, size.X, size.Y);
    }

    private static AreaMarkerRectangle Offset(AreaMarkerRectangle rectangle, Vector2 offset)
        => rectangle with { Left = rectangle.Left + offset.X, Top = rectangle.Top + offset.Y };

    private ArmyMapMarkerSources(MapMarkerLayout topAndUi,
        MapMarkerLayout singleFlag, MapMarkerLayout groupFlag,
        MapMarkerLayout normal, MapMarkerLayout grouping,
        MapMarkerLayout unitAction, MapMarkerLayout face)
        => (TopAndUi, SingleFlag, GroupFlag, Normal, Grouping, UnitAction, Face) =
            (topAndUi, singleFlag, groupFlag, normal, grouping, unitAction, face);

    public static ArmyMapMarkerSources Load(GameResourceRepository repository) => new(
        MapMarkerLayout.Load(repository, "00_81_07_0400_unit_flag", 0x3DA1641D, "LCSK8500"),
        MapMarkerLayout.Load(repository, "00_81_07_0410_btn_unit_offset_00", 0x625934A1, "LCSK7500"),
        MapMarkerLayout.Load(repository, "00_81_07_0450_btn_army_offset_00", 0x417953E4, "LCSK7500"),
        MapMarkerLayout.Load(repository, "00_81_07_0420_btn_unit_offset_10", 0x757F1A41, "LCSK8500"),
        MapMarkerLayout.Load(repository, "00_81_07_0460_btn_army_offset_11", 0x5C046EA3, "LCSK7500"),
        MapMarkerLayout.Load(repository, "00_81_09_unit_action", 0x47FB32BE, "LCSK8500"),
        MapMarkerLayout.Load(repository, "00_81_07_btn_domestic_offset_00", 0x7104E19F, "LCSK7500"));

    public static int NormalMapSettledDisplayLevel(int sourceCameraLevel) => sourceCameraLevel switch
    {
        >= 0 and <= 2 => 0,
        3 or 4 => 1,
        5 => 2,
        6 => -1,
        _ => throw new InvalidDataException("Original Army label level is outside its seven source rows.")
    };

    // 19E620 centers the currently enabled32x32 Simple child; these rects
    // include that screen root once. ChildHit is not the full mouse dispatch.
    public static ArmySimpleMarkerGeometry SimpleGeometry(bool group) => group
        ? new(new(-16, -16, 32, 32), new(-16, -13, 32, 26))
        : new(new(-12, -12, 24, 24), new(-11, -11, 22, 22));

    // E940 rows0/4 are passed from the loaded source style owner. This is a
    // screen offset; Army Unit world position has no City/Area height offset.
    public static Vector2 NormalScreenOffset(double cameraDistance90, float sourceNearDistance,
        float sourceDistanceRange)
    {
        int distanceOffset = (int)(((float)cameraDistance90 - sourceNearDistance) * 100f / sourceDistanceRange);
        return new(-39, -Math.Max(40, 100 - distanceOffset));
    }
}
