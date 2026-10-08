using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
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

/// <summary>Pinned successful initializer recipes: white BGRA8, one mip, point
/// CubeArray with six faces and spot Texture2DArray with one layer. Original
/// array SRV creation, mapping/fill/unmapping, typed settings and preflight are
/// verified as inert source bytes. Complete shadow CB state stays independent.</summary>
public sealed class LoadedOriginalDeferredSourceFallbackShadowImages
{
    public LoadedOriginalDeferredSamplerNativeSource Native { get; }
    public string ResourceUri { get; }
    public IReadOnlyDictionary<string, string> WindowSha256 { get; }
    public byte PixelFormat => 1;
    public uint ShaderViewDxgiFormat => 87;
    public uint Width => 1;
    public uint Height => 1;
    public uint Mipmaps => 1;
    public bool ActiveOriginalBindingObserved => false;
    internal LoadedOriginalDeferredSourceFallbackShadowImages(LoadedOriginalDeferredSamplerNativeSource native,
        IReadOnlyDictionary<string, string> windows)
    {
        Native = native; WindowSha256 = windows;
        ResourceUri = GameResourceId.File($"SAN14CE_{native.Locale}.exe").ToString() +
            "#successful-graphics-initializer/CommonViewSetting-EF3D849D/white-shadow-arrays";
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
            Native.ExecutableSha256, final);
    }
}

public static class OriginalDeferredSourceFallbackShadowImages
{
    private sealed record Window(string Name, int Rva, int Bytes, string Sha256);
    private static Window[] Profile(string language) => language switch
    {
        "TC" => [
            new("settingsLookup", 0xBFEAC0, 38, "1C6531E3B42D336385082A78AAE1CA89E5B3C1F81E088C1CFE8E16B6F2D54701"),
            new("selectedSettings", 0xBFBDBC, 16, "A36FED28099C9330082D610F8FF1AE19FFB2D38DBE872830FAC26B65EBDDBB49"),
            new("spotWhiteInitialize", 0xBFD238, 172, "371D0FFCBBE64D073A003AB4C5D1A58B736C00F32EC25CBE26E1D5CA3D2DB38E"),
            new("pointWhiteInitialize", 0xBFD38D, 190, "B732569758980E6743F78AF28892C04321CEAC8E3745D283E704505F70AAFC05"),
            new("spotTextureCreator", 0x952DC0, 737, "23DB7FDB29AD7CE55669A888E8EE6D3CA7C45969DDCD335A162B89BAB74976EB"),
            new("pointTextureCreator", 0x94C4C0, 457, "8657E5795186DFF81F4A621CD12C32E9B6C016BC4C78749D0CE0C7FBD0D42745"),
            new("spotArrayView", 0x94F6E0, 256, "8A8D3AB33493C0EF9B66CE85EAF4224C2F61698F4F761EEEB15E2F1C0825EF93"),
            new("pointArrayView", 0x94F810, 248, "02153015240E0DB1DD99A7331686A9FFE8BD78AF2158DA92CB3C7818A9381E55"),
            new("settingsConstructor", 0xD02010, 1051, "47A36EC67AFC898E468A7A8AD06E13881C400182918068CB535E6DC690D86273"),
            new("settingsCopyShadowFields", 0xD1F0B4, 57, "802E7E5E4998136FEB85BB7B0B9E5B7D6886673EBADDC9EBACF912FD60199737"),
            new("frameArenaAux", 0xD12AF7, 121, "661BE06F9BEBFD8A78F6E2905BCB06DCDAC175FBE49426BFB5A139FB0E05B54E"),
            new("pointPreflight", 0x9B8890, 370, "127E3F069C47D71CF9E684AE15B864A0D1CE68AA0EAB627B2FA362AEDF3C3008"),
            new("spotPreflight", 0x9B9650, 353, "C306C12D25D0FA27E74681B060365E2CA57F54405945B08CAA7DF2431FBCF1FB"),
            new("f103R9Frame", 0xEACB50, 332, "FF1F2FD4702534FA3AF7CDB18966FE9658375FF91377043E2560C5EA2B72AC7F"),
            new("f103Fallback", 0xEADCA4, 139, "B92EEC268C9AF3DD65BAB7A73F14718C1E995A1FDE9C66EC9794EFD64162A3F7")
        ],
        "SC" => [
            new("settingsLookup", 0xBFFFA0, 38, "32DD9B9B0750CF08309F403E217BC693F11ACA33D55E0E0E577451E97B5F52FB"),
            new("selectedSettings", 0xBFD28C, 16, "A36FED28099C9330082D610F8FF1AE19FFB2D38DBE872830FAC26B65EBDDBB49"),
            new("spotWhiteInitialize", 0xBFE708, 172, "BBB2856B41CB144ABC01C12E944366E05B17F1B0B9914816737693A50C35939C"),
            new("pointWhiteInitialize", 0xBFE85D, 190, "EAACCDC70D8E3FA8BE6C3C7709187C5345ED4E24E3B5F2F7AF57DA2100FF20A8"),
            new("spotTextureCreator", 0x953E90, 737, "9323C3BA152C5E519FE557362D5D67F76CA2DBC9CFE4A21157DB75C3359064E3"),
            new("pointTextureCreator", 0x94D590, 457, "CD65E69E2AFB16862DD30DEDB8A1CFD742FD61DA884059F3DC83CE245556DD72"),
            new("spotArrayView", 0x9507B0, 256, "820597704E27E09A0EC48D7C556D1A1F3B4E90CD9A71EF6E1D8FBF8F2C19C772"),
            new("pointArrayView", 0x9508E0, 248, "6DDEA3A1CAB1B52FA57B05822ED9C69E3A5A2BFBD7A7C7309A4AAF423F95925A"),
            new("settingsConstructor", 0xD02D40, 1051, "4085C90B4D616425428F916C7521BA9B91761EEA069CAA1ABD63896804A08019"),
            new("settingsCopyShadowFields", 0xD1FDE4, 57, "802E7E5E4998136FEB85BB7B0B9E5B7D6886673EBADDC9EBACF912FD60199737"),
            new("frameArenaAux", 0xD13827, 121, "661BE06F9BEBFD8A78F6E2905BCB06DCDAC175FBE49426BFB5A139FB0E05B54E"),
            new("pointPreflight", 0x9B9950, 370, "D0894AD1CAA0ACA48520FE76BBE58A8AB3D174FB8CF959AA19044254404E31E1"),
            new("spotPreflight", 0x9BA5E0, 353, "EFCDDCC6A90AF9402D6FD793BB71D3C33471FC581F9942B58A8AC08F93F29F25"),
            new("f103R9Frame", 0xEADB40, 332, "522C041686D5ABC0429957E9BE9AC0CF95DB3004F1356C01AE83503813A998A0"),
            new("f103Fallback", 0xEAEC94, 139, "6E9B1FF1D544702AABB40E4D9329FB72D6D8C0941A6C5AD27473D5951B5D7A18")
        ],
        _ => throw new InvalidDataException("Fallback shadow source supports pinned SC/TC revisions only.")
    };
    public static LoadedOriginalDeferredSourceFallbackShadowImages Load(OriginalGameRepository repository, string language)
    { ArgumentNullException.ThrowIfNull(repository); return Load(repository.SourceDirectory, language); }
    public static LoadedOriginalDeferredSourceFallbackShadowImages Load(string sourceDirectory, string language)
    {
        var windows = Profile(language);
        var native = OriginalDeferredSamplerSources.Load(sourceDirectory, language);
        int formatRva = language == "SC" ? 0x14BF9A8 : 0x14BEAC8;
        var verified = OriginalDeferredSamplerSources.VerifyFallbackShadowWindows(native,
            windows.Select(w => (w.Name, w.Rva, w.Bytes, w.Sha256)).ToArray(), formatRva);
        return new(native, verified);
    }
}

