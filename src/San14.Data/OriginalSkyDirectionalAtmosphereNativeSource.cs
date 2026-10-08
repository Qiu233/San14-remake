using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

public sealed class LoadedOriginalSkyDirectionalAtmosphereNativeSource
{
    internal LoadedOriginalSkyDirectionalAtmosphereNativeSource(LoadedOriginalSkyAtmosphereNativeSource scattering,
        string fogProvenance, float fallback, LoadedOriginalSky2MoonGeometryNativeSource moonGeometry)
        => (ScatteringSource, FogSourceProvenance, IntervalFallback, MoonGeometrySource) = (scattering, fogProvenance, fallback, moonGeometry);
    public LoadedOriginalSkyAtmosphereNativeSource ScatteringSource { get; }
    public string FogSourceProvenance { get; }
    public float IntervalFallback { get; }
    public LoadedOriginalSky2MoonGeometryNativeSource MoonGeometrySource { get; }
    public string SourceSelectedEvidence { get; internal init; } = "";
    public LoadedOriginalSkyDirectionalSetterColorNativeSource ColorSetterContinuationSource { get; internal init; } = null!;
}

/// <summary>The specific stock renderer4/profile1 branch at97C377. Its
/// coefficient table slot4 is file-backed1; lazy slots2/3 are separate and are
/// never inferred from their file zeros. Source Fog mode7 is not renderer7.</summary>
public static class OriginalSkyDirectionalAtmosphereNativeSource
{
    public static LoadedOriginalSkyDirectionalAtmosphereNativeSource Load(OriginalGameRepository repository,
        string locale, OriginalNativeLogDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string evidence = OriginalDeferredSamplerSources.ReadDirectionalSkyMode4FinalFogRecipe(repository.SourceDirectory, locale);
        var shader = repository.LoadShaderContainer("system", 0x340AC335);
        if (shader.Source.OverrideSource is not null ||
            shader.Source.DecodedSha256 != "CE4CEC6271BDE59E40D5B97746A7A1877188E133550A0C608D41C353A4999424" ||
            Convert.ToHexString(SHA256.HashData(shader.Source.Bytes)) != shader.Source.DecodedSha256 ||
            Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(shader.Source.Bytes, 0))) !=
                "E0E2FEB809484A59D79E28A58BBE384BAAE01B67914296C37CF1BBC26ED2AB36")
            throw new InvalidDataException("Directional Sky fog transport requires the unchanged stock shader and exact VS0 MOV program.");
        var row = shader.ReadG1Bindings().Features[0].Feature;
        if (row.Words[6] != 4 || row.Words[7] != 1)
            throw new InvalidDataException("Directional Sky selected row0 renderer/profile changed.");
        evidence += ";" + OriginalDeferredSamplerSources.ReadSelectedSkyPackageNativeRecipe(repository.SourceDirectory, locale) +
            $";sourceShader={shader.Source.ResourceUri}@{shader.Source.DecodedSha256};VS0=exact-Fog-MOV-transport";
        return new(OriginalSkyAtmosphereNativeSource.Load(repository, locale, dispatch), evidence,
            BitConverter.UInt32BitsToSingle(0x7F7FFFFF),OriginalSky2MoonGeometryNativeSource.Load(repository,locale))
            {SourceSelectedEvidence=OriginalDeferredSamplerSources.ReadDirectionalSkySourceSelectedRecipe(repository.SourceDirectory,locale),
                ColorSetterContinuationSource=OriginalSkyDirectionalSetterColorNativeSource.Load(repository,locale)};
    }
}

/// <summary>An exact current mode4/profile1 finalFog writer receipt.
/// Its source is the same accepted selected Directional packet's whole Fog96.</summary>
public sealed class OriginalSkyDirectionalMode4FogReceipt
{
    private readonly OriginalSkyDirectionalSelectedLightSource source;
    private readonly byte[] bytes;
    private readonly Vector4 value;
    private readonly uint sourceMode;
    private OriginalSkyDirectionalMode4FogReceipt(OriginalSkyDirectionalSelectedLightSource source,
        byte[] bytes, Vector4 value, uint sourceMode) { this.source = source; this.bytes = bytes; this.value = value; this.sourceMode = sourceMode; }

