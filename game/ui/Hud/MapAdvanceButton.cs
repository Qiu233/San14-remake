using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Original progress-button artwork; activation and cancellation use BaseButton input.</summary>
internal partial class MapAdvanceButton : TextureButton
{
    private readonly Texture2D[] _backgrounds = new Texture2D[5];
    private readonly MapCornerColors[] _colors = new MapCornerColors[5];
    private Texture2D _caption = null!;
    private Texture2D _play = null!;
    private Texture2D _advanceCaption = null!,_stopCaption = null!,_advanceIcon = null!,_stopIcon = null!;
    private bool _stopping;
    private Rect2 _captionRectangle;
    private Rect2 _playRectangle;
    private Vector2 _sourceSize;

    internal int VisualState => Disabled ? (IsHovered() ? 4 : 3) : GetDrawMode() switch
    {
        DrawMode.Pressed or DrawMode.HoverPressed => 2,
        DrawMode.Hover => 1,
        _ => 0
    };

    internal void Configure(UiAssets assets, GameResourceRepository repository)
    {
        var layout = repository.LoadLayout("ScreenLayout", 0xC7725FCB);
        var nodes = LcskNodeReader.Read(layout.Layout);
        LcskNodeRecord hit = nodes.Single(n => n.Name == "HitArea");
        Size = _sourceSize = new(hit.Value44, hit.Value46);
        IgnoreTextureSize = true;
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Stop;
        MapSpritePalettes palettes = MapSpritePalettes.Create();
        for (int i = 0; i < _backgrounds.Length; i++)
        {
            _backgrounds[i] = assets.Sprite($"000_cmn_btn_adv_{i}").Texture;
            // CProgressBtnBehavior writes palettes 25..29 to both sprite glyphs.
            _colors[i] = palettes[25 + i];
        }
        _caption = _advanceCaption = assets.Sprite("000_cmn_txt_adv_shinko").Texture;
        _play = _advanceIcon = assets.Sprite("000_cmn_icon_advance_play").Texture;
        _stopCaption=assets.Sprite("000_cmn_txt_adv_teishi").Texture;
        _stopIcon=assets.Sprite("000_cmn_icon_advance_stop").Texture;
        _captionRectangle = GlyphRectangle("cmn_txt_adv_xx_pc026_030");
        _playRectangle = GlyphRectangle("play_pc026_030");
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        Resized += QueueRedraw;
        QueueRedraw();

        Rect2 GlyphRectangle(string name)
        {
            LcskNodeRecord node = nodes.Single(n => n.Name == name);
            if (node.Value48 != 0x1111)
                throw new InvalidDataException($"Unsupported progress glyph anchor: {name}.");
            Vector2 size = new(node.Value44 * node.HeaderValues[7], node.Value46 * node.HeaderValues[8]);
            Vector2 center = new(node.HeaderValues[0], -node.HeaderValues[1]);
            return new(center - size / 2, size);
        }
    }

    internal void ApplyExecution(bool stopping)
    {
        if(_stopping==stopping)return;
        _stopping=stopping;_caption=stopping?_stopCaption:_advanceCaption;
        _play=stopping?_stopIcon:_advanceIcon;QueueRedraw();
    }

    public override void _Draw()
    {
        if (_backgrounds[0] is null) return;
        int state = VisualState;
        DrawTextureRect(_backgrounds[state], new(Vector2.Zero, Size), false);
        Vector2 scale = Size / _sourceSize;
        Glyph(_caption, _captionRectangle);
        Glyph(_play, _playRectangle);

        void Glyph(Texture2D texture, Rect2 source)
        {
            Rect2 rectangle = new(source.Position * scale, source.Size * scale);
            Rect2 uv = new(Vector2.Zero, Vector2.One);
            if (texture is AtlasTexture atlas)
            {
                texture = atlas.Atlas;
                uv = new(atlas.Region.Position / texture.GetSize(), atlas.Region.Size / texture.GetSize());
            }
            MapCornerColors colors = _colors[state];
            DrawPolygon([rectangle.Position, new(rectangle.End.X, rectangle.Position.Y), rectangle.End,
                    new(rectangle.Position.X, rectangle.End.Y)],
                [Color(colors.LT), Color(colors.RT), Color(colors.RB), Color(colors.LB)],
                [uv.Position, new(uv.End.X, uv.Position.Y), uv.End, new(uv.Position.X, uv.End.Y)], texture);
        }
    }

    private static Color Color(AreaMarkerColor color) => new(color.AuthoredRed / 255f,
        color.AuthoredGreen / 255f, color.AuthoredBlue / 255f, color.AuthoredAlpha / 255f);
}
