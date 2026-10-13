using San14.Data;
using San14.Data.Formats;

/// <summary>Joins the completed geometry producers to the exact current Main
/// View. Final primitive parents come from their accepted source preparation;
/// model declarations and an earlier source frame cannot replace those parents.</summary>
public static class DeferredSourceViewFamilyCurrentFactory
{
    public static DeferredSourceViewFamilyCurrentPublication Prepare(
        DeferredSourceViewFamilyOwner owner,
        MapSourceCreatedNativeSessionPreparedFrame accepted,
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army)
        => DeferredSourceViewFamilyCurrentPublication.Create(owner, accepted, population, army);
}

/// <summary>Complete ordinary membership for source RenderType0/state16.
/// Nonempty terrain is retained as actual geometry membership even though the
/// source RenderType0 dispatcher excludes its separate terrain family.</summary>
public sealed class DeferredSourceViewFamilyCurrentPublication
{
    private readonly NativeMapModelPopulationAcceptedFrame population;
    private readonly NativeMapArmyPopulationAcceptedFrame army;
    private readonly IReadOnlyList<NativeTerrainBatch> terrain;
    private readonly NativeTerrainBatch[] terrainSnapshot;
    private readonly DeferredSourceMode16Candidate[] candidates;
    private readonly string provenance;

    private DeferredSourceViewFamilyCurrentPublication(
        DeferredSourceViewFamilyOwner owner,
        MapSourceCreatedNativeSessionPreparedFrame accepted,
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army)
    {
        AcceptedFrame = accepted; this.population = population; this.army = army;
        ValidateBoundary(accepted, population, army);
        terrain = accepted.Context.AcceptedTerrainBatches!;
        terrainSnapshot = terrain.Select(b => new NativeTerrainBatch(b.Coarse,
            Array.AsReadOnly(b.Patches.ToArray()))).ToArray();
        candidates = ReadCompleteCandidates(population, army);
        ValidateCompleteRoster(population, army, candidates);
        provenance = "actual-accepted-source-geometry/final-primitive-parents;attempt=" +
            accepted.Context.FrameNumber + ";ViewGeneration=" + owner.ViewGenerationIdentity +
            ";static=" + population.Frame.Visibility.Visible.Count +
            ";ordinaryActors=" + army.Prepared.OrdinaryActors + ";flagActors=" + army.Prepared.FlagActors +
            ";terrainBatches=" + terrain.Count + ";terrainPatches=" + terrain.Sum(b => b.Patches.Count);
        SourceFamilies = owner.Prepare(accepted.PreparedSourceFrame!, candidates, provenance);
    }

    public MapSourceCreatedNativeSessionPreparedFrame AcceptedFrame { get; }
    public DeferredSourceViewFamilyPacket SourceFamilies { get; }
    public int AcceptedTerrainBatchCount => terrainSnapshot.Length;
    public int AcceptedTerrainPatchCount => terrainSnapshot.Sum(b => b.Patches.Count);

    internal static DeferredSourceViewFamilyCurrentPublication Create(
        DeferredSourceViewFamilyOwner owner,
        MapSourceCreatedNativeSessionPreparedFrame accepted,
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army)
    {
        ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(population); ArgumentNullException.ThrowIfNull(army);
        return new(owner, accepted, population, army);
    }

    public void ValidateCurrent(DeferredAuxiliarySourcePixelStages stages,
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army)
    {
        ArgumentNullException.ThrowIfNull(stages);
        ValidateCurrent(stages.AcceptedFrame, population, army);
        if (!ReferenceEquals(stages.RefractionCopy.AcceptedSelection, AcceptedFrame.Selection) ||
            stages.RefractionCopy.CopyDepthReceipt.FrameNumber != AcceptedFrame.Context.FrameNumber)
            throw new InvalidDataException("View family publication differs from the accepted refraction copy.");
    }

