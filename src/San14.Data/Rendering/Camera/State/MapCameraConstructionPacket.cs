using System.Numerics;

namespace San14.Data;

/// <summary>A prepared camera frame. Geometry and optics own their values;
/// Prefix is the renderer ABI output, including retained fields from other stages.</summary>
public sealed class MapCameraConstructionPacket
{
    private readonly byte[] prefix;
    private MapCameraConstructionPacket(MapCameraConstructionPacket accepted, long frameNumber,
        string frameProvenance, MapSeasonCameraPostEffectScalars? scalars,
        MapSeasonCameraPostEffectScalarWriteIdentity? write)
    {
        prefix = accepted.prefix; Source = accepted.Source; Geometry = accepted.Geometry; Optics = accepted.Optics;
        ExecutableLocale = accepted.ExecutableLocale; FrameNumber = frameNumber; FrameProvenance = frameProvenance;
        CurrentSeasonPostEffectScalars = scalars; CurrentSeasonPostEffectWriteIdentity = write;
    }
    internal MapCameraConstructionPacket(byte[] prefix,
        LoadedMapCameraConstructionSource source, MapCameraGeometry geometry,
        string executableLocale, long frameNumber, string frameProvenance,
        MapSeasonCameraPostEffectScalars? currentSeasonPostEffectScalars=null,
        MapSeasonCameraPostEffectScalarWriteIdentity? currentSeasonPostEffectWriteIdentity=null,
        CameraOpticsSettings? optics=null)
    {
        Geometry = geometry;
        Optics = optics ?? CameraOpticsSettings.FromSource(source);
        CameraGeometryEncoding.Write(prefix, geometry);
        CameraOpticsEncoding.WritePrefix(Optics, prefix);
        // Own the encoded output once. Accepted snapshots share this private
        // immutable allocation, never a caller's mutable construction buffer.
        this.prefix = prefix.ToArray(); Source = source;
        ExecutableLocale = executableLocale;
        FrameNumber = frameNumber; FrameProvenance = frameProvenance;
        CurrentSeasonPostEffectScalars=currentSeasonPostEffectScalars;
        CurrentSeasonPostEffectWriteIdentity=currentSeasonPostEffectWriteIdentity;
        if(currentSeasonPostEffectWriteIdentity is{}write&&
            !ReferenceEquals(currentSeasonPostEffectScalars,write.Scalars))
            throw new InvalidDataException("Camera current optics words differ from its native writer receipt.");
        currentSeasonPostEffectScalars?.ValidateForCamera(this);
    }
    public ReadOnlyMemory<byte> Prefix => prefix;
    internal bool SharesEncodedOutputWith(MapCameraConstructionPacket other)
        => ReferenceEquals(prefix, other.prefix);
    public LoadedMapCameraConstructionSource Source { get; }
    public MapCameraGeometry Geometry { get; }
    public CameraOpticsSettings Optics { get; }
    public DeferredCameraInputs SceneInputs => Geometry.SceneInputs;
    public MapCameraCurrentPose CurrentPose => Geometry.Pose;
    public CameraLens Lens => Geometry.Lens;
    public MapCameraConstructionMatrices Matrices => Geometry.Matrices;
    public Matrix4x4 SourceWorldToView => Matrices.WorldToView80;
    public Matrix4x4 SourceProjection => Matrices.ProjectionC0;
    public Matrix4x4 SourceWorldToProjection => Matrices.WorldToProjection100;
    public string ExecutableLocale { get; }
    public long FrameNumber { get; }
    public string FrameProvenance { get; }
    public MapSeasonCameraPostEffectScalars? CurrentSeasonPostEffectScalars{get;}
    public MapSeasonCameraPostEffectScalarWriteIdentity? CurrentSeasonPostEffectWriteIdentity{get;}
    /// <summary>Current source raw keys after the accepted V468 direct writer,
    /// or the original Main declaration if no such writer ran. Neither key is
    /// a resolved resource pointer or proof of lookup success/absence.</summary>
    public uint HdrLutKey932=>Optics.ColorGrading.HdrLutKey;
    public uint LdrLutKey933=>Optics.ColorGrading.LdrLutKey;
    public void RequireEquivalentCurrentPostEffectScalars(MapCameraConstructionPacket actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if((CurrentSeasonPostEffectScalars is null)!=(actual.CurrentSeasonPostEffectScalars is null)||
            (CurrentSeasonPostEffectWriteIdentity is null)!=(actual.CurrentSeasonPostEffectWriteIdentity is null))
            throw new InvalidDataException("Camera current optics writer presence differs despite matching prefix.");
        CurrentSeasonPostEffectScalars?.RequireEquivalentWrites(actual.CurrentSeasonPostEffectScalars!);
        CurrentSeasonPostEffectWriteIdentity?.RequireEquivalentWrite(actual.CurrentSeasonPostEffectWriteIdentity!);
    }
    /// <summary>Capture accepted immutable values for queued work.</summary>
    public MapCameraConstructionPacket CopyAcceptedSnapshot()
        => CopyAcceptedSnapshotForOwnedFrame(FrameNumber,FrameProvenance);

    /// <summary>Assign an already captured Camera to an independent renderer's
    /// accepted attempt. Shares immutable optics, geometry and encoded output;
    /// does not tick the controller, prepare the Camera again or write its
    /// retained FOV. The new identity belongs to the owned render attempt and
    /// does not attest execution of a frame in the original game.</summary>
    public MapCameraConstructionPacket CopyAcceptedSnapshotForOwnedFrame(
        long frameNumber,string frameProvenance)
    {
        if(frameNumber<0||string.IsNullOrWhiteSpace(frameProvenance))
            throw new InvalidDataException("Owned Camera snapshot requires an explicit render attempt and provenance.");
        var write=CurrentSeasonPostEffectWriteIdentity?.ForOwnedFrame(frameNumber,frameProvenance);
        return new(this,frameNumber,frameProvenance,
            write?.Scalars??CurrentSeasonPostEffectScalars?.ForOwnedFrame(frameNumber,frameProvenance),write);
    }
    public float Exposure170 => Optics.Exposure.UserScale;
    public float Luminance17C => Optics.Exposure.Luminance;
    /// <summary>Separate exact source-to-prepared copies outside the returned3C0 prefix.</summary>
    public uint SourceSpecularOcclusionType3C0 => Optics.Surface.SpecularOcclusionType;
    public uint SourceTemporalAaEnable133C => Optics.TemporalAa.Enable;
    public uint SourceReflectionProbeRsmCaptureMode15A0 => Optics.ReflectionProbeRsmCaptureMode;
    public uint SourceSpecialRectangleFlag1510 =>
        CurrentSeasonPostEffectWriteIdentity?.PreparedSpecialRectangleFlag1510??CameraDeclarationDefaults.InitialExposureRectangleFlags;
}
