using System.Numerics;

namespace San14.Data;

/// <summary>One immutable publication from the independent camera owner.</summary>
public sealed record StoryEffectCommonCameraFrame(MapCameraGeometry Geometry,
    Func<int, int, Matrix4x4?> EmitterBinding, uint ClipProfile,
    NativeCameraTangentDispatch TangentDispatch);

/// <summary>The Common camera-id1 Data owner. The application publishes the
/// same controller to this camera and Main separately; their declarations,
/// lenses and completed matrices are never shared or relabelled.</summary>
public sealed class StoryEffectCommonCamera : IDisposable
{
    public const uint CameraObjectId = 0x36C14145;
    private readonly LoadedMapCameraConstructionSource source;
    private readonly Vector3 scale;
    private MainMapCameraControllerState? controller;
    private bool reverseDepth;
    private uint clipProfile;
    private NativeCameraTangentDispatch tangentDispatch;
    private bool retired;
    private StoryEffectCommonCameraFrame current;

    private StoryEffectCommonCamera(LoadedMapCameraConstructionSource source, string locale,
        bool reverseDepth, uint clipProfile, NativeCameraTangentDispatch tangentDispatch,
        long frameNumber, string provenance)
    {
        this.source = source;
        this.reverseDepth = reverseDepth; this.clipProfile = clipProfile; this.tangentDispatch = tangentDispatch;
        var freshPose = MapCameraConstruction.ConstructFreshSourcePose(source);
        scale = freshPose.Scale90; // This camera's own unparented getter90.
        var geometry = MapCameraConstruction.PrepareFreshPerspective(source, freshPose, reverseDepth,
            clipProfile, tangentDispatch, locale, frameNumber, provenance).Geometry;
        current = new(geometry, UniqueTacticEmitterBinding.FromCamera(geometry), clipProfile, tangentDispatch);
    }

    public LoadedMapCameraConstructionSource Source => source;
    public StoryEffectCommonCameraFrame Capture()
    { ObjectDisposedException.ThrowIf(retired, this); return Volatile.Read(ref current); }

    public static StoryEffectCommonCamera ConstructFresh(GameResourceRepository repository, string locale,
        bool reverseDepth, uint clipProfile, NativeCameraTangentDispatch tangentDispatch,
        long frameNumber, string provenance)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var source = MapCameraConstructionSource.ReadPerspective(
            repository.LoadObjects("system", MapCameraConstructionSource.SourceFileId), CameraObjectId);
        return new(source, locale, reverseDepth, clipProfile, tangentDispatch, frameNumber, provenance);
    }

    /// <summary>Application17BD0 calls the same controller V20 a second time,
    /// then writes camera-id1's own FOV/near/far and forces its own V418.
    /// The supplied value is controller state, not a Main camera frame.</summary>
    public void PublishController(MainMapCameraControllerState currentController, bool currentReverseDepth,
        uint currentClipProfile, NativeCameraTangentDispatch currentTangentDispatch)
    {
        ObjectDisposedException.ThrowIf(retired, this);
        ArgumentNullException.ThrowIfNull(currentController);
        if (controller == currentController && reverseDepth == currentReverseDepth &&
            clipProfile == currentClipProfile && tangentDispatch == currentTangentDispatch) return;

        var pose = MapCameraController.Publish(currentController, scale);
        var eye = new Vector4(pose.Position80, 1f);
        var declaration = source.Declaration.Perspective;
        var vectors = MapCameraConstructionMath.PoseFromQuaternionC45956(
            eye, pose.Quaternion88, declaration.LookDistance, scale.Z);
        // These are the actual camera-id1 setters after V20. Main's season,
        // owner-FOV override and retained far-plane writers do not apply here.
        var lens = declaration.Lens with
        {
            VerticalFovRadians = BitConverter.UInt32BitsToSingle(0x3E860A92),
            Near = 8f * scale.Z, Far = 10000f * scale.Z,
            Aspect = (scale.X / scale.Y) * declaration.Lens.Aspect,
            OrthographicScale = 0,
            Flags = currentReverseDepth ? 2u : 0u
        };
        var matrices = MapCameraConstructionMath.CompleteAB6C60(
            eye, vectors.Target20, vectors.Up30, lens, currentClipProfile, currentTangentDispatch);
        var geometry = new MapCameraGeometry(pose, eye, vectors.Target20, vectors.Up30, lens, matrices);
        Volatile.Write(ref current, new(geometry, UniqueTacticEmitterBinding.FromCamera(geometry), currentClipProfile, currentTangentDispatch));
        controller = currentController;
        reverseDepth = currentReverseDepth; clipProfile = currentClipProfile; tangentDispatch = currentTangentDispatch;
    }

    public void Dispose() => retired = true;
}
