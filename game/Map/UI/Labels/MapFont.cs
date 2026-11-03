using System.Runtime.InteropServices;
using Godot;
using San14.Data;

/// <summary>Original G1N glyphs, independent text caches and a local linear
/// compositor over the encoded native Main image. No system font is used.</summary>
internal sealed class MapFont : IDisposable
{
    private readonly Dictionary<ulong, DrawOwner> _owners = [];
    private readonly Shader _shader = CreateShader();
    internal readonly record struct PreparedTextKey(string Value, MapFontProfile Profile, string? InlineColourSource = null);
    // Source is diagnostic provenance. This font owner and the remaining
    // complete profile already identify the rendered pixels. Keep the
    // caller's profile intact while sharing equal text across UI layouts.
    private static PreparedTextKey TextKey(string value, MapFontProfile profile, string? inlineColourSource = null) =>
        new(value, profile.Source.Length == 0 ? profile : profile with { Source = string.Empty }, inlineColourSource);
    private readonly IReadOnlyDictionary<PreparedTextKey, MapTextCache>? _preparedText;
    // Finite prepared captions remain resident with this resource owner.
    // Other text is retained only by live slots, so scrolling numbers cannot
    // grow a historical cache indefinitely.
    private readonly Dictionary<PreparedTextKey, TextTextures> _textTextures = [];
    private sealed class TextTextures(PreparedTextKey? key, MapTextCache cache, ImageTexture[] textures, bool resident)
    {
        internal PreparedTextKey? Key { get; } = key;
        internal MapTextCache Cache { get; } = cache;
        internal ImageTexture[] Textures { get; } = textures;
        internal bool Resident { get; } = resident;
        internal int Users = 1;
    }
    private TextTextures AcquireText(string value, MapFontProfile profile, IReadOnlyList<uint?>? glyphColours,
        IReadOnlyList<MapTextPreset?>? glyphBodyPresets = null, string? inlineColourSource = null)
    {
        PreparedTextKey? key = glyphBodyPresets is null && (glyphColours is null || inlineColourSource is not null)
            ? TextKey(value, profile, inlineColourSource) : null;
        if (key is {} sharedKey && _textTextures.TryGetValue(sharedKey, out var shared))
        { shared.Users++; return shared; }
        MapTextCache cache = key is {} preparedKey && _preparedText is {} prepared &&
            prepared.TryGetValue(preparedKey, out var pixels) ? pixels : MapTextCache.Build(Source, value, profile, glyphColours, glyphBodyPresets);
        var textures = new List<ImageTexture>(3);
        try
        {
            foreach (var channel in new[] { cache.Offset, cache.Stroke, cache.Body })
            {
                using var image = Image.CreateFromData(cache.Width, cache.Height, false,
                    Image.Format.Rgbaf, MemoryMarshal.AsBytes(channel.AsSpan()).ToArray());
                textures.Add(ImageTexture.CreateFromImage(image));
            }
            var result = new TextTextures(key, cache, textures.ToArray(),
                key is {} staticKey && _preparedText?.ContainsKey(staticKey) == true);
            if (key is {} addedKey) _textTextures.Add(addedKey, result);
            return result;
        }
        catch
        { foreach (var texture in textures) texture.Dispose(); throw; }
    }
    private void ReleaseText(TextTextures text)
    {
        if (--text.Users != 0 || text.Resident) return;
        if (text.Key is {} key) _textTextures.Remove(key);
        foreach (var texture in text.Textures) texture.Dispose();
    }
    // The finite prepared captions are static. Their textures remain owned by
    // this resource generation even when no page currently draws them.
    internal void MaterializePreparedText()
    {
        if (_preparedText is null) return;
        foreach (var entry in _preparedText)
        {
            var text = AcquireText(entry.Key.Value, entry.Key.Profile, null, inlineColourSource: entry.Key.InlineColourSource);
            ReleaseText(text);
        }
    }

    internal MapFontSource Source { get; }
    internal MapFont(MapFontSource source, IReadOnlyDictionary<PreparedTextKey, MapTextCache>? preparedText = null)
    { Source = source; _preparedText = preparedText; }