    internal static OriginalSkyFogPacket Generate(OriginalSkyDirectionalSelectedLightSource source,
        LoadedOriginalSkyDirectionalAtmosphereNativeSource native)
    {
        source.ValidateCurrent();
        ReadOnlySpan<byte> fog = source.PackageFog96.Span;
        uint mode = BinaryPrimitives.ReadUInt32LittleEndian(fog.Slice(0x10, 4));
        Vector4 v = Vector4.Zero;
        if (mode != 0)
        {
            float start = Read(fog, 0x14), end = Read(fog, 0x18);
            float ratio = end > start ? Divide(1, Subtract(end, start)) : native.IntervalFallback;
            // 97C3C8..97C3E2: multiply table4*start before multiplying ratio.
            float product = Multiply(1, start);
            float x = Multiply(1, ratio), y = Multiply(product, ratio);
            y = BitConverter.UInt32BitsToSingle(BitConverter.SingleToUInt32Bits(y) ^ 0x80000000);
            v = new(x, y, 0, 0);
        }
        byte[] bytes = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0), v.X); BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4), v.Y);
        var proof = new OriginalSkyDirectionalMode4FogReceipt(source, bytes.ToArray(), v, mode);
        return new(4, 1, true, mode, v, bytes, false, null) { SourceMode4Proof = proof };
    }
    public void ValidateValue(Vector4 actual)
    {
        source.ValidateCurrent();
        if(!SameBits(actual,value))throw new InvalidDataException("Sky finalFog transport differs from the exact native mode4 result.");
    }
    internal void ValidateDefinitions(DxbcResourceDefinitions definitions)
    {
        source.ValidateCurrent();
        string hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(definitions)));
        // The unchanged VS0 MOV and PS3/4/10/6 unused-Fog interfaces are
        // transport scopes. PS5/11 are a different, consumed-Fog CPU scope:
        // their source tail computes mul/mad/exp/min on TEXCOORD3.xy.
        // Admitting the exact native writer bytes here is not a claim about
        // backend interpolation, NaN/min translation, or an atmospheric draw.
        string[] allowed=["C754217B19B596A178D0D1E3E96849B63B1E1574E9834D4FA976CF1E7950AF66",
            "07968ADE85829BC7AF1467C579650502E8A21F0A22C0FCCDA6CC62357B95C110",
            "26AB110240A9ED99C4EF11E77799A232A4C844462E3DD1FE0E3984B5F6E45915",
            "806CE4187E93332191C3FF86EE89D0C73AF23244245E729C2B9F18AE2AF398BC",
            "A1B7C8F6EA195E911FB8FE0FD3C64C31BB0628A28EFF37FA142799241E22AC87"];
        if(allowed.Contains(hash))return;
        if(hash is "C509015C71C314D76A405598B435AD08AE178B6A5EB7FF5AFE7C59F23924491E" or
            "288C31D6F32200C0235E8238EF30B2370E9ECE5610EBDF096108699FE4374366")
        { ValidateConsumedFogCpuOperands();return; }
        throw new InvalidDataException("Native mode4 Fog requires its exact original transport or consumed-Fog CPU program scope.");
    }

    /// <summary>Validate both the original full shader/program bytes and the
    /// exact current native mode4 Fog writer for a named CPU packet. PS5/11
    /// consume Fog.xy; preserving these raw operands does not prove a backend
    /// atmospheric draw has original D3D interpolation/NaN/min behavior.</summary>
    public void ValidateOriginalPixelCpuPacking(LoadedShaderContainer actualShader, int pixelProgramIndex)
    {
        ArgumentNullException.ThrowIfNull(actualShader); source.ValidateCurrent();
        string expected = pixelProgramIndex switch
        {
            5 => "E0F26D96BC871BA23979AE4DE3996C7638AAD2DFE526CDDD907D0FBBBA296719",
            11 => "B0A176C05E2476DAA787BB75028185CDB59CCC7F344AFC3C782F2CDCECE6C4FF",
            6 => "0B6E8D02E2CB98706826E7935AC9249429D758FFCE2BDD502821F469F0A3DBF2",
            _ => throw new InvalidDataException("Moon mode4 CPU scope requires exact original PS5/11/6.")
        };
        if(actualShader.Source.OverrideSource is not null ||
            Convert.ToHexString(SHA256.HashData(actualShader.Source.Bytes)) !=
                "CE4CEC6271BDE59E40D5B97746A7A1877188E133550A0C608D41C353A4999424" ||
            Convert.ToHexString(SHA256.HashData(actualShader.Container.GetProgramBytes(actualShader.Source.Bytes,pixelProgramIndex))) != expected)
            throw new InvalidDataException("Consumed Fog CPU packing requires the complete unchanged original Sky shader and selected pixel program.");
        ValidateDefinitions(actualShader.ReadResourceDefinitions(pixelProgramIndex));
    }

    private void ValidateConsumedFogCpuOperands()
    {
        // All-finite native outputs need no special arithmetic capability.
        if(float.IsFinite(value.X)&&float.IsFinite(value.Y)&&float.IsFinite(value.Z)&&float.IsFinite(value.W))return;
        // The admitted exceptional writer is specifically native97C377's
        // nonincreasing interval, coefficient1 and fallbackfloat-max. Reject
        // arbitrary NaNs/Infs rather than extending a general finite bypass.
        ReadOnlySpan<byte> fog=source.PackageFog96.Span;
        float start=BinaryPrimitives.ReadSingleLittleEndian(fog.Slice(0x14,4));
        float end=BinaryPrimitives.ReadSingleLittleEndian(fog.Slice(0x18,4));
        if(!float.IsFinite(start)||!float.IsFinite(end)||end>start||
            BitConverter.SingleToUInt32Bits(value.X)!=0x7F7FFFFF ||
            BitConverter.SingleToUInt32Bits(value.Y)!=0xFF800000 ||
            BitConverter.SingleToUInt32Bits(value.Z)!=0 ||BitConverter.SingleToUInt32Bits(value.W)!=0)
            throw new InvalidDataException("Consumed nonfinite Fog CPU scope requires the exact current native float-max/-Inf writer operands.");
    }

    public void Validate(OriginalSkyFogPacket packet)
    {
        source.ValidateCurrent();
        if (!ReferenceEquals(packet.SourceMode4Proof, this) || packet.RendererFogMode != 4 || packet.Profile != 1 ||
            !packet.FogEnabled || packet.SourceFogMode != sourceMode || !SameBits(packet.Value,value) ||
            packet.RequiresMode7NeutralScattering || packet.ScatteringOverride is not null || !packet.Bytes.AsSpan().SequenceEqual(bytes))
            throw new InvalidDataException("Mode4 finalFog differs from its actual selected Directional writer receipt.");
    }
    private static bool SameBits(Vector4 a,Vector4 b) => new[]{a.X,a.Y,a.Z,a.W}.Select(BitConverter.SingleToUInt32Bits)
        .SequenceEqual(new[]{b.X,b.Y,b.Z,b.W}.Select(BitConverter.SingleToUInt32Bits));
    private static float Read(ReadOnlySpan<byte> bytes, int at)
    { float value = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at, 4)); return float.IsFinite(value) ? value : throw new InvalidDataException("Mode4 finalFog input is nonfinite."); }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Multiply(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Divide(float a, float b) => a / b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
}

