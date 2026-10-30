using System.Runtime.InteropServices;
using Godot;
using San14.Data;

/// <summary>Original G1N glyphs, independent text caches and a local linear
/// compositor over the encoded native Main image. No system font is used.</summary>
internal sealed class MapFont : IDisposable
{
    private readonly Dictionary<ulong, DrawOwner> _owners = [];
    private readonly Shader _shader = CreateShader();
    internal readonly record struct PreparedTextKey(string Value, MapFontProfile Profile);
    private readonly IReadOnlyDictionary<PreparedTextKey, MapTextCache>? _preparedText;
    internal MapFontSource Source { get; }
    internal MapFont(MapFontSource source, IReadOnlyDictionary<PreparedTextKey, MapTextCache>? preparedText = null)
    { Source = source; _preparedText = preparedText; }

    // Pure CPU work for a page-owned font source, before its one-way handoff.
    internal static void PrepareText(MapFontSource source, string value, MapFontProfile profile, float width,
        IDictionary<PreparedTextKey, MapTextCache> caches)
    {
        if (value.Length == 0) return;
        profile.ValidateSingleLine();
        var fitted = source.FitToWidth(value, profile, width);
        var key = new PreparedTextKey(value, fitted);
        if (!caches.ContainsKey(key)) caches.Add(key, MapTextCache.Build(source, value, fitted));
    }
    internal MapFont(GameResourceRepository repository, string locale) =>
        Source = MapFontSource.Load(repository, locale);
    internal MapFontMeasure Measure(string value, MapFontProfile profile) => Source.Measure(value, profile);
    internal float CellBaselineY(MapFontProfile profile, float cellHeight) => Source.CellBaselineY(profile, cellHeight);
    internal void Prepare(IEnumerable<string> values, MapFontProfile profile)
    {
        // Validate when the UI accepts text, so unsupported source options
        // fail configuration and its normal cleanup instead of every redraw.
        profile.ValidateSingleLine();
        var characters = new HashSet<char>();
        foreach (string value in values)
            foreach (char code in value)
            {
                if (char.IsControl(code))
                    throw new InvalidDataException($"Original UI text requires a single source line: {profile.Source}.");
                if (characters.Add(code)) _ = Source.Glyph(code, profile.Face, profile.Palette);
            }
    }

    // A redraw reuses text slots. A changed name/profile replaces only that
    // slot's caches; exiting controls release their textures and Canvas RIDs.
    internal DrawOwner BeginDraw(CanvasItem canvas, Vector2? scale = null)
    {
        var owner = Owner(canvas);
        owner.Begin(scale ?? Vector2.One);
        return owner;
    }

    internal void SetDrawingVisibilityLayer(CanvasItem canvas, uint layer) => Owner(canvas).SetVisibilityLayer(layer);

    private DrawOwner Owner(CanvasItem canvas)
    {
        ulong id = canvas.GetInstanceId();
        if (!_owners.TryGetValue(id, out var owner))
        {
            owner = new(canvas, _shader, () => { _owners.Remove(id, out var old); old?.Dispose(); }, _preparedText);
            _owners.Add(id, owner);
        }
        return owner;
    }
    internal void Draw(CanvasItem canvas, string value, Vector2 baseline,
        MapFontProfile profile, HorizontalAlignment alignment = HorizontalAlignment.Left,
        float width = -1, Color? modulate = null,IReadOnlyList<uint?>? glyphColours=null)
    {
        if (value.Length == 0) return;
        float x = baseline.X;
        if (width >= 0)
            x += alignment switch
            {
                HorizontalAlignment.Center => (width - Measure(value, profile).Width) * .5f,
                HorizontalAlignment.Right => width - Measure(value, profile).Width,
                _ => 0
            };
        _owners[canvas.GetInstanceId()].Draw(Source, value, profile, new(x, baseline.Y), modulate ?? Colors.White,glyphColours);
    }

