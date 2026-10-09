using System.Runtime.InteropServices;
using Godot;
using San14.Data;

/// <summary>Original G1N glyphs, independent text caches and a local linear
/// compositor over the encoded native Main image. No system font is used.</summary>
internal sealed class MapFont : IDisposable
{
    private readonly Dictionary<ulong, DrawOwner> _owners = [];
    private readonly Shader _shader = CreateShader();
    internal MapFontSource Source { get; }
    internal MapFont(GameResourceRepository repository, string locale) =>
        Source = MapFontSource.Load(repository, locale);
    internal MapFontMeasure Measure(string value, MapFontProfile profile) => Source.Measure(value, profile);
    internal float CellBaselineY(MapFontProfile profile, float cellHeight) => Source.CellBaselineY(profile, cellHeight);
    internal void Prepare(IEnumerable<string> values, MapFontProfile profile)
    {
        foreach (char code in values.SelectMany(value => value).Distinct())
            _ = Source.Glyph(code, profile.Face, profile.Palette);
    }

    // A redraw reuses text slots. A changed name/profile replaces only that
    // slot's caches; exiting controls release their textures and Canvas RIDs.
    internal DrawOwner BeginDraw(CanvasItem canvas, Vector2? scale = null)
    {
        ulong id = canvas.GetInstanceId();
        if (!_owners.TryGetValue(id, out var owner))
        {
            owner = new(canvas, _shader, () => { _owners.Remove(id, out var old); old?.Dispose(); });
            _owners.Add(id, owner);
        }
        owner.Begin(scale ?? Vector2.One);
        return owner;
    }
    internal void Draw(CanvasItem canvas, string value, Vector2 baseline,
        MapFontProfile profile, HorizontalAlignment alignment = HorizontalAlignment.Left,
        float width = -1, Color? modulate = null)
    {
        if (value.Length == 0) return;
        if ((profile.RawFlags & ~1u) != 0)
            throw new InvalidDataException($"Original text effects require their source consumer: {profile.Source}.");
        float x = baseline.X;
        if (width >= 0)
            x += alignment switch
            {
                HorizontalAlignment.Center => (width - Measure(value, profile).Width) * .5f,
                HorizontalAlignment.Right => width - Measure(value, profile).Width,
                _ => 0
            };
        _owners[canvas.GetInstanceId()].Draw(Source, value, profile, new(x, baseline.Y), modulate ?? Colors.White);
    }

    internal sealed class DrawOwner : IDisposable
    {
        private readonly CanvasItem _canvas;
        private readonly Shader _shader;
        private readonly Action _exiting;
        private readonly List<TextSlot> _slots = [];
        private Vector2 _scale;
        private int _used;
        internal int ActiveCount => _used;
        internal DrawOwner(CanvasItem canvas, Shader shader, Action exiting)
        { _canvas = canvas; _shader = shader; _exiting = exiting; canvas.TreeExiting += exiting; }
        internal void Begin(Vector2 scale) { _used = 0; _scale = scale; }
        internal void Draw(MapFontSource source, string value, MapFontProfile profile, Vector2 baseline, Color tint)
        {
            if (_used == _slots.Count) _slots.Add(new(_canvas.GetCanvasItem(), _shader, _used));
            _slots[_used++].Draw(source, value, profile, baseline, tint, _scale,
                _canvas.GetViewportTransform() * _canvas.GetGlobalTransform());
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
        private readonly Rid _copy, _draw;
        private readonly ShaderMaterial _material;
        private ImageTexture[] _textures = [];
        private string? _value;
        private MapFontProfile? _profile;
        private MapTextCache? _cache;
        internal TextSlot(Rid parent, Shader shader, int index)
        {
            _copy = RenderingServer.CanvasItemCreate(); _draw = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(_copy, parent); RenderingServer.CanvasItemSetParent(_draw, parent);
            // Text belongs to the parent's drawing, before its existing child
            // layers (Army). Keep each copy/draw pair consecutive and distinct
            // from the nonnegative scene-tree child indexes.
            RenderingServer.CanvasItemSetDrawIndex(_copy, int.MinValue + index * 2);
            RenderingServer.CanvasItemSetDrawIndex(_draw, int.MinValue + index * 2 + 1);
            _material = new() { Shader = shader };
            RenderingServer.CanvasItemSetMaterial(_draw, _material.GetRid());
        }
        internal void Draw(MapFontSource source, string value, MapFontProfile profile,
            Vector2 baseline, Color tint, Vector2 scale, Transform2D parentTransform)
        {
            if (_value != value || _profile != profile)
            {
                _cache = MapTextCache.Build(source, value, profile);
                foreach (var texture in _textures) texture.Dispose();
                _textures = new[] { _cache.Offset, _cache.Stroke, _cache.Body }.Select(pixels =>
                {
                    using var image = Image.CreateFromData(_cache.Width, _cache.Height, false,
                        Image.Format.Rgbaf, MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray());
                    return ImageTexture.CreateFromImage(image);
                }).ToArray();
                _material.SetShaderParameter("offset_cache", _textures[0]);
                _material.SetShaderParameter("stroke_cache", _textures[1]);
                _material.SetShaderParameter("body_cache", _textures[2]);
                _value = value; _profile = profile;
            }
            _material.SetShaderParameter("node_tint", new Vector4(Cpu(tint.R), Cpu(tint.G), Cpu(tint.B), tint.A));
            var rect = new Rect2(baseline + new Vector2(_cache!.Left, _cache.Top), new(_cache.Width, _cache.Height));
            var transform = Transform2D.Identity.Scaled(scale);
            RenderingServer.CanvasItemSetTransform(_copy, transform);
            RenderingServer.CanvasItemSetTransform(_draw, transform);
            // Automatic screen copying only once would restore stale pixels
            // behind later labels. Refresh the local background before each.
            RenderingServer.CanvasItemSetCopyToBackbuffer(_copy, true, parentTransform * (transform * rect.Grow(1)));
            RenderingServer.CanvasItemSetVisible(_copy, true); RenderingServer.CanvasItemSetVisible(_draw, true);
            RenderingServer.CanvasItemClear(_draw);
            RenderingServer.CanvasItemAddTextureRect(_draw, rect, _textures[2].GetRid());
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
        vec4 over_cache(vec4 dst, vec4 src) {
            float a = src.a * node_tint.a;
            // Ordinary text node common00554400: SRC_ALPHA/INV_SRC_ALPHA
            // applies to both RGB and A. Cache RGB remains straight.
            return vec4(src.rgb * node_tint.rgb * a + dst.rgb * (1.0 - a), a * a + dst.a * (1.0 - a));
        }
        void fragment() {
            vec4 offset = texture(offset_cache, UV);
            vec4 stroke = texture(stroke_cache, UV);
            vec4 body = texture(body_cache, UV);
            if (max(max(offset.a, stroke.a), body.a) * node_tint.a == 0.0) discard;
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
