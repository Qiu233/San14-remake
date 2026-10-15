using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record TerrainTexturePreviewPolicy(TerrainTextureGroupKind Group, uint Slot,
    int Layer, Vector2 UvScale, Vector2 UvOffset);

/// <summary>
/// Displays one explicitly selected source image/layer over the height grid. Image
/// choice, normalized XZ mapping, color space and sampling are preview conventions.
/// Owns its material, shader and original-size texture. RD retains the exact mip
/// chain; compatibility uses base mip. Arrays retain their physical layer order.
/// </summary>
public sealed class TerrainPreviewMaterial : IDisposable, ITerrainSurface
{
    public LoadedTerrainTextureGroup Group { get; }
    public TerrainTextureBinding Binding { get; }
    public LoadedRdbAsset ProjectionSource => Binding.Resource.Source;
    public string VertexInitializationCode => "";
    public string FragmentInitializationCode => "";
    public TerrainTexturePreviewPolicy Policy { get; }
    public Vector2 SourceExtent { get; }
    public Texture Texture { get; }
    public int Width => Texture is TextureLayered layered ? layered.GetWidth() : ((Texture2D)Texture).GetWidth();
    public int Height => Texture is TextureLayered layered ? layered.GetHeight() : ((Texture2D)Texture).GetHeight();
    public bool IsArray => Texture is Texture2DArray or Texture2DArrayRD;
    public string SamplingCode => SamplingFor(Texture, _deviceTexture?.SourceImage.MipCount > 1);
    public string SamplingTextureUniform => IsArray ? "original_color_array" : "original_color";
    public ShaderMaterial Material { get; }
    private readonly Shader _shader;
    private readonly TerrainDeviceTexture? _deviceTexture;
    private bool _disposed;

    private TerrainPreviewMaterial(LoadedTerrainTextureGroup group, TerrainTextureBinding binding,
        TerrainTexturePreviewPolicy policy, Vector2 sourceExtent,
        Texture texture, ShaderMaterial material, Shader shader, TerrainDeviceTexture? deviceTexture = null)
        => (Group, Binding, Policy, SourceExtent, Texture, Material, _shader, _deviceTexture) =
            (group, binding, policy, sourceExtent, texture, material, shader, deviceTexture);

    // Keep source sampling in one place. The playable ownership overlay uses this
    // same function; a verified multilayer binding can replace it without diverging.
    public const string SamplingShaderCode = """
        uniform sampler2D original_color : source_color, filter_linear, repeat_disable;
        uniform vec2 source_extent;
        uniform vec2 preview_uv_scale;
        uniform vec2 preview_uv_offset;
        vec3 sample_original_terrain(vec2 source_xz) {
            vec2 uv = (source_xz / source_extent) * preview_uv_scale + preview_uv_offset;
            return texture(original_color, uv).rgb;
        }
        """;

    public const string ArraySamplingShaderCode = """
        uniform sampler2DArray original_color_array : source_color, filter_linear, repeat_disable;
        uniform int preview_array_layer;
        uniform vec2 source_extent;
        uniform vec2 preview_uv_scale;
        uniform vec2 preview_uv_offset;
        vec3 sample_original_terrain(vec2 source_xz) {
            vec2 uv = (source_xz / source_extent) * preview_uv_scale + preview_uv_offset;
            return texture(original_color_array, vec3(uv, float(preview_array_layer))).rgb;
        }
        """;

    public void BindSamplingParameters(ShaderMaterial target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.SetShaderParameter(SamplingTextureUniform, Texture);
        if (IsArray) target.SetShaderParameter("preview_array_layer", Policy.Layer);
        target.SetShaderParameter("source_extent", SourceExtent);
        target.SetShaderParameter("preview_uv_scale", Policy.UvScale);
        target.SetShaderParameter("preview_uv_offset", Policy.UvOffset);
    }

    public static TerrainPreviewMaterial Build(GameResourceRepository repository,
        TerrainMeshResult terrain, TerrainTexturePreviewPolicy policy,
        LoadedTerrainParameterResources sourceParameters)
        => BuildCore(repository, terrain, policy, sourceParameters);