public static partial class OriginalDeferredSamplerSources
{
    /// <summary>Reuse the existing pinned inert PE decoder for bounded added
    /// source windows. No game module is loaded or native instruction executed.</summary>
    internal static IReadOnlyDictionary<string, string> VerifyFallbackShadowWindows(
        LoadedOriginalDeferredSamplerNativeSource native,
        IReadOnlyList<(string Name, int Rva, int Bytes, string Sha256)> windows, int normalPixel1SrvRva)
    {
        byte[] exe = File.ReadAllBytes(native.PhysicalPath);
        VerifyHash(exe, native.ExecutableSha256, "fallback shadow executable");
        var image = new NativeImage(exe); byte[] code = image.DecodeText(out int textRva);
        VerifyHash(code, native.DecodedTextSha256, "fallback shadow decoded text");
        var output = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var window in windows)
        {
            if (window.Bytes <= 0 || window.Rva < textRva || window.Rva - textRva > code.Length - window.Bytes)
                throw new InvalidDataException("Fallback shadow instruction window exceeds the pinned text section.");
            VerifyHash(code.AsSpan(window.Rva - textRva, window.Bytes), window.Sha256, window.Name);
            output.Add(window.Name, window.Sha256);
        }
        ReadOnlySpan<byte> format = image.Raw(normalPixel1SrvRva, 4);
        if (BinaryPrimitives.ReadUInt32LittleEndian(format) != 87)
            throw new InvalidDataException("Original fallback PixelFormat1 normal shader view is not DXGI87.");
        output.Add("normalPixel1SrvFormat", Convert.ToHexString(SHA256.HashData(format)));
        return new ReadOnlyDictionary<string, string>(output);
    }
}
