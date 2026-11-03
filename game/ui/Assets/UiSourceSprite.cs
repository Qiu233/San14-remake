using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Original linear UI blending over the preceding Canvas RGB.
/// Clipped buttons reproduce the mask and both base draws locally, because
/// Canvas screen copies do not preserve the destination alpha channel.</summary>
internal partial class UiSourceSprite : Control, IUiSourceDrawing
{
    internal enum Blend { Ordinary, ClippedButton, ThreeTexturePortrait, TwoTexturePortrait, SourceAlphaDestinationAlpha, GaugeUvMask, Additive, AlphaMask, UniqueRulerPortrait, TranslatedAlphaMask, PoemScroll, DestinationAlphaAdditive }
    private Rid _copy, _draw;
    private ShaderMaterial? _material;
    private Texture2D? _texture;
    private MapCornerColors? _colors;
    private uint _materialColorScale = 0x00FFFFFF;
    private uint _materialColorOffset;
    private bool _flipU, _flipV;
    private Rect2? _samplingRectangle;
    private bool _addressedSampling;
    private bool _nativePortrait, _portraitWrapU, _portraitWrapV;
    private uint? _drawingVisibilityLayer;

    // A prefix capture needs structural ancestors in several layers, while
    // this copy/draw pair belongs only to its actual ordered drawing segment.
    public void SetDrawingVisibilityLayer(uint layer)
    {
        _drawingVisibilityLayer = layer;
        if (_copy.IsValid) RenderingServer.CanvasItemSetVisibilityLayer(_copy, layer);
        if (_draw.IsValid) RenderingServer.CanvasItemSetVisibilityLayer(_draw, layer);
    }

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

