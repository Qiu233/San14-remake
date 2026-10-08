namespace San14.Data;

public sealed record OriginalArmyWorldCurrentPrimitiveActor(string ActorIdentity, string Family,
    OriginalArmyPrimitivePublicationSource Source, OriginalArmyCurrentWorldActorVisibility Visibility);

/// <summary>A complete canonical CPU primitive and its exact admitted World
/// record owners. Equal masks never merge independent counter owners.</summary>
public sealed record OriginalArmyWorldPrimitiveAdmission(string ActorIdentity, string Family,
    int SourceDrawIndex, string CurrentSourceIdentity, uint Exclusion78,
    IReadOnlyList<string> AdmittedRecordOwners)
{
    public string PacketKey => ActorIdentity + "/draw" + SourceDrawIndex;
}

/// <summary>Current Army source CPU publication, including invisible/excluded
/// primitives. No GPU success/statistic supplies a count.</summary>
public sealed class OriginalArmyWorldPrimitivePublication
{
    private readonly IReadOnlyList<OriginalArmyWorldCurrentPrimitiveActor> actors;
    private readonly IReadOnlyList<OriginalArmyWorldPrimitiveAdmission> primitives;
    private OriginalArmyWorldPrimitivePublication(OriginalMapWorldKind1RecordCounts counts,
        IReadOnlyList<OriginalArmyWorldCurrentPrimitiveActor> actors,
        IReadOnlyList<OriginalArmyWorldPrimitiveAdmission> primitives, OriginalDeferredCameraInputs? actualCamera)
        => (Counts, this.actors, this.primitives, ActualVisibilityCamera) = (counts, actors, primitives, actualCamera);
    public OriginalMapWorldKind1RecordCounts Counts { get; }
    public IReadOnlyList<OriginalArmyWorldCurrentPrimitiveActor> Actors => actors;
    public IReadOnlyList<OriginalArmyWorldPrimitiveAdmission> Primitives => primitives;
    public OriginalDeferredCameraInputs? ActualVisibilityCamera { get; }
    public IReadOnlyDictionary<string, int> CompleteCountsByRecordOwner => Counts.CompleteCurrentCpuCounts;

    public static OriginalArmyWorldPrimitivePublication Prepare(OriginalMapWorldKind1CurrentQueue actualQueue,
        OriginalMapFreshCommonResourceSnapshot actualCommon,
        IReadOnlyList<OriginalArmyWorldCurrentPrimitiveActor> completeCurrentActors, string currentPublicationIdentity)
        => Prepare(actualQueue, actualCommon, completeCurrentActors, currentPublicationIdentity, null);

    public static OriginalArmyWorldPrimitivePublication Prepare(OriginalMapWorldKind1CurrentQueue actualQueue,
        OriginalMapFreshCommonResourceSnapshot actualCommon,
        IReadOnlyList<OriginalArmyWorldCurrentPrimitiveActor> completeCurrentActors, string currentPublicationIdentity,
        OriginalDeferredCameraInputs? actualVisibilityCamera)
    {
        ArgumentNullException.ThrowIfNull(actualQueue); ArgumentNullException.ThrowIfNull(actualCommon);
        ArgumentNullException.ThrowIfNull(completeCurrentActors);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPublicationIdentity);
        if (actualCommon.FrameNumber != actualQueue.FrameNumber ||
            actualCommon.SelectedViewOwnerIdentity != actualQueue.Binding.ViewIdentity ||
            actualCommon.CurrentAuxiliaryExclusion404 is not uint commonExclude ||
            completeCurrentActors.Any(a => a is null || a.Source is null || a.Visibility is null ||
                string.IsNullOrWhiteSpace(a.ActorIdentity) || a.Family is not ("ArmyOrdinary" or "ArmyFlag")) ||
            completeCurrentActors.Select(a => a.ActorIdentity).Distinct(StringComparer.Ordinal).Count() != completeCurrentActors.Count)
            throw new InvalidDataException("World Army publication needs the complete current actor inventory and same-frame View/Common exclusion.");
        if (completeCurrentActors.Count != 0 && actualQueue.Records.Any(r => (r.LightDataF20 & 1) != 0))
            throw new NotSupportedException("World Army alternate F20 classification needs its reached original category producer.");
        var values = actualQueue.Records.ToDictionary(r => r.RecordOwnerIdentity, _ => 0, StringComparer.Ordinal);
        var result = new List<OriginalArmyWorldPrimitiveAdmission>();
        foreach (var actor in completeCurrentActors)
        {
            var visible = actor.Visibility;
            if (visible.FrameNumber != actualQueue.FrameNumber || visible.ViewIdentity != actualQueue.Binding.ViewIdentity ||
                visible.ActorIdentity != actor.ActorIdentity || !ReferenceEquals(visible.Source.PrimitiveSource, actor.Source) ||
                (actor.Family == "ArmyOrdinary" ? actor.Source.Category.Category is not (0 or 1) : actor.Source.Category.Category is not (2 or 3)))
                throw new InvalidDataException("Army current visibility and canonical actor source do not belong to this World inventory.");
            uint exclusion = actor.Source.Exclusion78 | commonExclude;
            string[] admitted = visible.CurrentOrdinaryView0Eligible ? actualQueue.Records
                .Where(r => (r.Mask78 & exclusion) == 0).Select(r => r.RecordOwnerIdentity).ToArray() : [];
            foreach (int draw in actor.Source.OrdinarySourceDrawIndices)
                result.Add(new(actor.ActorIdentity, actor.Family, draw, visible.Source.SourceIdentity, actor.Source.Exclusion78,
                    Array.AsReadOnly(admitted.ToArray())));
            foreach (string owner in admitted)
            {
                var record = actualQueue.Records.Single(r => r.RecordOwnerIdentity == owner);
                int next = checked(values[owner] + actor.Source.OrdinaryModelCpuCount);
                if (next > 0 && next >= record.FirstCounterCapacity) // original BB8680 JGE.
                    throw new InvalidDataException("World Army complete source primitives reach the exact first-counter capacity: " + owner);
                values[owner] = next;
            }
        }
        var counts = new OriginalMapWorldKind1RecordCounts(actualQueue, currentPublicationIdentity +
            ";TC:DB2ED0/DB7A18/C393F5/C398E2/F587F0/BB8680;actual-Army-independent-World-record-owners", values);
        return new(counts, Array.AsReadOnly(completeCurrentActors.ToArray()), result.AsReadOnly(), actualVisibilityCamera);
    }

    public OriginalMapWorldKind1RecordCounts MergeIntoActualStaticCounts(OriginalMapWorldKind1RecordCounts actualStaticCounts)
    {
        ArgumentNullException.ThrowIfNull(actualStaticCounts);
        if (!ReferenceEquals(actualStaticCounts.Queue, Counts.Queue))
            throw new InvalidDataException("Army/static counters must share the exact actual counted World queue.");
        return OriginalMapSourceCreatedWorldKind1Publication.MergeCompleteOtherCounts(actualStaticCounts,
            Counts.Queue.FrameNumber, Counts.Queue.Binding.ViewIdentity, CompleteCountsByRecordOwner, Counts.PublicationIdentity);
    }
}
