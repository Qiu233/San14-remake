using System.Security.Cryptography;
using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record TexturePreviewPolicy(ushort TextureKind, ushort UvLayer);
public sealed record PreviewMaterialSet(IReadOnlyList<ShaderMaterial> Materials,
    Godot.Collections.Array<Godot.Collections.Dictionary> Sources);

/// <summary>
/// Owns one scene load's explicit texture preview. Entry kind and UV layer are preview
/// choices, not a reconstruction of native shader, sampler or render-state selection.
/// Recreate for each scene load so modified overrides are read again before reuse.
/// </summary>
public sealed class ModelPreviewMaterials : IDisposable
{
    private sealed record TextureValue(LoadedTextureContainer Source, ImageTexture Texture,
        string PixelHash, bool HasTransparency);
    private readonly GameResourceRepository _repository;
    private readonly TexturePreviewPolicy _policy;
    private readonly Dictionary<string, TextureValue> _textures = [];
    private readonly Dictionary<(string Uri, string Hash, uint Object, string ModelUri, string ModelHash), PreviewMaterialSet> _models = [];
    private readonly List<ShaderMaterial> _ownedMaterials = [];
    private readonly Shader _shader;
    private readonly Shader _alphaShader;
    private bool _disposed;
    public int TextureCount => _textures.Count;

    public ModelPreviewMaterials(GameResourceRepository repository, TexturePreviewPolicy policy)
    {
        if (policy.UvLayer > 1) throw new InvalidDataException("Texture preview supports UV layers 0 and 1 only.");
        _repository = repository;
        _policy = policy;
        _shader = new Shader
        {
            ResourceName = "OriginalTextureUnlitPreview",
            Code = """
                shader_type spatial;
                render_mode unshaded, cull_disabled;
                uniform sampler2D original_color : source_color, filter_linear, repeat_enable;
                uniform bool use_second_uv = false;
                void fragment() {
                    ALBEDO = texture(original_color, use_second_uv ? UV2 : UV).rgb;
                }
                """
        };
        _alphaShader = new Shader
        {
            ResourceName = "OriginalTextureAlphaPreview",
            Code = """
                shader_type spatial;
                render_mode blend_mix, depth_prepass_alpha, cull_disabled;
                uniform sampler2D original_color : source_color, filter_linear, repeat_enable;
                uniform bool use_second_uv = false;
                void fragment() {
                    vec4 color = texture(original_color, use_second_uv ? UV2 : UV);
                    ALBEDO = color.rgb;
                    ALPHA = color.a;
                }
                """
        };
    }

