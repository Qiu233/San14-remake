using System.Buffers.Binary;
using System.Numerics;
using San14.Data;

public sealed record OriginalArmyWorldCurrentPrimitiveTuple(OriginalArmyWorldPrimitiveAdmission Primitive,
    OriginalArmyWorldPrimitiveTableRow ShaderPublication, OriginalWorldPrimitiveModelDistance SourceDistance,
    OriginalMapModelNativeOrientedBounds CurrentBounds, float CurrentDistance, ulong FirstCounterKey);

/// <summary>Actual owned BB85A0 first tuples in CPU publisher insertion order,
/// before radix sorting. Invisible primitives remain in the complete inventory
/// and enter no owner's counter. No GPU completion is implied.</summary>
public sealed class OriginalArmyWorldCurrentTuplePublication
{
    internal OriginalArmyWorldCurrentTuplePublication(OriginalArmyWorldPrimitivePublication population,
        OriginalMainMapCameraFrame camera, OriginalArmyWorldPrimitiveTablePublication tables,
        OriginalWorldPrimitiveDepthRange range, Vector4 plane,
        IReadOnlyList<OriginalArmyWorldCurrentPrimitiveTuple> tuples, Action current)
        => (Population, Camera, Tables, ViewRange, ViewZPlane, Tuples, RequireActualCurrentWorld) =
            (population, camera, tables, range, plane, tuples, current);
    public OriginalArmyWorldPrimitivePublication Population { get; }
    public OriginalMainMapCameraFrame Camera { get; }
    public OriginalArmyWorldPrimitiveTablePublication Tables { get; }
    public OriginalWorldPrimitiveDepthRange ViewRange { get; }
    public Vector4 ViewZPlane { get; }
    public IReadOnlyList<OriginalArmyWorldCurrentPrimitiveTuple> Tuples { get; }
    internal Action RequireActualCurrentWorld { get; }
    internal Dictionary<string, IReadOnlyList<OriginalArmyWorldCurrentPrimitiveTuple>> SortedFirstBanks { get; } = new(StringComparer.Ordinal);
}

/// <summary>Loaded ViewParam sorting applied to actual owned first-bank tuples.
/// Nodes49 and50 both select bank0 and Opaque14→3. Aux4.bit8 filtering happens
/// after the complete record bank is sorted, independently of counts.</summary>
public sealed class OriginalNativeMapArmyWorldOrderOwner
{
    private readonly OriginalWorldPrimitiveOrderSource viewOrder;
    private readonly Dictionary<OriginalArmyPrimitivePublicationSource, OriginalWorldPrimitiveModelDistance> distances =
        new(ReferenceEqualityComparer.Instance);
    private OriginalArmyWorldCurrentTuplePublication? current;
    public OriginalNativeMapArmyWorldOrderOwner(OriginalWorldPrimitiveOrderSource actualSelectedViewParameter)
        => viewOrder = actualSelectedViewParameter ?? throw new ArgumentNullException(nameof(actualSelectedViewParameter));
    public string SourceEvidence => viewOrder.SourceIdentity +
        ";actual-owned-Primitive18-full-type16-table;actual-Model1A0/1A4/OBB;Camera100/140/144/158;" +
        "BB85A0/BB8680-first-bank-owned-insertion-order;EA6470/EA6C5D/D58B50-bank0;" +
        "D4B7E0-stable-radix-Opaque14-then3;49/50-Aux4.bit8-after-complete-bank-sort";

