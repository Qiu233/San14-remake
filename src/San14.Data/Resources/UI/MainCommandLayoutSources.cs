using System.Collections.Immutable;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Authored channel values sampled at a real clip's final frame.</summary>
public sealed record MainCommandLayoutStableFrame(string Name, float Frame,
    ImmutableArray<LcskAnimationValue> Values);

/// <summary>A normal resource owner and its finite set of presentation clips.
/// Stable values are prepared once with the existing LCSK curve reader.</summary>
public sealed class MainCommandLayoutAsset
{
    public LoadedLayout Source { get; }
    public IReadOnlyList<LcskNodeRecord> Nodes { get; }
    public IReadOnlyList<LcskAnimationClip> Clips { get; }
    private readonly Dictionary<string, MainCommandLayoutStableFrame> stable;

    internal MainCommandLayoutAsset(LoadedLayout source, IReadOnlySet<string> names)
    {
        Source = source;
        Nodes = LcskNodeReader.Read(source.Layout);
        //Attention has actual static artwork and no binding block. It does not
        //need an invented animation or a call that requires nonempty bindings.
        Clips = names.Count == 0 ? Array.Empty<LcskAnimationClip>() :
            LcskAnimationReader.Read(source.Layout, names);
        stable = Clips.ToDictionary(clip => clip.Name,
            clip => new MainCommandLayoutStableFrame(clip.Name, clip.EndFrame,
                clip.Sample(clip.EndFrame).ToImmutableArray()), StringComparer.Ordinal);
        if (names.Any(name => !stable.ContainsKey(name)))
            throw new InvalidDataException("main_command.layout.animation_name");
    }

    public MainCommandLayoutStableFrame Stable(string name) => stable.TryGetValue(name, out var value)
        ? value : throw new InvalidDataException("main_command.layout.animation_name");
}

/// <summary>Each semantic category is attached to its actual visible-row mounts.
/// The caller changes hit width without resizing the160x64 button artwork.</summary>
public sealed record MainCommandButtonMount(MapCommandCategory Category, int Row,
    int ButtonNode, int HitWidth, int HitHeight, int CursorNode, int AttentionNode,
    int BadgeNode, int CaptionMessageId);

/// <summary>The ordinary main-command layout's actual resource hierarchy.
/// Own this with the HUD resources and rebuild it when those resources change.</summary>
public sealed class MainCommandLayoutSources
{
    public const int OuterMainMount = 1;
    public const int OuterSubmenuMount = 2;
    public const int OuterLinkMount = 3;
    public const int MainFoldMount = 77;
    public const int MainGuideMount = 79;
    public const int LinkRotationNode = 1;

    public MainCommandLayoutAsset Outer { get; }
    public MainCommandLayoutAsset Main { get; }
    public MainCommandLayoutAsset Submenu { get; }
    public MainCommandLayoutAsset Link { get; }
    public MainCommandLayoutAsset Button { get; }
    public MainCommandLayoutAsset Fold { get; }
    public MainCommandLayoutAsset Cursor { get; }
    public MainCommandLayoutAsset Attention { get; }
    public MainCommandLayoutAsset Badge { get; }

    private static readonly HashSet<string> OuterNames =
        ["cmd_slide_in", "cmd_slide_out", "cmd_hide_in", "cmd_hide_loop", "cmd_hide_out"];
    private static readonly HashSet<string> MainNames =
        ["main_6btn_in", "main_6btn_loop", "main_6btn_out", "main_7btn_in",
            "main_7btn_loop", "main_7btn_out", "close_btn_in", "close_btn_loop", "close_btn_out"];
    private static readonly HashSet<string> SubmenuNames = ["sub_in", "sub_loop", "sub_out"];
    private static readonly HashSet<string> ButtonNames =
        ["0_normal", "1_focus", "2_push_select", "3_disable", "4_disable_forcus"];
    private static readonly HashSet<string> CursorNames = ["1_focus", "4_disable_forcus"];
    private static readonly HashSet<string> LinkNames = ["sub_in"];
    private static readonly HashSet<string> BadgeNames = ["flicker_loop"];
    private static readonly HashSet<string> NoAnimationNames = [];
    private static readonly int[] ButtonNodes = [21, 25, 29, 33, 37, 41, 46];
    private static readonly int[] HitWidths = [142, 141, 140, 140, 141, 142, 142];
    private static readonly int[] CursorNodes = [10, 11, 12, 13, 14, 15, 17];
    private static readonly int[] AttentionNodes = [52, 53, 54, 55, 56, 57, 59];
    private static readonly int[] BadgeNodes = [62, 64, 66, 68, 70, 72, 75];
    private static readonly int[] CaptionIds = [0x2D0, 0x2D1, 0x2D2, 0x2D3, 0x2D4, 0x331, 0x2D5];
    private static readonly float[] LinkAngles = [18f, 11.4f, 4.3f, -2.3f, -9f, -16.5f, -23.5f];
    private static readonly ImmutableArray<MainCommandButtonMount> SixButtons = CreateButtons(1);
    private static readonly ImmutableArray<MainCommandButtonMount> SevenButtons = CreateButtons(0);

