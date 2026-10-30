using System.Numerics;
using San14.Data;
using San14.Data.Formats;

public sealed record ReflectionCaptureStaticPacketFrame(ReflectionCaptureFieldCellGeometry Cell,
    ReflectionCaptureCameraInputs TopDownCamera, Matrix4x4 FaceWorldToProjection,
    CommonClipPlanePacket ClipPlanes, float CopiedCameraAmbientMapRate);
public sealed record ReflectionCaptureStaticRetainedDraw(ReadOnlyMemory<byte> Vertex256,
    ReadOnlyMemory<byte> Pixel176, uint PreviousMaterialFlags, ModelRasterInputs Raster,
    byte? ForcedAlphaReference = null);

/// <summary>Actual static state12 packets. Uses the same final shader table as
/// normal World traversal, captures actual type17 and material resources once,
/// then writes the current face matrices, source scope and retained parameters.</summary>
public sealed class ReflectionCaptureStaticModelPacketSource
{
    private sealed class RendererState
    {
        public ReflectionCaptureRendererAmbientState Ambient = ReflectionCaptureRendererAmbientState.CreateFreshClass18();
        public bool AmbientBindingKnown = true; // New backend renderer has native P26=null.
        public ReflectionCaptureAmbientCube? AmbientBinding;
        public ReflectionCaptureAmbientCube? KnownNullBackendBinding;
    }
    private sealed record PreparedDraw(NativeWorldShaderTableState Table,
        ReflectionCaptureModelSourceDraw Source, ModelMatrixBindingSource Matrix, RendererState Renderer);
    private readonly GameResourceRepository repository;
    private readonly SceneViewNativeMapModelSourceOwner staticSources;
    private readonly ModelGbufferLayer layer;
    private readonly Dictionary<MapStaticModelAsset, Dictionary<int, PreparedDraw?>> assets = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<(string Uri, string Version, uint ObjectId), RendererState> renderers = [];

