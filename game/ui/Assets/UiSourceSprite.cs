using Godot;
using San14.Data;

/// <summary>Original linear UI blending over the preceding Canvas RGB.
/// Clipped buttons reproduce the mask and both base draws locally, because
/// Canvas screen copies do not preserve the destination alpha channel.</summary>
internal partial class UiSourceSprite : Control
{
    internal enum Blend { Ordinary, ClippedButton, ThreeTexturePortrait, TwoTexturePortrait, SourceAlphaDestinationAlpha, GaugeUvMask, Additive }
    private Rid _copy, _draw;
    private ShaderMaterial? _material;
    private Texture2D? _texture;
    private MapCornerColors? _colors;
    private bool _flipU, _flipV;
    private Rect2? _samplingRectangle;

    // The second component rotates its UVs, while the pane keeps its authored
    // geometry. The source builds this matrix from a float32 half-angle.
    internal void SetGaugeUvRotation(float degrees)
    {
        float half = degrees * (MathF.PI / 360f);
        float s = MathF.Sin(half), c = MathF.Cos(half);
        _material!.SetShaderParameter("component_rotation", new Vector4(1 - 2 * s * s,
            -2 * s * c, 2 * s * c, 1 - 2 * s * s));
        QueueRedraw();
    }

    internal void SetGaugeComponent(Texture2D texture)
    {
        Rect2 uv = new(Vector2.Zero, Vector2.One);
        if (texture is AtlasTexture atlas)
        {
            texture = atlas.Atlas;
            uv = new(atlas.Region.Position / texture.GetSize(), atlas.Region.Size / texture.GetSize());
        }
        _material!.SetShaderParameter("first_image", texture);
        _material.SetShaderParameter("first_uv", new Vector4(uv.Position.X, uv.Position.Y, uv.Size.X, uv.Size.Y));
    }

    internal void SetSamplingRectangle(Rect2 rectangle)
    {
        if(!float.IsFinite(rectangle.Position.X)||!float.IsFinite(rectangle.Position.Y)||
            !float.IsFinite(rectangle.Size.X)||!float.IsFinite(rectangle.Size.Y)||
            rectangle.Position.X<0||rectangle.Position.Y<0||rectangle.Size.X<0||rectangle.Size.Y<0||
            rectangle.End.X>1||rectangle.End.Y>1)
            throw new InvalidDataException("Source sprite sampling rectangle is outside its declared sprite.");
        _samplingRectangle=rectangle;QueueRedraw();
    }

    internal void SetUvFlip(bool horizontal,bool vertical)
    {_flipU=horizontal;_flipV=vertical;QueueRedraw();}

