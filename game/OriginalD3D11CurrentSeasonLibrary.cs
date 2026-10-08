using System.Buffers.Binary;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Four lazy loaded source seasons in one live Main session. The
/// library owns declarations and immutable LUT inputs; it does not own or
/// replace Main, Cloud, Ambient, Ocean or Post temporal state.</summary>
public sealed class OriginalD3D11CurrentSeasonLibrary
{
    private readonly OriginalGameRepository _repository;
    private readonly string _locale;
    private readonly LoadedObjects _lutDefinitions;
    private readonly LoadedOriginalMapPostEffect3StaticHdrNativeSource _hdrSource;
    private readonly uint _lutSelector;
    private readonly Dictionary<uint, OriginalD3D11CurrentSeasonOceanStaticSource> _oceanImages = new();
    private readonly OriginalD3D11CurrentSeasonInstallation?[] _loaded = new OriginalD3D11CurrentSeasonInstallation?[4];

    public OriginalD3D11CurrentSeasonLibrary(OriginalGameRepository repository,
        OriginalD3D11CurrentSeasonResources initial, OriginalMainMapCameraFrame initialMain)
    {
        _repository = repository; _locale = initial.Source.Locale;
        _lutDefinitions = repository.LoadObjects("KIDSSystemResource", 0xD2ECCCDF);
        _hdrSource = OriginalMapPostEffect3StaticHdrNativeSource.Load(repository, _locale);
        var context = OriginalMainMapContextCurrentSource.CreateFresh(repository, _locale,
            "native-current-season/loaded-Main-Q");
        _lutSelector = OriginalMapPostEffect3LutSelectorSource.Load(repository, _locale)
            .ResolveOwnedMain(context.BeginDeferredPreparation(initialMain.Camera.FrameNumber,
                "native-current-season/initial-source-loading")).Selector;
        _loaded[initial.Selection.SeasonIndex] = LoadInstallation(initial);
    }

    public IReadOnlyList<OriginalD3D11CurrentSeasonInstallation> LoadedInstallations
        => _loaded.Where(entry => entry is not null).Select(entry => entry!).ToArray();

    /// <summary>Only a first load of the selected season reads source archives.
    /// Repeated dates and return transitions borrow the same installed source.</summary>
    public OriginalD3D11CurrentSeasonInstallation Resolve(NativeMapSeasonState ownedSeason,
        OriginalMainMapCameraFrame currentMain)
    {
        int index = OriginalMapSeasonRuntime.PrepareFrame(ownedSeason,
            currentMain.Camera.FrameNumber).Selection.SeasonIndex;
        return _loaded[index] ??= LoadInstallation(OriginalD3D11CurrentSeasonResources
            .LoadSourceReadyCpu(_repository, ownedSeason, currentMain));
    }

    public OriginalD3D11CurrentSeasonInstallation RequireLoaded(int seasonIndex)
        => (uint)seasonIndex < 4 && _loaded[seasonIndex] is { } entry ? entry
            : throw new InvalidOperationException("The actual requested season has not been loaded.");

