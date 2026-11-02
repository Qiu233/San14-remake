using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>The imported Directional declaration used by one world
/// record. This does not select a View, run a provider or construct LightData.</summary>
public sealed class MapWorldKind1SourceMetadata
{
    internal MapWorldKind1SourceMetadata(MapSeasonDirectionalLightSourceSet source,
        uint objectId, uint mask, int capacity, byte classification)
    {
        SourceObjectId = objectId; DeclaredGroupMask = mask;
        MaxPrimitiveNum = capacity; SourceLightDataF20 = classification;
        SourceDeclarationIdentity = $"{source.Source.ResourceUri}#{objectId:X8}@{source.Source.DecodedSha256}";
    }
    public uint SourceObjectId { get; }
    public uint DeclaredGroupMask { get; }
    public int MaxPrimitiveNum { get; }
    public byte SourceLightDataF20 { get; }
    public string SourceDeclarationIdentity { get; }
    public uint CapacityPropertyHash => 0xDB26951D;
    public uint GroupPropertyHash => 0xC0C5A5B5;
    public uint ClassificationPropertyHash => 0x8A44928D;
}

/// <summary>Actual publication inputs from one constructed Directional owner.
/// Null retention inputs are unknown, never an implicit source/default value.
/// Only the explicitly unchanged source-group/capacity/F20 branch is supported.</summary>
public sealed record MapWorldKind1CurrentRecordInput(long FrameNumber, string ViewIdentity, MapWorldKind1SourceMetadata Source,
    int CurrentQueueIndex, string RecordOwnerIdentity, uint CurrentMask78, int CurrentFirstCounterCapacity,
    byte? CurrentLightDataF20, bool? SourceGroupRetained, bool? SourceMaxPrimitiveNumRetained,
    bool? SourceClassificationRetained, string CurrentProducerProvenance,DirectionalLightConstructionPacket? SelectedActor=null);

public sealed class MapWorldKind1CurrentRecord
{
    internal MapWorldKind1CurrentRecord(MapWorldKind1CurrentRecordInput input)
    {
        Source = input.Source;SelectedActor=input.SelectedActor; CurrentQueueIndex = input.CurrentQueueIndex; RecordOwnerIdentity = input.RecordOwnerIdentity;
        Mask78 = input.CurrentMask78; FirstCounterCapacity = input.CurrentFirstCounterCapacity;
        LightDataF20 = input.CurrentLightDataF20!.Value; ProducerProvenance = input.CurrentProducerProvenance;
    }
    public MapWorldKind1SourceMetadata Source { get; }
    public DirectionalLightConstructionPacket? SelectedActor {get;}
    public int CurrentQueueIndex { get; }
    public string RecordOwnerIdentity { get; }
    public uint Mask78 { get; }
    public int FirstCounterCapacity { get; }
    public byte LightDataF20 { get; }
    public string ProducerProvenance { get; }
}

/// <summary>Complete ordered current seasonal world kind1 queue publication.
/// Record identity is separate from mask, so equal masks never merge counters.</summary>
public sealed class MapWorldKind1CurrentQueue
{
    private readonly MapWorldKind1CurrentRecord[] records;
    internal MapWorldKind1CurrentQueue(long frame, MapLightingBinding binding,
        string queueOwner, MapWorldKind1CurrentRecord[] records)
    { FrameNumber = frame; Binding = binding; QueueOwnerIdentity = queueOwner; this.records = records; }
    public long FrameNumber { get; }
    public MapLightingBinding Binding { get; }
    public string QueueOwnerIdentity { get; }
    public IReadOnlyList<MapWorldKind1CurrentRecord> Records => Array.AsReadOnly(records.ToArray());
    internal void ValidateSources()
    {
        foreach (var record in records)
            if (record.Mask78 != (record.SelectedActor?.Metadata.SourceGroupMask ?? record.Source.DeclaredGroupMask) ||
                record.FirstCounterCapacity != (record.SelectedActor is { } actor?checked((int)actor.Metadata.MaximumPrimitiveCount):record.Source.MaxPrimitiveNum) ||
                record.LightDataF20 != (record.SelectedActor?.Metadata.SourceF20 ?? record.Source.SourceLightDataF20))
                throw new InvalidDataException("Current world record differs from its retained source group/capacity/classification.");
    }
}

