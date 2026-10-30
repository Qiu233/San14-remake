using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Current CPU primitive publication counts for every source kind1
/// record. These counts describe queued CPU records in this frame; GPU draw
/// completion is not their producer.</summary>
public sealed class MapSourceCreatedViewRecordCounts
{
    private readonly IReadOnlyDictionary<uint, int> counts;
    public MapSourceCreatedViewRecordCounts(long frameNumber, string viewIdentity,
        string publicationIdentity, IReadOnlyDictionary<uint, int> completeCurrentCpuCounts)
    {
        ArgumentNullException.ThrowIfNull(completeCurrentCpuCounts);
        if (frameNumber <= 0 || string.IsNullOrWhiteSpace(viewIdentity) || string.IsNullOrWhiteSpace(publicationIdentity) ||
            completeCurrentCpuCounts.Any(item => item.Value < 0))
            throw new InvalidDataException("View record counts require a positive current frame, its owned View and nonnegative CPU publication counts.");
        FrameNumber = frameNumber; ViewIdentity = viewIdentity; PublicationIdentity = publicationIdentity;
        counts = completeCurrentCpuCounts.ToDictionary(item => item.Key, item => item.Value);
    }
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public string PublicationIdentity { get; }
    public IReadOnlyDictionary<uint, int> CompleteCurrentCpuCounts => counts.ToDictionary(item => item.Key, item => item.Value);
    internal IReadOnlyDictionary<uint, int> Counts => counts;

