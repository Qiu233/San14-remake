using Godot;
using San14.Data;

/// <summary>The actual first-counter/sort publisher supplies this capability.
/// It must preserve original tuple keys and the current selected 15-field
/// descriptor. There is deliberately no candidate-array/default-order adapter.</summary>
public interface IArmyWorldQueueOrderSource
{
    IReadOnlyList<string> ReadActualOrderedCandidateIdentities(ArmyWorldQueuePrimitivePlan actualPlan, int sourceNode);
    void RequireCurrent(ArmyWorldQueuePrimitivePlan actualPlan, int sourceNode);
    string SourceEvidence { get; }
    ArmyWorldCurrentTuplePublication ActualTuplePublication { get; }
}

/// <summary>Current World renderer lifecycle comes from its owning record,
/// shader/cache/plugin owner. Declarations do not manufacture retained gates.</summary>
public interface IArmyWorldQueueCurrentSource
{
    void RequireCurrent(NativeMapArmyPreparedFrame actualArmy, ArmyWorldPrimitivePublication actualPublication);
    ModelFreshOwnedStencilPublication ReadActualSelectedRenderer(ArmyWorldQueuePrimitiveCandidate candidate);
}

/// <summary>Original D436DA bound-mask and D48742 per-record masks determine
/// these actual draw-time color states. A global full-RGBA substitute is not
/// an implementation of this source capability.</summary>
public interface IArmyWorldQueueNativeColorSource
{
    IReadOnlyList<ModelBlendAttachment> ReadActualDrawTimeStates(ArmyWorldQueuePrimitiveCandidate candidate);
    IReadOnlyList<ModelBlendAttachment> ReadActualDrawTimeStates(ArmyWorldQueuePrimitiveCandidate candidate,
        IReadOnlyList<ModelBlendAttachment> actualPreparedAttachments)
        => ReadActualDrawTimeStates(candidate);
    void RequireCurrent(ArmyWorldQueuePrimitivePlan actualPlan, int sourceNode);
}

public sealed record ArmyWorldQueueSubmittedPrimitive(string CandidateIdentity, string RecordOwnerIdentity,
    string CanonicalPacketKey, ModelGeometrySubmittedPrimitive ActualPrimitive,
    IReadOnlyList<ModelBlendAttachment> ActualDeviceColorStates);

/// <summary>Copied only after successful render-thread submission. This is
/// an execution receipt, not a GPU fence/readback or a completed World graph.</summary>
public sealed class ArmyWorldQueueSubmission
{
    internal ArmyWorldQueueSubmission(ArmyWorldQueueGraphicsOwner owner,
        ArmyWorldQueuePreparedFrame prepared, int node, ModelGeometrySubmittedFrame actual,
        IReadOnlyList<ArmyWorldQueueSubmittedPrimitive> primitives, string orderEvidence)
        => (Owner, Prepared, SourceQueueNode, Target, LayerSubmissionOrdinal, Primitives, SourceOrderEvidence) =
            (owner, prepared, node, actual.Target, actual.LayerSubmissionOrdinal,
                Array.AsReadOnly(primitives.ToArray()), orderEvidence);
    public ArmyWorldQueueGraphicsOwner Owner { get; }
    public ArmyWorldQueuePreparedFrame Prepared { get; }
    public int SourceQueueNode { get; }
    public RenderTargetFrame Target { get; }
    public long LayerSubmissionOrdinal { get; }
    public IReadOnlyList<ArmyWorldQueueSubmittedPrimitive> Primitives { get; }
    public string SourceOrderEvidence { get; }
    public void RequireAccepted() => Owner.RequireAccepted(this);
}

