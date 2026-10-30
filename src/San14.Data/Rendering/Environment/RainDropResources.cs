using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public sealed record RainDropTextureKeys(uint Drop, uint Splash, uint HeightMask, uint SnowFragment)
{
    public bool HasRainPair => Drop != 0 && Splash != 0;
    public bool HasParticles => HasRainPair || SnowFragment != 0;
}

/// <summary>The selected Camera declaration. Current precipitation and temperature
/// are supplied by the same prepared weather frame, not copied from a season default.</summary>
public sealed record RainDropCameraSettings(RainDropTextureKeys Textures,
    float RippleRateScale, float RippleMaximumPrecipitation, float RippleFadeInSeconds,
    float RippleFadeOutSeconds, float RippleFlowSpeedSlope, float DrawRadius,
    float HeightRange, float Bottom, float DeclaredPrecipitation, float DeclaredTemperature,
    float SplashLifeSeconds, float RippleLifeSeconds, uint Group)
{
    private static readonly ConditionalWeakTable<MapSeasonCameraPackage, RainDropCameraSettings> Imported = new();
    public static RainDropCameraSettings Import(MapSeasonCameraPackage package) => Imported.GetValue(package, Read);
    private static RainDropCameraSettings Read(MapSeasonCameraPackage package)
    {
        var r = new CameraNumericPropertyReader(package.Camera);
        return new(new(r.Word(680, 0x33D957C7, 5), r.Word(681, 0x48ABDE0E, 5),
            r.Word(718, 0xF57F839A, 5), r.Word(800, 0x69BB5466, 5)),
            r.Scalar(247, 0xB87AA426), r.Scalar(885, 0x7B5F56EF), r.Scalar(889, 0xC5A71F08),
            r.Scalar(890, 0x97493D87), r.Scalar(906, 0xC9B4C7A2), r.Scalar(687, 0x23455238),
            r.Scalar(688, 0x668B54CD), r.Scalar(689, 0x3CEF738E), r.Scalar(690, 0xD35643C0),
            r.Scalar(803, 0x6D06DBDE), r.Scalar(729, 0xD94E2590), r.Scalar(730, 0xB0950619),
            r.Word(701, 0x6EA5A887, 4));
    }
}

/// <summary>Actual retained Rain factory and its two shader declarations from
/// the renderer's existing ordered resource namespace.</summary>
public sealed record RainDropProviderSource(RenderResourceDescriptor Owner,
    RenderResourceDescriptor RainDeclaration, NamedModelShader RainShader, LoadedShaderContainer RainPrograms,
    RenderResourceDescriptor SnowDeclaration, NamedModelShader SnowShader, LoadedShaderContainer SnowPrograms,
    float MinimumInterval, uint CommonFlags)
{
    public const uint NormalMapObjectId = 0x3BDCA5CA;
    public bool IsCurrent(RenderResourceContext context) =>
        ReferenceEquals(Owner, context.Lookup(Owner.Object.ObjectId)) &&
        ReferenceEquals(RainDeclaration, context.Lookup(RainDeclaration.Object.ObjectId)) &&
        ReferenceEquals(SnowDeclaration, context.Lookup(SnowDeclaration.Object.ObjectId));
    public static RainDropProviderSource Load(GameResourceRepository repository, RenderResourceContext context)
    {
        var owner = context.Lookup(NormalMapObjectId) ?? throw new InvalidDataException("Current Rain provider is absent.");
        if (owner.Object.Kind != DokObjectKind.Inline || owner.Object.ClassId != 0x5781F63B)
            throw new InvalidDataException("Current Rain provider has no declared Rain factory.");
        uint Key(uint hash)
        {
            var field = owner.Object.Properties.Single(value => value.NameHash == hash);
            if (field.TypeCode != 5 || field.ElementCount != 1 || field.RawValue.Length != 4)
                throw new InvalidDataException("Rain provider requires a resource key declaration.");
            return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        (RenderResourceDescriptor, NamedModelShader, LoadedShaderContainer) Shader(uint key, uint expectedClass)
        {
            var descriptor = context.Lookup(key) ?? throw new InvalidDataException("Rain shader provider is absent.");
            if (descriptor.Object.Kind != DokObjectKind.Inline || descriptor.Object.ClassId != expectedClass)
                throw new InvalidDataException("Rain shader has another factory class.");
            var shader = ModelShaderResources.ReadDeclaredShader(descriptor.Source, key);
            return (descriptor, shader, ModelShaderResources.LoadDeclaredG1S(repository, shader));
        }
        var rain = Shader(Key(0xE6D26600), 0xEC656A13);
        var snow = Shader(Key(0xE75424AF), 0xCD5A3BC4);
        var common = repository.LoadObjects("KIDSSystemResource", CommonRenderResources.SourceFileId);
        float minimumInterval = BitConverter.UInt32BitsToSingle(CommonRenderResources.ReadScalar(common, 0x2A4D96DC, 8));
        var settings = CommonRenderResources.ImportSettings(common);
        return new(owner, rain.Item1, rain.Item2, rain.Item3, snow.Item1, snow.Item2, snow.Item3, minimumInterval,
            settings.Flags14 ?? throw new InvalidDataException("Rain current Common flags are absent."));
    }
}
