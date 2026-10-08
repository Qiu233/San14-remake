using San14.Data;

/// <summary>Uploads the seven current native Cloud consumers and the remaining
/// STA Plane entry from their exact
/// Static/image/SRV/sampler selections. This owns textures, not Cloud draws;
/// the borrowing producer must retire before this owner.</summary>
public sealed class OriginalSceneViewNativeMapSkyCloudTextureOwner : IAsyncDisposable
{
    private readonly OriginalSkySceneTextures textures;
    private readonly IReadOnlyList<OriginalSkyAppliedCloudTextureBinding> sources;
    private OriginalSkyAppliedSeasonCloudTextureSources sourceGroups;
    private IReadOnlyList<OriginalSky2DeviceInput>? deviceInputs;
    private OriginalSky2DeviceInput? staEntry2;
    private bool retiring;
    public OriginalSkyCloudSourceTextureGroup StaPlaneSource => sourceGroups.SkylightSta;

    private OriginalSceneViewNativeMapSkyCloudTextureOwner(OriginalSkyAppliedSeasonCloudTextureSources groups,
        IReadOnlyList<OriginalSkyAppliedCloudTextureBinding> sources, OriginalSkySceneTextures textures)
    { sourceGroups = groups; this.sources = sources; this.textures = textures; }

    public static OriginalSceneViewNativeMapSkyCloudTextureOwner ReadSource(
        OriginalSkyAppliedSourceCloudSelection selection, OriginalSkyAppliedSeasonCloudTextureSources groups)
    {
        var bindings = OriginalSkyAppliedCloudTextureBindings.PrepareCurrent(selection, groups);
        if (bindings.Count != 7 || !bindings.Select(b => b.SourceBindPoint).SequenceEqual(Enumerable.Range(0, 7).Select(i => (uint)i)))
            throw new InvalidDataException("Actual Cloud source must supply its complete ordered seven-consumer readset.");
        var selections = bindings.Select(b => OriginalSkySceneTextureSelection.FromStatic(Key(b),
            b.SourceStaticBinding, b.NativeViewFormat, b.CompleteSamplerDescriptor.ToArray())).Append(
                OriginalSkySceneTextureSelection.FromStatic(StaEntry2Key, groups.SkylightSta.Entries[2],
                    groups.SkylightSta.ReadSourceShaderViewFormat(2), groups.SkylightSta.ReadSourceSamplerDescriptor(2))).ToArray();
        return new(groups, bindings, new OriginalSkySceneTextures(selections));
    }

    public void ValidateCurrent(OriginalSkyAppliedSourceCloudSelection selection)
    {
        ObjectDisposedException.ThrowIf(retiring, this);
        var current = OriginalSkyAppliedCloudTextureBindings.PrepareCurrent(selection, sourceGroups);
        if (current.Count != sources.Count) throw new InvalidDataException("Cloud source binding count changed.");
        for (int i = 0; i < current.Count; i++)
        {
            var expected = sources[i]; var actual = current[i];
            var a = actual.SourceStaticBinding; var e = expected.SourceStaticBinding;
            if (actual.SourceBindPoint != expected.SourceBindPoint || actual.SourceDimension != expected.SourceDimension ||
                actual.TextureName != expected.TextureName || actual.NativeViewFormat != expected.NativeViewFormat ||
                a.ImageIndex != e.ImageIndex || a.Resource.Source.ResourceUri != e.Resource.Source.ResourceUri ||
                a.Resource.Source.DecodedSha256 != e.Resource.Source.DecodedSha256 ||
                !actual.CompleteSamplerDescriptor.AsSpan().SequenceEqual(expected.CompleteSamplerDescriptor))
                throw new InvalidDataException("Current Cloud source differs from this owned seven-input allocation.");
        }
    }

