using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

public sealed record StaticWorldCurrentPrimitiveTuple(string InstanceKey, int SourceDrawIndex,
    MapStaticModelPreparedState MaterialState, NativeWorldShaderTableState ShaderState,
    WorldPrimitiveModelDistance SourceDistance, MapModelNativeOrientedBounds CurrentBounds,
    float CurrentDistance, ulong FirstCounterKey);

/// <summary>Actual current CPU tuple publication before GPU work. The list
/// retains the accepted population's publisher order; no sort is inferred
/// from this list.</summary>
public sealed class StaticWorldCurrentTuplePublication
{
    internal StaticWorldCurrentTuplePublication(MapStaticModelCurrentOwnedFrame population,
        MapStaticModelPreparedFrame prepared, WorldPrimitiveDepthRange range, Vector4 plane,
        IReadOnlyList<StaticWorldCurrentPrimitiveTuple> tuples)
        => (Population, Prepared, ViewRange, ViewZPlane, Tuples) = (population, prepared, range, plane, tuples);
    public MapStaticModelCurrentOwnedFrame Population { get; }
    public MapStaticModelPreparedFrame Prepared { get; }
    public WorldPrimitiveDepthRange ViewRange { get; }
    public Vector4 ViewZPlane { get; }
    public IReadOnlyList<StaticWorldCurrentPrimitiveTuple> Tuples { get; }
    internal Dictionary<(MapSourceCreatedStaticPrimitiveAdmissionPlan Admission, string RecordOwner),
        IReadOnlyList<StaticWorldCurrentPrimitiveTuple>> SortedFirstBanks { get; } = [];
}

/// <summary>The current first-bank publication and loaded ViewParam radix
/// consumer. Ordinary alpha draw selection is applied after this bank sort;
/// it does not select the Alpha sort descriptor.</summary>
public sealed class NativeMapStaticWorldOrderOwner
{
    private readonly SceneViewNativeMapModelSourceOwner models;
    private readonly WorldPrimitiveOrderSource viewOrder;
    private readonly Func<MapStaticModelPreparedState, NativeWorldShaderTableState> readActualFinalTable;
    private readonly Dictionary<MapStaticModelAsset, WorldPrimitiveModelDistance> distances
        = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, (MapStaticModelAsset Asset, MapModelNativeFreshVisibilityPrefix Prefix)> freshBounds
        = new(StringComparer.Ordinal);
    private StaticWorldCurrentTuplePublication? current;
    private FacilitySceneModelFrame? facilityFrame;
    private HashSet<string> facilityBounds = new(StringComparer.Ordinal);
    public NativeMapStaticWorldOrderOwner(SceneViewNativeMapModelSourceOwner actualModels,
        WorldPrimitiveOrderSource actualViewParameter,
        Func<MapStaticModelPreparedState, NativeWorldShaderTableState> readActualFinalShaderTable)
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

