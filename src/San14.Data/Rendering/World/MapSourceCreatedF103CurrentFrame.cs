namespace San14.Data;

public enum MapSourceCreatedReflectionQueryScope
{
    ExplicitCurrentQuery,
    InactiveBecauseSourceCamera1808IsNull
}

/// <summary>Current owner fields that source assets alone cannot resolve.
/// Model queue mask retention and GeometryInput fallback belong to the owned
/// CPU publisher. Additional View queues and later exclusion setters are
/// separate current producers. No unknown value selects an empty queue.</summary>
public sealed record MapSourceCreatedF103PopulationPublicationInputs(
    bool? FreshWorldPqOrdinaryMaskFields328And334Retained,
    bool? ActualGeometryInputFallbackOwnsSource,
    MapSourceCreatedViewPublicationState CurrentViewPublication,
    IReadOnlyDictionary<string, MapSourceCreatedModelExclusionOverride> CurrentExclusionOverrides,
    bool? OtherOrdinaryPublishersAbsent,
    MapSourceCreatedViewRecordCounts? OtherCurrentOrdinaryPrimitiveCounts,
    string CpuPublicationIdentity);

/// <summary>One current owned preparation boundary. The same immutable
/// Camera/light generation, published View, Common settings and Host words
/// produce both keys consumed by the original F103 shader selector.</summary>
public sealed class MapSourceCreatedF103CurrentFrame
{
    internal MapSourceCreatedF103CurrentFrame(MapCameraConstructionPacket camera,
        MapSourceCreatedLightingPacket lighting, MapSourceCreatedViewFieldsPacket fields,
        MapFreshCommonResourceSnapshot common, MainMapContextCurrentPacket context,
        MapSourceCreatedF103ColorPacket color, DeferredF103CurrentViewInputs view,
        DeferredF103CurrentVariantInputs variant, MapSourceCreatedF103FeaturePacket feature,
        DeferredF103SelectedSource selection, MapSourceCreatedReflectionQueryScope reflectionScope,
        int? actualReflectionQuery, MapStaticModelCurrentOwnedFrame? population)
    {
        Camera = camera.CopyAcceptedSnapshot(); Lighting = lighting.CopyAcceptedSnapshot(); ViewFields = fields;
        Common = common; Context = context; Color = color; View = view; Variant = variant;
        Feature = feature; Selection = selection; ReflectionQueryScope = reflectionScope;
        ActualReflectionQueryD57E20 = actualReflectionQuery; CpuPopulation = population;
    }
    public long FrameNumber => Camera.FrameNumber;
    public MapCameraConstructionPacket Camera { get; }
    public MapSourceCreatedLightingPacket Lighting { get; }
    public MapSourceCreatedViewFieldsPacket ViewFields { get; }
    public MapFreshCommonResourceSnapshot Common { get; }
    public MainMapContextCurrentPacket Context { get; }
    public MapSourceCreatedF103ColorPacket Color { get; }
    public DeferredF103CurrentViewInputs View { get; }
    public DeferredF103CurrentVariantInputs Variant { get; }
    public MapSourceCreatedF103FeaturePacket Feature { get; }
    public DeferredF103SelectedSource Selection { get; }
    public MapSourceCreatedReflectionQueryScope ReflectionQueryScope { get; }
    /// <summary>Null is an unrequested inactive query, not an observed getter0.</summary>
    public int? ActualReflectionQueryD57E20 { get; }
    /// <summary>The exact CPU population borrowed by the later native model
    /// submission. No GPU completion is used to produce this frame's View.</summary>
    public MapStaticModelCurrentOwnedFrame? CpuPopulation { get; }
}

/// <summary>Source construction plus a same-frame join. It never substitutes
/// feature row0, constructor-empty current counters, or a fixed combination.
/// Header source validation is retained once; View count publication stays
/// with its actual CPU producer.</summary>
public sealed class MapSourceCreatedF103CurrentFrameFactory
{
    private readonly LoadedDeferredTexturePlan plan;
    private readonly MapSourceCreatedF103ColorSource colorSource;
    public MapSourceCreatedF103CurrentFrameFactory(LoadedDeferredTexturePlan sourcePlan,
        MapSourceCreatedF103ColorScope explicitHeaderScope)
    {
        ArgumentNullException.ThrowIfNull(sourcePlan);
        colorSource = MapSourceCreatedF103ColorMode.ReadFreshSource(sourcePlan, explicitHeaderScope);
        plan = sourcePlan;
    }