    public OriginalArmyWorldCurrentTuplePublication PrepareCurrentTuples(OriginalArmyWorldPrimitivePublication actualPopulation,
        OriginalMainMapCameraFrame actualCamera, OriginalArmyWorldPrimitiveTablePublication actualTables,
        Action requireActualCurrentWorld)
    {
        ArgumentNullException.ThrowIfNull(actualPopulation); ArgumentNullException.ThrowIfNull(actualCamera);
        ArgumentNullException.ThrowIfNull(actualTables); ArgumentNullException.ThrowIfNull(requireActualCurrentWorld);
        requireActualCurrentWorld(); actualTables.RequireCurrent();
        if (!ReferenceEquals(actualTables.Population, actualPopulation) || actualPopulation.ActualVisibilityCamera is not { } visibilityCamera ||
            actualCamera.Camera.FrameNumber != actualPopulation.Counts.Queue.FrameNumber ||
            visibilityCamera.SourceWorldToView != actualCamera.Camera.SourceWorldToView ||
            visibilityCamera.SourceWorldToProjection != actualCamera.Camera.SourceWorldToProjection)
            throw new InvalidDataException("Army order needs the exact same current Camera used by its complete visibility/World publication.");
        var prefix = actualCamera.Camera.Prefix.Span;
        var range = OriginalWorldPrimitiveDistanceKey.PrepareView(BinaryPrimitives.ReadSingleLittleEndian(prefix[0x140..]),
            BinaryPrimitives.ReadSingleLittleEndian(prefix[0x144..]));
        var plane = OriginalWorldPrimitiveDistanceKey.PrepareViewZPlane(ReadMatrix(prefix[0x100..]),
            BinaryPrimitives.ReadUInt32LittleEndian(prefix[0x158..]));
        var actors = actualPopulation.Actors.ToDictionary(a => a.ActorIdentity, StringComparer.Ordinal);
        var rows = new List<OriginalArmyWorldCurrentPrimitiveTuple>(actualPopulation.Primitives.Count);
        foreach (var primitive in actualPopulation.Primitives)
        {
            var actor = actors[primitive.ActorIdentity];
            if (!distances.TryGetValue(actor.Source, out var declaration))
            {
                var model = actor.Source.Catalog.Definitions.Single(d => d.ModelObject.ObjectId == actor.Source.Category.ModelObjectId).ModelObject;
                distances.Add(actor.Source, declaration = OriginalWorldPrimitiveDistanceKey.ReadModelDeclaration(model));
            }
            float distance = declaration.ForceRegisterNormalZDistance1A0;
            if (distance < 0f) distance = OriginalWorldPrimitiveDistanceKey.ReadOrientedBoundsDistance(actor.Visibility.Bounds, plane);
            rows.Add(new(primitive, actualTables.ReadActualFinalTable(primitive), declaration, actor.Visibility.Bounds, distance,
                OriginalWorldPrimitiveDistanceKey.PrepareFirstCounter(range, distance, declaration.RegisterNormalZDistanceBias1A4)));
        }
        return current = new(actualPopulation, actualCamera, actualTables, range, plane, rows.AsReadOnly(), requireActualCurrentWorld);
    }

    public IReadOnlyList<OriginalArmyWorldCurrentPrimitiveTuple> ReadSortedFirstCounter(
        OriginalArmyWorldCurrentTuplePublication publication, string actualRecordOwnerIdentity)
    {
        RequireCurrent(publication);
        if (!publication.Population.Counts.Queue.Records.Any(r => r.RecordOwnerIdentity == actualRecordOwnerIdentity))
            throw new InvalidDataException("Army first-bank order needs an actual owning World record, not a mask alias.");
        if (publication.SortedFirstBanks.TryGetValue(actualRecordOwnerIdentity, out var found)) return found;
        var admitted = publication.Tuples.Where(t => t.Primitive.AdmittedRecordOwners.Contains(actualRecordOwnerIdentity, StringComparer.Ordinal)).ToArray();
        if (admitted.Length != publication.Population.CompleteCountsByRecordOwner[actualRecordOwnerIdentity])
            throw new InvalidDataException("Army first-bank tuples differ from this record owner's complete CPU count.");
        var result = Array.AsReadOnly(viewOrder.Sort(OriginalWorldPrimitiveSortFamily.Opaque, admitted,
            (tuple, field) => field switch { 3 => tuple.ShaderPublication.FinalTable.OrderedAllocationIdentity,
                14 => tuple.FirstCounterKey, _ => throw new NotSupportedException("Army current key producer has no selected source field " + field) }));
        publication.SortedFirstBanks.Add(actualRecordOwnerIdentity, result); return result;
    }

    public IOriginalArmyWorldQueueOrderSource PrepareCurrentOrder(OriginalArmyWorldQueuePrimitivePlan actualPlan,
        OriginalArmyWorldCurrentTuplePublication actualTuples)
    {
        ArgumentNullException.ThrowIfNull(actualPlan); RequireCurrent(actualTuples);
        if (!ReferenceEquals(actualPlan.Publication, actualTuples.Population))
            throw new InvalidDataException("Army candidate filter and first-bank tuple publication differ.");
        foreach (var candidate in actualPlan.SourceCandidates)
        {
            var table = actualTuples.Tables.ReadActualFinalTable(candidate.Primitive).FinalTable;
            var selected = actualTuples.Tables.ReadSelectedInterface(table, 18);
            var expected = candidate.SelectedSource.SelectedRenderer;
            if (selected.Owner.Object.ObjectId != expected.OwnerObjectId || selected.Provider is not { } provider ||
                provider.Object.ObjectId != expected.ProviderObjectId || provider.Objects.Source.ResourceUri != table.TableOwner.Objects.Source.ResourceUri ||
                provider.Objects.Source.DecodedSha256 != table.TableOwner.Objects.Source.DecodedSha256)
                throw new InvalidDataException("Army final Primitive18 table does not select the actual queue packet renderer; reached material override must be prepared first.");
        }
        return new CurrentOrder(this, actualPlan, actualTuples);
    }

