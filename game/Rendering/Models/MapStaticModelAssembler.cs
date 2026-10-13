using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>Source-selected queue state for one immutable model draw. A denied
/// queue entry retains its explicit source reason. The source provider owns
/// import and reuse of graph, shader and queue preparation.</summary>
public sealed record MapStaticModelDrawSource(ModelGbufferSourceDraw? Source,
    string QueueKey, bool QueueEligible, string? UnavailableReason);
public sealed record MapStaticModelDrawContext(MapStaticModelInstance Instance,
    int DrawIndex, string PacketKey, MapStaticModelCamera Camera,
    MapStaticModelDrawSource Selection);

/// <summary>Complete retained templates and runtime state are supplied per draw
/// in final queue order. State providers can retain previous material/specular
/// values; preparation never invents a first-frame pixel template.</summary>
public sealed record MapStaticModelDrawRuntime(ReadOnlyMemory<byte> VertexTemplate,
    ReadOnlyMemory<byte> PreviousPixelTemplate, uint PreviousCachedMaterialFlags,
    bool BackendAlphaEnabled, byte? ForcedAlphaReference, ModelRasterInputs Raster)
{
    /// <summary>When the original material/modifier pair is unchanged, the
    /// source setter is skipped and its previous cSpec is retained. The named
    /// material-ID parameter has its own independent source cache.</summary>
    public ModelDrawMaterialTransition? SourceMaterialTransition { get; init; }
}
public sealed record MapStaticModelPreparedState(MapStaticModelDrawContext Context,
    MapStaticModelDrawRuntime Runtime, ModelGbufferPreparedDraw Prepared);
public sealed record MapStaticModelFailure(string InstanceKey, int DrawIndex, string Reason);
public sealed record MapStaticModelPreparedFrame(MapStaticModelVisibleSet Visibility,
    IReadOnlyList<ModelGbufferInstancePacket> Packets,
    IReadOnlyList<MapStaticModelPreparedState> MaterialStates,
    IReadOnlyList<MapStaticModelFailure> Unresolved,
    IReadOnlyList<MapStaticModelFailure> QueueExcluded)
{
    public bool Complete => Unresolved.Count == 0;
    public IReadOnlyList<DeferredSourceMode16Candidate>? CurrentViewFamilyCandidates { get; init; }
    public bool HasVisibleDraws => Packets.Count != 0;
}

/// <summary>Map source assembly separated from GPU lifetime. Category/source
/// changes load draw plans once. Each frame selects visible original instances,
/// writes the shared camera and explicit per-instance matrices, and publishes
/// raw geometry/source textures for OriginalModelGbufferLayer. The caller
/// commits returned material states only after the native frame is accepted.</summary>
public sealed class MapStaticModelAssembler
{
    private readonly string _sourceRevision;
    public string SourceRevision => _sourceRevision;
    private readonly Func<MapStaticModelAsset, int, MapStaticModelDrawSource> _loadSource;

