using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MainDirectionalLightSettings(Vector4 WorldQuaternion, Vector3 Color);

public sealed record MainLightShadowSettings(
    byte CasterPlane,
    uint MaximumPrimitives,
    uint MaximumCasters,
    byte ReceiveShadowType,
    uint Samples,
    float SlopeBias,
    float LightSize);

public sealed record MainLightExposureSettings(
    float ExposureValue,
    uint SceneColorRange,
    float LuminanceMaximum,
    Vector3 AmbientColor);

public sealed record MainLightEyeTwinkleSettings(
    float Scale,
    float ExponentScale);

public sealed record MainLightRampSettings(
    float Coefficient,
    float SublightCoefficient,
    float UScale,
    float SublightUScale,
    float UBias,
    float SublightUBias,
    float ShadowFade,
    float OcclusionThresholdForIrShadow,
    float IrShadowFade,
    float LocalLightDiffuser,
    float LocalLightScale,
    float AmbientOcclusionFade,
    float URangeMinimum,
    float URangeMaximum,
    byte RimDirectionType,
    Vector3 RimGlobalScale);

public sealed record MainLightOutlineSettings(
    float IntensityMinimum,
    float IntensityMaximum,
    float DepthDecayRatio,
    float ConstantThicknessRatio,
    float DepthScale,
    Vector3 ColorScale,
    byte CalculationMode,
    float NearThickness,
    float FarThickness,
    float FadeMinimumThickness);

public sealed record MainLightHairHighlightSettings(
    float Threshold,
    float ReflectionRatio,
    Vector3 ColorScale,
    float LightElevationRatio,
    float LightAzimuthRatio);

public sealed record MainLightEdgeSettings(
    float ToonThickness,
    Vector4 ToonColor,
    float SilhouetteFade,
    float Thickness,
    float DepthThreshold,
    float CameraRimSharpness,
    Vector4 HighlightColor,
    float HighlightRamp,
    float ReflectionScale,
    float BackThickness,
    float BackDepthThreshold,
    float BackRimScale,
    float BackRimSharpness);

public sealed record MainLightFxaaSettings(
    float EdgeThreshold,
    float MinimumEdgeThreshold,
    float EdgeSharpness,
    float PixelRange,
    uint Enabled,
    int Type,
    float QualityEdgeThreshold,
    float QualityMinimumEdgeThreshold,
    float QualitySubpixelSharpness,
    int QualityLevel,
    int QualityPerformance);

public sealed record MainLightPostSettings(
    float BloomWeight,
    float BloomThreshold,
    float BloomThresholdSoftKnee,
    float BloomRadius,
    float LuminanceThreshold,
    byte Module);

public sealed record MainLightResourceReferences(
    uint SpecularMap,
    uint DiffuseMap,
    uint IndexedRamp,
    uint HairHighlight,
    uint OutlineColor,
    uint EyeTwinkle,
    uint HdrLut);

/// <summary>Immutable lighting controls imported from a selected main-light declaration.
/// Resource keys remain references; normalization and rendering policy belong to consumers.</summary>
public sealed class MainLightSettings
{
    private readonly byte[] extensions;

    private MainLightSettings(IReadOnlyList<MainDirectionalLightSettings> directionalLights,
        uint groupMask,
        uint groupMaskMain,
        float ambientMapsRotation,
        byte[] extensions,
        MainLightShadowSettings shadow,
        MainLightExposureSettings exposure,
        MainLightEyeTwinkleSettings eyeTwinkle,
        MainLightRampSettings ramp,
        MainLightOutlineSettings outline,
        MainLightHairHighlightSettings hairHighlight,
        MainLightEdgeSettings edge,
        MainLightFxaaSettings fxaa,
        MainLightPostSettings post,
        MainLightResourceReferences resources)
    {
        DirectionalLights = directionalLights; GroupMask = groupMask; GroupMaskMain = groupMaskMain;
        AmbientMapsRotation = ambientMapsRotation; this.extensions = extensions;
        Shadow = shadow;
        Exposure = exposure;
        EyeTwinkle = eyeTwinkle;
        Ramp = ramp;
        Outline = outline;
        HairHighlight = hairHighlight;
        Edge = edge;
        Fxaa = fxaa;
        Post = post;
        Resources = resources;
    }

