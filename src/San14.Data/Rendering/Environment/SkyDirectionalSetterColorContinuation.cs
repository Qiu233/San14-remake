using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Actual D3AC07..ACE1 helpers and native1147EE0 integer Kelvin
/// table. Loading source does not select a Main or provide a setter receipt.</summary>
public sealed class LoadedSkyDirectionalSetterColorNativeSource
{
    private readonly MapPostEffect3ParameterCompositeMathSources math;
    internal LoadedSkyDirectionalSetterColorNativeSource(MapPostEffect3ParameterCompositeMathSources math,string evidence)
    {this.math=math;SourceEvidence=evidence;}
    public string Locale=>math.Locale;
    public string RuleVersion => RenderRuleIdentity.For("directional-sky-setter-color", Locale);
    public string SourceEvidence{get;}
    public ReadOnlyMemory<byte> LookupColorTemperature(uint actualKelvin)=>math.LookupColorTemperature(actualKelvin);
}

public static class SkyDirectionalSetterColorNativeSource
{
    public static LoadedSkyDirectionalSetterColorNativeSource Load(GameResourceRepository repository,string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new(MapPostEffect3ParameterCompositeMathSources.Load(repository,locale),
            RenderSamplerSourceAdapters.ReadDirectionalSetterColorRecipe(repository.SourceDirectory,locale));
    }
}

/// <summary>Pure original global Sun/Moon records after RGB→HSV, Main H/S
/// clamping, HSV→RGB and D3's Moon temperature ratio. Input/output W are opaque
/// copied words. This pure operation carries no Main/current setter authority.</summary>
public static class SkyDirectionalSetterColorMath
{
    public static byte[] Apply(ReadOnlySpan<byte> actualSunMoonRecords32,ReadOnlySpan<byte> actualMainHueSaturation16,
        uint actualMainKelvin21C,LoadedSkyDirectionalSetterColorNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);
        if(actualSunMoonRecords32.Length!=32||actualMainHueSaturation16.Length!=16)
            throw new InvalidDataException("D3 setter color continuation needs both complete16B baseline records and the actual Main HS16.");
        byte[] result=actualSunMoonRecords32.ToArray();
        for(int light=0;light<2;light++)
        {
            int at=light*16;var hsv=SkylightColorMath.RgbToHsv(Vector(actualSunMoonRecords32.Slice(at,16)));
            hsv.X=SkylightColorMath.Clamp01(Finite(actualMainHueSaturation16,light*8));
            hsv.Y=SkylightColorMath.Clamp01(Finite(actualMainHueSaturation16,light*8+4));
            var rgb=SkylightColorMath.HsvToRgb(hsv);
            WriteRgb(result.AsSpan(at,12),new(rgb.X,rgb.Y,rgb.Z));
        }
        byte[] reference=native.LookupColorTemperature(6500).ToArray();
        byte[] actual=native.LookupColorTemperature(actualMainKelvin21C).ToArray();
        // D3ACD3's RDX is Moon RGB27EEA80, not Sun RGB27EEA60.
        // C3F880 computes B, G, R ratios and then multiplies R/G/B.
        float b=Ratio(Finite(actual,8),Finite(reference,8)),
            g=Ratio(Finite(actual,4),Finite(reference,4)),
            r=Ratio(Finite(actual,0),Finite(reference,0));
        BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(16,4),NativeFloat.Multiply(r,Finite(result,16)));
        BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(20,4),NativeFloat.Multiply(g,Finite(result,20)));
        BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(24,4),NativeFloat.Multiply(b,Finite(result,24)));
        if(!result.AsSpan(12,4).SequenceEqual(actualSunMoonRecords32.Slice(12,4))||
            !result.AsSpan(28,4).SequenceEqual(actualSunMoonRecords32.Slice(28,4)))
            throw new InvalidDataException("D3 color continuation changed the original fourth-word transport.");
        return result;
    }
    internal static Vector3 ReadRgb(ReadOnlySpan<byte> bytes)=>new(Finite(bytes,0),Finite(bytes,4),Finite(bytes,8));
    private static Vector4 Vector(ReadOnlySpan<byte> bytes)=>new(Finite(bytes,0),Finite(bytes,4),Finite(bytes,8),
        BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(12,4)));
    private static void WriteRgb(Span<byte> bytes,Vector3 rgb)
    {BinaryPrimitives.WriteSingleLittleEndian(bytes,rgb.X);BinaryPrimitives.WriteSingleLittleEndian(bytes.Slice(4),rgb.Y);BinaryPrimitives.WriteSingleLittleEndian(bytes.Slice(8),rgb.Z);}
    private static float Finite(ReadOnlySpan<byte> bytes,int at)
    {float f=BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at,4));return float.IsFinite(f)?f:throw new InvalidDataException("D3 color continuation consumed a nonfinite RGB/HS/table word.");}
    private static float Ratio(float value,float reference)
    {float ratio=Divide(value,reference);if(!float.IsFinite(ratio))throw new InvalidDataException("Source temperature ratio is outside the finite original domain.");return ratio>=1?1:ratio>=0?ratio:0;}
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Divide(float left,float right)=>left/right;
}