public sealed class ArmyWorldQueuePreparedFrame
{
    internal ArmyWorldQueuePreparedFrame(ArmyWorldQueueGraphicsOwner owner,
        ArmyWorldQueuePreparedPackets body, ArmyWorldQueuePreparedPackets flags,
        ArmyWorldQueuePrimitivePlan plan, MapCommonRenderState common,
        uint contextFlags, IReadOnlyDictionary<string,ModelGbufferInstancePacket> packets,
        IArmyWorldQueueCurrentSource current)
        => (Owner, Body, Flags, Plan, Common, ActualContextFlags3E4, Packets, CurrentSource) =
            (owner, body, flags, plan, common, contextFlags, packets, current);
    public ArmyWorldQueueGraphicsOwner Owner { get; }
    public ArmyWorldQueuePreparedPackets Body { get; }
    public ArmyWorldQueuePreparedPackets Flags { get; }
    public ArmyWorldQueuePrimitivePlan Plan { get; }
    public MapCommonRenderState Common { get; }
    public uint ActualContextFlags3E4 { get; }
    internal IReadOnlyDictionary<string,ModelGbufferInstancePacket> Packets { get; }
    internal IArmyWorldQueueCurrentSource CurrentSource { get; }
    public void RequireCurrent()
    {
        Owner.RequireCurrent(this); Body.RequireCurrent(); Flags.RequireCurrent();
        CurrentSource.RequireCurrent(Body.Frame, Plan.Publication);
    }
}

public sealed class ArmyWorldQueuesCompletedFrame
{
    internal ArmyWorldQueuesCompletedFrame(ArmyWorldQueueGraphicsOwner owner,
        ArmyWorldQueuePreparedFrame prepared, ArmyWorldQueueSubmission opaque,
        ArmyWorldQueueSubmission alpha)
        => (Owner, Prepared, Opaque49, Alpha50) = (owner, prepared, opaque, alpha);
    public ArmyWorldQueueGraphicsOwner Owner { get; }
    public ArmyWorldQueuePreparedFrame Prepared { get; }
    public ArmyWorldQueueSubmission Opaque49 { get; }
    public ArmyWorldQueueSubmission Alpha50 { get; }
    public ArmyWorldQueuesCompletedPublication Publication { get; internal set; } = null!;
    public void RequireAccepted()
    {
        Opaque49.RequireAccepted(); Alpha50.RequireAccepted();
        if (!ReferenceEquals(Owner.LastCompletedFrame, this))
            throw new InvalidDataException("Army two-queue aggregate was superseded, failed or retired.");
    }
}

/// <summary>One shared device layer merges body and linked flag packets before
/// either queue is drawn. Counts remain the complete record-owner publication.
/// Actual native order and target binding are explicit caller capabilities.</summary>
public sealed class ArmyWorldQueueGraphicsOwner : IAsyncDisposable
{
    private readonly ModelGbufferLayer layer;
    private readonly SemaphoreSlim gate = new(1,1);
    private ArmyWorldQueuePreparedFrame? current;
    private readonly Dictionary<int,ArmyWorldQueueSubmission> submitted = [];
    private ArmyWorldCurrentTuplePublication? orderedTuples;
    private Army48TargetBinding? boundTarget;
    private bool retired;
    public ArmyWorldQueuesCompletedFrame? LastCompletedFrame { get; private set; }
    public int ProgramCount => layer.ProgramCount;
    public int GeometryCount => layer.GeometryCount;
    public long DrawCreationCount => layer.DrawCreationCount;
    public ArmyWorldQueueGraphicsOwner(Vkd3dShaderCompiler actualCompiler)
        => layer = ModelGbufferLayer.Create(actualCompiler);
    public IReadOnlyList<ArmyWorldQueueSubmission> CompletedQueues => submitted.OrderBy(p => p.Key).Select(p => p.Value).ToArray();

