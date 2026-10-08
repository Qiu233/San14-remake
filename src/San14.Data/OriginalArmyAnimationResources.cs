using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalArmyAnimationChannel(int DescriptorOffsetDwords,
    ushort Type, IReadOnlyList<ushort> KeyTimes, uint FirstCoefficientIndex);

/// <summary>Verified packed G2A model channels, with the original native pre-0500 table conversion.</summary>
public sealed record OriginalArmyAnimationClip(uint Version, float FrameRate,
    ushort FrameCount, IReadOnlyList<uint> PackedJointChannels,
    IReadOnlyList<OriginalArmyAnimationChannel> Channels, byte[] PackedCoefficients,
    byte[] RawHeader)
{
    public IReadOnlyList<uint> RawPackedJointChannels { get; init; } = PackedJointChannels;
}

public sealed record LoadedOriginalArmyAnimationClip(DokObject AnimationDataObject,
    OriginalArmyAnimationClip Clip, LoadedRdbAsset Source);

public sealed record LoadedOriginalArmyAnimationAction(OriginalCeArmyModelCategory Category,
    LoadedObjects Catalog, LoadedObjects Singleton, DokObject Setting,
    int ActionIndex, DokObject ActionObject, uint RawActionId,
    IReadOnlyList<LoadedOriginalArmyAnimationClip> AnimationData);

/// <summary>
/// Resolves the source Army setting, SequentialPlayer and G1AFileResourceHash graph.
/// Supported installed packed files use _A2G0300 or _A2G0500. Reads retain source provenance; selection,
/// looping, model defaults and current activity are separate explicit inputs.
/// </summary>
public static class OriginalArmyAnimationResources
{
    public const uint SingletonId = 0xD14D76EF;

    public static LoadedOriginalArmyAnimationAction LoadAction(OriginalGameRepository repository,
        int category, int actionIndex) =>
        LoadAction(repository, OriginalCeArmyModels.LoadCatalog(repository), category, actionIndex);

