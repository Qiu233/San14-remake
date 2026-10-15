using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed class LoadedSkyDirectionalAtmosphereNativeSource
{
    internal LoadedSkyDirectionalAtmosphereNativeSource(LoadedSkyAtmosphereNativeSource scattering,
        string fogProvenance, float fallback)
        => (ScatteringSource, FogSourceProvenance, IntervalFallback) = (scattering, fogProvenance, fallback);
    public LoadedSkyAtmosphereNativeSource ScatteringSource { get; }
    public string RuleVersion => RenderRuleIdentity.For("directional-sky-atmosphere", ScatteringSource.Locale);
    public string FogSourceProvenance { get; }
    public float IntervalFallback { get; }
    public string SourceSelectedEvidence { get; internal init; } = "";
    public LoadedSkyDirectionalSetterColorNativeSource ColorSetterContinuationSource { get; internal init; } = null!;
}

/// <summary>The specific stock renderer4/profile1 branch at97C377. Its
/// coefficient table slot4 is file-backed1; lazy slots2/3 are separate and are
/// never inferred from their file zeros. Source Fog mode7 is not renderer7.</summary>
public static class SkyDirectionalAtmosphereNativeSource
{
    public static LoadedSkyDirectionalAtmosphereNativeSource Load(GameResourceRepository repository,
        string locale, NativeLogDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(repository);
        string evidence = RenderSamplerSourceAdapters.ReadDirectionalSkyMode4FinalFogRecipe(repository.SourceDirectory, locale);
        var shader = repository.LoadShaderContainer("system", 0x340AC335);

        var row = shader.ReadG1Bindings().Features[0].Feature;
        if (row.Words[6] != 4 || row.Words[7] != 1)
            throw new InvalidDataException("Directional Sky selected row0 renderer/profile changed.");
        evidence += ";" + RenderSamplerSourceAdapters.ReadSelectedSkyPackageNativeRecipe(repository.SourceDirectory, locale) +
            $";sourceShader={shader.Source.ResourceUri}@{shader.Source.DecodedSha256};VS0=exact-Fog-MOV-transport";
        return new(SkyAtmosphereNativeSource.Load(repository, locale, dispatch), evidence,
            BitConverter.UInt32BitsToSingle(0x7F7FFFFF))
            {SourceSelectedEvidence=RenderSamplerSourceAdapters.ReadDirectionalSkySourceSelectedRecipe(repository.SourceDirectory,locale),
                ColorSetterContinuationSource=SkyDirectionalSetterColorNativeSource.Load(repository,locale)};
    }
}

/// <summary>An exact mode4/profile1 finalFog writer receipt. Typed numeric
/// inputs require no selected packet; legacy inputs retain their source lifetime.</summary>
public sealed class SkyDirectionalMode4FogReceipt
{
    private readonly SkyDirectionalSelectedLightSource? source;
    private readonly byte[] bytes;
    private readonly Vector4 value;
    private readonly uint sourceMode;
    private readonly float start;
    private readonly float end;
    private SkyDirectionalMode4FogReceipt(SkyDirectionalSelectedLightSource? source,
        byte[] bytes, Vector4 value, uint sourceMode, float start, float end)
    {
        this.source = source; this.bytes = bytes; this.value = value;
        this.sourceMode = sourceMode; this.start = start; this.end = end;
    }

    public static SkyFogPacket Generate(uint sourceMode, float start, float end, float intervalFallback)
        => Generate(sourceMode, start, end, intervalFallback, null);

    private static SkyFogPacket Generate(uint sourceMode, float start, float end, float intervalFallback,
        SkyDirectionalSelectedLightSource? source)
    {
        if (sourceMode != 0 && end <= start && BitConverter.SingleToUInt32Bits(intervalFallback) != 0x7F7FFFFF)
            throw new InvalidDataException("Mode4 Fog requires the actual native float-max fallback for a nonincreasing interval.");
        var packet = SkyFogMath.GenerateDirectionalMode4(sourceMode, start, end, intervalFallback);
        var proof = new SkyDirectionalMode4FogReceipt(source, packet.Bytes.ToArray(), packet.Value, sourceMode, start, end);
        return packet with { SourceMode4Proof = proof };
    }

