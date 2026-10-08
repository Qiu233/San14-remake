using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>One original ModelCPU selector. Draw references retain the native
/// part/group order; this is a complete source inventory before View culling.</summary>
public sealed record OriginalModelPrimitiveCategoryInventory(int Category,
    IReadOnlyList<int> SourceDrawIndices)
{
    public int Count => SourceDrawIndices.Count;
    public int PrimitiveStride => 0x70;
}

/// <summary>Loaded, finite original ModelCPU inventory. C222A0 always returns
/// positive zero, so the current distance does not select a different LOD row
/// for this provider. Optional/current GeometryInput providers are separate.</summary>
public sealed class OriginalModelPrimitiveInventorySource
{
    private static readonly ConditionalWeakTable<OriginalMapStaticModelAsset, OriginalModelPrimitiveInventorySource> StaticSources = new();
    private static readonly ConditionalWeakTable<OriginalArmyPrimitivePublicationSource, OriginalModelPrimitiveInventorySource> ArmySources = new();
    private readonly OriginalModelPrimitiveCategoryInventory[] categories;

    private OriginalModelPrimitiveInventorySource(string identity, int[] first,
        OriginalWorldPrimitiveModelDistance declaredDistance)
    {
        SourceIdentity = identity; DeclaredDistance = declaredDistance;
        categories = [new(0, Array.AsReadOnly(first)), new(1, Array.AsReadOnly(Array.Empty<int>()))];
    }

    public string SourceIdentity { get; }
    public OriginalWorldPrimitiveModelDistance DeclaredDistance { get; }
    public string SourceProvenance => SourceIdentity +
        ";TC:ModelVT16B1E58/+08=C222A0/+10=C240C0/+18=C193B0;" +
        "ABB360/section10009;F587F0/F57390;D600A0;one-part-classification0-first-group-order";

    /// <summary>Returns the actual provider's key, including when its unused
    /// distance argument is negative or nonfinite. It is not a tuple sort key.</summary>
    public static float ReadModelCacheKey(float actualModelDistance) => 0f;

    public OriginalModelPrimitiveCategoryInventory SelectCurrent(int category, float actualModelDistance)
    {
        if ((uint)category >= categories.Length)
            throw new NotSupportedException("The complete early19 Model inventory has selectors0 and1.");
        _ = ReadModelCacheKey(actualModelDistance);
        return categories[category];
    }

    public OriginalModelPrimitiveCategoryInventory Category0 => categories[0];
    public OriginalModelPrimitiveCategoryInventory Category1 => categories[1];

    /// <summary>Import/reload boundary. The cached source is reused without
    /// rereading RDB data, hashing bytes or comparing full arrays per request.</summary>
    public static OriginalModelPrimitiveInventorySource LoadStatic(OriginalMapStaticModelAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return StaticSources.GetValue(asset, static actual =>
        {
            var source = ReadLoaded(actual.RawTransforms.Source, actual.Reference.Dictionary, actual.Reference.ModelObject.ObjectId);
            var geometry = G1ModelGeometryReader.Read(actual.RawTransforms.Source.Bytes, actual.RawTransforms.Source.Bytes.LongLength);
            var draws = G1ModelDrawReader.Read(geometry).Draws;
            if (actual.Reference.ModelFileId != actual.RawTransforms.Source.FileId || actual.Draws.Count != draws.Count ||
                actual.Draws.Where((draw, index) => draw.Index != draws[index].Index ||
                    !draw.RawBytes.AsSpan().SequenceEqual(draws[index].RawBytes)).Any())
                throw new InvalidDataException("Static inventory asset does not retain its actual source draw directory.");
            return source;
        });
    }

    public static OriginalModelPrimitiveInventorySource LoadArmy(OriginalArmyPrimitivePublicationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ArmySources.GetValue(source, static actual =>
        {
            actual.ValidateSourceUnchanged();
            var loaded = ReadLoaded(actual.Model, actual.Catalog.Dictionary, actual.Category.ModelObjectId);
            if (!loaded.Category0.SourceDrawIndices.SequenceEqual(actual.OrdinarySourceDrawIndices))
                throw new InvalidDataException("Army inventory differs from its actual ordinary ModelCPU group order.");
            return loaded;
        });
    }

