using Godot;

/// <summary>One normal Type6 cut. The caller supplies the actual ordered prefix
/// and screen-space quad. C and H stay alive until this cut is retired; V is
/// drawn directly into the next full-size prefix, never another quarter image.
/// GPU target format/color acceptance is still pending.</summary>
internal sealed class UiSourceType6Pass : IDisposable
{
    internal SubViewport Capture { get; }
    internal SubViewport Horizontal { get; }
    internal UiSourceType6Quad Vertical { get; }
    private readonly List<Shader> shaders = [];
    private readonly List<ShaderMaterial> materials = [];
    private bool disposed;

    internal UiSourceType6Pass(SubViewport successor, SubViewport predecessor,
        Control originalCanvas, UiSourceType6Parameters parameters, Vector2[] screenQuad,
        Vector2[] sourceUvs, Rect2I sourceRegion, Vector2I contextSize, uint drawingLayer,
        float effectiveAlpha, Vector4 captureColor, Vector4 prefillColor)
    {
        if (successor.Size != predecessor.Size || screenQuad.Length != 4 || sourceUvs.Length != 4 ||
            contextSize.X <= 0 || contextSize.Y <= 0 || predecessor.Size.X < 4 || predecessor.Size.Y < 4 ||
            !float.IsFinite(effectiveAlpha) || effectiveAlpha < 0 || effectiveAlpha > 1)
            throw new ArgumentException("Type6 requires an actual full-size prefix and four quad corners.");
        Vector2I quarter = new(predecessor.Size.X / 4, predecessor.Size.Y / 4);
        Horizontal = Viewport("Type6Horizontal", quarter);
        Capture = Viewport("Type6Capture", quarter);
        successor.AddChild(Horizontal);
        Horizontal.AddChild(Capture);
        // Every dependency is a child of its consumer. Moving the predecessor
        // preserves its shared World2D and does not move the original Controls.
        predecessor.Reparent(Capture);

        var copy = Material(CopyCode, "Type6EncodedRgbaCopy");
        copy.SetShaderParameter("source_image", predecessor.GetTexture());
        copy.SetShaderParameter("copy_color", captureColor);
        AddFull(Capture, copy, quarter);

        bool prefill = sourceRegion.Size != contextSize;
        if (prefill)
        {
            var fill = Material(CopyCode, "Type6HorizontalPrefill");
            fill.SetShaderParameter("source_image", Capture.GetTexture());
            // White source vertices do not reset the stock copy ColorScale.
            // Both capture and prefill take the actual owning copy state.
            fill.SetShaderParameter("copy_color", prefillColor);
            AddFull(Horizontal, fill, quarter);
        }
        Vector4 region = UiSourceType6Parameters.BlurRegion(sourceRegion, contextSize, quarter);
        Vector2 perPixel = parameters.PerPixel(contextSize);
        float[] weights = parameters.Weights();
        var h = Material(KernelCode + HorizontalCode, "Type6HorizontalRgb");
        ConfigureKernel(h, Capture.GetTexture(), perPixel.X);
        var hQuad = new UiSourceType6Quad { Name = "HorizontalLocalQuad", Material = h,
            Positions = screenQuad.Select(p => p / (Vector2)contextSize * (Vector2)quarter).ToArray(),
            Uvs = sourceUvs.ToArray() };
        Horizontal.AddChild(hQuad);

        var v = Material(KernelCode + VerticalCode, "Type6VerticalFinalBlend");
        ConfigureKernel(v, Horizontal.GetTexture(), perPixel.Y);
        v.SetShaderParameter("destination_image", predecessor.GetTexture());
        v.SetShaderParameter("blend_kind", (int)parameters.Blend);
        Vertical = new UiSourceType6Quad { Name = "VerticalLocalQuad", Material = v,
            Positions = screenQuad.Select(p => originalCanvas.GetGlobalTransformWithCanvas().AffineInverse() * p).ToArray(),
            Uvs = sourceUvs.ToArray(), Alpha = effectiveAlpha,
            VisibilityLayer = drawingLayer };
        originalCanvas.AddChild(Vertical);
        if (originalCanvas is UiSourceType6) originalCanvas.MoveChild(Vertical, 0);

        void ConfigureKernel(ShaderMaterial material, Texture2D source, float delta)
        {
            material.SetShaderParameter("source_image", source);
            material.SetShaderParameter("per_pixel", delta);
            material.SetShaderParameter("weights", weights);
            material.SetShaderParameter("blur_region", region);
        }
    }

    private static SubViewport Viewport(string name, Vector2I size) => new()
    {
        Name = name, Size = size, Disable3D = true, GuiDisableInput = true, TransparentBg = true,
        UseHdr2D = false, RenderTargetClearMode = SubViewport.ClearMode.Always,
        RenderTargetUpdateMode = SubViewport.UpdateMode.Always
    };
    private ShaderMaterial Material(string code, string name)
    {
        var shader = new Shader { Code = code, ResourceName = name }; shaders.Add(shader);
        var material = new ShaderMaterial { Shader = shader }; materials.Add(material); return material;
    }
    private static void AddFull(SubViewport target, ShaderMaterial material, Vector2I size)
    {
        target.AddChild(new UiSourceType6Quad { Material = material,
            Positions = [Vector2.Zero, new(size.X, 0), new(0, size.Y), (Vector2)size],
            Uvs = [Vector2.Zero, Vector2.Right, Vector2.Down, Vector2.One] });
    }