    /// <summary>This constructor creates this program's fresh state12 renderer
    /// owners. A later import must publish its real retained endpoints below.</summary>
    public ReflectionCaptureStaticModelPacketSource(GameResourceRepository repository,
        SceneViewNativeMapModelSourceOwner actualStaticSources, ModelGbufferLayer actualSharedLayer)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(actualStaticSources);
        ArgumentNullException.ThrowIfNull(actualSharedLayer);
        (this.repository, staticSources, layer) = (repository, actualStaticSources, actualSharedLayer);
    }

    public static ReflectionCaptureStaticPacketFrame PrepareCurrentFrame(
        ReflectionCaptureFieldCellGeometry actualCell, MapCameraConstructionPacket actualSourceMainCamera,
        SceneViewNativeMapSourceFields actualSourceViewScalars, Matrix4x4 actualFaceWorldToProjection,
        CommonClipPlanePacket actualCurrentFrontendClipPlanes)
    {
        ArgumentNullException.ThrowIfNull(actualSourceMainCamera); ArgumentNullException.ThrowIfNull(actualSourceViewScalars);
        ArgumentNullException.ThrowIfNull(actualCurrentFrontendClipPlanes);
        // D02C60 copies source View3CC bit-for-bit; capture pose/lens updates
        // leave that scalar intact. Eye is the actual top-down capture Eye,
        // which differs from both the Main Eye and the cell/cube center.
        var camera = ReflectionCaptureFieldGeometry.PrepareCamera(actualCell,
            actualSourceMainCamera.Geometry.Up, actualSourceMainCamera.Lens);
        return new(actualCell, camera, actualFaceWorldToProjection, actualCurrentFrontendClipPlanes,
            actualSourceViewScalars.PrepareBuiltinAmbientCameraRate(actualSourceMainCamera));
    }

    private PreparedDraw? Load(MapStaticModelAsset asset, int drawIndex)
    {
        var table = staticSources.LoadFinalOwnedTable(asset, drawIndex);
        staticSources.RequireCurrentShaderState(table);
        if (!assets.TryGetValue(asset, out var draws)) assets.Add(asset, draws = []);
        if (draws.TryGetValue(drawIndex, out var found)) return found;
        var selected = staticSources.ReadSelectedShaderInterface(table, 12);
        if (selected.Provider is not { } provider) { draws.Add(drawIndex, null); return null; }
        var shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
        var bindings = ModelShaderResources.LoadBindings(repository, asset.TextureResources);
        var input = ModelMatrixBindingSourceReader.PrepareUnboundGroup(bindings, drawIndex) ??
            throw new InvalidDataException("reflection.capture.static.actual_type17");
        var matrix = ModelMatrixBindingSourceReader.Select(shader, input);
        var selection = ModelShaderSelectionSourceReader.Read(provider, shader);
        if (selection.Dimensions.Where(d => d.DimensionIndex is not (0 or 2)).Any(d => d.RawCount > 1))
            throw new NotSupportedException("reflection.capture.static.other_current_dimensions");
        var initial = ModelShaderSelectionSourceReader.ReadInitialCache(selection);
        // EC7100 clears the complete48-byte request before D362B0 passes it
        // unchanged to D15260 param11/v28. The separate v50 three-byte
        // flags request has a different owner. Match; do not assume row0.
        var feature = ModelShaderSelectionSourceReader.SelectFeature(selection, initial.Indices[0], new uint[12]);
        uint[] indices = initial.Indices.ToArray(); indices[0] = feature.Write.NextIndex;
        if (matrix.Selection.RowIndex < 0) throw new InvalidDataException("reflection.capture.static.matrix_row");
        indices[2] = checked((uint)matrix.Selection.RowIndex);
        var source = ReflectionCaptureModelSourceDraw.Load(repository, asset.TextureResources, drawIndex,
            selected, indices, 0, new(false, false, 0));
        if (source.VertexDefinitions.Buffers.Single().ByteSize != 256)
            throw new NotSupportedException("reflection.capture.static.rigid_vertex_profile");
        var key = (provider.Objects.Source.ResourceUri, provider.Objects.Source.DecodedSha256, provider.Object.ObjectId);
        if (!renderers.TryGetValue(key, out var renderer)) renderers.Add(key, renderer = new());
        var prepared = new PreparedDraw(table, source, matrix, renderer); draws.Add(drawIndex, prepared); return prepared;
    }

    /// <summary>A final null type16 remains a native skip. Caller supplies the
    /// actual retained material image/cache and current raster, in tuple order.
    /// No source World visibility/order or a five-MRT packet is reused here.</summary>
    public async Task<ReflectionCaptureModelPreparedDraw?> PrepareAsync(string actualPrimitiveKey,
        MapStaticModelInstance actualInstance, int drawIndex, ReflectionCaptureStaticPacketFrame actualFrame,
        ReflectionCaptureStaticRetainedDraw actualRetained)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualPrimitiveKey); ArgumentNullException.ThrowIfNull(actualInstance);
        ArgumentNullException.ThrowIfNull(actualFrame); ArgumentNullException.ThrowIfNull(actualRetained);
        var prepared = Load(actualInstance.Asset, drawIndex); if (prepared is null) return null;
        if (!actualInstance.DrawTransforms.TryGetValue(drawIndex, out var transform))
            throw new InvalidDataException("reflection.capture.static.current_draw_transform");
        if (!prepared.Renderer.AmbientBindingKnown)
            throw new InvalidDataException("reflection.capture.static.unknown_prior_P26");
        var ambient = prepared.Renderer.AmbientBinding;
        if (ambient is null)
        {
            ambient = prepared.Renderer.KnownNullBackendBinding ?? await layer.GetKnownNullReflectionAmbientAsync(prepared.Source);
            prepared.Renderer.KnownNullBackendBinding = ambient;
        }
        var matrix = actualFrame.FaceWorldToProjection;
        float[] projection = [matrix.M11,matrix.M12,matrix.M13,matrix.M14,matrix.M21,matrix.M22,matrix.M23,matrix.M24,
            matrix.M31,matrix.M32,matrix.M33,matrix.M34,matrix.M41,matrix.M42,matrix.M43,matrix.M44];
        byte[] vertex = ReflectionCaptureModelParameters.RigidVertex(prepared.Source.VertexDefinitions,
            actualRetained.Vertex256.Span, new(projection, transform.NormalRows, transform.PositionRows), actualFrame.ClipPlanes.Planes);
        var lighting = prepared.Renderer.Ambient.PrepareCapture(actualFrame.TopDownCamera.Eye, actualFrame.CopiedCameraAmbientMapRate);
        return prepared.Source.Prepare(actualPrimitiveKey, new Dictionary<uint, ReadOnlyMemory<byte>> { [0] = vertex },
            actualRetained.Pixel176.Span, actualRetained.PreviousMaterialFlags, actualFrame.Cell, lighting, ambient,
            true, actualRetained.ForcedAlphaReference, actualRetained.Raster);
    }

    /// <summary>An actual same-renderer parameter writer/import can replace
    /// the fresh endpoints. EC7100's no-write path never calls this method.</summary>
    public void ImportActualRendererAmbient(MapStaticModelAsset asset, int drawIndex,
        ReflectionCaptureRendererAmbientState actualParameters, bool actualP26Known,
        ReflectionCaptureAmbientCube? actualP26)
    {
        ArgumentNullException.ThrowIfNull(actualParameters);
        var current = Load(asset, drawIndex) ?? throw new InvalidDataException("reflection.capture.static.null_renderer_import");
        current.Renderer.Ambient = actualParameters;
        current.Renderer.AmbientBindingKnown = actualP26Known;
        current.Renderer.AmbientBinding = actualP26;
    }
}
