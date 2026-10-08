using System.Security.Cryptography;

namespace San14.Data;

/// <summary>Typed owned Q80 publication projected at the actual F103 ambient
/// consumer. State168 and View19B8 are current producer values, not World-mask
/// unions or inferred GPU progress. This packet identifies a source wrapper;
/// the game layer must borrow its actual completed resource before binding.</summary>
public sealed class OriginalDeferredSelected383SkyProjection
{
    private readonly OriginalDeferredSelected383SkyProjectionOwner owner;
    internal OriginalMainCameraView19B8Projection? CameraWriter { get; set; }
    internal OriginalSkyCpuState168Snapshot? SkyWriter { get; set; }
    internal OriginalDeferredSelected383SkyProjection(OriginalDeferredSelected383SkyProjectionOwner owner,
        OriginalDeferredF103SelectedSource selection, OriginalMapSourceCreatedLightingPacket lighting,
        int slot, uint view19B8, uint state, OriginalCubeRenderTextureDefinition cube, string provenance)
    {
        this.owner=owner; Selection=selection; FrameNumber=lighting.FrameNumber; Binding=lighting.Binding;
        LocalLightDataSha256=Convert.ToHexString(SHA256.HashData(lighting.LocalLightData.Span));
        SelectedViewSlot=slot; CurrentView19B8=view19B8; NativeSkyState168=state;
        SelectedSpecularIndex=(state>>8)&3; CubeSource=cube; CurrentProducerProvenance=provenance;
    }
    public OriginalDeferredF103SelectedSource Selection { get; }
    public long FrameNumber { get; }
    public OriginalMapLightingBinding Binding { get; }
    public string LocalLightDataSha256 { get; }
    public int SelectedViewSlot { get; }
    public uint CurrentView19B8 { get; }
    public uint NativeSkyState168 { get; }
    public uint SelectedSpecularIndex { get; }
    public OriginalCubeRenderTextureDefinition CubeSource { get; }
    public string OwnedQIdentity => owner.OwnedQIdentity;
    public string OwnedSkyIdentity => owner.OwnedSkyIdentity;
    public short EncodedParameter => 27;
    public uint PixelRegister => Selection.PixelDefinitions.Bindings.Single(b=>b.Name.Text=="sAmbSpc").BindPoint;
    public bool UsesExecutionContext328Fallback => false;
    public bool HasOwnedCurrentWriters => CameraWriter is not null && SkyWriter is not null;
    public string CurrentProducerProvenance { get; }
    public void ValidateCurrent(OriginalDeferredF103SelectedSource selection, OriginalMapSourceCreatedLightingPacket lighting)
        => owner.Validate(this,selection,lighting);
    /// <summary>Validate the source metadata of a borrowed cube. This check
    /// supplies no GPU readiness or lifetime authority; that remains with the
    /// actual game resource lease, whose source declaration must match.</summary>
    public void ValidateBorrowedCubeSource(OriginalCubeRenderTextureDefinition actualBorrowedSource)
    {
        ArgumentNullException.ThrowIfNull(actualBorrowedSource);
        var actual=OriginalCubeRenderTextureResources.Read(actualBorrowedSource.Source,actualBorrowedSource.Object.ObjectId);
        if (actual!=actualBorrowedSource || actual.Object.ObjectId!=CubeSource.Object.ObjectId ||
            actual.Source.Source.ResourceUri!=CubeSource.Source.Source.ResourceUri ||
            actual.Source.Source.DecodedSha256!=CubeSource.Source.Source.DecodedSha256)
            throw new InvalidDataException("The borrowed completed cube does not own F103's current selected Sky wrapper.");
    }
}