public static partial class OriginalDeferredSamplerSources
{
    internal static string ReadDirectionalSkyMode4FinalFogRecipe(string sourceDirectory, string locale)
    {
        var profile = Profile(locale);
        byte[] exe = File.ReadAllBytes(Path.Combine(sourceDirectory, $"SAN14CE_{locale}.exe"));
        VerifyHash(exe, profile.ExeHash, "Directional Sky fog executable");
        var image = new NativeImage(exe); byte[] code = image.DecodeText(out int textRva);
        VerifyHash(code, profile.TextHash, "Directional Sky fog decoded text");
        (int start, string hash, int table4, int fallback) = locale switch
        {
            "TC" => (0x97C377, "D689A025B0B772FC136C9029622D58649F0CD9B1024E2BB2BA63FC2978A22CFF", 0x1A8B738, 0x1343CD0),
            "SC" => (0x97D507, "6C80B526B296CB354F2764D1611DD3EF82C2D2F4E2E341368D2F2BA829560E7A", 0x1A8C798, 0x1344CD0),
            _ => throw new InvalidDataException("Directional Sky fog source requires SC or TC.")
        };
        VerifyHash(code.AsSpan(start - textRva, 0x77), hash, "Directional active mode4/profile1 finalFog branch");
        if (BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(table4, 4)) != 0x3F800000 ||
            BinaryPrimitives.ReadUInt32LittleEndian(image.Raw(fallback, 4)) != 0x7F7FFFFF)
            throw new InvalidDataException("Directional source finalFog interval coefficient or fallback literal changed.");
        return $"san14://file/SAN14CE_{locale}.exe@{profile.ExeHash};mode4-profile1={start:X8}/77@{hash};" +
            "table4=original-file1;nonincreasing-fallback=7F7FFFFF;SourceFogMode-independent-of-renderer4";
    }
}
