using San14.Data;

/// <summary>One source-admitted record-owner/primitive candidate. Candidate
/// enumeration is membership only; it never supplies native sorted order.</summary>
public sealed record OriginalNativeMapStaticQueueCandidate(int QueueNodeIndex,
    string CandidateIdentity, string RecordOwnerIdentity,
    OriginalMapStaticModelPreparedState MaterialState,
    OriginalModelOrdinaryNativeStencilReceipt SourceStencil,
    OriginalModelGbufferInstancePacket Packet);

/// <summary>The original tuple/key producer owns ordering. This interface has
/// no canonical-array or lexical-order fallback.</summary>
public interface IOriginalStaticWorldQueueOrderSource
{
    IReadOnlyList<string> ReadActualOrderedCandidateIdentities(
        OriginalNativeMapStaticQueuePlan actualPlan, int sourceNode);
    void RequireCurrent(OriginalNativeMapStaticQueuePlan actualPlan, int sourceNode);
    string SourceEvidence { get; }
}

public sealed class OriginalNativeMapStaticQueuePlan
{
    private OriginalNativeMapStaticQueuePlan(OriginalNativeMapFrameContext context,
        OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan admission,
        OriginalMapStaticModelPreparedFrame prepared,
        IReadOnlyList<OriginalNativeMapStaticQueueCandidate> candidates)
        => (Context, Admission, Prepared, SourceCandidates) = (context, admission, prepared, candidates);
    public OriginalNativeMapFrameContext Context { get; }
    public OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan Admission { get; }
    public OriginalMapStaticModelPreparedFrame Prepared { get; }
    public IReadOnlyList<OriginalNativeMapStaticQueueCandidate> SourceCandidates { get; }

    public static OriginalNativeMapStaticQueuePlan Prepare(OriginalNativeMapFrameContext context,
        OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan admission,
        OriginalMapStaticModelPreparedFrame prepared,
        OriginalNativeStaticStencilCurrentOwner currentStencilOwner)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(prepared); ArgumentNullException.ThrowIfNull(currentStencilOwner);
        var worldQueue = admission.CurrentWorldQueue ??
            throw new InvalidDataException("Static54/55 needs the actual record-owner keyed World queue.");
        if (context.FrameNumber != admission.FrameNumber || !prepared.Complete ||
            !ReferenceEquals(prepared.Visibility, admission.CpuPopulation.Visibility) ||
            prepared.Packets.Count != prepared.MaterialStates.Count)
            throw new InvalidDataException("Static queues require this complete current CPU population, Camera and primitive preparation.");
        var candidates = new List<OriginalNativeMapStaticQueueCandidate>();
        var records = admission.Frame.ViewFields.Kind1Records.ToDictionary(r => r.LightOwnerIdentity, StringComparer.Ordinal);
        foreach (var state in prepared.MaterialStates)
        {
            var member = admission.Require(state.Context.Instance.InstanceKey, state.Context.DrawIndex);
            if (member.CurrentSourceIdentity != state.Context.Instance.CurrentSourceIdentity ||
                state.Context.PacketKey != state.Prepared.Packet.InstanceKey ||
                state.Context.Selection.Source?.GraphSource.Node.Index is not (54 or 55))
                throw new InvalidDataException("Static queue source primitive differs from its current admission or selected54/55 renderer.");
            foreach (var record in worldQueue.Records)
            {
                if (!member.IsAdmittedToWorldRecord(record.RecordOwnerIdentity)) continue;
                var receipt = currentStencilOwner.PrepareCurrentPrimitive(context, admission, state,
                    records[record.RecordOwnerIdentity]);
                if (!receipt.ShouldSubmitPrimitive) continue;
                var packet = state.Prepared.Packet;
                var native = OriginalModelNativeStencilRaster.Bind(context, packet, receipt);
                string key = packet.InstanceKey + "|native-record-owner:" + record.RecordOwnerIdentity;
                candidates.Add(new(receipt.Source.QueueNodeIndex, key, record.RecordOwnerIdentity, state,
                    receipt, packet with { InstanceKey = key, Raster = packet.Raster with { NativeStencil = native } }));
            }
        }
        if (candidates.Select(c => c.CandidateIdentity).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
            throw new InvalidDataException("Current static record-owner/primitive identities are not unique.");
        return new(context, admission, prepared, candidates.AsReadOnly());
    }

    public IReadOnlyList<OriginalNativeMapStaticQueueCandidate> ReadOrderedQueue(int node,
        IOriginalStaticWorldQueueOrderSource actualOrder)
    {
        ArgumentNullException.ThrowIfNull(actualOrder);
        if (node is not (54 or 55)) throw new ArgumentOutOfRangeException(nameof(node));
        actualOrder.RequireCurrent(this, node);
        var selected = SourceCandidates.Where(c => c.QueueNodeIndex == node)
            .ToDictionary(c => c.CandidateIdentity, StringComparer.Ordinal);
        var order = actualOrder.ReadActualOrderedCandidateIdentities(this, node) ??
            throw new InvalidDataException("Actual static queue order is unavailable.");
        if (string.IsNullOrWhiteSpace(actualOrder.SourceEvidence) || order.Count != selected.Count ||
            order.Distinct(StringComparer.Ordinal).Count() != order.Count || order.Any(key => !selected.ContainsKey(key)))
            throw new InvalidDataException("Actual static queue order must cover every admitted primitive exactly once.");
        return Array.AsReadOnly(order.Select(key => selected[key]).ToArray());
    }
}
