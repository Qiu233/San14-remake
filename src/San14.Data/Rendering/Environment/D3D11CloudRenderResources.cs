using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One Native session's declared Cloud surfaces and imported image inputs.
/// The owner encodes prepared resources; it does not use an earlier render cache
/// as a source or change the session's animation and history state.</summary>
public sealed class D3D11CloudRenderResources
{
    private sealed record Target(string Role, uint Key, RenderTextureDefinition Declaration,
        RenderTextureFormats Formats, int Width, int Height);
    private sealed record CameraSettings(float Scale, uint Split, uint Resolution);

    private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    private readonly Dictionary<uint, RenderTextureDefinition> declarations;
    private readonly Dictionary<MapSeasonCameraPackage, CameraSettings> cameras = new(ReferenceEqualityComparer.Instance);
    private readonly uint[] bayerRgb, bayerAlpha, bayerInfo, historyRgb, historyAlpha;
    private readonly Target[] targets;
    private readonly Dictionary<uint, RenderTextureFormats> formats = new();
    private uint acceptedSplit, acceptedResolution;
    private float acceptedEffectiveScale;
    private readonly SkyAppliedCloudTextureSet cloudTextures;
    private readonly SkyTextureSource? earthTexture;
    private MapSeasonCameraPackage acceptedPackage;
    private SkyAppliedSeasonCloudTextureSources acceptedCloudTextures;
    private float retainedScale;

