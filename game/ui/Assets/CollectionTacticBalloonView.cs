using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The gallery's inherited full tactic balloon. The original effect
/// positions and height setter are retained; no campaign query runs on hover.</summary>
internal partial class CollectionTacticBalloonView : Control
{
    private GameResourceRepository repository = null!;
    private UiAssets assets = null!;
    private Shader shader = null!;
    private MapFont font = null!;
    private LoadedLayout authored = null!;
    private CollectionTacticBalloon? current;
    private Control? contents;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font)
    {
        this.repository = repository; this.assets = assets; this.shader = shader; this.font = font;
        authored = MapMarkerLayout.LoadNamed(repository, "11_00_01_offset_05");
        Name = "CollectionTacticBalloon"; ZIndex = 20; MouseFilter = MouseFilterEnum.Ignore;
        SetMeta("source_layout", authored.Source.ResourceUri); Hide();
    }

    internal void Display(CollectionTacticBalloon? value)
    {
        if (value is null) { Hide(); return; }
        if (!ReferenceEquals(current, value))
        {
            if (contents is { } old) { old.Hide(); old.QueueFree(); }
            contents = new Control { Name = "TacticBalloonContents", MouseFilter = MouseFilterEnum.Ignore };
            AddChild(contents);
            var original = new UiSourceLayout(authored);
            int count = value.Effects.Count;
            if (count > 2) throw new InvalidDataException("collection.officer.tactic_balloon_effect_count");
            //8A68E0 is called twice by the native consumer. Both calls derive
            //sizes from authored geometry, so neither compounds the reduction.
            float row = original.Rectangle(18).Position.Y - original.Rectangle(16).Position.Y;
            float hiddenTail = original.Rectangle(19).Size.Y + original.Rectangle(20).Size.Y;
            float reduction = (2 - count) * row + hiddenTail;
            Size = new(original.Nodes[0].Value44, original.Nodes[0].Value46 - reduction);
            var geometry = new Dictionary<int, UiSourceNodeGeometry>
            {
                [0] = new(Size: Size),
                [1] = new(Size: original.Rectangle(1).Size - new Vector2(0, reduction)),
                [2] = new(Size: Size),
                [14] = new(Size: original.Rectangle(14).Size - new Vector2(0, count < 2 ? row : 0))
            };
            var source = new UiSourceLayout(authored, geometry);
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
            current = value;
        }
        Show(); UpdatePointer(GetParent<Control>().GetLocalMousePosition());
    }

    internal void UpdatePointer(Vector2 pointer)
    {
        int x = (int)pointer.X + 20, y = (int)pointer.Y + 20;
        Position = new(Math.Clamp(x, 0, 1920 - (int)Size.X), Math.Clamp(y, 0, 1080 - (int)Size.Y));
    }
}
