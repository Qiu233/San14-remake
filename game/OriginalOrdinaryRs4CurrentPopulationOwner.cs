using San14.Data;

public sealed record OriginalOrdinaryRs4CurrentPrimitive(string Family, string PacketIdentity,
    OriginalNativeWorldShaderTableState ActualFinalTable, ulong ActualFirstCounterKey,
    IReadOnlyList<string> AdmittedRecordOwners,
    IReadOnlyDictionary<int, OriginalOrdinaryRs4FinalInterface> QueueInterfaces);

/// <summary>Complete current CPU inputs to earlier RS4 queues. It retains
/// source-null primitives and record membership. Separate family publication
/// order is recorded, never concatenated into an invented common radix bank.</summary>
public sealed class OriginalOrdinaryRs4CurrentPopulation
{
    internal OriginalOrdinaryRs4CurrentPopulation(long frameNumber, string viewIdentity,
        IReadOnlyList<OriginalOrdinaryRs4CurrentPrimitive> statics,
        IReadOnlyList<OriginalOrdinaryRs4CurrentPrimitive> armies, Action current)
        => (FrameNumber, ViewIdentity, StaticPrimitives, ArmyPrimitives, requireCurrent) =
            (frameNumber, viewIdentity, statics, armies, current);
    private readonly Action requireCurrent;
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public IReadOnlyList<OriginalOrdinaryRs4CurrentPrimitive> StaticPrimitives { get; }
    public IReadOnlyList<OriginalOrdinaryRs4CurrentPrimitive> ArmyPrimitives { get; }
    public void RequireCurrent() => requireCurrent();
    public IReadOnlyList<OriginalOrdinaryRs4CurrentPrimitive> ReadCurrentRecordInputs(string recordOwnerIdentity)
    {
        RequireCurrent();
        return Array.AsReadOnly(StaticPrimitives.Concat(ArmyPrimitives)
            .Where(p => p.AdmittedRecordOwners.Contains(recordOwnerIdentity, StringComparer.Ordinal)).ToArray());
    }
    public bool RequiresRenderer(int queueNode)
    {
        RequireCurrent();
        return StaticPrimitives.Concat(ArmyPrimitives).Any(p => p.AdmittedRecordOwners.Count != 0 &&
            p.QueueInterfaces[queueNode].Boundary == OriginalOrdinaryRs4InterfaceBoundary.RequiresRenderer);
    }
}

/// <summary>Same current View/record publication and the real final typed16
/// tables used by ordinary World. These are complete tuples, not the state18
/// GPU candidates. This owner classifies 44/45/46 interfaces without executing
/// their dispatcher, declaring source pixels, or clearing RS4.</summary>
public sealed class OriginalOrdinaryRs4CurrentPopulationOwner
{
    private readonly LoadedOriginalOrdinaryRs4Source source;
    private OriginalOrdinaryRs4CurrentPopulation? current;
    public OriginalOrdinaryRs4CurrentPopulationOwner(LoadedOriginalOrdinaryRs4Source actualLoadedSource)
        => source = actualLoadedSource ?? throw new ArgumentNullException(nameof(actualLoadedSource));

    public OriginalOrdinaryRs4CurrentPopulation CaptureCurrent(
        OriginalNativeMapStaticWorldOrderOwner staticOrder,
        OriginalMapSourceCreatedStaticPrimitiveAdmissionPlan actualStaticAdmission,
        OriginalArmyWorldCurrentTuplePublication actualArmyTuples)
    {
        ArgumentNullException.ThrowIfNull(staticOrder); ArgumentNullException.ThrowIfNull(actualStaticAdmission);
        ArgumentNullException.ThrowIfNull(actualArmyTuples);
        var statics = staticOrder.RequireCurrentTuples();
        var queue = actualStaticAdmission.CurrentWorldQueue ??
            throw new InvalidDataException("RS4 needs the current record-owner keyed World View publication.");
        actualArmyTuples.RequireActualCurrentWorld(); actualArmyTuples.Tables.RequireCurrent();
        if (!ReferenceEquals(actualStaticAdmission.CpuPopulation, statics.Population) ||
            !ReferenceEquals(actualArmyTuples.Population.Counts.Queue, queue))
            throw new InvalidDataException("RS4 static/Army tuples must belong to the same actual current World View.");
        OriginalOrdinaryRs4CurrentPrimitive Build(string family, string packet,
            OriginalNativeWorldShaderTableState table, ulong key, IReadOnlyList<string> members)
            => new(family, packet, table, key, members,
                new[] {44,45,46}.ToDictionary(n => n, n => source.ReadFinalInterface(n, table.TableOwner)));
        var staticRows = statics.Tuples.Select(t => Build("Static", t.InstanceKey + "/draw:" + t.SourceDrawIndex,
            t.ShaderState, t.FirstCounterKey,
            actualStaticAdmission.Require(t.InstanceKey, t.SourceDrawIndex).AdmittedWorldKind1RecordOwnerIdentities)).ToArray();
        var armyRows = actualArmyTuples.Tuples.Select(t => Build("Army", t.Primitive.PacketKey,
            t.ShaderPublication.FinalTable, t.FirstCounterKey, t.Primitive.AdmittedRecordOwners)).ToArray();
        OriginalOrdinaryRs4CurrentPopulation? result = null;
        result = new(actualStaticAdmission.FrameNumber, actualStaticAdmission.ViewIdentity,
            Array.AsReadOnly(staticRows), Array.AsReadOnly(armyRows), () =>
            {
                if (!ReferenceEquals(current, result) || !ReferenceEquals(staticOrder.RequireCurrentTuples(), statics))
                    throw new InvalidDataException("RS4 current tuple publication was superseded.");
                actualArmyTuples.RequireActualCurrentWorld(); actualArmyTuples.Tables.RequireCurrent();
            });
        return current = result;
    }
}
