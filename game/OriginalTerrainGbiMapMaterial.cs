using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Original ordinary material inputs and sampling; final lighting remains a separate pass.</summary>
public sealed class OriginalTerrainGbiMapMaterial : IOriginalTerrainSurface, IDisposable, IAsyncDisposable
{
    public LoadedTerrainGbiMapResources Source { get; }
    public LoadedRdbAsset ProjectionSource => Source.InitialControl.Binding.Resource.Source;
    public ShaderMaterial Material { get; }
    public string SamplingCode { get; }
    public string VertexInitializationCode => OriginalTerrainGbiMapShader.VertexInitializationCode;
    public string FragmentInitializationCode => OriginalTerrainGbiMapShader.FragmentInitializationCode;
    public uint RequiredAnisotropy { get; private set; } = 1;
    private readonly Shader _shader;
    private readonly ImageTexture _control;
    private readonly IReadOnlyList<Uploaded> _textures;
    private Vector3 _eye;
    private Task? _disposal;
    private sealed record Uploaded(string Name, Texture Texture, G1TextureSamplerRecipe Sampler, bool Srgb,
        IDisposable Owner, IAsyncDisposable? AsyncOwner, bool CompleteMips);

    private OriginalTerrainGbiMapMaterial(LoadedTerrainGbiMapResources source, ShaderMaterial material,
        Shader shader, ImageTexture control, IReadOnlyList<Uploaded> textures, string sampling)
        => (Source, Material, _shader, _control, _textures, SamplingCode) =
            (source, material, shader, control, textures, sampling);

