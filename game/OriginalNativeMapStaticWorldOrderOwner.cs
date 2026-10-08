using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

public sealed record OriginalStaticWorldCurrentPrimitiveTuple(string InstanceKey, int SourceDrawIndex,
    OriginalMapStaticModelPreparedState MaterialState, OriginalNativeWorldShaderTableState ShaderState,
    OriginalWorldPrimitiveModelDistance SourceDistance, OriginalMapModelNativeOrientedBounds CurrentBounds,
    float CurrentDistance, ulong FirstCounterKey);

/// <summary>Actual current CPU tuple publication before GPU work. The list
/// retains the accepted population's publisher order; no sort is inferred
/// from this list.</summary>
public sealed class OriginalStaticWorldCurrentTuplePublication
{
    internal OriginalStaticWorldCurrentTuplePublication(OriginalMapStaticModelCurrentOwnedFrame population,
        OriginalMapStaticModelPreparedFrame prepared, OriginalWorldPrimitiveDepthRange range, Vector4 plane,
        IReadOnlyList<OriginalStaticWorldCurrentPrimitiveTuple> tuples)
        => (Population, Prepared, ViewRange, ViewZPlane, Tuples) = (population, prepared, range, plane, tuples);
    public OriginalMapStaticModelCurrentOwnedFrame Population { get; }
    public OriginalMapStaticModelPreparedFrame Prepared { get; }
    public OriginalWorldPrimitiveDepthRange ViewRange { get; }
    public Vector4 ViewZPlane { get; }
    public IReadOnlyList<OriginalStaticWorldCurrentPrimitiveTuple> Tuples { get; }
    internal Dictionary<(OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan Admission, string RecordOwner),
        IReadOnlyList<OriginalStaticWorldCurrentPrimitiveTuple>> SortedFirstBanks { get; } = [];
}

/// <summary>The current first-bank publication and loaded ViewParam radix
/// consumer. Ordinary alpha draw selection is applied after this bank sort;
/// it does not select the Alpha sort descriptor.</summary>
public sealed class OriginalNativeMapStaticWorldOrderOwner
{
    private readonly OriginalSceneViewNativeMapModelSourceOwner models;
    private readonly OriginalWorldPrimitiveOrderSource viewOrder;
    private readonly Func<OriginalMapStaticModelPreparedState, OriginalNativeWorldShaderTableState> readActualFinalTable;
    private readonly Dictionary<OriginalMapStaticModelAsset, OriginalWorldPrimitiveModelDistance> distances
        = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, (OriginalMapStaticModelAsset Asset, OriginalMapModelNativeFreshVisibilityPrefix Prefix)> freshBounds
        = new(StringComparer.Ordinal);
    private OriginalStaticWorldCurrentTuplePublication? current;
    public OriginalNativeMapStaticWorldOrderOwner(OriginalSceneViewNativeMapModelSourceOwner actualModels,
        OriginalWorldPrimitiveOrderSource actualViewParameter,
        Func<OriginalMapStaticModelPreparedState, OriginalNativeWorldShaderTableState> readActualFinalShaderTable)
    {
        models = actualModels ?? throw new ArgumentNullException(nameof(actualModels));
        viewOrder = actualViewParameter ?? throw new ArgumentNullException(nameof(actualViewParameter));
        readActualFinalTable = readActualFinalShaderTable ?? throw new ArgumentNullException(nameof(readActualFinalShaderTable));
    }
    public string SourceEvidence => viewOrder.SourceIdentity +
        ";actual-owned-ModelCPU-force1A0/bias1A4/current-OBB/PreparedView18C0-plane40/range19C0-19C4;" +
        "BB85A0/BB8680-current-first-bank-publication;D58B50-bank0/D4E790-group0;" +
        "D5BDA0/D4B7E0-stable-radix;base-full-type16-table-ordered-allocation-identity;" +
        "54/55-alpha-filter-after-first-bank-sort;not-GPU-completion";

