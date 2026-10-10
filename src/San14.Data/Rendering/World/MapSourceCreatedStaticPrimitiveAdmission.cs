namespace San14.Data;

/// <summary>One current ordinary primitive and the actual kind1 records into
/// which its source exclusion predicate admits it. An empty record list keeps
/// the primitive in the population; it does not invent another queue route.</summary>
public sealed class MapSourceCreatedStaticPrimitiveAdmission
{
    private readonly uint[] records;
    private readonly string[] worldRecordOwners;
    internal MapSourceCreatedStaticPrimitiveAdmission(string instanceKey, string sourceIdentity,
        int drawIndex, uint modelExclusion, uint commonExclusion, IEnumerable<uint> admittedRecords,
        IEnumerable<string>? admittedWorldRecordOwners = null)
    {
        InstanceKey = instanceKey; CurrentSourceIdentity = sourceIdentity; SourceDrawIndex = drawIndex;
        ModelExclusion78 = modelExclusion; CommonExclusion404 = commonExclusion;
        records = admittedRecords.ToArray();
        worldRecordOwners = admittedWorldRecordOwners?.ToArray() ?? [];
    }
    public string InstanceKey { get; }
    public string CurrentSourceIdentity { get; }
    public int SourceDrawIndex { get; }
    public uint ModelExclusion78 { get; }
    public uint CommonExclusion404 { get; }
    public uint AllowedSourceChannels => ~(ModelExclusion78 | CommonExclusion404);
    public IReadOnlyList<uint> AdmittedKind1RecordMasks => Array.AsReadOnly(records.ToArray());
    public bool HasKind1Record => records.Length != 0;
    public bool IsAdmittedTo(uint actualRecordMask78) => records.Contains(actualRecordMask78);
    public IReadOnlyList<string> AdmittedWorldKind1RecordOwnerIdentities => Array.AsReadOnly(worldRecordOwners.ToArray());
    public bool IsAdmittedToWorldRecord(string recordOwnerIdentity) => worldRecordOwners.Contains(recordOwnerIdentity, StringComparer.Ordinal);
}

/// <summary>Same-frame membership for every visible static source draw.
/// The actual View publication has already validated the original counters.
/// This plan supplies per-primitive identities without replacing the full
/// population, choosing a different light record, or claiming GPU execution.</summary>
public sealed class MapSourceCreatedStaticPrimitiveAdmissionPlan
{
    private readonly Dictionary<(string Instance, int Draw), MapSourceCreatedStaticPrimitiveAdmission> entries;
    private readonly Dictionary<uint, int> counts;
    private readonly Dictionary<string, int> worldCounts;
    private readonly bool ambiguousMaskCounts;
    private MapSourceCreatedStaticPrimitiveAdmissionPlan(MapSourceCreatedF103CurrentFrame frame,
        MapStaticModelCurrentOwnedFrame population, string provenance,
        Dictionary<(string Instance, int Draw), MapSourceCreatedStaticPrimitiveAdmission> primitives,
        Dictionary<uint, int> recordCounts, Dictionary<string, int>? worldRecordCounts = null,
        MapWorldKind1CurrentQueue? currentWorldQueue = null)
    {
        Frame = frame; CpuPopulation = population; SourceProvenance = provenance;
        entries = primitives; counts = recordCounts;
        worldCounts = worldRecordCounts is null ? new(StringComparer.Ordinal) : new(worldRecordCounts, StringComparer.Ordinal);
        CurrentWorldQueue = currentWorldQueue;
        ambiguousMaskCounts = currentWorldQueue is not null && currentWorldQueue.Records.Select(record => record.Mask78).Distinct().Count() != currentWorldQueue.Records.Count;
    }
    public MapSourceCreatedF103CurrentFrame Frame { get; }
    public MapStaticModelCurrentOwnedFrame CpuPopulation { get; }
    public long FrameNumber => Frame.FrameNumber;
    public string ViewIdentity => Frame.Lighting.Binding.ViewIdentity;
    public string SourceProvenance { get; }
    public MapWorldKind1CurrentQueue? CurrentWorldQueue { get; }
    public IReadOnlyList<MapSourceCreatedStaticPrimitiveAdmission> Primitives =>
        Array.AsReadOnly(entries.Values.ToArray());
    public IReadOnlyDictionary<uint, int> StaticKind1RecordCounts => ambiguousMaskCounts
        ? throw new NotSupportedException("The actual world queue has repeated masks; use record-owner keyed StaticWorldKind1RecordCounts rather than merging independent counters.")
        : new System.Collections.ObjectModel.ReadOnlyDictionary<uint, int>(new Dictionary<uint, int>(counts));
    public IReadOnlyDictionary<string, int> StaticWorldKind1RecordCounts =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(new Dictionary<string, int>(worldCounts, StringComparer.Ordinal));
    public int UnassignedKind1PrimitiveCount => entries.Values.Count(value => !value.HasKind1Record);