    /// <summary>Actual current Main CPU population → original ordinary
    /// primitive counters → final View19AC/19B4 → complete F103 keys. Source
    /// model definitions, classification, capacities and Common selector are
    /// read by their proved producers; callers do not repeat draw dictionaries.
    /// The complete population remains available on the returned frame.</summary>
    public MapSourceCreatedF103CurrentFrame PrepareFromPopulation(
        MapStaticModelCurrentOwnedFrame currentCpuPopulation,
        MapSourceCreatedLightingSource sourceLighting, MapSourceCreatedLightingPacket currentLighting,
        MapFreshCommonResourceSnapshot currentCommon, MainMapContextCurrentPacket currentContext,
        MapSourceCreatedF103PopulationPublicationInputs publication,
        int? actualReflectionQueryD57E20, string currentPreparationIdentity)
    {
        ArgumentNullException.ThrowIfNull(currentCpuPopulation); ArgumentNullException.ThrowIfNull(sourceLighting);
        ArgumentNullException.ThrowIfNull(currentLighting); ArgumentNullException.ThrowIfNull(currentCommon);
        ArgumentNullException.ThrowIfNull(publication); ArgumentException.ThrowIfNullOrWhiteSpace(publication.CpuPublicationIdentity);
        if (!currentCpuPopulation.Population.Complete || currentCommon.CurrentViewAux3F2 is not byte selector)
            throw new InvalidDataException("F103 population publication needs the complete CPU population and actual Common3F2 selector.");
        if (publication.OtherCurrentOrdinaryPrimitiveCounts is null && publication.OtherOrdinaryPublishersAbsent != true ||
            publication.OtherCurrentOrdinaryPrimitiveCounts is not null && publication.OtherOrdinaryPublishersAbsent != false)
            throw new InvalidDataException("Other current ordinary publishers must be explicitly absent or provide their complete CPU counter publication.");
        var draws = currentCpuPopulation.Visibility.Visible.SelectMany(instance => instance.Asset.Draws.Select(draw =>
            new MapSourceCreatedPreparedModelPrimitive(currentCpuPopulation.FrameNumber,
                currentLighting.Binding.ViewIdentity, instance.InstanceKey, draw.Index,
                publication.CpuPublicationIdentity + ";ordinary-geometry-from-current-owned-source"))).ToArray();
        var counts = MapSourceCreatedViewFields.PrepareFreshModelPopulationCounts(currentCpuPopulation,
            sourceLighting, currentLighting, selector, publication.FreshWorldPqOrdinaryMaskFields328And334Retained,
            currentCommon.CurrentAuxiliaryExclusion404, publication.CurrentExclusionOverrides, draws,
            publication.ActualGeometryInputFallbackOwnsSource, publication.CpuPublicationIdentity);
        if (publication.OtherCurrentOrdinaryPrimitiveCounts is { } other)
        {
            var own = counts.CompleteCurrentCpuCounts; var additional = other.CompleteCurrentCpuCounts;
            if (other.FrameNumber != counts.FrameNumber || other.ViewIdentity != counts.ViewIdentity ||
                !own.Keys.Order().SequenceEqual(additional.Keys.Order()))
                throw new InvalidDataException("Additional ordinary CPU primitives belong to another frame, View or kind1 record sequence.");
            var capacities = MapSourceCreatedViewFields.ReadFreshFirstCounterCapacities(sourceLighting, currentLighting, selector);
            var joined = own.ToDictionary(item => item.Key, item => checked(item.Value + additional[item.Key]));
            if (joined.Any(item => item.Value > 0 && item.Value >= capacities[item.Key]))
                throw new InvalidDataException("The combined actual CPU queues exceed the original strict first-counter admission capacity.");
            counts = new(counts.FrameNumber, counts.ViewIdentity,
                counts.PublicationIdentity + ";other=" + other.PublicationIdentity, joined);
        }
        var camera = currentCpuPopulation.MainCamera.Camera;
        var retained = MapSourceCreatedViewFields.ReadFreshRetainedCameraFields(camera);
        var view = MapSourceCreatedViewFields.PrepareAcceptedViewFields(camera, currentLighting,
            retained, selector, counts, publication.CurrentViewPublication);
        return Prepare(camera, currentLighting, view, currentCommon, currentContext,
            actualReflectionQueryD57E20, currentPreparationIdentity, currentCpuPopulation);
    }