    // Pure CPU work for a page-owned font source, before its one-way handoff.
    internal static void PrepareText(MapFontSource source, string value, MapFontProfile profile, float width,
        IDictionary<PreparedTextKey, MapTextCache> caches, IReadOnlyList<uint?>? glyphColours = null,
        string? inlineColourSource = null)
    {
        if (value.Length == 0) return;
        profile.ValidateSingleLine();
        var fitted = source.FitToWidth(value, profile, width);
        var key = TextKey(value, fitted, inlineColourSource);
        if (!caches.ContainsKey(key)) caches.Add(key, MapTextCache.Build(source, value, fitted, glyphColours));
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

    // Existing glyph commands remain parented to this actual CanvasItem.
    // Moving it changes their inherited placement; only the viewport-aligned
    // backbuffer copy rectangles/inverse transforms need to be refreshed.
    internal void RefreshDrawingPlacement(CanvasItem canvas)
    {
        if(_owners.TryGetValue(canvas.GetInstanceId(),out var owner))owner.RefreshPlacement();
    }

    private DrawOwner Owner(CanvasItem canvas)
    {
        ulong id = canvas.GetInstanceId();
        if (!_owners.TryGetValue(id, out var owner))
        {
            owner = new(canvas, _shader, () => { _owners.Remove(id, out var old); old?.Dispose(); }, this);
            _owners.Add(id, owner);
        }
        return owner;
    }
    internal void Draw(CanvasItem canvas, string value, Vector2 baseline,
        MapFontProfile profile, HorizontalAlignment alignment = HorizontalAlignment.Left,
        float width = -1, Color? modulate = null,IReadOnlyList<uint?>? glyphColours=null, IReadOnlyList<MapTextPreset?>? glyphBodyPresets=null, string? inlineColourSource=null)
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
        _owners[canvas.GetInstanceId()].Draw(Source, value, profile, new(x, baseline.Y), modulate ?? Colors.White,glyphColours,glyphBodyPresets,inlineColourSource);
    }

    internal sealed class DrawOwner : IDisposable
    {
        private readonly CanvasItem _canvas;
        private readonly Shader _shader;
        private readonly Action _exiting;
        private readonly MapFont _font;
        private readonly List<TextSlot> _slots = [];
        private Vector2 _scale;
        private int _used;
        private bool _disposed;
        private uint _visibilityLayer;
        internal int ActiveCount => _used;
        internal DrawOwner(CanvasItem canvas, Shader shader, Action exiting, MapFont font)
        { _canvas = canvas; _shader = shader; _exiting = exiting; _font = font; _visibilityLayer = canvas.VisibilityLayer; canvas.TreeExiting += exiting; }
        internal void SetVisibilityLayer(uint layer)
        {
            _visibilityLayer = layer;
            foreach (var slot in _slots) slot.SetVisibilityLayer(layer);
        }
        internal void Begin(Vector2 scale) { _used = 0; _scale = scale; }
        internal void Draw(MapFontSource source, string value, MapFontProfile profile, Vector2 baseline, Color tint,
            IReadOnlyList<uint?>? glyphColours=null, IReadOnlyList<MapTextPreset?>? glyphBodyPresets=null, string? inlineColourSource=null)
        {
            if (_used == _slots.Count) _slots.Add(new(_canvas, _shader, _used, _visibilityLayer, _font));
            _slots[_used++].Draw(source, value, profile, baseline, tint, _scale, glyphColours, glyphBodyPresets,inlineColourSource);
        }
        internal void End()
        {
            for (int i = _used; i < _slots.Count; i++) _slots[i].Hide();
        }
        internal void RefreshPlacement()
        {
            for(int i=0;i<_used;i++)_slots[i].RefreshPlacement();
        }
        public void Dispose()
        {
            if (_disposed) return;
            // A configured Canvas may be freed without entering the tree.
            // Its local draw slots still belong to this font generation.
            if (GodotObject.IsInstanceValid(_canvas)) _canvas.TreeExiting -= _exiting;
            foreach (var slot in _slots) slot.Dispose();
            _slots.Clear();
            _disposed = true;
        }
    }