    internal void Configure(Shader shader, Texture2D texture, Blend blend, MapCornerColors? colors = null)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetNotifyTransform(true);
        _material = new ShaderMaterial { Shader = shader };
        _material.SetShaderParameter("blend_kind", (int)blend);
        SetSource(texture, colors);
    }

    internal void SetSource(Texture2D texture, MapCornerColors? colors = null)
    {
        _texture = texture;
        _colors = colors;
        QueueRedraw();
    }

    internal void SetColors(MapCornerColors colors)
    {
        _colors = colors;
        QueueRedraw();
    }

    internal void SetClipping(Texture2D first, Texture2D mask, Rect2 maskRectangle,
        Texture2D? secondMask = null, Rect2 secondMaskRectangle = default)
    {
        Bind("first", first);
        Bind("mask", mask);
        _material!.SetShaderParameter("mask_rectangle", new Vector4(maskRectangle.Position.X,
            maskRectangle.Position.Y, maskRectangle.Size.X, maskRectangle.Size.Y));
        _material.SetShaderParameter("has_second_mask", secondMask is not null);
        if (secondMask is not null)
        {
            Bind("second_mask", secondMask);
            _material.SetShaderParameter("second_mask_rectangle", new Vector4(secondMaskRectangle.Position.X,
                secondMaskRectangle.Position.Y, secondMaskRectangle.Size.X, secondMaskRectangle.Size.Y));
        }
        void Bind(string name, Texture2D texture)
        {
            Rect2 uv = new(Vector2.Zero, Vector2.One);
            if (texture is AtlasTexture atlas)
            {
                texture = atlas.Atlas;
                uv = new(atlas.Region.Position / texture.GetSize(), atlas.Region.Size / texture.GetSize());
            }
            _material!.SetShaderParameter(name + "_image", texture);
            _material.SetShaderParameter(name + "_uv", new Vector4(uv.Position.X, uv.Position.Y, uv.Size.X, uv.Size.Y));
        }
        QueueRedraw();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTransformChanged || what == NotificationResized) QueueRedraw();
    }

    public override void _Draw()
    {
        if (_texture is null || _material is null) return;
        if (!_draw.IsValid)
        {
            _copy = RenderingServer.CanvasItemCreate();
            _draw = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(_copy, GetCanvasItem());
            RenderingServer.CanvasItemSetParent(_draw, GetCanvasItem());
            RenderingServer.CanvasItemSetDrawIndex(_copy, 0);
            RenderingServer.CanvasItemSetDrawIndex(_draw, 1);
            RenderingServer.CanvasItemSetMaterial(_draw, _material.GetRid());
            RenderingServer.CanvasItemSetDefaultTextureFilter(_draw, RenderingServer.CanvasItemTextureFilter.Linear);
        }
        Rect2 rectangle = new(Vector2.Zero, Size);
        _material.SetShaderParameter("draw_size", Size);
        // The copy RID inherits this node's transform. Godot transforms its
        // local copy rectangle into the target viewport during culling.
        RenderingServer.CanvasItemSetCopyToBackbuffer(_copy, true, rectangle.Grow(1));
        Texture2D texture = _texture;
        Rect2 uv = new(Vector2.Zero, Vector2.One);
        if (texture is AtlasTexture atlas)
        {
            texture = atlas.Atlas;
            uv = new(atlas.Region.Position / texture.GetSize(), atlas.Region.Size / texture.GetSize());
        }
        if(_samplingRectangle is { } sample)
            uv=new(uv.Position+uv.Size*sample.Position,uv.Size*sample.Size);
        Color[] colors = _colors is MapCornerColors palette
            ? [ColorOf(palette.LT), ColorOf(palette.RT), ColorOf(palette.RB), ColorOf(palette.LB)]
            : [Colors.White, Colors.White, Colors.White, Colors.White];
        float left=_flipU?uv.End.X:uv.Position.X,right=_flipU?uv.Position.X:uv.End.X;
        float top=_flipV?uv.End.Y:uv.Position.Y,bottom=_flipV?uv.Position.Y:uv.End.Y;
        RenderingServer.CanvasItemClear(_draw);
        RenderingServer.CanvasItemAddPolygon(_draw,
            new Vector2[] { Vector2.Zero, new(Size.X, 0), Size, new(0, Size.Y) }, colors,
            new Vector2[] { new(left,top),new(right,top),new(right,bottom),new(left,bottom) }, texture.GetRid());
    }

    private static Color ColorOf(AreaMarkerColor value) =>
        new(value.Red / 255f, value.Green / 255f, value.Blue / 255f, value.Alpha / 255f);

    internal static Shader CreateShader() => new()
    {
        ResourceName = "OriginalUiSpriteBlend",
        Code = """
        shader_type canvas_item;
        render_mode unshaded, blend_disabled;
        uniform sampler2D screen_texture : hint_screen_texture, filter_nearest, repeat_disable;
        uniform sampler2D first_image : filter_linear, repeat_disable;
        uniform sampler2D mask_image : filter_linear, repeat_disable;
        uniform sampler2D second_mask_image : filter_linear, repeat_disable;
        uniform vec4 first_uv = vec4(0.0, 0.0, 1.0, 1.0);
        uniform vec4 mask_uv = vec4(0.0, 0.0, 1.0, 1.0);
        uniform vec4 mask_rectangle;
        uniform vec4 second_mask_uv;
        uniform vec4 second_mask_rectangle;
        uniform bool has_second_mask = false;
        uniform vec2 draw_size;
        uniform int blend_kind = 0;
        uniform vec4 component_rotation = vec4(1.0, 0.0, 0.0, 1.0);
        varying vec4 sprite_tint;
        varying vec2 local_position;
        void vertex() { sprite_tint = COLOR; local_position = VERTEX; }
        vec3 decode_srgb(vec3 v) {
            return mix(pow((v + 0.055) / 1.055, vec3(2.4)), v / 12.92, lessThanEqual(v, vec3(0.04045)));
        }
        vec3 encode_srgb(vec3 v) {
            return mix(1.055 * pow(max(v, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055,
                v * 12.92, lessThanEqual(v, vec3(0.0031308)));
        }
        void fragment() {
            vec4 sample_color = texture(TEXTURE, UV);
            // The source image was decoded per texel before bilinear sampling.
            // These sprite nodes use compare=7: even alpha zero must write.
            vec4 src = sample_color * sprite_tint;
            vec4 dst = texture(screen_texture, SCREEN_UV);
            vec3 rgb = decode_srgb(dst.rgb);
            float alpha;
            if (blend_kind == 1) {
                vec2 mask_point = (local_position - mask_rectangle.xy) / mask_rectangle.zw;
                float coverage = texture(mask_image, mask_uv.xy + mask_point * mask_uv.zw).a;
                coverage = floor(clamp(coverage, 0.0, 1.0) * 255.0 + 0.5) / 255.0;
                if (has_second_mask) {
                    vec2 point = (local_position - second_mask_rectangle.xy) / second_mask_rectangle.zw;
                    coverage *= texture(second_mask_image, second_mask_uv.xy + point * second_mask_uv.zw).a;
                    coverage = floor(clamp(coverage, 0.0, 1.0) * 255.0 + 0.5) / 255.0;
                }
                float first_alpha = texture(first_image, first_uv.xy + local_position / draw_size * first_uv.zw).a;
                float prepared = first_alpha * coverage + coverage * (1.0 - coverage);
                prepared = floor(clamp(prepared, 0.0, 1.0) * 255.0 + 0.5) / 255.0;
                rgb = src.rgb * prepared + rgb * (1.0 - prepared);
                alpha = src.a * prepared;
            } else if (blend_kind == 2 || blend_kind == 3) {
                vec2 first_point = (local_position - mask_rectangle.xy) / mask_rectangle.zw;
                vec4 first_mask = texture(mask_image, mask_uv.xy + first_point * mask_uv.zw);
                vec3 portrait_rgb = src.rgb * first_mask.rgb;
                float portrait_alpha = min(sample_color.a, first_mask.a);
                if (blend_kind == 2) {
                    vec2 second_point = (local_position - second_mask_rectangle.xy) / second_mask_rectangle.zw;
                    vec4 second_mask = texture(second_mask_image, second_mask_uv.xy + second_point * second_mask_uv.zw);
                    portrait_rgb *= second_mask.rgb;
                    portrait_alpha = max(portrait_alpha, second_mask.a);
                }
                portrait_alpha *= sprite_tint.a;
                rgb = portrait_rgb * portrait_alpha + rgb * (1.0 - portrait_alpha);
                alpha = portrait_alpha * portrait_alpha + dst.a * (1.0 - portrait_alpha);
            } else if (blend_kind == 5) {
                vec2 p = local_position / draw_size - vec2(0.5);
                vec2 rotated = vec2(dot(component_rotation.xy, p), dot(component_rotation.zw, p)) + vec2(0.5);
                vec4 second = texture(first_image, first_uv.xy + clamp(rotated, vec2(0.0), vec2(1.0)) * first_uv.zw);
                // Original PS19 component mode2/1: RGB lerp and alpha min,
                // followed by the pane's vertex tint, in a single draw.
                src = vec4(mix(sample_color.rgb, second.rgb, second.a), min(sample_color.a, second.a)) * sprite_tint;
                rgb = src.rgb * src.a + rgb * (1.0 - src.a);
                alpha = src.a * src.a + dst.a * (1.0 - src.a);
            } else if (blend_kind == 4) {
                // Ordinary full standing portrait node 00058400: RGB uses
                // SRC_ALPHA/INV_SRC_ALPHA; alpha uses DEST_ALPHA/ZERO.
                rgb = src.rgb * src.a + rgb * (1.0 - src.a);
                alpha = src.a * dst.a;
            } else if (blend_kind == 6) {
                // Original common00114400 uses SRC_ALPHA/ONE for RGB and alpha.
                rgb = src.rgb * src.a + rgb;
                alpha = src.a * src.a + dst.a;
            } else {
                rgb = src.rgb * src.a + rgb * (1.0 - src.a);
                alpha = src.a * src.a + dst.a * (1.0 - src.a);
            }
            COLOR = vec4(encode_srgb(rgb), alpha);
        }
        """
    };

    public override void _ExitTree()
    {
        if (_copy.IsValid) RenderingServer.FreeRid(_copy);
        if (_draw.IsValid) RenderingServer.FreeRid(_draw);
        _copy = _draw = default;
        _material?.Dispose();
        _material = null;
    }
}
