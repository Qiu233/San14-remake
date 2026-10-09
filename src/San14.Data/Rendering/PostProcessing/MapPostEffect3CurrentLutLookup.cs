namespace San14.Data;

/// <summary>Actual source-selected seasonal Camera cache lookup, followed by
/// the owned Main context's native selector. The resolved Data declaration and
/// Static texture bytes are separate from a successful GPU wrapper upload.</summary>
public sealed class MapPostEffect3CurrentLutLookup
{
    private readonly MapCameraConstructionPacket camera;
    private readonly bool hdr;
    private readonly MapPostEffect3OwnedMainLutSelector selector;
    private readonly MapSeasonPreparedLutTransfer? preparedTransfer;
    private readonly uint requestedKey;
    private readonly string? failedLookupEvidence;
    private MapPostEffect3CurrentLutLookup(MapCameraConstructionPacket camera,bool hdr,
        LoadedMapPostEffect3LutSource? declaration,MapPostEffect3OwnedMainLutSelector selector,
        StaticTextureBinding? selected,MapSeasonPreparedLutTransfer? transfer=null,string? failedEvidence=null)
    {
        this.camera=camera.CopyAcceptedSnapshot();this.hdr=hdr;this.selector=selector;
        requestedKey=hdr?camera.HdrLutKey932:camera.LdrLutKey933;
        ResolvedDeclaration=declaration;SelectedStaticTexture=selected;preparedTransfer=transfer;failedLookupEvidence=failedEvidence;
    }
    public long FrameNumber=>camera.FrameNumber;
    /// <summary>The actual Camera932/933 lookup argument. An RDOK may resolve
    /// to a differently numbered inline declaration; that does not change this key.</summary>
    public uint SourceObjectKey=>requestedKey;
    public LoadedMapPostEffect3LutSource? ResolvedDeclaration{get;}
    public bool LookupSucceeded=>ResolvedDeclaration is not null;
    public bool LookupExplicitlyFailed=>failedLookupEvidence is not null;
    public StaticTextureBinding? SelectedStaticTexture{get;}
    public uint SourceSelector=>selector.Selector;
    public string ContextOwnerIdentity=>selector.ContextOwnerIdentity;
    public string ExecutableLocale=>camera.ExecutableLocale;
    public bool IsHdrLookup=>hdr;
    public bool ProvesGpuTextureReady=>false;
    public bool ActiveContextIdentityVerified=>false;
    public bool ProvesPreparedCameraHandleTransfer=>preparedTransfer is not null;
    public MapSeasonPreparedLutTransfer? PreparedTransfer=>preparedTransfer;
    public string SourceEvidence=>(ResolvedDeclaration?.SourceEvidence??"explicit-owned-source593-lookup-failed;"+failedLookupEvidence)+";"+selector.SourceEvidence+
        ";selected-season-resource-cache-lookup;"+
        (preparedTransfer is null?"prepared-transfer-not-joined":preparedTransfer.SourceEvidence)+";"+
        (hdr?"HDR932":"LDR933")+";source-Static-resolution-only;not-a-GPU-wrapper-receipt";

