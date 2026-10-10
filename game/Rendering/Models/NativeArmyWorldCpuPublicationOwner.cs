using San14.Data;
using San14.Core;

public sealed record NativeArmyWorldLogicalActor(string Family, string ActorIdentity,
    ArmyPrimitivePublicationSource Source, UnitWorldTransformState ActualWorld,
    RemakeArmyInitialization ActualInitialization);

/// <summary>CPU-only ordinary Army/linked flag visibility and independent
/// World-record publication. Does not release World/Terrain/Stock guards or
/// acknowledge graphics work. Sources and constructor words are reused.</summary>
public sealed class NativeArmyWorldCpuPublicationOwner
{
    private readonly Dictionary<LoadedRdbAsset, ArmyPrimitivePublicationSource> primitiveSources =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<ArmyPrimitivePublicationSource, ArmyModelWorldVisibilitySource> visibilitySources =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, (ArmyPrimitivePublicationSource Source,
        MapModelNativeCurrentVisibilityWords Words)> nodes =
        new(StringComparer.Ordinal);
    private bool retained = true;

    public void MarkImportedEditedOrUnknownVisibilityWriter(string actualWriter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter);
        retained = false; nodes.Clear(); primitiveSources.Clear(); visibilitySources.Clear();
    }

    /// <summary>Pure source CPU seam also usable before target construction.
    /// Complete ordinary/flag inventories must come from the same owned pose
    /// preparation; no marker provider, partial model list or GPU count is used.</summary>
    public IReadOnlyList<NativeArmyWorldLogicalActor> CaptureActualLogicalInventory(
        GameState actualCurrentWorld,
        IReadOnlyDictionary<int, RemakeArmyVisualFrame> actualOrdinary,
        IReadOnlyDictionary<int, LoadedArmyModel> actualModels,
        IReadOnlyDictionary<int, RemakeArmyFlagFrame> actualFlags)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentWorld);
        if (!actualCurrentWorld.Units.Keys.Order().SequenceEqual(actualOrdinary.Keys.Order()) ||
            actualCurrentWorld.Units.Values.Any(u => u.NativeArmyOrigin is null || !ArmyConfigurationSourceAdapter.HasConfiguration(u) ||
                !actualOrdinary.TryGetValue(u.Id, out var f) || f.HexId != u.HexId || f.FormationSlot != u.FormationId ||
                ArmyConfigurationSourceAdapter.ReadCurrentLand(actualCurrentWorld, u) != u.FormationId))
            throw new InvalidDataException("World Army logical inventory needs every actual source-created current unit and matching Hex/formation pose.");
        return CaptureKnownCompleteInventory(actualOrdinary, actualModels, actualFlags);
    }

    private IReadOnlyList<NativeArmyWorldLogicalActor> CaptureKnownCompleteInventory(
        IReadOnlyDictionary<int, RemakeArmyVisualFrame> actualOrdinary,
        IReadOnlyDictionary<int, LoadedArmyModel> actualModels,
        IReadOnlyDictionary<int, RemakeArmyFlagFrame> actualFlags)
    {
        RequireRetained(); ArgumentNullException.ThrowIfNull(actualOrdinary);
        ArgumentNullException.ThrowIfNull(actualModels); ArgumentNullException.ThrowIfNull(actualFlags);
        if (!actualOrdinary.Keys.Order().SequenceEqual(actualFlags.Keys.Order()))
            throw new InvalidDataException("World Army inventory requires every ordinary unit's actual linked flag preparation.");
        var actors = new List<NativeArmyWorldLogicalActor>();
        foreach (var (id, frame) in actualOrdinary)
        {
            if (frame.UnitId != id || frame.Mode != 0 || !frame.OrdinaryWrites.Complete || !frame.FlagWrites.Complete ||
                frame.Publications.Count == 0)
                throw new InvalidDataException("World Army ordinary source records/poses are incomplete.");
            foreach (var actor in frame.Publications.Values)
            {
                if (actor.Category is not (0 or 1) || !actualModels.TryGetValue(actor.Category, out var model) ||
                    model.Category.Category != actor.Category)
                    throw new InvalidDataException("World Army ordinary actor has no exact source-created category model.");
                actors.Add(new("ArmyOrdinary", $"remake-army/{id}/{actor.ComponentId}",
                    Source(model.Catalog, actor.Category, model.Transforms.Source), actor.WorldTransform, frame.Initialization));
            }
            var flags = actualFlags[id];
            if (flags.UnitId != id || flags.Actors.Count == 0)
                throw new InvalidDataException("World Army linked flag CPU inventory is incomplete.");
            foreach (var actor in flags.Actors)
            {
                if (actor.UnitId != id || !ReferenceEquals(actor.Initialization, frame.Initialization) || actor.Category is not (2 or 3))
                    throw new InvalidDataException("World Army flag belongs to another ordinary source initialization.");
                actors.Add(new("ArmyFlag", actor.InstanceKey, Source(actor.Source.Catalog, actor.Category,
                    actor.Source.Model.Source), actor.World, actor.Initialization));
            }
        }
        if (actors.Select(a => a.ActorIdentity).Distinct(StringComparer.Ordinal).Count() != actors.Count)
            throw new InvalidDataException("World Army logical inventory duplicates an actual actor.");
        return actors.AsReadOnly();
    }

    public ArmyWorldPrimitivePublication PrepareActualBeforeFrame(NativeMapArmyPreparedFrame actualArmy,
        MapWorldKind1CurrentQueue actualQueue, MapFreshCommonResourceSnapshot actualCommon,
        ArmyCurrentWorldVisibilityView actualView, string currentPublicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualArmy);
        if (actualArmy.Unresolved.Count != 0 || actualArmy.CpuPrimitivePopulation is not { } population ||
            population.Context.FrameNumber != actualQueue.FrameNumber || actualArmy.Context.PreparedCamera is not { } camera)
            throw new InvalidDataException("World Army requires this complete current BeforeFrame inventory and accepted Camera.");
        var actors = CaptureKnownCompleteInventory(actualArmy.Ordinary, actualArmy.Models, actualArmy.Flags);
        if (!actors.Select(a => (a.Family, a.ActorIdentity, a.Source.Identity)).ToHashSet().SetEquals(
            population.Actors.Select(a => (a.Family, a.Actor, a.Source.Identity))))
            throw new InvalidDataException("World Army logical actors differ from the actual accepted CPU primitive owner.");
        return PrepareLogicalCurrent(actors, camera, actualQueue, actualCommon, actualView, currentPublicationIdentity);
    }

    public ArmyWorldPrimitivePublication PrepareLogicalCurrent(
        IReadOnlyList<NativeArmyWorldLogicalActor> completeActualActors, DeferredCameraInputs actualCamera,
        MapWorldKind1CurrentQueue actualQueue, MapFreshCommonResourceSnapshot actualCommon,
        ArmyCurrentWorldVisibilityView actualView, string currentPublicationIdentity)
    {
        RequireRetained(); ArgumentNullException.ThrowIfNull(completeActualActors); ArgumentNullException.ThrowIfNull(actualCamera);
        ArgumentNullException.ThrowIfNull(actualQueue); ArgumentNullException.ThrowIfNull(actualCommon);
        ArgumentNullException.ThrowIfNull(actualView);
        if (actualView.FrameNumber != actualQueue.FrameNumber || actualView.ViewIdentity != actualQueue.Binding.ViewIdentity)
            throw new InvalidDataException("Army current View/camera source selection differs from the counted World queue.");
        var camera = MapStaticModelCamera.FromExplicitRows(Rows(actualCamera.SourceWorldToProjection),
            MapStaticModelTransforms.ToShaderAffineRows(actualCamera.SourceWorldToView));
        var current = new List<ArmyWorldCurrentPrimitiveActor>();
        foreach (var actor in completeActualActors)
        {
            if (!visibilitySources.TryGetValue(actor.Source, out var source))
                visibilitySources.Add(actor.Source, source = ArmyModelWorldVisibilitySource.Read(actor.Source));
            // A discrete pose/animation reset on Hex changes does not create
            // another ModelWorldPQ node. Retain its actual visibility words
            // until removal or a real source-model replacement.
            if (!nodes.TryGetValue(actor.ActorIdentity, out var node) || !ReferenceEquals(node.Source, actor.Source))
                nodes[actor.ActorIdentity] = node = (actor.Source, source.ConstructOwnedVisibility(actor.ActorIdentity));
            var visible = source.PrepareOwnedUnparentedCurrent(actualQueue.FrameNumber, actor.ActorIdentity,
                actor.ActualWorld, node.Words, camera, actualView);
            current.Add(new(actor.ActorIdentity, actor.Family, actor.Source, visible));
        }
        var result = ArmyWorldPrimitivePublication.Prepare(actualQueue, actualCommon, current, currentPublicationIdentity, actualCamera);
        var retainedActors = completeActualActors.Select(a => a.ActorIdentity).ToHashSet(StringComparer.Ordinal);
        foreach (string key in nodes.Keys.Where(k => !retainedActors.Contains(k)).ToArray()) nodes.Remove(key);
        return result;
    }

    private ArmyPrimitivePublicationSource Source(LoadedArmyCatalog catalog, int category, LoadedRdbAsset model)
    {
        if (!primitiveSources.TryGetValue(model, out var source))
            primitiveSources.Add(model, source = ArmyPrimitivePublicationSource.ReadKnownCategory(catalog, category, model));
        if (source.Category.Category != category)
            throw new InvalidDataException("Army model source was assigned to a different category.");
        return source;
    }
    private void RequireRetained()
    { if (!retained) throw new InvalidDataException("World Army current visibility/source generation has an unhandled writer; rebuild or provide its actual source branch."); }
    private static float[] Rows(System.Numerics.Matrix4x4 m) => [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,
        m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44];
}