/// <summary>One selected source owner freezes actual accepted Main3C0 and
/// selected L baseline32, then applies the original setter-before-position
/// continuation. Main/source identity and color setter generation stay explicit.</summary>
public sealed class SkyDirectionalSetterColorReceipt
{
    private readonly SkyDirectionalSelectedLightSource source;
    private readonly MapSourceCreatedSeasonLightingFrame present;
    private readonly byte[] mainPrefix,selectedLight,baseline32,prepared32;
    private readonly long revision;
    internal SkyDirectionalSetterColorReceipt(SkyDirectionalSelectedLightSource source,
        LoadedSkyDirectionalSetterColorNativeSource native,long revision)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(native);source.ValidateCurrent();
        this.source=source;this.revision=revision;
        present=source.AcceptedLighting.AcceptedInputs.PresentSeasonCamera??
            throw new InvalidDataException("D3 setter continuation needs this actual accepted Main season lighting owner.");
        present.ValidatePreparedLighting(source.AcceptedLighting);
        var inputs=present.AcceptedSeasonCamera.Inputs;
        if(inputs.MainFrame.Camera.FrameNumber!=source.SelectedPacket.Identity.FrameNumber||
            inputs.MainFrame.Camera.Source.Camera.ObjectId!=MainMapCameraAdapter.MainCameraObjectId||
            inputs.MainFrame.Camera.ExecutableLocale!=native.Locale)
            throw new InvalidDataException("D3 setter continuation Main/frame/native locale identity differs from selected L.");
        mainPrefix=present.PreparedCameraPrefix.ToArray();selectedLight=source.SelectedPacket.CompleteLightData.ToArray();
        if(!source.AcceptedLighting.PreparedCameraPrefix.Span.SequenceEqual(mainPrefix)||
            !present.CurrentSkyCamera.PreparedCameraPrefix.Span.SequenceEqual(mainPrefix))
            throw new InvalidDataException("D3 setter continuation did not receive the actual current Main3C0 byte snapshot.");
        MainKelvin21C=BinaryPrimitives.ReadUInt32LittleEndian(mainPrefix.AsSpan(0x21C,4));
        float numeric=inputs.CurrentColorTemperature21C;
        if(!float.IsFinite(numeric)||numeric<0||(double)numeric>uint.MaxValue||MathF.Truncate(numeric)!=numeric||
            MainKelvin21C!=checked((uint)numeric))
            throw new InvalidDataException("Actual Main21C is not its accepted uint Kelvin word; float bits or metadata replacement are forbidden.");
        baseline32=new byte[32];selectedLight.AsSpan(0xFA0,16).CopyTo(baseline32);selectedLight.AsSpan(0xFB0,16).CopyTo(baseline32.AsSpan(16));
        prepared32=SkyDirectionalSetterColorMath.Apply(baseline32,mainPrefix.AsSpan(0x29C,16),MainKelvin21C,native);
        SourceEvidence=native.SourceEvidence+";same-Main="+inputs.MainFrame.Camera.FrameProvenance+
            $";frame={FrameNumber};"+
            "Main939/type4->CPUC8->21Cuint;D3AC07/AC79-Main29C..2A8-HS;D3ACD3->MoonRGB/C3F880/1147EE0-temperature;baseline-not-Computed/no-GPU";
    }
    public long FrameNumber=>present.FrameNumber;
    public uint MainKelvin21C{get;}
    public string SourceEvidence{get;}
    public SkySceneSelectedViewLightIdentity SelectedIdentity=>source.SelectedPacket.Identity;
    public ReadOnlyMemory<byte> BaselineSunMoonRecords32=>baseline32.ToArray();
    public ReadOnlyMemory<byte> PreparedSunMoonRecords32=>prepared32.ToArray();
    public ReadOnlyMemory<byte> ActualMainHueSaturation16=>mainPrefix.AsSpan(0x29C,16).ToArray();
    public Vector3 SunRgb=>SkyDirectionalSetterColorMath.ReadRgb(prepared32.AsSpan(0,12));
    public Vector3 MoonRgb=>SkyDirectionalSetterColorMath.ReadRgb(prepared32.AsSpan(16,12));
    public void ValidateCurrent(SkyDirectionalSelectedLightSource actualSource)
    {
        if(!ReferenceEquals(source,actualSource))throw new InvalidDataException("D3 color receipt belongs to another selected source owner.");
        source.ValidateCurrent();source.ValidateColorReceiptOwner(this,revision);
        // Constructor accepted the immutable Present/Table producer once.
        // The color consumer needs the current owner identity and exact
        // Main/L words; it must not reparse an unused static table per draw.
        if(!ReferenceEquals(source.AcceptedLighting.AcceptedInputs.PresentSeasonCamera,present)||
            !source.AcceptedLighting.PreparedCameraPrefix.Span.SequenceEqual(mainPrefix)||
            !present.PreparedCameraPrefix.Span.SequenceEqual(mainPrefix)||
            !present.CurrentSkyCamera.PreparedCameraPrefix.Span.SequenceEqual(mainPrefix)||
            !source.SelectedPacket.CompleteLightData.Span.SequenceEqual(selectedLight))
            throw new InvalidDataException("D3 color receipt's Main/L current identity or bytes changed.");
    }
    public void ValidateLights(SkySceneLights actualLights,SkyDirectionalSelectedLightSource actualSource)
    {
        ValidateCurrent(actualSource);
        actualSource.ValidateActualColorPreparedLights(actualLights);
        var sun=Sky2CameraLightPreparation.PrepareRgbCache(SunRgb);
        float maximum=MoonRgb.X>MoonRgb.Y?MoonRgb.X:MoonRgb.Y;
        if(maximum<MoonRgb.Z)maximum=MoonRgb.Z;
        if(!actualLights.SunPresent||!actualLights.MoonPresent||actualLights.SunRgbCache1460 is not { } actualSun||
            !Equal(actualSun.NormalizedRgb,sun.NormalizedRgb)||!Equal(actualSun.Maximum,sun.Maximum)||
            !Equal(actualLights.RawMoonRgb,MoonRgb)||!Equal(actualLights.MoonMaximum147C,maximum))
            throw new InvalidDataException("Actual light setter caches differ from their current D3 global color receipt.");
    }
    public void ValidateFrame(SkySceneSourceFramePacket frame,SkyDirectionalSelectedLightSource actualSource)
    {ValidateLights(frame.SourceFrame.Lights,actualSource);actualSource.ValidateActualAppliedFrame(frame);}
    private static bool Equal(Vector3 a,Vector3 b)=>Equal(a.X,b.X)&&Equal(a.Y,b.Y)&&Equal(a.Z,b.Z);
    private static bool Equal(float a,float b)=>BitConverter.SingleToUInt32Bits(a)==BitConverter.SingleToUInt32Bits(b);

}

public static partial class RenderSamplerSourceAdapters
{
    internal static string ReadDirectionalSetterColorRecipe(string sourceDirectory,string locale)
    {
        return $"{RenderRuleIdentity.For("directional-sky-setter-color",locale)};D3ABB7..ACE6-global-baseline/HScurrent/temperature;"+
            "B4BDB0-RGBtoHSV/B4BBE0-HSVtoRGB;D3ACD3->MoonRGB/C3F880/uint21C/1147EE0-388table;original-fourth-word-copy";
    }
}