    public MapStaticModelAssembler(string sourceRevision,
        Func<MapStaticModelAsset, int, MapStaticModelDrawSource> loadSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRevision); ArgumentNullException.ThrowIfNull(loadSource);
        (_sourceRevision, _loadSource) = (sourceRevision, loadSource);
    }

    /// <summary>Caller order is queue order. Source state and runtime preparation
    /// can fail visibly without replacing a matrix, shader or previous material
    /// with a default. An incomplete result must not enable the native map.</summary>
    public MapStaticModelPreparedFrame Prepare(IEnumerable<MapStaticModelInstance> instances,
        MapStaticModelCamera camera,
        Func<MapStaticModelDrawContext, MapStaticModelDrawRuntime> runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var visibility = MapStaticModelInstances.SelectVisible(instances, camera);
        return Prepare(visibility, runtime);
    }

    /// <summary>Consumes an explicitly planned original visibility set. The
    /// population owner verifies complete coverage and the accepted Camera;
    /// preparation preserves this visible queue order.</summary>
    public MapStaticModelPreparedFrame Prepare(MapStaticModelVisibleSet visibility,
        Func<MapStaticModelDrawContext, MapStaticModelDrawRuntime> runtime)
    {
        ArgumentNullException.ThrowIfNull(visibility); ArgumentNullException.ThrowIfNull(runtime);
        var packets = new List<ModelGbufferInstancePacket>(); var states = new List<MapStaticModelPreparedState>();
        var unresolved = new List<MapStaticModelFailure>(); var excluded = new List<MapStaticModelFailure>();
        var candidates = new List<DeferredSourceMode16Candidate>(); bool completeCandidates=true;
        foreach (var instance in visibility.Visible)
        foreach (var draw in instance.Asset.Draws)
        {
            try
            {
                var selected = _loadSource(instance.Asset, draw.Index) ??
                    throw new InvalidDataException("Native map draw source provider returned no selection.");
                if (string.IsNullOrWhiteSpace(selected.QueueKey))
                    throw new InvalidDataException("Native map draw source has no explicit queue identity.");
                if (selected.QueueEligible && selected.Source is null)
                    throw new InvalidDataException(selected.UnavailableReason ?? "Native map eligible draw source is unresolved.");
                if (selected.Source is { } selectedSource)
                    candidates.Add(new("StaticModel",instance.InstanceKey,draw.Index,
                        selectedSource.Model.Source.ResourceUri,selectedSource.Model.Source.DecodedSha256,
                        selectedSource.GraphSource.Draw.Binding.Shader,_sourceRevision+"/actual-static-preparation/"+instance.InstanceKey));
                else completeCandidates=false;
                if (!selected.QueueEligible)
                {
                    if (string.IsNullOrWhiteSpace(selected.UnavailableReason))
                        throw new InvalidDataException("Native map excluded queue draw has no source reason.");
                    excluded.Add(new(instance.InstanceKey, draw.Index, selected.UnavailableReason)); continue;
                }
                string packetKey = $"{instance.InstanceKey}|{selected.QueueKey}|draw:{draw.Index}";
                var context = new MapStaticModelDrawContext(instance, draw.Index, packetKey, visibility.Camera, selected);
                var input = runtime(context) ?? throw new InvalidDataException("Native map draw runtime is unresolved.");
                var transform = instance.DrawTransforms[draw.Index];
                var prepared = selected.Source!.PrepareRigid(packetKey, input.VertexTemplate.Span,
                    new(visibility.Camera.WorldToProjectionRows, transform.NormalRows, transform.PositionRows),
                    input.PreviousPixelTemplate.Span, visibility.Camera.WorldToViewRows,
                    input.PreviousCachedMaterialFlags, input.BackendAlphaEnabled, input.ForcedAlphaReference, input.Raster);
                if (input.SourceMaterialTransition is { } transition)
                    prepared = ApplyMaterialTransition(selected.Source, input, visibility.Camera, prepared, transition);
                packets.Add(prepared.Packet); states.Add(new(context, input, prepared));
            }
            catch (InvalidDataException error) {unresolved.Add(new(instance.InstanceKey, draw.Index, error.Message));}
        }
        return new(visibility, packets.AsReadOnly(), states.AsReadOnly(), unresolved.AsReadOnly(), excluded.AsReadOnly())
        {CurrentViewFamilyCandidates=completeCandidates&&unresolved.Count==0?candidates.AsReadOnly():null};
    }

    private static ModelGbufferPreparedDraw ApplyMaterialTransition(ModelGbufferSourceDraw source,
        MapStaticModelDrawRuntime input, MapStaticModelCamera camera,
        ModelGbufferPreparedDraw prepared, ModelDrawMaterialTransition transition)
    {
        var buffer = source.PixelDefinitions.Buffers.Single();
        var template = input.PreviousPixelTemplate.Span;
        int specOffset = checked((int)buffer.Variables.Single(v => v.Name.Text == "cSpec").StartOffset);
        int idOffset = checked((int)buffer.Variables.Single(v => v.Name.Text == "nMtrID").StartOffset);
        Vector4 retainedSpecular = new(F32(template, specOffset), F32(template, specOffset + 4),
            F32(template, specOffset + 8), F32(template, specOffset + 12));
        Vector4 specular = transition.SpecularPreparation.Value ?? retainedSpecular;
        int retainedId = BinaryPrimitives.ReadInt32LittleEndian(template.Slice(idOffset, 4));
        int materialId = source.Material.MaterialId ?? retainedId;
        bool alphaProfile = buffer.Variables.Any(v => v.Name.Text == "vATest");
        float? alpha = alphaProfile ? ModelRendererStateSourceReader.PrepareAlphaTest(transition.Alpha.Next) : null;
        var pixel = ModelGbufferConstants.Pixel(source.PixelDefinitions, template,
            new(specular, camera.WorldToViewRows, materialId, alpha));
        var packet = prepared.Packet with {Pixel = prepared.Packet.Pixel with
            {Constants = new Dictionary<uint, ReadOnlyMemory<byte>> {[0] = pixel.Bytes}}};
        return new(packet, transition.Next.CachedMaterialFlags, transition.SpecularPreparation, pixel.Writes);
    }
    private static float F32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4));
}
