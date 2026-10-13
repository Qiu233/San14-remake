using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One original ModelCPU selector. Draw references retain the native
/// part/group order; this is a complete source inventory before View culling.</summary>
public sealed record ModelPrimitiveCategoryInventory(int Category,
    IReadOnlyList<int> SourceDrawIndices)
{
    public int Count => SourceDrawIndices.Count;
    public int PrimitiveStride => 0x70;
}

/// <summary>Loaded, finite original ModelCPU inventory. C222A0 always returns
/// positive zero, so the current distance does not select a different LOD row
/// for this provider. Optional/current GeometryInput providers are separate.</summary>
public sealed class ModelPrimitiveInventorySource
{
    private static readonly ConditionalWeakTable<MapStaticModelAsset, ModelPrimitiveInventorySource> StaticSources = new();
    private readonly ModelPrimitiveCategoryInventory[] categories;

    private ModelPrimitiveInventorySource(string identity, int[] first,
        WorldPrimitiveModelDistance declaredDistance)
    {
        SourceIdentity = identity; DeclaredDistance = declaredDistance;
        categories = [new(0, Array.AsReadOnly(first)), new(1, Array.AsReadOnly(Array.Empty<int>()))];
    }

    public string SourceIdentity { get; }
    public WorldPrimitiveModelDistance DeclaredDistance { get; }
    public string SourceProvenance => SourceIdentity +
        ";TC:ModelVT16B1E58/+08=C222A0/+10=C240C0/+18=C193B0;" +
        "ABB360/section10009;F587F0/F57390;D600A0;one-part-classification0-first-group-order";

    /// <summary>Returns the actual provider's key, including when its unused
    /// distance argument is negative or nonfinite. It is not a tuple sort key.</summary>
    public static float ReadModelCacheKey(float actualModelDistance) => 0f;

    public ModelPrimitiveCategoryInventory SelectCurrent(int category, float actualModelDistance)
    {
        if ((uint)category >= categories.Length)
            throw new NotSupportedException("The complete early19 Model inventory has selectors0 and1.");
        _ = ReadModelCacheKey(actualModelDistance);
        return categories[category];
    }

    public ModelPrimitiveCategoryInventory Category0 => categories[0];
    public ModelPrimitiveCategoryInventory Category1 => categories[1];

    /// <summary>Import/reload boundary. The cached source is reused without
    /// rereading RDB data, hashing bytes or comparing full arrays per request.</summary>
    public static ModelPrimitiveInventorySource LoadStatic(MapStaticModelAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return StaticSources.GetValue(asset, static actual =>
            ReadLoaded(new(actual.RawTransforms.Geometry, actual.RawTransforms.Source),
                actual.Reference.Dictionary, actual.Reference.ModelObject.ObjectId));
    }

    public static ModelPrimitiveInventorySource LoadArmy(ArmyPrimitivePublicationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Primitives;
    }

    /// <summary>Read once from the actual selected dictionary/model sources.
    /// This finite implementation accepts the proven one-part category0 profile;
    /// an unsupported profile never becomes a synthetic empty category1.</summary>
    public static ModelPrimitiveInventorySource ReadLoaded(LoadedModelGeometry model,
        LoadedObjects dictionary, uint selectedModelObjectId)
    {
        ArgumentNullException.ThrowIfNull(model);
        LoadedRdbAsset modelSource = model.Source;
        G1ModelGeometry geometry = model.Geometry;
        ArgumentNullException.ThrowIfNull(dictionary);
        var declaration = dictionary.Document.Objects.Single(value =>
            value.Kind == DokObjectKind.Inline && value.ClassId == 0xD40B3C8F && value.ObjectId == selectedModelObjectId);
        var file = declaration.Properties.Single(property => property.NameHash == 0x8AB68B3F);
        if (file.TypeCode != 5 || file.ElementCount != 1 || file.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(file.RawValue) != modelSource.FileId)
            throw new InvalidDataException("Model inventory dictionary does not select this actual geometry resource.");
        RequireZero(0x07AAF542, 0);
        RequireZero(0x0B534ABC, 1);
        RequireZero(0x95424354, 1);
        var bindings = G1ModelDrawReader.Read(geometry);
        var part = geometry.Sections.Single(section => section.Id == 0x10009);
        int[] first = bindings.Groups.SelectMany(group => group.DrawIndices).ToArray();
        if (part.ItemCount != 1 || part.RawBytes.Length < 48 ||
            (BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(16)) & 0x7FFFFFFF) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(24)) != bindings.Groups.Count ||
            BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(28)) != 0 ||
            bindings.Draws.Any(draw => draw.UnknownValue32 != 1))
            throw new NotSupportedException("Model inventory requires the actual complete unique one-part LOD0/classification0 first-group profile.");
        string identity = dictionary.Source.ResourceUri + "@" + dictionary.Source.DecodedSha256 +
            "/" + selectedModelObjectId.ToString("X8") + "|" + modelSource.ResourceUri + "@" + modelSource.DecodedSha256;
        return new(identity, first, WorldPrimitiveDistanceKey.ReadModelDeclaration(declaration));

        void RequireZero(uint hash, int count)
        {
            var property = declaration.Properties.Single(property => property.NameHash == hash);
            if (property.TypeCode != 5 ||
                property.ElementCount != count || property.RawValue.Length != count * 4 ||
                count == 1 && BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue) != 0)
                throw new NotSupportedException("Model inventory requires the actual source null G1ii dependency and absent shader swaps.");
        }
    }
}