public sealed class MapWorldKind1RecordCounts
{
    private readonly IReadOnlyDictionary<string, int> counts;
    internal MapWorldKind1RecordCounts(MapWorldKind1CurrentQueue queue, string provenance,
        IReadOnlyDictionary<string, int> values)
    { Queue = queue; PublicationIdentity = provenance; counts = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(values, StringComparer.Ordinal)); }
    public MapWorldKind1CurrentQueue Queue { get; }
    public string PublicationIdentity { get; }
    public IReadOnlyDictionary<string, int> CompleteCurrentCpuCounts => counts;
}

/// <summary>Actual world record → primitive first counters → View19AC.
/// The legacy single-HDRI finite path remains in OriginalMapSourceCreatedViewFields.</summary>
public static class MapSourceCreatedWorldKind1Publication
{
    public const string ContractId = "ce-season-world-kind1-publication-20261003";
    private static readonly ConditionalWeakTable<MapSeasonDirectionalLightSourceSet,
        IReadOnlyList<MapWorldKind1SourceMetadata>> loadedMetadata = new();

    public static IReadOnlyList<MapWorldKind1SourceMetadata> ReadSeasonSourceRecords(
        MapSeasonDirectionalLightSourceSet declaredSource)
    {
        ArgumentNullException.ThrowIfNull(declaredSource);
        return loadedMetadata.GetValue(declaredSource, ReadSourceMetadata);
    }

    private static IReadOnlyList<MapWorldKind1SourceMetadata> ReadSourceMetadata(
        MapSeasonDirectionalLightSourceSet declaredSource)
    {
        var source = declaredSource;
        return Array.AsReadOnly(source.Roots.Select(provider =>
        {
            var settings = source.GetSettings(provider.Root.ObjectId);
            uint capacity = settings.MaximumPrimitiveCount;
            uint group = settings.SourceGroupMask;
            byte classification = settings.Classification;
            if (capacity > int.MaxValue)
                throw new NotSupportedException("Directional first-counter capacity exceeds the finite nonnegative signed range.");
            // Retaining the root's classification through the bounded provider
            // requires every possible declared child to retain the same byte.
            foreach (var child in provider.Children)
            {
                if (source.GetSettings(child.ObjectId).Classification != classification)
                    throw new NotSupportedException("Directional provider classification differs between children; its exact current selection producer is required.");
            }
            return new MapWorldKind1SourceMetadata(source, provider.Root.ObjectId, group, (int)capacity, classification);
        }).ToArray());
    }

