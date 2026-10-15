using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using San14.Data;
using San14.Data.Formats;

/// <summary>Import a program's actual resource roles and sampler recipes.
/// Generated images remain borrowed from their Native producers. Missing
/// producers keep their declarations and make the recipe unsupported.</summary>
public static class D3D11HdrResourceBindings
{
    // One declaration belongs to one loaded HDR generation. Its recipes share
    // static target imports; a repository reload creates new declarations.
    private sealed class Targets
    {
        internal readonly Lazy<LoadedObjects> Textures;
        internal readonly Lazy<LoadedRenderGraph> Graph;
        internal readonly Lazy<LoadedRenderGraphTargets> Geometry;
        internal readonly Lazy<ProducerKeys> Refraction;
        internal readonly Lazy<LoadedSkyAmbientResources> Ambient;
        internal Targets(GameResourceRepository repository)
        {
            Textures = new(() => repository.LoadObjects("system", 0x93FB3280));
            Graph = new(() => repository.LoadRenderGraph("system", 0x024496EE));
            Geometry = new(() => RenderGraphTargetResources.Read(Graph.Value, 53, Textures.Value));
            Refraction = new(() => ReadProducerKeys(Graph.Value));
            Ambient = new(() => SkyAmbientResources.Load(repository,
                repository.LoadObjects("system", 0xDAB638B2), 0x910603AC,
                repository.LoadObjects("system", 0x718CCB8B), Textures.Value));
        }
    }
    private sealed record TextureRecipe(string Role, string Key, byte[]? SamplerKey = null,
        bool UsesBackendDefaultSampler = false, string? AlternateKey = null, byte[]? AlternateSamplerKey = null);
    private sealed record ProducerKeys(uint Depth, uint Transmitted, uint HalfFiltered, uint Filtered, uint Blurred);
    private static readonly ConditionalWeakTable<HdrShaderDeclaration, Targets> ImportedTargets = new();