    /// <summary>Read once from the actual selected dictionary/model sources.
    /// This finite implementation accepts the proven one-part category0 profile;
    /// an unsupported profile never becomes a synthetic empty category1.</summary>
    public static OriginalModelPrimitiveInventorySource ReadLoaded(LoadedRdbAsset modelSource,
        LoadedObjects dictionary, uint selectedModelObjectId)
    {
        ArgumentNullException.ThrowIfNull(modelSource); ArgumentNullException.ThrowIfNull(dictionary);
        Verify(modelSource); Verify(dictionary.Source);
        var model = DokReader.Read(dictionary.Source.Bytes).Objects.Single(value =>
            value.Kind == DokObjectKind.Inline && value.ClassId == 0xD40B3C8F && value.ObjectId == selectedModelObjectId);
        var file = model.Properties.Single(property => property.NameHash == 0x8AB68B3F);
        if (file.TypeCode != 5 || file.ElementCount != 1 || file.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(file.RawValue) != modelSource.FileId)
            throw new InvalidDataException("Model inventory dictionary does not select this actual geometry resource.");
        RequireZero(24, 0x07AAF542, 0);
        RequireZero(25, 0x0B534ABC, 1);
        RequireZero(41, 0x95424354, 1);
        var geometry = G1ModelGeometryReader.Read(modelSource.Bytes, modelSource.Bytes.LongLength);
        var bindings = G1ModelDrawReader.Read(geometry);
        var part = geometry.Sections.Single(section => section.Id == 0x10009);
        int[] first = bindings.Groups.SelectMany(group => group.DrawIndices).ToArray();
        if (part.ItemCount != 1 || part.RawBytes.Length < 48 ||
            (BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(16)) & 0x7FFFFFFF) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(24)) != bindings.Groups.Count ||
            BinaryPrimitives.ReadUInt32LittleEndian(part.RawBytes.AsSpan(28)) != 0 ||
            bindings.Draws.Any(draw => draw.UnknownValue32 != 1) || first.Length != bindings.Draws.Count ||
            first.Distinct().Count() != first.Length)
            throw new NotSupportedException("Model inventory requires the actual complete unique one-part LOD0/classification0 first-group profile.");
        string identity = dictionary.Source.ResourceUri + "@" + dictionary.Source.DecodedSha256 +
            "/" + selectedModelObjectId.ToString("X8") + "|" + modelSource.ResourceUri + "@" + modelSource.DecodedSha256;
        return new(identity, first, OriginalWorldPrimitiveDistanceKey.ReadModelDeclaration(model));

        void RequireZero(int ordinal, uint hash, int count)
        {
            var property = model.Properties[ordinal];
            if (property.Index != ordinal || property.NameHash != hash || property.TypeCode != 5 ||
                property.ElementCount != count || property.RawValue.Length != count * 4 ||
                count == 1 && BinaryPrimitives.ReadUInt32LittleEndian(property.RawValue) != 0)
                throw new NotSupportedException("Model inventory requires the actual source null G1ii dependency and absent shader swaps.");
        }
    }

    private static void Verify(LoadedRdbAsset source)
    {
        if (string.IsNullOrWhiteSpace(source.ResourceUri) ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != source.DecodedSha256)
            throw new InvalidDataException("Model inventory load source bytes differ from their declared identity.");
    }
}

/// <summary>The current owner explicitly retains the canonical ModelCPU
/// GeometryInput. Unknown or replaced providers require their own inventory.</summary>
public sealed record OriginalModelCurrentPrimitiveBinding(string CurrentSourceIdentity,
    bool? RetainedSourceModelCpuIsCurrent, string CurrentProducerProvenance);

public sealed record OriginalModelCurrentPrimitiveModel(string ModelIdentity, string CurrentSourceIdentity,
    OriginalModelPrimitiveInventorySource Source, string CurrentProducerProvenance);