    public static MapWorldKind1CurrentQueue PrepareSourceRetainedQueue(long frameNumber,
        MapLightingBinding currentBinding, string currentQueueOwnerIdentity,
        MapSeasonDirectionalLightSourceSet source,
        IReadOnlyList<MapWorldKind1CurrentRecordInput> completeCurrentRecords)
    {
        ArgumentNullException.ThrowIfNull(currentBinding); ArgumentNullException.ThrowIfNull(completeCurrentRecords);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentQueueOwnerIdentity);
        var canonical = ReadSeasonSourceRecords(source);
        if (frameNumber <= 0 || new[] { currentBinding.ViewIdentity, currentBinding.CameraOwnerIdentity,
            currentBinding.LightOwnerIdentity }.Any(string.IsNullOrWhiteSpace) || canonical.Count != completeCurrentRecords.Count ||
            completeCurrentRecords.Any(item => item is null || item.Source is null) ||
            !canonical.Select(item => item.SourceObjectId).SequenceEqual(completeCurrentRecords.Select(item => item.Source.SourceObjectId)) ||
            completeCurrentRecords.Select(item => item.RecordOwnerIdentity).Distinct(StringComparer.Ordinal).Count() != completeCurrentRecords.Count)
            throw new InvalidDataException("Current world kind1 publication requires every ordered seasonal source owner in one known frame/View queue.");
        for (int i = 0; i < completeCurrentRecords.Count; i++)
        {
            var input = completeCurrentRecords[i]; var actual = canonical[i];
            var actor=input.SelectedActor;
            if(actor is not null&&(actor.SourceUri!=source.Source.ResourceUri||actor.SourceSha256!=source.Source.DecodedSha256||
                actor.SourceObjectId!=actual.SourceObjectId&&!source.Roots[i].Children.Any(child=>child.ObjectId==actor.SourceObjectId)))
                throw new InvalidDataException("Current record selected actor is outside its registered source family.");
            uint mask=actor?.Metadata.SourceGroupMask??actual.DeclaredGroupMask;
            int capacity=actor is null?actual.MaxPrimitiveNum:checked((int)actor.Metadata.MaximumPrimitiveCount);
            byte classification=actor?.Metadata.SourceF20??actual.SourceLightDataF20;
            if (input.FrameNumber != frameNumber || input.ViewIdentity != currentBinding.ViewIdentity ||
                input.Source.SourceDeclarationIdentity != actual.SourceDeclarationIdentity ||
                input.CurrentQueueIndex < 0 || (i > 0 && input.CurrentQueueIndex <= completeCurrentRecords[i - 1].CurrentQueueIndex) ||
                string.IsNullOrWhiteSpace(input.RecordOwnerIdentity) || string.IsNullOrWhiteSpace(input.CurrentProducerProvenance) ||
                input.SourceGroupRetained != (mask==actual.DeclaredGroupMask) || input.SourceMaxPrimitiveNumRetained != (capacity==actual.MaxPrimitiveNum) ||
                input.SourceClassificationRetained != (classification==actual.SourceLightDataF20) ||
                input.CurrentMask78 != mask || input.CurrentFirstCounterCapacity != capacity ||
                input.CurrentLightDataF20 != classification)
                throw new InvalidDataException("World kind1 record requires exact current owner group/capacity/classification retention; unknown overrides cannot use the declaration as runtime state.");
        }
        return new(frameNumber, currentBinding, currentQueueOwnerIdentity,
            completeCurrentRecords.Select(input => new MapWorldKind1CurrentRecord(input)).ToArray());
    }

    public static MapWorldKind1RecordCounts PrepareModelPopulationCounts(
        MapStaticModelCurrentOwnedFrame sourceFrame, MapWorldKind1CurrentQueue queue,
        int currentSelectedViewIndex, uint? currentOrdinaryViewMask, uint? currentCommonAuxExclusion404,
        IReadOnlyDictionary<string, uint> completeCurrentModelExclusions78,
        IReadOnlyList<MapSourceCreatedPreparedModelPrimitive> completeCurrentPreparedDraws,
        string publicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame); ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(completeCurrentModelExclusions78); ArgumentNullException.ThrowIfNull(completeCurrentPreparedDraws);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicationIdentity); queue.ValidateSources();
        if (!sourceFrame.Population.Complete || sourceFrame.FrameNumber != queue.FrameNumber || currentSelectedViewIndex is < 0 or >= 32 ||
            currentOrdinaryViewMask is not uint viewMask || currentCommonAuxExclusion404 is not uint commonExclude)
            throw new InvalidDataException("World first counters require the complete same-frame population and known current View mask/exclusions.");
        var visible = sourceFrame.Visibility.Visible;
        if (!visible.Select(item => item.InstanceKey).Order(StringComparer.Ordinal).SequenceEqual(completeCurrentModelExclusions78.Keys.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("World first counters require every visible source instance's current exclusion78.");
        var expected = visible.SelectMany(instance => instance.Asset.Draws.Select(draw => (instance.InstanceKey, draw.Index))).ToHashSet();
        var supplied = completeCurrentPreparedDraws.Select(draw => (draw.InstanceKey, draw.SourceDrawIndex)).ToArray();
        if (supplied.Distinct().Count() != supplied.Length || !expected.SetEquals(supplied) ||
            completeCurrentPreparedDraws.Any(draw => draw.FrameNumber != queue.FrameNumber || draw.ViewIdentity != queue.Binding.ViewIdentity ||
                string.IsNullOrWhiteSpace(draw.CurrentCpuPreparationProvenance)))
            throw new InvalidDataException("World counters require every visible source draw exactly once from this current CPU preparation.");
        foreach (var asset in visible.Select(instance => instance.Asset).Distinct())
            MapSourceCreatedViewFields.ValidateCanonicalPreparedPrimitiveSource(asset);
        if (queue.Records.Any(record => (record.LightDataF20 & 1) != 0))
            throw new NotSupportedException("A world record's alternate LightDataF20 counter classification requires its exact current category producer.");
        var counts = queue.Records.ToDictionary(record => record.RecordOwnerIdentity, _ => 0, StringComparer.Ordinal);
        if ((viewMask & (1u << currentSelectedViewIndex)) != 0)
            foreach (var instance in visible)
            {
                uint exclusion = completeCurrentModelExclusions78[instance.InstanceKey] | commonExclude;
                foreach (var record in queue.Records)
                {
                    if ((record.Mask78 & exclusion) != 0 || instance.Asset.Draws.Count == 0) continue;
                    int next = checked(counts[record.RecordOwnerIdentity] + instance.Asset.Draws.Count);
                    if (next >= record.FirstCounterCapacity) // BB8680 JGE, equality also refuses.
                        throw new InvalidDataException("A current world record's exact first-counter capacity refuses the complete source primitive publication.");
                    counts[record.RecordOwnerIdentity] = next;
                }
            }
        return new(queue, publicationIdentity + ";" + ContractId + ";TC:C39475/C398E2/F587F0/BB8680;" +
            "independent-record-counters;sourceRevision=" + sourceFrame.SourceRevision, counts);
    }

    public static MapWorldKind1RecordCounts PrepareFreshModelPopulationCounts(
        MapStaticModelCurrentOwnedFrame sourceFrame, MapWorldKind1CurrentQueue queue,
        bool? freshWorldPqOrdinaryMaskFields328And334Retained, uint? currentCommonAuxExclusion404,
        IReadOnlyDictionary<string, MapSourceCreatedModelExclusionOverride> explicitCurrentExclusionOverrides,
        IReadOnlyList<MapSourceCreatedPreparedModelPrimitive> completeCurrentPreparedDraws,
        bool? actualGeometryInputFallbackOwnsSource, string publicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame); ArgumentNullException.ThrowIfNull(queue);
        if (sourceFrame.MainCamera.Camera.Source.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId ||
            freshWorldPqOrdinaryMaskFields328And334Retained != true || actualGeometryInputFallbackOwnsSource != true)
            throw new InvalidDataException("Fresh world counters require Main View0, retained WorldPQ ordinary fields and actual source geometry fallback.");
        var exclusions = MapSourceCreatedViewFields.ReadCurrentModelExclusions78(sourceFrame,
            queue.Binding.ViewIdentity, explicitCurrentExclusionOverrides);
        return PrepareModelPopulationCounts(sourceFrame, queue, 0, 1, currentCommonAuxExclusion404,
            exclusions, completeCurrentPreparedDraws, publicationIdentity + ";sourceMainViewSlot0;WorldPQ328/334-retained;geometry-fallback-owned");
    }

    /// <summary>Explicit complete other CPU publishers may merge only into
    /// this same queue generation, keyed by each record's owner identity.</summary>
    public static MapWorldKind1RecordCounts MergeCompleteOtherCounts(MapWorldKind1RecordCounts modelCounts,
        long currentFrameNumber, string currentViewIdentity, IReadOnlyDictionary<string, int> completeOtherCpuCounts,
        string currentOtherPublicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(modelCounts); ArgumentNullException.ThrowIfNull(completeOtherCpuCounts);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentOtherPublicationIdentity); modelCounts.Queue.ValidateSources();
        if (currentFrameNumber != modelCounts.Queue.FrameNumber || currentViewIdentity != modelCounts.Queue.Binding.ViewIdentity ||
            !modelCounts.CompleteCurrentCpuCounts.Keys.Order(StringComparer.Ordinal).SequenceEqual(completeOtherCpuCounts.Keys.Order(StringComparer.Ordinal)) ||
            completeOtherCpuCounts.Values.Any(value => value < 0))
            throw new InvalidDataException("Other world CPU counters require the complete current queue record identities and nonnegative counts.");
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var record in modelCounts.Queue.Records)
        {
            int count = checked(modelCounts.CompleteCurrentCpuCounts[record.RecordOwnerIdentity] + completeOtherCpuCounts[record.RecordOwnerIdentity]);
            if (count > 0 && count >= record.FirstCounterCapacity)
                throw new InvalidDataException("The complete other CPU publication exceeds this world's actual first-counter capacity.");
            values.Add(record.RecordOwnerIdentity, count);
        }
        return new(modelCounts.Queue, modelCounts.PublicationIdentity + ";other=" + currentOtherPublicationIdentity, values);
    }

    public static MapSourceCreatedViewFieldsPacket PrepareAcceptedViewFields(
        MapCameraConstructionPacket acceptedCamera, MapSourceCreatedLightingPacket selectedLighting,
        MapSourceCreatedViewCameraFields retainedSourceCamera, MapWorldKind1RecordCounts currentCounts,
        MapSourceCreatedViewPublicationState current)
    {
        ArgumentNullException.ThrowIfNull(acceptedCamera); ArgumentNullException.ThrowIfNull(selectedLighting);
        ArgumentNullException.ThrowIfNull(retainedSourceCamera); ArgumentNullException.ThrowIfNull(currentCounts);
        ArgumentNullException.ThrowIfNull(current); retainedSourceCamera.Validate(acceptedCamera);
        var queue = currentCounts.Queue; queue.ValidateSources();
        if (queue.FrameNumber != retainedSourceCamera.FrameNumber || selectedLighting.FrameNumber != queue.FrameNumber ||
            current.FrameNumber != queue.FrameNumber || queue.Binding != selectedLighting.Binding ||
            current.ViewIdentity != queue.Binding.ViewIdentity || current.CameraOwnerIdentity != queue.Binding.CameraOwnerIdentity ||
            string.IsNullOrWhiteSpace(current.CurrentPublicationProvenance))
            throw new InvalidDataException("World View construction requires current Camera, queue, counts and selected lighting in the same frame/View generation.");
        if (current.AdditionalOwners1B80_1BB0_1BB8_1B98Absent != true && current.Terrain1BB8 is null)
            throw new NotSupportedException("World View additional owners need their actual typed current producers.");
        if (current.AdditionalOwners1B80_1BB0_1BB8_1B98Absent == true && current.Terrain1BB8 is not null)
            throw new InvalidDataException("Current Landscape owner cannot also be absent.");
        if (current.ShadowPrimitiveCount1BFC < 0 || current.CollectionPrimitiveCounts2To5 is null ||
            current.CollectionPrimitiveCounts2To5.Count != 4 || current.CollectionPrimitiveCounts2To5.Any(value => value < 0))
            throw new InvalidDataException("World View requires complete nonnegative shadow and collection2..5 counts.");
        var masks = queue.Records.Select(record => record.Mask78).ToArray();
        uint terrainFlags = current.Terrain1BB8?.ValidateAndGetFlags(selectedLighting, current, masks) ?? 0;
        var records = queue.Records.Select(record => new MapSourceCreatedKind1ViewRecord(record.Mask78,
            currentCounts.CompleteCurrentCpuCounts[record.RecordOwnerIdentity], record.RecordOwnerIdentity,
            currentCounts.PublicationIdentity + ";source=" + record.Source.SourceDeclarationIdentity)).ToArray();
        uint flags = terrainFlags, oceanFlags = 0, rainFlags = 0;
        foreach (var record in records)
        {
            if (record.CurrentCpuPrimitiveCount != 0) flags |= record.Mask78;
            oceanFlags |= SelectedGroup(retainedSourceCamera.StatisticalOceanGroupIdAC0) & record.Mask78;
            rainFlags |= SelectedGroup(retainedSourceCamera.RainDropGroupId12A8) & record.Mask78;
        }
        flags |= oceanFlags | rainFlags;
        if (current.ShadowPrimitiveCount1BFC != 0 || current.CollectionPrimitiveCounts2To5.Any(value => value != 0)) flags |= 0x40000000;
        ulong? vtable = null;
        // This selected record's F1D is only the View shadow gate; per-record
        // classification above always comes from each individual source owner.
        if (retainedSourceCamera.ViewSignedA00 > 0 && (flags & 0x40000000) != 0 && selectedLighting.LocalLightData.Span[0xF1D] != 0)
        {
            if (current.SelectedShadowWrapper1BD0Present is not bool present)
                throw new InvalidDataException("Active world View shadows require the actual current selected wrapper branch.");
            if (!present) vtable = 0;
            else if (current.SelectedShadowCpuVtable is not ulong actual || actual == 0)
                throw new InvalidDataException("Present world View shadow wrapper requires its actual current CPU vtable.");
            else vtable = actual;
        }
        return new(retainedSourceCamera, queue.Binding, current, records, flags, oceanFlags, rainFlags, vtable,
            retainedSourceCamera.SourceProvenance + ";" + ContractId + ";queue=" + queue.QueueOwnerIdentity +
            ";counts=" + currentCounts.PublicationIdentity + ";View19AC=TC:D7D9D0/D7DF0F;" + current.CurrentPublicationProvenance, queue);
    }

    private static uint SelectedGroup(uint group) => group is >= 1 and <= 7 ? 1u << ((int)group - 1) : 0;
}