    public MapSourceCreatedF103CurrentFrame Prepare(
        MapCameraConstructionPacket acceptedMainCamera, MapSourceCreatedLightingPacket currentLighting,
        MapSourceCreatedViewFieldsPacket currentPublishedView,
        MapFreshCommonResourceSnapshot currentCommon, MainMapContextCurrentPacket currentContext,
        int? actualReflectionQueryD57E20, string currentPreparationIdentity,
        MapStaticModelCurrentOwnedFrame? sameCurrentCpuPopulation = null,
        MapSourceCreatedLightingPacket? currentInnerModifierLighting = null,
        MapSourceCreatedF103FeaturePacket? previousOrderedFeature = null)
    {
        ArgumentNullException.ThrowIfNull(acceptedMainCamera); ArgumentNullException.ThrowIfNull(currentLighting);
        ArgumentNullException.ThrowIfNull(currentPublishedView); ArgumentNullException.ThrowIfNull(currentCommon);
        ArgumentNullException.ThrowIfNull(currentContext); ArgumentException.ThrowIfNullOrWhiteSpace(currentPreparationIdentity);
        var camera = acceptedMainCamera.CopyAcceptedSnapshot();
        if (camera.Source.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId || camera.FrameNumber <= 0 ||
            currentLighting.FrameNumber != camera.FrameNumber || currentContext.FrameNumber != camera.FrameNumber ||
            currentPublishedView.Camera.FrameNumber != camera.FrameNumber ||
            currentPublishedView.Binding != currentLighting.Binding ||
            currentCommon.FrameNumber != camera.FrameNumber || currentCommon.SelectedViewOwnerIdentity != currentLighting.Binding.ViewIdentity)
            throw new InvalidDataException("Owned F103 fields must come from this accepted Main Camera, View/light owner and serial frame.");
        currentPublishedView.Camera.Validate(camera);
        if (sameCurrentCpuPopulation is not null &&
            (sameCurrentCpuPopulation.FrameNumber != camera.FrameNumber ||
                !sameCurrentCpuPopulation.MainCamera.Camera.Prefix.Span.SequenceEqual(camera.Prefix.Span)))
            throw new InvalidDataException("F103 current CPU population belongs to another Camera or preparation frame.");
        if (currentContext.Phase == MainMapContextCurrentPhase.FreshHostConstructor)
            throw new InvalidDataException("Current F103 needs the actual Host preparation writer or a complete current owned publication.");
        var color = colorSource.Prepare(currentContext);
        if (!color.PreparationEnabled) throw new InvalidDataException("The native F103 color gate rejects this current preparation.");
        string provenance = currentPreparationIdentity + ";frame=" + camera.FrameNumber + ";" +
            currentPublishedView.SourceProvenance + ";" + currentCommon.ProducerProvenance + ";" + color.SourceProvenance;
        var view = currentPublishedView.CreateF103Inputs(currentCommon.CommonViewResource240Present,
            currentCommon.Resource240Flags0C) with { CurrentProducerProvenance = provenance };
        var reflectionScope = actualReflectionQueryD57E20.HasValue
            ? MapSourceCreatedReflectionQueryScope.ExplicitCurrentQuery
            : MapSourceCreatedReflectionQueryScope.InactiveBecauseSourceCamera1808IsNull;
        if (!actualReflectionQueryD57E20.HasValue && currentPublishedView.SelectedCamera1808Present)
            throw new InvalidDataException("An active Camera1808 reflection branch needs its current native D57E20 query.");
        // EAD8A1 tests query!=0 AND Camera1808. Exact source Camera1808 is
        // null, so either query result yields the same variant writer. Zero
        // here is only the inactive projection; no native getter0 is claimed.
        var variant = new DeferredF103CurrentVariantInputs(camera.FrameNumber, currentContext.ContextFlags3F0,
            color.NativeColorMode, currentCommon.CommonViewFlags8, currentCommon.QueryD5BeA0,
            actualReflectionQueryD57E20 ?? 0, currentPublishedView.SelectedCamera1808Present,
            currentCommon.CommonViewResource788Registered, currentCommon.CommonViewResource78CRegistered,
            currentCommon.CommonViewResource8B8Registered, provenance);
        var feature = previousOrderedFeature is null
            ? currentInnerModifierLighting is null
                ? MapSourceCreatedF103Feature.PrepareFirstKind1(camera, currentLighting, view)
                : MapSourceCreatedF103Feature.PrepareFirstKind1WithModifier(camera, currentLighting, currentInnerModifierLighting, view)
            : currentInnerModifierLighting is null
                ? MapSourceCreatedF103Feature.PrepareNextKind1(previousOrderedFeature, camera, currentLighting, view)
                : MapSourceCreatedF103Feature.PrepareNextKind1WithModifier(previousOrderedFeature, camera, currentLighting, currentInnerModifierLighting, view);
        var selected = MapSourceCreatedShaderSelection.PrepareFreshShader(plan, feature, currentLighting, variant);
        return new(camera, currentLighting, currentPublishedView, currentCommon, currentContext, color, view,
            variant, feature, selected, reflectionScope, actualReflectionQueryD57E20, sameCurrentCpuPopulation);
    }

