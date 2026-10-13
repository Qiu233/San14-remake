using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

internal sealed record FlagMaterialPreparedJob(ArmyFlagMaterialTextureUse Primary,
    IReadOnlyList<ArmyFlagMaterialTextureUse> TextureUses, LoadedArmyFlagGlyphAtlasPrefix? Atlas,
    LoadedArmyFlagFreshPaneLayout? Layout, LoadedArmyFlagEmptyPaneLayout? EmptyLayout, byte[] NativeJob);

/// <summary>Shared source glyph/static-image encoding for actual accessory and
/// Army material requests. CPU media never certifies GPU completion.</summary>
internal sealed class FlagMaterialInputWriter
{
    public const uint MaterialMagic = 0x314D4641; // AFM1.
    readonly GameResourceRepository repository;
    readonly LoadedDeferredSamplerNativeSource samplerSource;
    readonly ArmyFlagPaneColors colors;
    readonly Dictionary<ushort, LoadedArmyFlagGlyphAtlasPrefix> atlases = [];
    readonly Dictionary<(uint File, uint Tid), LoadedArmyFlagMaterialImage> staticImages = [];
    readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
    readonly List<string> fileOrder = [];
    readonly Dictionary<string, int> writtenFiles = new(StringComparer.OrdinalIgnoreCase);
    public LoadedArmyFlagFontSource Font { get; }

