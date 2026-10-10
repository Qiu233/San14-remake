using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Immutable depth words retained by one owned backend generation
/// and bound to the same accepted Main Camera/View frame.</summary>
public sealed class MainMapViewportDepthPacket
{
    private readonly MainMapViewportDepthSource owner;
    private readonly DeferredF103SelectedSource acceptedSelection;
    internal MainMapViewportDepthPacket(MainMapViewportDepthSource owner,
        DeferredF103SelectedSource selected, long frame, uint minimum, uint maximum,
        string backend, string view, long revision, string provenance)
    { this.owner = owner; acceptedSelection = selected;
      Locale = owner.Locale; FrameNumber = frame; MinimumDepthBits = minimum; MaximumDepthBits = maximum;
      BackendOwnerIdentity = backend; ViewOwnerIdentity = view; WriterRevision = revision;
      SourceProvenance = provenance; }
    public string Locale { get; }
    public long FrameNumber { get; }
    public uint MinimumDepthBits { get; }
    public uint MaximumDepthBits { get; }
    public float ViewportMinimumDepth => BitConverter.UInt32BitsToSingle(MinimumDepthBits);
    public float ViewportMaximumDepth => BitConverter.UInt32BitsToSingle(MaximumDepthBits);
    public string BackendOwnerIdentity { get; }
    public string ViewOwnerIdentity { get; }
    public long WriterRevision { get; }
    public string SourceProvenance { get; }

    /// <summary>Validate this captured word revision at the consuming Main
    /// frame. A later setter/invalidation or another selection is rejected.</summary>
    public void ValidateFrame(DeferredF103SelectedSource selected, string ownedViewOwner)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedViewOwner);

        if (!ReferenceEquals(selected, acceptedSelection) || ownedViewOwner != ViewOwnerIdentity ||
            selected.Camera.FrameNumber != FrameNumber || selected.Current.FrameNumber != FrameNumber ||
            selected.Camera.ExecutableLocale != Locale)
            throw new InvalidDataException("Viewport depth packet belongs to another accepted Camera/View selection.");
        owner.ValidateCurrentRevision(WriterRevision, MinimumDepthBits, MaximumDepthBits);
    }
}

/// <summary>Owned backend depth defaults and current setter state. Viewport crop changes XYWH and preserves depth.</summary>
public sealed class MainMapViewportDepthSource
{
    private uint minimum, maximum;
    private bool known = true;
    private long revision, lastPublishedFrame;
    private string writerProvenance;

    private MainMapViewportDepthSource(MainViewportDepthDefaults recipe, string identity)
    {
        Locale = recipe.Locale; BackendOwnerIdentity = identity; RuleVersion = recipe.RuleVersion;
        minimum = recipe.MinimumBits; maximum = recipe.MaximumBits;
        writerProvenance = "fresh-owned-backend-construction;" + recipe.RuleVersion;
    }

    public string Locale { get; }
    public string BackendOwnerIdentity { get; }
    public string RuleVersion { get; }
    public bool CurrentDepthKnown => known;

    internal void ValidateCurrentRevision(long packetRevision, uint packetMinimum, uint packetMaximum)
    {
        if (!known || packetRevision != revision || packetMinimum != minimum || packetMaximum != maximum)
            throw new InvalidDataException("Viewport depth packet predates the current backend word revision.");
    }

    /// <summary>Create a new backend generation with depth endpoints zero and one.</summary>
    public static MainMapViewportDepthSource CreateFreshBackend(
        GameResourceRepository repository, string locale, string ownedBackendId)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedBackendId);
        return new(new(MainMapCameraRules.Language(locale),0,0x3F800000,MainMapCameraRules.Version),ownedBackendId);
    }

    /// <summary>Record the exact native24B Width/Height/X/Y/min/max setter
    /// input. Both95C070 and967B20 transport the endpoint bits without math.</summary>
    public void ApplyNativeDepthSetter(ReadOnlySpan<byte> full24B, string writerProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(writerProvenance);
        if (full24B.Length != 24)
            throw new InvalidDataException("Native viewport depth setter requires its complete24-byte input.");
        minimum = BinaryPrimitives.ReadUInt32LittleEndian(full24B.Slice(16, 4));
        maximum = BinaryPrimitives.ReadUInt32LittleEndian(full24B.Slice(20, 4));
        known = true; revision = checked(revision + 1);
        this.writerProvenance = writerProvenance + ";native-depth-setter=95C070/967B20;rawV10/V14";
    }

    /// <summary>An untracked writer makes this generation unavailable until
    /// an explicit complete setter input establishes both current words.</summary>
    public void InvalidateCurrentDepth(string unknownWriterProvenance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unknownWriterProvenance);
        known = false; revision = checked(revision + 1); writerProvenance = unknownWriterProvenance;
    }

    public MainMapViewportDepthPacket PrepareSourceMainFrame(
        DeferredF103SelectedSource selected, string ownedViewOwner)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownedViewOwner);

        var camera = selected.Camera; var source = selected.SourcePlan;
        var reference = source.ShaderReference;
        if (!known)
            throw new InvalidDataException("Main viewport depth is unavailable after an untracked current writer: " + writerProvenance);
        if (camera.ExecutableLocale != Locale || camera.FrameNumber <= 0 ||
            camera.FrameNumber != selected.Current.FrameNumber || camera.FrameNumber < lastPublishedFrame ||
            camera.Source.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId ||
            source.ViewId != 0 || source.Node.Index != 166 || source.Node.Call.FactoryClassId != 0xF103DB00 ||
            reference.ShaderObject.ObjectId != 0xC96F6A61 ||
            reference.ShaderObject.ClassId != RenderGraphShaderReferences.ShaderObjectClassId ||
            reference.ShaderFileHash != 0x9D8E811F || selected.Current.NativeColorMode != 0x60 ||
            (selected.OwnedFeatureSource is { } feature && feature.CurrentView.ViewIdentity != ownedViewOwner))
            throw new InvalidDataException("Viewport depth requires this same backend profile and acceptedMain38D/View0/Class868/Color60 frame.");
        lastPublishedFrame = camera.FrameNumber;
        return new(this, selected, camera.FrameNumber, minimum, maximum, BackendOwnerIdentity, ownedViewOwner, revision,
            camera.FrameProvenance + ";" + writerProvenance + ";backend=" + BackendOwnerIdentity +
            ";View=" + ownedViewOwner + ";Main-crop=TC:D75E50/SC:D76C10;getter24B-XYWH-only;depth-preserved");
    }
}

internal sealed record MainViewportDepthDefaults(string Locale, uint MinimumBits, uint MaximumBits,
    string RuleVersion);