    public IReadOnlyList<MainDirectionalLightSettings> DirectionalLights { get; }
    public uint GroupMask { get; }
    public uint GroupMaskMain { get; }
    /// <summary>The authored angle in degrees.</summary>
    public float AmbientMapsRotation { get; }
    public ReadOnlyMemory<byte> Extensions => extensions;
    public MainLightShadowSettings Shadow { get; }
    public MainLightExposureSettings Exposure { get; }
    public MainLightEyeTwinkleSettings EyeTwinkle { get; }
    public MainLightRampSettings Ramp { get; }
    public MainLightOutlineSettings Outline { get; }
    public MainLightHairHighlightSettings HairHighlight { get; }
    public MainLightEdgeSettings Edge { get; }
    public MainLightFxaaSettings Fxaa { get; }
    public MainLightPostSettings Post { get; }
    public MainLightResourceReferences Resources { get; }

    internal static MainLightSettings Import(DokObject declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        DokProperty Field(uint hash, uint type, int count, int width)
        {
            DokProperty[] matches = declaration.Properties.Where(value => value.NameHash == hash).ToArray();
            if (matches.Length != 1 || matches[0].TypeCode != type || matches[0].ElementCount != count ||
                matches[0].ElementSize != width || matches[0].RawValue.Length != count * width)
                throw new InvalidDataException($"Main-light property {hash:X8} requires one type {type}, count {count} declaration.");
            return matches[0];
        }
        float Float(uint hash) => BinaryPrimitives.ReadSingleLittleEndian(Field(hash, 8, 1, 4).RawValue);
        uint Unsigned(uint hash) => BinaryPrimitives.ReadUInt32LittleEndian(Field(hash, 5, 1, 4).RawValue);
        int Integer(uint hash) => BinaryPrimitives.ReadInt32LittleEndian(Field(hash, 4, 1, 4).RawValue);
        byte Byte(uint hash) => Field(hash, 1, 1, 1).RawValue[0];
        Vector3 Three(uint hash)
        {
            byte[] value = Field(hash, 8, 3, 4).RawValue;
            return new(BinaryPrimitives.ReadSingleLittleEndian(value),
                BinaryPrimitives.ReadSingleLittleEndian(value.AsSpan(4)),
                BinaryPrimitives.ReadSingleLittleEndian(value.AsSpan(8)));
        }
        Vector4 Four(uint hash)
        {
            byte[] value = Field(hash, 8, 4, 4).RawValue;
            return new(BinaryPrimitives.ReadSingleLittleEndian(value),
                BinaryPrimitives.ReadSingleLittleEndian(value.AsSpan(4)),
                BinaryPrimitives.ReadSingleLittleEndian(value.AsSpan(8)),
                BinaryPrimitives.ReadSingleLittleEndian(value.AsSpan(12)));
        }

        void Empty(uint hash, uint type, int width)
        {
            DokProperty[] matches = declaration.Properties.Where(value => value.NameHash == hash).ToArray();
            if (matches.Length != 1 || matches[0].TypeCode != type || matches[0].ElementSize != width ||
                matches[0].ElementCount < 0 || matches[0].RawValue.Length != (long)matches[0].ElementCount * width)
                throw new InvalidDataException($"Main-light array {hash:X8} has an invalid declaration.");
            if (matches[0].ElementCount != 0)
                throw new NotSupportedException($"Nonempty main-light array {hash:X8} needs an implemented producer.");
        }

        // These authored array forms need real nonempty producers before they can be consumed.
        Empty(0x3FF39715u, 5, 4); // IRim_Indices
        Empty(0x4D694803u, 1, 1); // IRim_rmModes
        Empty(0x42E7F506u, 8, 4); // IRim_rmScaleSharps
        Empty(0xDE0EEED5u, 1, 1); // IRim_rmTargets
        Empty(0x81C00D8Du, 0, 1); // IRim_rmExtIndices
        Empty(0xC6BCC03Eu, 8, 4); // IRim_tkEmiParams
        Empty(0xF0D717C3u, 8, 4); // IRim_tkEmiCurveParams
        Empty(0x71B6BF9Du, 8, 4); // IRim_rflScalerMainAndSubs
        Empty(0x225C8915u, 8, 4); // IRim_rflScalerLocalAndAmbients
        Empty(0x4437CBB8u, 8, 4); // IRim_trsScalers
        Empty(0x229FCFB2u, 8, 4); // Ramp_RimScales
        Empty(0x4FB7C03Au, 8, 4); // Ramp_RimSharpnesses
        Empty(0x20FF70DDu, 8, 4); // Ramp_RimColorScales

        var directionalLights = Array.AsReadOnly(new MainDirectionalLightSettings[]
        {
            new(Four(0xF52EA910u), Three(0x754E8B8Du)),
            new(Four(0xB65C0542u), Three(0xF600D81Bu)),
            new(Four(0x0705E343u), Three(0xFDB27D7Au)),
            new(Four(0x57AFC144u), Three(0x056422D9u)),
        });
        return new(directionalLights, Unsigned(0xC0C5A5B5u), Unsigned(0x54E95FFCu),
            Float(0xA6966013u), Field(0x4439EF34u, 1, 8, 1).RawValue.ToArray(),
            new MainLightShadowSettings(
                Byte(0xD7E3FF50u), Unsigned(0xDB26951Du), Unsigned(0x6F486116u),
                Byte(0x2DAB8C43u), Unsigned(0xA3FB1464u), Float(0x30647C5Cu),
                Float(0x0D8D3389u)),
            new MainLightExposureSettings(
                Float(0x00014B31u), Unsigned(0x65B6C39Au), Float(0x462994C1u),
                Three(0x95E12FCBu)),
            new MainLightEyeTwinkleSettings(
                Float(0xE1BCBA93u), Float(0x84E351E4u)),
            new MainLightRampSettings(
                Float(0x5DC0CD34u), Float(0x6621A29Eu), Float(0x8ACE841Eu),
                Float(0x63A6CF88u), Float(0x29E3322Fu), Float(0x0AB8DC99u),
                Float(0xA513F5D7u), Float(0x3D5A3A68u), Float(0x4368D1CEu),
                Float(0xC9BFB618u), Float(0x9BDA4572u), Float(0x7261DB29u),
                Float(0xB38FA50Bu), Float(0x97A8659Du), Byte(0x6543DFD0u),
                Three(0x6A81F3D4u)),
            new MainLightOutlineSettings(
                Float(0x567F8C3Fu), Float(0xF57EDDEDu), Float(0xE74D8D09u),
                Float(0xDC4A0E2Bu), Float(0xFE80308Eu), Three(0x8EF4EBF7u),
                Byte(0x5CDA1BA6u), Float(0x8B75BEBAu), Float(0xF5DF0F9Bu),
                Float(0x826C4960u)),
            new MainLightHairHighlightSettings(
                Float(0x6F4C479Au), Float(0x4489B8C5u), Three(0x96F342CCu),
                Float(0xBB391281u), Float(0x5348FBD0u)),
            new MainLightEdgeSettings(
                Float(0x4E12FF39u), Four(0x06E8AAE8u), Float(0xB3565F94u),
                Float(0xAE31C48Au), Float(0x03343BEEu), Float(0x87158306u),
                Four(0x7080C2A7u), Float(0xEAA499BCu), Float(0x3ADB7B29u),
                Float(0xEA7002A3u), Float(0x80E02807u), Float(0x89E01ADBu),
                Float(0x25C8EFC4u)),
            new MainLightFxaaSettings(
                Float(0x307216E4u), Float(0x77CBE856u), Float(0x350DC6CEu),
                Float(0xE9D823E9u), Unsigned(0x20196D95u), Integer(0x855F598Cu),
                Float(0x54A05D41u), Float(0xDF0F678Fu), Float(0xBEB97EB5u),
                Integer(0x4DCC7F17u), Integer(0x07D11283u)),
            new MainLightPostSettings(
                Float(0xC96AEEA5u), Float(0xC81F204Du), Float(0x119CC5BAu),
                Float(0x69C17F10u), Float(0x3DF6821Au), Byte(0x668912BDu)),
            new MainLightResourceReferences(
                Unsigned(0xD56E89F9u), Unsigned(0x183E0816u), Unsigned(0x556972D8u),
                Unsigned(0x00D75054u), Unsigned(0x2FCAA686u), Unsigned(0x05288AA2u),
                Unsigned(0xC45E26D5u)));
    }
}
