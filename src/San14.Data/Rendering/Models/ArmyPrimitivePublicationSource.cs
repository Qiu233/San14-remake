using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The actual Army category's default WorldPQ and ordinary ModelCPU
/// prefix. Geometry classification, exclusions and fallback are read from the
/// source bytes; a category number is never a light-record number.</summary>
public sealed class ArmyPrimitivePublicationSource
{
    private ArmyPrimitivePublicationSource(LoadedArmyCatalog catalog,
        CeArmyModelCategory category, LoadedModelGeometry model, uint exclusion,
        ModelPrimitiveInventorySource primitives)
        => (Catalog, Category, Model, Exclusion78, Primitives) = (catalog, category, model, exclusion, primitives);

    public LoadedArmyCatalog Catalog { get; }
    public CeArmyModelCategory Category { get; }
    public LoadedModelGeometry Model { get; }
    public ModelPrimitiveInventorySource Primitives { get; }
    public uint Exclusion78 { get; }
    public uint FreshOrdinaryViewMask328 => uint.MaxValue;
    public int FreshEligibilitySelector334 => -1;
    public int OrdinaryModelCpuCount => Primitives.Category0.Count;
    public IReadOnlyList<int> OrdinarySourceDrawIndices => Primitives.Category0.SourceDrawIndices;
    public string Identity => $"{Catalog.Catalog.Source.ResourceUri}@{Catalog.Catalog.Source.DecodedSha256}/" +
        $"{Category.DefinitionId:X8}|{Catalog.Dictionary.Source.ResourceUri}@{Catalog.Dictionary.Source.DecodedSha256}/" +
        $"{Category.ModelObjectId:X8}|{Model.Source.ResourceUri}@{Model.Source.DecodedSha256}";
    public string SourceProvenance => Identity + ";C4B9B28D/default370-creator;" +
        "TC:DA7DF1/DA7E72/D9E890/D9EC00;property9-C0C5A5B5-to-WorldPQ78;" +
        "TC:C222A0/C240C0/ABB360/F587F0/F57390;selector0/key0;" +
        "TC:C392F9-ModelData20-fallback;source-G1ii/swap/destruction-null";

    public static ArmyPrimitivePublicationSource ReadKnownCategory(
        LoadedArmyCatalog catalog, int category, LoadedModelGeometry actualModel)
    {
        ArgumentNullException.ThrowIfNull(catalog); ArgumentNullException.ThrowIfNull(actualModel);
        if ((uint)category >= CeArmyModels.KnownSourceCategories.Count)
            throw new NotSupportedException("Army primitive publication has no source-backed category.");
        var known = CeArmyModels.KnownSourceCategories[category];
        if (catalog.Catalog.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", CeArmyModels.CatalogId).ToString() ||
            catalog.Dictionary.Source.ResourceUri != GameResourceId.Rdb("KIDSSystemResource", CeArmyModels.DictionaryId).ToString() ||
            actualModel.Source.ResourceUri != GameResourceId.Rdb("CharacterEditor", known.ModelFileId).ToString())
            throw new InvalidDataException("Army publication source URI does not identify this actual category.");
        var definitions = catalog.Catalog.Document.Objects;
        var definition = definitions.Single(o => o.ObjectId == known.DefinitionId && o.Kind == DokObjectKind.Inline && o.ClassId == 0xC4B9B28D);
        if (Scalar(definition, 0x0B6E1578) != known.ModelObjectId ||
            actualModel.Source.FileId != known.ModelFileId)
            throw new InvalidDataException("Army definition does not select this exact model source.");
        // Zero override selector chooses the proven default 370-byte creator.
        if (Scalar(definition, 0xECEC336A) != 0 || Scalar(definition, 0xB2B6E876) != 0)
            throw new NotSupportedException("Army source geometry override/fallback requires a different producer.");
        uint exclusion = Scalar(definition, MapSourceCreatedViewFields.ModelExclusion78PropertyHash);
        var primitives = ModelPrimitiveInventorySource.ReadLoaded(actualModel, catalog.Dictionary, known.ModelObjectId);
        return new(catalog, known, actualModel, exclusion, primitives);
    }

    private static uint Scalar(DokObject obj, uint hash)
    {
        var property = obj.Properties.Single(property => property.NameHash == hash);
        if (property.TypeCode != 5 || property.ElementCount != 1 || property.RawValue.Length != 4)
            throw new InvalidDataException("Army publication requires a typed source scalar binding.");
        return BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue);
    }
}

/// <summary>One actor's complete CPU preparation. Current eligibility is a
/// separate source publisher, not inferred from troop count or GPU success.</summary>
public sealed record ArmyCurrentPrimitiveActor(string ActorIdentity,
    ArmyPrimitivePublicationSource Source, bool? CurrentOrdinaryView0Eligible,
    bool? FreshWorldPqMaskAndGeometryFallbackRetained, uint? CurrentExclusion78,
    string CurrentProducerProvenance);

public sealed record ArmyCurrentPrimitiveAdmission(string ActorIdentity, int SourceDrawIndex,
    string CurrentSourceIdentity, uint Exclusion78, IReadOnlyList<uint> AdmittedRecordMasks)
{
    public string PacketKey => ActorIdentity + "/draw" + SourceDrawIndex;
}