    private OriginalD3D11CurrentSeasonInstallation LoadInstallation(
        OriginalD3D11CurrentSeasonResources resources)
    {
        var source = resources.Source.CameraSource;
        var provider = source.Selection.Provider;
        var packages = provider.CameraChildren.Prepend(provider.Root).ToArray();
        var luts = packages.Select(package =>
        {
            uint key = Word(package.Camera, 932, 0x2572A4E9);
            uint ldr = Word(package.Camera, 933, 0xDF7EBEED);
            if (key == 0) return new OriginalD3D11CurrentSeasonLut(package.Camera.ObjectId,
                key, ldr, null, null);
            var declaration = OriginalMapPostEffect3LutSource.Load(_repository,
                _lutDefinitions, "KIDSSystemResource", key);
            var binding = declaration.LoadStaticForSelector(_repository, _lutSelector,
                _lutDefinitions, "KIDSSystemResource", "RRPreview")
                ?? throw new InvalidDataException("The selected nonzero seasonal HDR LUT has no child.");
            return new OriginalD3D11CurrentSeasonLut(package.Camera.ObjectId, key, ldr,
                declaration, _hdrSource.ReadConstruction(binding));
        }).ToArray();
        var sampling = OriginalSkySeasonPackageTextures.Read(source.Selection.FirstPackage);
        var declared = OriginalSkySceneSourceDefaults.ReadPerspectiveCamera(
            source.Selection.FirstPackage.Objects, source.Selection.FirstPackage.Camera.ObjectId);
        var selectedCamera = source.Selection.FirstPackage.Camera;
        var ocean = new[] {
            new OriginalD3D11CurrentSeasonResourceReference("StatisticalOceanSurfaceMap",0x2D60,86,0x8E4803B2,Word(selectedCamera,86,0x8E4803B2)),
            new OriginalD3D11CurrentSeasonResourceReference("StatisticalOceanReflectMap",0x2D68,87,0x3D3E03A2,Word(selectedCamera,87,0x3D3E03A2)),
            new OriginalD3D11CurrentSeasonResourceReference("StatisticalOceanWaveMap",0x2D70,442,0x5512AD40,Word(selectedCamera,442,0x5512AD40)),
            new OriginalD3D11CurrentSeasonResourceReference("StatisticalOceanBubbleMap",0x2D78,443,0xEC1E2493,Word(selectedCamera,443,0xEC1E2493)),
            new OriginalD3D11CurrentSeasonResourceReference("StatisticalOceanBubbleReflectanceMap",0x2D80,453,0x1CAFF81F,Word(selectedCamera,453,0x1CAFF81F)),
            new OriginalD3D11CurrentSeasonResourceReference("StatisticalOceanHeightMap",0x2D88,444,0x801E0DAE,Word(selectedCamera,444,0x801E0DAE)),
            new OriginalD3D11CurrentSeasonResourceReference("StatisticalOceanWaveAndBubbleMaskMap",0x2D90,764,0x47F7A169,Word(selectedCamera,764,0x47F7A169)),
            new OriginalD3D11CurrentSeasonResourceReference("StatisticalOceanRippleTexture",0x2D98,811,0xA204F9D8,Word(selectedCamera,811,0xA204F9D8)) };
        foreach (uint key in ocean.Select(row => row.Key).Where(key => key != 0).Distinct())
            if (!_oceanImages.ContainsKey(key)) _oceanImages.Add(key, ReadOceanStatic(key));
        return new(resources, _lutSelector, Array.AsReadOnly(luts), sampling,
            declared.References.CloudNoise, declared.References.CloudCirrus,
            declared.References.SkylightSta,Array.AsReadOnly(ocean),
            Array.AsReadOnly(ocean.Select(row => row.Key).Where(key => key != 0).Distinct()
                .Select(key => _oceanImages[key]).ToArray()));
    }

