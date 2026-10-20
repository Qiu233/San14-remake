using Godot;
using San14.Data;

/// <summary>The original text-column header, sharing its view's font and artwork.</summary>
internal partial class UiSourceColumnButton : UiSourceButton
{
    private Control? _sortDown, _sortUp;
    private UiSourceText? _label;
    private readonly List<UiSourceSprite> _sortSprites = [];

    internal void ApplyCaption(string caption)
    {
        if (_label is not null) _label.Text=caption;
    }

    internal void ApplySortDirection(bool? ascending)
    {
        if (_sortDown is null || _sortUp is null) return;
        _sortDown.Visible = ascending == false;
        _sortUp.Visible = ascending == true;
    }

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string caption, string? icon = null, bool personList = false)
    {
        var source = new UiSourceLayout(repository, 0x7A7B5B8C);
        var presets = MapTextPresets.Create();
        var states = new[] { 3, 5, 8, 11, 11 }
            .Select(index => source.Art(this, index, new Rect2(Vector2.Zero, Size), assets, shader)).ToArray();
        UiSourceText? label = null;
        UiSourceSprite? picture = null;
        MapFontProfile? profile = null;
        var palettes = MapSpritePalettes.Create();
        if (icon is null)
        {
            Rect2 captionRectangle = source.Rectangle(13);
            captionRectangle.Size += Size - new Vector2(source.Nodes[0].Value44, source.Nodes[0].Value46);
            if (personList) captionRectangle = new(new Vector2(7, 10), new Vector2(Size.X - 14, 28));
            profile = MapFontProfile.FromNode(source.Nodes[13], source.Source.Source.ResourceUri);
            if (personList) profile = profile with { WidthFitMode = 2, WidthReserve = 0 };
            label = source.Text(this, 13, default, caption, font, rectangle: captionRectangle);
            _label = label;
        }
        else
        {
            picture = new UiSourceSprite { Name = "PlaceIcon", Position = new(8, 10), Size = new(33, 28) };
            AddChild(picture);
            picture.Configure(shader, assets.SrgbSprite(icon), UiSourceSprite.Blend.Ordinary);
            picture.SetMeta("source_sprite", icon);
        }
        ConfigureStates(states, state =>
        {
            if (label is not null)
            {
                label.Configure(font, profile!.WithPreset(presets[3 + Math.Min(state, 3)]));
                label.QueueRedraw();
            }
            ApplyPictureColours(state);
        });
        if (personList)
        {
            var arrows = new UiSourceLayout(source.Source, new Dictionary<int,UiSourceNodeGeometry>
            {
                [0] = new(Size: Size),
                // The source top-left setter targets (width-16,0), before rotation.
                // Integer own-anchor (6,9) minus parent attachment (26,24)
                // converts that root-relative target to this layout's raw offset.
                [16] = new(Size: new(32,32), Offset: new(Size.X-36,-15)),
                [17] = new(Size: new(32,32), Offset: new(Size.X-36,-15))
            });
            _sortDown = arrows.Art(this,16,arrows.Rectangle(16),assets,shader);
            _sortUp = arrows.Art(this,17,arrows.Rectangle(17),assets,shader);
            _sortSprites.AddRange(_sortDown.FindChildren("*","",true,false).OfType<UiSourceSprite>());
            _sortSprites.AddRange(_sortUp.FindChildren("*","",true,false).OfType<UiSourceSprite>());
            ApplyPictureColours(VisualState);
            ApplySortDirection(null);
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        void ApplyPictureColours(int state)
        {
            var colours = palettes[state switch { 0 => 25, 1 => 26, 2 => 27, _ => 20 }];
            picture?.SetColors(colours);
            foreach (var sprite in _sortSprites) sprite.SetColors(colours);
        }
    }
}
