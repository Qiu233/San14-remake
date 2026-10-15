using San14.Data;

public sealed partial class MapPostEffect3StaticLutTextureOwner
{
    private MapPostEffect3StaticHdrNativeRecipe? sourceSelectedHdrRecipe;
    private MapPostEffect3CurrentLutLookup? sourceSelectedHdrLookup;
    private LoadedMapPostEffect3StaticHdrNativeSource? sourceSelectedHdrSource;

    /// <summary>Upload the actual prepared seasonal Static HDR selection using
    /// its verified native constructor format. Source selection, native
    /// descriptor fields and completed GPU upload are distinct joins.</summary>
    public static async Task<MapPostEffect3StaticLutTextureOwner> BuildActualSelectedHdrAsync(
        GameResourceRepository repository, MapPostEffect3CurrentLutLookup actualPreparedHdrLookup)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualPreparedHdrLookup);
        var source = MapPostEffect3StaticHdrNativeSource.Load(repository, actualPreparedHdrLookup.ExecutableLocale);
        var recipe = source.ReadSelectedCurrent(actualPreparedHdrLookup);
        var owner = await BuildImportedAsync(recipe.Binding, recipe.Texture,
            recipe.SourceEvidence + ";actual-owned-prepared-HDR-selection;not-an-original-process-observation");
        owner.sourceSelectedHdrRecipe = recipe; owner.sourceSelectedHdrLookup = actualPreparedHdrLookup;
        owner.sourceSelectedHdrSource = source;
        return owner;
    }

    /// <summary>Advance only the current selection metadata. The immutable
    /// uploaded volume remains owned by this renderer generation.</summary>
    public void RefreshSelectedHdrMetadata(MapPostEffect3CurrentLutLookup actualLookup)
    {
        if(!TryRefreshSelectedHdrMetadata(actualLookup))
            throw new NotSupportedException("HDR selection changed its physical source; the actual replacement resource owner is required.");
    }

    /// <summary>Reuse only this real upload. False requires uploading the new
    /// selected volume; it does not install foreign metadata on the old RID.</summary>
    public bool TryRefreshSelectedHdrMetadata(MapPostEffect3CurrentLutLookup actualLookup)
    {
        RequireCurrent();
        var source=sourceSelectedHdrSource??throw new InvalidDataException("HDR owner has no native selection source.");
        var recipe=source.ReadSelectedCurrent(actualLookup);
        if(!IsSameUploadedHdr(recipe))return false;
        sourceSelectedHdrRecipe=recipe;sourceSelectedHdrLookup=actualLookup;
        return true;
    }

    private void RequireSameUploadedHdr(MapPostEffect3StaticHdrNativeRecipe recipe)
    {if(!IsSameUploadedHdr(recipe))throw new NotSupportedException("HDR selection changed its physical source; the actual replacement resource owner is required.");}

    private bool IsSameUploadedHdr(MapPostEffect3StaticHdrNativeRecipe recipe)
    {
        var selected=recipe.Binding;
        return texture.Source.Identity==recipe.TextureIdentity&&recipe.SourceStaticObjectId==Binding.Definition.ObjectId&&
            selected.Definitions.Source.ResourceUri==Binding.Definitions.Source.ResourceUri&&
            selected.Definitions.Source.DecodedSha256==DefinitionSourceSha256;
    }

    /// <summary>The descriptor+28 is proven null by the actual source asset
    /// and loader. It is unrelated to module30/38's manager sampler carriers.
    /// This is a CPU metadata receipt; shader reads use BindCurrentParameter.</summary>
    public MapPostEffect3ControlledSelectedHdrTexture CaptureSourceSelectedHdrMetadata()
    {
        RequireCurrent();
        var recipe = sourceSelectedHdrRecipe ?? throw new InvalidDataException("LUT owner has no source-joined native HDR construction.");
        var lookup = sourceSelectedHdrLookup ?? throw new InvalidDataException("LUT owner has no actual prepared HDR selection.");
        RequireSameUploadedHdr(recipe);
        if (!recipe.NativeDescriptorMetadata28IsProvenNull || lookup.SelectedStaticTexture is not{} selected ||
            !recipe.MatchesLoadedBinding(selected))
            throw new InvalidDataException("HDR owner no longer matches its exact source descriptor and selected GPU view.");
        return MapPostEffect3ControlledSelectedHdrTexture.CaptureActualStaticSelection(
            lookup, new(lookup.FrameNumber, OwnerIdentity), recipe.NativeDescriptorMetadata28Chain,
            recipe.SourceEvidence + ";" + SourceEvidence + ";actual-owned-selected-Static-wrapper/native28-source-null");
    }
}