    public static async Task<OriginalTerrainGbiMapMaterial> BuildAsync(OriginalGameRepository repository,
        OriginalTerrainMeshResult mesh, LoadedTerrainParameterResources parameters, string locale)
    {
        if (ProjectSettings.GetSetting("rendering/textures/default_filters/use_nearest_mipmap_filter", false).AsBool())
            throw new NotSupportedException("Original GBI source samplers require linear interpolation between mips.");
        LoadedTerrainGbiMapResources source = OriginalTerrainGbiMapResources.LoadStockDefault(repository, mesh.Source, parameters, locale);
        if (mesh.Patches.Count == 0 || mesh.Patches.Any(patch => !patch.Mesh.GetMeta("original_initial_height_adapter").AsBool()))
            throw new InvalidDataException("GBI material requires the original ordinary initial-height mesh.");
        foreach (OriginalTerrainPatch patch in mesh.Patches)
        {
            byte[] packet = patch.Mesh.GetMeta("source_patch_instance_bytes").AsByteArray();
            if (packet.Length != 56 || !patch.Mesh.HasMeta("source_patch_shader_frequency_count"))
                throw new InvalidDataException("GBI material requires each original patch's full source input and float frequency/count attribute.");
            var expected = new Vector2(System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(packet.AsSpan(48, 4)),
                System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(packet.AsSpan(52, 4)));
            if (!expected.IsFinite() || expected != patch.Mesh.GetMeta("source_patch_shader_frequency_count").AsVector2())
                throw new InvalidDataException("GBI patch shader frequency/count differs from its source instance.");
            if (patch.Mesh.GetMeta("source_patch_material_class", "").AsString() !=
                (patch.SourceNode.Level == 0 ? "detail" : "coarse"))
                throw new InvalidDataException("GBI patch shader class differs from its original node level.");
        }
        var textures = new List<Uploaded>();
        ImageTexture? control = null; Shader? shader = null; ShaderMaterial? material = null;
        try
        {
            using (Image image = Image.CreateFromData(source.InitialControl.SourceMap.Width, source.InitialControl.SourceMap.Height,
                false, Image.Format.Rgba8, source.InitialControl.RgbaPixels))
                control = ImageTexture.CreateFromImage(image);
            control.SetMeta("initialization_state_profile", LoadedTerrainInitialControlResources.StateProfile);
            control.SetMeta("source_resource_uri", ProjectionUri(source));
            control.SetMeta("source_decoded_sha256", source.InitialControl.Binding.Resource.Source.DecodedSha256);
            control.SetMeta("original_control_runtime_observed", false);
            (LoadedTerrainTextureGroup Group, uint Slot, string Name)[] entries =
            [ (source.Root,0,"gbi_albedo_occlusion"), (source.Root,1,"gbi_normal_height"),
              (source.Root,2,"gbi_reflectance_roughness"), (source.Field,2,"gbi_far_albedo_occlusion"),
              (source.Field,3,"gbi_far_normal_height"), (source.Field,4,"gbi_far_reflectance_roughness") ];
            foreach (var entry in entries) textures.Add(await Upload(entry.Group, entry.Slot, entry.Name));
            if (source.Auxiliary is not null) textures.Add(await Upload(source.Auxiliary, 0, "gbi_extra_albedo"));
            uint[] anisotropies = textures.Where(texture => texture.Sampler.Filter == 0x55)
                .Select(texture => texture.Sampler.MaxAnisotropy).Distinct().ToArray();
            if (anisotropies.Length > 1 || anisotropies.Any(value => value is not (1 or 2 or 4 or 8 or 16)))
                throw new NotSupportedException("Original material needs multiple or unsupported viewport anisotropy values.");
            int paletteCount = source.Palette.Entries.Count;
            string declarations = "uniform sampler2D gbi_land_control : filter_nearest, repeat_disable;\n" +
                string.Join('\n', textures.Select(texture =>
                    $"uniform sampler{(texture.Texture is Texture2DArray or Texture2DArrayRD ? "2DArray" : "2D")} {texture.Name} : " +
                    Hints(texture) + ";")) + "\n" +
                $"uniform int gbi_layers[{paletteCount}];\n" +
                $"uniform float gbi_sharpness[{paletteCount}];\nuniform float gbi_scale_u[{paletteCount}];\nuniform float gbi_scale_v[{paletteCount}];\n" +
                $"uniform float gbi_tess_scale[{paletteCount}];\nuniform float gbi_tess_bias[{paletteCount}];\n";
            string sampling = declarations + OriginalTerrainGbiShader.MathCode + OriginalTerrainGbiShader.SamplingCode +
                OriginalTerrainGbiMapShader.CreateSamplingCode(RenderingServer.GetRenderingDevice() is not null, source.ExtraAlbedoEnabled);
            shader = new Shader { ResourceName = "OriginalOrdinaryTerrainMaterial",
                Code = "shader_type spatial;\nrender_mode unshaded, cull_disabled;\n" + sampling +
                    "\nvoid vertex(){" + OriginalTerrainGbiMapShader.VertexInitializationCode + "}\nvoid fragment(){" +
                    OriginalTerrainGbiMapShader.FragmentInitializationCode + "ALBEDO=sample_original_terrain(VERTEX.xz);}\n" };
            material = new ShaderMaterial { Shader = shader, ResourceName = "OriginalOrdinaryTerrainMaterialBeforeLighting" };
            var result = new OriginalTerrainGbiMapMaterial(source, material, shader, control, textures.AsReadOnly(), sampling);
            result.RequiredAnisotropy = anisotropies.SingleOrDefault(1u);
            result.BindSamplingParameters(material);
            material.SetMeta("original_material_sampling_implemented", true);
            material.SetMeta("original_lighting_implemented", false);
            material.SetMeta("original_frame_selection_observed", false);
            material.SetMeta("source_feature_record", source.Feature.RawBytes);
            material.SetMeta("source_feature_row", source.Feature.Index);
            material.SetMeta("source_pixel_program", source.Combination.Stages.Single(stage => stage.StageCode == 0).Entry!.ProgramIndex);
            material.SetMeta("source_coarse_feature_row", source.CoarseFeature.Index);
            material.SetMeta("source_coarse_pixel_program", source.CoarseCombination.Stages.Single(stage => stage.StageCode == 0).Entry!.ProgramIndex);
            material.SetMeta("source_coarse_vertex_program", source.CoarseCombination.Stages.Single(stage => stage.StageCode == 1).Entry!.ProgramIndex);
            material.SetMeta("source_shader_uri", source.Shader.Shader.Source.ResourceUri);
            material.SetMeta("source_shader_sha256", source.Shader.Shader.Source.DecodedSha256);
            material.SetMeta("source_palette_rows", paletteCount);
            material.SetMeta("source_cliff_threshold", source.CliffThreshold);
            material.SetMeta("source_near_far", new Vector2(source.NearFar.X, source.NearFar.Y));
            material.SetMeta("complete_source_mips", textures.All(texture => texture.CompleteMips));
            material.SetMeta("coarse_derivatives", RenderingServer.GetRenderingDevice() is not null);
            material.SetMeta("source_anisotropy", result.RequiredAnisotropy);
            material.SetMeta("render_context_extra_texture_enabled", false);
            return result;
        }
        catch
        {
            material?.Dispose(); shader?.Dispose(); control?.Dispose();
            await Release(textures);
            throw;
        }
    }