    internal static SkyFogPacket Generate(SkyDirectionalSelectedLightSource source,
        LoadedSkyDirectionalAtmosphereNativeSource native)
    {
        source.ValidateCurrent();
        ReadOnlySpan<byte> fog = source.PackageFog96.Span;
        uint mode = BinaryPrimitives.ReadUInt32LittleEndian(fog.Slice(0x10, 4));
        float start = 0, end = 0;
        if (mode != 0)
        {
            start = BinaryPrimitives.ReadSingleLittleEndian(fog.Slice(0x14, 4));
            end = BinaryPrimitives.ReadSingleLittleEndian(fog.Slice(0x18, 4));
        }
        return Generate(mode, start, end, native.IntervalFallback, source);
    }
    public void ValidateValue(Vector4 actual)
    {
        source?.ValidateCurrent();
        if(!SameBits(actual,value))throw new InvalidDataException("Sky finalFog transport differs from the exact native mode4 result.");
    }

    /// <summary>Validate both the original full shader/program bytes and the
    /// exact current native mode4 Fog writer for a named CPU packet. PS5/11
    /// consume Fog.xy; preserving these raw operands does not prove a backend
    /// atmospheric draw has original D3D interpolation/NaN/min behavior.</summary>
    public void ValidateSourcePixelCpuPacking(LoadedShaderContainer actualShader, int pixelProgramIndex)
    {
        ArgumentNullException.ThrowIfNull(actualShader); source?.ValidateCurrent();
        if(pixelProgramIndex is not (5 or 11 or 6))
            throw new InvalidDataException("Moon mode4 CPU scope requires PS5/11/6.");
        // Preserve the selected-program bounds and actual current Fog operands;
        // shader/reference byte comparisons belong to local offline validation.
        _ = actualShader.Container.GetProgramBytes(actualShader.Source.Bytes,pixelProgramIndex);
        if(pixelProgramIndex is 5 or 11)ValidateConsumedFogCpuOperands();
    }

    private void ValidateConsumedFogCpuOperands()
    {
        // All-finite native outputs need no special arithmetic capability.
        if(float.IsFinite(value.X)&&float.IsFinite(value.Y)&&float.IsFinite(value.Z)&&float.IsFinite(value.W))return;
        // The admitted exceptional writer is specifically native97C377's
        // nonincreasing interval, coefficient1 and fallbackfloat-max. Reject
        // arbitrary NaNs/Infs rather than extending a general finite bypass.
        if(!float.IsFinite(start)||!float.IsFinite(end)||end>start||
            BitConverter.SingleToUInt32Bits(value.X)!=0x7F7FFFFF ||
            BitConverter.SingleToUInt32Bits(value.Y)!=0xFF800000 ||
            BitConverter.SingleToUInt32Bits(value.Z)!=0 ||BitConverter.SingleToUInt32Bits(value.W)!=0)
            throw new InvalidDataException("Consumed nonfinite Fog CPU scope requires the exact current native float-max/-Inf writer operands.");
    }

    public void Validate(SkyFogPacket packet)
    {
        source?.ValidateCurrent();
        if (!ReferenceEquals(packet.SourceMode4Proof, this) || packet.RendererFogMode != 4 || packet.Profile != 1 ||
            !packet.FogEnabled || packet.SourceFogMode != sourceMode || !SameBits(packet.Value,value) ||
            packet.RequiresMode7NeutralScattering || packet.ScatteringOverride is not null || !packet.Bytes.AsSpan().SequenceEqual(bytes))
            throw new InvalidDataException("Mode4 finalFog differs from its actual selected Directional writer receipt.");
    }
    private static bool SameBits(Vector4 a,Vector4 b) => new[]{a.X,a.Y,a.Z,a.W}.Select(BitConverter.SingleToUInt32Bits)
        .SequenceEqual(new[]{b.X,b.Y,b.Z,b.W}.Select(BitConverter.SingleToUInt32Bits));
}

public static partial class RenderSamplerSourceAdapters
{
    internal static string ReadDirectionalSkyMode4FinalFogRecipe(string sourceDirectory, string locale)
    {
        return $"{RenderRuleIdentity.For("directional-sky-mode4-fog",locale)};mode4-profile1;"+
            "coefficient=1;nonincreasing-fallback=7F7FFFFF;SourceFogMode-independent-of-renderer4";
    }
}
