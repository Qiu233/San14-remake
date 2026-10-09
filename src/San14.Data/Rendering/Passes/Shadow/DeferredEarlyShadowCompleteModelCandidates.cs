namespace San14.Data;

/// <summary>The same owned node's current GeometryInput, sixteen opacity
/// words, World masks and bound publication. A Main visibility result is not
/// an input: the independent shadow culler has not run at this boundary.</summary>
public sealed record DeferredEarlyShadowCurrentStaticModelInputs(
    ModelCurrentPrimitiveBinding GeometryInput,
    MapModelNativeCurrentVisibilityWords OpacityB0,
    DeferredEarlyShadowCurrentWorldCallerFields WorldCaller,
    MapModelNativeBoundsPublication Bounds);

public sealed record DeferredEarlyShadowCurrentArmyModelInputs(
    ModelCurrentPrimitiveBinding GeometryInput,
    DeferredEarlyShadowCurrentWorldCallerFields WorldCaller);

/// <summary>C394F0 keeps one actual Model OBB for the complete categories.
/// Its D08F80 callback tests that OBB, then BB85A0 publishes each accepted
/// category span in its native part/group/reference order.</summary>
public sealed class DeferredEarlyShadowCurrentModelCandidate
{
    internal DeferredEarlyShadowCurrentModelCandidate(string model, string family,
        string currentSource, ModelPrimitiveInventorySource geometry,
        MapModelNativeOrientedBounds bounds, float opacity,
        DeferredEarlyShadowCurrentWorldCallerFields caller, string source)
        => (ModelIdentity, Family, CurrentSourceIdentity, Geometry, Bounds, PerViewOpacityB0,
            WorldCaller, CurrentProducerProvenance) =
            (model, family, currentSource, geometry, bounds, opacity, caller, source);

    public string ModelIdentity { get; }
    public string Family { get; }
    public string CurrentSourceIdentity { get; }
    public ModelPrimitiveInventorySource Geometry { get; }
    public MapModelNativeOrientedBounds Bounds { get; }
    public float PerViewOpacityB0 { get; }
    public DeferredEarlyShadowCurrentWorldCallerFields WorldCaller { get; }
    public string CurrentProducerProvenance { get; }
    public ModelPrimitiveCategoryInventory Category0 => Geometry.Category0;
    public ModelPrimitiveCategoryInventory Category1 => Geometry.Category1;

    /// <summary>DB7A18 compares zero with B0, retaining the unordered branch.
    /// This only describes the slot gate; it does not admit a caster or grant
    /// normal frustum visibility.</summary>
    public bool OpacitySlotEligible => !(PerViewOpacityB0 <= 0f);
}

/// <summary>Complete same-family current candidates before the original
/// shadow culler. Publication order is the supplied family's actual owner
/// order. Cross-family insertion order, tuples and counters are separate.</summary>
public sealed class DeferredEarlyShadowCompleteModelCandidates
{
    private DeferredEarlyShadowCompleteModelCandidates(long frame, string view, int index,
        DeferredEarlyShadowCurrentModelCandidate[] models, string publication)
        => (FrameNumber, ViewIdentity, ViewIndex, Models, PublicationIdentity) =
            (frame, view, index, Array.AsReadOnly(models), publication);

    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public int ViewIndex { get; }
    public string PublicationIdentity { get; }
    public IReadOnlyList<DeferredEarlyShadowCurrentModelCandidate> Models { get; }
    public int Category0ReferenceCount => Models.Sum(model => model.Category0.Count);
    public int Category1ReferenceCount => Models.Sum(model => model.Category1.Count);

