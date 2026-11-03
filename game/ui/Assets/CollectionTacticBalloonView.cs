using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original full tactic balloon shared by gallery and deployment.
/// Both callers supply immutable publications; no campaign query runs on hover.</summary>
internal partial class CollectionTacticBalloonView : Control
{
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapFont font = null!;
    private UiSourceLayout authored = null!;
    private TacticBalloonContent? current;
    private string? currentWarning;
    private Control? contents;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font)
    {
        this.repository = repository; this.assets = assets; this.shader = shader; this.font = font;
        authored = assets.SourceLayout("11_00_01_offset_05");
        Name = "CollectionTacticBalloon"; ZIndex = 20; MouseFilter = MouseFilterEnum.Ignore;
        SetMeta("source_layout", authored.Source.Source.ResourceUri); Hide();
    }

    internal void Display(TacticBalloonContent? value,string? warning=null)
    {
        if (value is null) { Hide(); return; }
        if (!ReferenceEquals(current, value)||currentWarning!=warning)
        {
            if (contents is { } old) { old.Hide(); old.QueueFree(); }
            contents = new Control { Name = "TacticBalloonContents", MouseFilter = MouseFilterEnum.Ignore };
            AddChild(contents);
            var original = authored;
            int count = value.Effects.Count;
            if (count > 2) throw new InvalidDataException("collection.officer.tactic_balloon_effect_count");
            //8A68E0 is called twice by the native consumer. Both calls derive
            //sizes from authored geometry, so neither compounds the reduction.
            float row = original.Rectangle(18).Position.Y - original.Rectangle(16).Position.Y;
            float hiddenTail = original.Rectangle(19).Size.Y + original.Rectangle(20).Size.Y;
            float reduction = (2 - count) * row + (warning is null ? hiddenTail : 0);
            Size = new(original.Nodes[0].Value44, original.Nodes[0].Value46 - reduction);
            var geometry = new Dictionary<int, UiSourceNodeGeometry>
            {
                [0] = new(Size: Size),
                [1] = new(Size: original.Rectangle(1).Size - new Vector2(0, reduction)),
                [2] = new(Size: Size),
                [14] = new(Size: original.Rectangle(14).Size - new Vector2(0, count < 2 ? row : 0))
            };
            if(warning is not null)
            {
                //8A2570 places the separator after the resized effect base.
                //92DF80 reads the warning's nominal height24; LF changes its
                //text layout, never that cell or the surrounding window size.
                float baseHeight=geometry[14].Size!.Value.Y;
                float separatorY=original.Rectangle(14).Position.Y+baseHeight-1;
                geometry[19]=new(Origin:new(original.Rectangle(19).Position.X,separatorY));
                geometry[20]=new(Origin:new(original.Rectangle(20).Position.X,
                    separatorY+original.Rectangle(19).Size.Y+2));
            }
            var source = new UiSourceLayout(authored.Source, geometry);
            source.Blur(contents, 1, source.Rectangle(1));
            foreach (int node in new[] { 2, 5, 6, 10, 11, 14 })
                source.Art(contents, node, source.Rectangle(node), assets, shader);
            var presets = MapTextPresets.Create();
            foreach (var (node, caption, preset) in new[]
            {
                (3, value.Category, 0), (7, value.TargetCaption, 13), (8, value.Target, 0),
                (12, value.WaitingCaption, 13), (13, value.WaitingDays, 0)
            }) source.Text(contents, node, default, caption, font, presets[preset]);
            var palettes = MapSpritePalettes.Create();
            foreach (var effect in value.Effects)
            {
                int iconNode = 15 + effect.Slot * 2, textNode = iconNode + 1;
                source.Art(contents, iconNode, source.Rectangle(iconNode), assets, shader,
                    palettes[effect.SpritePalette], commonSpriteOverride: effect.IconSpriteName);
                source.Text(contents, textNode, default, effect.Caption, font, presets[effect.TextPreset]);
            }
            if(warning is not null)
            {
                source.Art(contents,19,source.Rectangle(19),assets,shader);
                var box=source.Rectangle(20);
                var paragraph=new UiSourceParagraph {Name="TacticDisabledReason",Position=box.Position,Size=box.Size};
                contents.AddChild(paragraph);
                paragraph.Configure(font,WarningProfile(font.Source,original,warning));
                paragraph.Apply(warning);
            }
            current = value;currentWarning=warning;
        }
        Show(); UpdatePointer(GetParent<Control>().GetLocalMousePosition());
    }

    private static MapFontProfile WarningProfile(MapFontSource font,UiSourceLayout source,string warning)
    {
        var profile=MapFontProfile.FromNode(source.Nodes[20],source.Source.Source.ResourceUri);
        // The source's mode2 fits all literal lines with the widest one and
        // preserves height. The paragraph consumes each LF at an explicit
        //baseline, so it receives that fitted profile with no second fitting.
        string widest=warning.Split('\n').MaxBy(line=>font.Measure(line,profile).Width)!;
        return font.FitToWidth(widest,profile,source.Rectangle(20).Size.X) with {WidthFitMode=0};
    }

    internal static void PrepareWarningText(UiSourceLayout source,MapFontSource font,string warning,
        IDictionary<MapFont.PreparedTextKey,MapTextCache> caches)
    {
        var profile=WarningProfile(font,source,warning);
        foreach(string line in warning.Split('\n'))
            MapFont.PrepareText(font,line,profile,source.Rectangle(20).Size.X,caches);
    }

    internal void UpdatePointer(Vector2 pointer)
    {
        int x = (int)pointer.X + 20, y = (int)pointer.Y + 20;
        Position = new(Math.Clamp(x, 0, 1920 - (int)Size.X), Math.Clamp(y, 0, 1080 - (int)Size.Y));
    }
}
