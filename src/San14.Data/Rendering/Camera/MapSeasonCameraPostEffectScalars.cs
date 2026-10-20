using System.Collections.ObjectModel;

namespace San14.Data;

/// <summary>One selected optical update and its independent caller controls. Optical parameters have no CPU offsets.</summary>
public sealed partial class MapSeasonCameraPostEffectScalars
{
    private readonly string selectionEvidence;
    private MapSeasonCameraPostEffectScalars(MapCameraConstructionPacket camera,
        CameraOpticsPatch optics, string evidence, CameraRenderQualityPatch quality,
        VegetationWindSettings vegetationWind)
    {
        FrameNumber = camera.FrameNumber; MainCameraObjectId = camera.Source.Camera.ObjectId;
        ExecutableLocale = camera.ExecutableLocale;
        MainResourceUri = camera.Source.Resource.Source.ResourceUri;
        MainResourceSha256 = camera.Source.Resource.Source.DecodedSha256;
        OpticsPatch = optics; QualityPatch = quality; VegetationWind = vegetationWind;
        SourceEvidence = evidence; selectionEvidence = evidence;
    }
    private MapSeasonCameraPostEffectScalars(MapSeasonCameraPostEffectScalars prior, long frame, string evidence)
    {
        FrameNumber = frame; MainCameraObjectId = prior.MainCameraObjectId; ExecutableLocale = prior.ExecutableLocale;
        MainResourceUri = prior.MainResourceUri; MainResourceSha256 = prior.MainResourceSha256;
        OpticsPatch = prior.OpticsPatch; QualityPatch = prior.QualityPatch; VegetationWind = prior.VegetationWind;
        SourceEvidence = prior.SourceEvidence + ";" + evidence; selectionEvidence = prior.selectionEvidence;
    }
    public long FrameNumber { get; }
    public uint MainCameraObjectId { get; }
    public string ExecutableLocale { get; }
    public string MainResourceUri { get; }
    public string MainResourceSha256 { get; }
    public string SourceEvidence { get; }
    public CameraOpticsPatch OpticsPatch { get; }
    public CameraRenderQualityPatch QualityPatch { get; }
    public VegetationWindSettings VegetationWind { get; }
    // Existing Native packet users consume this encoding boundary, not a second parameter owner.
    public IReadOnlyList<int> PreparedCameraOffsets => Array.AsReadOnly(EncodedWrites.Select(pair => pair.Key).ToArray());
    public IEnumerable<KeyValuePair<int, uint>> EncodedWrites => CameraOpticsEncoding.WrittenWords(OpticsPatch)
        .Concat(CameraRenderQualityEncoding.WrittenWords(QualityPatch))
        .Concat(CameraVegetationWindEncoding.WrittenWords(VegetationWind))
        .OrderBy(pair => pair.Key);
    public uint HdrLutKey932 => OpticsPatch.ColorGradingHdrLutKey!.Value;
    public uint LdrLutKey933 => OpticsPatch.ColorGradingLdrLutKey!.Value;
    public bool TryReadWord(int encodedOffset, out uint value)
    {
        if (CameraOpticsEncoding.TryRead(OpticsPatch, encodedOffset, out value)) return true;
        foreach (var pair in CameraRenderQualityEncoding.WrittenWords(QualityPatch))
            if (pair.Key == encodedOffset) { value = pair.Value; return true; }
        foreach (var pair in CameraVegetationWindEncoding.WrittenWords(VegetationWind))
            if (pair.Key == encodedOffset) { value = pair.Value; return true; }
        value = 0; return false;
    }
    /// <summary>The attached tail may be newer than this camera's geometry/prefix phase. Prefix optical values must retain their own stage.</summary>
    public CameraOpticsSettings ApplyOpticalTailTo(CameraOpticsSettings current) => current.Apply(OpticsPatch.ForPreparedTail());
    public static MapSeasonCameraPostEffectScalars CaptureDirectSelection(
        MapSeasonCameraSelection selection, MapCameraConstructionPacket acceptedMain)
    {
        ArgumentNullException.ThrowIfNull(selection); ArgumentNullException.ThrowIfNull(acceptedMain);
        selection.Validate();
        if (acceptedMain.ExecutableLocale is not ("TC" or "SC") || acceptedMain.FrameNumber < 1 ||
            acceptedMain.Source.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId || selection.SecondPackage is not null)
            throw new NotSupportedException("Season optics require an accepted primary camera and a directly selected package.");
        var resource = selection.FirstPackage.Objects.Source;
        string identity = $"selected-camera-optics/{resource.ResourceUri}#{selection.FirstPackage.Camera.ObjectId:X8}@{resource.DecodedSha256};" +
            $"{selection.SelectionPolicy}/{selection.FirstIndex}/{BitConverter.SingleToUInt32Bits(selection.Weight):X8}";
        return new(acceptedMain, CameraOpticsImport.Direct(selection.FirstPackage), identity,
            CameraShadowBiasImport.Direct(selection.FirstPackage), CameraVegetationWindImport.Direct(selection.FirstPackage));
    }
    public void ValidateForCamera(MapCameraConstructionPacket camera)
    {
        if (camera.FrameNumber != FrameNumber || camera.ExecutableLocale != ExecutableLocale ||
            camera.Source.Camera.ObjectId != MainCameraObjectId || camera.Source.Resource.Source.ResourceUri != MainResourceUri ||
            camera.Source.Resource.Source.DecodedSha256 != MainResourceSha256)
            throw new InvalidDataException("Season optics belong to another camera frame.");
    }
    internal void RequireEquivalentWrites(MapSeasonCameraPostEffectScalars actual)
    {
        if (FrameNumber != actual.FrameNumber || ExecutableLocale != actual.ExecutableLocale ||
            MainCameraObjectId != actual.MainCameraObjectId || MainResourceUri != actual.MainResourceUri ||
            MainResourceSha256 != actual.MainResourceSha256 || selectionEvidence != actual.selectionEvidence ||
            !EncodedWrites.SequenceEqual(actual.EncodedWrites))
            throw new InvalidDataException("Accepted camera has another optical update.");
    }
    internal MapSeasonCameraPostEffectScalars ForOwnedFrame(long frame, string evidence) => new(this, frame, evidence);
    internal MapSeasonCameraPostEffectScalars WithMainPostSelection(MapMainCameraPostSelectionPacket post, MapCameraConstructionPacket camera)
    {
        ValidateForCamera(camera); post.ValidateSameMain(camera);
        return new(camera, OpticsPatch.Then(post.OpticsPatch), selectionEvidence + ";main-post-settings", QualityPatch.Then(post.QualityPatch), VegetationWind);
    }
    internal MapSeasonCameraPostEffectScalars WithActualMainPhase(SkySeasonMainCameraInputs inputs, MapCameraConstructionPacket camera)
    {
        ValidateForCamera(camera);
        var phase = CameraRenderQualityEncoding.Import(new[] { 0x1C4, 0x1C8, 0x1D0 }
            .Select(at => new KeyValuePair<int, uint>(at, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(inputs.BeforeFinalPreparationPrefix.Span.Slice(at, 4)))));
        return new(camera, OpticsPatch, selectionEvidence + ";main-phase", QualityPatch.Then(phase), VegetationWind);
    }
    public MapCameraConstructionPacket ApplyToSameAcceptedCamera(MapCameraConstructionPacket camera)
    {
        ValidateForCamera(camera);
        return new(camera.Prefix.ToArray(), camera.Source, camera.Geometry, camera.ExecutableLocale,
            camera.FrameNumber, camera.FrameProvenance, this, null, ApplyOpticalTailTo(camera.Optics));
    }
}