    public MapSourceCreatedStaticPrimitiveAdmission Require(string instanceKey, int sourceDrawIndex)
    {
        if (!entries.TryGetValue((instanceKey, sourceDrawIndex), out var found))
            throw new InvalidDataException("The current static admission plan has no such prepared source primitive.");
        return found;
    }

    /// <summary>Use the exact population borrowed by current F103 preparation.
    /// Current Common exclusions and explicit WorldPQ setter overrides are
    /// checked again against that publication. Unknown retention/fallback or
    /// additional ordinary publishers never become default membership.</summary>
    public static MapSourceCreatedStaticPrimitiveAdmissionPlan PrepareFresh(
        MapSourceCreatedF103CurrentFrame currentFrame,
        MapSourceCreatedF103PopulationPublicationInputs actualPublication)
    {
        ArgumentNullException.ThrowIfNull(currentFrame); ArgumentNullException.ThrowIfNull(actualPublication);
        var population = currentFrame.CpuPopulation ??
            throw new InvalidDataException("Static admission requires the exact CPU population retained by current F103 preparation.");
        if (!population.Population.Complete || population.FrameNumber != currentFrame.FrameNumber ||
            actualPublication.FreshWorldPqOrdinaryMaskFields328And334Retained != true ||
            actualPublication.ActualGeometryInputFallbackOwnsSource != true ||
            !SamePublication(actualPublication.CurrentViewPublication, currentFrame.ViewFields.CurrentPublication) ||
            currentFrame.Common.ModelExclusionMask is not uint commonExclusion ||
            currentFrame.Common.ViewGroupSelector is not byte selector ||
            string.IsNullOrWhiteSpace(actualPublication.CpuPublicationIdentity))
            throw new InvalidDataException("Static admission requires this complete current Main publication, retained ordinary mask and source geometry fallback.");
        var recordMasks = MapSourceCreatedViewFields.SourceKind1RecordMasks(currentFrame.Lighting, selector);
        var exclusions = MapSourceCreatedViewFields.ReadCurrentModelExclusions78(population,
            currentFrame.Lighting.Binding.ViewIdentity, actualPublication.CurrentExclusionOverrides);
        var primitives = new Dictionary<(string Instance, int Draw), MapSourceCreatedStaticPrimitiveAdmission>();
        var staticCounts = recordMasks.ToDictionary(mask => mask, _ => 0);
        foreach (var instance in population.Visibility.Visible)
        {
            MapSourceCreatedViewFields.ValidateCanonicalPreparedPrimitiveSource(instance.Asset);
            string identity = instance.CurrentSourceIdentity ??
                throw new InvalidDataException("Current source primitive identity is unresolved.");
            uint modelExclusion = exclusions[instance.InstanceKey];
            uint combined = modelExclusion | commonExclusion;
            uint[] admitted = recordMasks.Where(mask => (mask & combined) == 0).ToArray();
            foreach (var draw in instance.Asset.Draws)
            {
                primitives.Add((instance.InstanceKey, draw.Index), new(instance.InstanceKey, identity,
                    draw.Index, modelExclusion, commonExclusion, admitted));
                foreach (uint mask in admitted) staticCounts[mask] = checked(staticCounts[mask] + 1);
            }
        }
        bool absent = actualPublication.OtherOrdinaryPublishersAbsent == true;
        var other = actualPublication.OtherCurrentOrdinaryPrimitiveCounts;
        if (absent && other is not null || !absent && (actualPublication.OtherOrdinaryPublishersAbsent != false || other is null))
            throw new InvalidDataException("Additional ordinary publishers require the same complete explicit current count publication.");
        var additional = other?.CompleteCurrentCpuCounts;
        if (other is not null && (other.FrameNumber != currentFrame.FrameNumber || other.ViewIdentity != currentFrame.Lighting.Binding.ViewIdentity ||
            !recordMasks.Order().SequenceEqual(additional!.Keys.Order())))
            throw new InvalidDataException("Additional primitive publication belongs to a different View record sequence or frame.");
        var actualRecords = currentFrame.ViewFields.Kind1Records;
        if (!recordMasks.Order().SequenceEqual(actualRecords.Select(record => record.Mask78).Order()) ||
            actualRecords.Any(record => record.CurrentCpuPrimitiveCount != checked(staticCounts[record.Mask78] + (additional?[record.Mask78] ?? 0))))
            throw new InvalidDataException("Per-primitive static membership differs from the accepted current View counters.");
        return new(currentFrame, population, actualPublication.CpuPublicationIdentity +
            ";exact-current-static-draw-membership;WorldPQ328/334-retained/View0-mask1;" +
            "TC:C39475/C398E2/F587F0/BB8680;unmatched-channels-retained-without-invented-route", primitives, staticCounts);
    }