    /// <summary>Declare the actual copy97 and filter137 outputs that Native
    /// creates or selects. Logical keys travel with their physical owners.</summary>
    public static void WriteProducerTargets(GameResourceRepository repository, string hdrBankDirectory,
        int outputWidth, int outputHeight)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(hdrBankDirectory);
        var imported = new Targets(repository);
        var keys = imported.Refraction.Value;
        (string Role, uint Key)[] outputs = [("RefractionDepth", keys.Depth),
            ("TransmittedSpecular", keys.Transmitted), ("HalfFilteredSpecular", keys.HalfFiltered),
            ("FilteredSpecular", keys.Filtered),
            ("BlurredDepth", keys.Blurred)];
        var rows = new List<string>(outputs.Length);
        foreach (var (role, key) in outputs)
        {
            var texture = RenderTextureResources.Read(imported.Textures.Value, key);
            if (texture.Kind != RenderTextureKind.Texture2D)
                throw new InvalidDataException("HDR refraction producer requires a 2D target: " + role);
            var formats = RenderTextureFormatMath.Resolve(texture);
            int width = DeferredAuxiliaryTargetSources.ResolveNativeDimension(texture.Width, outputWidth);
            int height = DeferredAuxiliaryTargetSources.ResolveNativeDimension(texture.Height, outputHeight);
            byte mipCount = ResolveMipCount(texture, width, height);
            rows.Add(FormattableString.Invariant($"{role} {key:X8} {formats.ResourceFormat} {formats.ShaderViewFormat} {formats.TargetViewFormat} {width} {height} {mipCount}"));
        }
        Directory.CreateDirectory(hdrBankDirectory);
        File.WriteAllLines(Path.Combine(hdrBankDirectory, "refraction-targets.txt"), rows);
    }

    private static byte ResolveMipCount(RenderTextureDefinition texture, int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidDataException("HDR target resolved an empty extent.");
        int maximumMips = 1;
        for (int largest = Math.Max(width, height); largest > 1; largest >>= 1) maximumMips++;
        int mipCount = texture.LevelNum == 0 ? maximumMips : texture.LevelNum;
        if (mipCount > maximumMips)
            throw new InvalidDataException("HDR target mip count exceeds its resolved extent.");
        return checked((byte)mipCount);
    }

    private static ProducerKeys ReadProducerKeys(LoadedRenderGraph graph)
    {
        uint Reference(int nodeIndex, uint factory, uint hash)
        {
            if ((uint)nodeIndex >= graph.Document.Nodes.Count ||
                graph.Document.Nodes[nodeIndex].Call.FactoryClassId != factory)
                throw new InvalidDataException("HDR refraction target has no actual graph producer.");
            var fields = graph.Document.Nodes[nodeIndex].Call.Arguments.Where(a => a.NameHash == hash).ToArray();
            if (fields.Length != 1 || fields[0].DescriptorWord != 0x05000001 || fields[0].RawValue.Length != 4)
                throw new InvalidDataException("HDR refraction producer requires a typed target reference: " + hash.ToString("X8"));
            uint key = BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue);
            if (key == 0) throw new InvalidDataException("HDR refraction producer target reference is zero.");
            return key;
        }
        return new(Reference(97, 0xFD1176C6, ResourceNameHash.Compute("DstDepthTextureObjectNameHash")),
            Reference(97, 0xFD1176C6, ResourceNameHash.Compute("DstSceneTextureObjectNameHash")),
            Reference(137, 0xC41F73B1, 0x1F5E2225),
            Reference(137, 0xC41F73B1, 0xAC84A635), Reference(137, 0xC41F73B1, 0x4DAA4058));
    }

    public static IReadOnlyList<string> Write(GameResourceRepository repository,
        HdrShaderDeclaration declaration, HdrShaderProgram program, string directory, string locale,
        D3D11CurrentSeasonFrame current, int outputWidth, int outputHeight)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(program); ArgumentNullException.ThrowIfNull(current);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        var imported = ImportedTargets.GetValue(declaration, _ => new(repository));
        var native = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, locale);
        var mappings = program.ResourceBindings.Entries;
        var definitions = program.PixelDefinitions;
        var reasons = new List<string>();
        var textureRows = new List<string>(); var samplerRows = new List<string>();
        var textures = new Dictionary<string, TextureRecipe>(StringComparer.Ordinal);
        foreach (var binding in definitions.Bindings.Where(b => b.InputType != 0 && b.InputType != 3))
        {
            string name = binding.Name.Text;
            TextureRecipe recipe;
            if (binding.InputType != 2 || binding.BindCount != 1)
            {
                Missing(name, "its declared resource type or array extent has no actual producer");
                recipe = new("Unsupported_" + name, "unresolved");
            }
            else recipe = Texture(binding);
            if (!textures.TryAdd(name, recipe))
                throw new InvalidDataException("HDR selected program repeats a texture name: " + name);
            textureRows.Add(FormattableString.Invariant($"{binding.BindPoint} {recipe.Role} {recipe.Key}") +
                (recipe.AlternateKey is null ? "" : " " + recipe.AlternateKey));
        }
        foreach (var binding in definitions.Bindings.Where(b => b.InputType == 3))
        {
            string name = binding.Name.Text;
            string imageName = name == "__smpsShadowPointSampler" ? "sGBuf3" :
                name.StartsWith("__smp", StringComparison.Ordinal) ? name[5..] : "";
            string file = "sampler-" + binding.BindPoint.ToString(CultureInfo.InvariantCulture) + ".bin";
            textures.TryGetValue(imageName, out var image);
            string? alternateFile = image?.AlternateKey is null ? null :
                "sampler-" + binding.BindPoint.ToString(CultureInfo.InvariantCulture) + "-alternate.bin";
            samplerRows.Add(FormattableString.Invariant($"{binding.BindPoint} {file}") +
                (alternateFile is null ? "" : " " + alternateFile));
            byte[]? descriptor = null, alternateDescriptor = null;
            if (binding.BindCount != 1 || binding.Dimension != 0 || image is null)
                Missing(name, "no unique selected image owns its sampler");
            else if (name != "__smpsShadowPointSampler" &&
                definitions.Bindings.Single(b => b.InputType == 2 && b.Name.Text == imageName).BindPoint != binding.BindPoint)
                throw new InvalidDataException("HDR ordinary sampler differs from its selected image slot: " + name);
            else
            {
                if (image.UsesBackendDefaultSampler) descriptor = RenderSamplerSourceAdapters.BackendDefaultDescriptor(native);
                else if (image.SamplerKey is not null)
                    descriptor = RenderSamplerSourceAdapters.DecodeKey(native, image.SamplerKey).EncodeD3D11Descriptor();
                else Missing(name, "its image has no completed sampler-key producer");
                if (alternateFile is not null)
                {
                    if (image.AlternateSamplerKey is not null)
                        alternateDescriptor = RenderSamplerSourceAdapters.DecodeKey(native, image.AlternateSamplerKey).EncodeD3D11Descriptor();
                    else Missing(name, "its alternate image has no completed sampler-key producer");
                }
            }
            WriteSampler(file, descriptor);
            if (alternateFile is not null) WriteSampler(alternateFile, alternateDescriptor);
        }
        File.WriteAllLines(Path.Combine(directory, "textures.txt"), textureRows);
        File.WriteAllLines(Path.Combine(directory, "samplers.txt"), samplerRows);
        return Array.AsReadOnly(reasons.Distinct(StringComparer.Ordinal).ToArray());

        void WriteSampler(string file, byte[]? descriptor)
        {
            string path = Path.Combine(directory, file);
            if (descriptor is not null)
            {
                if (descriptor.Length != 52) throw new InvalidDataException("HDR sampler descriptor extent changed.");
                File.WriteAllBytes(path, descriptor);
            }
            else if (File.Exists(path)) File.Delete(path); // Never retain an old ready descriptor.
        }
        void Missing(string name, string reason) => reasons.Add(name + ": " + reason + ".");
        void Shape(DxbcRdefBinding binding, uint dimension, uint returnType = 5)
        {
            if (binding.Dimension != dimension || binding.ReturnType != returnType)
                throw new InvalidDataException("HDR resource role differs from its selected RDEF type: " + binding.Name.Text);
        }
        void Mapping(DxbcRdefBinding binding, short encoded)
        {
            var rows = mappings.Where(m => m.PrimaryEncoded == encoded).ToArray();
            if (rows.Length != 1 || rows[0].Name != binding.Name.Text || rows[0].SlotIndex < 0 ||
                rows[0].SlotIndex != binding.BindPoint)
                throw new InvalidDataException("HDR resource role differs from its selected G1 mapping: " + binding.Name.Text);
        }
        uint GraphKey(uint hash)
        {
            var rows = declaration.Reference.Node.Call.Arguments.Where(a => a.NameHash == hash).ToArray();
            if (rows.Length != 1 || rows[0].DescriptorWord != 0x05000001 || rows[0].RawValue.Length != 4)
                throw new InvalidDataException("HDR graph texture requires its typed reference: " + hash.ToString("X8"));
            return BinaryPrimitives.ReadUInt32LittleEndian(rows[0].RawValue);
        }
        TextureRecipe Target(DxbcRdefBinding binding, string role, short encoded, uint hash, uint dimension,
            uint? producerKey = null)
        {
            Shape(binding, dimension); Mapping(binding, encoded);
            uint key = GraphKey(hash);
            if (key == 0)
            {
                Missing(binding.Name.Text, "a zero graph target needs its separate fallback producer");
                return new(role, "unresolved");
            }
            var target = RenderTextureResources.Read(imported.Textures.Value, key);
            uint actualDimension = target.Kind == RenderTextureKind.Texture2D ? 4u : 5u;
            if (dimension != actualDimension)
                throw new InvalidDataException("HDR graph target dimension differs from its selected shader role.");
            if (producerKey is not null && key != producerKey.Value)
                Missing(binding.Name.Text, "its graph reference does not name the actual " + role + " producer target");
            int width = DeferredAuxiliaryTargetSources.ResolveNativeDimension(target.Width, outputWidth);
            int height = DeferredAuxiliaryTargetSources.ResolveNativeDimension(target.Height, outputHeight);
            byte mipCount = ResolveMipCount(target, width, height);
            return new(role, key.ToString("X8"),
                RenderSamplerSourceAdapters.FinalRenderTextureKey(native, target with { LevelNum = mipCount }));
        }
        bool NullDirectionalSlot(int slot)
        {
            foreach (var light in current.Lights)
            {
                var source = light.ResourceSlots.Single(s => s.Slot == slot);
                if (source.RequiredInterfaceClassId != 0xB672FAA2 || source.DeclaredObjectKey != 0 ||
                    source.EffectiveDataHandle != 0)
                    return false;
            }
            return true;
        }
        TextureRecipe NullableDirectional(DxbcRdefBinding binding, string role, short encoded, int slot, uint dimension)
        {
            Shape(binding, dimension); Mapping(binding, encoded);
            if (NullDirectionalSlot(slot)) return new(role, "00000000", UsesBackendDefaultSampler: true);
            Missing(binding.Name.Text, "a current Directional resource requires its actual image and sampler owner");
            return new(role, "unresolved");
        }
        TextureRecipe Texture(DxbcRdefBinding binding)
        {
            switch (binding.Name.Text)
            {
                case "sDepth": return GBuffer("GBufferDepth", 8, 0xB7CCDD1F, 4);
                case "sGBuf0": return GBuffer("GBufferAB", 128, 0x33E0132B, 4);
                case "sGBuf1": return GBuffer("GBufferRS", 129, 0x454BF54B, 5);
                case "sGBuf2": return GBuffer("GBufferNS", 130, 0xF43A3947, 4);
                case "sGBuf3": return GBuffer("GBufferRR", 131, 0x5102842C, 5);
                case "sDepthMap": return Target(binding, "RefractionDepth", 35, 0xF744A94F, 4, imported.Refraction.Value.Depth);
                case "sTrnsSpc":
                    if (declaration.OceanRendering)
                        return Target(binding, "TransmittedSpecular", 34, 0x7AA5F8D7, 4, imported.Refraction.Value.Transmitted);
                    // Native selects the image and its own sampler together
                    // using the actual current Camera/Common SourceIndex.
                    var fullSpecular = Target(binding, "TranslucentSpecular", 34, 0x1D2BAAE7, 4, imported.Refraction.Value.Filtered);
                    var specular = Target(binding, "TranslucentSpecular", 34, 0x7AA5F8D7, 4, imported.Refraction.Value.HalfFiltered);
                    return specular with { AlternateKey = fullSpecular.Key, AlternateSamplerKey = fullSpecular.SamplerKey };
                case "sTrnsDpt": return Target(binding, "BlurredDepth", 4, 0x1424ADE6, 4, imported.Refraction.Value.Blurred);
                case "sGxAmbLut":
                    Shape(binding, 4); Mapping(binding, 54);
                    return new("GGX", "00000000", RenderSamplerSourceAdapters.RenderTextureKey(native, 1));
                case "sAmbSpc":
                    Shape(binding, 9); Mapping(binding, 27);
                    var cubes = imported.Ambient.Value.SpecularTargets;
                    if (cubes.Select(c => c.LevelNum).Distinct().Count() != 1)
                    {
                        Missing(binding.Name.Text, "changing cube mip counts require a current sampler selection");
                        return new("AmbientSpecular", "unresolved");
                    }
                    return new("AmbientSpecular", cubes[0].Object.ObjectId.ToString("X8"),
                        RenderSamplerSourceAdapters.AmbientSpecularKey(RenderSamplerSourceAdapters.CubeTextureKey(native, cubes[0].LevelNum)));
                case "sHatchingShadow": return NullableDirectional(binding, "Hatching", 36, 5, 4);
                case "sRamp": return NullableDirectional(binding, "Ramp", 16, 3, 4);
                case "sIES":
                    Shape(binding, 5); Mapping(binding, 32);
                    var camera = current.Mode.AcceptedMainFrame.Camera.Source.Camera;
                    var ies = camera.Properties.Single(p => p.NameHash == 0x9148D7AD);
                    if (ies.TypeCode != 5 || ies.ElementCount != 1 || ies.RawValue.Length != 4)
                        throw new InvalidDataException("HDR IES requires its actual Camera resource reference.");
                    if (BinaryPrimitives.ReadUInt32LittleEndian(ies.RawValue) == 0)
                        return new("IES", "00000000", UsesBackendDefaultSampler: true);
                    Missing(binding.Name.Text, "the nonzero Camera IES reference needs its current image owner");
                    return new("IES", "unresolved");
                case "tMaterialTable":
                    Shape(binding, 1, 4);
                    if (declaration.MaterialObjectId != 0) return new("MaterialTable", declaration.MaterialObjectId.ToString("X8"));
                    Missing(binding.Name.Text, "the shader declares a material table but its graph reference is zero");
                    return new("MaterialTable", "unresolved");
                case "sCausticsSampler":
                    Shape(binding, 4); Mapping(binding, 58);
                    // The Native bank constructs the real fresh module, reads
                    // this Main's exported dimensions and checks the same View
                    // before accepting its still-null image. This is an owner
                    // binding, not a declaration-only proof of absence.
                    return new("Caustics", "owner", UsesBackendDefaultSampler: true);
                default:
                    Missing(binding.Name.Text, "no actual resource producer is connected for this declared role");
                    return new("Unsupported_" + binding.Name.Text, "unresolved");
            }

            TextureRecipe GBuffer(string role, short encoded, uint hash, uint dimension)
            {
                if (GraphKey(0xCA398DA4) != 0)
                {
                    Shape(binding, dimension); Mapping(binding, encoded);
                    Missing(binding.Name.Text, "composite GBuffer targets require their actual composite owner");
                    return new(role, "unresolved");
                }
                var target = Target(binding, role, encoded, hash, dimension);
                var geometry = imported.Geometry.Value;
                if (geometry.ColorTargets.Count < 4 || geometry.DepthTarget is null)
                {
                    Missing(binding.Name.Text, "the current geometry producer lacks the required GBuffer attachments");
                    return target;
                }
                uint ownerKey = role switch
                {
                    "GBufferDepth" => geometry.DepthObjectNameHash,
                    "GBufferAB" => geometry.ColorTargets[0].ObjectNameHash,
                    "GBufferNS" => geometry.ColorTargets[1].ObjectNameHash,
                    "GBufferRS" => geometry.ColorTargets[2].ObjectNameHash,
                    "GBufferRR" => geometry.ColorTargets[3].ObjectNameHash,
                    _ => throw new InvalidDataException("HDR GBuffer role has no geometry attachment.")
                };
                if (GraphKey(hash) != ownerKey)
                    Missing(binding.Name.Text, "its graph reference does not name the actual geometry producer's " + role + " target");
                return target;
            }
        }
    }
}
