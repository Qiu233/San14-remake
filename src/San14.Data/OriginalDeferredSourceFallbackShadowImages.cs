using San14.Data.Formats;

namespace San14.Data;

public enum OriginalDeferredFallbackShadowKind { Point, Spot }
public enum OriginalDeferredFallbackShadowStateScope
{
    CurrentOrEditedState,
    SuccessfulFreshGraphicsInitializationUnassignedCommonSettings
}

/// <summary>Explicit owned source phase. The native initializer only fills an
/// unassigned CommonViewSetting field when it successfully creates and maps that
/// graphics owner's default resource. This token does not observe the original
/// process. A native failed Map does not establish the white-pixel recipe.</summary>
public sealed record OriginalDeferredFallbackShadowConstruction(
    OriginalDeferredFallbackShadowStateScope Scope, string GraphicsOwnerIdentity,
    string CommonSettingsOwnerIdentity, bool? PointSettingWasUnassigned,
    bool? SpotSettingWasUnassigned)
{
    public void Validate()
    {
        if (Scope != OriginalDeferredFallbackShadowStateScope.SuccessfulFreshGraphicsInitializationUnassignedCommonSettings ||
            string.IsNullOrWhiteSpace(GraphicsOwnerIdentity) || string.IsNullOrWhiteSpace(CommonSettingsOwnerIdentity) ||
            PointSettingWasUnassigned != true || SpotSettingWasUnassigned != true)
            throw new InvalidDataException("Source fallback shadows require successful fresh owned graphics initialization and explicitly unassigned CommonViewSetting+340/+330; current overrides are separate producers.");
    }
}

/// <summary>Explicit final descriptor selection, including the source owner's
/// choice of the constructed defaults. Unchanged CommonViewSetting fields alone
/// do not prove these assertions when a live shadow producer can supersede them.</summary>
public sealed record OriginalDeferredFallbackShadowCurrentSelection(string CommonSettingsOwnerIdentity,
    bool? PointStillUsesConstructedDefault, bool? SpotStillUsesConstructedDefault)
{
    public void Validate(OriginalDeferredFallbackShadowConstruction construction)
    {
        construction.Validate();
        if (CommonSettingsOwnerIdentity != construction.CommonSettingsOwnerIdentity ||
            PointStillUsesConstructedDefault != true || SpotStillUsesConstructedDefault != true)
            throw new InvalidDataException("Source fallback images cannot replace unknown, changed, or overridden current CommonViewSetting descriptors.");
    }
}

/// <summary>Owned initializer defaults: white BGRA8, one mip, point CubeArray
/// with six faces and spot Texture2DArray with one layer.</summary>
public sealed class LoadedOriginalDeferredSourceFallbackShadowImages
{
    public LoadedOriginalDeferredSamplerNativeSource Native { get; }
    public string ResourceUri { get; }
    public byte PixelFormat => 1;
    public uint ShaderViewDxgiFormat => 87;
    public uint Width => 1;
    public uint Height => 1;
    public uint Mipmaps => 1;
    public bool ActiveOriginalBindingObserved => false;
    internal LoadedOriginalDeferredSourceFallbackShadowImages(LoadedOriginalDeferredSamplerNativeSource native)
    {
        Native = native;
        ResourceUri = "san14://project/render-rules/default-white-shadow-arrays";
    }
    public uint ArrayLayers(OriginalDeferredFallbackShadowKind kind) => kind switch
    { OriginalDeferredFallbackShadowKind.Point => 6, OriginalDeferredFallbackShadowKind.Spot => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    public uint RdefDimension(OriginalDeferredFallbackShadowKind kind) => kind switch
    { OriginalDeferredFallbackShadowKind.Point => 10, OriginalDeferredFallbackShadowKind.Spot => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)) };
    public byte[] LayerPixels(OriginalDeferredFallbackShadowKind kind)
    { _ = ArrayLayers(kind); return [255, 255, 255, 255]; }
    public string ImageUri(OriginalDeferredFallbackShadowKind kind, OriginalDeferredFallbackShadowConstruction construction)
    {
        construction.Validate(); _ = ArrayLayers(kind);
        return ResourceUri + $";kind={kind};graphics={construction.GraphicsOwnerIdentity};settings={construction.CommonSettingsOwnerIdentity};unassigned-field={(kind == OriginalDeferredFallbackShadowKind.Point ? "340" : "330")};fresh-successful-initialization";
    }
    public void ValidateConsumer(OriginalDeferredFallbackShadowKind kind, DxbcRdefBinding consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        string name = kind == OriginalDeferredFallbackShadowKind.Point ? "sSplShadow" : "sSslShadow";
        if (consumer.InputType != 2 || consumer.ReturnType != 5 || consumer.Dimension != RdefDimension(kind) ||
            consumer.BindCount != 1 || consumer.Name.Text != name)
            throw new InvalidDataException("Fallback shadow image differs from the selected reflected float array consumer.");
    }
    public OriginalDeferredSamplerSourceInput SamplerInput(OriginalDeferredFallbackShadowKind kind,
        DxbcRdefBinding textureConsumer, DxbcRdefBinding samplerConsumer,
        OriginalDeferredFallbackShadowConstruction construction,
        OriginalDeferredFallbackShadowCurrentSelection current, uint currentGlobalOwnerCount)
    {
        ArgumentNullException.ThrowIfNull(current); ArgumentNullException.ThrowIfNull(samplerConsumer);
        ValidateConsumer(kind, textureConsumer); current.Validate(construction);
        if (samplerConsumer.InputType != 3 || samplerConsumer.Dimension != 0 || samplerConsumer.BindCount != 1 ||
            samplerConsumer.Name.Text != "__smp" + textureConsumer.Name.Text)
            throw new InvalidDataException("Fallback shadow sampler requires its selected reflected sampler consumer.");
        byte[] original = kind == OriginalDeferredFallbackShadowKind.Point
            ? OriginalDeferredSamplerSources.CubeTextureKey(Native, 1)
            : OriginalDeferredSamplerSources.RenderTextureKey(Native, 1);
        byte[] final = OriginalDeferredSamplerSources.SubShadowKey(original, 2, Math.Min(currentGlobalOwnerCount, 256u));
        var recipe = OriginalDeferredSamplerSources.DecodeKey(Native, final);
        if (final.Length != 8 || (recipe.Filter & 0x80) == 0)
            throw new InvalidDataException("Fallback shadow preflight lost its original Filter2 comparison key.");
        return new(samplerConsumer.BindPoint, textureConsumer.Name.Text,
            ImageUri(kind, construction) + $";Sub{kind}LightShadows-Filter2;count={Math.Min(currentGlobalOwnerCount, 256u)}",
            Native.RenderRuleIdentity, final);
    }
}

public static class OriginalDeferredSourceFallbackShadowImages
{
    public static LoadedOriginalDeferredSourceFallbackShadowImages Load(OriginalGameRepository repository, string language)
    { ArgumentNullException.ThrowIfNull(repository); return Load(repository.SourceDirectory, language); }
    public static LoadedOriginalDeferredSourceFallbackShadowImages Load(string sourceDirectory, string language)
    {
        var native = OriginalDeferredSamplerSources.Load(sourceDirectory, language);
        return new(native);
    }
}