    /// <summary>The Environment-published seasonal queue retains a separate
    /// first counter per record owner. No single-HDRI mask or capacity supplies
    /// this join. Current lighting is the independently selected complete
    /// payload, while every world record supplies its own classification.</summary>
    public MapSourceCreatedF103CurrentFrame PrepareFromWorldPopulation(
        MapStaticModelCurrentOwnedFrame currentCpuPopulation,
        MapWorldKind1CurrentQueue currentWorldQueue,
        MapSourceCreatedLightingPacket currentSelectedLighting,
        MapFreshCommonResourceSnapshot currentCommon, MainMapContextCurrentPacket currentContext,
        MapSourceCreatedF103PopulationPublicationInputs publication,
        IReadOnlyDictionary<string, int>? completeOtherCountsByRecordOwner,
        int? actualReflectionQueryD57E20, string currentPreparationIdentity,
        MapSourceCreatedLightingPacket? currentInnerModifierLighting = null)
    {
        ArgumentNullException.ThrowIfNull(currentCpuPopulation); ArgumentNullException.ThrowIfNull(currentWorldQueue);
        ArgumentNullException.ThrowIfNull(currentSelectedLighting); ArgumentNullException.ThrowIfNull(currentCommon);
        ArgumentNullException.ThrowIfNull(publication);
        if (currentWorldQueue.FrameNumber != currentCpuPopulation.FrameNumber ||
            currentWorldQueue.Binding != currentSelectedLighting.Binding ||
            publication.OtherCurrentOrdinaryPrimitiveCounts is not null ||
            completeOtherCountsByRecordOwner is null && publication.OtherOrdinaryPublishersAbsent != true ||
            completeOtherCountsByRecordOwner is not null && publication.OtherOrdinaryPublishersAbsent != false)
            throw new InvalidDataException("Seasonal world F103 needs its actual selected lighting and record-owner keyed complete current counts.");
        var draws = currentCpuPopulation.Visibility.Visible.SelectMany(instance => instance.Asset.Draws.Select(draw =>
            new MapSourceCreatedPreparedModelPrimitive(currentCpuPopulation.FrameNumber,
                currentWorldQueue.Binding.ViewIdentity, instance.InstanceKey, draw.Index,
                publication.CpuPublicationIdentity + ";ordinary-geometry-from-current-owned-source"))).ToArray();
        var counts = MapSourceCreatedWorldKind1Publication.PrepareFreshModelPopulationCounts(
            currentCpuPopulation, currentWorldQueue, publication.FreshWorldPqOrdinaryMaskFields328And334Retained,
            currentCommon.CurrentAuxiliaryExclusion404, publication.CurrentExclusionOverrides, draws,
            publication.ActualGeometryInputFallbackOwnsSource, publication.CpuPublicationIdentity);
        if (completeOtherCountsByRecordOwner is not null)
            counts = MapSourceCreatedWorldKind1Publication.MergeCompleteOtherCounts(counts,
                currentCpuPopulation.FrameNumber, currentWorldQueue.Binding.ViewIdentity,
                completeOtherCountsByRecordOwner, publication.CpuPublicationIdentity + ";other-current-publishers");
        var camera = currentCpuPopulation.MainCamera.Camera;
        var fields = MapSourceCreatedWorldKind1Publication.PrepareAcceptedViewFields(camera,
            currentSelectedLighting, MapSourceCreatedViewFields.ReadFreshRetainedCameraFields(camera),
            counts, publication.CurrentViewPublication);
        return Prepare(camera, currentSelectedLighting, fields, currentCommon, currentContext,
            actualReflectionQueryD57E20, currentPreparationIdentity, currentCpuPopulation, currentInnerModifierLighting);
    }
}