    private sealed class TextSlot : IDisposable
    {
        private readonly CanvasItem _owner;
        private readonly Rid _copy, _draw;
        private readonly ShaderMaterial _material;
        private TextTextures? _text;
        private List<TextTextures>? _pendingRelease;
        private string? _value;
        private MapFontProfile? _profile;
        private IReadOnlyList<uint?>? _glyphColours;
        private IReadOnlyList<MapTextPreset?>? _glyphBodyPresets;
        private string? _inlineColourSource;
        private MapTextCache? _cache;
        private Rect2 _copyRectangle;
        private Transform2D _drawingTransform;
        private readonly MapFont _font;
        internal TextSlot(CanvasItem owner, Shader shader, int index, uint visibilityLayer, MapFont font)
        {
            _owner = owner; _font = font;
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
            Vector2 baseline, Color tint, Vector2 scale, IReadOnlyList<uint?>? glyphColours=null, IReadOnlyList<MapTextPreset?>? glyphBodyPresets=null, string? inlineColourSource=null)
        {
            if (_value != value || _profile != profile || !ReferenceEquals(_glyphColours,glyphColours) || !ReferenceEquals(_glyphBodyPresets,glyphBodyPresets) || _inlineColourSource != inlineColourSource)
            {
                // A failed partial Canvas/material update may still reference
                // either resource. Retain both until a completed replacement
                // or this slot's own RID/material retirement.
                if (_text is not null)
                {
                    _pendingRelease ??= [];
                    _pendingRelease.EnsureCapacity(_pendingRelease.Count + 1);
                }
                var next = _font.AcquireText(value, profile, glyphColours, glyphBodyPresets,inlineColourSource);
                if (_text is {} previous) _pendingRelease!.Add(previous);
                _text = next; _cache = next.Cache;
                _value = null; // A partial material bind must retry even if the next requested text is the previous value.
                _material.SetShaderParameter("offset_cache", next.Textures[0]);
                _material.SetShaderParameter("stroke_cache", next.Textures[1]);
                _material.SetShaderParameter("body_cache", next.Textures[2]);
                _value = value; _profile = profile;_glyphColours=glyphColours;_glyphBodyPresets=glyphBodyPresets;_inlineColourSource=inlineColourSource;
            }
            _material.SetShaderParameter("node_tint", new Vector4(Cpu(tint.R), Cpu(tint.G), Cpu(tint.B), tint.A));
            var rect = new Rect2(baseline + new Vector2(_cache!.Left, _cache.Top), new(_cache.Width, _cache.Height));
            var transform = Transform2D.Identity.Scaled(scale);
            RenderingServer.CanvasItemSetTransform(_draw, transform);
            _copyRectangle=rect.Grow(1);_drawingTransform=transform;
            RefreshPlacement();
            RenderingServer.CanvasItemSetVisible(_copy, true); RenderingServer.CanvasItemSetVisible(_draw, true);
            RenderingServer.CanvasItemClear(_draw);
            RenderingServer.CanvasItemAddTextureRect(_draw, rect, _text!.Textures[2].GetRid());
            // Both the material and the retained Canvas command must release
            // the old textures before their managed/native owners are retired.
            ReleasePendingText();
        }
        private void ReleasePendingText()
        {
            if (_pendingRelease is not {} pending) return;
            foreach (var text in pending) _font.ReleaseText(text);
            pending.Clear();
        }
        private static float Cpu(float encoded) => MathF.Truncate(MathF.Pow(encoded, 2.2f) * 255f) / 255f;
        internal void RefreshPlacement()=>UiSourceBackBufferCopy.Configure(_owner,_copy,_copyRectangle,_drawingTransform);
        internal void Hide()
        { RenderingServer.CanvasItemSetVisible(_copy, false); RenderingServer.CanvasItemSetVisible(_draw, false); }
        public void Dispose()
        {
            RenderingServer.FreeRid(_copy); RenderingServer.FreeRid(_draw);
            _material.Dispose();
            if (_text is {} text) { _text = null; _font.ReleaseText(text); }
            ReleasePendingText();
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
        _owners.Clear();
        foreach (var text in _textTextures.Values)
            foreach (var texture in text.Textures) texture.Dispose();
        _textTextures.Clear(); _shader.Dispose();
    }
}