    internal void RequireCurrent(OriginalArmyWorldCurrentTuplePublication publication)
    {
        if (!ReferenceEquals(current, publication))
            throw new InvalidDataException("Army current first-bank publication was superseded.");
        publication.RequireActualCurrentWorld(); publication.Tables.RequireCurrent();
    }

    private sealed class CurrentOrder : IOriginalArmyWorldQueueOrderSource
    {
        private readonly OriginalNativeMapArmyWorldOrderOwner owner;
        private readonly OriginalArmyWorldQueuePrimitivePlan plan;
        public OriginalArmyWorldCurrentTuplePublication Publication { get; }
        public OriginalArmyWorldCurrentTuplePublication ActualTuplePublication => Publication;
        private readonly Dictionary<int, IReadOnlyList<string>> orders = [];
        internal CurrentOrder(OriginalNativeMapArmyWorldOrderOwner owner, OriginalArmyWorldQueuePrimitivePlan plan,
            OriginalArmyWorldCurrentTuplePublication publication) => (this.owner, this.plan, Publication) = (owner, plan, publication);
        public string SourceEvidence => owner.SourceEvidence;
        public void RequireCurrent(OriginalArmyWorldQueuePrimitivePlan actualPlan, int sourceNode)
        {
            if (!ReferenceEquals(actualPlan, plan) || sourceNode is not (49 or 50))
                throw new InvalidDataException("Army source ordering belongs to another candidate publication or queue.");
            owner.RequireCurrent(Publication);
        }
        public IReadOnlyList<string> ReadActualOrderedCandidateIdentities(OriginalArmyWorldQueuePrimitivePlan actualPlan, int sourceNode)
        {
            RequireCurrent(actualPlan, sourceNode);
            if (orders.TryGetValue(sourceNode, out var found)) return found;
            var result = new List<string>();
            foreach (var record in plan.Publication.Counts.Queue.Records)
            {
                var candidates = plan.SourceCandidates.Where(c => c.RecordOwnerIdentity == record.RecordOwnerIdentity && c.QueueNodeIndex == sourceNode)
                    .ToDictionary(c => c.Primitive.PacketKey, StringComparer.Ordinal);
                foreach (var tuple in owner.ReadSortedFirstCounter(Publication, record.RecordOwnerIdentity))
                    if (candidates.TryGetValue(tuple.Primitive.PacketKey, out var candidate)) result.Add(candidate.CandidateIdentity);
            }
            orders.Add(sourceNode, found = result.AsReadOnly()); return found;
        }
    }

    private static Matrix4x4 ReadMatrix(ReadOnlySpan<byte> b) => new(
        BinaryPrimitives.ReadSingleLittleEndian(b), BinaryPrimitives.ReadSingleLittleEndian(b[4..]), BinaryPrimitives.ReadSingleLittleEndian(b[8..]), BinaryPrimitives.ReadSingleLittleEndian(b[12..]),
        BinaryPrimitives.ReadSingleLittleEndian(b[16..]), BinaryPrimitives.ReadSingleLittleEndian(b[20..]), BinaryPrimitives.ReadSingleLittleEndian(b[24..]), BinaryPrimitives.ReadSingleLittleEndian(b[28..]),
        BinaryPrimitives.ReadSingleLittleEndian(b[32..]), BinaryPrimitives.ReadSingleLittleEndian(b[36..]), BinaryPrimitives.ReadSingleLittleEndian(b[40..]), BinaryPrimitives.ReadSingleLittleEndian(b[44..]),
        BinaryPrimitives.ReadSingleLittleEndian(b[48..]), BinaryPrimitives.ReadSingleLittleEndian(b[52..]), BinaryPrimitives.ReadSingleLittleEndian(b[56..]), BinaryPrimitives.ReadSingleLittleEndian(b[60..]));
}
