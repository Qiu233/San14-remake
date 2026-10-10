namespace San14.Data;

/// <summary>Compatibility view for encoded post programs. Optical values belong to the captured camera settings.</summary>
public sealed class MapPostEffect3ApplyUncontrolledCameraInputs
{
    private readonly MapCameraConstructionPacket camera;
    private MapPostEffect3ApplyUncontrolledCameraInputs(MapCameraConstructionPacket snapshot, string owner)
    { camera = snapshot; OwnerIdentity = owner; }
    public long FrameNumber => camera.FrameNumber;
    public uint CameraObjectId => camera.Source.Camera.ObjectId;
    public string OwnerIdentity { get; }
    public CameraOpticsSettings Settings => camera.Optics;
    public string SourceEvidence => "camera-optics/" + OwnerIdentity;
    public uint Word(int encodedOffset) => CameraOpticsEncoding.ReadUncontrolled(camera.Optics, encodedOffset);
    public float Float(int encodedOffset) => BitConverter.UInt32BitsToSingle(Word(encodedOffset));
    public int Int(int encodedOffset) => unchecked((int)Word(encodedOffset));
    public IReadOnlyList<int> SupportedExtraOffsets => CameraOpticsEncoding.UncontrolledOffsets;
    public static MapPostEffect3ApplyUncontrolledCameraInputs ConstructFreshSource(MapCameraConstructionPacket currentCamera, string actualInputOwner)
    {
        ArgumentNullException.ThrowIfNull(currentCamera);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualInputOwner);
        if ((currentCamera.Optics.LensSimulation.ObjectDistanceFlags & 1) != 0)
            throw new NotSupportedException("Pose-relative object distance requires its current lens preparation.");
        return new(currentCamera.CopyAcceptedSnapshot(), actualInputOwner);
    }
    public void ValidateSameCamera(MapCameraConstructionPacket candidate)
    {
        camera.RequireEquivalentCurrentPostEffectScalars(candidate);
        if (candidate.FrameNumber != FrameNumber || candidate.Source.Camera.ObjectId != CameraObjectId ||
            !candidate.Prefix.Span.SequenceEqual(camera.Prefix.Span) ||
            candidate.Source.Resource.Source.DecodedSha256 != camera.Source.Resource.Source.DecodedSha256)
            throw new InvalidDataException("Post input belongs to a different camera frame.");
    }
    public ReadOnlyMemory<byte> ReadCameraBytes(MapCameraConstructionPacket candidate, int offset, int count)
    {
        ValidateSameCamera(candidate);
        return CameraOpticsEncoding.ReadUncontrolledRange(candidate, offset, count);
    }
}