    public void ValidateCurrent(MapSourceCreatedNativeSessionPreparedFrame accepted,
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army)
    {
        if (!ReferenceEquals(accepted, AcceptedFrame) || !ReferenceEquals(population, this.population) ||
            !ReferenceEquals(army, this.army))
            throw new InvalidDataException("View family publication belongs to another accepted geometry frame.");
        ValidateBoundary(accepted, population, army);
        var currentTerrain = accepted.Context.AcceptedTerrainBatches!;
        if (!ReferenceEquals(currentTerrain, terrain) || currentTerrain.Count != terrainSnapshot.Length ||
            currentTerrain.Where((b, i) => b.Coarse != terrainSnapshot[i].Coarse ||
                !b.Patches.SequenceEqual(terrainSnapshot[i].Patches)).Any())
            throw new InvalidDataException("Accepted terrain membership changed after View family publication.");
        var current = ReadCompleteCandidates(population, army);
        if (!current.SequenceEqual(candidates))
            throw new InvalidDataException("Accepted final primitive parents changed after View family publication.");
        ValidateCompleteRoster(population, army, current);
        SourceFamilies.ValidateCurrent(accepted.PreparedSourceFrame!);
    }

    private static void ValidateBoundary(MapSourceCreatedNativeSessionPreparedFrame accepted,
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army)
    {
        var source = accepted.PreparedSourceFrame ??
            throw new InvalidDataException("View families require the complete current owned source frame.");
        var geometry = accepted.ActualGeometrySourceFrame ??
            throw new InvalidDataException("View families require the original accepted geometry source snapshot.");
        WorldGeometryShadingPhase.RequireSameGeometrySnapshot(geometry, source);
        var terrain = accepted.Context.AcceptedTerrainBatches ??
            throw new InvalidDataException("View families require the actual accepted terrain batch snapshot.");
        if (!army.Prepared.Ordinary.Keys.ToHashSet().SetEquals(army.Prepared.Flags.Keys))
            throw new InvalidDataException("Accepted Army frame omits a complete unit flag family.");
        if (accepted.Context.FrameNumber <= 0 || source.FrameNumber != accepted.Context.FrameNumber ||
            !ReferenceEquals(population.Context, accepted.Context) ||
            !ReferenceEquals(army.Prepared.Context, accepted.Context) || source.CpuPopulation is null ||
            !ReferenceEquals(source.CpuPopulation.Visibility, population.Frame.Visibility) ||
            !population.Frame.Complete || army.Prepared.Unresolved.Count != 0 || terrain.Count == 0 ||
            terrain.Any(b => b is null || b.Patches is null || b.Patches.Count == 0))
            throw new InvalidDataException("View families need the complete same-attempt static, Army and terrain geometry owners.");
        if (army.WorldQueuesCompletedPublication is not null)
        {
            if (population.StaticQueueSubmission is null)
                throw new InvalidDataException("World View families need the actual static54/55 completion following Army49/50.");
            _ = NativeMapStaticSubmission.RequireCurrent(population, accepted.Context, source);
            _ = NativeArmyWorldAcceptedGeometry.RequireCurrent(army, accepted.Context, source);
        }
        else
        {
            if (population.GeometrySubmission is not { } models ||
                !ReferenceEquals(models.Target, accepted.Context.TerrainFrame) ||
                army.FlagStatuses.Any(s => !s.Submitted) || army.OrdinaryGeometrySubmission is not { } ordinary ||
                army.FlagGeometrySubmission is not { } flags ||
                !ReferenceEquals(ordinary.Target, accepted.Context.TerrainFrame) ||
                !ReferenceEquals(flags.Target, accepted.Context.TerrainFrame))
                throw new InvalidDataException("Legacy View family publication needs its two actual current Army layer submissions.");
            models.RequireCurrent(); ordinary.RequireCurrent(); flags.RequireCurrent();
        }
         accepted.Feature.ValidateFrame(accepted.MainCamera, accepted.Lighting);
    }