    public FlagMaterialInputWriter(GameResourceRepository repository, string locale)
    {
        this.repository = repository;
        colors = ArmyFlagPaneColors.LoadCurrent(locale);
        Font = ArmyFlagFontSources.LoadFreshHostDefault(repository, true,
            "owned fresh Host default selector; normal accessory and Army flag material preparation");
        samplerSource = RenderSamplerSourceAdapters.Load(repository.SourceDirectory, locale);
        Add("common/glyph-vs.dxbc", ProjectStockShaders.Vertex.Dxbc.ToArray());
        Add("common/glyph-ps.dxbc", ProjectStockShaders.Default3.Dxbc.ToArray());
        Add("common/glyph-indices.bin", ArmyFlagGlyphQuad.CopyNativeIndices());
        Add("common/glyph-atlas-sampler.bin", RenderSamplerSourceAdapters.DecodeKey(samplerSource,
            Convert.FromHexString("1514300000000000")).EncodeD3D11Descriptor());
        byte[] vertex = new byte[32], pixel = new byte[16];
        float[] screen = [2f / 128, -2f / 64, -1, 1, 1, 1, 1, 1];
        for (int i = 0; i < screen.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(vertex.AsSpan(i * 4), screen[i]);
        BinaryPrimitives.WriteUInt32LittleEndian(pixel, 0xFF7FFFFF);
        Add("common/glyph-vs-b0.bin", vertex); Add("common/glyph-ps-b0.bin", pixel);

    }

    public FlagMaterialPreparedJob PrepareJob(int manager, int force, byte? color, ArmyFlagPaneGlyph glyph,
        string provenance, string directory, IReadOnlyList<ArmyFlagMaterialTextureUse> uses)
    {
        ushort code = glyph.TwoUtf16Units[0];
        var pending = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var runtime = uses.Where(u => u.Slot.RequiresProducerImage).ToArray();
        if (runtime.Length != 1)
            throw new NotSupportedException("Flag material requires exactly one actual glyph primary producer.");
        var primary = runtime[0]; var target = primary.Slot.RenderTarget!;
        if (target.Kind != RenderTextureKind.Texture2D || target.Width != 128 || target.Height != 64 ||
            target.LevelNum != 1 || target.MultiSample != 0)
            throw new NotSupportedException("Native glyph producer requires its actual single-mip 128 by 64 primary.");
        var format = RenderTextureFormatMath.Resolve(target);
        var palette = colors.Resolve(color, provenance);
        LoadedArmyFlagGlyphAtlasPrefix? atlas = null;
        LoadedArmyFlagFreshPaneLayout? layout = null;
        LoadedArmyFlagEmptyPaneLayout? empty = null;
        byte[] vertices = [];
        if (code == 0)
            empty = ArmyFlagEmptyPaneLayout.Build(Font, glyph, palette, manager, true);
        else
        {
            if (!atlases.TryGetValue(code, out atlas))
            {
                var decodedGlyph = ArmyFlagFontSources.ReadGlyph(Font, code, 0, 0);
                atlas = ArmyFlagGlyphAtlasPrefix.WriteFirst(Font, decodedGlyph, new byte[ArmyFlagGlyphAtlasPrefix.AtlasByteLength],
                    "owned cleared fresh flag atlas; actual first source glyph insertion", true);
                atlases.Add(code, atlas);
            }
            layout = ArmyFlagFreshPaneLayout.BuildSameMetricGlyph(Font, atlas, glyph, palette, manager,
                new byte[48], "owned fresh flag glyph record; actual source atlas is separately bound", true);
            vertices = layout.Quad.CopyNativeVertexBytes();
            pending.TryAdd($"atlases/{code:X4}/atlas.bin", atlas.Bytes);
        }
        byte[] job;
        using (var stream = new MemoryStream())
        {
            using (var output = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                foreach (uint word in new[] { (uint)manager, (uint)force, primary.Slot.ProxyObjectId, (uint)code,
                    format.ResourceFormat, format.ShaderViewFormat, format.TargetViewFormat, code == 0 ? 0u : 1u }) output.Write(word);
                output.Write(palette.BackgroundTint.RedFloat); output.Write(palette.BackgroundTint.GreenFloat);
                output.Write(palette.BackgroundTint.BlueFloat); output.Write(palette.BackgroundTint.AlphaFloat);
                output.Write(vertices);
            }
            job = stream.ToArray();
        }
        pending.Add(directory + "/glyph-job.bin", job);
        pending.Add(directory + "/primary-sampler.bin", ArmyFlagMaterialVariations.ReadProducerSampler(samplerSource, primary).EncodeD3D11Descriptor());
        using (var stream = new MemoryStream())
        {
            using (var output = new BinaryWriter(stream, Encoding.UTF8, true))
                foreach (uint word in new[] { MaterialMagic, (uint)manager, (uint)force, color is byte actualColor ? actualColor : uint.MaxValue,
                    (uint)code, primary.Slot.ProxyObjectId, primary.TextureRegister, primary.SamplerRegister }) output.Write(word);
            pending.Add(directory + "/material.bin", stream.ToArray());
        }
        WriteTextures(directory, uses, pending);
        Merge(pending);
        return new(primary, uses, atlas, layout, empty, job);
    }


    public void PrepareStatic(string directory, IReadOnlyList<ArmyFlagMaterialTextureUse> uses)
    {
        if (uses.Any(use => use.Slot.RequiresProducerImage))
            throw new NotSupportedException("Static material group requires an actual runtime producer.");
        var pending = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        WriteTextures(directory, uses, pending); Merge(pending);
    }

    public void PrepareBindings(string directory, IReadOnlyList<ArmyFlagMaterialTextureUse> uses, string primaryDirectory)
    {
        var pending = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        WriteTextures(directory, uses, pending, primaryDirectory);
        // ModelRenderer's real static-image import uses this same four-column format.
        pending.Add(directory + "/textures-native.txt", pending[directory + "/texture-formats.txt"]);
        Merge(pending);
    }

    public void AddMedia(string relativePath, byte[] bytes) => Add(relativePath, bytes);
    public void WriteNativeInputs(string materialSourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialSourceDirectory);
        string root = Path.GetFullPath(materialSourceDirectory);
        int first = writtenFiles.GetValueOrDefault(root);
        for (int i = first; i < fileOrder.Count; i++)
        {
            string relative = fileOrder[i], path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, files[relative]);
            writtenFiles[root] = i + 1;
        }
    }
    void WriteTextures(string directory, IReadOnlyList<ArmyFlagMaterialTextureUse> uses, Dictionary<string, byte[]> pending, string? primaryDirectory = null)
    {
        var textures = new List<string>(); var formats = new List<string>(); var samplers = new List<string>();
        foreach (var use in uses)
        {
            string imagePath, samplerPath;
            if (use.Slot.RequiresProducerImage)
            {
                imagePath = Relative(directory, (primaryDirectory ?? directory) + "/glyph-job.bin");
                samplerPath = Relative(directory, (primaryDirectory ?? directory) + "/primary-sampler.bin");
                textures.Add(FormattableString.Invariant($"{use.TextureRegister} {use.SamplerRegister} 1 {use.Slot.ProxyObjectId} {imagePath} {samplerPath}"));
            }
            else
            {
                uint file = use.Slot.TextureFileId ?? throw new InvalidDataException("Flag static texture file is absent.");
                uint tid = use.Slot.RawTextureId ?? throw new InvalidDataException("Flag static texture selector is absent.");
                if (!staticImages.TryGetValue((file, tid), out var image))
                {
                    image = ArmyFlagMaterialVariations.LoadStaticImage(repository, use);
                    staticImages.Add((file, tid), image);
                }
                string prefix = $"static/{image.Container.Source.FileId:X8}-{image.Image.Index}";
                if (!files.ContainsKey(prefix + ".tex") && !pending.ContainsKey(prefix + ".tex"))
                    pending.Add(prefix + ".tex", D3D11ModelResourceWriter.EncodeTexture(image.Container, image.Image, image.NativeViewFormat));
                if (!files.ContainsKey(prefix + "-s.bin") && !pending.ContainsKey(prefix + "-s.bin"))
                    pending.Add(prefix + "-s.bin", image.Sampler.EncodeD3D11Descriptor());
                imagePath = Relative(directory, prefix + ".tex"); samplerPath = Relative(directory, prefix + "-s.bin");
                textures.Add(FormattableString.Invariant($"{use.TextureRegister} {use.SamplerRegister} 0 {use.Slot.ProxyObjectId} {imagePath} {samplerPath}"));
                var resolved = ModelTextureFormatMath.Resolve(image.Image.PixelFormat, ViewSelector(image.Image));
                formats.Add(FormattableString.Invariant($"{use.TextureRegister} {resolved.ResourceFormat} {image.NativeViewFormat} {imagePath}"));
            }
            samplers.Add(FormattableString.Invariant($"{use.SamplerRegister} {samplerPath}"));
        }
        pending.Add(directory + "/textures.txt", Text(textures));
        pending.Add(directory + "/texture-formats.txt", Text(formats));
        pending.Add(directory + "/samplers-native.txt", Text(samplers));
    }

    static byte ViewSelector(G1TextureImage image) => image.RawHeader.Length >= 20 ? (byte)(image.RawHeader[19] & 1) : (byte)0;
    static string Relative(string directory, string path) => Path.GetRelativePath(directory, path).Replace('\\', '/');
    static byte[] Text(IEnumerable<string> rows) => Encoding.UTF8.GetBytes(string.Join('\n', rows) + "\n");
    void Add(string path, byte[] bytes) { if (files.TryAdd(path, bytes)) fileOrder.Add(path); }
    void Merge(Dictionary<string, byte[]> pending) { foreach (var (path, bytes) in pending) Add(path, bytes); }
}
