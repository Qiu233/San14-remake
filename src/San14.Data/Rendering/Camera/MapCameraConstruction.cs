using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Build a perspective frame from an imported declaration and the
/// controller's current pose, clip profile and frame identity.</summary>
public static class MapCameraConstruction
{
    public const int PrefixByteLength = 0x3C0;
    public const string ContractId = "ce-20261002-map-camera-fresh-construction";

    /// <summary>Initial unparented transform. The declaration supplies position
    /// and quaternion; world scale begins at one independently of its saved local scale.</summary>
    public static MapCameraCurrentPose ConstructFreshSourcePose(
        LoadedMapCameraConstructionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Declaration.Perspective.Pose;
    }

    /// <summary>Initialize the camera ABI payload before its field groups are encoded.</summary>
    public static byte[] ConstructFreshPerspectivePrefix(string executableLocale)
    {
        _ = MainMapCameraRules.Language(executableLocale);
        return CameraDeclarationEncoding.CreateEmptyPrefix();
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
            M(source.Declaration.Perspective.Lens.VerticalFovRadians, .5f), tangentDispatch);
        return PrepareFreshPerspectiveFromTangent(source, currentPose, contextOwner3E4Bit29,
            nativeClipProfile, tangent, executableLocale, frameNumber, frameProvenance);
    }

    /// <summary>Recomputes the four native matrix slots from complete current
    /// camera fields, preserving the caller's current lighting, exposure and
    /// transition bytes. Current drag/zoom edits are published by the caller
    /// into eye10, target20, up30 and lens140..15C before this forced update.</summary>
    public static byte[] UpdateMatricesForCurrentPerspectivePrefix(ReadOnlySpan<byte> currentPrefix,
        uint nativeClipProfile, NativeCameraTangentDispatch tangentDispatch)
        => CameraGeometryEncoding.UpdateMatrices(currentPrefix, nativeClipProfile, tangentDispatch);

    /// <summary>Prepare geometry independently of later optical and lighting stages.
    /// Look distance uses the current Z scale; lens simulation retains its own outputs.</summary>
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
        byte[] result = source.CreateInitialPrefix(executableLocale);
        var declaration = source.Declaration.Perspective;
        var declaredLens = declaration.Lens;
        var eye = new Vector4(currentPose.Position80, 1f);
        // The helper retains raw source XYZW and the original quaternion-vector
        // normalization; native setters do not normalize the stored quaternion.
        var vectors = MapCameraConstructionMath.PoseFromQuaternionC45956(
            eye, currentPose.Quaternion88, declaration.LookDistance, currentPose.Scale90.Z);
        var lens = new CameraLens(M(currentPose.Scale90.Z, declaredLens.Near),
            M(currentPose.Scale90.Z, declaredLens.Far), declaredLens.VerticalFovRadians,
            M(D(currentPose.Scale90.X, currentPose.Scale90.Y), declaredLens.Aspect),
            declaredLens.ViewOffset, 0f, contextOwner3E4Bit29 ? 2u : 0u);
        var matrices = MapCameraConstructionMath.CompleteFromTangentAB6C60(
            eye, vectors.Target20, vectors.Up30, lens, nativeClipProfile, originalHalfFovTangent);
        var geometry = new MapCameraGeometry(currentPose, eye, vectors.Target20, vectors.Up30, lens, matrices);
        return new(result, source, geometry,
            executableLocale, frameNumber, frameProvenance);
    }

    private static void ValidatePose(MapCameraCurrentPose pose)
    {
        foreach (float x in new[] { pose.Position80.X, pose.Position80.Y, pose.Position80.Z,
            pose.Quaternion88.X, pose.Quaternion88.Y, pose.Quaternion88.Z, pose.Quaternion88.W,
            pose.Scale90.X, pose.Scale90.Y, pose.Scale90.Z })
            if (!float.IsFinite(x)) throw new InvalidDataException("Current source Camera getter pose requires finite explicit fields.");
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float M(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