    public StaticWorldCurrentTuplePublication PrepareCurrentTuples(
        MapStaticModelCurrentOwnedFrame actualPopulation,
        MapStaticModelPreparedFrame actualPrepared)
    {
        ArgumentNullException.ThrowIfNull(actualPopulation); ArgumentNullException.ThrowIfNull(actualPrepared);
        if (actualPopulation.FrameNumber <= 0 || !actualPopulation.Population.Complete || !actualPrepared.Complete ||
            !ReferenceEquals(actualPopulation.Visibility, actualPrepared.Visibility))
            throw new InvalidDataException("Static primitive order needs the exact complete current CPU population and packet preparation.");
        if (!ReferenceEquals(facilityFrame, actualPopulation.FacilitySceneModels))
        {
            var retained = actualPopulation.FacilitySceneModels?.Rows.Select(row => row.Identity)
                .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);
            foreach (string identity in facilityBounds)
                if (!retained.Contains(identity)) freshBounds.Remove(identity);
            facilityBounds = retained; facilityFrame = actualPopulation.FacilitySceneModels;
        }
        var sourceOrder = actualPopulation.Visibility.Visible.SelectMany(instance =>
            instance.Asset.Draws.Select(draw => (instance.InstanceKey, Draw: draw.Index))).ToArray();
        if (!sourceOrder.SequenceEqual(actualPrepared.MaterialStates.Select(state =>
            (state.Context.Instance.InstanceKey, state.Context.DrawIndex))))
            throw new InvalidDataException("Prepared static primitive order differs from its actual current CPU publisher.");
        var prefix = actualPopulation.MainCamera.Camera.Prefix.Span;
        var range = WorldPrimitiveDistanceKey.PrepareView(
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x140..]),
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x144..]));
        var plane = WorldPrimitiveDistanceKey.PrepareViewZPlane(
            ReadMatrix(prefix[0x100..]), BinaryPrimitives.ReadUInt32LittleEndian(prefix[0x158..]));
        var rows = new List<StaticWorldCurrentPrimitiveTuple>(sourceOrder.Length);
        var bounds = new Dictionary<string, MapModelNativeOrientedBounds>(StringComparer.Ordinal);
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
                    loaded = (instance.Asset, MapModelNativeVisibilitySources.PrepareFreshSingleNode(instance,
                        new(instance.CurrentSourceIdentity, false, true, true)));
                    freshBounds[instance.CurrentSourceIdentity] = loaded;
                }
                var fresh = loaded.Prefix;
                if (!fresh.UseModelBounds)
                    throw new NotSupportedException("Static order View-extrema bound needs its actual current source producer.");
                bound = MapModelNativeBoundsSources.FromSourceWorld(instance, fresh.BoundsPublication);
                bounds.Add(instance.InstanceKey, bound);
            }
            if (!distances.TryGetValue(instance.Asset, out var declaration))
            {
                declaration = WorldPrimitiveDistanceKey.ReadModelDeclaration(instance.Asset.TextureResources.ModelObject);
                distances.Add(instance.Asset, declaration);
            }
            float distance = declaration.ForceRegisterNormalZDistance1A0;
            if (distance < 0f) distance = WorldPrimitiveDistanceKey.ReadOrientedBoundsDistance(bound, plane);
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
                distance, WorldPrimitiveDistanceKey.PrepareFirstCounter(range, distance, declaration.RegisterNormalZDistanceBias1A4)));
        }
        return current = new(actualPopulation, actualPrepared, range, plane, rows.AsReadOnly());
    }

    public IStaticWorldQueueOrderSource PrepareCurrentOrder(NativeMapStaticQueuePlan actualPlan)
    {
        ArgumentNullException.ThrowIfNull(actualPlan);
        var publication = PrepareCurrentTuples(actualPlan.Admission.CpuPopulation, actualPlan.Prepared);
        return new CurrentOrder(this, actualPlan, publication);
    }

    /// <summary>Sort one actual owner's first bank. CPU publication/counts
    /// and record membership remain available even without a GPU target.</summary>
    public IReadOnlyList<StaticWorldCurrentPrimitiveTuple> ReadSortedFirstCounter(
        StaticWorldCurrentTuplePublication publication,
        MapSourceCreatedStaticPrimitiveAdmissionPlan actualAdmission, string currentRecordOwnerIdentity)
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
        var result = Array.AsReadOnly(viewOrder.Sort(WorldPrimitiveSortFamily.Opaque, admitted,
            (tuple, field) => field switch { 3 => tuple.ShaderState.OrderedAllocationIdentity,
                14 => tuple.FirstCounterKey, _ => throw new NotSupportedException("This current static tuple producer has no source for selected sort field " + field) }));
        publication.SortedFirstBanks.Add(cacheKey, result);
        return result;
    }

    public StaticWorldCurrentTuplePublication RequireCurrentTuples()
    {
        var publication=current??throw new InvalidOperationException("No current static first-bank tuple publication.");
        RequireCurrentPublication(publication);return publication;
    }

    private void RequireCurrentPublication(StaticWorldCurrentTuplePublication publication)
    {
        if (!ReferenceEquals(current, publication))
            throw new InvalidDataException("Static tuple publication has been superseded.");
        foreach (var state in publication.Tuples.Select(t => t.ShaderState)
            .Distinct<NativeWorldShaderTableState>(ReferenceEqualityComparer.Instance))
            models.RequireCurrentShaderState(state);
    }

    private sealed class CurrentOrder : IStaticWorldQueueOrderSource
    {
        private readonly NativeMapStaticWorldOrderOwner owner;
        private readonly NativeMapStaticQueuePlan plan;
        private readonly StaticWorldCurrentTuplePublication publication;
        private readonly Dictionary<int, IReadOnlyList<string>> orders = [];
        internal CurrentOrder(NativeMapStaticWorldOrderOwner owner, NativeMapStaticQueuePlan plan,
            StaticWorldCurrentTuplePublication publication)
            => (this.owner, this.plan, this.publication) = (owner, plan, publication);
        public string SourceEvidence => owner.SourceEvidence;
        public void RequireCurrent(NativeMapStaticQueuePlan actualPlan, int sourceNode)
        {
            if (!ReferenceEquals(actualPlan, plan) || sourceNode is not (54 or 55))
                throw new InvalidDataException("Static radix input belongs to another current primitive publication or queue.");
            owner.RequireCurrentPublication(publication);
        }
        public IReadOnlyList<string> ReadActualOrderedCandidateIdentities(NativeMapStaticQueuePlan actualPlan, int sourceNode)
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
