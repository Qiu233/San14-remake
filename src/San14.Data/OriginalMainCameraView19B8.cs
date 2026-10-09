using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The actual Main Camera.v420 result stored by the View builder at
/// +19B8. This belongs to an independently owned Camera header, not a native
/// process sample or a union of the World light/primitive groups.</summary>
public sealed class OriginalMainCameraView19B8Projection
{
    private readonly OriginalMainCameraView19B8Owner owner;
    internal OriginalMainCameraView19B8Projection(OriginalMainCameraView19B8Owner owner,
        OriginalMapCameraConstructionPacket camera, uint result, string provenance)
    {
        this.owner = owner; Camera = camera.CopyAcceptedSnapshot();
        CurrentView19B8 = result; CurrentProducerProvenance = provenance;
        CameraPrefixSha256 = Convert.ToHexString(SHA256.HashData(Camera.Prefix.Span));
    }
    public OriginalMapCameraConstructionPacket Camera { get; }
    public long FrameNumber => Camera.FrameNumber;
    public int ViewSlot => 0;
    public uint CurrentView19B8 { get; }
    public string CameraOwnerIdentity => owner.CameraOwnerIdentity;
    public string SourceIdentity => owner.SourceIdentity;
    public string CameraPrefixSha256 { get; }
    public string CurrentProducerProvenance { get; }

    /// <summary>Accepted Camera snapshots may be copied by shader selection.
    /// The complete prefix, optics, pose, source and frame must still agree.</summary>
    public void ValidateCurrent(OriginalMapCameraConstructionPacket camera, long frameNumber,
        int actualViewSlot, string actualCameraOwnerIdentity)
        => owner.Validate(this, camera, frameNumber, actualViewSlot, actualCameraOwnerIdentity);
}

/// <summary>A fresh independently owned Main CPU header. The native constructor
/// clears CPU+8; this owner exposes only the proved v420 operation, which takes
/// and releases bit31 and preserves all lower bits. Other source CPU writers,
/// Reference retargeting and native-process state require another owner.</summary>
public sealed class OriginalMainCameraView19B8Owner
{
    private const uint LockBit = 0x80000000u;
    private readonly (string ResourceUri, uint FileId, string DecodedSha256) mainSource;
    private readonly object gate = new();
    private uint header08;
    private OriginalMainCameraView19B8Projection? latest;

    private OriginalMainCameraView19B8Owner(LoadedOriginalDefaultView0Source declaration, string identity)
    {
        var source = declaration.MainCamera.Resource.Source;
        mainSource = (source.ResourceUri, source.FileId, source.DecodedSha256);
        CameraOwnerIdentity = identity;
        // TC C3BF88/C3BF9B, SC C3D638/C3D64B: r10=0; [MainCPU+8]=r10.
        header08 = 0;
        SourceIdentity = declaration.SourceIdentity + ";owned-MainCPU8=constructor-zero;" +
            "Reference.v420=TC:C5C040/SC:C5D660;Main.v420=TC:C5BFE0/SC:C5D600;" +
            "View19B8=TC:D13F75/D13F7B;managed-header-owner=" + identity;
    }
    public string CameraOwnerIdentity { get; }
    public string SourceIdentity { get; }

    public static OriginalMainCameraView19B8Owner ConstructFresh(
        LoadedOriginalDefaultView0Source actualDeclaration, string cameraOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualDeclaration);
        ArgumentException.ThrowIfNullOrWhiteSpace(cameraOwnerIdentity);
        var owned = OriginalDefaultView0Source.ReadDeclarationDefaults(actualDeclaration.CopyViewDefinitions(),
            actualDeclaration.CopyCameraDefinitions(), actualDeclaration.CopyTaskGraph());
        return new(owned, cameraOwnerIdentity);
    }

    /// <summary>Project the owned Main v420 result after the supplied current
    /// embedded Camera preparation. Native B992A0 exports that prepared Camera;
    /// the returned EAX is the successful unlock CAS's OLD header, with bit31
    /// still set. The unlocked header value is retained separately by this owner.</summary>
    public OriginalMainCameraView19B8Projection ProjectCurrentMain0(
        OriginalMapCameraConstructionPacket currentCamera, string currentProducerProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentProducerProvenance);
        RequireCamera(currentCamera);
        lock (gate)
        {
            if (latest is not null && currentCamera.FrameNumber < latest.FrameNumber)
                throw new InvalidDataException("Main Camera19B8 cannot project an older owned frame.");
            if ((header08 & LockBit) != 0)
                throw new InvalidDataException("Main Camera19B8 requires its unlocked owned header.");
            // B060 acquires the lock without changing lower31 bits. C5C020's
            // cmpxchg returns old EAX, although ECX stores the cleared word.
            uint locked = header08 | LockBit;
            uint returnedEax = locked;
            header08 = locked & ~LockBit;
            latest = new(this, currentCamera, returnedEax, SourceIdentity + ";" + currentProducerProvenance);
            return latest;
        }
    }

    internal void Validate(OriginalMainCameraView19B8Projection projection,
        OriginalMapCameraConstructionPacket camera, long frameNumber, int slot, string cameraOwner)
    {
        RequireCamera(camera); RequireCamera(projection.Camera);
        lock (gate)
        {
            var accepted = projection.Camera;
            if (!ReferenceEquals(latest, projection) || frameNumber != camera.FrameNumber ||
                projection.FrameNumber != frameNumber || slot != 0 || cameraOwner != CameraOwnerIdentity ||
                projection.CurrentView19B8 != (header08 | LockBit) ||
                camera.ExecutableLocale != accepted.ExecutableLocale ||
                camera.FrameProvenance != accepted.FrameProvenance || camera.CurrentPose != accepted.CurrentPose ||
                camera.Lens != accepted.Lens || camera.Matrices != accepted.Matrices ||
                Convert.ToHexString(SHA256.HashData(camera.Prefix.Span)) != projection.CameraPrefixSha256 ||
                Convert.ToHexString(SHA256.HashData(accepted.Prefix.Span)) != projection.CameraPrefixSha256)
                throw new InvalidDataException("Main Camera19B8 differs from its current owned header, Camera, View0 or accepted frame.");
        }
    }

    private void RequireCamera(OriginalMapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (camera.FrameNumber < 1 || string.IsNullOrWhiteSpace(camera.FrameProvenance) ||
            camera.ExecutableLocale is not ("SC" or "TC") ||
            camera.Prefix.Length != OriginalMapCameraConstruction.PrefixByteLength ||
            camera.Source.Camera.ObjectId != OriginalDefaultView0Source.MainCameraObjectId)
            throw new InvalidDataException("Main Camera19B8 requires a complete current stock Main38D04576 Camera.");
        var source = camera.Source.Resource.Source;
        if (source.ResourceUri != mainSource.ResourceUri || source.FileId != mainSource.FileId ||
            source.DecodedSha256 != mainSource.DecodedSha256)
            throw new InvalidDataException("Main Camera19B8 source differs from the actual default Reference/Main declaration.");
    }
}
