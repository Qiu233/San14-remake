using San14.Data;

/// <summary>The actual complete source regular texture-group record. Every
/// table slot retains its static image or actual producer image/sampler; a
/// TextPane primary alone cannot create this record. It is independent of
/// shader-table and material parameter-record ownership.</summary>
public sealed class NativeArmyFlagMaterialTextureGroupRecord : INativeArmyMaterialTextureTypedRecord
{
    private readonly Dictionary<int, LoadedArmyFlagMaterialImage> staticImages = [];
    private readonly Dictionary<uint, RemakeArmyFlagRuntimeTexture> runtimeImages;
    private readonly Dictionary<(LoadedShaderContainer Shader, int Combination), IReadOnlyList<ArmyFlagMaterialTextureUse>> uses = [];
    private bool retained = true;
    private NativeArmyFlagMaterialTextureGroupRecord(GameResourceRepository repository,
        LoadedArmyFlagMaterialVariation source, LoadedShaderContainer actualShader, int actualCombination,
        IReadOnlyList<ArmyFlagMaterialTextureUse> actualUses,
        IReadOnlyDictionary<uint, RemakeArmyFlagRuntimeTexture> actualRuntimeImages)
    {
        Source = source; runtimeImages = new(actualRuntimeImages);
        var initialUses = actualUses.GroupBy(u => u.Slot.Index).ToDictionary(g => g.Key, g => g.First());
        if (!initialUses.Keys.ToHashSet().SetEquals(source.Slots.Select(s => s.Index)))
            throw new NotSupportedException("Complete Army material group requires each actual source slot, including unconsumed slots.");
        foreach (var slot in source.Slots)
        {
            if (slot.RequiresProducerImage)
            {
                if (!runtimeImages.TryGetValue(slot.ProxyObjectId, out var actual))
                    throw new InvalidDataException("Complete Army texture group is missing its actual runtime slot owner.");
                RequireRuntime(actual, slot.ProxyObjectId);
            }
            else staticImages.Add(slot.Index, ArmyFlagMaterialVariations.LoadStaticImage(repository, initialUses[slot.Index]));
        }
        uses.Add((actualShader, actualCombination), actualUses);
    }
    public LoadedArmyFlagMaterialVariation Source { get; }
    public int ActualSlotCount => Source.Slots.Count;
    public static NativeArmyFlagMaterialTextureGroupRecord LoadComplete(
        GameResourceRepository repository, LoadedArmyFlagMaterialVariation source,
        LoadedShaderContainer actualShader, int actualCombination, IReadOnlyList<ArmyFlagMaterialTextureUse> actualUses,
        IReadOnlyDictionary<uint, RemakeArmyFlagRuntimeTexture> actualRuntimeImages)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(actualShader); ArgumentNullException.ThrowIfNull(actualUses);
        ArgumentNullException.ThrowIfNull(actualRuntimeImages);
        return new(repository, source, actualShader, actualCombination, actualUses, actualRuntimeImages);
    }
    public NativeArmyMaterialTextureResource RegisterActualReadyData(NativeArmyMaterialTextureRegistry actualDirectory)
        => actualDirectory.RegisterReadyTextureGroup(Source.Materials, Source.TextureGroup, () => this, RequireCurrent);
    public void RequireCurrent()
    {
        if (!retained) throw new InvalidDataException("Army material texture-group record was retired or replaced by another resource writer.");
        foreach (var slot in Source.Slots.Where(s => s.RequiresProducerImage)) RequireRuntime(runtimeImages[slot.ProxyObjectId], slot.ProxyObjectId);
    }
    /// <summary>Publish a completed producer binding without replacing the
    /// regular group retained by a material. Prepared packets already own
    /// their detached binding lists; their image owners remain retained.</summary>
    public void PublishRuntimeImage(RemakeArmyFlagRuntimeTexture actual)
    {
        if (!retained) throw new InvalidDataException("Army material texture group was retired.");
        if (!Source.Slots.Any(slot => slot.RequiresProducerImage && slot.ProxyObjectId == actual.ProxyObjectId))
            throw new InvalidDataException("Runtime image does not belong to this material texture group.");
        RequireRuntime(actual, actual.ProxyObjectId);
        runtimeImages[actual.ProxyObjectId] = actual;
    }
    public NativeArmyMaterialPixelTextureInputs PrepareActualPixelBindings(LoadedShaderContainer actualShader, int actualCombination)
    {
        RequireCurrent(); ArgumentNullException.ThrowIfNull(actualShader);
        if (!uses.TryGetValue((actualShader, actualCombination), out var selected))
            uses.Add((actualShader, actualCombination), selected = ArmyFlagMaterialVariations.ResolveTextureUsesForActualShader(Source, actualShader, actualCombination));
        var owned = new List<ModelGbufferTextureUse>();
        var borrowed = new List<ModelDeviceBinding>();
        foreach (var use in selected)
        {
            if (use.Slot.RequiresProducerImage)
            {
                var actual = runtimeImages[use.Slot.ProxyObjectId];
                borrowed.Add(actual.Texture with { SourceBindPoint = use.TextureRegister });
                borrowed.Add(actual.Sampler with { SourceBindPoint = use.SamplerRegister });
            }
            else
            {
                var image = staticImages[use.Slot.Index];
                byte[] sampler = image.Sampler.EncodeD3D11Descriptor();
                owned.Add(new("Texture", use.TextureRegister, image.Container, image.Image.Index, image.NativeViewFormat, sampler));
                owned.Add(new("Sampler", use.SamplerRegister, image.Container, image.Image.Index, image.NativeViewFormat, sampler));
            }
        }
        return new(owned.AsReadOnly(), borrowed.AsReadOnly());
    }
    public void Invalidate(string actualResourceWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualResourceWriter); retained = false; }
    private static void RequireRuntime(RemakeArmyFlagRuntimeTexture actual, uint proxy)
    {
        if (actual.ProxyObjectId != proxy || actual.RequireActualCurrentOwner is not { } current ||
            actual.Texture.DescriptorKind != "Texture" || actual.Sampler.DescriptorKind != "Sampler" ||
            !actual.Texture.Resource.IsValid || actual.Texture.OwnerTexture is not { IsValid: true } || !actual.Sampler.Resource.IsValid)
            throw new InvalidDataException("Army material runtime slot lacks its actual complete image/sampler owner.");
        current();
    }
}

public sealed record NativeArmyMaterialPixelTextureInputs(
    IReadOnlyList<ModelGbufferTextureUse> Owned, IReadOnlyList<ModelDeviceBinding> Borrowed);
