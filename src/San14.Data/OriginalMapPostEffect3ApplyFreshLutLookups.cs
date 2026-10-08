using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Fresh original source Camera lookup construction, with no later
/// resource/per-view/LensPreset writer beyond an accepted source season copy.
/// Current raw LUT keys require their actual source lookup, including failure;
/// the two secondary handles are explicitly cleared by CAAEE0. LensPreset is
/// the separate property285 lookup; its successful resolution or explicit
/// failure is required before prepared1810 presence is established.</summary>
public sealed class OriginalMapPostEffect3ApplyFreshLutLookups
{
    private readonly OriginalMapCameraConstructionPacket camera;
    private readonly LoadedOriginalMapPostEffect3LensPresetSource? lensPreset;
    private readonly OriginalMapPostEffect3CurrentLutLookup? hdrLookup;
    private readonly OriginalMapPostEffect3CurrentLutLookup? ldrLookup;
    private readonly string lookupEvidence;
    private OriginalMapPostEffect3ApplyFreshLutLookups(OriginalMapCameraConstructionPacket snapshot,string owner,
        LoadedOriginalMapPostEffect3LensPresetSource? preset,string evidence,
        OriginalMapPostEffect3CurrentLutLookup? actualHdrLookup,OriginalMapPostEffect3CurrentLutLookup? actualLdrLookup)
    {camera=snapshot;OwnerIdentity=owner;lensPreset=preset;lookupEvidence=evidence;hdrLookup=actualHdrLookup;ldrLookup=actualLdrLookup;}
    public long FrameNumber=>camera.FrameNumber;
    public uint CameraObjectId=>camera.Source.Camera.ObjectId;
    public string OwnerIdentity{get;}
    public uint SourceHdrLutKey932=>camera.HdrLutKey932;
    public uint SourceLdrLutKey933=>camera.LdrLutKey933;
    public uint SourceLensPresetKey285=>0x3D60C3C7;
    public LoadedOriginalMapPostEffect3LensPresetSource? ResolvedLensPreset=>lensPreset;
    /// <summary>Successful or explicitly failed selected-camera cache lookups.
    /// Prepared presence also requires the actual seasonal Main transfer.</summary>
    public OriginalMapPostEffect3CurrentLutLookup? ResolvedHdrLookup=>hdrLookup;
    public OriginalMapPostEffect3CurrentLutLookup? ResolvedLdrLookup=>ldrLookup;
    /// <summary>C57BE6 CPU2EC0 -> CAB236/CAB23D Camera1810. The native
    /// LensPresetSplit/Intermediate writer consumes this resource handle;
    /// property285/hashA4541BE1 is PostEffect3LensPreset. RBP+3C means
    /// canonicalRSP+13C, not the unrelated property957 slot at RSP+3C.</summary>
    public bool FreshLensPreset1810Present=>lensPreset is not null;
    public bool HdrPrimary1828Present=>PreparedPresence(SourceHdrLutKey932,hdrLookup,"HDR1828");
    public bool HdrSecondary1838Present=>false;
    public bool LdrPrimary1830Present=>PreparedPresence(SourceLdrLutKey933,ldrLookup,"LDR1830");
    public bool LdrSecondary1840Present=>false;
    /// <summary>A source 593 handle is not the selected A0 texture or the
    /// created controller. Positive HDR needs its own caller-side producer.</summary>
    public bool FreshControllerPresent190=>!HdrPrimary1828Present&&!LdrPrimary1830Present?false:
        throw new NotSupportedException("A prepared593 handle is present. Supply the actual owned selected A0/controller route; prepared presence does not create a controller.");
    public string SourceEvidence=>camera.Source.ConstructorOrigin+
        $";TC:C57BF0/C57C30:accepted932-key={SourceHdrLutKey932:X8};"+
        "C57BA6-RBP3C=RSP13C:285/A4541BE1-key3D60C3C7;C57BE6=>CPU2EC0;CAB236/CAB23D=>Camera1810;"+
        $"C57C3A/C57C7A:accepted933-key={SourceLdrLutKey933:X8};"+
        "CAB260/CAB267:selected-source2EC8-candidate=>Main2B58=Camera1828;CAB26E/CAB275:selected-source2ED0-candidate=>Main2B60=Camera1830;"+
        (hdrLookup?.ProvesPreparedCameraHandleTransfer==true||ldrLookup?.ProvesPreparedCameraHandleTransfer==true?
            "typed-actual-Main17BD0-nonnegative-slot=>CAAEE0-no-prior-primary-restore;":"prepared-transfer-not-joined;")+
        "CAB27C/CAB27E/CAB285:secondary2B68/2B70=Camera1838/1840-cleared;"+
        "current-nonzero-keys-require-owned593-lookup-receipts;key0-source-lookup-null;"+
        "positive-HDR-handle-does-not-infer-A0/controller;fresh-owned-secondary-handles-cleared;"+
        (hdrLookup?.SourceEvidence??"source-HDR-key0-null")+";"+(ldrLookup?.SourceEvidence??"source-LDR-key0-null")+";"+
        lookupEvidence+";"+OwnerIdentity;