/// <summary>A complete current ordinary Army publication before GPU work.
/// Capacities are checked by the full View join after every CPU family is
/// merged; this packet never drops overflowing or unassigned primitives.</summary>
public sealed class ArmyCurrentPrimitivePublication
{
    private readonly ArmyCurrentPrimitiveAdmission[] primitives;
    private ArmyCurrentPrimitivePublication(MapSourceCreatedViewRecordCounts counts,
        ArmyCurrentPrimitiveAdmission[] values) => (Counts, primitives) = (counts, values);
    public MapSourceCreatedViewRecordCounts Counts { get; }
    public IReadOnlyList<ArmyCurrentPrimitiveAdmission> Primitives => Array.AsReadOnly(primitives.ToArray());
    public ArmyCurrentPrimitiveAdmission Require(string actorIdentity, int drawIndex) =>
        primitives.Single(p => p.ActorIdentity == actorIdentity && p.SourceDrawIndex == drawIndex);

    /// <summary>The captured empty Army still publishes actual zero counters
    /// for every independent World record. Nonempty armies require record
    /// identities and per-record capacity admission before using this path.</summary>
    public static ArmyCurrentPrimitivePublication PrepareEmptyWorld(
        MapSourceCreatedLightingPacket lighting, MapCommonRenderState common,
        MapWorldKind1CurrentQueue queue,
        IReadOnlyList<ArmyCurrentPrimitiveActor> completeCurrentActors, string currentPublicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(lighting); ArgumentNullException.ThrowIfNull(common);
        ArgumentNullException.ThrowIfNull(queue); ArgumentNullException.ThrowIfNull(completeCurrentActors);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPublicationIdentity);
        queue.ValidateSources();
        common.ValidateFrame(lighting.FrameNumber, common.CommonTableOwnerIdentity, lighting.Binding.ViewIdentity);
        if (completeCurrentActors.Count != 0)
            throw new NotSupportedException("Nonempty World Army publication needs actual per-record primitive admission.");
        if (queue.FrameNumber != lighting.FrameNumber || queue.Binding != lighting.Binding ||
            lighting.SourceLightClassId != DirectionalLightSource.SourceClass ||
            common.ModelExclusionMask is null || queue.Records.Count != 5 ||
            queue.Records.Select(r => r.Mask78).Distinct().Count() != queue.Records.Count ||
            !queue.Records.Any(r => r.Source.SourceObjectId == lighting.SourceIdentity.MainLightObjectId))
            throw new InvalidDataException("Empty Army publication requires its exact five-record current World and Common.");
        var counts = queue.Records.ToDictionary(r => r.Mask78, _ => 0);
        return new(new(lighting.FrameNumber, lighting.Binding.ViewIdentity, currentPublicationIdentity +
            ";actual-empty-Army;independent-source-World=" + queue.QueueOwnerIdentity, counts), []);
    }
    public static ArmyCurrentPrimitivePublication Prepare(long frameNumber,
        MapSourceCreatedLightingPacket lighting, byte currentViewAux3F2, uint? actualCommonExclusion404,
        IReadOnlyList<ArmyCurrentPrimitiveActor> completeCurrentActors, string currentPublicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(lighting); ArgumentNullException.ThrowIfNull(completeCurrentActors);
        if (lighting.FrameNumber != frameNumber || actualCommonExclusion404 is not uint common ||
            string.IsNullOrWhiteSpace(currentPublicationIdentity) ||
            completeCurrentActors.Select(a => a.ActorIdentity).Distinct(StringComparer.Ordinal).Count() != completeCurrentActors.Count)
            throw new InvalidDataException("Army publication requires the complete same-frame actor owner and current Common exclusion.");
        if ((lighting.LocalLightData.Span[0xF20] & 1) != 0)
            throw new NotSupportedException("Army alternate LightDataF20 classification needs its own current source producer.");
        var masks = MapSourceCreatedViewFields.SourceKind1RecordMasks(lighting, currentViewAux3F2);
        var counts = masks.ToDictionary(m => m, _ => 0);
        var result = new List<ArmyCurrentPrimitiveAdmission>();
        foreach (var actor in completeCurrentActors)
        {
            if (string.IsNullOrWhiteSpace(actor.ActorIdentity) || string.IsNullOrWhiteSpace(actor.CurrentProducerProvenance) ||
                actor.CurrentOrdinaryView0Eligible is not bool eligible || actor.FreshWorldPqMaskAndGeometryFallbackRetained != true ||
                actor.CurrentExclusion78 is not uint exclusion)
                throw new InvalidDataException("Army current visibility, mask/fallback or exclusion publication is unresolved.");
            uint[] admitted = eligible ? masks.Where(mask => ((exclusion | common) & mask) == 0).ToArray() : [];
            foreach (int draw in actor.Source.OrdinarySourceDrawIndices)
            {
                result.Add(new(actor.ActorIdentity, draw, actor.Source.Identity, exclusion, Array.AsReadOnly(admitted.ToArray())));
                foreach (uint mask in admitted) counts[mask] = checked(counts[mask] + 1);
            }
        }
        return new(new(frameNumber, lighting.Binding.ViewIdentity, currentPublicationIdentity +
            ";actual-Army-ModelCPU-selector0/key0;source-definition-exclusion78;TC:C398E2/BB8680", counts), result.ToArray());
    }
}
