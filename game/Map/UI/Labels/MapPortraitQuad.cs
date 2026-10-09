using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary three-component LayoutShader portrait, on one quad.
/// Godot SDR Canvas uses encoded textures; decode each texel before the source
/// linear filter, then encode once for Canvas. Alpha is coverage throughout.</summary>
internal partial class MapPortraitQuad : Node2D
{
    private Rect2 _rectangle;
    internal void Configure(Rect2 rectangle, ShaderMaterial material)
    {
        _rectangle = rectangle;
        Material = material;
    }

    public override void _Draw()
    {
        DrawPolygon([_rectangle.Position, new(_rectangle.End.X, _rectangle.Position.Y),
            _rectangle.End, new(_rectangle.Position.X, _rectangle.End.Y)], [Colors.White],
            [Vector2.Zero, Vector2.Right, Vector2.One, Vector2.Down]);
    }

    internal static Shader CreateShader() => new() { ResourceName = "OriginalLayoutPortraitPS19", Code = """
        shader_type canvas_item;
        render_mode unshaded;
        uniform sampler2D portrait_texture : filter_nearest, repeat_disable;
        uniform sampler2D mask1_texture : filter_nearest, repeat_disable;
        uniform sampler2D mask2_texture : filter_nearest, repeat_disable;
        uniform vec4 portrait_region;
        uniform vec4 mask1_region;
        uniform vec4 mask2_region;
        uniform bool portrait_srgb;
        uniform bool mask1_srgb;
        uniform bool mask2_srgb;
        uniform bool mask_enabled = true;
        uniform float source_gamma = 1.0;
        uniform float alpha_test = -3.402823466e38;
        varying vec4 vertex_tint;

        vec3 decode_srgb(vec3 v) {
            return mix(pow((v + 0.055) / 1.055, vec3(2.4)), v / 12.92,
                lessThanEqual(v, vec3(0.04045)));
        }
        vec3 encode_srgb(vec3 v) {
            return mix(1.055 * pow(max(v, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055,
                v * 12.92, lessThanEqual(v, vec3(0.0031308)));
        }
        vec4 read_texel(sampler2D source, ivec2 p, ivec2 extent, bool srgb) {
            vec4 v = texelFetch(source, clamp(p, ivec2(0), extent - ivec2(1)), 0);
            if (srgb) v.rgb = decode_srgb(v.rgb);
            return v;
        }
        vec4 source_linear(sampler2D source, vec4 region, vec2 local_uv, bool srgb) {
            ivec2 extent = textureSize(source, 0);
            vec2 at = (region.xy + local_uv * region.zw) * vec2(extent) - 0.5;
            ivec2 p = ivec2(floor(at));
            vec2 f = fract(at);
            return mix(mix(read_texel(source, p, extent, srgb),
                           read_texel(source, p + ivec2(1, 0), extent, srgb), f.x),
                       mix(read_texel(source, p + ivec2(0, 1), extent, srgb),
                           read_texel(source, p + ivec2(1, 1), extent, srgb), f.x), f.y);
        }
        void vertex() { vertex_tint = COLOR; }
        void fragment() {
            vec4 t0 = source_linear(portrait_texture, portrait_region, UV, portrait_srgb);
            float a = t0.a;
            vec3 rgb = pow(abs(t0.rgb), vec3(source_gamma));
            if (mask_enabled) {
                vec4 t1 = source_linear(mask1_texture, mask1_region, UV, mask1_srgb);
                vec4 t2 = source_linear(mask2_texture, mask2_region, UV, mask2_srgb);
                // Actual pool4 modes: T1 Color3/Alpha1, T2 Color3/Alpha0.
                rgb *= pow(abs(t1.rgb), vec3(source_gamma));
                rgb *= pow(abs(t2.rgb), vec3(source_gamma));
                a = max(min(a, t1.a), t2.a);
            }
            a *= vertex_tint.a;
            if (a < alpha_test) discard;
            COLOR = vec4(encode_srgb(rgb * vertex_tint.rgb), a);
        }
        """ };

    internal static ShaderMaterial CreateMaterial(GameResourceRepository repository, Shader shader,
        UiAssets assets, string portraitName, IReadOnlyList<LcskSpriteNodePart> parts)
    {
        var portrait = assets.SpritePart(portraitName, parts[0].Uv);
        var mask1 = assets.SpritePart(parts[1].SpriteName, parts[1].Uv);
        var mask2 = assets.SpritePart(parts[2].SpriteName, parts[2].Uv);
        bool portraitSrgb = SourceSrgb(portraitName);
        bool mask1Srgb = SourceSrgb(parts[1].SpriteName), mask2Srgb = SourceSrgb(parts[2].SpriteName);
        var material = new ShaderMaterial { Shader = shader, ResourceName = portraitName + "_LayoutPS19" };
        Bind("portrait", portrait);
        Bind("mask1", mask1);
        Bind("mask2", mask2);
        // 9518E0 extra.byte19 -> metadata36.bit15 -> alternate sRGB SRV.
        material.SetShaderParameter("portrait_srgb", portraitSrgb);
        material.SetShaderParameter("mask1_srgb", mask1Srgb);
        material.SetShaderParameter("mask2_srgb", mask2Srgb);
        return material;

        bool SourceSrgb(string name)
        {
            var source = assets.Sprite(name).Source.Texture;
            var image = repository.LoadTextureContainer("ScreenLayout", source.FileId).Container.Textures[source.TextureIndex];
            var sampler = G1TextureSamplerReader.ReadRetainedSource(image);
            if (image.PixelFormat != 0x5B || image.MipCount != 1 || image.RawHeader.Length < 20 ||
                sampler.Filter != 0x14 || sampler.AddressU != 3 || sampler.AddressV != 3 ||
                sampler.MinimumLodBits != 0 || sampler.MaximumLodBits != 0)
                throw new NotSupportedException($"Army portrait component sampling needs its source branch: {name}.");
            return image.RawHeader[19] != 0;
        }

        void Bind(string name, AtlasTexture atlas)
        {
            Vector2 extent = atlas.Atlas.GetSize();
            material.SetShaderParameter(name + "_texture", atlas.Atlas);
            material.SetShaderParameter(name + "_region", new Vector4(atlas.Region.Position.X / extent.X,
                atlas.Region.Position.Y / extent.Y, atlas.Region.Size.X / extent.X, atlas.Region.Size.Y / extent.Y));
        }
    }
}