    public static LoadedOriginalArmyAnimationAction LoadAction(OriginalGameRepository repository,
        LoadedOriginalArmyCatalog catalog, int category, int actionIndex)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalog);
        if ((uint)category >= OriginalCeArmyModels.KnownSourceCategories.Count)
            throw new InvalidDataException($"Army animation category {category} has no source-backed identity.");
        var known = OriginalCeArmyModels.KnownSourceCategories[category];
        string context = catalog.Catalog.Source.ResourceUri;
        if (context != GameResourceId.Rdb("KIDSSystemResource", OriginalCeArmyModels.CatalogId).ToString())
            throw Error(context, "requires the original Army catalog document");
        var definitions = catalog.Definitions.Where(item => item.Definition.ObjectId == known.DefinitionId).ToArray();
        if (definitions.Length != 1 || definitions[0].ModelObject.ObjectId != known.ModelObjectId ||
            definitions[0].ModelFileId != known.ModelFileId)
            throw Error(context, $"category {category} model identity differs from the supported source profile");
        DokObject[] settings = catalog.Catalog.Document.Objects.Where(item =>
            item.Kind == DokObjectKind.Inline && item.ClassId == 0xD186CEEB &&
            Scalar(item, 0x68A6F779, context) == known.DefinitionId).ToArray();
        if (settings.Length != 1)
            throw Error(context, $"category {category} requires one MotorCharacterSetting for definition {known.DefinitionId:X8}");
        var setting = settings[0];
        uint[] actionIds = Values(setting, 0x7245DD82, context);
        if ((uint)actionIndex >= actionIds.Length)
            throw Error(context, $"action index {actionIndex} is outside category {category}'s declared action array");
        LoadedObjects singleton = repository.LoadObjects("KIDSSystemResource", SingletonId);
        string actionContext = singleton.Source.ResourceUri;
        DokObject action = Unique(singleton, actionIds[actionIndex], actionContext);
        if (action.Kind != DokObjectKind.Inline || action.ClassId != 0x11A85121)
            throw Error(actionContext, $"action {action.ObjectId:X8} is not a supported inline SequentialPlayer");
        uint[] animationIds = Values(action, 0x9E1E3953, actionContext);
        if (animationIds.Length == 0)
            throw Error(actionContext, $"action {action.ObjectId:X8} has no declared AnimationDataObjectNameHashArray");
        var animationData = new List<LoadedOriginalArmyAnimationClip>(animationIds.Length);
        foreach (uint animationId in animationIds)
        {
            DokObject data = Unique(singleton, animationId, actionContext);
            if (data.Kind != DokObjectKind.Inline || data.ClassId != 0x34F2EC8B)
                throw Error(actionContext, $"animation data {animationId:X8} is not a supported inline G1A channel data object");
            uint fileId = Scalar(data, 0x1927F731, actionContext);
            LoadedRdbAsset source = repository.LoadRdbAsset("RRPreview", fileId);
            OriginalArmyAnimationClip clip;
            try { clip = Read(source.Bytes); }
            catch (InvalidDataException error)
            { throw new InvalidDataException($"Army action {action.ObjectId:X8} data {animationId:X8} failed to read {source.ResourceUri}: {error.Message}", error); }
            animationData.Add(new(data, clip, source));
        }
        return new(known, catalog.Catalog, singleton, setting, actionIndex, action,
            Scalar(action, 0x05D92ACC, actionContext), animationData.AsReadOnly());
    }

    public static OriginalArmyAnimationClip Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 28 || !bytes[..4].SequenceEqual("_A2G"u8) ||
            (U32(bytes, 4) != 0x30303330 && U32(bytes, 4) != 0x30303530))
            throw new InvalidDataException("Unsupported Army animation format: requires original _A2G0300 or _A2G0500 packed channels.");
        uint version = U32(bytes, 4);
        int headerSize = version == 0x30303530 ? 32 : 28;
        if (bytes.Length < headerSize || U32(bytes, 8) != bytes.Length ||
            (headerSize == 32 && U32(bytes, 28) != 0))
            throw new InvalidDataException("Army G2A length or reserved header field is invalid.");
        float rate = BitConverter.Int32BitsToSingle(unchecked((int)U32(bytes, 12)));
        if (!float.IsFinite(rate) || rate < 0)
            throw new InvalidDataException("Army G2A frame rate is not finite and nonnegative.");
        ushort frames = U16(bytes, 16), packedCount = U16(bytes, 18);
        if ((packedCount & 15) != 0)
            throw new InvalidDataException("Army G2A packed table header flags are unsupported.");
        int count = packedCount >> 4;
        uint rawTimingSize = U32(bytes, 20), rawCoefficientCount = U32(bytes, 24);
        long descriptorBase = headerSize + 4L * count;
        long coefficientsBase = descriptorBase + rawTimingSize;
        if ((rawTimingSize & 3) != 0 || coefficientsBase > bytes.Length ||
            rawCoefficientCount * 32L != bytes.Length - coefficientsBase)
            throw new InvalidDataException("Army G2A channel and coefficient section lengths are invalid.");
        int timingSize = (int)rawTimingSize, timingBase = (int)descriptorBase;
        var packed = new uint[count];
        var rawPacked = new uint[count];
        var channels = new SortedDictionary<int, OriginalArmyAnimationChannel>();
        var joints = new HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            uint rawWord = rawPacked[i] = U32(bytes, headerSize + i * 4);
            uint word = packed[i] = version == 0x30303530 ? rawWord :
                ((rawWord >> 16) << 14) | (rawWord & 0x3FFF);
            int joint = (int)((word >> 4) & 0x3FF), channelCount = (int)(word & 15);
            if (!joints.Add(joint))
                throw new InvalidDataException($"Army G2A repeats joint {joint}.");
            int offset = (int)(word >> 14) * 4;
            for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
            {
                if (offset > timingSize - 8)
                    throw new InvalidDataException($"Army G2A joint {joint} descriptor is outside its channel section.");
                ushort type = U16(bytes, timingBase + offset), keyCount = U16(bytes, timingBase + offset + 2);
                uint coefficient = U32(bytes, timingBase + offset + 4);
                int length = 8 + 4 * ((keyCount + 1) >> 1);
                if (length > timingSize - offset || coefficient >= rawCoefficientCount ||
                    Math.Max(1, (int)keyCount) > rawCoefficientCount - coefficient)
                    throw new InvalidDataException($"Army G2A joint {joint} key/coefficient range is invalid.");
                var times = new ushort[keyCount];
                for (int k = 0; k < keyCount; k++) times[k] = U16(bytes, timingBase + offset + 8 + 2 * k);
                var channel = new OriginalArmyAnimationChannel(offset / 4, type,
                    Array.AsReadOnly(times), coefficient);
                if (channels.TryGetValue(offset / 4, out var prior))
                {
                    if (prior.Type != type || prior.FirstCoefficientIndex != coefficient || !prior.KeyTimes.SequenceEqual(times))
                        throw new InvalidDataException("Army G2A shared descriptor has inconsistent bytes.");
                }
                else channels.Add(offset / 4, channel);
                offset += length;
            }
        }
        return new(version, rate, frames, Array.AsReadOnly(packed),
            Array.AsReadOnly(channels.Values.ToArray()), bytes[(int)coefficientsBase..].ToArray(), bytes[..headerSize].ToArray())
        { RawPackedJointChannels = Array.AsReadOnly(rawPacked) };
    }

    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));

    private static uint[] Values(DokObject item, uint key, string context)
    {
        var properties = item.Properties.Where(property => property.NameHash == key).ToArray();
        if (properties.Length != 1 || properties[0].TypeCode != 5 || properties[0].ElementSize != 4 ||
            properties[0].ElementCount < 0 || properties[0].RawValue.Length != 4L * properties[0].ElementCount)
            throw Error(context, $"object {item.ObjectId:X8} requires one type-5 property {key:X8}");
        var result = new uint[properties[0].ElementCount];
        for (int i = 0; i < result.Length; i++) result[i] = U32(properties[0].RawValue, i * 4);
        return result;
    }
    private static uint Scalar(DokObject item, uint key, string context)
    {
        uint[] values = Values(item, key, context);
        if (values.Length != 1) throw Error(context, $"object {item.ObjectId:X8} property {key:X8} requires one scalar");
        return values[0];
    }
    private static DokObject Unique(LoadedObjects document, uint id, string context)
    {
        var objects = document.Document.Objects.Where(item => item.ObjectId == id).ToArray();
        if (objects.Length != 1) throw Error(context, $"object {id:X8} is missing or ambiguous");
        return objects[0];
    }
    private static InvalidDataException Error(string context, string message) => new($"Army animation {context}: {message}.");
}
