using San14.Data;

/// <summary>The actual complete source regular texture-group record. Every
/// table slot retains its static image or actual producer image/sampler; a
/// TextPane primary alone cannot create this record. It is independent of
/// shader-table and material parameter-record ownership.</summary>
public sealed class OriginalNativeArmyFlagMaterialTextureGroupRecord : IOriginalNativeArmyMaterialTextureTypedRecord
{
    private readonly Dictionary<int, LoadedOriginalArmyFlagMaterialImage> staticImages = [];
    private readonly Dictionary<uint, OriginalRemakeArmyFlagRuntimeTexture> runtimeImages;
    private readonly Dictionary<LoadedShaderContainer, IReadOnlyList<OriginalArmyFlagMaterialTextureUse>> uses = [];
    private bool retained = true;
    private OriginalNativeArmyFlagMaterialTextureGroupRecord(OriginalGameRepository repository,
        LoadedOriginalArmyFlagMaterialVariation source,
        IReadOnlyDictionary<uint, OriginalRemakeArmyFlagRuntimeTexture> actualRuntimeImages)
    {
        Source = source; runtimeImages = new(actualRuntimeImages);
        var initialUses = source.TextureUses.GroupBy(u => u.Slot.Index).ToDictionary(g => g.Key, g => g.First());
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
            else staticImages.Add(slot.Index, OriginalArmyFlagMaterialVariations.LoadStaticImage(repository, initialUses[slot.Index]));
        }
        uses.Add(source.Shader, source.TextureUses);
    }
    public LoadedOriginalArmyFlagMaterialVariation Source { get; }
    public int ActualSlotCount => Source.Slots.Count;
    public static OriginalNativeArmyFlagMaterialTextureGroupRecord LoadComplete(
        OriginalGameRepository repository, LoadedOriginalArmyFlagMaterialVariation source,
        IReadOnlyDictionary<uint, OriginalRemakeArmyFlagRuntimeTexture> actualRuntimeImages)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(actualRuntimeImages); return new(repository, source, actualRuntimeImages);
    }
    public OriginalNativeArmyMaterialTextureResource RegisterActualReadyData(OriginalNativeArmyMaterialTextureRegistry actualDirectory)
        => actualDirectory.RegisterReadyTextureGroup(Source.Materials, Source.TextureGroup, () => this, RequireCurrent);
    public void RequireCurrent()
    {
        if (!retained) throw new InvalidDataException("Army material texture-group record was retired or replaced by another resource writer.");
        foreach (var slot in Source.Slots.Where(s => s.RequiresProducerImage)) RequireRuntime(runtimeImages[slot.ProxyObjectId], slot.ProxyObjectId);
    }
    public OriginalNativeArmyMaterialPixelTextureInputs PrepareActualPixelBindings(LoadedShaderContainer actualShader)
    {
        RequireCurrent(); ArgumentNullException.ThrowIfNull(actualShader);
        if (!uses.TryGetValue(actualShader, out var selected))
            uses.Add(actualShader, selected = OriginalArmyFlagMaterialVariations.ResolveTextureUsesForActualShader(Source, actualShader));
        var owned = new List<OriginalModelGbufferTextureUse>();
        var borrowed = new List<OriginalModelDeviceBinding>();
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
    private static void RequireRuntime(OriginalRemakeArmyFlagRuntimeTexture actual, uint proxy)
    {
        if (actual.ProxyObjectId != proxy || actual.RequireActualCurrentOwner is not { } current ||
            actual.Texture.DescriptorKind != "Texture" || actual.Sampler.DescriptorKind != "Sampler" ||
            !actual.Texture.Resource.IsValid || actual.Texture.OwnerTexture is not { IsValid: true } || !actual.Sampler.Resource.IsValid)
            throw new InvalidDataException("Army material runtime slot lacks its actual complete image/sampler owner.");
        current();
    }
}

public sealed record OriginalNativeArmyMaterialPixelTextureInputs(
    IReadOnlyList<OriginalModelGbufferTextureUse> Owned, IReadOnlyList<OriginalModelDeviceBinding> Borrowed);