    public OriginalStaticWorldCurrentTuplePublication PrepareCurrentTuples(
        OriginalMapStaticModelCurrentOwnedFrame actualPopulation,
        OriginalMapStaticModelPreparedFrame actualPrepared)
    {
        ArgumentNullException.ThrowIfNull(actualPopulation); ArgumentNullException.ThrowIfNull(actualPrepared);
        if (actualPopulation.FrameNumber <= 0 || !actualPopulation.Population.Complete || !actualPrepared.Complete ||
            !ReferenceEquals(actualPopulation.Visibility, actualPrepared.Visibility))
            throw new InvalidDataException("Static primitive order needs the exact complete current CPU population and packet preparation.");
        var sourceOrder = actualPopulation.Visibility.Visible.SelectMany(instance =>
            instance.Asset.Draws.Select(draw => (instance.InstanceKey, Draw: draw.Index))).ToArray();
        if (!sourceOrder.SequenceEqual(actualPrepared.MaterialStates.Select(state =>
            (state.Context.Instance.InstanceKey, state.Context.DrawIndex))))
            throw new InvalidDataException("Prepared static primitive order differs from its actual current CPU publisher.");
        var prefix = actualPopulation.MainCamera.Camera.Prefix.Span;
        var range = OriginalWorldPrimitiveDistanceKey.PrepareView(
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x140..]),
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x144..]));
        var plane = OriginalWorldPrimitiveDistanceKey.PrepareViewZPlane(
            ReadMatrix(prefix[0x100..]), BinaryPrimitives.ReadUInt32LittleEndian(prefix[0x158..]));
        var rows = new List<OriginalStaticWorldCurrentPrimitiveTuple>(sourceOrder.Length);
        var bounds = new Dictionary<string, OriginalMapModelNativeOrientedBounds>(StringComparer.Ordinal);
        foreach (var state in actualPrepared.MaterialStates)
        {
            var instance = state.Context.Instance;
            if (instance.NativePublication is not { FreshConstructorBindPrefix: true, ParentBound: false } ||
                string.IsNullOrWhiteSpace(instance.CurrentSourceIdentity))
                throw new NotSupportedException("Changed static flags/parent bounds require their actual current OBB publication.");
            if (!bounds.TryGetValue(instance.InstanceKey, out var bound))
            {
                if (!freshBounds.TryGetValue(instance.CurrentSourceIdentity, out var loaded) || !ReferenceEquals(loaded.Asset, instance.Asset))
                {
                    loaded = (instance.Asset, OriginalMapModelNativeVisibilitySources.PrepareFreshSingleNode(instance,
                        new(instance.CurrentSourceIdentity, false, true, true)));
                    freshBounds[instance.CurrentSourceIdentity] = loaded;
                }
                var fresh = loaded.Prefix;
                if (!fresh.UseModelBounds)
                    throw new NotSupportedException("Static order View-extrema bound needs its actual current source producer.");
                bound = OriginalMapModelNativeBoundsSources.FromSourceWorld(instance, fresh.BoundsPublication);
                bounds.Add(instance.InstanceKey, bound);
            }
            if (!distances.TryGetValue(instance.Asset, out var declaration))
            {
                declaration = OriginalWorldPrimitiveDistanceKey.ReadModelDeclaration(instance.Asset.TextureResources.ModelObject);
                distances.Add(instance.Asset, declaration);
            }
            float distance = declaration.ForceRegisterNormalZDistance1A0;
            if (distance < 0f) distance = OriginalWorldPrimitiveDistanceKey.ReadOrientedBoundsDistance(bound, plane);
            var shaderState = readActualFinalTable(state) ??
                throw new InvalidDataException("Current final primitive18 shader table is unavailable; base shader cannot fill an unknown material override.");
            models.RequireCurrentShaderState(shaderState);
            var actualProvider = models.ReadSelectedShaderInterface(shaderState,
                state.Context.Selection.Source!.GraphSource.Interface.StateIndex).Provider;
            var packetProvider = state.Context.Selection.Source.GraphSource.Interface.Provider;
            if (actualProvider?.Object.ObjectId != packetProvider?.Object.ObjectId ||
                actualProvider?.Objects.Source.ResourceUri != packetProvider?.Objects.Source.ResourceUri ||
                actualProvider?.Objects.Source.DecodedSha256 != packetProvider?.Objects.Source.DecodedSha256)
                throw new InvalidDataException("Current final shader table does not select this packet's actual renderer provider.");
            rows.Add(new(instance.InstanceKey, state.Context.DrawIndex, state, shaderState, declaration, bound,
                distance, OriginalWorldPrimitiveDistanceKey.PrepareFirstCounter(range, distance, declaration.RegisterNormalZDistanceBias1A4)));
        }
        return current = new(actualPopulation, actualPrepared, range, plane, rows.AsReadOnly());
    }

    public IOriginalStaticWorldQueueOrderSource PrepareCurrentOrder(OriginalNativeMapStaticQueuePlan actualPlan)
    {
        ArgumentNullException.ThrowIfNull(actualPlan);
        var publication = PrepareCurrentTuples(actualPlan.Admission.CpuPopulation, actualPlan.Prepared);
        return new CurrentOrder(this, actualPlan, publication);
    }

    /// <summary>Sort one actual owner's first bank. CPU publication/counts
    /// and record membership remain available even without a GPU target.</summary>
    public IReadOnlyList<OriginalStaticWorldCurrentPrimitiveTuple> ReadSortedFirstCounter(
        OriginalStaticWorldCurrentTuplePublication publication,
        OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan actualAdmission, string currentRecordOwnerIdentity)
    {
        ArgumentNullException.ThrowIfNull(publication); ArgumentNullException.ThrowIfNull(actualAdmission);
        RequireCurrentPublication(publication);
        var queue = actualAdmission.CurrentWorldQueue ??
            throw new NotSupportedException("Static bank ordering needs the actual record-owner keyed World queue.");
        if (!ReferenceEquals(actualAdmission.CpuPopulation, publication.Population) ||
            !queue.Records.Any(record => record.RecordOwnerIdentity == currentRecordOwnerIdentity))
            throw new InvalidDataException("Static first-counter order needs the exact current CPU publication and owning World record.");
        var cacheKey = (actualAdmission, currentRecordOwnerIdentity);
        if (publication.SortedFirstBanks.TryGetValue(cacheKey, out var found)) return found;
        var admitted = publication.Tuples.Where(tuple => actualAdmission.Require(tuple.InstanceKey, tuple.SourceDrawIndex)
            .IsAdmittedToWorldRecord(currentRecordOwnerIdentity)).ToArray();
        var result = Array.AsReadOnly(viewOrder.Sort(OriginalWorldPrimitiveSortFamily.Opaque, admitted,
            (tuple, field) => field switch { 3 => tuple.ShaderState.OrderedAllocationIdentity,
                14 => tuple.FirstCounterKey, _ => throw new NotSupportedException("This current static tuple producer has no source for selected sort field " + field) }));
        publication.SortedFirstBanks.Add(cacheKey, result);
        return result;
    }

    public OriginalStaticWorldCurrentTuplePublication RequireCurrentTuples()
    {
        var publication=current??throw new InvalidOperationException("No current static first-bank tuple publication.");
        RequireCurrentPublication(publication);return publication;
    }

    private void RequireCurrentPublication(OriginalStaticWorldCurrentTuplePublication publication)
    {
        if (!ReferenceEquals(current, publication))
            throw new InvalidDataException("Static tuple publication has been superseded.");
        foreach (var state in publication.Tuples.Select(t => t.ShaderState)
            .Distinct<OriginalNativeWorldShaderTableState>(ReferenceEqualityComparer.Instance))
            models.RequireCurrentShaderState(state);
    }

    private sealed class CurrentOrder : IOriginalStaticWorldQueueOrderSource
    {
        private readonly OriginalNativeMapStaticWorldOrderOwner owner;
        private readonly OriginalNativeMapStaticQueuePlan plan;
        private readonly OriginalStaticWorldCurrentTuplePublication publication;
        private readonly Dictionary<int, IReadOnlyList<string>> orders = [];
        internal CurrentOrder(OriginalNativeMapStaticWorldOrderOwner owner, OriginalNativeMapStaticQueuePlan plan,
            OriginalStaticWorldCurrentTuplePublication publication)
            => (this.owner, this.plan, this.publication) = (owner, plan, publication);
        public string SourceEvidence => owner.SourceEvidence;
        public void RequireCurrent(OriginalNativeMapStaticQueuePlan actualPlan, int sourceNode)
        {
            if (!ReferenceEquals(actualPlan, plan) || sourceNode is not (54 or 55))
                throw new InvalidDataException("Static radix input belongs to another current primitive publication or queue.");
            owner.RequireCurrentPublication(publication);
        }
        public IReadOnlyList<string> ReadActualOrderedCandidateIdentities(OriginalNativeMapStaticQueuePlan actualPlan, int sourceNode)
        {
            RequireCurrent(actualPlan, sourceNode);
            if (orders.TryGetValue(sourceNode, out var found)) return found;
            var result = new List<string>();
            foreach (var record in plan.Admission.CurrentWorldQueue!.Records)
            {
                var sorted = owner.ReadSortedFirstCounter(publication, plan.Admission, record.RecordOwnerIdentity);
                var candidates = plan.SourceCandidates.Where(c => c.RecordOwnerIdentity == record.RecordOwnerIdentity &&
                    c.QueueNodeIndex == sourceNode).ToDictionary(c => (c.MaterialState.Context.Instance.InstanceKey, c.MaterialState.Context.DrawIndex));
                foreach (var tuple in sorted)
                    if (candidates.TryGetValue((tuple.InstanceKey, tuple.SourceDrawIndex), out var candidate))
                        result.Add(candidate.CandidateIdentity);
            }
            orders.Add(sourceNode, found = result.AsReadOnly()); return found;
        }
    }

    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> bytes)
    {
        return new(BinaryPrimitives.ReadSingleLittleEndian(bytes), BinaryPrimitives.ReadSingleLittleEndian(bytes[4..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[8..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[12..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[16..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[20..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[24..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[28..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[32..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[36..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[40..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[44..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[48..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[52..]),
            BinaryPrimitives.ReadSingleLittleEndian(bytes[56..]), BinaryPrimitives.ReadSingleLittleEndian(bytes[60..]));
    }
}