    public static OriginalMapPostEffect3ApplyFreshLutLookups ConstructFreshSource(
        OriginalMapCameraConstructionPacket currentCamera,string actualLookupOwner)
        =>throw new NotSupportedException("Fresh LensPreset285 is nonzero: provide its resolved source or an explicit failed owned lookup.");
    public static OriginalMapPostEffect3ApplyFreshLutLookups ConstructFreshSource(
        OriginalMapCameraConstructionPacket currentCamera,string actualLookupOwner,
        LoadedOriginalMapPostEffect3LensPresetSource actualPreset)
        =>ConstructFreshSource(currentCamera,actualLookupOwner,actualPreset,null,null);
    public static OriginalMapPostEffect3ApplyFreshLutLookups ConstructFreshSource(
        OriginalMapCameraConstructionPacket currentCamera,string actualLookupOwner,
        LoadedOriginalMapPostEffect3LensPresetSource actualPreset,
        OriginalMapPostEffect3CurrentLutLookup? actualHdrLookup,OriginalMapPostEffect3CurrentLutLookup? actualLdrLookup)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);ArgumentException.ThrowIfNullOrWhiteSpace(actualLookupOwner);
        ArgumentNullException.ThrowIfNull(actualPreset);
        if(currentCamera.FrameNumber<=0)throw new InvalidDataException("Fresh PostEffect3 lookup requires an owned Camera frame.");
        var snapshot=currentCamera.CopyAcceptedSnapshot();
        ValidateKeys(snapshot);ValidateLutLookups(snapshot,actualHdrLookup,actualLdrLookup);
        if(actualPreset.ExecutableLocale!=snapshot.ExecutableLocale)
            throw new InvalidDataException("LensPreset native constructor source belongs to another Camera executable locale.");
        actualPreset.ValidateForCamera(snapshot.Source);
        return new(snapshot,actualLookupOwner,actualPreset,"successful-owned-source-LensPreset-resolution;"+actualPreset.SourceEvidence,
            actualHdrLookup,actualLdrLookup);
    }
    public static OriginalMapPostEffect3ApplyFreshLutLookups ConstructAfterOwnedFailedLensLookup(
        OriginalMapCameraConstructionPacket currentCamera,string actualLookupOwner,string actualFailedLookupEvidence)
        =>ConstructAfterOwnedFailedLensLookup(currentCamera,actualLookupOwner,actualFailedLookupEvidence,null,null);
    public static OriginalMapPostEffect3ApplyFreshLutLookups ConstructAfterOwnedFailedLensLookup(
        OriginalMapCameraConstructionPacket currentCamera,string actualLookupOwner,string actualFailedLookupEvidence,
        OriginalMapPostEffect3CurrentLutLookup? actualHdrLookup,OriginalMapPostEffect3CurrentLutLookup? actualLdrLookup)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);ArgumentException.ThrowIfNullOrWhiteSpace(actualLookupOwner);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualFailedLookupEvidence);
        if(currentCamera.FrameNumber<=0)throw new InvalidDataException("Failed LensPreset lookup requires an owned Camera frame.");
        var snapshot=currentCamera.CopyAcceptedSnapshot();ValidateKeys(snapshot);ValidateLutLookups(snapshot,actualHdrLookup,actualLdrLookup);
        return new(snapshot,actualLookupOwner,null,
            "explicit-owned-LensPreset285-lookup-failed-C57BA3-null-retained;"+actualFailedLookupEvidence,actualHdrLookup,actualLdrLookup);
    }
    public void ValidateSameCamera(OriginalMapCameraConstructionPacket currentCamera)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);
        if(currentCamera.FrameNumber!=FrameNumber||currentCamera.Source.Camera.ObjectId!=CameraObjectId||
            currentCamera.ExecutableLocale!=camera.ExecutableLocale||
            currentCamera.Source.Resource.Source.ResourceUri!=camera.Source.Resource.Source.ResourceUri||
            currentCamera.Source.Resource.Source.DecodedSha256!=camera.Source.Resource.Source.DecodedSha256||
            currentCamera.HdrLutKey932!=SourceHdrLutKey932||currentCamera.LdrLutKey933!=SourceLdrLutKey933||
            !currentCamera.Prefix.Span.SequenceEqual(camera.Prefix.Span))
            throw new InvalidDataException("Fresh Camera lookup receipt belongs to another Camera/frame/prefix.");
        camera.RequireEquivalentCurrentPostEffectScalars(currentCamera);
        ValidateKeys(currentCamera);ValidateLutLookups(currentCamera,hdrLookup,ldrLookup);
        lensPreset?.ValidateForCamera(currentCamera.Source);
    }
    private static void ValidateKeys(OriginalMapCameraConstructionPacket camera)
    {
        var source=OriginalMapCameraConstructionSource.ReadPerspective(camera.Source.Resource,camera.Source.Camera.ObjectId);
        foreach(var (ordinal,hash) in new[]{(932,0x2572A4E9u),(933,0xDF7EBEEDu)})
        {
            var field=source.Camera.Properties[ordinal];
            if(field.Index!=ordinal||field.NameHash!=hash||field.TypeCode!=5||field.ElementCount!=1||
                field.ElementSize!=4||field.RawValue.Length!=4)
                throw new InvalidDataException("Source PostEffect3 LUT declaration differs from its original loader shape.");
        }
        var preset=source.Camera.Properties[285];
        if(preset.Index!=285||preset.NameHash!=0xA4541BE1||preset.TypeCode!=5||preset.ElementCount!=1||
            preset.ElementSize!=4||preset.RawValue.Length!=4||BinaryPrimitives.ReadUInt32LittleEndian(preset.RawValue)!=0x3D60C3C7)
            throw new InvalidDataException("Source LensPreset requires exact property285/A4541BE1/key3D60C3C7.");
    }
    private static void ValidateLutLookups(OriginalMapCameraConstructionPacket camera,
        OriginalMapPostEffect3CurrentLutLookup? hdr,OriginalMapPostEffect3CurrentLutLookup? ldr)
    {
        void Require(uint key,OriginalMapPostEffect3CurrentLutLookup? lookup,string kind,bool expectedHdr)
        {
            if(lookup is null)
            {
                if(key!=0)throw new NotSupportedException($"Current {kind} LUT key {key:X8} requires its actual owned source593 lookup; constructor key0 is not current absence.");
                return;
            }
            lookup.ValidateSameCamera(camera);
            if(lookup.SourceObjectKey!=key||lookup.FrameNumber!=camera.FrameNumber||lookup.IsHdrLookup!=expectedHdr||
                key==0&&lookup.ResolvedDeclaration is not null)
                throw new InvalidDataException("Current LUT lookup belongs to another accepted raw key/frame.");
        }
        Require(camera.HdrLutKey932,hdr,"HDR",true);Require(camera.LdrLutKey933,ldr,"LDR",false);
    }
    private static bool PreparedPresence(uint key,OriginalMapPostEffect3CurrentLutLookup? lookup,string slot)
    {
        if(key==0)return false;
        if(lookup is null||!lookup.ProvesPreparedCameraHandleTransfer)
            throw new NotSupportedException($"Current {slot} presence needs its owned source593 lookup joined with actual Main17BD0 seasonal preparation.");
        if(!lookup.LookupSucceeded&&!lookup.LookupExplicitlyFailed)
            throw new InvalidDataException("Prepared LUT transfer has no successful or explicitly failed source lookup.");
        return lookup.LookupSucceeded;
    }
}