    public void BindSamplingParameters(ShaderMaterial target)
    {
        ObjectDisposedException.ThrowIf(_disposal is not null, this);
        target.SetShaderParameter("gbi_land_control", _control);
        foreach (Uploaded texture in _textures) target.SetShaderParameter(texture.Name, texture.Texture);
        target.SetShaderParameter("gbi_layers", Source.Palette.Entries.Select(row => checked((int)row.DetailTextureNameIndex)).ToArray());
        target.SetShaderParameter("gbi_sharpness", Source.Palette.Entries.Select(row => F32(row.BlendSharpnessBits)).ToArray());
        target.SetShaderParameter("gbi_scale_u", Source.Palette.Entries.Select(row => F32(row.ScaleUBits)).ToArray());
        target.SetShaderParameter("gbi_scale_v", Source.Palette.Entries.Select(row => F32(row.ScaleVBits)).ToArray());
        target.SetShaderParameter("gbi_tess_scale", Source.Palette.Entries.Select(row => F32(row.TessScaleBits)).ToArray());
        target.SetShaderParameter("gbi_tess_bias", Source.Palette.Entries.Select(row => F32(row.TessBiasBits)).ToArray());
        target.SetShaderParameter("gbi_cliff_threshold", Source.CliffThreshold);
        target.SetShaderParameter("gbi_near_far", new Vector2(Source.NearFar.X, Source.NearFar.Y));
        target.SetShaderParameter("gbi_eye", _eye);
    }
    public void SetEye(ShaderMaterial target, Vector3 position)
    {
        _eye = position;
        target.SetShaderParameter("gbi_eye", position);
    }

    private static async Task<Uploaded> Upload(LoadedTerrainTextureGroup group, uint slot, string name)
    {
        TerrainTextureBinding binding = group.Bindings.Single(binding => binding.Slot == slot);
        G1TextureSamplerRecipe recipe = G1TextureSamplerReader.ReadRetainedSource(binding.Image);
        bool srgb = (binding.Image.RawHeader[19] & 1) != 0;
        if (RenderingServer.GetRenderingDevice() is not null)
        {
            OriginalTerrainDeviceTexture device = await OriginalTerrainDeviceTexture.BuildAsync(group, slot, srgb,
                allowBc1RgbView: name == "gbi_extra_albedo");
            return new(name, device.Texture, recipe, srgb, device, device, true);
        }
        if (binding.Image.LayerCount > 1)
        {
            OriginalTerrainArrayTexture array = OriginalTerrainArrayTexture.Build(group, slot);
            return new(name, array.Texture, recipe, srgb, array, null, binding.Image.MipCount == 1);
        }
        byte[] encoded = binding.Image.GetEncodedMip(binding.Resource.Source.Bytes, 0).ToArray();
        Image.Format format = binding.Image.PixelFormat switch
        {
            0x00 or 0x01 => Image.Format.Rgba8, 0x59 => Image.Format.Dxt1, 0x5B => Image.Format.Dxt5,
            _ => throw new InvalidDataException("Unsupported ordinary GBI compatibility texture format.")
        };
        if (binding.Image.PixelFormat == 1)
            for (int i = 0; i < encoded.Length; i += 4) (encoded[i], encoded[i + 2]) = (encoded[i + 2], encoded[i]);
        using Image image = Image.CreateFromData(binding.Image.Width, binding.Image.Height, false, format, encoded);
        ImageTexture texture = ImageTexture.CreateFromImage(image);
        return new(name, texture, recipe, srgb, texture, null, binding.Image.MipCount == 1);
    }
    private static string Hints(Uploaded texture)
    {
        G1TextureSamplerRecipe recipe = texture.Sampler;
        if (!texture.CompleteMips && recipe.Filter == 0x15) recipe = recipe with { Filter = 0x14, MaximumLodBits = 0 };
        return OriginalTerrainSamplerHints.Create(recipe, texture.Srgb, texture.Sampler.MaxAnisotropy);
    }
    private static string ProjectionUri(LoadedTerrainGbiMapResources source) => source.InitialControl.Binding.Resource.Source.ResourceUri;
    private static float F32(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static async Task Release(IEnumerable<Uploaded> textures)
    {
        foreach (Uploaded texture in textures.Reverse())
            if (texture.AsyncOwner is not null) await texture.AsyncOwner.DisposeAsync(); else texture.Owner.Dispose();
    }
    private async Task DisposeCore()
    {
        Material.Dispose(); _shader.Dispose(); _control.Dispose();
        await Release(_textures);
    }
    public ValueTask DisposeAsync() => new(_disposal ??= DisposeCore());
    public void Dispose() => _ = (_disposal ??= DisposeCore());
}