/// <summary>The current owner explicitly retains the canonical ModelCPU
/// GeometryInput. Unknown or replaced providers require their own inventory.</summary>
public sealed record ModelCurrentPrimitiveBinding(string CurrentSourceIdentity,
    bool? RetainedSourceModelCpuIsCurrent, string CurrentProducerProvenance);

public sealed record ModelCurrentPrimitiveModel(string ModelIdentity, string CurrentSourceIdentity,
    ModelPrimitiveInventorySource Source, string CurrentProducerProvenance);

/// <summary>Complete current per-family models, including offscreen and
/// slot-ineligible nodes. Cross-family World insertion order is a separate owner.</summary>
public sealed class ModelCurrentPrimitiveInventory
{
    private ModelCurrentPrimitiveInventory(long frame, string view, string publication,
        ModelCurrentPrimitiveModel[] models)
        => (FrameNumber, ViewIdentity, PublicationIdentity, Models) =
            (frame, view, publication, Array.AsReadOnly(models));
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public string PublicationIdentity { get; }
    public IReadOnlyList<ModelCurrentPrimitiveModel> Models { get; }
    public int Category0Count => Models.Sum(model => model.Source.Category0.Count);
    public int Category1Count => Models.Sum(model => model.Source.Category1.Count);

    public static ModelCurrentPrimitiveInventory PrepareCurrentStatic(
        MapStaticModelCurrentOwnedFrame actualFrame, string viewIdentity,
        IReadOnlyDictionary<string, ModelCurrentPrimitiveBinding> completeCurrentGeometryBindings,
        string publicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualFrame); ArgumentNullException.ThrowIfNull(completeCurrentGeometryBindings);
        ValidateIdentity(actualFrame.FrameNumber, viewIdentity, publicationIdentity);
        var instances = actualFrame.Population.Instances;
        if (!actualFrame.Population.Complete || instances.Select(instance => instance.InstanceKey).Distinct(StringComparer.Ordinal).Count() != instances.Count ||
            !instances.Select(instance => instance.InstanceKey).ToHashSet(StringComparer.Ordinal).SetEquals(completeCurrentGeometryBindings.Keys))
            throw new InvalidDataException("Current static inventory requires every complete Population instance and its GeometryInput binding.");
        var models = new List<ModelCurrentPrimitiveModel>(instances.Count);
        foreach (var instance in instances)
        {
            var binding = completeCurrentGeometryBindings[instance.InstanceKey];
            if (string.IsNullOrWhiteSpace(instance.CurrentSourceIdentity) || binding.CurrentSourceIdentity != instance.CurrentSourceIdentity ||
                binding.RetainedSourceModelCpuIsCurrent != true || string.IsNullOrWhiteSpace(binding.CurrentProducerProvenance))
                throw new InvalidDataException("Current static inventory requires the actual node identity and retained original ModelCPU provider.");
            models.Add(new(instance.InstanceKey, binding.CurrentSourceIdentity,
                ModelPrimitiveInventorySource.LoadStatic(instance.Asset), binding.CurrentProducerProvenance));
        }
        return new(actualFrame.FrameNumber, viewIdentity, publicationIdentity, models.ToArray());
    }

    public static ModelCurrentPrimitiveInventory PrepareCurrentArmy(long frameNumber,
        string viewIdentity, IReadOnlyList<ArmyCurrentPrimitiveActor> completeCurrentActors,
        string publicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(completeCurrentActors);
        ValidateIdentity(frameNumber, viewIdentity, publicationIdentity);
        if (completeCurrentActors.Select(actor => actor.ActorIdentity).Distinct(StringComparer.Ordinal).Count() != completeCurrentActors.Count)
            throw new InvalidDataException("Current Army inventory contains duplicate actual actor identities.");
        var models = new List<ModelCurrentPrimitiveModel>(completeCurrentActors.Count);
        foreach (var actor in completeCurrentActors)
        {
            if (string.IsNullOrWhiteSpace(actor.ActorIdentity) || actor.FreshWorldPqMaskAndGeometryFallbackRetained != true ||
                string.IsNullOrWhiteSpace(actor.CurrentProducerProvenance))
                throw new InvalidDataException("Current Army inventory requires the actual retained original ModelCPU provider.");
            // OrdinaryView0Eligible and record exclusions do not filter an inventory.
            models.Add(new(actor.ActorIdentity, actor.ActorIdentity,
                ModelPrimitiveInventorySource.LoadArmy(actor.Source), actor.CurrentProducerProvenance));
        }
        return new(frameNumber, viewIdentity, publicationIdentity, models.ToArray());
    }

    private static void ValidateIdentity(long frame, string view, string publication)
    {
        if (frame <= 0 || string.IsNullOrWhiteSpace(view) || string.IsNullOrWhiteSpace(publication))
            throw new InvalidDataException("Current Model inventory requires its actual frame, View and publication identity.");
    }
}