    internal void SetMaskUvRotation(float degrees)
    {
        SetGaugeUvRotation(degrees);
        _material!.SetShaderParameter("has_component_rotation",true);
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

    internal void SetMaskUvOffset(Vector2 offset)
    {
        _material!.SetShaderParameter("mask_uv_offset",offset);
        QueueRedraw();
    }

    internal void SetMaskAlphas(float glyph, float clip)
    {
        _material!.SetShaderParameter("mask_alpha", glyph);
        _material.SetShaderParameter("second_mask_alpha", clip);
    }

    internal void SetMaskRectangles(Rect2 glyph, Rect2 clip)
    {
        _material!.SetShaderParameter("mask_rectangle", new Vector4(glyph.Position.X,
            glyph.Position.Y, glyph.Size.X, glyph.Size.Y));
        _material.SetShaderParameter("second_mask_rectangle", new Vector4(clip.Position.X,
            clip.Position.Y, clip.Size.X, clip.Size.Y));
    }

    internal void SetEncodedPortraitSampling(San14.Data.Formats.G1TextureImage image)
    {
        var sampler=G1TextureSamplerReader.ReadRetainedSource(image);
        _nativePortrait=true;_portraitWrapU=sampler.AddressU==1;_portraitWrapV=sampler.AddressV==1;
        _material!.SetShaderParameter("native_portrait",true);
        _material.SetShaderParameter("portrait_wrap",new Vector2(_portraitWrapU?1:0,_portraitWrapV?1:0));
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

    internal void SetAddressedSampling(Rect2 rectangle,uint addressU,uint addressV)
    {
        if(!float.IsFinite(rectangle.Position.X)||!float.IsFinite(rectangle.Position.Y)||
            !float.IsFinite(rectangle.Size.X)||!float.IsFinite(rectangle.Size.Y)||
            rectangle.Size.X<=0||rectangle.Size.Y<=0||addressU is not (1 or 2 or 3)||addressV is not (1 or 2 or 3))
            throw new InvalidDataException("Local source addressing requires a finite rectangle and supported source sampler.");
        _material!.SetShaderParameter("addressed_sampling",true);
        _addressedSampling=true;
        _material.SetShaderParameter("sampling_uv",new Vector4(rectangle.Position.X,rectangle.Position.Y,rectangle.Size.X,rectangle.Size.Y));
        _material.SetShaderParameter("source_address",new Vector2(addressU,addressV));
        QueueRedraw();
    }

    internal void SetUvFlip(bool horizontal,bool vertical)
    {_flipU=horizontal;_flipV=vertical;QueueRedraw();}

    internal void Configure(Shader shader, Texture2D texture, Blend blend, MapCornerColors? colors = null)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetNotifyTransform(true);
        _material = new ShaderMaterial { Shader = shader };
        _addressedSampling=false;
        _nativePortrait=_portraitWrapU=_portraitWrapV=false;
        _materialColorScale = 0x00FFFFFF;
        _materialColorOffset = 0;
        _material.SetShaderParameter("material_color_scale", Vector3.One);
        _material.SetShaderParameter("material_color_offset", Vector3.Zero);
        _material.SetShaderParameter("material_saturation", 1f);
        _material.SetShaderParameter("blend_kind", (int)blend);
        SetSource(texture, colors);
    }

    internal void SetSource(Texture2D texture, MapCornerColors? colors = null)
    {
        _texture = texture;
        _colors = colors;
        QueueRedraw();
    }

    // Both fresh Art and retained button artwork consume the same resolved
    // parts. Shader programs and blending arithmetic remain unchanged.
    internal void ConfigureArtwork(Shader shader, UiSourceArtPart part)
    {
        Configure(shader, part.Texture, part.Blend, part.Colors);
        if (part.Material is { } material)
        {
            SetMaterialColorScale(material.ColorScale);
            SetMaterialColorOffset(material.ColorOffset);
            SetMaterialSaturation(material.Saturation);
            SetSourceAlpha(material.Alpha);
        }
        ApplyArtworkSampling(part);
    }

    internal void ApplyArtwork(UiSourceArtPart part)
    {
        SetSource(part.Texture, part.Colors);
        _material!.SetShaderParameter("blend_kind", (int)part.Blend);
        SetMaterialColorScale(part.Material?.ColorScale ?? 0x00FFFFFF);
        SetMaterialColorOffset(part.Material?.ColorOffset ?? 0);
        SetMaterialSaturation(part.Material?.Saturation ?? 1f);
        SetSourceAlpha(part.Material?.Alpha ?? 255);
        // Clear the previous state's addressing and constant-UV branch before
        // applying this part. A normal source part cannot inherit either.
        _samplingRectangle = null;
        _addressedSampling = false;
        _material.SetShaderParameter("addressed_sampling", false);
        ApplyArtworkSampling(part);
        // State changes can occur in the button's Canvas redraw callback,
        // after ordinary redraw scheduling. Record pure drawing commands now
        // so the existing render frame sees the new state immediately.
        if (IsInsideTree()) RecordDrawing();
    }

    private void ApplyArtworkSampling(UiSourceArtPart part)
    {
        if (part.AddressedRectangle is { } addressed)
            SetAddressedSampling(addressed, part.Addressing.AddressU, part.Addressing.AddressV);
        if (part.SamplingRectangle is { } sample) SetSamplingRectangle(sample);
        SetUvFlip(part.FlipU, part.FlipV);
    }

    //The layout material has a separate RGB scale, independent of the
    //four vertex colours and alpha. Its ordinary Gamma1 path squares each
    //normalised byte before the pixel shader applies the white interpolation.
    internal void SetMaterialColorScale(uint packed)
    {
        uint rgb = packed & 0x00FFFFFF;
        if (_materialColorScale == rgb) return;
        var scale = new Vector3((byte)rgb / 255f, (byte)(rgb >> 8) / 255f,
            (byte)(rgb >> 16) / 255f);
        _material!.SetShaderParameter("material_color_scale", scale * scale);
        _materialColorScale = rgb;
    }

    //AC4D80 derives the black interpolation endpoint from low24 RGB;
    //the packed alpha byte does not enable or disable that endpoint.
    internal void SetMaterialColorOffset(uint packed)
    {
        uint rgb=packed&0x00FFFFFF;
        if(_materialColorOffset==rgb)return;
        var offset=new Vector3((byte)rgb/255f,(byte)(rgb>>8)/255f,(byte)(rgb>>16)/255f);
        _material!.SetShaderParameter("material_color_offset",offset*offset);
        _materialColorOffset=rgb;
    }
    internal void SetMaterialSaturation(float saturation)=>
        _material!.SetShaderParameter("material_saturation",saturation);

    // The pane alpha is separate from vertex tint and material RGB.
    internal void SetSourceAlpha(byte alpha) =>
        _material!.SetShaderParameter("pane_alpha", alpha / 255f);

    internal void ResetColors()
    {
        if(_colors is null)return;
        _colors=null;QueueRedraw();
    }

    internal void SetColors(MapCornerColors colors)
    {
        if(_colors==colors)return;
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
        if (what == NotificationResized) QueueRedraw();
        else if ((what == NotificationTransformChanged || what == NotificationVisibilityChanged) &&
            _copy.IsValid && IsVisibleInTree())
            RefreshDrawingPlacement();
    }

    public override void _Draw() => RecordDrawing();

    private void RecordDrawing()
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
            SetDrawingVisibilityLayer(_drawingVisibilityLayer ?? VisibilityLayer);
            RenderingServer.CanvasItemSetMaterial(_draw, _material.GetRid());
            RenderingServer.CanvasItemSetDefaultTextureFilter(_draw, RenderingServer.CanvasItemTextureFilter.Linear);
        }
        if(_nativePortrait)RenderingServer.CanvasItemSetDefaultTextureRepeat(_draw,(_portraitWrapU||_portraitWrapV)
            ?RenderingServer.CanvasItemTextureRepeat.Enabled:RenderingServer.CanvasItemTextureRepeat.Disabled);
        _material.SetShaderParameter("draw_size", Size);
        RefreshDrawingPlacement();
        Texture2D texture = _texture;
        Rect2 uv = new(Vector2.Zero, Vector2.One);
        if (texture is AtlasTexture atlas)
        {
            texture = atlas.Atlas;
            uv = new(atlas.Region.Position / texture.GetSize(), atlas.Region.Size / texture.GetSize());
        }
        if(_samplingRectangle is { } sample)
            uv=new(uv.Position+uv.Size*sample.Position,uv.Size*sample.Size);
        if(_addressedSampling)
            _material.SetShaderParameter("source_region",new Vector4(uv.Position.X,uv.Position.Y,uv.Size.X,uv.Size.Y));
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

