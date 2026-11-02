using Godot;
using San14.Data;

/// <summary>A current object row from the original common list, borrowing view assets.</summary>
internal partial class UiSourceListRow : UiSourceButton
{
    internal event Action<int>? TextVisualStateChanged;
    private Action<string,string?>? applyContents;
    internal void ApplyContents(string caption, string? icon = null) => applyContents?.Invoke(caption, icon);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string caption, float columnWidth, bool leadingCell, string? icon = null, bool inlineIcon = false)
    {
        var source = assets.SourceLayout(0xF2C06161);
        var presets = MapTextPresets.Create();
        float inset = leadingCell ? source.Nodes[1].Value44 : 0;
        Size = new(inset + columnWidth, source.Nodes[0].Value46);
        int[] bodyNodes = [23, 25, 28, 31, 33];
        int[] frontNodes = [4, 6, 9, 12, 14];
        UiSourceArtwork? front = leadingCell
            ? source.StateArt(this, frontNodes, new Rect2(Vector2.Zero, new(inset, Size.Y)), assets, shader) : null;
        var body = source.StateArt(this, bodyNodes, new Rect2(new(inset, 0), new(columnWidth, Size.Y)), assets, shader);

        // The caller creates a fresh type-2 cell; it does not copy template36.
        // AEE280 defaults plus the row creator's size/alignment/width writes.
        var profile = new MapFontProfile(source.Source.Source.ResourceUri + "/dynamic40", 0, 0,
            .875f, .875f, 0, 0, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue,
            0, 0x01, 1, 0, 0, 0xFF000000, 0xFF000000, 0xFF000000, 0xFF000000)
        { WidthFitMode = 2, WidthReserve = 0, StrokeColour = 0xFF000000, StrokeWidth = 3 };
        var label = new UiSourceText { Name = "CurrentSelectionCaption",
            Position = new(inset + 10, 10), Size = new(columnWidth - 20, 28) };
        AddChild(label);
        label.Configure(font, profile.WithPreset(presets[Selected ? 10 : 8]));
        label.Text = caption;
        UiSourceSprite? placeIcon = null;
        var labelBounds=new Rect2(label.Position,label.Size);
        string? currentIcon=null;
        var palettes = MapSpritePalettes.Create();
        void SetIcon(string? name)
        {
            if (!leadingCell && !inlineIcon) return;
            currentIcon=name;
            label.Position=labelBounds.Position;label.Size=labelBounds.Size;
            if (inlineIcon) label.Configure(font,profile.WithPreset(presets[8+Math.Min(VisualState,3)]));
            if (name is null) { placeIcon?.Hide(); return; }
            if (placeIcon is null)
            {
                placeIcon = new UiSourceSprite { Name = "PlaceIcon", Position = new(8, 8), Size = new(32, 32) };
                AddChild(placeIcon);
                placeIcon.Configure(shader,assets.SrgbSprite(name),UiSourceSprite.Blend.Ordinary);
            }
            else placeIcon.SetSource(assets.SrgbSprite(name));
            placeIcon.SetMeta("source_sprite", name);
            if (inlineIcon)
                UiSourceInlineIconLayout.Apply(label,placeIcon,labelBounds,font,
                    profile.WithPreset(presets[8+Math.Min(VisualState,3)]),assets.Sprite(name).Texture.Region.Size);
            else placeIcon.SetColors(palettes[36 + VisualState]);
            placeIcon.Show();
        }
        SetIcon(icon);
        applyContents = (value, nextIcon) => { label.Text = value; SetIcon(nextIcon); };
        ConfigureVisualState(state =>
        {
            front?.Apply(state); body.Apply(state);
            label.Configure(font, profile.WithPreset(presets[8 + Math.Min(state, 3)]));
            label.QueueRedraw();
            if (inlineIcon && currentIcon is not null) SetIcon(currentIcon);
            else placeIcon?.SetColors(palettes[36 + state]);
            TextVisualStateChanged?.Invoke(state);
        });
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
}
