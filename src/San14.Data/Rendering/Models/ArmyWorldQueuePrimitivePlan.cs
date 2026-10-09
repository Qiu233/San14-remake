namespace San14.Data;

/// <summary>Load-boundary declarations for the shader actually selected for
/// this canonical CPU primitive. Cloth replacement uses its actual variation
/// owner, while geometry/count membership retains the original draw key.</summary>
public sealed record ArmyWorldQueuePrimitiveSource(string PacketKey,
    ModelOrdinaryQueueSelectionSource Opaque49,
    ModelOrdinaryQueueSelectionSource Alpha50,
    ModelGroupRasterSource? SourceGroupRaster);

public sealed record ArmyWorldQueuePrimitiveCandidate(int QueueNodeIndex,
    string RecordOwnerIdentity, uint RecordMask78,
    ArmyWorldPrimitiveAdmission Primitive,
    ModelOrdinaryQueueSelectionSource SelectedSource,
    ModelQueueDepthState SourceQueueDepth,
    ModelGroupRasterSource? SourceGroupRaster)
{
    // Actual submissions must retain this record identity independently of
    // mask. This is a candidate identity, never a completed draw receipt.
    public string CandidateIdentity => RecordOwnerIdentity + "/queue" + QueueNodeIndex + "/" + Primitive.PacketKey;
}

/// <summary>Complete source candidates after World CPU publication, in graph
/// queue49 then50 order. Shader alpha filtering never changes the original
/// World first-counter counts. Actual ordered geometry submission to the
/// same source E13 depth/stencil attachment remains a later consumer.</summary>
public sealed class ArmyWorldQueuePrimitivePlan
{
    private ArmyWorldQueuePrimitivePlan(ArmyWorldPrimitivePublication publication,
        IReadOnlyList<ArmyWorldQueuePrimitiveCandidate> candidates)
        => (Publication, SourceCandidates) = (publication, candidates);
    public ArmyWorldPrimitivePublication Publication { get; }
    public IReadOnlyList<ArmyWorldQueuePrimitiveCandidate> SourceCandidates { get; }

    public static ArmyWorldQueuePrimitivePlan PrepareFreshSourceCandidates(
        ArmyWorldPrimitivePublication actualPublication,
        IReadOnlyList<ArmyWorldQueuePrimitiveSource> completeSelectedPrimitiveSources,
        uint actualContextFlags3E4)
    {
        ArgumentNullException.ThrowIfNull(actualPublication);
        ArgumentNullException.ThrowIfNull(completeSelectedPrimitiveSources);
        var primitives = actualPublication.Primitives;
        if (completeSelectedPrimitiveSources.Any(s => s is null || s.Opaque49 is null || s.Alpha50 is null) ||
            completeSelectedPrimitiveSources.Select(s => s.PacketKey).Distinct(StringComparer.Ordinal).Count() != completeSelectedPrimitiveSources.Count ||
            !completeSelectedPrimitiveSources.Select(s => s.PacketKey).Order(StringComparer.Ordinal)
                .SequenceEqual(primitives.Select(p => p.PacketKey).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("World Army source queue plan requires every canonical primitive, including invisible draws.");
        var sources = completeSelectedPrimitiveSources.ToDictionary(s => s.PacketKey, StringComparer.Ordinal);
        foreach (var primitive in primitives)
        {
            var pair = sources[primitive.PacketKey];
            var left = pair.Opaque49.SelectedRenderer;
            var right = pair.Alpha50.SelectedRenderer;
            if (pair.Opaque49.Queue.Node.Index != 49 || pair.Alpha50.Queue.Node.Index != 50 ||
                !ReferenceEquals(pair.Opaque49.Queue.Graph, pair.Alpha50.Queue.Graph) ||
                left.OwnerObjectId != right.OwnerObjectId || left.ProviderObjectId != right.ProviderObjectId ||
                left.ShaderSha256 != right.ShaderSha256 ||
                pair.Opaque49.NativeFreshAux4Bit8 != pair.Alpha50.NativeFreshAux4Bit8 ||
                pair.Opaque49.SourceQueueSelectsShader == pair.Alpha50.SourceQueueSelectsShader)
                throw new InvalidDataException("Army source queue pair must classify the same selected renderer into exactly one original alpha branch.");
            if (pair.SourceGroupRaster is { } raster)
            {
                var actor = actualPublication.Actors.Single(a => a.ActorIdentity == primitive.ActorIdentity);
                if (raster.Draw.Draw.Index != primitive.SourceDrawIndex ||
                    raster.Model.Model.Source.ResourceUri != actor.Source.Model.ResourceUri ||
                    raster.Model.Model.Source.DecodedSha256 != actor.Source.Model.DecodedSha256)
                    throw new InvalidDataException("Army source group raster belongs to another canonical geometry draw.");
            }
        }
        var result = new List<ArmyWorldQueuePrimitiveCandidate>();
        foreach (int node in new[] { 49, 50 })
        foreach (var record in actualPublication.Counts.Queue.Records)
        foreach (var primitive in primitives)
        {
            if (!primitive.AdmittedRecordOwners.Contains(record.RecordOwnerIdentity, StringComparer.Ordinal)) continue;
            var source = sources[primitive.PacketKey];
            var selection = node == 49 ? source.Opaque49 : source.Alpha50;
            if (selection.SourceQueueSelectsShader)
                result.Add(new(node, record.RecordOwnerIdentity, record.Mask78, primitive, selection,
                    selection.PrepareSourceQueueDepth(actualContextFlags3E4), source.SourceGroupRaster));
        }
        return new(actualPublication, result.AsReadOnly());
    }
}
