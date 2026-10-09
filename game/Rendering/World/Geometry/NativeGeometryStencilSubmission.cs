using San14.Data;

public delegate ModelOrdinaryNativeStencilReceipt StaticPrimitiveNativeStencilProvider(
    MapStaticModelPreparedState primitive, MapSourceCreatedKind1ViewRecord record);

/// <summary>Complete finite static batch. Record order and source primitive
/// order are retained. Unassigned primitives stay visible CPU candidates;
/// this owner never invents their missing ordinary record route.</summary>
public sealed class NativeGeometryStencilSubmission
{
    private readonly ModelGbufferInstancePacket[] packets;
    private NativeGeometryStencilSubmission(NativeMapFrameContext context,
        MapSourceCreatedStaticPrimitiveAdmissionPlan admission,
        ModelGbufferInstancePacket[] packets)
        => (Context, Admission, this.packets) = (context, admission, packets);
    public NativeMapFrameContext Context { get; }
    public MapSourceCreatedStaticPrimitiveAdmissionPlan Admission { get; }
    public IReadOnlyList<ModelGbufferInstancePacket> Packets => Array.AsReadOnly(packets.ToArray());
    public int UnassignedPrimitiveCount => Admission.UnassignedKind1PrimitiveCount;
    public static NativeGeometryStencilSubmission PrepareStatic(NativeMapFrameContext context,
        MapSourceCreatedStaticPrimitiveAdmissionPlan admission,
        MapStaticModelPreparedFrame actualPrepared,
        StaticPrimitiveNativeStencilProvider currentFinalSource)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(actualPrepared); ArgumentNullException.ThrowIfNull(currentFinalSource);
        if (context.FrameNumber != admission.FrameNumber || !actualPrepared.Complete ||
            !ReferenceEquals(actualPrepared.Visibility, admission.CpuPopulation.Visibility) ||
            actualPrepared.Packets.Count != actualPrepared.MaterialStates.Count)
            throw new InvalidDataException("Static native stencil needs the exact complete F103 CPU preparation and accepted scene attempt.");
        var states = actualPrepared.MaterialStates;
        foreach (var state in states)
        {
            var member = admission.Require(state.Context.Instance.InstanceKey, state.Context.DrawIndex);
            if (member.CurrentSourceIdentity != state.Context.Instance.CurrentSourceIdentity ||
                state.Context.PacketKey != state.Prepared.Packet.InstanceKey)
                throw new InvalidDataException("Static stencil source primitive identity differs from current admission.");
        }
        var output = new List<ModelGbufferInstancePacket>();
        foreach (var record in admission.Frame.ViewFields.Kind1Records)
        foreach (var state in states)
        {
            if (!admission.Require(state.Context.Instance.InstanceKey, state.Context.DrawIndex).IsAdmittedTo(record.Mask78)) continue;
            var source = currentFinalSource(state, record) ?? throw new InvalidDataException("Current final native stencil source is unavailable.");
            if (source.DrawTimeState.RecordMask78 != record.Mask78 || source.CurrentPublication.ViewIdentity != admission.ViewIdentity)
                throw new InvalidDataException("Native stencil receipt uses another actual View record.");
            if (!source.ShouldSubmitPrimitive) continue;
            var packet = state.Prepared.Packet;
            var native = ModelNativeStencilRaster.Bind(context, packet, source);
            output.Add(packet with { InstanceKey = packet.InstanceKey + "|native-record:" + record.Mask78.ToString("X8"),
                Raster = packet.Raster with { NativeStencil = native } });
        }
        return new(context, admission, output.ToArray());
    }
    internal NativeGeometryStencilCompletedFrame Complete(ModelGeometrySubmittedFrame actual)
    {
        actual.RequireCurrent();
        if (!ReferenceEquals(actual.Target, Context.TerrainFrame) || actual.Primitives.Count != packets.Length ||
            actual.Primitives.Where((p,i) => p.PacketKey != packets[i].InstanceKey ||
                !ReferenceEquals(p.NativeStencil, packets[i].Raster.NativeStencil)).Any())
            throw new InvalidDataException("Native stencil completion does not match the submitted complete source batch.");
        return new(this, actual);
    }
}

/// <summary>Produced only by the actual static renderer after successful
/// Layer.RenderAsync, never by a Boolean acceptance callback.</summary>
public sealed class NativeGeometryStencilCompletedFrame
{
    internal NativeGeometryStencilCompletedFrame(NativeGeometryStencilSubmission submission,
        ModelGeometrySubmittedFrame actual) => (Submission, ActualSubmission) = (submission, actual);
    public NativeGeometryStencilSubmission Submission { get; }
    public ModelGeometrySubmittedFrame ActualSubmission { get; }
    public void ValidateCurrent(NativeMapFrameContext context,
        MapSourceCreatedF103CurrentFrame f103)
    {
        ActualSubmission.RequireCurrent();
        if (!ReferenceEquals(context, Submission.Context) || !ReferenceEquals(f103, Submission.Admission.Frame) ||
            !ReferenceEquals(context.TerrainFrame, ActualSubmission.Target))
            throw new InvalidDataException("Native geometry stencil receipt belongs to another accepted context, target or F103 frame.");
    }
}
