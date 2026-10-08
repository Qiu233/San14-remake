using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

/// <summary>Source-selected queue state for one immutable model draw. A denied
/// queue entry retains its explicit source reason. The cache revision includes
/// the graph, shader objects and source frontend/backend selection inputs.</summary>
public sealed record OriginalMapStaticModelDrawSource(OriginalModelGbufferSourceDraw? Source,
    string QueueKey, bool QueueEligible, string? UnavailableReason);
public sealed record OriginalMapStaticModelDrawContext(OriginalMapStaticModelInstance Instance,
    int DrawIndex, string PacketKey, OriginalMapStaticModelCamera Camera,
    OriginalMapStaticModelDrawSource Selection);

/// <summary>Complete retained templates and runtime state are supplied per draw
/// in final queue order. State providers can retain previous material/specular
/// values; preparation never invents a first-frame pixel template.</summary>
public sealed record OriginalMapStaticModelDrawRuntime(ReadOnlyMemory<byte> VertexTemplate,
    ReadOnlyMemory<byte> PreviousPixelTemplate, uint PreviousCachedMaterialFlags,
    bool BackendAlphaEnabled, byte? ForcedAlphaReference, OriginalModelRasterInputs Raster)
{
    /// <summary>When the original material/modifier pair is unchanged, the
    /// source setter is skipped and its previous cSpec is retained. The named
    /// material-ID parameter has its own independent source cache.</summary>
    public OriginalModelDrawMaterialTransition? SourceMaterialTransition { get; init; }
}
public sealed record OriginalMapStaticModelPreparedState(OriginalMapStaticModelDrawContext Context,
    OriginalMapStaticModelDrawRuntime Runtime, OriginalModelGbufferPreparedDraw Prepared);
public sealed record OriginalMapStaticModelFailure(string InstanceKey, int DrawIndex, string Reason);
public sealed record OriginalMapStaticModelPreparedFrame(OriginalMapStaticModelVisibleSet Visibility,
    IReadOnlyList<OriginalModelGbufferInstancePacket> Packets,
    IReadOnlyList<OriginalMapStaticModelPreparedState> MaterialStates,
    IReadOnlyList<OriginalMapStaticModelFailure> Unresolved,
    IReadOnlyList<OriginalMapStaticModelFailure> QueueExcluded)
{
    public bool Complete => Unresolved.Count == 0;
    public IReadOnlyList<OriginalDeferredSourceMode16Candidate>? CurrentViewFamilyCandidates { get; init; }
    public bool HasVisibleDraws => Packets.Count != 0;
}

/// <summary>Map source assembly separated from GPU lifetime. Category/source
/// changes load draw plans once. Each frame selects visible original instances,
/// writes the shared camera and explicit per-instance matrices, and publishes
/// raw geometry/source textures for OriginalModelGbufferLayer. The caller
/// commits returned material states only after the native frame is accepted.</summary>
public sealed class OriginalMapStaticModelAssembler
{
    private readonly string _sourceRevision;
    public string SourceRevision => _sourceRevision;
    private readonly Func<OriginalMapStaticModelAsset, int, OriginalMapStaticModelDrawSource> _loadSource;
    private readonly Dictionary<(string Revision, string Asset, int Draw), OriginalMapStaticModelDrawSource> _sources = [];
    private readonly Dictionary<(string Revision, string Asset, int Draw), string> _sourceFailures = [];
    public int CachedSourceDrawCount => _sources.Count;
    public long SourceDrawLoadCount { get; private set; }
    public int CachedSourceFailureCount => _sourceFailures.Count;