    /// <summary>Reads Population.Instances, including every offscreen or
    /// ordinary slot-ineligible node. The complete current bindings must come
    /// from the actual node owner; an unknown/replaced GeometryInput cannot
    /// borrow the source ModelCPU inventory.</summary>
    public static DeferredEarlyShadowCompleteModelCandidates PrepareCurrentStatic(
        MapStaticModelCurrentOwnedFrame actualFrame, string actualViewIdentity,
        int actualViewIndex,
        IReadOnlyDictionary<string, DeferredEarlyShadowCurrentStaticModelInputs> actualCompleteInputs,
        string actualPublicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualFrame);
        ArgumentNullException.ThrowIfNull(actualCompleteInputs);
        RequireIdentity(actualFrame.FrameNumber, actualViewIdentity, actualViewIndex, actualPublicationIdentity);
        var instances = actualFrame.Population.Instances;
        if (!actualFrame.Population.Complete ||
            !instances.Select(instance => instance.InstanceKey).ToHashSet(StringComparer.Ordinal).SetEquals(actualCompleteInputs.Keys) ||
            instances.Select(instance => instance.InstanceKey).Distinct(StringComparer.Ordinal).Count() != instances.Count)
            throw new InvalidDataException("Shadow candidate inputs must cover the complete actual static Population exactly once.");
        var models = new DeferredEarlyShadowCurrentModelCandidate[instances.Count];
        for (int i = 0; i < instances.Count; i++)
        {
            var instance = instances[i]; var input = actualCompleteInputs[instance.InstanceKey];
            RequireGeometry(input.GeometryInput, instance.CurrentSourceIdentity);
            RequireWorld(input.WorldCaller);
            if (input.OpacityB0.CurrentSourceIdentity != instance.CurrentSourceIdentity)
                throw new InvalidDataException("Shadow candidate B0 belongs to another actual static node.");
            var bound = MapModelNativeBoundsSources.FromSourceWorld(instance, input.Bounds);
            var geometry = ModelPrimitiveInventorySource.LoadStatic(instance.Asset);
            models[i] = new(instance.InstanceKey, "Static", instance.CurrentSourceIdentity!, geometry,
                bound, input.OpacityB0.ReadSlot(actualViewIndex), input.WorldCaller,
                input.GeometryInput.CurrentProducerProvenance + ";" + input.OpacityB0.Basis + ";" + bound.Basis +
                ";C394F0:actual-model-OBB/category0/1-complete-reference-spans");
        }
        return new(actualFrame.FrameNumber, actualViewIdentity, actualViewIndex, models, actualPublicationIdentity);
    }

    /// <summary>The current Army actor receipt already contains its actual
    /// source Model OBB and B0 View0 scalar. Keep the complete Actors list;
    /// CurrentOrdinaryView0Eligible and RejectedByCulling never filter it.</summary>
    public static DeferredEarlyShadowCompleteModelCandidates PrepareCurrentArmy(
        ArmyWorldPrimitivePublication actualPublication,
        IReadOnlyDictionary<string, DeferredEarlyShadowCurrentArmyModelInputs> actualCompleteInputs,
        string actualPublicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualPublication);
        ArgumentNullException.ThrowIfNull(actualCompleteInputs);
        var actors = actualPublication.Actors;
        RequireIdentity(actualPublication.Counts.Queue.FrameNumber, actualPublication.Counts.Queue.Binding.ViewIdentity,
            0, actualPublicationIdentity);
        if (actors.Select(actor => actor.ActorIdentity).Distinct(StringComparer.Ordinal).Count() != actors.Count ||
            !actors.Select(actor => actor.ActorIdentity).ToHashSet(StringComparer.Ordinal).SetEquals(actualCompleteInputs.Keys))
            throw new InvalidDataException("Shadow candidate inputs must cover every actual Army actor exactly once.");
        var models = new DeferredEarlyShadowCurrentModelCandidate[actors.Count];
        for (int i = 0; i < actors.Count; i++)
        {
            var actor = actors[i]; var current = actor.Visibility; var input = actualCompleteInputs[actor.ActorIdentity];
            RequireGeometry(input.GeometryInput, actor.ActorIdentity); RequireWorld(input.WorldCaller);
            if (current.FrameNumber != actualPublication.Counts.Queue.FrameNumber ||
                current.ViewIdentity != actualPublication.Counts.Queue.Binding.ViewIdentity ||
                current.ActorIdentity != actor.ActorIdentity || !ReferenceEquals(current.Source.PrimitiveSource, actor.Source))
                throw new InvalidDataException("Shadow candidate Army transform/B0 belongs to another actual actor, frame or View.");
            MapModelNativeBoundsSources.Validate(current.Bounds);
            models[i] = new(actor.ActorIdentity, actor.Family, actor.ActorIdentity,
                ModelPrimitiveInventorySource.LoadArmy(actor.Source), current.Bounds,
                current.CurrentViewScalar, input.WorldCaller,
                input.GeometryInput.CurrentProducerProvenance + ";" + current.SourceEvidence +
                ";C394F0:actual-model-OBB/category0/1-complete-reference-spans");
        }
        return new(actualPublication.Counts.Queue.FrameNumber, actualPublication.Counts.Queue.Binding.ViewIdentity,
            0, models, actualPublicationIdentity);
    }

    private static void RequireGeometry(ModelCurrentPrimitiveBinding actual, string? identity)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (string.IsNullOrWhiteSpace(identity) || actual.CurrentSourceIdentity != identity ||
            actual.RetainedSourceModelCpuIsCurrent != true || string.IsNullOrWhiteSpace(actual.CurrentProducerProvenance))
            throw new InvalidDataException("Shadow candidate requires the actual retained current ModelCPU GeometryInput provider.");
    }
    private static void RequireWorld(DeferredEarlyShadowCurrentWorldCallerFields actual)
    {
        if (string.IsNullOrWhiteSpace(actual.CurrentWriterEvidence))
            throw new InvalidDataException("Shadow candidate current World caller fields have no actual writer source.");
    }
    private static void RequireIdentity(long frame, string view, int index, string publication)
    {
        if (frame <= 0 || string.IsNullOrWhiteSpace(view) || (uint)index >= 16 || string.IsNullOrWhiteSpace(publication))
            throw new InvalidDataException("Shadow candidate owner needs the actual frame, View and B0 slot0..15.");
    }
}