/// <summary>One actual owned SkyAmbient/view-clone projection receiver at Q80.
/// It never creates a fallback cube or selects another completed generation
/// when the native consumer requests a different original wrapper.</summary>
public sealed class OriginalDeferredSelected383SkyProjectionOwner
{
    private readonly LoadedOriginalSkyAmbientResources source;
    private readonly string locale,native;
    private readonly OriginalMapLightingBinding binding;
    private long latestFrame;
    private uint latestState,latestViewMask;
    private int latestSlot;
    private OriginalDeferredSelected383SkyProjectionOwner(LoadedOriginalSkyAmbientResources source,
        string locale, OriginalMapLightingBinding binding, string q, string sky, string native)
    { this.source=source;this.locale=locale;this.binding=binding;OwnedQIdentity=q;OwnedSkyIdentity=sky;this.native=native; }
    public string OwnedQIdentity { get; }
    public string OwnedSkyIdentity { get; }
    public static OriginalDeferredSelected383SkyProjectionOwner Create(OriginalGameRepository repository,
        string executableLocale, LoadedOriginalSkyAmbientResources actualSkySource,
        OriginalMapLightingBinding binding, string ownedQIdentity, string ownedSkyIdentity)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualSkySource);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedQIdentity);ArgumentException.ThrowIfNullOrWhiteSpace(ownedSkyIdentity);
        ValidateSource(actualSkySource);
        return new(actualSkySource,executableLocale,binding,ownedQIdentity,ownedSkyIdentity,
            OriginalDeferredSamplerSources.ReadSelected383AmbientNativeSource(repository.SourceDirectory,executableLocale));
    }
    public OriginalDeferredSelected383SkyProjection PrepareCurrentQ80(OriginalDeferredF103SelectedSource selection,
        OriginalMapSourceCreatedLightingPacket lighting, int actualSelectedViewSlot, uint? actualView19B8,
        uint? actualSkyState168, bool? actualQ80PublishesThisSkyOwner, string currentProducerProvenance)
    {
        RequireSelection(selection,lighting);ArgumentException.ThrowIfNullOrWhiteSpace(currentProducerProvenance);
        if (actualSelectedViewSlot is not (0 or 6) || actualView19B8 is not uint viewMask ||
            actualSkyState168 is not uint state || actualQ80PublishesThisSkyOwner!=true)
            throw new InvalidDataException("Actual Q80 Sky selection requires its current slot0/6, known View19B8/state168 and actual nonnull owner publication.");
        if (viewMask==(1u<<actualSelectedViewSlot) || (state&0x40000000u)!=0)
            throw new NotSupportedException("F103 retains its earlier Envmap wrapper on this current View/Sky branch; Q80 cannot replace it.");
        if (lighting.FrameNumber<latestFrame) throw new InvalidDataException("Sky projection cannot republish an older owned frame.");
        latestFrame=lighting.FrameNumber;latestState=state;latestViewMask=viewMask;latestSlot=actualSelectedViewSlot;
        return new(this,selection,lighting,actualSelectedViewSlot,viewMask,state,source.SpecularTargets[(int)((state>>8)&3)],
            native+$";owned-Q={OwnedQIdentity};actual-Q80={OwnedSkyIdentity};"+currentProducerProvenance);
    }
    /// <summary>Consume the actual managed Main.v420 and Sky168 writers.
    /// Their private current receipts authorize CPU selection only; Q80
    /// publication and the GPU wrapper lease remain independent owners.</summary>
    public OriginalDeferredSelected383SkyProjection PrepareCurrentQ80(OriginalDeferredF103SelectedSource selection,
        OriginalMapSourceCreatedLightingPacket lighting, OriginalMainCameraView19B8Projection actualCameraWriter,
        OriginalSkyCpuState168Snapshot actualSkyWriter, bool? actualQ80PublishesThisSkyOwner,
        string currentProducerProvenance)
    {
        ArgumentNullException.ThrowIfNull(actualCameraWriter); ArgumentNullException.ThrowIfNull(actualSkyWriter);
        actualCameraWriter.ValidateCurrent(selection.Camera, lighting.FrameNumber, 0, lighting.Binding.CameraOwnerIdentity);
        actualSkyWriter.ValidateCurrent(OwnedSkyIdentity, lighting.FrameNumber);
        var projection = PrepareCurrentQ80(selection, lighting, 0, actualCameraWriter.CurrentView19B8,
            actualSkyWriter.State168, actualQ80PublishesThisSkyOwner,
            actualCameraWriter.CurrentProducerProvenance+";"+actualSkyWriter.SourceProvenance+";"+currentProducerProvenance);
        projection.CameraWriter=actualCameraWriter; projection.SkyWriter=actualSkyWriter;
        return projection;
    }
    internal void Validate(OriginalDeferredSelected383SkyProjection projection,
        OriginalDeferredF103SelectedSource selection, OriginalMapSourceCreatedLightingPacket lighting)
    {
        RequireSelection(selection,lighting);
        projection.CameraWriter?.ValidateCurrent(selection.Camera, lighting.FrameNumber, 0, lighting.Binding.CameraOwnerIdentity);
        projection.SkyWriter?.ValidateCurrent(OwnedSkyIdentity, lighting.FrameNumber);
        if (!ReferenceEquals(projection.Selection,selection) || projection.FrameNumber!=lighting.FrameNumber ||
            projection.Binding!=lighting.Binding || projection.LocalLightDataSha256!=Convert.ToHexString(SHA256.HashData(lighting.LocalLightData.Span)) ||
            latestFrame!=projection.FrameNumber || latestState!=projection.NativeSkyState168 ||
            latestViewMask!=projection.CurrentView19B8 || latestSlot!=projection.SelectedViewSlot ||
            projection.CubeSource!=source.SpecularTargets[(int)projection.SelectedSpecularIndex])
            throw new InvalidDataException("Ambient projection belongs to a different selection/current Q80 state or accepted light frame.");
    }
    private void RequireSelection(OriginalDeferredF103SelectedSource selection, OriginalMapSourceCreatedLightingPacket lighting)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(lighting);ValidateSource(source);
        selection.Validate();selection.OwnedFeatureSource?.ValidateFrame(selection.Camera,lighting);
        if (selection.OwnedFeatureSource is null || selection.Combination.Index!=510 || selection.PixelProgramIndex!=383 ||
            selection.PixelProgramSha256!=OriginalDeferredSelected383Constants.PixelProgramSha256 ||
            selection.Camera.ExecutableLocale!=locale || lighting.Binding!=binding || lighting.FrameNumber<=0 ||
            selection.Current.FrameNumber!=lighting.FrameNumber ||
            OriginalMapSelectedSourceImagePlan.Read(selection).RequireRole("AmbientSpecular").Declaration.BindPoint!=3)
            throw new InvalidDataException("Sky projection requires this owned View's actual mode7/510/PS383 source selection.");
    }
    private static void ValidateSource(LoadedOriginalSkyAmbientResources source)
    {
        if (source.Resource.Source.ResourceUri!="san14://rdb/system/DAB638B2" || source.Object.ObjectId!=0x910603AC ||
            source.Object.ClassId!=OriginalSkyAmbientResources.ClassId ||
            source.Resource.Source.DecodedSha256!="357F6A380CF19EB2A86B439AF24CF2BACDD38074AAF9BC3AE99F0EAD21EE9271" ||
            Convert.ToHexString(SHA256.HashData(source.Resource.Source.Bytes))!=source.Resource.Source.DecodedSha256 ||
            !source.SpecularTargets.Select(t=>t.Object.ObjectId).SequenceEqual(new uint[]{0x23F44EB5,0x498857B6,0x6F1C60B7,0x121611C4}))
            throw new InvalidDataException("Sky projection requires the exact original910603AC and its ordered four specular wrapper declarations.");
        foreach (var target in source.SpecularTargets)
            if (OriginalCubeRenderTextureResources.Read(target.Source,target.Object.ObjectId)!=target ||
                Convert.ToHexString(SHA256.HashData(target.Source.Source.Bytes))!=target.Source.Source.DecodedSha256)
                throw new InvalidDataException("Current Sky source cube metadata or original source bytes changed.");
    }
}
