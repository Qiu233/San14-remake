using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Current source Camera getter80/88/90 results at the native preparation boundary.</summary>
public readonly record struct MapCameraCurrentPose(Vector3 Position80,
    Quaternion Quaternion88, Vector3 Scale90);

/// <summary>A complete fresh CKIDSCamera prefix after source scalar copy and forced matrix update.</summary>
public sealed class MapCameraConstructionPacket
{
    private readonly byte[] prefix;
    internal MapCameraConstructionPacket(byte[] prefix,
        LoadedMapCameraConstructionSource source, MapCameraCurrentPose pose,
        CameraLens lens, MapCameraConstructionMatrices matrices,
        string executableLocale, long frameNumber, string frameProvenance,
        MapSeasonCameraPostEffectScalars? currentSeasonPostEffectScalars=null,
        MapSeasonCameraPostEffectScalarWriteIdentity? currentSeasonPostEffectWriteIdentity=null)
    {
        this.prefix = prefix; Source = source; CurrentPose = pose; Lens = lens; Matrices = matrices;
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
    public LoadedMapCameraConstructionSource Source { get; }
    public MapCameraCurrentPose CurrentPose { get; }
    public CameraLens Lens { get; }
    public MapCameraConstructionMatrices Matrices { get; }
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
    public uint HdrLutKey932=>CurrentSeasonPostEffectScalars?.HdrLutKey932??Source.CpuWord(0xEB8);
    public uint LdrLutKey933=>CurrentSeasonPostEffectScalars?.LdrLutKey933??Source.CpuWord(0xEBC);
    public void RequireEquivalentCurrentPostEffectScalars(MapCameraConstructionPacket actual)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if((CurrentSeasonPostEffectScalars is null)!=(actual.CurrentSeasonPostEffectScalars is null)||
            (CurrentSeasonPostEffectWriteIdentity is null)!=(actual.CurrentSeasonPostEffectWriteIdentity is null))
            throw new InvalidDataException("Camera current optics writer presence differs despite matching prefix.");
        CurrentSeasonPostEffectScalars?.RequireEquivalentWrites(actual.CurrentSeasonPostEffectScalars!);
        CurrentSeasonPostEffectWriteIdentity?.RequireEquivalentWrite(actual.CurrentSeasonPostEffectWriteIdentity!);
    }
    /// <summary>Copy the accepted current prefix before queued work and reuse
    /// its immutable verified loaded declaration. Pose, lens and matrices
    /// contain only value types.</summary>
    public MapCameraConstructionPacket CopyAcceptedSnapshot()
        => CopyAcceptedSnapshotForOwnedFrame(FrameNumber,FrameProvenance);

    /// <summary>Assign an already captured Camera to an independent renderer's
    /// accepted attempt. Copies all optics, matrices and retained prefix bytes;
    /// does not tick the controller, prepare the Camera again or write its
    /// retained FOV. The new identity belongs to the owned render attempt and
    /// does not attest execution of a frame in the original game.</summary>
    public MapCameraConstructionPacket CopyAcceptedSnapshotForOwnedFrame(
        long frameNumber,string frameProvenance)
    {
        if(frameNumber<0||string.IsNullOrWhiteSpace(frameProvenance))
            throw new InvalidDataException("Owned Camera snapshot requires an explicit render attempt and provenance.");
        var write=CurrentSeasonPostEffectWriteIdentity?.ForOwnedFrame(frameNumber,frameProvenance);
        return new(prefix.ToArray(),Source,CurrentPose,Lens,Matrices,ExecutableLocale,frameNumber,frameProvenance,
            write?.Scalars??CurrentSeasonPostEffectScalars?.ForOwnedFrame(frameNumber,frameProvenance),write);
    }
    public float Exposure170 => BinaryPrimitives.ReadSingleLittleEndian(prefix.AsSpan(0x170, 4));
    public float Luminance17C => BinaryPrimitives.ReadSingleLittleEndian(prefix.AsSpan(0x17C, 4));
    public MapCameraConstructionExtraScalars ExtraScalars
    {
        get
        {
            var result=MapCameraConstructionExtraScalars.FromSource(Source);
            if(CurrentSeasonPostEffectScalars is not{}current)return result;
            current.ValidateForCamera(this);
            float F(int at,float prior)=>current.TryReadWord(at,out uint word)?BitConverter.UInt32BitsToSingle(word):prior;
            int I(int at,int prior)=>current.TryReadWord(at,out uint word)?unchecked((int)word):prior;
            return result with{FxaaEdgeSensitivity404=F(0x404,result.FxaaEdgeSensitivity404),
                FxaaEdgeSensitivityMin408=F(0x408,result.FxaaEdgeSensitivityMin408),
                FxaaSharpness40C=F(0x40C,result.FxaaSharpness40C),FxaaPixelRange410=F(0x410,result.FxaaPixelRange410),
                LimbDarkeningWeight7B0=F(0x7B0,result.LimbDarkeningWeight7B0),
                DistantBlurZThresholdE78=F(0xE78,result.DistantBlurZThresholdE78),DistantBlurIntensityE7C=F(0xE7C,result.DistantBlurIntensityE7C),
                Lut1Type13B0=I(0x13B0,result.Lut1Type13B0),Lut2Type13B4=I(0x13B4,result.Lut2Type13B4),
                LutInterpolation13B8=F(0x13B8,result.LutInterpolation13B8),LutIntensity13BC=F(0x13BC,result.LutIntensity13BC)};
        }
    }
    /// <summary>Separate exact source-to-prepared copies outside the returned3C0 prefix.</summary>
    public uint SourceSpecularOcclusionType3C0 => CurrentSeasonPostEffectScalars is{}current&&current.TryReadWord(0x3C0,out uint word)?word:Source.CpuWord(0x164);
    public uint SourceTemporalAaEnable133C => CurrentSeasonPostEffectScalars is{}current&&current.TryReadWord(0x133C,out uint word)?word:Source.CpuWord(0x1088);
    public uint SourceSpecialRectangleFlag1510 =>
        CurrentSeasonPostEffectWriteIdentity?.PreparedSpecialRectangleFlag1510??Source.CpuWord(0x1240);
}