    private static DeferredSourceMode16Candidate[] ReadCompleteCandidates(
        NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army)
    {
        var models = population.CurrentViewFamilyCandidates ??
            throw new InvalidDataException("Actual final static primitive parent publication is unavailable.");
        var actors = army.CurrentViewFamilyCandidates ??
            throw new InvalidDataException("Actual final ordinary Army/pole/cloth parent publication is unavailable.");
        if (army.WorldQueuesCompletedPublication is { } world && !actors.SequenceEqual(world.CurrentMode16Candidates))
            throw new InvalidDataException("Army final parents differ from the actual admitted49/50 aggregate.");
        if (models.Any(c => c is null || c.Family != "StaticModel") ||
            actors.Any(c => c is null || c.Family is not ("ArmyOrdinary" or "ArmyFlagBase" or "ArmyFlagCloth")))
            throw new InvalidDataException("Accepted primitive parent publisher has an unsupported source family.");
        return models.Concat(actors).ToArray();
    }

    private readonly record struct Member(string Family, string InstanceKey, int DrawIndex,
        string ResourceUri, string Sha256);

    private static void ValidateCompleteRoster(NativeMapModelPopulationAcceptedFrame population,
        NativeMapArmyPopulationAcceptedFrame army, DeferredSourceMode16Candidate[] candidates)
    {
        var expected = new List<Member>();
        foreach (var instance in population.Frame.Visibility.Visible)
        foreach (var draw in instance.Asset.Draws)
            expected.Add(new("StaticModel", instance.InstanceKey, draw.Index,
                instance.Asset.RawTransforms.Source.ResourceUri, instance.Asset.RawTransforms.Source.DecodedSha256));
        var completeArmyInventory = new List<Member>();
        foreach (var (unitId, frame) in army.Prepared.Ordinary)
        {
            if (frame.UnitId != unitId)
                throw new InvalidDataException("Ordinary Army roster differs from its accepted unit owner.");
            foreach (var actor in frame.Publications.Values)
            {
                if (!army.Prepared.Models.TryGetValue(actor.Category, out var model))
                    throw new InvalidDataException("Ordinary Army roster has no accepted actual model owner.");
                foreach (var draw in G1ModelDrawReader.Read(model.Transforms.Geometry).Draws)
                    completeArmyInventory.Add(new("ArmyOrdinary", $"remake-army/{unitId}/{actor.ComponentId}", draw.Index,
                        model.Transforms.Source.ResourceUri, model.Transforms.Source.DecodedSha256));
            }
        }
        var expectedStatuses = new List<(int Unit, int Descriptor, ulong Node, int Draw)>();
        foreach (var (unitId, frame) in army.Prepared.Flags)
        {
            if (frame.UnitId != unitId || !army.Prepared.Ordinary.ContainsKey(unitId))
                throw new InvalidDataException("Flag roster differs from its accepted ordinary Army owner.");
            foreach (var actor in frame.Actors)
            {
                if (actor.UnitId != unitId)
                    throw new InvalidDataException("Flag actor differs from its accepted unit owner.");
                foreach (var draw in actor.Source.Draws)
                {
                    completeArmyInventory.Add(new("ArmyFlag", actor.InstanceKey, draw.Index,
                        actor.Source.Model.Source.ResourceUri, actor.Source.Model.Source.DecodedSha256));
                    expectedStatuses.Add((unitId, actor.DescriptorOffset, actor.OwnedNodeIdentity, draw.Index));
                }
            }
        }
        if (army.WorldQueuesCompletedPublication is { } world)
        {
            // Source visibility/counter admission removes ordinary View members,
            // while the complete canonical inventory and flag statuses remain.
            // Successful per-record draws may repeat one canonical primitive.
            var actors = world.CompleteCpuInventory.Actors.ToDictionary(a => a.ActorIdentity, StringComparer.Ordinal);
            var inventory = world.CompleteCpuInventory.Primitives.Select(p => new Member(p.Family, p.ActorIdentity,
                p.SourceDrawIndex, actors[p.ActorIdentity].Source.Model.Source.ResourceUri,
                actors[p.ActorIdentity].Source.Model.Source.DecodedSha256)).ToArray();
            RequireSameMembers(completeArmyInventory, inventory, "Army aggregate omits or replaces a canonical source actor/draw.");
            expected.AddRange(world.CompleteCpuInventory.Primitives.Where(p => p.AdmittedRecordOwners.Count != 0)
                .Select(p => new Member(p.Family, p.ActorIdentity, p.SourceDrawIndex,
                    actors[p.ActorIdentity].Source.Model.Source.ResourceUri, actors[p.ActorIdentity].Source.Model.Source.DecodedSha256)));
            var admitted = world.SubmittedPrimitives.Select(p => p.CanonicalPacketKey).ToHashSet(StringComparer.Ordinal);
            foreach (var status in army.FlagStatuses)
            {
                var actor = army.Prepared.Flags[status.UnitId].Actors.Single(a => a.DescriptorOffset == status.DescriptorOffset &&
                    a.OwnedNodeIdentity == status.NodeIdentity);
                if (status.Submitted != admitted.Contains(actor.InstanceKey + "/draw" + status.DrawIndex))
                    throw new InvalidDataException("Army flag status disagrees with actual admitted49/50 command submission.");
            }
        }
        else expected.AddRange(completeArmyInventory);
        var actual = candidates.Select(c => new Member(c.Family is "ArmyFlagBase" or "ArmyFlagCloth" ?
            "ArmyFlag" : c.Family, c.InstanceKey, c.SourceDrawIndex, c.ModelResourceUri, c.ModelSha256)).ToArray();
        RequireSameMembers(expected, actual, "View family publication omits, duplicates or replaces an actual admitted source draw.");
        var statuses = army.FlagStatuses.Select(s => (s.UnitId, s.DescriptorOffset, s.NodeIdentity, s.DrawIndex)).ToArray();
        if (statuses.Distinct().Count() != statuses.Length || expectedStatuses.Count != statuses.Length ||
            !expectedStatuses.ToHashSet().SetEquals(statuses))
            throw new InvalidDataException("View family publication lacks the full completed flag draw roster.");
        if (army.WorldQueuesCompletedPublication is null)
        {
            ValidateArmySubmission(army.OrdinaryGeometrySubmission!, actual.Where(c => c.Family == "ArmyOrdinary"));
            ValidateArmySubmission(army.FlagGeometrySubmission!, actual.Where(c => c.Family == "ArmyFlag"));
        }
    }

    private static void RequireSameMembers(IReadOnlyList<Member> expected, IReadOnlyList<Member> actual, string failure)
    {
        if (expected.Distinct().Count() != expected.Count || actual.Distinct().Count() != actual.Count ||
            expected.Count != actual.Count || !expected.ToHashSet().SetEquals(actual))
            throw new InvalidDataException(failure);
    }

    private static void ValidateArmySubmission(ModelGeometrySubmittedFrame submission,
        IEnumerable<Member> members)
    {
        var expected = members.Select(m => (Key: m.InstanceKey + "/draw" + m.DrawIndex,
            m.DrawIndex, m.ResourceUri, m.Sha256)).ToArray();
        var actual = submission.Primitives.Select(p => (Key: p.PacketKey,
            DrawIndex: p.SourceDrawIndex, ResourceUri: p.ModelUri, Sha256: p.ModelSha256)).ToArray();
        if (submission.LayerSubmissionOrdinal <= 0 || actual.Distinct().Count() != actual.Length ||
            expected.Length != actual.Length || !expected.ToHashSet().SetEquals(actual))
            throw new InvalidDataException("View family roster differs from the actual completed Army geometry batch.");
    }
}