    /// <summary>Actual seasonal world queue membership. Each original source
    /// draw is tested against each independent record owner's mask78; no legacy
    /// HDRI splitting or mask-keyed count merge supplies this publication.</summary>
    public static MapSourceCreatedStaticPrimitiveAdmissionPlan PrepareFreshWorld(
        MapSourceCreatedF103CurrentFrame currentFrame,
        MapSourceCreatedF103PopulationPublicationInputs actualPublication,
        MapWorldKind1CurrentQueue currentWorldQueue,
        IReadOnlyDictionary<string, int>? completeOtherCountsByRecordOwner = null)
    {
        ArgumentNullException.ThrowIfNull(currentFrame); ArgumentNullException.ThrowIfNull(actualPublication);
        ArgumentNullException.ThrowIfNull(currentWorldQueue); currentWorldQueue.ValidateSources();
        var population = currentFrame.CpuPopulation ??
            throw new InvalidDataException("World static admission requires the exact retained current F103 CPU population.");
        if (!population.Population.Complete || population.FrameNumber != currentFrame.FrameNumber ||
            currentWorldQueue.FrameNumber != currentFrame.FrameNumber || currentWorldQueue.Binding != currentFrame.Lighting.Binding ||
            !ReferenceEquals(currentWorldQueue, currentFrame.ViewFields.CurrentWorldQueue) ||
            actualPublication.FreshWorldPqOrdinaryMaskFields328And334Retained != true ||
            actualPublication.ActualGeometryInputFallbackOwnsSource != true ||
            !SamePublication(actualPublication.CurrentViewPublication, currentFrame.ViewFields.CurrentPublication) ||
            currentFrame.Common.ModelExclusionMask is not uint commonExclusion ||
            string.IsNullOrWhiteSpace(actualPublication.CpuPublicationIdentity) ||
            actualPublication.OtherCurrentOrdinaryPrimitiveCounts is not null ||
            (completeOtherCountsByRecordOwner is null ? actualPublication.OtherOrdinaryPublishersAbsent != true :
                actualPublication.OtherOrdinaryPublishersAbsent != false))
            throw new InvalidDataException("World static admission requires this exact frame/View queue, known ordinary fields/geometry fallback and complete record-owner keyed other publication.");

        var preparedDraws = population.Visibility.Visible.SelectMany(instance => instance.Asset.Draws.Select(draw =>
            new MapSourceCreatedPreparedModelPrimitive(currentFrame.FrameNumber, currentWorldQueue.Binding.ViewIdentity,
                instance.InstanceKey, draw.Index, actualPublication.CpuPublicationIdentity + ";current-world-static-admission"))).ToArray();
        var sourceCounts = MapSourceCreatedWorldKind1Publication.PrepareFreshModelPopulationCounts(population,
            currentWorldQueue, true, commonExclusion, actualPublication.CurrentExclusionOverrides, preparedDraws,
            true, actualPublication.CpuPublicationIdentity);
        var totalCounts = completeOtherCountsByRecordOwner is null ? sourceCounts :
            MapSourceCreatedWorldKind1Publication.MergeCompleteOtherCounts(sourceCounts, currentFrame.FrameNumber,
                currentWorldQueue.Binding.ViewIdentity, completeOtherCountsByRecordOwner, actualPublication.CpuPublicationIdentity + ";other-current-CPU");
        var queueRecords = currentWorldQueue.Records;
        var accepted = currentFrame.ViewFields.Kind1Records;
        if (accepted.Count != queueRecords.Count || accepted.Where((record, index) =>
            record.Mask78 != queueRecords[index].Mask78 || record.LightOwnerIdentity != queueRecords[index].RecordOwnerIdentity ||
            record.CurrentCpuPrimitiveCount != totalCounts.CompleteCurrentCpuCounts[queueRecords[index].RecordOwnerIdentity]).Any())
            throw new InvalidDataException("World static membership differs from the ordered accepted View record owners/masks/counters.");

        var exclusions = MapSourceCreatedViewFields.ReadCurrentModelExclusions78(population,
            currentWorldQueue.Binding.ViewIdentity, actualPublication.CurrentExclusionOverrides);
        var primitives = new Dictionary<(string Instance, int Draw), MapSourceCreatedStaticPrimitiveAdmission>();
        var perOwner = queueRecords.ToDictionary(record => record.RecordOwnerIdentity, _ => 0, StringComparer.Ordinal);
        foreach (var instance in population.Visibility.Visible)
        {
            string identity = instance.CurrentSourceIdentity ??
                throw new InvalidDataException("World static primitive source identity is unresolved.");
            uint excluded = exclusions[instance.InstanceKey];
            var admitted = queueRecords.Where(record => (record.Mask78 & (excluded | commonExclusion)) == 0).ToArray();
            foreach (var draw in instance.Asset.Draws)
            {
                primitives.Add((instance.InstanceKey, draw.Index), new(instance.InstanceKey, identity, draw.Index,
                    excluded, commonExclusion, admitted.Select(record => record.Mask78), admitted.Select(record => record.RecordOwnerIdentity)));
                foreach (var record in admitted)
                    perOwner[record.RecordOwnerIdentity] = checked(perOwner[record.RecordOwnerIdentity] + 1);
            }
        }
        if (perOwner.Any(item => item.Value != sourceCounts.CompleteCurrentCpuCounts[item.Key]))
            throw new InvalidDataException("Per-draw world record membership differs from the canonical source first-counter publication.");
        // Existing callers may project actual distinct masks. Repeated masks
        // keep their independent owner counts and refuse the legacy projection.
        var distinctMasks = queueRecords.Select(record => record.Mask78).Distinct().Count() == queueRecords.Count;
        var perMask = distinctMasks ? queueRecords.ToDictionary(record => record.Mask78, record => perOwner[record.RecordOwnerIdentity]) : new Dictionary<uint, int>();
        return new(currentFrame, population, actualPublication.CpuPublicationIdentity +
            ";actual-world-record-owner-static-membership;TC:C39475/C398E2/F587F0/BB8680;queue=" + currentWorldQueue.QueueOwnerIdentity,
            primitives, perMask, perOwner, currentWorldQueue);
    }

    private static bool SamePublication(MapSourceCreatedViewPublicationState actual,
        MapSourceCreatedViewPublicationState accepted) => actual is not null &&
        actual.FrameNumber == accepted.FrameNumber && actual.ViewIdentity == accepted.ViewIdentity &&
        actual.CameraOwnerIdentity == accepted.CameraOwnerIdentity &&
        actual.CurrentPublicationProvenance == accepted.CurrentPublicationProvenance &&
        actual.AdditionalOwners1B80_1BB0_1BB8_1B98Absent == accepted.AdditionalOwners1B80_1BB0_1BB8_1B98Absent &&
        actual.ShadowPrimitiveCount1BFC == accepted.ShadowPrimitiveCount1BFC &&
        actual.CollectionPrimitiveCounts2To5 is not null &&
        actual.CollectionPrimitiveCounts2To5.SequenceEqual(accepted.CollectionPrimitiveCounts2To5) &&
        actual.SelectedShadowWrapper1BD0Present == accepted.SelectedShadowWrapper1BD0Present &&
        ReferenceEquals(actual.SelectedShadowRecord1BD0, accepted.SelectedShadowRecord1BD0) &&
        actual.SelectedShadowCpuVtable == accepted.SelectedShadowCpuVtable;
}