    public ArmyWorldQueuePreparedFrame PrepareActualQueues(ArmyWorldQueuePreparedPackets body,
        ArmyWorldQueuePreparedPackets flags, ArmyWorldPrimitivePublication actualPublication,
        MapCommonRenderState actualCommon, uint actualContextFlags3E4,
        IArmyWorldQueueCurrentSource actualCurrentSource)
    {
        ObjectDisposedException.ThrowIf(retired, this);
        ArgumentNullException.ThrowIfNull(body); ArgumentNullException.ThrowIfNull(flags);
        ArgumentNullException.ThrowIfNull(actualPublication); ArgumentNullException.ThrowIfNull(actualCommon);
        ArgumentNullException.ThrowIfNull(actualCurrentSource);
        body.RequireCurrent(); flags.RequireCurrent();
        actualCurrentSource.RequireCurrent(body.Frame, actualPublication);
        if (!ReferenceEquals(body.Frame, flags.Frame) || body.Family != "ArmyOrdinary" || flags.Family != "ArmyFlag" ||
            body.Frame.Context.FrameNumber != actualPublication.Counts.Queue.FrameNumber ||
            actualCommon.FrameNumber != actualPublication.Counts.Queue.FrameNumber ||
            actualCommon.SelectedViewOwnerIdentity != actualPublication.Counts.Queue.Binding.ViewIdentity ||
            (actualContextFlags3E4 & (1u << 29)) == 0 || (actualContextFlags3E4 & 3) != 1)
            throw new InvalidDataException("Army ordinary World queues require this current same-frame/View publication, renderer mode1 and actual reverse-depth bit29.");
        var packets = body.Packets.Concat(flags.Packets).ToDictionary(p => p.InstanceKey, StringComparer.Ordinal);
        if (!packets.Keys.Order(StringComparer.Ordinal).SequenceEqual(actualPublication.Primitives.Select(p => p.PacketKey).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Army queues must prepare every actual canonical body/pole/cloth primitive, including invisible ones.");
        foreach (var primitive in actualPublication.Primitives)
        {
            var packet = packets[primitive.PacketKey];
            var actor = actualPublication.Actors.Single(a => a.ActorIdentity == primitive.ActorIdentity);
            if (packet.DrawIndex != primitive.SourceDrawIndex || packet.Model.Source.ResourceUri != actor.Source.Model.Source.ResourceUri ||
                packet.Model.Source.DecodedSha256 != actor.Source.Model.Source.DecodedSha256)
                throw new InvalidDataException("Army GPU packet geometry differs from this actual owning-record CPU inventory.");
        }
        var plan = ArmyWorldQueuePrimitivePlan.PrepareFreshSourceCandidates(actualPublication,
            body.Sources.Concat(flags.Sources).ToArray(), actualContextFlags3E4);
        // All source shaders in this finite family publish group code2/Back.
        // Other group ordinals need their actual selected type10 recipe.
        if (plan.SourceCandidates.Any(c => c.SourceGroupRaster is not { GroupRasterCode: 2, NativeCullMode: 3,
                FrontCounterClockwise: true } || c.SourceQueueDepth is not { DepthTest: true, DepthWrite: true, CompareCode: 6 }))
            throw new NotSupportedException("Army queue backend currently requires its source group Back and original ordinary GreaterEqual depth profile.");
        submitted.Clear(); LastCompletedFrame=null; orderedTuples=null; boundTarget=null;
        return current = new(this, body, flags, plan, actualCommon, actualContextFlags3E4, packets, actualCurrentSource);
    }

    public async Task<ArmyWorldQueueSubmission> SubmitQueueAsync(ArmyWorldQueuePreparedFrame prepared,
        int sourceQueueNode, Army48TargetBinding actualBoundTarget,
        IArmyWorldQueueNativeColorSource actualNativeColorSource,
        IArmyWorldQueueOrderSource actualSourceOrdering)
    {
        ArgumentNullException.ThrowIfNull(prepared); ArgumentNullException.ThrowIfNull(actualBoundTarget);
        if (actualSourceOrdering is null)
            throw new NotSupportedException("Army queue submission needs the actual original first-counter tuple and selected sort descriptor/key producer; canonical candidates are not native order.");
        ArgumentNullException.ThrowIfNull(actualNativeColorSource);
        await gate.WaitAsync();
        try
        {
            prepared.RequireCurrent(); actualBoundTarget.RequireCurrent();
            if (!ReferenceEquals(actualBoundTarget.Context, prepared.Body.Frame.Context) || sourceQueueNode is not (49 or 50) ||
                submitted.ContainsKey(sourceQueueNode) || (sourceQueueNode == 50 && !submitted.ContainsKey(49)))
                throw new InvalidDataException("Army submission must bind current node48, then execute each source queue49/50 once in graph order.");
            actualSourceOrdering.RequireCurrent(prepared.Plan, sourceQueueNode);
            actualNativeColorSource.RequireCurrent(prepared.Plan, sourceQueueNode);
            var tuples = actualSourceOrdering.ActualTuplePublication;
            if (!ReferenceEquals(tuples.Population, prepared.Plan.Publication) ||
                (orderedTuples is not null && !ReferenceEquals(orderedTuples, tuples)) ||
                (boundTarget is not null && !ReferenceEquals(boundTarget, actualBoundTarget)))
                throw new InvalidDataException("Army49/50 must consume the same actual full first-bank/table publication and bound node48 scope.");
            orderedTuples = tuples; boundTarget = actualBoundTarget;
            var expected = prepared.Plan.SourceCandidates.Where(c => c.QueueNodeIndex == sourceQueueNode)
                .ToDictionary(c => c.CandidateIdentity, StringComparer.Ordinal);
            var order = actualSourceOrdering.ReadActualOrderedCandidateIdentities(prepared.Plan, sourceQueueNode).ToArray();
            if (string.IsNullOrWhiteSpace(actualSourceOrdering.SourceEvidence) || order.Length != expected.Count ||
                order.Distinct(StringComparer.Ordinal).Count() != order.Length || order.Any(k => !expected.ContainsKey(k)))
                throw new InvalidDataException("Actual Army source ordering must contain exactly this queue's record-owner candidates.");
            var packets = new List<ModelGbufferInstancePacket>();
            foreach (string identity in order)
            {
                var candidate = expected[identity]; var canonical = prepared.Packets[candidate.Primitive.PacketKey];
                var colors = Army48GraphicsContract.PrepareAttachments(
                    actualNativeColorSource.ReadActualDrawTimeStates(candidate, canonical.Raster.Attachments));
                var currentRenderer = prepared.CurrentSource.ReadActualSelectedRenderer(candidate);
                if (currentRenderer.PrimitiveIdentity != canonical.InstanceKey ||
                    !candidate.SelectedSource.SelectsCurrentPrimitive(currentRenderer))
                    throw new InvalidDataException("Current Army selected renderer does not admit this exact primitive into its source alpha queue.");
                var record = new ModelOrdinaryDispatcherRecord(prepared.Common.FrameNumber,
                    prepared.Plan.Publication.Counts.Queue.Binding.ViewIdentity, candidate.RecordMask78, true,
                    prepared.Plan.Publication.CompleteCountsByRecordOwner[candidate.RecordOwnerIdentity], true,
                    prepared.Plan.Publication.Counts.PublicationIdentity + ";owner=" + candidate.RecordOwnerIdentity);
                var stencil = candidate.SelectedSource.SelectedRenderer.PrepareCurrent(record, prepared.Common,
                    prepared.ActualContextFlags3E4, currentRenderer);
                if (!stencil.ShouldSubmitPrimitive)
                    throw new InvalidDataException("Army source candidate reached a dispatcher gate excluded from the prepared queue; its current producer is required.");
                var boundStencil = ModelNativeStencilRaster.Bind(prepared.Body.Frame.Context, canonical, stencil);
                packets.Add(canonical with { InstanceKey = identity, Raster = canonical.Raster with
                { Cull = RenderingDevice.PolygonCullMode.Back, FrontFace = RenderingDevice.PolygonFrontFace.CounterClockwise,
                    DepthTest = true, DepthWrite = true, DepthCompare = RenderingDevice.CompareOperator.GreaterOrEqual,
                    Attachments = colors, NativeStencil = boundStencil } });
            }
            prepared.RequireCurrent(); actualSourceOrdering.RequireCurrent(prepared.Plan, sourceQueueNode);
            var actual = await layer.RenderArmyQueueAsync(actualBoundTarget, sourceQueueNode, packets, () =>
            { prepared.RequireCurrent(); actualSourceOrdering.RequireCurrent(prepared.Plan, sourceQueueNode);
                actualNativeColorSource.RequireCurrent(prepared.Plan, sourceQueueNode); });
            actual.RequireCurrent();
            var result = new ArmyWorldQueueSubmission(this, prepared, sourceQueueNode, actual,
                actual.Primitives.Select((p,i) => new ArmyWorldQueueSubmittedPrimitive(order[i],
                    expected[order[i]].RecordOwnerIdentity, expected[order[i]].Primitive.PacketKey, p,
                    Array.AsReadOnly(packets[i].Raster.Attachments.ToArray()))).ToArray(),
                actualSourceOrdering.SourceEvidence);
            submitted.Add(sourceQueueNode, result); return result;
        }
        catch { current=null; submitted.Clear(); LastCompletedFrame=null; orderedTuples=null; boundTarget=null; await layer.RetireTargetAsync(); throw; }
        finally { gate.Release(); }
    }
    internal void RequireCurrent(ArmyWorldQueuePreparedFrame prepared)
    {
        ObjectDisposedException.ThrowIf(retired, this);
        if (!ReferenceEquals(current, prepared)) throw new InvalidDataException("Army queue preparation was superseded or retired.");
    }
    internal void RequireAccepted(ArmyWorldQueueSubmission actual)
    {
        RequireCurrent(actual.Prepared);
        if (!submitted.TryGetValue(actual.SourceQueueNode, out var found) || !ReferenceEquals(found, actual))
            throw new InvalidDataException("Army queue receipt was failed, superseded or retired.");
    }
    /// <summary>Publish only after both actual source queues succeeded. The
    /// copied49 receipt is an accepted earlier stage; only50 is layer-current.</summary>
    public ArmyWorldQueuesCompletedFrame CompleteActualQueues(ArmyWorldQueuePreparedFrame prepared)
    {
        prepared.RequireCurrent();
        if (!submitted.TryGetValue(49,out var opaque) || !submitted.TryGetValue(50,out var alpha) ||
            !ReferenceEquals(opaque.Prepared,prepared) || !ReferenceEquals(alpha.Prepared,prepared) ||
            !ReferenceEquals(opaque.Target,alpha.Target) || layer.LastSubmittedFrame is not {} terminal ||
            terminal.LayerSubmissionOrdinal!=alpha.LayerSubmissionOrdinal ||
            opaque.Primitives.Count+alpha.Primitives.Count!=prepared.Plan.SourceCandidates.Count ||
            orderedTuples is null || boundTarget is null)
            throw new InvalidDataException("Army aggregate requires complete actual49/50 submissions on the same six MRT/E13 target.");
        terminal.RequireCurrent();
        var completed = new ArmyWorldQueuesCompletedFrame(this,prepared,opaque,alpha);
        completed.Publication = new(completed, orderedTuples, boundTarget);
        return LastCompletedFrame=completed;
    }
    public async Task RetireTargetAsync()
    {
        await gate.WaitAsync();
        try { current = null; submitted.Clear(); LastCompletedFrame=null; orderedTuples=null; boundTarget=null; await layer.RetireTargetAsync(); }
        finally { gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try { retired = true; current = null; submitted.Clear(); LastCompletedFrame=null; orderedTuples=null; boundTarget=null; await layer.DisposeAsync(); }
        finally { gate.Release(); }
    }
}