    internal sealed class DrawOwner : IDisposable
    {
        private readonly CanvasItem _canvas;
        private readonly Shader _shader;
        private readonly Action _exiting;
        private readonly IReadOnlyDictionary<PreparedTextKey, MapTextCache>? _preparedText;
        private readonly List<TextSlot> _slots = [];
        private Vector2 _scale;
        private int _used;
        private uint _visibilityLayer;
        internal int ActiveCount => _used;
        internal DrawOwner(CanvasItem canvas, Shader shader, Action exiting,
            IReadOnlyDictionary<PreparedTextKey, MapTextCache>? preparedText)
        { _canvas = canvas; _shader = shader; _exiting = exiting; _preparedText = preparedText; _visibilityLayer = canvas.VisibilityLayer; canvas.TreeExiting += exiting; }
        internal void SetVisibilityLayer(uint layer)
        {
            _visibilityLayer = layer;
            foreach (var slot in _slots) slot.SetVisibilityLayer(layer);
        }
        internal void Begin(Vector2 scale) { _used = 0; _scale = scale; }
        internal void Draw(MapFontSource source, string value, MapFontProfile profile, Vector2 baseline, Color tint,
            IReadOnlyList<uint?>? glyphColours=null)
        {
            if (_used == _slots.Count) _slots.Add(new(_canvas, _shader, _used, _visibilityLayer, _preparedText));
            _slots[_used++].Draw(source, value, profile, baseline, tint, _scale, glyphColours);
        }
        internal void End()
        {
            for (int i = _used; i < _slots.Count; i++) _slots[i].Hide();
        }
        public void Dispose()
        {
            _canvas.TreeExiting -= _exiting;
            foreach (var slot in _slots) slot.Dispose();
            _slots.Clear();
        }
    }

    private sealed class TextSlot : IDisposable
    {
        private readonly CanvasItem _owner;
        private readonly Rid _copy, _draw;
        private readonly ShaderMaterial _material;
        private ImageTexture[] _textures = [];
        private string? _value;
        private MapFontProfile? _profile;
        private IReadOnlyList<uint?>? _glyphColours;
        private MapTextCache? _cache;
        private readonly IReadOnlyDictionary<PreparedTextKey, MapTextCache>? _preparedText;
        internal TextSlot(CanvasItem owner, Shader shader, int index, uint visibilityLayer,
            IReadOnlyDictionary<PreparedTextKey, MapTextCache>? preparedText)
        {
            _owner = owner; _preparedText = preparedText;
            Rid parent = owner.GetCanvasItem();
            _copy = RenderingServer.CanvasItemCreate(); _draw = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(_copy, parent); RenderingServer.CanvasItemSetParent(_draw, parent);
            // Text belongs to the parent's drawing, before its existing child
            // layers (Army). Keep each copy/draw pair consecutive and distinct
            // from the nonnegative scene-tree child indexes.
            RenderingServer.CanvasItemSetDrawIndex(_copy, int.MinValue + index * 2);
            RenderingServer.CanvasItemSetDrawIndex(_draw, int.MinValue + index * 2 + 1);
            SetVisibilityLayer(visibilityLayer);
            _material = new() { Shader = shader };
            RenderingServer.CanvasItemSetMaterial(_draw, _material.GetRid());
        }
        internal void SetVisibilityLayer(uint layer)
        {
            RenderingServer.CanvasItemSetVisibilityLayer(_copy, layer);
            RenderingServer.CanvasItemSetVisibilityLayer(_draw, layer);
        }
        internal void Draw(MapFontSource source, string value, MapFontProfile profile,
            Vector2 baseline, Color tint, Vector2 scale, IReadOnlyList<uint?>? glyphColours=null)
        {
            ImageTexture[] replaced = [];
            if (_value != value || _profile != profile || !ReferenceEquals(_glyphColours,glyphColours))
            {
                if (glyphColours is null && _preparedText is { } texts &&
                    texts.TryGetValue(new(value, profile), out var prepared)) _cache = prepared;
                else _cache = MapTextCache.Build(source, value, profile,glyphColours);
                replaced = _textures;
                _textures = new[] { _cache.Offset, _cache.Stroke, _cache.Body }.Select(pixels =>
                {
                    using var image = Image.CreateFromData(_cache.Width, _cache.Height, false,
                        Image.Format.Rgbaf, MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
                    return ImageTexture.CreateFromImage(image);
                }).ToArray();
                _material.SetShaderParameter("offset_cache", _textures[0]);
                _material.SetShaderParameter("stroke_cache", _textures[1]);
                _material.SetShaderParameter("body_cache", _textures[2]);
                _value = value; _profile = profile;_glyphColours=glyphColours;
            }
            _material.SetShaderParameter("node_tint", new Vector4(Cpu(tint.R), Cpu(tint.G), Cpu(tint.B), tint.A));
            var rect = new Rect2(baseline + new Vector2(_cache!.Left, _cache.Top), new(_cache.Width, _cache.Height));
            var transform = Transform2D.Identity.Scaled(scale);
            RenderingServer.CanvasItemSetTransform(_draw, transform);
            UiSourceBackBufferCopy.Configure(_owner, _copy, rect.Grow(1), transform);
            RenderingServer.CanvasItemSetVisible(_copy, true); RenderingServer.CanvasItemSetVisible(_draw, true);
            RenderingServer.CanvasItemClear(_draw);
            RenderingServer.CanvasItemAddTextureRect(_draw, rect, _textures[2].GetRid());
            // Both the material and the retained Canvas command must release
            // the old textures before their managed/native owners are retired.
            foreach (var texture in replaced) texture.Dispose();
        }
        private static float Cpu(float encoded) => MathF.Truncate(MathF.Pow(encoded, 2.2f) * 255f) / 255f;
        internal void Hide()
        { RenderingServer.CanvasItemSetVisible(_copy, false); RenderingServer.CanvasItemSetVisible(_draw, false); }
        public void Dispose()
        {
            RenderingServer.FreeRid(_copy); RenderingServer.FreeRid(_draw);
            _material.Dispose();
            foreach (var texture in _textures) texture.Dispose();
        }
    }

