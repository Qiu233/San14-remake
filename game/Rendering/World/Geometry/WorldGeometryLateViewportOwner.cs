using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>Current frontend viewport for the actual node43 or53 target. The
/// original setter changes both wrapper rectangles and the retained backend
/// depth words; it does not toggle scissor enable. RenderingDevice draw lists
/// must consume CurrentDrawDomain explicitly. This owner does not claim an
/// implicit global GPU viewport or issue a draw.</summary>
public sealed class WorldGeometryLateViewportOwner
{
    private readonly WorldGeometryTargetBinding binding;
    private readonly MapSourceCreatedWorldPlayableHost host;
    private readonly MapPostEffect3ApplyFallbackCameraRectangles cameraRectangles;
    private readonly WorldGeometryLateViewportTarget wrapper;
    private WorldGeometryLateNativeViewport current;
    private FinalEffectScissor scissor;
    private bool scissorEnabled;
    private string currentWriter;

    public WorldGeometryLateViewportOwner(WorldGeometryTargetBinding actualBinding,
        MapSourceCreatedWorldPlayableHost actualHost,
        MapPostEffect3ApplyFallbackCameraRectangles actualSourceCurrentCameraRectangles)
    {
        ArgumentNullException.ThrowIfNull(actualBinding); ArgumentNullException.ThrowIfNull(actualHost);
        ArgumentNullException.ThrowIfNull(actualSourceCurrentCameraRectangles);
        binding = actualBinding; host = actualHost; cameraRectangles = actualSourceCurrentCameraRectangles;
        RequireCurrent();
        if (cameraRectangles.FrameNumber != binding.Accepted.Context.FrameNumber ||
            cameraRectangles.SourceCameraObjectId != binding.Accepted.MainCamera.Source.Camera.ObjectId)
            throw new InvalidDataException("Late viewport rectangles require this same accepted source Camera frame.");

        // The immediate frontend selects the first bound surface's wrapper,
        // not the output/display extent or the cropped viewport dimensions.
        var firstSurface = binding.Frame.Source.ColorTargets.FirstOrDefault() ??
            throw new InvalidDataException($"Actual node{binding.SourceNode} has no first color surface wrapper.");
        var texture = binding.Frame.Textures[firstSurface.Texture.Object.ObjectId];
        wrapper = new(texture.ExplicitExtent.X, texture.ExplicitExtent.Y,
            "actual" + binding.SourceNode + "/first-surface=" + texture.Source.Object.ObjectId.ToString("X8") +
            ";storage=" + texture.StorageRid.Id + ";" + binding.Domain.SourceEvidence);
        var domain = binding.Domain; domain.Validate();
        current = new(NativeInteger(domain.Width), NativeInteger(domain.Height),
            NativeInteger(domain.Left), NativeInteger(domain.Top), domain.MinimumDepth, domain.MaximumDepth);
        scissor = domain.ScissorRectangle; scissorEnabled = domain.ScissorEnabled;
        currentWriter = domain.SourceEvidence;
        var depth = ReadCurrentDepth();
        if (depth.MinimumDepthBits != Bits(current.MinimumDepth) || depth.MaximumDepthBits != Bits(current.MaximumDepth))
            throw new InvalidDataException($"Actual node{binding.SourceNode} domain and retained Host viewport depth words disagree.");
        Scope = new(ReadActualNativeViewport, ApplyActualNativeViewportAndScissor);
    }

    /// <summary>Construct the source-created Camera rectangle from the already
    /// loaded native constructor words. No EXE/RDB read occurs here. This is
    /// the existing owned Camera's fresh rectangle scope; later rectangle
    /// writers require their own actual publication before using this owner.</summary>
    public static WorldGeometryLateViewportOwner FromLoadedSourceCamera(
        WorldGeometryTargetBinding actualBinding, MapSourceCreatedWorldPlayableHost actualHost,
        MapPostEffect3FallbackCameraConstructorWords loadedRectangleSource)
    {
        ArgumentNullException.ThrowIfNull(actualBinding); ArgumentNullException.ThrowIfNull(loadedRectangleSource);
        return new(actualBinding, actualHost, MapPostEffect3ApplyFallbackCameraRectangles.ConstructFreshSource(
            actualBinding.Accepted.MainCamera, loadedRectangleSource,
            actualBinding.Accepted.Feature.Binding.CameraOwnerIdentity + ";actual" + actualBinding.SourceNode + "-current-viewport"));
    }