    public PreviewMaterialSet Load(SceneModelReference reference, ModelMeshResult mesh)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(mesh);
        var key = (reference.Dictionary.Source.ResourceUri, reference.Dictionary.Source.DecodedSha256,
            reference.ModelObject.ObjectId, mesh.Source.ResourceUri, mesh.Source.DecodedSha256);
        if (_models.TryGetValue(key, out var cached)) return cached;
        return Load(ModelTextureResources.Load(_repository,
            reference.Dictionary, reference.ModelObject.ObjectId), mesh);
    }

    /// <summary>Explicit preloaded source graph, including CharacterEditor army models.
    /// The caller chooses source archives; this owner never retries another archive.</summary>
    public PreviewMaterialSet Load(LoadedModelTextureResources links, ModelMeshResult mesh)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(mesh);
        var key = (links.Dictionary.Source.ResourceUri, links.Dictionary.Source.DecodedSha256,
            links.ModelObject.ObjectId, mesh.Source.ResourceUri, mesh.Source.DecodedSha256);
        if (_models.TryGetValue(key, out var cached)) return cached;
        if (links.Model.Source.ResourceUri != mesh.Source.ResourceUri ||
            links.Model.Source.DecodedSha256 != mesh.Source.DecodedSha256)
            throw new InvalidDataException($"Texture preview {links.Dictionary.Source.ResourceUri}: model source changed while its geometry was loading.");
        var materials = new List<ShaderMaterial>(mesh.Surfaces.Count);
        var sources = new Godot.Collections.Array<Godot.Collections.Dictionary>();
        foreach (ModelMeshSurface surface in mesh.Surfaces)
        {
            string context = $"Texture preview {mesh.Source.ResourceUri}, draw {surface.DrawIndex}";
            try
            {
                using Godot.Collections.Array arrays = mesh.Mesh.SurfaceGetArrays(surface.SurfaceIndex);
                Mesh.ArrayType uvSlot = _policy.UvLayer == 0 ? Mesh.ArrayType.TexUV : Mesh.ArrayType.TexUV2;
                if (arrays[(int)uvSlot].VariantType != Variant.Type.PackedVector2Array ||
                    arrays[(int)uvSlot].AsVector2Array().Length != surface.VertexCount)
                    throw new InvalidDataException($"Required original UV layer {_policy.UvLayer} is absent or has a different vertex window.");
                ModelDrawTextureResources draw = ModelTextureResources.ResolveDrawTextures(links, surface.DrawIndex);
                ModelTextureEntryBinding[] candidates = draw.TextureEntries.Where(entry =>
                    entry.Entry.RawValue4 == _policy.TextureKind && entry.Entry.RawValue2 == _policy.UvLayer).ToArray();
                if (candidates.Length != 1)
                    throw new InvalidDataException($"Record {draw.Binding.RawIndex24} requires one preview kind {_policy.TextureKind}/UV {_policy.UvLayer} entry; found {candidates.Length}.");
                ModelTextureEntryBinding selected = candidates[0];
                TextureValue texture = LoadTexture(links, selected.Slot);
                var material = new ShaderMaterial
                {
                    Shader = texture.HasTransparency ? _alphaShader : _shader,
                    ResourceName = $"OriginalTexturePreview_Draw{surface.DrawIndex}"
                };
                _ownedMaterials.Add(material);
                material.SetShaderParameter("original_color", texture.Texture);
                material.SetShaderParameter("use_second_uv", _policy.UvLayer == 1);
                var entries = new Godot.Collections.Array<Godot.Collections.Dictionary>();
                foreach (ModelTextureEntryBinding entry in draw.TextureEntries)
                    entries.Add(EntryMetadata(entry));
                var provenance = new Godot.Collections.Dictionary
                {
                    ["surface_index"] = surface.SurfaceIndex, ["draw_index"] = surface.DrawIndex,
                    ["model_source"] = SourceMetadata(links.Model.Source),
                    ["dictionary_source"] = SourceMetadata(links.Dictionary.Source),
                    ["model_object_id"] = links.ModelObject.ObjectId,
                    ["texture_table_source"] = SourceMetadata(links.TextureTable),
                    ["texture_record_index"] = draw.Binding.RawIndex24,
                    ["raw_draw"] = draw.Binding.Draw.RawBytes, ["all_texture_entries"] = entries,
                    ["selected_entry"] = EntryMetadata(selected),
                    ["texture_source"] = SourceMetadata(texture.Source.Source),
                    ["image_index"] = 0, ["mip_level"] = 0,
                    ["width"] = texture.Texture.GetWidth(), ["height"] = texture.Texture.GetHeight(),
                    ["rgba_sha256"] = texture.PixelHash,
                    ["source_texture_has_transparency"] = texture.HasTransparency,
                    ["preview_uses_source_alpha"] = texture.HasTransparency,
                    ["original_alpha_render_mode_verified"] = false,
                    ["source_mip_count"] = texture.Source.Container.Textures[0].MipCount,
                    ["uv_layer"] = _policy.UvLayer, ["preview_texture_kind"] = _policy.TextureKind,
                    ["original_materials_resolved"] = false,
                    ["preview_policy"] = "single image; original base mip; unlit RGB with source alpha when nonopaque; source_color; linear without mips; repeat; double-sided; UV unchanged"
                };
                foreach (var item in provenance) material.SetMeta(item.Key.AsString(), item.Value);
                sources.Add(provenance);
                materials.Add(material);
            }
            catch (Exception error) when (error is InvalidDataException or ArgumentException or InvalidOperationException or IOException)
            { throw new InvalidDataException($"{context}: {error.Message}", error); }
        }
        var result = new PreviewMaterialSet(materials.AsReadOnly(), sources);
        _models.Add(key, result);
        return result;
    }

    private TextureValue LoadTexture(LoadedModelTextureResources links, ModelTextureSlot slot)
    {
        // Within one load, each URI denotes its first read. A subsequent scene selection
        // creates a new owner and rereads the active override, including failures.
        if (slot.RawValue00200571 != 0)
            throw new InvalidDataException($"{slot.TextureUri}, slot {slot.Index}, proxy {slot.ProxyObjectId:X8}: nonzero unresolved image selector is unsupported by this preview.");
        if (_textures.TryGetValue(slot.TextureUri, out var cached)) return cached;
        LoadedTextureContainer loaded = ModelTextureResources.LoadTextureContainer(_repository, links, slot.Index);
        if (loaded.Container.Textures.Count != 1 || loaded.Container.Textures[0].LayerCount != 1 ||
            loaded.Container.Textures[0].SubresourceLayout != G1TextureSubresourceLayout.SingleLayerMipChain)
            throw new InvalidDataException($"{slot.TextureUri}, slot {slot.Index}: preview requires a single ordinary 2D image.");
        RgbaTexture pixels;
        try
        {
            G1TextureImage sourceImage = loaded.Container.Textures[0];
            if (sourceImage.PixelFormat == 0x5F)
            {
                // Native type 5F is a BC7 block stream. Godot can decode the
                // original base mip; keep the regular preview path in RGBA8.
                byte[] encoded = sourceImage.GetEncodedMip(loaded.Source.Bytes, 0).ToArray();
                using Image compressed = Image.CreateFromData(sourceImage.Width,
                    sourceImage.Height, false, Image.Format.BptcRgba, encoded);
                if (compressed.Decompress() != Error.Ok || compressed.GetFormat() != Image.Format.Rgba8)
                    throw new InvalidDataException("Godot could not decode the source BC7 image to RGBA8.");
                pixels = new(sourceImage.Width, sourceImage.Height, compressed.GetData());
            }
            else pixels = G1TextureReader.Decode(loaded.Source.Bytes, 0);
        }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"{slot.TextureUri}, slot {slot.Index}, proxy {slot.ProxyObjectId:X8}: {error.Message}", error); }
        using Image image = Image.CreateFromData(pixels.Width, pixels.Height, false, Image.Format.Rgba8, pixels.Pixels);
        ImageTexture texture = ImageTexture.CreateFromImage(image);
        try
        {
            string hash = Convert.ToHexString(SHA256.HashData(pixels.Pixels));
            bool hasTransparency = false;
            for (int index = 3; index < pixels.Pixels.Length; index += 4)
                if (pixels.Pixels[index] != byte.MaxValue)
                {
                    hasTransparency = true;
                    break;
                }
            texture.ResourceName = $"OriginalTexturePreview_{loaded.Source.FileId:X8}";
            texture.SetMeta("source_resource_uri", loaded.Source.ResourceUri);
            texture.SetMeta("source_decoded_sha256", loaded.Source.DecodedSha256);
            texture.SetMeta("rgba_sha256", hash);
            texture.SetMeta("has_transparency", hasTransparency);
            texture.SetMeta("image_index", 0);
            texture.SetMeta("mip_level", 0);
            texture.SetMeta("resized", false);
            texture.SetMeta("generated_mipmaps", false);
            var value = new TextureValue(loaded, texture, hash, hasTransparency);
            _textures.Add(slot.TextureUri, value);
            return value;
        }
        catch { texture.Dispose(); throw; }
    }

    private static Godot.Collections.Dictionary EntryMetadata(ModelTextureEntryBinding value) => new()
    {
        ["record_index"] = value.RecordIndex, ["entry_index"] = value.Entry.Index,
        ["raw_entry"] = value.Entry.RawBytes, ["slot_index"] = value.Slot.Index,
        ["raw_slot_row"] = value.Slot.RawTableRow, ["proxy_object_id"] = value.Slot.ProxyObjectId,
        ["proxy_raw_image_value"] = value.Slot.RawValue00200571, ["texture_uri"] = value.Slot.TextureUri
    };

    private static Godot.Collections.Dictionary SourceMetadata(LoadedRdbAsset source) => new()
    {
        ["uri"] = source.ResourceUri, ["decoded_sha256"] = source.DecodedSha256,
        ["container_path"] = source.SourceContainerPath,
        ["is_override"] = source.OverrideSource is not null
    };

    public void Dispose()
    {
        if (_disposed) return;
        foreach (ShaderMaterial material in _ownedMaterials) material.Dispose();
        foreach (TextureValue texture in _textures.Values) texture.Texture.Dispose();
        _shader.Dispose();
        _alphaShader.Dispose();
        _ownedMaterials.Clear(); _textures.Clear(); _models.Clear();
        _disposed = true;
    }
}