    private static uint Word(DokObject camera, int ordinal, uint hash)
    {
        var property = camera.Properties[ordinal];
        if (property.Index != ordinal || property.NameHash != hash || property.TypeCode != 5 ||
            property.ElementCount != 1 || property.ElementSize != 4 || property.RawValue.Length != 4)
            throw new InvalidDataException("Season LUT source loader binding changed.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }

    private OriginalD3D11CurrentSeasonOceanStaticSource ReadOceanStatic(uint key)
    {
        var definition = _lutDefinitions.Document.Objects.Single(obj => obj.ObjectId == key);
        if (definition.Kind != DokObjectKind.Inline || definition.ClassId != 0xFF7DBFD4)
            throw new NotSupportedException("Season Ocean reference requires its actual Static resource factory.");
        uint Field(uint hash,int type)
        {
            var property = definition.Properties.Single(property => property.NameHash == hash);
            if (property.TypeCode != type || property.ElementCount != 1 || property.ElementSize != 4 || property.RawValue.Length != 4)
                throw new InvalidDataException("Season Ocean Static source field differs.");
            return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
        }
        uint file = Field(0x6C7321D2,5),tid = Field(0x00200571,4);
        // Existing Ocean imports own the specialized array-header/G1T reader,
        // actual view and sampler. This is the declaration + original blob
        // association, not a second generic decoder or a ready GPU texture.
        return new(key,file,tid,_lutDefinitions,definition,_repository.LoadRdbAsset("RRPreview",file));
    }

    /// <summary>Load/reload boundary transport for the thin native LUT owner.
    /// Physical Static/TID/format duplicates share one file and one GPU view.
    /// Source identities are already checked by the original import readers.</summary>
    public void WriteNativeLutInputs(string directory)
    {
        Directory.CreateDirectory(directory);
        var written = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<string>();
        foreach (var season in LoadedInstallations)
        foreach (var lut in season.Luts)
        {
            if (lut.Recipe is not { } recipe) continue; // Source zero skips the persistent setter.
            string name = $"hdr-{recipe.SourceTextureFileId:X8}-{recipe.SourceTid}-{recipe.SourceStaticObjectId:X8}-{recipe.NativeShaderViewFormat}.tex";
            if (written.Add(name)) WriteHdr(Path.Combine(directory, name), recipe);
            rows.Add(FormattableString.Invariant($"{season.SeasonIndex} {lut.CameraObjectId} {lut.HdrKey} {lut.LdrKey} {season.LutSelector} {recipe.SourceStaticObjectId} {recipe.SourceTextureFileId} {recipe.SourceTid} {recipe.NativeResourceFormat} {recipe.NativeShaderViewFormat} {name}"));
        }
        File.WriteAllLines(Path.Combine(directory, "season-luts.txt"), rows);
    }

    private static void WriteHdr(string path, OriginalMapPostEffect3StaticHdrNativeRecipe recipe)
    {
        var image = recipe.Binding.Image;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write((uint)image.Width); writer.Write((uint)image.Height);
        writer.Write((uint)image.Depth); writer.Write((uint)image.MipCount);
        writer.Write(recipe.NativeShaderViewFormat);
        foreach (var mip in image.Mips)
        {
            var bytes = image.GetEncodedVolumeMip(recipe.Binding.Resource.Source.Bytes, mip.Level);
            int slice = bytes.Length / mip.Depth, row = slice / mip.Height;
            if (slice * mip.Depth != bytes.Length || row * mip.Height != slice)
                throw new InvalidDataException("Actual seasonal HDR volume pitches are not exact.");
            writer.Write((uint)row); writer.Write((uint)slice);
            writer.Write((uint)bytes.Length); writer.Write(bytes);
        }
    }
}

public sealed record OriginalD3D11CurrentSeasonLut(uint CameraObjectId, uint HdrKey, uint LdrKey,
    LoadedOriginalMapPostEffect3LutSource? Declaration,
    OriginalMapPostEffect3StaticHdrNativeRecipe? Recipe);

public sealed record OriginalD3D11CurrentSeasonResourceReference(string Name,int RawSourceCpuOffset,
    int PropertyOrdinal,uint NameHash,uint Key);

public sealed record OriginalD3D11CurrentSeasonOceanStaticSource(uint ObjectId,uint TextureFileId,
    uint Tid,LoadedObjects Definitions,DokObject Definition,LoadedRdbAsset TextureSource);

public sealed record OriginalD3D11CurrentSeasonInstallation(
    OriginalD3D11CurrentSeasonResources Resources, uint LutSelector,
    IReadOnlyList<OriginalD3D11CurrentSeasonLut> Luts,
    OriginalSkySeasonPackageSampling Sky, uint CloudNoiseKey, uint CloudCirrusKey,
    uint SkylightStaKey,IReadOnlyList<OriginalD3D11CurrentSeasonResourceReference> OceanReferences,
    IReadOnlyList<OriginalD3D11CurrentSeasonOceanStaticSource> OceanStaticSources)
{
    public int SeasonIndex => Resources.Selection.SeasonIndex;
    public OriginalD3D11CurrentSeasonLut SelectedLut => Luts.Single(row =>
        row.CameraObjectId == Resources.Source.CameraSource.Selection.FirstPackage.Camera.ObjectId);
}