    private D3D11CloudRenderResources(GameResourceRepository repository,
        MapSourceCreatedSeasonLightingFrame present, int width, int height,
        SkyAppliedCloudTextureSet cloudTextures, Sky2TextureBindingSnapshot sampling, D3D11CloudProgramMedia programs)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        SceneWidth = width;
        SceneHeight = height;
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var calls = graph.Document.Nodes.Where(node => node.Call.LookupKey == 0x8B23D986 &&
            node.Call.FactoryClassId == 0x1709F9D7).ToArray();
        if (calls.Length != 1)
            throw new InvalidDataException("The Cloud render declaration must identify one supported factory call.");
        var call = calls[0].Call;
        uint[] Keys(uint hash, int count)
        {
            var fields = call.Arguments.Where(arg => arg.NameHash == hash).ToArray();
            if (fields.Length != 1 || fields[0].DescriptorWord != (0x05000000u | (uint)count) ||
                fields[0].RawValue.Length != count * 4)
                throw new InvalidDataException("Cloud target references require an explicit typed declaration array.");
            var result = new uint[count];
            for (int index = 0; index < count; index++)
                result[index] = BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue.AsSpan(index * 4));
            return result;
        }
        bayerRgb = Keys(0x4A1CF814, 2);
        bayerAlpha = Keys(0xA0D0D540, 2);
        bayerInfo = Keys(0xFE6D800E, 2);
        historyRgb = Keys(0x4B408B21, 4);
        historyAlpha = Keys(0xD39DC475, 4);
        var objects = repository.LoadObjects("system", 0x93FB3280);
        declarations = bayerRgb.Concat(bayerAlpha).Concat(bayerInfo).Concat(historyRgb)
            .Concat(historyAlpha).Distinct().ToDictionary(key => key, key => RenderTextureResources.Read(objects, key));

        acceptedPackage = present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        acceptedCloudTextures = present.AcceptedSeasonCamera.Inputs.Source.CloudTextures;
        var selected = ReadCamera(acceptedPackage);
        retainedScale = ApplySelectedScale(ReadScale(present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.Source.Camera), selected.Scale);
        acceptedSplit = selected.Split;
        acceptedResolution = Resolution(present, selected);
        acceptedEffectiveScale = EffectiveScale(retainedScale);
        targets = SelectTargets(acceptedSplit, acceptedResolution, acceptedEffectiveScale);
        BayerWidth = targets[0].Width;
        BayerHeight = targets[0].Height;
        ReconstructionWidth = targets[3].Width;
        ReconstructionHeight = targets[3].Height;
        this.cloudTextures = cloudTextures;
        sampling.RequireCurrent();
        earthTexture = sampling.GetTexture(SkySamplingRole.Earth)?.Texture;
        byte[] presence = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(presence, 0x31424D53); // SMB1: installed shared Sky sampling.
        BinaryPrimitives.WriteUInt32LittleEndian(presence.AsSpan(4),
            (sampling.Presence.Earth ? 1u : 0u) | (sampling.Presence.Moon ? 2u : 0u) |
            (sampling.Presence.Milky ? 4u : 0u));
        files.Add("ambient-input/sampling-presence.bin", presence);
        ValidateAliases(programs.Main.Color, [("sBase", 0), ("sCirrus", 3),
            ("sSkyAmbientAltitude2dTable", 4), ("sTransmit2dTable", 5)]);
        ValidateAliases(programs.Main.History, [("sReprojRaymarchedCloud", 0),
            ("sReprojRaymarchedInfo", 2), ("sReprojPrevVolumetricCloud", 3)]);
        ValidateAliases(programs.Main.Composite, [("sReprojectedVolumetricCloud", 0)]);
        Encode(repository, present.AcceptedSeasonCamera.Inputs.MainFrame.Camera.ExecutableLocale);
    }

    public int SceneWidth { get; }
    public int SceneHeight { get; }
    public int BayerWidth { get; }
    public int BayerHeight { get; }
    public int ReconstructionWidth { get; }
    public int ReconstructionHeight { get; }

    public static D3D11CloudRenderResources Prepare(GameResourceRepository repository,
        MapSourceCreatedSeasonLightingFrame present, int width, int height,
        SkyAppliedCloudTextureSet cloudTextures, Sky2TextureBindingSnapshot sampling, D3D11CloudProgramMedia programs)
        => new(repository, present, width, height, cloudTextures, sampling, programs);

    /// <summary>Reject changes that need a physical Native replacement before
    /// accepting the candidate scale. Zero selected scales retain the Main value.</summary>
    public void RequireCurrent(MapSourceCreatedSeasonLightingFrame present,
        int width, int height, SkyTextureSource? currentEarth)
    {
        if (width != SceneWidth || height != SceneHeight)
            throw new NotSupportedException("Native Cloud backend resizing requires target and history replacement.");
        var package = present.AcceptedSeasonCamera.Inputs.Source.Selection.FirstPackage;
        var selected = ReadCamera(package);
        float candidateScale = ApplySelectedScale(retainedScale, selected.Scale);
        uint resolution = Resolution(present, selected);
        float effectiveScale = EffectiveScale(candidateScale);
        if (selected.Split != acceptedSplit || resolution != acceptedResolution || !effectiveScale.Equals(acceptedEffectiveScale))
        {
            var candidate = SelectTargets(selected.Split, resolution, effectiveScale);
            if (!targets.Zip(candidate).All(pair => pair.First.Key == pair.Second.Key &&
                pair.First.Width == pair.Second.Width && pair.First.Height == pair.Second.Height &&
                pair.First.Formats == pair.Second.Formats))
                throw new NotSupportedException("Changed Native Cloud targets require a physical resource replacement.");
        }
        var groups = present.AcceptedSeasonCamera.Inputs.Source.CloudTextures;
        if (!ReferenceEquals(package, acceptedPackage) || !ReferenceEquals(groups, acceptedCloudTextures))
        {
            var textures = SkyAppliedCloudTextureSet.Import(groups);
            // The Main sampling owner applies zero-reference retention. A new
            // package's declaration alone does not describe its installed image.
            if (!cloudTextures.SamePhysical(textures) || !SameTexture(earthTexture, currentEarth))
                throw new NotSupportedException("Changed Native Cloud or Main Sky images require a physical resource replacement.");
        }
        retainedScale = candidateScale;
        acceptedEffectiveScale = effectiveScale;
        acceptedSplit = selected.Split;
        acceptedResolution = resolution;
        acceptedPackage = package;
        acceptedCloudTextures = groups;
    }

    public void WriteNativeInputs(string inputRoot)
    {
        foreach (var file in files)
        {
            string path = Path.Combine(inputRoot, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, file.Value);
        }
    }

    private CameraSettings ReadCamera(MapSeasonCameraPackage package)
    {
        if (!cameras.TryGetValue(package, out var result))
        {
            result = new(ReadScale(package.Camera),
                BinaryPrimitives.ReadUInt32LittleEndian(Scalar(package.Camera, 0xC18C1355, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(Scalar(package.Camera, 0x58804691, 4)));
            cameras.Add(package, result);
        }
        return result;
    }

    private static byte[] Scalar(DokObject camera, uint hash, uint type)
    {
        var fields = camera.Properties.Where(field => field.NameHash == hash).ToArray();
        if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != 1 || fields[0].RawValue.Length != 4)
            throw new InvalidDataException("Cloud render controls require declared typed scalars.");
        return fields[0].RawValue;
    }

    private static float ReadScale(DokObject camera)
        => BinaryPrimitives.ReadSingleLittleEndian(Scalar(camera, 0x95C18937, 8));

    private static float ApplySelectedScale(float previous, float selected)
        => selected > 0 ? (selected <= 2 ? selected : 2) : previous;

    private static float EffectiveScale(float scale) => scale <= 0 || scale > 2 ? 1 : scale;

    private static uint Resolution(MapSourceCreatedSeasonLightingFrame present, CameraSettings selected)
    {
        var post = present.AcceptedSeasonCamera.Inputs.MainPostSelection;
        return post is not null && post.TryReadPreparedWord(0x7A0, out uint value) ? value : selected.Resolution;
    }

    private Target[] SelectTargets(uint split, uint resolution, float scale)
    {
        if (split != 1 || resolution is not (1 or 2))
            throw new NotSupportedException("Native Cloud currently supports the declared split RGB/alpha quality paths.");
        int index = (int)resolution - 1;
        Target Make(string role, uint key)
        {
            var declaration = declarations[key];
            if (!formats.TryGetValue(key, out var targetFormats))
            {
                if (declaration.Kind != RenderTextureKind.Texture2D || declaration.LevelNum != 1 || declaration.MultiSample != 0)
                    throw new NotSupportedException("Native Cloud targets require single-level, single-sample two-dimensional declarations.");
                targetFormats = RenderTextureFormatMath.Resolve(declaration);
                if (targetFormats.TargetViewFormat is 20 or 40 or 45 or 55)
                    throw new NotSupportedException("Cloud color targets cannot use depth views.");
                formats.Add(key, targetFormats);
            }
            int Dimension(int declared, int backend)
            {
                int inherited = DeferredAuxiliaryTargetSources.ResolveNativeDimension(declared, backend);
                double value = declared > 0 ? inherited : (float)(inherited * scale);
                if (!(value >= 1 && value <= 16384))
                    throw new NotSupportedException("The declared Cloud extent is outside the Native texture range.");
                return (int)value;
            }
            return new(role, key, declaration, targetFormats, Dimension(declaration.Width, SceneWidth), Dimension(declaration.Height, SceneHeight));
        }
        Target[] result = [Make("bayer-rgb", bayerRgb[index]), Make("bayer-alpha", bayerAlpha[index]),
            Make("bayer-info", bayerInfo[index]), Make("current-rgb", historyRgb[index * 2]),
            Make("current-alpha", historyAlpha[index * 2]), Make("previous-rgb", historyRgb[index * 2 + 1]),
            Make("previous-alpha", historyAlpha[index * 2 + 1])];
        if (result.Select(target => target.Key).Distinct().Count() != result.Length)
            throw new NotSupportedException("Aliased Cloud read/write targets require a shared surface lifecycle.");
        bool Extent(Target a, Target b) => a.Width == b.Width && a.Height == b.Height;
        if (!Extent(result[0], result[1]) || !Extent(result[0], result[2]) ||
            result.Skip(4).Any(target => !Extent(result[3], target)) ||
            result[3].Formats != result[5].Formats || result[4].Formats != result[6].Formats)
            throw new NotSupportedException("Cloud MRT extents and physical ping target formats must be compatible.");
        return result;
    }

    private static bool SameTexture(SkyTextureSource? first, SkyTextureSource? second)
        => first is null ? second is null : second is not null &&
            first.Identity == second.Identity && first.SamplerDescriptorSha256 == second.SamplerDescriptorSha256;

    private static void ValidateAliases(SkyCloudMainPass pass, (string Name, int Slot)[] expected)
    {
        var aliases = pass.Shader.ReadG1ResourceBindings(pass.Combination.Index).Entries;
        if (aliases.Count != expected.Length || expected.Where((item, index) =>
            !aliases.Any(alias => alias.Name == item.Name && alias.SlotIndex == item.Slot &&
                alias.PrimaryEncoded == 128 + index && alias.SecondarySamplerEncoded == 128 + index &&
                alias.RawAuxiliary == 8)).Any())
            throw new NotSupportedException("The selected Cloud source aliases do not support the Native grouped texture routes.");
    }

    private void Text(string path, string value) => files.Add(path, Encoding.UTF8.GetBytes(value));

    private void Encode(GameResourceRepository repository, string locale)
    {
        const string main = "main-cloud-input/", ambient = "ambient-input/";
        var targetRows = targets.Select(target => FormattableString.Invariant(
            $"{target.Role} {target.Key:X8} {target.Width} {target.Height} {target.Formats.ResourceFormat} {target.Formats.ShaderViewFormat} {target.Formats.TargetViewFormat}"));
        Text(main + "targets.txt", string.Join("\n", targetRows) + "\n");
        Text(main + "passes.txt", FormattableString.Invariant(
            $"pass1 {BayerWidth} {BayerHeight} 3 0\npass2 {ReconstructionWidth} {ReconstructionHeight} 2 0\npass4 {SceneWidth} {SceneHeight} 1 1\n"));
        // All selected vertex programs consume SV_VertexID, with the existing
        // source full-screen strip and no vertex-buffer input layout.
        Text(main + "geometry.txt", "SV_VertexID 5 0 4\n");
        Text(main + "textures.txt", "pass2 0 bayer-rgb\npass2 1 bayer-alpha\npass2 2 bayer-info\npass2 3 previous-rgb\npass2 4 previous-alpha\npass4 0 current-rgb\npass4 1 current-alpha\n");
        Text(main + "rtvs.txt", "pass1 0 bayer-rgb\npass1 1 bayer-alpha\npass1 2 bayer-info\npass2 0 current-rgb\npass2 1 current-alpha\npass4 0 current-scene\n");
        var native = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, locale);
        var samplerRows = new List<string>();
        foreach (var route in new[] { ("pass2", 0, 0, 0), ("pass2", 1, 1, 1), ("pass2", 2, 2, 2),
            ("pass2", 3, 5, 3), ("pass2", 4, 6, 4), ("pass4", 0, 3, 5), ("pass4", 1, 4, 6) })
        {
            string EncodeSampler(int targetIndex, string suffix)
            {
                byte[] key = RenderSamplerSourceAdapters.FinalRenderTextureKey(native, targets[targetIndex].Declaration);
                if (route.Item1 == "pass4")
                {
                    ushort low = BinaryPrimitives.ReadUInt16LittleEndian(key);
                    BinaryPrimitives.WriteUInt16LittleEndian(key, (ushort)((low & 0x03FF) | 0x1400));
                }
                string name = $"{route.Item1}-s{route.Item2}{suffix}.bin";
                files.Add(main + name, RenderSamplerSourceAdapters.DecodeKey(native, key).EncodeD3D11Descriptor());
                return name;
            }
            string side0 = EncodeSampler(route.Item3, "");
            // A history role changes physical image with ping. Its sampler must
            // follow the same declaration, including the composite override.
            string side1 = route.Item3 == route.Item4 ? side0 : EncodeSampler(route.Item4, "-swap");
            samplerRows.Add($"{route.Item1} {route.Item2} {side0} {side1}");
        }
        var mainRows = new List<string>();
        var cloudRows = new List<string>();
        foreach (var input in cloudTextures.CloudInputs)
        {
            string prefix = $"cloud-slot{input.SourceBindPoint}";
            cloudRows.Add(Texture(ambient, "clouds", input.SourceBindPoint, prefix, input.Texture));
            if (input.SourceBindPoint < 6)
            {
                mainRows.Add(Texture(main, "pass1", input.SourceBindPoint, prefix, input.Texture));
                samplerRows.Add($"pass1 {input.SourceBindPoint} {prefix}-sampler.bin {prefix}-sampler.bin");
            }
        }
        Text(main + "pass1-textures.tsv", string.Join("\n", mainRows) + "\n");
        Text(main + "samplers.txt", string.Join("\n", samplerRows) + "\n");
        Text(ambient + "cloud-textures.tsv", string.Join("\n", cloudRows) + "\n");
        Text(ambient + "sky-textures.tsv", earthTexture is null ? "" : Texture(ambient, "sky-current", 0, "sky-earth", earthTexture) + "\n");
    }

    private string Texture(string directory, string program, uint slot, string prefix, SkyTextureSource texture)
        => SkyTextureInputEncoding.Add(files, directory, program, slot, prefix, texture);
}