    public OriginalMapStaticModelAssembler(string sourceRevision,
        Func<OriginalMapStaticModelAsset, int, OriginalMapStaticModelDrawSource> loadSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRevision); ArgumentNullException.ThrowIfNull(loadSource);
        (_sourceRevision, _loadSource) = (sourceRevision, loadSource);
    }

    /// <summary>Caller order is queue order. Source state and runtime preparation
    /// can fail visibly without replacing a matrix, shader or previous material
    /// with a default. An incomplete result must not enable the native map.</summary>
    public OriginalMapStaticModelPreparedFrame Prepare(IEnumerable<OriginalMapStaticModelInstance> instances,
        OriginalMapStaticModelCamera camera,
        Func<OriginalMapStaticModelDrawContext, OriginalMapStaticModelDrawRuntime> runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var visibility = OriginalMapStaticModelInstances.SelectVisible(instances, camera);
        return Prepare(visibility, runtime);
    }

    /// <summary>Consumes an explicitly planned original visibility set. The
    /// population owner verifies complete coverage and the accepted Camera;
    /// preparation preserves this visible queue order.</summary>
    public OriginalMapStaticModelPreparedFrame Prepare(OriginalMapStaticModelVisibleSet visibility,
        Func<OriginalMapStaticModelDrawContext, OriginalMapStaticModelDrawRuntime> runtime)
    {
        ArgumentNullException.ThrowIfNull(visibility); ArgumentNullException.ThrowIfNull(runtime);
        var packets = new List<OriginalModelGbufferInstancePacket>(); var states = new List<OriginalMapStaticModelPreparedState>();
        var unresolved = new List<OriginalMapStaticModelFailure>(); var excluded = new List<OriginalMapStaticModelFailure>();
        var candidates = new List<OriginalDeferredSourceMode16Candidate>(); bool completeCandidates=true;
        foreach (var instance in visibility.Visible)
        foreach (var draw in instance.Asset.Draws)
        {
            try
            {
                var key = (_sourceRevision, instance.Asset.SourceKey, draw.Index);
                if (_sourceFailures.TryGetValue(key, out string? failedSource))
                    throw new InvalidDataException(failedSource);
                if (!_sources.TryGetValue(key, out var selected))
                {
                    try
                    {
                        selected = _loadSource(instance.Asset, draw.Index) ??
                            throw new InvalidDataException("Native map draw source provider returned no selection.");
                        if (string.IsNullOrWhiteSpace(selected.QueueKey))
                            throw new InvalidDataException("Native map draw source has no explicit queue identity.");
                        if (selected.QueueEligible && selected.Source is null)
                            throw new InvalidDataException(selected.UnavailableReason ?? "Native map eligible draw source is unresolved.");
                        if (selected.Source is { } loaded && (loaded.DrawIndex != draw.Index ||
                            loaded.Model.Source.ResourceUri != instance.Asset.RawTransforms.Source.ResourceUri ||
                            loaded.Model.Source.DecodedSha256 != instance.Asset.RawTransforms.Source.DecodedSha256))
                            throw new InvalidDataException("Native map cached draw source differs from its source asset/draw revision.");
                        _sources.Add(key, selected); SourceDrawLoadCount++;
                    }
                    catch (InvalidDataException error) {_sourceFailures.Add(key, error.Message); throw;}
                }
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
                var context = new OriginalMapStaticModelDrawContext(instance, draw.Index, packetKey, visibility.Camera, selected);
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

    public void Invalidate() {_sources.Clear(); _sourceFailures.Clear();}

    private static OriginalModelGbufferPreparedDraw ApplyMaterialTransition(OriginalModelGbufferSourceDraw source,
        OriginalMapStaticModelDrawRuntime input, OriginalMapStaticModelCamera camera,
        OriginalModelGbufferPreparedDraw prepared, OriginalModelDrawMaterialTransition transition)
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
        float? alpha = alphaProfile ? OriginalModelRendererStateSourceReader.PrepareAlphaTest(transition.Alpha.Next) : null;
        var pixel = OriginalModelGbufferConstants.Pixel(source.PixelDefinitions, template,
            new(specular, camera.WorldToViewRows, materialId, alpha));
        var packet = prepared.Packet with {Pixel = prepared.Packet.Pixel with
            {Constants = new Dictionary<uint, ReadOnlyMemory<byte>> {[0] = pixel.Bytes}}};
        return new(packet, transition.Next.CachedMaterialFlags, transition.SpecularPreparation, pixel.Writes);
    }
    private static float F32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset, 4));
}