/// <summary>Fresh source Camera constructor and ordinary Perspective preparation.
/// Source CPU C3D610 initializes embedded Camera18C0 at +1330; C42070 copies
/// source fields and getters, then C45B30 forces AB6C60's four matrix writes.
/// Current pose, context flags, clip profile and frame identity are explicit.</summary>
public static class MapCameraConstruction
{
    public const int PrefixByteLength = 0x3C0;
    public const string ContractId = "ce-20261002-map-camera-fresh-construction";

    /// <summary>Getter80/88/90 values immediately after the source constructor
    /// and property loader, before any parent/cache binding or runtime edits.
    /// Constructor C3F946 clears the parent cache; getter90 then returns literal
    /// unit scale. The source WorldScale field is not that getter's fallback.</summary>
    public static MapCameraCurrentPose ConstructFreshSourcePose(
        LoadedMapCameraConstructionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(new(Float(source, 0x12FC), Float(source, 0x1300), Float(source, 0x1304)),
            new(Float(source, 0x1308), Float(source, 0x130C), Float(source, 0x1310), Float(source, 0x1314)),
            Vector3.One);
    }

    /// <summary>The exact fresh embedded Camera prefix before source preparation.
    /// The concrete original source CPU constructor clears18C0, installs the
    /// Project perspective type tag and projection dirty161. Perspective bit0 is clear.</summary>
    public static byte[] ConstructFreshPerspectivePrefix(string executableLocale)
    {
        _ = MainMapCameraRules.Language(executableLocale);
        byte[] result = new byte[PrefixByteLength];
        BinaryPrimitives.WriteUInt64LittleEndian(result, (ulong)RenderStateRecordKind.PerspectiveCamera);
        result[0x161] = 1;
        return result;
    }