    /// <summary>Publish this frame's complete prepared source draw list into
    /// ordinary first counters. Each source group draw reference produces one
    /// native70-byte primitive. The current View mask and per-instance native
    /// exclusion fields are separate owner inputs, never inferred from a G1M
    /// group label or from completed GPU draws.</summary>
    public static MapSourceCreatedViewRecordCounts FromCurrentOwnedPreparedModelDraws(
        MapStaticModelCurrentOwnedFrame sourceFrame, MapSourceCreatedLightingPacket lighting,
        byte currentViewAux3F2, int currentSelectedViewIndex, uint? currentOrdinaryViewMask,
        uint? currentCommonAuxExclusion404, IReadOnlyDictionary<string, uint> completeCurrentModelExclusions78,
        IReadOnlyList<MapSourceCreatedPreparedModelPrimitive> completeCurrentPreparedDraws,
        IReadOnlyDictionary<uint, int> completeCurrentCounterCapacities, string publicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(completeCurrentModelExclusions78); ArgumentNullException.ThrowIfNull(completeCurrentPreparedDraws);
        ArgumentNullException.ThrowIfNull(completeCurrentCounterCapacities);
        if (sourceFrame.FrameNumber != lighting.FrameNumber || currentSelectedViewIndex is < 0 or >= 32 ||
            currentOrdinaryViewMask is not uint viewMask || currentCommonAuxExclusion404 is not uint commonExclude ||
            string.IsNullOrWhiteSpace(publicationIdentity))
            throw new InvalidDataException("Prepared primitive publication requires the same current CPU frame and resolved View mask/exclusion owner inputs.");
        var visible = sourceFrame.Visibility.Visible;
        if (!visible.Select(item => item.InstanceKey).Order(StringComparer.Ordinal).SequenceEqual(completeCurrentModelExclusions78.Keys.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Primitive publication requires the current native exclusion78 field for every visible source instance.");
        var expected = visible.SelectMany(instance => instance.Asset.Draws.Select(draw => (instance.InstanceKey, draw.Index))).ToHashSet();
        var supplied = completeCurrentPreparedDraws.Select(draw => (draw.InstanceKey, draw.SourceDrawIndex)).ToArray();
        if (supplied.Distinct().Count() != supplied.Length || !expected.SetEquals(supplied) ||
            completeCurrentPreparedDraws.Any(draw => draw.FrameNumber != sourceFrame.FrameNumber ||
                draw.ViewIdentity != lighting.Binding.ViewIdentity || string.IsNullOrWhiteSpace(draw.CurrentCpuPreparationProvenance)))
            throw new InvalidDataException("Primitive publication requires each visible instance/source DrawIndex exactly once from this CPU preparation frame.");
        foreach (var asset in visible.Select(instance => instance.Asset).Distinct())
            MapSourceCreatedViewFields.ValidateCanonicalPreparedPrimitiveSource(asset);
        uint[] masks = MapSourceCreatedViewFields.SourceKind1RecordMasks(lighting, currentViewAux3F2).ToArray();
        if (!masks.Order().SequenceEqual(completeCurrentCounterCapacities.Keys.Order()) || completeCurrentCounterCapacities.Any(item => item.Value < 0))
            throw new InvalidDataException("Primitive publication requires the actual nonnegative capacity of each exact source kind1 first counter.");
        if ((lighting.LocalLightData.Span[0xF20] & 1) != 0)
            throw new NotSupportedException("The alternate native LightDataF20 counter classification requires its separate current source producer.");
        bool ordinary = (viewMask & (1u << currentSelectedViewIndex)) != 0;
        var result = masks.ToDictionary(mask => mask, _ => 0);
        if (ordinary)
        {
            foreach (var instance in visible)
            {
                uint exclusion = completeCurrentModelExclusions78[instance.InstanceKey] | commonExclude;
                int primitiveCount = instance.Asset.Draws.Count;
                foreach (uint mask in masks)
                {
                    if ((mask & exclusion) != 0) continue;
                    int next = checked(result[mask] + primitiveCount);
                    // BB8680 compares requested total against capacity with
                    // JGE. Equality is an unsuccessful native append too.
                    if (next >= completeCurrentCounterCapacities[mask])
                        throw new InvalidDataException("Current native first-counter capacity cannot publish this complete source primitive list.");
                    result[mask] = next;
                }
            }
        }
        return new(sourceFrame.FrameNumber, lighting.Binding.ViewIdentity,
            publicationIdentity + $";sourceRevision={sourceFrame.SourceRevision};" +
            "current-owned-world="+sourceFrame.CurrentOwnedWorldProvenance+";"+
            "one-primitive-per-draw=TC:F57CDD/F581B0/F5820E/F58213;ordinary-part=TC:ABB360/F587F0/C240C0;current-mask=TC:DB86A7/C39475;" +
            "current-exclusion=TC:C398E2;counter-append=TC:BB8680", result);
    }

    /// <summary>Decode the exact first16-byte native counter of each current
    /// kind1 record. Its atomic C word can use the negative native encoding;
    /// an absent or truncated counter is not an empty publication.</summary>
    public static MapSourceCreatedViewRecordCounts FromCompleteCurrentNativeCounters(long frameNumber,
        string viewIdentity, string publicationIdentity, IReadOnlyDictionary<uint, ReadOnlyMemory<byte>> completeCounters)
    {
        ArgumentNullException.ThrowIfNull(completeCounters);
        var decoded = new Dictionary<uint, int>();
        foreach (var item in completeCounters)
        {
            if (item.Value.Length != 16) throw new InvalidDataException("A current native View counter requires its complete16-byte struct.");
            int capacity = BinaryPrimitives.ReadInt32LittleEndian(item.Value.Span.Slice(8, 4));
            int stored = BinaryPrimitives.ReadInt32LittleEndian(item.Value.Span.Slice(12, 4));
            if (capacity < 0) throw new InvalidDataException("The finite owned native View counter requires a nonnegative capacity.");
            decoded.Add(item.Key, stored >= 0 ? stored : Math.Min(capacity, -1 - stored));
        }
        return new(frameNumber, viewIdentity, publicationIdentity, decoded);
    }
}

public sealed record MapSourceCreatedPreparedModelPrimitive(long FrameNumber, string ViewIdentity,
    string InstanceKey, int SourceDrawIndex, string CurrentCpuPreparationProvenance);

/// <summary>An actual current setter publication replacing the retained fresh
/// WorldPQ+78 declaration for one owned instance.</summary>
public sealed record MapSourceCreatedModelExclusionOverride(long FrameNumber, string ViewIdentity,
    string InstanceKey, string CurrentSourceIdentity, uint Value78, string CurrentWriterProvenance);

/// <summary>The finite additional-owner branch of D7D9D0. Counts and wrapper
/// selection are current publications. Constructor clears alone do not prove
/// them after records have been queued.</summary>
public sealed record MapSourceCreatedViewPublicationState(long FrameNumber,
    string ViewIdentity, string CameraOwnerIdentity, string CurrentPublicationProvenance,
    bool? AdditionalOwners1B80_1BB0_1BB8_1B98Absent, int ShadowPrimitiveCount1BFC,
    IReadOnlyList<int> CollectionPrimitiveCounts2To5, bool? SelectedShadowWrapper1BD0Present,
    ulong? SelectedShadowCpuVtable)
{
    public TerrainCurrentViewPublication? Terrain1BB8 { get; init; }
    public bool? OtherAdditionalOwners1B80_1BB0_1B98Absent { get; init; }
    public TerrainSelectedKind1Record? SelectedShadowRecord1BD0 { get; init; }
}

public sealed record MapSourceCreatedKind1ViewRecord(uint Mask78, int CurrentCpuPrimitiveCount,
    string LightOwnerIdentity, string CurrentCountPublicationIdentity);

/// <summary>The source Camera tail retained by a remake Camera owner. The
/// original zero-key loader and final reference copy prove 1808 is null in
/// this source generation; this packet is not a live original Camera read.</summary>
public sealed class MapSourceCreatedViewCameraFields
{
    private readonly byte[] prefix;
    internal MapSourceCreatedViewCameraFields(MapCameraConstructionPacket camera,
        int a00, float a28, uint a3c, uint ocean, uint rain, string provenance)
    {
        FrameNumber = camera.FrameNumber; CameraObjectId = camera.Source.Camera.ObjectId;
        prefix = camera.Prefix.ToArray(); ViewSignedA00 = a00; ViewA28 = a28; ViewA3C = a3c;
        StatisticalOceanGroupIdAC0 = ocean; RainDropGroupId12A8 = rain; SourceProvenance = provenance;
        ReflectionProbeRsmCaptureMode15A0 = camera.SourceReflectionProbeRsmCaptureMode15A0;
    }
    public long FrameNumber { get; }
    public uint CameraObjectId { get; }
    public int ViewSignedA00 { get; }
    public float ViewA28 { get; }
    public uint ViewA3C { get; }
    public uint StatisticalOceanGroupIdAC0 { get; }
    public uint RainDropGroupId12A8 { get; }
    public uint ReflectionProbeRsmCaptureMode15A0 { get; }
    public bool SelectedCamera1808Present => false;
    public string SourceProvenance { get; }
    internal void Validate(MapCameraConstructionPacket camera)
    {
        var accepted = camera.CopyAcceptedSnapshot();
        if (FrameNumber != accepted.FrameNumber || CameraObjectId != accepted.Source.Camera.ObjectId ||
            ReflectionProbeRsmCaptureMode15A0 != accepted.SourceReflectionProbeRsmCaptureMode15A0 ||
            !prefix.AsSpan().SequenceEqual(accepted.Prefix.Span))
            throw new InvalidDataException("View source fields require the same accepted Camera bytes and frame.");
    }
}

/// <summary>Current accepted View fields produced from exact source Camera
/// fields and counted kind1 records, including the final 19AC publisher.</summary>
public sealed class MapSourceCreatedViewFieldsPacket
{
    private readonly MapSourceCreatedKind1ViewRecord[] records;
    internal MapSourceCreatedViewFieldsPacket(MapSourceCreatedViewCameraFields camera,
        MapLightingBinding binding, MapSourceCreatedViewPublicationState current,
        IEnumerable<MapSourceCreatedKind1ViewRecord> records, uint flags, uint oceanFlags, uint rainFlags, ulong? shadowVtable,
        string provenance, MapWorldKind1CurrentQueue? currentWorldQueue = null)
    {
        Camera = camera; Binding = binding; CurrentPublication = current with
        { CollectionPrimitiveCounts2To5 = Array.AsReadOnly(current.CollectionPrimitiveCounts2To5.ToArray()) };
        this.records = records.ToArray(); ViewFlags19AC = flags; StatisticalOceanFlags19B0 = oceanFlags;
        RainDropFlags19B4 = rainFlags; View1BD0CpuVtable = shadowVtable;
        SourceProvenance = provenance;
        CurrentWorldQueue = currentWorldQueue;
        if (currentWorldQueue is not null && (currentWorldQueue.FrameNumber != camera.FrameNumber || currentWorldQueue.Binding != binding ||
            this.records.Length != currentWorldQueue.Records.Count || this.records.Where((record, index) =>
                record.Mask78 != currentWorldQueue.Records[index].Mask78 || record.LightOwnerIdentity != currentWorldQueue.Records[index].RecordOwnerIdentity).Any()))
            throw new InvalidDataException("A World View packet must retain its exact current queue frame, binding and ordered record owners.");
    }
    public MapSourceCreatedViewCameraFields Camera { get; }
    public MapLightingBinding Binding { get; }
    public MapSourceCreatedViewPublicationState CurrentPublication { get; }
    public IReadOnlyList<MapSourceCreatedKind1ViewRecord> Kind1Records => Array.AsReadOnly(records.ToArray());
    public uint ViewFlags19AC { get; }
    public uint StatisticalOceanFlags19B0 { get; }
    public uint RainDropFlags19B4 { get; }
    public ulong? View1BD0CpuVtable { get; }
    public bool SelectedCamera1808Present => Camera.SelectedCamera1808Present;
    public string SourceProvenance { get; }
    /// <summary>The actual queue generation which produced World kind1
    /// counters. Legacy HDRI publication has no such world queue binding.</summary>
    public MapWorldKind1CurrentQueue? CurrentWorldQueue { get; }
    public DeferredF103CurrentViewInputs CreateF103Inputs(bool? currentAuxiliaryPresent,
        uint? currentAuxiliaryLightingFlags) => new(Camera.FrameNumber, Binding.ViewIdentity,
        Binding.CameraOwnerIdentity, Camera.ViewSignedA00, ViewFlags19AC, Camera.ViewA28,
        Camera.ViewA3C, View1BD0CpuVtable, currentAuxiliaryPresent, currentAuxiliaryLightingFlags,
        Binding.LightOwnerIdentity, SourceProvenance);
}

/// <summary>SC/TC source Camera declaration → clone fields and CE31D0 kind1
/// record masks → D7D9D0 current View flags. Additional typed owner branches
/// require their own producers before they can be supported.</summary>
public static class MapSourceCreatedViewFields
{
    public const string ContractId = "ce-20261002-main-fresh-view-fields";
    public const uint ModelExclusion78PropertyHash = 0xC0C5A5B5;
    // Imported supported source profile, scoped to the actual scene-owned asset.
    // Current frame/View/primitive admission remains the caller's work.
    private sealed class CanonicalPrimitiveSource { }
    private static readonly ConditionalWeakTable<MapStaticModelAsset, CanonicalPrimitiveSource> primitiveSources = new();
    private sealed record CanonicalExclusionSource(uint Value78);
    private static readonly ConditionalWeakTable<MapStaticModelAsset, CanonicalExclusionSource> exclusionSources = new();
    private sealed record CanonicalPlacementSource(IReadOnlyDictionary<uint, bool> HasExclusionOverride);
    private static readonly ConditionalWeakTable<LoadedObjects, CanonicalPlacementSource> placementSources = new();

    internal static void ValidateCanonicalPreparedPrimitiveSource(MapStaticModelAsset asset)
    {
        _ = primitiveSources.GetValue(asset, source =>
        {
            // Repository loading parsed these documents and geometry. The asset
            // catalog derives Draws from this same Geometry before publishing it.
            var model = source.Reference.Dictionary.Document.Objects.Single(item => item.ObjectId == source.Reference.ModelObject.ObjectId &&
                item.Kind == DokObjectKind.Inline && item.ClassId == 0xD40B3C8F);
            var file = model.Properties.Single(property => property.NameHash == 0x8AB68B3F);
            if (file.TypeCode != 5 || file.ElementCount != 1 || file.RawValue.Length != 4 ||
                BinaryPrimitives.ReadUInt32LittleEndian(file.RawValue) != source.RawTransforms.Source.FileId ||
                source.Reference.ModelFileId != source.RawTransforms.Source.FileId ||
                source.TextureResources.Model.Source.ResourceUri != source.RawTransforms.Source.ResourceUri ||
                source.TextureResources.Model.Source.DecodedSha256 != source.RawTransforms.Source.DecodedSha256)
                throw new InvalidDataException("Prepared primitive Model dictionary does not select this exact source geometry.");
            SourceZero(41, 0x95424354, 1); // KTGLG1iiResourceHash → optional dependency2 → ModelCPU+B0.
            SourceZero(24, 0x07AAF542, 0); // Empty swap array leaves the fixed dependency order unchanged.
            SourceZero(25, 0x0B534ABC, 1); // No common swap dependency precedes the fixed optional input.
            var geometry = source.RawTransforms.Geometry;
            var actual = source.Draws;
            var part = geometry.Sections.Single(section => section.Id == 0x10009);
            if (part.ItemCount != 1 || part.RawBytes.Length < 48 ||
                (BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(16)) & 0x7FFFFFFF) != 0 ||
                BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(28)) != 0)
                throw new NotSupportedException("Ordinary ModelCPU.V10 publication requires one source LOD0 part with no second-category groups.");
            if (actual.Any(draw => draw.UnknownValue32 != 1))
                throw new NotSupportedException("Prepared primitive publication requires the exact one-subdraw source profile.");
            return new CanonicalPrimitiveSource();
            void SourceZero(int ordinal, uint hash, int count)
            {
                var property = model.Properties[ordinal];
                if (property.Index != ordinal || property.NameHash != hash || property.TypeCode != 5 ||
                    property.ElementCount != count || property.RawValue.Length != count * 4 ||
                    (count == 1 && BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue) != 0))
                    throw new NotSupportedException("The current geometry fallback requires the exact source zero G1ii key and empty swap dependencies.");
            }
        });
    }

    /// <summary>Read WorldPQ constructor argument9 from the selected canonical
    /// source definition. DA6D40 binds property ordinal9; DA7E48 forwards it
    /// to D9E928/CPU+78. Every fresh instance inherits its own declaration.
    /// Current overrides are complete explicit setter publications, keyed only
    /// for the instances which actually received a later write.</summary>
    public static IReadOnlyDictionary<string, uint> ReadCurrentModelExclusions78(
        MapStaticModelCurrentOwnedFrame sourceFrame, string currentViewIdentity,
        IReadOnlyDictionary<string, MapSourceCreatedModelExclusionOverride> explicitCurrentOverrides)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame); ArgumentException.ThrowIfNullOrWhiteSpace(currentViewIdentity);
        ArgumentNullException.ThrowIfNull(explicitCurrentOverrides);
        var visible = sourceFrame.Visibility.Visible;
        var owned = sourceFrame.Population.Instances.ToDictionary(instance => instance.InstanceKey, StringComparer.Ordinal);
        if (!sourceFrame.Population.Complete || visible.Any(instance => !owned.TryGetValue(instance.InstanceKey, out var current) ||
                !ReferenceEquals(instance.Asset, current.Asset) || instance.CurrentSourceIdentity != current.CurrentSourceIdentity) ||
            sourceFrame.FrameNumber <= 0 || explicitCurrentOverrides.Any(item => !owned.TryGetValue(item.Key, out var instance) ||
            item.Value.FrameNumber != sourceFrame.FrameNumber || item.Value.ViewIdentity != currentViewIdentity ||
            item.Value.InstanceKey != item.Key || item.Value.CurrentSourceIdentity != instance.CurrentSourceIdentity ||
            string.IsNullOrWhiteSpace(item.Value.CurrentSourceIdentity) ||
            string.IsNullOrWhiteSpace(item.Value.CurrentWriterProvenance)))
            throw new InvalidDataException("Current exclusion78 overrides must name this frame's actual owned instances and matching current setter identities.");
        var result = new Dictionary<string, uint>(StringComparer.Ordinal);
        var declarations = new Dictionary<MapStaticModelAsset, uint>();
        foreach (var instance in visible)
        {
            if (explicitCurrentOverrides.TryGetValue(instance.InstanceKey, out var current))
            {
                if (current.FrameNumber != sourceFrame.FrameNumber || current.ViewIdentity != currentViewIdentity ||
                    current.InstanceKey != instance.InstanceKey || current.CurrentSourceIdentity != instance.CurrentSourceIdentity ||
                    string.IsNullOrWhiteSpace(current.CurrentWriterProvenance))
                    throw new InvalidDataException("Exclusion78 setter publication belongs to a different current owner/frame.");
                result.Add(instance.InstanceKey, current.Value78); continue;
            }
            if (instance.NativePublication is not { FreshConstructorBindPrefix: true, ParentBound: false } ||
                string.IsNullOrWhiteSpace(instance.CurrentSourceIdentity))
                throw new InvalidDataException("An instance without retained fresh WorldPQ fields requires its actual current exclusion78 setter publication.");
            if (!declarations.TryGetValue(instance.Asset, out uint value))
            {
                var cached = exclusionSources.GetValue(instance.Asset, asset =>
                {
                    var source = asset.TextureResources.Catalog ?? throw new InvalidDataException("Fresh exclusion78 source catalog is absent.");
                    var declared = asset.TextureResources.CatalogDefinition ?? throw new InvalidDataException("Fresh exclusion78 WorldPQ identity is absent.");
                    var canonical = source.Document.Objects.Single(item => item.ObjectId == declared.ObjectId &&
                        item.Kind == DokObjectKind.Inline && item.ClassId == ScenePlacementReader.ModelWorldPqClass);
                    var target = canonical.Properties.Single(property => property.NameHash == 0x0B6E1578);
                    if (target.TypeCode != 5 || target.ElementCount != 1 || target.RawValue.Length != 4 ||
                        BinaryPrimitives.ReadUInt32LittleEndian(target.RawValue) != asset.Reference.ModelObject.ObjectId)
                        throw new InvalidDataException("Fresh exclusion78 WorldPQ does not select the current owned Model asset.");
                    var properties = canonical.Properties.Where(property => property.NameHash == ModelExclusion78PropertyHash).ToArray();
                    if (properties.Length != 1 || properties[0].Index != 9 || properties[0].TypeCode != 5 ||
                        properties[0].ElementCount != 1 || properties[0].RawValue.Length != 4)
                        throw new InvalidDataException("Fresh WorldPQ exclusion78 requires the exact native property9/type5 scalar binding.");
                    return new(BinaryPrimitives.ReadUInt32LittleEndian(properties[0].RawValue));
                });
                value = cached.Value78;
                declarations.Add(instance.Asset, value);
            }
            if (instance.SourcePlacement is { } placement)
            {
                var source = placement.Source.InstanceDocument;
                var cached = placementSources.GetValue(source, document =>
                {
                    return new(document.Document.Objects.ToDictionary(item => item.ObjectId,
                        item => item.Properties.Any(property => property.NameHash == ModelExclusion78PropertyHash)));
                });
                if (!cached.HasExclusionOverride.TryGetValue(placement.Source.Object.ObjectId, out bool overridden) || overridden)
                    throw new NotSupportedException("A scene WorldPQ exclusion78 override requires its exact current setter publication.");
            }
            result.Add(instance.InstanceKey, value);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, uint>(result);
    }

    /// <summary>The exact fresh HDRI ordinary first-counter capacities.
    /// Primary/context capacities are zero; the split ordinary records use
    /// the imported maximum primitive count.</summary>
    public static IReadOnlyDictionary<uint, int> ReadFreshFirstCounterCapacities(
        MapSourceCreatedLightingSource source, MapSourceCreatedLightingPacket lighting,
        byte currentViewAux3F2)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(lighting);
        if (source.Identity != lighting.SourceIdentity)
            throw new InvalidDataException("Fresh first-counter capacities require the same constructed current HDRI owner.");
        uint raw = source.Construction.Source.Settings.Shadow.MaximumPrimitives;
        if (raw > int.MaxValue) throw new NotSupportedException("Fresh HDRI primitive capacity exceeds the finite signed counter range.");
        var masks = SourceKind1RecordMasks(lighting, currentViewAux3F2);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<uint, int>(masks.Select((mask, index) =>
            (mask, capacity: index == 0 || index == masks.Count - 1 ? 0 : (int)raw)).ToDictionary(item => item.mask, item => item.capacity));
    }

    /// <summary>Publish the complete current owned CPU preparation list for
    /// fresh source Main View0. The owner explicitly retains WorldPQ328/334,
    /// so its constructor all-one mask and negative V408 selector forward bit0.
    /// Actual CommonAux404 and later exclusion78 setters remain current inputs.
    /// A native capacity failure rejects the frame, preserving the complete
    /// population for a caller which can handle the native refusal explicitly.</summary>
    public static MapSourceCreatedViewRecordCounts PrepareFreshModelPopulationCounts(
        MapStaticModelCurrentOwnedFrame sourceFrame, MapSourceCreatedLightingSource lightingSource,
        MapSourceCreatedLightingPacket lighting, byte currentViewAux3F2,
        bool? freshWorldPqOrdinaryMaskFields328And334Retained, uint? currentCommonAuxExclusion404,
        IReadOnlyDictionary<string, MapSourceCreatedModelExclusionOverride> explicitCurrentExclusionOverrides,
        IReadOnlyList<MapSourceCreatedPreparedModelPrimitive> completeCurrentPreparedDraws,
        bool? actualGeometryInputFallbackOwnsSource, string publicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(sourceFrame);
        if (sourceFrame.MainCamera.Camera.Source.Camera.ObjectId != MainMapCameraAdapter.MainCameraObjectId ||
            freshWorldPqOrdinaryMaskFields328And334Retained != true || actualGeometryInputFallbackOwnsSource != true)
            throw new InvalidDataException("Fresh ordinary publication requires current Main View0, retained WorldPQ328/334 and the current geometry override/null→owned ModelCPU fallback branch.");
        var exclusions = ReadCurrentModelExclusions78(sourceFrame, lighting.Binding.ViewIdentity, explicitCurrentExclusionOverrides);
        var capacities = ReadFreshFirstCounterCapacities(lightingSource, lighting, currentViewAux3F2);
        return MapSourceCreatedViewRecordCounts.FromCurrentOwnedPreparedModelDraws(sourceFrame, lighting,
            currentViewAux3F2, 0, 1, currentCommonAuxExclusion404, exclusions, completeCurrentPreparedDraws, capacities,
            publicationIdentity + ";sourceMainViewSlot=0;WorldPQ328/334=retained-source-constructor;" +
            "ordinary-mask=TC:D9EC00/D9EC0B/DB2EDC/DB5AEF/DB5AF7/C393F5;capacity=TC:D13F6A/CE34FA/D068AA");
    }

    public static MapSourceCreatedViewCameraFields ReadFreshRetainedCameraFields(
        MapCameraConstructionPacket acceptedCamera)
    {
        ArgumentNullException.ThrowIfNull(acceptedCamera);
        var camera = acceptedCamera.CopyAcceptedSnapshot();
        if (camera.FrameNumber <= 0 || camera.Prefix.Length != MapCameraConstruction.PrefixByteLength)
            throw new InvalidDataException("View construction requires a complete current owned Camera prefix.");
        var source = camera.Source.Camera;
        uint a00 = Property(24, 0x0611DBC4, 4), a28 = Property(129, 0x0D8D3389, 8), a3c = Property(26, 0x96E32C4A, 4);
        uint ocean = Property(65, 0xB258D1A7, 5), rain = Property(701, 0x6EA5A887, 4);
        if (Property(382, 0x053A9DF4, 5) != 0)
            throw new NotSupportedException("The retained source Camera1808 producer requires the exact zero LocalAmbientSpecularMap key.");
        float size = BitConverter.UInt32BitsToSingle(a28);
        if (!float.IsFinite(size)) throw new InvalidDataException("Source View+A28 must be finite.");
        return new(camera, unchecked((int)a00), size, a3c, ocean, rain,
            $"{ContractId};{camera.Source.ConstructorOrigin};frame={camera.FrameNumber};" +
            "tail=retained-source-generation;TC:C42023/C420A2/C420DE/C421E2/C43922;" +
            "clone=TC:D02C60;Camera1808=null:property382-zero/C57B9C/CAB22F/D06242");

        uint Property(int ordinal, uint hash, uint type)
        {
            var property = source.Properties[ordinal];
            if (property.Index != ordinal || property.NameHash != hash || property.TypeCode != type ||
                property.ElementCount != 1 || property.ElementSize != 4 || property.RawValue.Length != 4)
                throw new InvalidDataException($"View source property{ordinal} differs from its native descriptor and loader binding.");
            return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
        }
    }

    /// <summary>The exact successful CE31D0 source record mask sequence. The
    /// ordinary source group mask is split into individual bits, and the final
    /// context record retains literal80000000. The current ViewAux byte is
    /// explicit because values1..8 replace the primary mask.</summary>
    public static IReadOnlyList<uint> SourceKind1RecordMasks(MapSourceCreatedLightingPacket lighting,
        byte currentViewAux3F2)
    {
        ArgumentNullException.ThrowIfNull(lighting);
        if (lighting.SourceLightClassId != 0x66ECB240)
            throw new NotSupportedException("The CE31D0 mask splitter requires an HDRI source; Directional publication requires its actual world record queue.");
        uint primary = currentViewAux3F2 is >= 1 and <= 8 ? 1u << (currentViewAux3F2 - 1) :
            lighting.SourceIdentity.DeclaredPrimaryGroupMask;
        var masks = new List<uint> { primary };
        uint remaining = lighting.SourceIdentity.DeclaredGroupMask & ~primary;
        for (int bit = 0; bit < 32; bit++)
            if ((remaining & (1u << bit)) != 0) masks.Add(1u << bit);
        masks.Add(0x80000000);
        if (masks.Distinct().Count() != masks.Count)
            throw new NotSupportedException("The finite source record contract requires distinct primary, ordinary and context masks.");
        return masks.AsReadOnly();
    }

    public static MapSourceCreatedViewFieldsPacket PrepareAcceptedViewFields(
        MapCameraConstructionPacket acceptedCamera, MapSourceCreatedLightingPacket lighting,
        MapSourceCreatedViewCameraFields retainedSourceCamera, byte currentViewAux3F2,
        MapSourceCreatedViewRecordCounts currentCounts, MapSourceCreatedViewPublicationState current)
    {
        ArgumentNullException.ThrowIfNull(acceptedCamera); ArgumentNullException.ThrowIfNull(lighting);
        ArgumentNullException.ThrowIfNull(retainedSourceCamera); ArgumentNullException.ThrowIfNull(currentCounts);
        ArgumentNullException.ThrowIfNull(current); retainedSourceCamera.Validate(acceptedCamera);
        if (lighting.FrameNumber != retainedSourceCamera.FrameNumber || current.FrameNumber != lighting.FrameNumber ||
            currentCounts.FrameNumber != lighting.FrameNumber || current.ViewIdentity != lighting.Binding.ViewIdentity ||
            currentCounts.ViewIdentity != lighting.Binding.ViewIdentity || current.CameraOwnerIdentity != lighting.Binding.CameraOwnerIdentity ||
            string.IsNullOrWhiteSpace(current.CurrentPublicationProvenance) || string.IsNullOrWhiteSpace(lighting.Binding.LightOwnerIdentity))
            throw new InvalidDataException("View construction requires its current Camera, LightData and counted CPU publication in the same frame.");
        if (current.AdditionalOwners1B80_1BB0_1BB8_1B98Absent != true && current.Terrain1BB8 is null)
            throw new NotSupportedException("View19AC additional owners1B80/1BB0/1BB8/1B98 require their actual typed producers; absence must be explicit.");
        if (current.AdditionalOwners1B80_1BB0_1BB8_1B98Absent == true && current.Terrain1BB8 is not null)
            throw new InvalidDataException("A current Landscape1BB8 record cannot also be declared absent.");
        if (current.ShadowPrimitiveCount1BFC < 0 || current.CollectionPrimitiveCounts2To5 is null ||
            current.CollectionPrimitiveCounts2To5.Count != 4 || current.CollectionPrimitiveCounts2To5.Any(value => value < 0))
            throw new InvalidDataException("View19AC requires the complete current nonnegative1BFC and collection2..5 CPU publication counts.");
        uint[] masks = SourceKind1RecordMasks(lighting, currentViewAux3F2).ToArray();
        uint terrainFlags = current.Terrain1BB8?.ValidateAndGetFlags(lighting, current, masks) ?? 0;
        if (!masks.Order().SequenceEqual(currentCounts.Counts.Keys.Order()))
            throw new InvalidDataException("View19AC requires current counts for every exact source-produced kind1 record mask.");
        var records = masks.Select(mask => new MapSourceCreatedKind1ViewRecord(mask,
            currentCounts.Counts[mask], lighting.Binding.LightOwnerIdentity, currentCounts.PublicationIdentity)).ToArray();
        uint flags = 0, oceanFlags = 0, rainFlags = 0;
        foreach (var record in records)
        {
            if (record.CurrentCpuPrimitiveCount != 0) flags |= record.Mask78;
            oceanFlags |= SelectedGroup(retainedSourceCamera.StatisticalOceanGroupIdAC0) & record.Mask78;
            rainFlags |= SelectedGroup(retainedSourceCamera.RainDropGroupId12A8) & record.Mask78;
        }
        flags |= oceanFlags | rainFlags | terrainFlags;
        if (current.ShadowPrimitiveCount1BFC != 0 || current.CollectionPrimitiveCounts2To5.Any(value => value != 0)) flags |= 0x40000000;
        ulong? vtable = null;
        bool shadow = retainedSourceCamera.ViewSignedA00 > 0 && (flags & 0x40000000) != 0 && lighting.LocalLightData.Span[0xF1D] != 0;
        if (shadow)
        {
            if (current.SelectedShadowWrapper1BD0Present is not bool present)
                throw new InvalidDataException("Active View shadow features require the current selected1BD0 wrapper branch.");
            if (!present) vtable = 0;
            else if (current.SelectedShadowCpuVtable is not ulong actual || actual == 0)
                throw new InvalidDataException("Present View1BD0 requires its actual source wrapper→CPU vtable identity.");
            else vtable = actual;
        }
        return new(retainedSourceCamera, lighting.Binding, current, records, flags, oceanFlags, rainFlags, vtable,
            retainedSourceCamera.SourceProvenance + $";View={current.ViewIdentity};CameraOwner={current.CameraOwnerIdentity};" +
            $"kind1-masks=TC:CE31D0/D653F0/D65620/CFD2D0/D0681E;counts={currentCounts.PublicationIdentity};" +
            $"View19AC=TC:D7D9D0/D7DF0F;current={current.CurrentPublicationProvenance}");
    }
    private static uint SelectedGroup(uint oneBasedGroup) => oneBasedGroup is >= 1 and <= 7 ? 1u << ((int)oneBasedGroup - 1) : 0;
}
