using Godot;
using San14.Data;

/// <summary>Source command sprite layers with ordinary BaseButton input ownership.</summary>
internal partial class MapCommandRailButton : TextureButton
{
    private Texture2D[] _first = [];
    private Texture2D[]? _second;
    private UiSourceSprite _firstLayer = null!;
    private Texture2D? _mask;
    private Rect2 _maskRectangle;
    private Texture2D? _secondMask;
    private Rect2 _secondMaskRectangle;
    private UiSourceSprite? _captionLayer;
    private UiSourceSprite? _focusLayer;
    private Texture2D[]? _focusTextures;
    private Color[]? _focusTints;
    private Texture2D? _caption;
    private UiSourceText? _text;
    private MapFont? _font;
    private MapFontProfile[]? _textProfiles;
    private readonly MapCornerColors[] _palettes = new MapCornerColors[5];
    private bool _selected;
    internal event Action<bool>? HoverChanged;
    internal bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; _selected = value; QueueRedraw(); }
    }

    internal int VisualState => Disabled ? (IsHovered() ? 4 : 3) : Selected ? 2 : GetDrawMode() switch
    {
        DrawMode.Pressed or DrawMode.HoverPressed => 2,
        DrawMode.Hover => 1,
        _ => 0
    };

    internal void Configure(Shader shader, Texture2D[] first, Texture2D[]? second,
        Texture2D? caption, Vector2 visualSize, Vector2 hitSize, Rect2 captionRectangle,
        Texture2D? mask = null, Rect2 maskRectangle = default,
        Texture2D? secondMask = null, Rect2 secondMaskRectangle = default)
    {
        _first = first;
        _second = second;
        _caption = caption;
        _mask = mask;
        _maskRectangle = maskRectangle;
        _secondMask = secondMask;
        _secondMaskRectangle = secondMaskRectangle;
        if (second is not null && mask is null)
            throw new ArgumentException("A clipped command button requires its authored mask.", nameof(mask));
        Size = hitSize;
        IgnoreTextureSize = true;
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Stop;
        MapSpritePalettes palettes = MapSpritePalettes.Create();
        for (int i = 0; i < _palettes.Length; i++) _palettes[i] = palettes[25 + i];
        _firstLayer = Layer("ButtonBase", second is null ? first[0] : second[0], new(Vector2.Zero, visualSize),
            second is null ? UiSourceSprite.Blend.Ordinary : UiSourceSprite.Blend.ClippedButton);
        if (caption is not null) _captionLayer = Layer("Caption", caption, captionRectangle, UiSourceSprite.Blend.Ordinary);
        MouseEntered += () => { QueueRedraw(); HoverChanged?.Invoke(true); };
        MouseExited += () => { QueueRedraw(); HoverChanged?.Invoke(false); };
        QueueRedraw();

        UiSourceSprite Layer(string name, Texture2D image, Rect2 rectangle, UiSourceSprite.Blend blend)
        {
            var layer = new UiSourceSprite { Name = name, Position = rectangle.Position, Size = rectangle.Size };
            layer.Configure(shader, image, blend);
            AddChild(layer);
            return layer;
        }
    }

    internal void ConfigureFocus(Shader shader, Texture2D focus, Texture2D disabledFocus,
        Color focusTint, Color disabledFocusTint)
    {
        _focusTextures = [focus, disabledFocus];
        _focusTints = [focusTint, disabledFocusTint];
        _focusLayer = new UiSourceSprite { Name = "SourceFocusEffect", Size = _firstLayer.Size };
        _focusLayer.Configure(shader, focus, _second is null
            ? UiSourceSprite.Blend.Additive : UiSourceSprite.Blend.ClippedButton);
        AddChild(_focusLayer);
        if (_captionLayer is not null) MoveChild(_focusLayer, _captionLayer.GetIndex());
        _focusLayer.Hide();
        QueueRedraw();
    }

    internal void ConfigureText(MapFont font, MapFontProfile profile, string caption)
    {
        _font = font;
        MapTextPresets presets = MapTextPresets.Create();
        _textProfiles = Enumerable.Range(28,5).Select(index => profile.WithPreset(presets[index])).ToArray();
        _text = new UiSourceText { Name = "Caption", Position = new(29,4), Size = new(130,30) };
        _text.Configure(font, _textProfiles[0]);
        _text.Text = caption;
        AddChild(_text);
        QueueRedraw();
    }

    public override bool _HasPoint(Vector2 point) => point.X >= 0 && point.Y >= 0 && point.X <= Size.X && point.Y <= Size.Y;

    internal void SetCaption(Texture2D caption)
    {
        _caption = caption;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_first.Length == 0) return;
        int state = VisualState;
        _firstLayer.SetSource(_second is null ? _first[state] : _second[state]);
        if (_second is not null) _firstLayer.SetClipping(_first[state], _mask!, _maskRectangle, _secondMask, _secondMaskRectangle);
        if (_focusLayer is not null)
        {
            _focusLayer.Visible = state is 1 or 4;
            if (_focusLayer.Visible)
            {
                int focus = state == 1 ? 0 : 1;
                _focusLayer.SetSource(_focusTextures![focus]);
                _focusLayer.Modulate = _focusTints![focus];
                if (_second is not null)
                    _focusLayer.SetClipping(_second[state], _mask!, _maskRectangle, _secondMask, _secondMaskRectangle);
            }
        }
        _captionLayer?.SetSource(_caption!, _palettes[state]);
        if (_text is not null)
        {
            _text.Configure(_font!, _textProfiles![state]);
            _text.QueueRedraw();
        }
    }

    public override void _ExitTree() => HoverChanged = null;
}
