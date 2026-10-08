using San14.Data;

/// <summary>Army's actual node48 six-MRT49/50 aggregate. This publication is
/// distinct from the legacy two five-MRT family-layer receipts. It retains
/// complete CPU inventory and actual admitted first-bank membership alongside
/// copied successful draw acknowledgements; it is not a GPU fence or node59
/// sampled-depth publication.</summary>
public sealed class OriginalArmyWorldQueuesCompletedPublication
{
    private readonly OriginalArmyWorldQueuesCompletedFrame completed;
    internal OriginalArmyWorldQueuesCompletedPublication(OriginalArmyWorldQueuesCompletedFrame actualCompleted,
        OriginalArmyWorldCurrentTuplePublication tuples, OriginalArmy48TargetBinding target)
    {
        completed = actualCompleted; ActualTuples = tuples; BoundTarget48 = target;
        target.RequireRetainedTargetBorrow();
        var population = actualCompleted.Prepared.Plan.Publication;
        if (!ReferenceEquals(tuples.Population, population) || !ReferenceEquals(target.Frame, actualCompleted.Opaque49.Target) ||
            !ReferenceEquals(target.Frame, actualCompleted.Alpha50.Target))
            throw new InvalidDataException("Army completed publication does not join the exact current six-MRT target and tuple source.");
        var submissions = actualCompleted.Opaque49.Primitives.Concat(actualCompleted.Alpha50.Primitives).ToArray();
        var expected = actualCompleted.Prepared.Plan.SourceCandidates.Select(c => c.CandidateIdentity).ToHashSet(StringComparer.Ordinal);
        if (submissions.Length != expected.Count || !expected.SetEquals(submissions.Select(s => s.CandidateIdentity)))
            throw new InvalidDataException("Army completed publication omits or duplicates an actual record-owner candidate submission.");
        SubmittedPrimitives = Array.AsReadOnly(submissions);
        var actors = population.Actors.ToDictionary(a => a.ActorIdentity, StringComparer.Ordinal);
        var admitted = tuples.Tuples.Where(t => t.Primitive.AdmittedRecordOwners.Count != 0).ToArray();
        if (!admitted.Select(t => t.Primitive.PacketKey).ToHashSet(StringComparer.Ordinal)
            .SetEquals(submissions.Select(s => s.CanonicalPacketKey)))
            throw new InvalidDataException("Army completed draws differ from actual admitted canonical first-bank membership.");
        CurrentMode16Candidates = Array.AsReadOnly(admitted.Select(tuple =>
        {
            var primitive = tuple.Primitive; var actor = actors[primitive.ActorIdentity];
            string family = primitive.Family == "ArmyOrdinary" ? "ArmyOrdinary" : tuple.ShaderPublication.FinalTable.TableOwner.Object.ObjectId switch
            { 0x739D10B8 => "ArmyFlagBase", 0x2F9960EF => "ArmyFlagCloth", _ => throw new NotSupportedException("Army mode16 family needs the reached final material table source.") };
            return new OriginalDeferredSourceMode16Candidate(family, primitive.ActorIdentity, primitive.SourceDrawIndex,
                actor.Source.Model.ResourceUri, actor.Source.Model.DecodedSha256, tuple.ShaderPublication.FinalTable.TableOwner,
                population.Counts.PublicationIdentity + ";actual-first-bank-member;" + tuple.ShaderPublication.SourceWriterEvidence);
        }).ToArray());
    }
    public OriginalArmy48TargetBinding BoundTarget48 { get; }
    public OriginalArmyWorldCurrentTuplePublication ActualTuples { get; }
    public OriginalArmyWorldPrimitivePublication CompleteCpuInventory => ActualTuples.Population;
    public IReadOnlyDictionary<string, int> CompleteCountsByRecordOwner => CompleteCpuInventory.CompleteCountsByRecordOwner;
    public IReadOnlyList<OriginalArmyWorldQueueSubmittedPrimitive> SubmittedPrimitives { get; }
    public IReadOnlyList<OriginalDeferredSourceMode16Candidate> CurrentMode16Candidates { get; }
    public void RequireAccepted()
    {
        completed.RequireAccepted(); completed.Prepared.RequireCurrent(); BoundTarget48.RequireRetainedTargetBorrow();
        ActualTuples.RequireActualCurrentWorld(); ActualTuples.Tables.RequireCurrent();
    }
}