    public static async Task<TerrainPreviewMaterial> BuildAsync(GameResourceRepository repository,
        TerrainMeshResult terrain, TerrainTexturePreviewPolicy policy,
        LoadedTerrainParameterResources sourceParameters)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(sourceParameters);
        if (RenderingServer.GetRenderingDevice() is null) return Build(repository, terrain, policy, sourceParameters);
        LoadedTerrainTextureGroup group = TerrainTextureResources.LoadGroup(repository, terrain.Source, policy.Group);
        TerrainTextureBinding binding = TerrainTexturePreparation.Select(group, policy.Slot);
        TerrainTexturePreparation.RequireLayer(binding, policy.Layer);
        if (binding.Image.PixelFormat != 0x5B) return BuildCore(repository, terrain, policy, sourceParameters, group);
        TerrainDeviceTexture device = await TerrainDeviceTexture.BuildAsync(group, policy.Slot);
        try { return BuildCore(repository, terrain, policy, sourceParameters, group, device); }
        catch { await device.DisposeAsync(); throw; }
    }

    private static TerrainPreviewMaterial BuildCore(GameResourceRepository repository,
        TerrainMeshResult terrain, TerrainTexturePreviewPolicy policy,
        LoadedTerrainParameterResources sourceParameters, LoadedTerrainTextureGroup? loadedGroup = null,
        TerrainDeviceTexture? deviceTexture = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(sourceParameters);
        string context = $"{terrain.Source.Objects.Source.ResourceUri}, group {policy.Group}, slot {policy.Slot}, layer {policy.Layer}";
        Texture? texture = null;
        Shader? shader = null;
        ShaderMaterial? material = null;
        try
        {
            if (!policy.UvScale.IsFinite() || !policy.UvOffset.IsFinite())
                throw new InvalidDataException("Preview UV scale and offset must be finite.");
            if (sourceParameters.Scene.TerrainBundle.Source.ResourceUri != terrain.Source.Objects.Source.ResourceUri ||
                !sourceParameters.Scene.TerrainBundle.Source.DecodedSha256.Equals(
                    terrain.Source.Objects.Source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Terrain parameter wrapper and source height bundle differ.");
            if (!(policy.UvScale + policy.UvOffset).IsFinite())
                throw new InvalidDataException("Preview UV transform overflows at the source grid endpoint.");
            var height = terrain.Source.HeightField.HeightField;
            Vector2 extent = new(height.CellWidth * height.SampleSpacing, height.CellHeight * height.SampleSpacing);
            if (!extent.IsFinite() || extent.X <= 0 || extent.Y <= 0)
                throw new InvalidDataException("Source height extent cannot be normalized for the preview.");
            LoadedTerrainTextureGroup group = loadedGroup ?? TerrainTextureResources.LoadGroup(repository, terrain.Source, policy.Group);
            TerrainTextureBinding binding = TerrainTexturePreparation.Select(group, policy.Slot);
            TerrainTexturePreparation.RequireLayer(binding, policy.Layer);
            LoadedRdbAsset source = binding.Resource.Source;
            context += $", texture {source.ResourceUri}, image {binding.TextureIndex}";
            G1TextureImage sourceImage = binding.Image;
            Image.Format format = sourceImage.PixelFormat switch
            {
                0x00 or 0x01 => Image.Format.Rgba8,
                0x59 => Image.Format.Dxt1,
                0x5B => Image.Format.Dxt5,
                _ => throw new InvalidDataException($"Unsupported preview color format {sourceImage.PixelFormat:X2}.")
            };
            // The high-level compatibility image API cannot represent the original
            // partial mip chains. The RD path owns their exact declared levels.
            if (deviceTexture is not null)
            {
                texture = deviceTexture.Texture;
            }
            else
            {
                var prepared = TerrainTexturePreparation.PrepareCompatibility(binding, policy.Layer);
                if (sourceImage.LayerCount > 1) texture = UploadArray(prepared, format);
                else
                {
                    using Image image = Image.CreateFromData(sourceImage.Width, sourceImage.Height, false, format, prepared.Layers[0].ToArray());
                    if (image is null || image.IsEmpty()) throw new InvalidDataException("Native source image creation failed.");
                    texture = ImageTexture.CreateFromImage(image);
                }
            }
            int width = texture is TextureLayered array ? array.GetWidth() : ((Texture2D?)texture)?.GetWidth() ?? 0;
            int textureHeight = texture is TextureLayered arrayHeight ? arrayHeight.GetHeight() : ((Texture2D?)texture)?.GetHeight() ?? 0;
            if (texture is null || width != sourceImage.Width || textureHeight != sourceImage.Height)
                throw new InvalidDataException("Native texture did not retain its source dimensions.");
            texture.ResourceName = $"TerrainSource_{source.FileId:X8}_Image{binding.TextureIndex}" +
                (texture is Texture2DArray or Texture2DArrayRD ? "_Array" : $"_Layer{policy.Layer}");
            shader = new Shader
            {
                ResourceName = "SourceTerrainImagePreview",
                Code = """
                    shader_type spatial;
                    render_mode unshaded, cull_disabled;
                    """ + SamplingFor(texture, deviceTexture?.SourceImage.MipCount > 1) + """
                    varying vec2 source_uv;
                    void vertex() {
                        source_uv = VERTEX.xz;
                    }
                    void fragment() { ALBEDO = sample_original_terrain(source_uv); }
                    """
            };
            material = new ShaderMaterial { Shader = shader, ResourceName = "TerrainSourceImagePreview" };
            material.SetShaderParameter(texture is Texture2DArray or Texture2DArrayRD ? "original_color_array" : "original_color", texture);
            if (texture is Texture2DArray or Texture2DArrayRD) material.SetShaderParameter("preview_array_layer", policy.Layer);
            material.SetShaderParameter("source_extent", extent);
            material.SetShaderParameter("preview_uv_scale", policy.UvScale);
            material.SetShaderParameter("preview_uv_offset", policy.UvOffset);
            var metadata = new Godot.Collections.Dictionary
            {
                ["bundle_resource_uri"] = terrain.Source.Objects.Source.ResourceUri,
                ["bundle_decoded_sha256"] = terrain.Source.Objects.Source.DecodedSha256,
                ["group_kind"] = policy.Group.ToString(), ["group_object_id"] = group.Definition.ObjectId,
                ["group_raw_header"] = group.Definition.RawHeader,
                ["table_resource_uri"] = group.TableSource.ResourceUri,
                ["table_decoded_sha256"] = group.TableSource.DecodedSha256,
                ["table_raw_bytes"] = group.TableSource.Bytes,
                ["slot"] = policy.Slot, ["proxy_object_id"] = binding.ProxyObject.ObjectId,
                ["source_resource_uri"] = source.ResourceUri, ["source_decoded_sha256"] = source.DecodedSha256,
                ["source_container_path"] = source.SourceContainerPath,
                ["source_is_override"] = source.OverrideSource is not null || group.TableSource.OverrideSource is not null ||
                    terrain.Source.Objects.Source.OverrideSource is not null,
                ["image_index"] = binding.TextureIndex, ["layer"] = policy.Layer, ["mip_level"] = 0,
                ["source_layer_count"] = sourceImage.LayerCount, ["source_mip_count"] = sourceImage.MipCount,
                ["uploaded_layer_count"] = sourceImage.LayerCount,
                ["uploaded_as_array"] = texture is Texture2DArray or Texture2DArrayRD,
                ["uploaded_mip_count"] = deviceTexture?.SourceImage.MipCount ?? 1,
                ["uploaded_via_rendering_device"] = deviceTexture is not null,
                ["sampling_mip_mode"] = deviceTexture?.SourceImage.MipCount > 1 ? "implicit source mip chain" : "base only",
                ["source_pixel_format"] = sourceImage.PixelFormat, ["source_image_header"] = sourceImage.RawHeader,
                ["uploaded_format"] = (int)format, ["width"] = sourceImage.Width, ["height"] = sourceImage.Height,
                ["source_extent"] = extent, ["preview_uv_scale"] = policy.UvScale, ["preview_uv_offset"] = policy.UvOffset,
                ["source_parameter_uri"] = sourceParameters.Wrapper.Source.ResourceUri,
                ["source_parameter_sha256"] = sourceParameters.Wrapper.Source.DecodedSha256,
                ["source_palette_count"] = (int)sourceParameters.DeclaredParameterCount,
                ["source_palette_object_id"] = sourceParameters.ParameterObject.ObjectId,
                ["source_palette_gpu_binding_verified"] = false,
                ["resized"] = false, ["generated_mipmaps"] = false, ["recompressed"] = false,
                ["original_materials_resolved"] = false, ["original_world_mapping_verified"] = false,
                ["preview_convention"] = "explicit group/slot/layer; exact source mips on RD, mip0 on compatibility; grid XZ / extent then scale+offset; opaque RGB; source_color; linear clamp; unlit double-sided"
            };
            foreach (var entry in metadata)
            {
                material.SetMeta(entry.Key.AsString(), entry.Value);
                texture.SetMeta(entry.Key.AsString(), entry.Value);
            }
            return new(group, binding, policy, extent, texture, material, shader, deviceTexture);
        }
        catch (Exception error)
        {
            material?.Dispose(); shader?.Dispose();
            if (deviceTexture is not null) deviceTexture.Dispose(); else texture?.Dispose();
            throw new InvalidDataException($"Terrain texture preview {context}: {error.Message}", error);
        }
    }

    private static Texture2DArray UploadArray(TerrainTextureUpload prepared, Image.Format format)
    {
        G1TextureImage sourceImage = prepared.Image;
        var images = new Godot.Collections.Array<Image>();
        var texture = new Texture2DArray();
        try
        {
            for (int layer = 0; layer < sourceImage.LayerCount; layer++)
            {
                byte[] encoded = prepared.Layers[layer].ToArray();
                Image image = Image.CreateFromData(sourceImage.Width, sourceImage.Height, false, format, encoded);
                if (image is null || image.IsEmpty())
                    throw new InvalidDataException($"Native array image creation failed for layer {layer}.");
                images.Add(image);
            }
            Error error = texture.CreateFromImages(images);
            if (error != Error.Ok || texture.GetLayers() != sourceImage.LayerCount)
                throw new InvalidDataException($"Native terrain array upload failed: {error}.");
            return texture;
        }
        catch
        {
            texture.Dispose();
            throw;
        }
        finally
        {
            foreach (Image image in images) image.Dispose();
            images.Clear();
        }
    }

    private static string SamplingFor(Texture texture, bool mipmaps)
    {
        string code = texture is Texture2DArray or Texture2DArrayRD ? ArraySamplingShaderCode : SamplingShaderCode;
        return mipmaps ? code.Replace("filter_linear,", "filter_linear_mipmap,", StringComparison.Ordinal) : code;
    }

    public Image GetSelectedLayerImage()
    {
        if (_deviceTexture is not null)
        {
            // Godot Image cannot represent this partial mip chain. Return the
            // original encoded base plane; GPU diagnostics use ReadEncodedLayerAsync.
            G1TextureImage image = _deviceTexture.SourceImage;
            return Image.CreateFromData(image.Width, image.Height, false, Image.Format.Dxt5,
                image.GetEncodedMip(Binding.Resource.Source.Bytes, 0, Policy.Layer).ToArray());
        }
        return Texture is Texture2DArray array ? array.GetLayerData(Policy.Layer) : ((ImageTexture)Texture).GetImage();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Material.Dispose(); _shader.Dispose();
        if (_deviceTexture is not null) _deviceTexture.Dispose(); else Texture.Dispose();
        _disposed = true;
    }
}