    // Moving the retained draw child already moves its local vertices. Only
    // the preceding viewport-aligned copy depends on the global transform.
    private void RefreshDrawingPlacement() =>
        UiSourceBackBufferCopy.Configure(this, _copy, new Rect2(Vector2.Zero, Size).Grow(1));

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
        uniform vec2 mask_uv_offset = vec2(0.0);
        uniform float mask_alpha = 1.0;
        uniform float second_mask_alpha = 1.0;
        uniform vec4 second_mask_uv;
        uniform vec4 second_mask_rectangle;
        uniform bool has_second_mask = false;
        uniform vec2 draw_size;
        uniform int blend_kind = 0;
        uniform vec3 material_color_scale = vec3(1.0);
        uniform vec3 material_color_offset = vec3(0.0);
        uniform float material_saturation = 1.0;
        uniform float pane_alpha = 1.0;
        uniform vec4 component_rotation = vec4(1.0, 0.0, 0.0, 1.0);
        uniform bool has_component_rotation = false;
        uniform bool native_portrait = false;
        uniform vec2 portrait_wrap = vec2(0.0);
        uniform bool addressed_sampling = false;
        uniform vec4 source_region;
        uniform vec4 sampling_uv;
        uniform vec2 source_address = vec2(3.0);
        varying vec4 sprite_tint;
        varying vec2 local_position;
        void vertex() { sprite_tint = COLOR; sprite_tint.a *= pane_alpha; local_position = VERTEX; }
        vec3 decode_srgb(vec3 v) {
            return mix(pow((v + 0.055) / 1.055, vec3(2.4)), v / 12.92, lessThanEqual(v, vec3(0.04045)));
        }
        vec3 encode_srgb(vec3 v) {
            return mix(1.055 * pow(max(v, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055,
                v * 12.92, lessThanEqual(v, vec3(0.0031308)));
        }
        float address_local(float value,float mode) {
            if(mode==1.0)return fract(value);
            if(mode==2.0)return 1.0-abs(1.0-mod(value,2.0));
            return clamp(value,0.0,1.0);
        }
        void fragment() {
            vec2 image_uv=UV;
            if(addressed_sampling) {
                vec2 local=sampling_uv.xy+sampling_uv.zw*((UV-source_region.xy)/source_region.zw);
                local=vec2(address_local(local.x,source_address.x),address_local(local.y,source_address.y));
                image_uv=source_region.xy+source_region.zw*local;
            }
            if(native_portrait && any(greaterThan(portrait_wrap,vec2(0.5)))) {
                // The canvas repeat sampler wraps both axes. A clamped axis
                // is bounded at its edge texel centres before that sampler,
                // preserving the source's independent U/V address modes.
                vec2 edge=vec2(0.5)/vec2(textureSize(TEXTURE,0));
                image_uv=mix(clamp(image_uv,edge,vec2(1.0)-edge),image_uv,portrait_wrap);
            }
            // Portraits retain their original typed sRGB/UNORM view. Hardware
            // performs texel decode, coordinate addressing and linear filtering.
            vec4 sample_color = texture(TEXTURE, image_uv);
            // The source image was decoded per texel before bilinear sampling.
            // These sprite nodes use compare=7: even alpha zero must write.
            vec4 src = sample_color * sprite_tint;
            vec3 base_rgb = src.rgb;
            src.rgb *= material_color_scale;
            if (any(notEqual(material_color_offset, vec3(0.0)))) {
                // PS13/20 add black using the original pre-white endpoint C.
                src.rgb += (vec3(1.0) - base_rgb) * material_color_offset;
            }
            if (material_saturation != 1.0 && blend_kind != 2 && blend_kind != 3) {
                float gray = dot(src.rgb, vec3(0.298910, 0.586610, 0.114480));
                src.rgb = material_saturation * (src.rgb - vec3(gray)) + vec3(gray);
            }
            vec4 dst = texture(screen_texture, SCREEN_UV);
            vec3 rgb = decode_srgb(dst.rgb);
            float alpha;
            if (blend_kind == 10) {
                // The poem scroll's 00011000 / 00018000 / 00098800
                // sequence, contracted locally because screen copies do not
                // retain the two intervening destination-alpha writes.
                vec2 cp = (local_position - second_mask_rectangle.xy) / second_mask_rectangle.zw;
                vec2 gp = (local_position - mask_rectangle.xy) / mask_rectangle.zw;
                float clip = texture(second_mask_image, second_mask_uv.xy + cp * second_mask_uv.zw).a * second_mask_alpha;
                clip = floor(clamp(clip, 0.0, 1.0) * 255.0 + 0.5) / 255.0;
                float prepared = texture(mask_image, mask_uv.xy + gp * mask_uv.zw).a * mask_alpha * clip;
                prepared = floor(clamp(prepared, 0.0, 1.0) * 255.0 + 0.5) / 255.0;
                rgb = src.rgb * prepared + rgb * (1.0 - prepared);
                alpha = src.a * prepared;
            } else if (blend_kind == 7) {
                // Source nine-slice 00011000: RGB ZERO/ONE, A ONE/ZERO.
                alpha = src.a;
            } else if (blend_kind == 11) {
                // Domestic 00114800: RGB DEST_ALPHA/ONE, A SRC_ALPHA/ONE.
                // Reconstruct the preceding alpha-only pane inside its
                //actual rectangle, rather than relying on a canvas copy there.
                vec2 point = (local_position - mask_rectangle.xy) / mask_rectangle.zw;
                float destination_alpha = dst.a;
                if (all(greaterThanEqual(point, vec2(0.0))) && all(lessThanEqual(point, vec2(1.0)))) {
                    destination_alpha = texture(mask_image, mask_uv.xy + point * mask_uv.zw).a * sprite_tint.a;
                    destination_alpha = floor(clamp(destination_alpha, 0.0, 1.0) * 255.0 + 0.5) / 255.0;
                }
                rgb += src.rgb * destination_alpha;
                alpha = src.a * src.a + destination_alpha;
            } else if (blend_kind == 1) {
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
            } else if (blend_kind == 8 || blend_kind == 9) {
                // Source mode13 selects PS25. Only mask alpha participates;
                // zero coverage discards without writing the destination.
                vec2 point = (local_position - mask_rectangle.xy) / mask_rectangle.zw;
                if(has_component_rotation) {
                    vec2 centred=point-vec2(0.5);
                    point=vec2(dot(component_rotation.xy,centred),dot(component_rotation.zw,centred))+vec2(0.5);
                }
                if (blend_kind == 9) point = clamp(point + mask_uv_offset, vec2(0.0), vec2(1.0));
                float coverage = texture(mask_image, mask_uv.xy + point * mask_uv.zw).a;
                if (coverage == 0.0) discard;
                float portrait_alpha = sample_color.a * coverage * sprite_tint.a;
                rgb = src.rgb * portrait_alpha + rgb * (1.0 - portrait_alpha);
                alpha = portrait_alpha * portrait_alpha + dst.a * (1.0 - portrait_alpha);
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
                // PS19 applies the group saturation after the mask RGB
                // product. Desaturating the portrait alone changes colours.
                if (material_saturation != 1.0) {
                    float gray = dot(portrait_rgb, vec3(0.298910, 0.586610, 0.114480));
                    portrait_rgb = material_saturation * (portrait_rgb - vec3(gray)) + vec3(gray);
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
                src.rgb *= material_color_scale;
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