    private static Shader CreateShader() => new() { ResourceName = "OriginalMapTextLinearComposite", Code = """
        shader_type canvas_item;
        render_mode unshaded, blend_disabled;
        uniform sampler2D screen_texture : hint_screen_texture, filter_nearest, repeat_disable;
        uniform sampler2D offset_cache : filter_linear, repeat_disable;
        uniform sampler2D stroke_cache : filter_linear, repeat_disable;
        uniform sampler2D body_cache : filter_linear, repeat_disable;
        uniform vec4 node_tint = vec4(1.0);
        vec3 decode_srgb(vec3 v) {
            return mix(pow((v + 0.055) / 1.055, vec3(2.4)), v / 12.92, lessThanEqual(v, vec3(0.04045)));
        }
        vec3 encode_srgb(vec3 v) {
            return mix(1.055 * pow(max(v, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055,
                v * 12.92, lessThanEqual(v, vec3(0.0031308)));
        }
        varying float inherited_alpha;
        void vertex() { inherited_alpha = COLOR.a; }
        vec4 over_cache(vec4 dst, vec4 src) {
            float a = src.a * node_tint.a * inherited_alpha;
            // Ordinary text node common00554400: SRC_ALPHA/INV_SRC_ALPHA
            // applies to both RGB and A. Cache RGB remains straight.
            return vec4(src.rgb * node_tint.rgb * a + dst.rgb * (1.0 - a), a * a + dst.a * (1.0 - a));
        }
        void fragment() {
            vec4 offset = texture(offset_cache, UV);
            vec4 stroke = texture(stroke_cache, UV);
            vec4 body = texture(body_cache, UV);
            if (max(max(offset.a, stroke.a), body.a) * node_tint.a * inherited_alpha == 0.0) discard;
            vec4 dst = texture(screen_texture, SCREEN_UV);
            dst.rgb = decode_srgb(dst.rgb);
            dst = over_cache(dst, offset);
            dst = over_cache(dst, stroke);
            dst = over_cache(dst, body);
            COLOR = vec4(encode_srgb(dst.rgb), dst.a);
        }
        """ };

    public void Dispose()
    {
        foreach (var owner in _owners.Values) owner.Dispose();
        _owners.Clear(); _shader.Dispose();
    }
}