    public WorldGeometryLateViewportScopeState Scope { get; }
    public WorldGeometryLateViewportTarget ReadActualCurrentWrapper()
    { RequireCurrent(); return wrapper; }

    /// <summary>Source CPU1270..127C is copied to embedded Camera1540..154C,
    /// then cloned unchanged into the selected prepared View. These fields
    /// are outside the3C0 lighting prefix. Read their typed current source
    /// publication, not prefix200's Skylight parameters.</summary>
    public Vector4 ReadActualSelectedView1540()
    { RequireCurrent(); return cameraRectangles.Viewport1540; }

    public WorldGeometryLateNativeViewport ReadActualNativeViewport()
    {
        RequireCurrent(); var depth = ReadCurrentDepth();
        return current with { MinimumDepth = depth.ViewportMinimumDepth, MaximumDepth = depth.ViewportMaximumDepth };
    }

    public WorldGeometryDrawDomain CurrentDrawDomain
    {
        get
        {
            var v = ReadActualNativeViewport();
            return new(v.Left, v.Top, v.Width, v.Height, v.MinimumDepth, v.MaximumDepth,
                scissorEnabled, scissor, currentWriter + ";actual-frontend-viewport;RD-draw-list-explicit-consumption");
        }
    }

    /// <summary>The real CPU95C070 transport: native WHXY becomes both
    /// wrapper XYWH rectangles, and its raw endpoint words reach Host.Viewport.
    /// No raster/depth-compare/stencil state is changed. Empty shader branches
    /// still execute this setter through the actual scope callbacks.</summary>
    public void ApplyActualNativeViewportAndScissor(WorldGeometryLateNativeViewport value)
    {
        RequireCurrent();
        Span<byte> bytes = stackalloc byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value.Width);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[4..], value.Height);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[8..], value.Left);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[12..], value.Top);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[16..], value.MinimumDepth);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[20..], value.MaximumDepth);
        host.Viewport.ApplyNativeDepthSetter(bytes,
            "actual" + binding.SourceNode + "/current-wrapper;source=TC:95C070/SC:95D140;" + cameraRectangles.SourceEvidence);
        current = value; scissor = new(value.Left, value.Top, value.Width, value.Height);
        currentWriter = "source=TC:95C070/SC:95D140;viewport-and-scissor-XYWH;enable-retained";
    }

    /// <summary>Separate actual frontend enable writer. The viewport setter
    /// and its57/58 scope exit never call this method.</summary>
    public void ApplyActualScissorEnable(bool enabled)
    { RequireCurrent(); scissorEnabled = enabled; }

    public void RequireCurrent()
    {
        binding.RequireCurrent();
        if (binding.SourceNode is not (43 or 53) || !ReferenceEquals(host.Session.CurrentFrame, binding.Accepted) ||
            !ReferenceEquals(host.Scene.TerrainFrame, binding.Owner.Frames.OrdinaryFrame))
            throw new InvalidDataException("Geometry viewport requires its actual current43/53 binding and Host storage generation.");
    }

    private MainMapViewportDepthPacket ReadCurrentDepth() =>
        host.Viewport.PrepareSourceMainFrame(binding.Accepted.Selection, binding.Accepted.Feature.Binding.ViewIdentity);
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static int NativeInteger(float value)
    {
        if (!float.IsFinite(value) || (double)value < int.MinValue || (double)value > int.MaxValue || value != MathF.Truncate(value))
            throw new InvalidDataException("Native viewport wrapper coordinates are actual signed integers.");
        return (int)value;
    }
}