    public MainCommandLayoutSources(GameResourceRepository repository, MapCommandRailSources rail)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(rail);
        //Use the normal Common-name resolver, including its actual5500 outer
        //and Link files. A similarly shaped8500 asset is not a substitute.
        Outer = new(MapMarkerLayout.LoadNamed(repository, "00_02_00_mein_command"), OuterNames);
        Main = new(new(rail.Main.Layout, rail.Main.Source), MainNames);
        Submenu = new(new(rail.Submenu.Layout, rail.Submenu.Source), SubmenuNames);
        Link = new(MapMarkerLayout.LoadNamed(repository, "00_02_00_mein_offset_32"), LinkNames);
        Button = new(new(rail.Button.Layout, rail.Button.Source), ButtonNames);
        Fold = new(new(rail.Fold.Layout, rail.Fold.Source), ButtonNames);
        Cursor = new(MapMarkerLayout.LoadNamed(repository, "00_02_00_mein_offset_23"), CursorNames);
        Attention = new(MapMarkerLayout.LoadNamed(repository, "00_02_00_mein_offset_24"), NoAnimationNames);
        Badge = new(MapMarkerLayout.LoadNamed(repository, "cmn_badge_cn"), BadgeNames);
    }

    public MainCommandLayoutStableFrame MainStable(int style, bool expanded)
    {
        bool seven = HasSeventhButton(style);
        return Main.Stable(seven ? expanded ? "main_7btn_in" : "main_7btn_out" :
            expanded ? "main_6btn_in" : "main_6btn_out");
    }

    public MainCommandLayoutStableFrame SubmenuStable(bool open) => Submenu.Stable(open ? "sub_in" : "sub_out");
    public MainCommandLayoutStableFrame FoldStable(bool visible) => Main.Stable(visible ? "close_btn_in" : "close_btn_out");

    public ImmutableArray<MainCommandButtonMount> ReadButtonMounts(int style)
        => HasSeventhButton(style) ? SevenButtons : SixButtons;

    /// <summary>891040 writes the actual visible row's source Euler-Z angle.
    /// A Y-down UI renderer converts the sign at its transform boundary.</summary>
    public float LinkAngleDegrees(int style, MapCommandCategory category)
    {
        bool seven = HasSeventhButton(style);
        int row = (int)category - (seven ? 0 : 1);
        if (row < 0 || row >= (seven ? 7 : 6))
            throw new ArgumentOutOfRangeException(nameof(category));
        return LinkAngles[row];
    }

    private static bool HasSeventhButton(int style) => style switch
    {
        //882B70's0 result also reaches8813E0's low-bit-clear basic six-row
        //layout. Special-context button admission is a different source gate.
        0 or 2 => false,
        3 => true,
        _ => throw new ArgumentOutOfRangeException(nameof(style))
    };

    private static ImmutableArray<MainCommandButtonMount> CreateButtons(int firstCategory)
    {
        var buttons = ImmutableArray.CreateBuilder<MainCommandButtonMount>(7 - firstCategory);
        for (int category = firstCategory; category < 7; category++)
        {
            int row = category - firstCategory;
            buttons.Add(new((MapCommandCategory)category, row, ButtonNodes[row], HitWidths[row], 64,
                CursorNodes[row], AttentionNodes[row], BadgeNodes[row], CaptionIds[category]));
        }
        return buttons.MoveToImmutable();
    }
}