    // Non-HDR Canvas uses encoded RGB in this project. Do linear interpolation
    // explicitly after decoding each texel: decoding an already-interpolated
    // encoded sample would not reproduce an sRGB SRV's bilinear sampling.
    private const string SamplingCode = """
        vec3 decode_srgb(vec3 v) {
            return mix(pow((v + 0.055) / 1.055, vec3(2.4)), v / 12.92, lessThanEqual(v, vec3(0.04045)));
        }
        vec3 encode_srgb(vec3 v) {
            return mix(1.055 * pow(max(v, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055,
                v * 12.92, lessThanEqual(v, vec3(0.0031308)));
        }
        vec4 linear_texel(sampler2D image, ivec2 at, ivec2 extent) {
            vec4 c = texelFetch(image, clamp(at, ivec2(0), extent - ivec2(1)), 0);
            return vec4(decode_srgb(c.rgb), c.a);
        }
        vec4 linear_sample(sampler2D image, vec2 uv) {
            ivec2 extent = textureSize(image, 0);
            vec2 p = uv * vec2(extent) - vec2(0.5);
            ivec2 at = ivec2(floor(p)); vec2 f = fract(p);
            return mix(mix(linear_texel(image, at, extent), linear_texel(image, at + ivec2(1,0), extent), f.x),
                mix(linear_texel(image, at + ivec2(0,1), extent), linear_texel(image, at + ivec2(1,1), extent), f.x), f.y);
        }
        """;
    private const string Header = """
        shader_type canvas_item;
        render_mode unshaded, blend_disabled;
        uniform sampler2D source_image : filter_nearest, repeat_disable;
        """;
    private const string CopyCode = Header + SamplingCode + """
        uniform vec4 copy_color;
        void fragment() {
            vec4 c = linear_sample(source_image, UV) * copy_color;
            COLOR = vec4(encode_srgb(c.rgb), c.a);
        }
        """;
    private const string KernelCode = Header + SamplingCode + """
        uniform float weights[10];
        uniform float per_pixel;
        uniform vec4 blur_region;
        varying float vertex_alpha;
        void vertex() { vertex_alpha = COLOR.a; }
        vec3 filter_rgb(vec2 uv, bool horizontal) {
            float delta = 9.0 * per_pixel;
            vec3 result = vec3(0.0);
            for (int i = 9; i >= 0; i--) {
                vec2 p = uv;
                if (horizontal) p.x = min(max(uv.x + delta, blur_region.x), blur_region.z);
                else p.y = min(max(uv.y + delta, blur_region.y), blur_region.w);
                result += linear_sample(source_image, p).rgb * weights[i];
                if (i > 0) {
                    if (horizontal) p.x = min(max(uv.x - delta, blur_region.x), blur_region.z);
                    else p.y = min(max(uv.y - delta, blur_region.y), blur_region.w);
                    result += linear_sample(source_image, p).rgb * weights[i];
                }
                delta -= per_pixel;
            }
            return result;
        }
        """;
    private const string HorizontalCode = """
        void fragment() { COLOR = vec4(encode_srgb(filter_rgb(UV, true)), 1.0); }
        """;
    private const string VerticalCode = """
        uniform sampler2D destination_image : filter_nearest, repeat_disable;
        uniform int blend_kind;
        void fragment() {
            vec3 src = filter_rgb(UV, false);
            ivec2 extent = textureSize(destination_image, 0);
            vec4 dst = linear_texel(destination_image, ivec2(SCREEN_UV * vec2(extent)), extent);
            float a = vertex_alpha;
            vec3 rgb = src;
            float alpha = a;
            if (blend_kind == 1) { rgb = dst.rgb; alpha = a * dst.a + dst.a * (1.0 - dst.a); }
            else if (blend_kind == 2) { rgb = src * dst.a + dst.rgb * (1.0 - dst.a); alpha = a * dst.a; }
            else if (blend_kind == 3) { rgb = src * dst.a + dst.rgb * (1.0 - dst.a); alpha = a * a + dst.a * (1.0 - a); }
            else if (blend_kind == 4) { rgb = src * a + dst.rgb * (1.0 - a); alpha = a * a + dst.a * (1.0 - a); }
            COLOR = vec4(encode_srgb(rgb), alpha);
        }
        """;

    public void Dispose() => Dispose(deferRemoval: false);
    internal void Dispose(bool deferRemoval)
    {
        if (disposed) return;
        disposed = true;
        // Consumer nodes must be retired before their sampled textures. The
        // composition owner removes the viewport tree only after these calls.
        if (GodotObject.IsInstanceValid(Vertical))
        {
            Vertical.Hide(); Vertical.Material = null;
            if (deferRemoval) Vertical.QueueFree(); else Vertical.Free();
        }
        if (GodotObject.IsInstanceValid(Capture)) Capture.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        if (GodotObject.IsInstanceValid(Horizontal)) Horizontal.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        foreach (var material in materials) material.Dispose();
        foreach (var shader in shaders) shader.Dispose();
    }
}

internal partial class UiSourceType6Quad : Control
{
    internal Vector2[] Positions { get; init; } = [];
    internal Vector2[] Uvs { get; init; } = [];
    internal float Alpha { get; init; } = 1;
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; BuildCommands(); }
    // Preserve source TL/TR/BL/BR order and its TR-BL diagonal. Automatic
    // polygon triangulation need not choose the source six-index topology.
    public override void _Draw() => BuildCommands();
    private void BuildCommands()
    {
        // Hosts may create helpers at FramePreDraw after the ordinary deferred
        // Canvas redraw queue has run. Record this pure render payload now;
        // later redraws replace it without replaying any UI business callback.
        RenderingServer.CanvasItemClear(GetCanvasItem());
        RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), [0, 1, 2, 1, 3, 2], Positions,
            Enumerable.Repeat(new Color(1, 1, 1, Alpha), Positions.Length).ToArray(), Uvs, [], [], default);
    }
}