    /// <summary>The actual source four-season profile retains these same
    /// seven image/view/sampler selections. Install the new loaded package
    /// association after the World gate drains; existing GPU descriptors stay
    /// valid only when every physical selection really matches.</summary>
    public void RequireCompatibleSeasonSources(OriginalSkyAppliedSeasonCloudTextureSources groups)
    {
        ArgumentNullException.ThrowIfNull(groups);ObjectDisposedException.ThrowIf(retiring,this);
        (OriginalSkyCloudSourceTextureGroup Group,int Entry)[] selected=[(groups.Noise,0),(groups.Noise,1),(groups.Noise,2),
            (groups.Cirrus,0),(groups.SkylightSta,3),(groups.SkylightSta,1),(groups.SkylightSta,0)];
        if(sources.Count!=selected.Length)throw new InvalidDataException("Season Cloud replacement requires its complete seven-input catalog.");
        for(int i=0;i<selected.Length;i++)
        {
            var actual=selected[i];var image=actual.Group.Entries[actual.Entry];var prior=sources[i];
            if(prior.SourceBindPoint!=(uint)i||prior.SourceDimension!=(image.Image.TextureSystem==2?8u:4u)||
                prior.NativeViewFormat!=actual.Group.ReadSourceShaderViewFormat(actual.Entry)||
                image.ImageIndex!=prior.SourceStaticBinding.ImageIndex||
                image.Resource.Source.ResourceUri!=prior.SourceStaticBinding.Resource.Source.ResourceUri||
                image.Resource.Source.DecodedSha256!=prior.SourceStaticBinding.Resource.Source.DecodedSha256||
                !actual.Group.ReadSourceSamplerDescriptor(actual.Entry).AsSpan().SequenceEqual(prior.CompleteSamplerDescriptor))
                throw new NotSupportedException("Changed seasonal Cloud image/view/sampler requires actual borrowing draw and texture replacement.");
        }
        var next = groups.SkylightSta;
        var previous = textures.Selections[StaEntry2Key];
        var entry = next.Entries[2];
        if (entry.ImageIndex != previous.ImageIndex || entry.Resource.Source.ResourceUri != previous.Source.Source.ResourceUri ||
            entry.Resource.Source.DecodedSha256 != previous.Source.Source.DecodedSha256 ||
            next.ReadSourceShaderViewFormat(2) != previous.NativeViewFormat ||
            !next.ReadSourceSamplerDescriptor(2).AsSpan().SequenceEqual(previous.RetainedSamplerDescriptor!.Value.Span))
            throw new NotSupportedException("Changed seasonal STA Plane entry2 requires actual texture replacement.");
    }
    public void InstallCompatibleSeasonSources(OriginalSkyAppliedSeasonCloudTextureSources groups)
    {RequireCompatibleSeasonSources(groups);sourceGroups=groups;}

    public async Task<IReadOnlyList<OriginalSky2DeviceInput>> GetCurrentInputsAsync(
        OriginalSkyAppliedSourceCloudSelection selection)
    {
        ValidateCurrent(selection);
        if (deviceInputs is not null) return deviceInputs;
        var inputs = new List<OriginalSky2DeviceInput>();
        foreach (var source in sources)
        {
            var input = await textures.GetInputAsync(source.SourceBindPoint, Key(source),
                source.CompleteSamplerDescriptor);
            if (input.SourceBindPoint != source.SourceBindPoint || input.SourceDimension != source.SourceDimension ||
                !input.OriginalSamplerDescriptor.Span.SequenceEqual(source.CompleteSamplerDescriptor))
                throw new InvalidDataException("Cloud GPU texture binding lost its actual source consumer identity.");
            inputs.Add(input);
        }
        ValidateCurrent(selection);
        return deviceInputs = inputs.AsReadOnly();
    }

    private static string Key(OriginalSkyAppliedCloudTextureBinding source) =>
        "native-Cloud/t" + source.SourceBindPoint + "/" + source.TextureName;

    private const string StaEntry2Key = "native-STA/plane/2";
    public async Task<IReadOnlyList<OriginalPatternCameraStaReadyPlaneEntry>> BorrowReadyStaPlaneEntriesAsync(
        OriginalSkyAppliedSourceCloudSelection selection)
    {
        var cloudInputs = await GetCurrentInputsAsync(selection);
        staEntry2 ??= await textures.GetInputAsync(2, StaEntry2Key, sourceGroups.SkylightSta.ReadSourceSamplerDescriptor(2));
        void RequireOwner() => ObjectDisposedException.ThrowIf(retiring, this);
        OriginalSky2DeviceInput[] descriptors = [cloudInputs[6], cloudInputs[5], staEntry2, cloudInputs[4]];
        var entries = new OriginalPatternCameraStaReadyPlaneEntry[4];
        for (int i = 0; i < entries.Length; i++)
            entries[i] = new(sourceGroups.SkylightSta.Entries[i], descriptors[i],
                sourceGroups.SkylightSta.ReadSourceShaderViewFormat(i), RequireOwner);
        return Array.AsReadOnly(entries);
    }

    public ValueTask DisposeAsync() { retiring = true; return textures.DisposeAsync(); }
}