/// <summary>Complete current per-family models, including offscreen and
/// slot-ineligible nodes. Cross-family World insertion order is a separate owner.</summary>
public sealed class OriginalModelCurrentPrimitiveInventory
{
    private OriginalModelCurrentPrimitiveInventory(long frame, string view, string publication,
        OriginalModelCurrentPrimitiveModel[] models)
        => (FrameNumber, ViewIdentity, PublicationIdentity, Models) =
            (frame, view, publication, Array.AsReadOnly(models));
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public string PublicationIdentity { get; }
    public IReadOnlyList<OriginalModelCurrentPrimitiveModel> Models { get; }
    public int Category0Count => Models.Sum(model => model.Source.Category0.Count);
    public int Category1Count => Models.Sum(model => model.Source.Category1.Count);

    public static OriginalModelCurrentPrimitiveInventory PrepareCurrentStatic(
        OriginalMapStaticModelCurrentOwnedFrame actualFrame, string viewIdentity,
        IReadOnlyDictionary<string, OriginalModelCurrentPrimitiveBinding> completeCurrentGeometryBindings,
        string publicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(actualFrame); ArgumentNullException.ThrowIfNull(completeCurrentGeometryBindings);
        ValidateIdentity(actualFrame.FrameNumber, viewIdentity, publicationIdentity);
        var instances = actualFrame.Population.Instances;
        if (!actualFrame.Population.Complete || instances.Select(instance => instance.InstanceKey).Distinct(StringComparer.Ordinal).Count() != instances.Count ||
            !instances.Select(instance => instance.InstanceKey).ToHashSet(StringComparer.Ordinal).SetEquals(completeCurrentGeometryBindings.Keys))
            throw new InvalidDataException("Current static inventory requires every complete Population instance and its GeometryInput binding.");
        var models = new List<OriginalModelCurrentPrimitiveModel>(instances.Count);
        foreach (var instance in instances)
        {
            var binding = completeCurrentGeometryBindings[instance.InstanceKey];
            if (string.IsNullOrWhiteSpace(instance.CurrentSourceIdentity) || binding.CurrentSourceIdentity != instance.CurrentSourceIdentity ||
                binding.RetainedSourceModelCpuIsCurrent != true || string.IsNullOrWhiteSpace(binding.CurrentProducerProvenance))
                throw new InvalidDataException("Current static inventory requires the actual node identity and retained original ModelCPU provider.");
            models.Add(new(instance.InstanceKey, binding.CurrentSourceIdentity,
                OriginalModelPrimitiveInventorySource.LoadStatic(instance.Asset), binding.CurrentProducerProvenance));
        }
        return new(actualFrame.FrameNumber, viewIdentity, publicationIdentity, models.ToArray());
    }

    public static OriginalModelCurrentPrimitiveInventory PrepareCurrentArmy(long frameNumber,
        string viewIdentity, IReadOnlyList<OriginalArmyCurrentPrimitiveActor> completeCurrentActors,
        string publicationIdentity)
    {
        ArgumentNullException.ThrowIfNull(completeCurrentActors);
        ValidateIdentity(frameNumber, viewIdentity, publicationIdentity);
        if (completeCurrentActors.Select(actor => actor.ActorIdentity).Distinct(StringComparer.Ordinal).Count() != completeCurrentActors.Count)
            throw new InvalidDataException("Current Army inventory contains duplicate actual actor identities.");
        var models = new List<OriginalModelCurrentPrimitiveModel>(completeCurrentActors.Count);
        foreach (var actor in completeCurrentActors)
        {
            if (string.IsNullOrWhiteSpace(actor.ActorIdentity) || actor.FreshWorldPqMaskAndGeometryFallbackRetained != true ||
                string.IsNullOrWhiteSpace(actor.CurrentProducerProvenance))
                throw new InvalidDataException("Current Army inventory requires the actual retained original ModelCPU provider.");
            // OrdinaryView0Eligible and record exclusions do not filter an inventory.
            models.Add(new(actor.ActorIdentity, actor.ActorIdentity,
                OriginalModelPrimitiveInventorySource.LoadArmy(actor.Source), actor.CurrentProducerProvenance));
        }
        return new(frameNumber, viewIdentity, publicationIdentity, models.ToArray());
    }

    private static void ValidateIdentity(long frame, string view, string publication)
    {
        if (frame <= 0 || string.IsNullOrWhiteSpace(view) || string.IsNullOrWhiteSpace(publication))
            throw new InvalidDataException("Current Model inventory requires its actual frame, View and publication identity.");
    }
}