    public static MapPostEffect3CurrentLutLookup ResolveOwnedSource(
        GameResourceRepository repository,MapCameraConstructionPacket currentCamera,bool hdr,
        LoadedObjects explicitDefinitions,string explicitDefinitionsArchive,string explicitTextureArchive,
        MapPostEffect3OwnedMainLutSelector actualOwnedMainSelector)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(currentCamera);
        ArgumentNullException.ThrowIfNull(explicitDefinitions);ArgumentNullException.ThrowIfNull(actualOwnedMainSelector);
        if(currentCamera.FrameNumber<=0||actualOwnedMainSelector.FrameNumber!=currentCamera.FrameNumber||
            actualOwnedMainSelector.Native.Native.Locale!=currentCamera.ExecutableLocale)
            throw new InvalidDataException("Current LUT source lookup requires this actual owned Main Camera/context frame.");
        uint key=hdr?currentCamera.HdrLutKey932:currentCamera.LdrLutKey933;
        if(key==0)throw new InvalidDataException("A zero native LUT source key has no resolved resource handle.");
        // Resolution uses the actual source declaration scope. No candidate
        // index, object/file ID interchange, or registry namespace is inferred.
        var declaration=MapPostEffect3LutSource.Load(repository,explicitDefinitions,explicitDefinitionsArchive,key);
        var selected=declaration.LoadStaticForSelector(repository,actualOwnedMainSelector.Selector,
            explicitDefinitions,explicitDefinitionsArchive,explicitTextureArchive);
        return new(currentCamera,hdr,declaration,actualOwnedMainSelector,selected);
    }

    /// <summary>Record a genuinely failed owned source lookup. Unknown source
    /// scope, unsupported declarations and decoding errors must still throw;
    /// they are not an observed native null lookup.</summary>
    public static MapPostEffect3CurrentLutLookup CaptureOwnedFailedSource(
        MapCameraConstructionPacket currentCamera,bool hdr,
        MapPostEffect3OwnedMainLutSelector actualOwnedMainSelector,string actualFailedLookupEvidence)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);ArgumentNullException.ThrowIfNull(actualOwnedMainSelector);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualFailedLookupEvidence);
        if(currentCamera.FrameNumber<=0||actualOwnedMainSelector.FrameNumber!=currentCamera.FrameNumber||
            actualOwnedMainSelector.Native.Native.Locale!=currentCamera.ExecutableLocale||
            (hdr?currentCamera.HdrLutKey932:currentCamera.LdrLutKey933)==0)
            throw new InvalidDataException("Failed LUT lookup requires this actual nonzero key and Main selector frame.");
        return new(currentCamera,hdr,null,actualOwnedMainSelector,null,failedEvidence:actualFailedLookupEvidence);
    }

    /// <summary>Join the actual Main17BD0 seasonal caller with this selected
    /// Camera lookup. The native nonnegative slot proves installs this
    /// lookup result in prepared1828/1830 without restoring the prior handle.
    /// Neither successful593 resolution nor this transfer proves an A0 GPU view.</summary>
    public MapPostEffect3CurrentLutLookup JoinActualMainPreparation(MapSeasonPreparedLutTransfer transfer)
    {
        ArgumentNullException.ThrowIfNull(transfer);transfer.ValidateSameCamera(camera);ValidateSameCamera(camera);
        if(transfer.Native.Native.Locale!=selector.Native.Native.Locale||
            transfer.SourceRestoresPreviousPreparedPrimaryHandles)
            throw new InvalidDataException("Prepared LUT lookup and Main selector use different native source routes.");
        return new(camera,hdr,ResolvedDeclaration,selector,SelectedStaticTexture,transfer,failedLookupEvidence);
    }

    public void ValidateOwnedMainSelector(MapPostEffect3OwnedMainLutSelector actualSelector)
    {
        ArgumentNullException.ThrowIfNull(actualSelector);
        if(!ReferenceEquals(selector,actualSelector))
            throw new InvalidDataException("Current LUT lookup belongs to another actual Main selector receipt.");
    }

    public void ValidateSameCamera(MapCameraConstructionPacket actualCamera)
    {
        ArgumentNullException.ThrowIfNull(actualCamera);
        camera.RequireEquivalentCurrentPostEffectScalars(actualCamera);
        if(actualCamera.FrameNumber!=FrameNumber||actualCamera.ExecutableLocale!=camera.ExecutableLocale||
            actualCamera.Source.Camera.ObjectId!=camera.Source.Camera.ObjectId||
            actualCamera.Source.Resource.Source.ResourceUri!=camera.Source.Resource.Source.ResourceUri||
            actualCamera.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256||
            !actualCamera.Prefix.Span.SequenceEqual(camera.Prefix.Span)||
            (hdr?actualCamera.HdrLutKey932:actualCamera.LdrLutKey933)!=SourceObjectKey)
            throw new InvalidDataException("Resolved seasonal LUT handle belongs to another actual Camera/cache/source frame.");
        _=ResolvedDeclaration?.GetDeclaredChildKey(selector.Selector);
        preparedTransfer?.ValidateSameCamera(actualCamera);
    }
}