    /// <summary>Perspective loader C59762 writes this initial lighting state
    /// immediately before its embedded Camera C46580 call. The caller retains
    /// all other current fields and selects the lighting producer's inputs.</summary>
    public static byte[] WritePerspectiveLoaderInitialLightingState(ReadOnlySpan<byte> completePrefix)
    {
        if (completePrefix.Length != PrefixByteLength)
            throw new InvalidDataException("Source loader lighting initialization requires a complete3C0 Camera prefix.");
        byte[] result = completePrefix.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x3B0, 4), 0xFFFFFFFFu);
        return result;
    }

    /// <summary>Uses the original camera-domain tangent polynomial with an explicit native dispatch.</summary>
    public static MapCameraConstructionPacket PrepareFreshPerspective(
        LoadedMapCameraConstructionSource source, MapCameraCurrentPose currentPose,
        bool contextOwner3E4Bit29, uint nativeClipProfile, NativeCameraTangentDispatch tangentDispatch,
        string executableLocale, long frameNumber, string frameProvenance)
    {
        ArgumentNullException.ThrowIfNull(source);
        float tangent = NativeCameraTangent.Tangent1020A90(
            M(Float(source, 0x12E8), .5f), tangentDispatch);
        return PrepareFreshPerspectiveFromTangent(source, currentPose, contextOwner3E4Bit29,
            nativeClipProfile, tangent, executableLocale, frameNumber, frameProvenance);
    }

    /// <summary>Recomputes the four native matrix slots from complete current
    /// camera fields, preserving the caller's current lighting, exposure and
    /// transition bytes. Current drag/zoom edits are published by the caller
    /// into eye10, target20, up30 and lens140..15C before this forced update.</summary>
    public static byte[] UpdateMatricesForCurrentPerspectivePrefix(ReadOnlySpan<byte> currentPrefix,
        uint nativeClipProfile, NativeCameraTangentDispatch tangentDispatch)
    {
        if (currentPrefix.Length != PrefixByteLength)
            throw new InvalidDataException("Current source Camera matrix update requires a complete3C0 prefix.");
        byte[] result = currentPrefix.ToArray();
        var lens = CameraLens.DecodePrefix(currentPrefix[..CameraLens.PrefixBytes]);
        var matrices = MapCameraConstructionMath.CompleteAB6C60(
            ReadVector(currentPrefix, 0x10), ReadVector(currentPrefix, 0x20), ReadVector(currentPrefix, 0x30),
            lens, nativeClipProfile, tangentDispatch);
        matrices.WriteEarlyMatrices(result);
        result[0x160] = result[0x161] = 0;
        return result;
    }

    /// <summary>Ordinary C42070/C45B30 preparation. Target's multiplier is the
    /// unchanged current scale.z. The optional LensPreset call writes separate
    /// post-effect outputs and cannot mutate the pose inputs or early matrices.</summary>
    public static MapCameraConstructionPacket PrepareFreshPerspectiveFromTangent(
        LoadedMapCameraConstructionSource source, MapCameraCurrentPose currentPose,
        bool contextOwner3E4Bit29,
        uint nativeClipProfile, float originalHalfFovTangent,
        string executableLocale, long frameNumber, string frameProvenance)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (frameNumber < 0 || string.IsNullOrWhiteSpace(frameProvenance))
            throw new InvalidDataException("Prepared source Camera requires an explicit frame number and provenance.");
        ValidatePose(currentPose);
        byte[] result = ConstructFreshPerspectivePrefix(executableLocale);
        CopyWord(result, 0x150, source, 0x18); CopyWord(result, 0x15C, source, 0x1C);
        for (int offset = 0x20; offset <= 0x78; offset += 4)
            CopyWord(result, offset + 0x150, source, offset);
        for (int offset = 0x7C; offset <= 0xD4; offset += 4)
            CopyWord(result, offset + 0x154, source, offset);
        for (int offset = 0xD8; offset <= 0x150; offset += 4)
            CopyWord(result, offset + 0x158, source, offset);
        for (int offset = 0x154; offset <= 0x160; offset += 4)
            CopyWord(result, offset + 0x24C, source, offset);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x1CC, 4), 0xFFFFFFFFu);
        var eye = new Vector4(currentPose.Position80, 1f);
        // The helper retains raw source XYZW and the original quaternion-vector
        // normalization; native setters do not normalize the stored quaternion.
        var vectors = MapCameraConstructionMath.PoseFromQuaternionC45956(
            eye, currentPose.Quaternion88, Float(source, 0x0C), currentPose.Scale90.Z);
        WriteVector(result, 0x10, eye);
        WriteVector(result, 0x20, vectors.Target20);
        WriteVector(result, 0x30, vectors.Up30);
        var lens = new CameraLens(M(currentPose.Scale90.Z, Float(source, 0x10)),
            M(currentPose.Scale90.Z, Float(source, 0x14)), Float(source, 0x12E8),
            M(D(currentPose.Scale90.X, currentPose.Scale90.Y), Float(source, 0x12EC)),
            Float(source, 0x18), 0f, contextOwner3E4Bit29 ? 2u : 0u);
        WriteFloat(result, 0x140, lens.Near); WriteFloat(result, 0x144, lens.Far);
        WriteFloat(result, 0x148, lens.VerticalFovRadians); WriteFloat(result, 0x14C, lens.Aspect);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x158, 4), lens.Flags);
        var matrices = MapCameraConstructionMath.CompleteFromTangentAB6C60(
            eye, vectors.Target20, vectors.Up30, lens, nativeClipProfile, originalHalfFovTangent);
        matrices.WriteEarlyMatrices(result);
        result[0x160] = result[0x161] = 0; // AB6C60 forced update owns both dirty bytes.
        return new(result, source, currentPose, lens, matrices,
            executableLocale, frameNumber, frameProvenance);
    }

    private static void ValidatePose(MapCameraCurrentPose pose)
    {
        foreach (float x in new[] { pose.Position80.X, pose.Position80.Y, pose.Position80.Z,
            pose.Quaternion88.X, pose.Quaternion88.Y, pose.Quaternion88.Z, pose.Quaternion88.W,
            pose.Scale90.X, pose.Scale90.Y, pose.Scale90.Z })
            if (!float.IsFinite(x)) throw new InvalidDataException("Current source Camera getter pose requires finite explicit fields.");
    }
    private static float Float(LoadedMapCameraConstructionSource source, int offset)
        => BitConverter.UInt32BitsToSingle(source.CpuWord(offset));
    private static void CopyWord(Span<byte> destination, int target,
        LoadedMapCameraConstructionSource source, int sourceOffset)
        => BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(target, 4), source.CpuWord(sourceOffset));
    private static void WriteFloat(Span<byte> bytes, int offset, float value)
        => BinaryPrimitives.WriteSingleLittleEndian(bytes.Slice(offset, 4), value);
    private static void WriteVector(Span<byte> bytes, int offset, Vector4 value)
    {
        WriteFloat(bytes, offset, value.X); WriteFloat(bytes, offset + 4, value.Y);
        WriteFloat(bytes, offset + 8, value.Z); WriteFloat(bytes, offset + 12, value.W);
    }
    private static Vector4 ReadVector(ReadOnlySpan<byte> bytes, int offset) => new(
        BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset + 4, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset + 8, 4)),
        BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset + 12, 4)));
    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
