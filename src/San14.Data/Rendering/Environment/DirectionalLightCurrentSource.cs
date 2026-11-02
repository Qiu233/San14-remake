using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Current numeric light values. No complete LightData or camera
/// byte image is retained by this domain state.</summary>
public sealed class CurrentDirectionalLightState
{
    internal CurrentDirectionalLightState(HdrLightState lighting, LightExposureSettings exposure,
        ShadowReceiverBias receiverBias, PatternLightModifier patternModifier,
        Vector4 sunBaseline, Vector4 moonBaseline, byte casterPlane, DirectionalLightFogState fog,
        IReadOnlyList<Vector4> quaternions, Vector4 hemisphereDirection,
        Vector3 lensFlareSourceVector, byte casterPlaneForLocalLight, byte casterPlaneForReflectiveShadowMap,
        bool contextApplied, bool mode6Applied, bool swapped, bool thirdColorMixEnabled,
        ContactOcclusionLightModifier contactOcclusion)
    {
        Lighting = lighting; Exposure = exposure; ReceiverBias = receiverBias;
        PatternModifier = patternModifier; SunBaseline = sunBaseline; MoonBaseline = moonBaseline;
        HeaderCasterPlane = casterPlane; Fog = fog; CurrentQuaternions = quaternions;
        HemisphereDirection = hemisphereDirection;
        LensFlareSourceVector = lensFlareSourceVector;
        CasterPlaneForLocalLight = casterPlaneForLocalLight;
        CasterPlaneForReflectiveShadowMap = casterPlaneForReflectiveShadowMap;
        ContextApplied = contextApplied; Mode6Applied = mode6Applied; SourceHighBitSwappedRows = swapped;
        ThirdColorMixEnabled = thirdColorMixEnabled;
        ContactOcclusion = contactOcclusion;
    }

    public HdrLightState Lighting { get; }
    public LightExposureSettings Exposure { get; }
    public ShadowReceiverBias ReceiverBias { get; }
    public PatternLightModifier PatternModifier { get; }
    public Vector4 SunBaseline { get; }
    public Vector4 MoonBaseline { get; }
    public byte HeaderCasterPlane { get; }
    public DirectionalLightFogState Fog { get; }
    public IReadOnlyList<Vector4> CurrentQuaternions { get; }
    public Vector4 HemisphereDirection { get; }
    public Vector3 LensFlareSourceVector { get; }
    public byte CasterPlaneForLocalLight { get; }
    public byte CasterPlaneForReflectiveShadowMap { get; }
    public bool ContextApplied { get; }
    public bool Mode6Applied { get; }
    public bool SourceHighBitSwappedRows { get; }
    public bool ThirdColorMixEnabled { get; }
    public ContactOcclusionLightModifier ContactOcclusion { get; }
}

/// <summary>The actual root/root provider temporary imported once. Its arithmetic
/// and retained representation have already run before current camera production.</summary>
public sealed class DirectionalLightCurrentSource
{
    private readonly CurrentDirectionalLightState initial;
    private readonly uint contextFlags;
    private readonly byte mode;
    private readonly bool mixThirdColor;
    private readonly Vector4 hemisphereQuaternion;

    private DirectionalLightCurrentSource(ReadOnlySpan<byte> bytes,uint context)
    {
        var lighting = HdrLightEncoding.Read(bytes);
        Vector4 sun = Four(bytes, 0xFA0), moon = Four(bytes, 0xFB0);
        mode = bytes[0x10B0]; mixThirdColor = bytes[0x10B2] == 8;
        contextFlags = context;
        hemisphereQuaternion = Four(bytes, 0xF0);
        initial = new(lighting, new(mode, bytes[0xFDC], new(sun.X, sun.Y, sun.Z)),
            ShadowReceiverBiasEncoding.ReadLight(bytes), PatternCurrentEncoding.ReadLight(bytes, lighting.Modifier.Present),
            sun, moon, bytes[0xF1C], DirectionalLightFogState.Read(bytes[0x150..]),
            Array.AsReadOnly(new[] { Four(bytes, 0xA0), Four(bytes, 0xB0), Four(bytes, 0xC0), Four(bytes, 0xD0) }),
            Four(bytes, 0x120), Three(bytes, 0xF80), bytes[0xF1F], bytes[0xF20], false, false, false, mixThirdColor,
            ContactOcclusionCurrentEncoding.ImportLight(bytes, lighting.Modifier.Present));
    }

    public static DirectionalLightCurrentSource FromTemporary(DirectionalLightProviderTemporary temporary)
    {
        ArgumentNullException.ThrowIfNull(temporary);
        return new(temporary.Bytes.Span,temporary.Root.ContextFlags3E4);
    }

    public static DirectionalLightCurrentSource FromPreparedConstruction(
        DirectionalLightConstructionPacket selected,ReadOnlySpan<byte> prepared)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if(prepared.Length!=DirectionalLightConstruction.LightDataBytes)
            throw new InvalidDataException("Selected actor requires its complete prepared light data.");
        return new(prepared,selected.ContextFlags3E4);
    }

    public CurrentDirectionalLightState PrepareCamera(ReadOnlySpan<byte> preparedCamera)
    {
        // Context bypass must not start validating an otherwise unused camera.
        if ((contextFlags & 3) != 1) return initial;
        return PrepareCamera(DirectionalLightCurrentCamera.Capture(preparedCamera));
    }

    public CurrentDirectionalLightState PrepareCamera(DirectionalLightCurrentCamera camera)
    {
        if ((contextFlags & 3) != 1) return initial;
        ArgumentNullException.ThrowIfNull(camera);
        HdrLightState lighting = initial.Lighting;
        if (mode == 2)
        {
            if (camera.AmbientScaleBits != 0x3F800000u)
                throw new NotSupportedException("Mode-two current lighting requires the prepared camera ambient scale to be exactly one.");
            var result = lighting.ShadowModes.Local == 2 ? lighting : WithCurrentLighting(lighting,
                lighting.Directional.ToArray(), new(lighting.ShadowModes.Receive, 2));
            return new(result, initial.Exposure, initial.ReceiverBias, initial.PatternModifier,
                initial.SunBaseline, initial.MoonBaseline, initial.HeaderCasterPlane, initial.Fog,
                initial.CurrentQuaternions, initial.HemisphereDirection, initial.LensFlareSourceVector,
                initial.CasterPlaneForLocalLight, initial.CasterPlaneForReflectiveShadowMap, true, false, false, mixThirdColor,
                initial.ContactOcclusion);
        }
        if (mode != 6)
            throw new NotSupportedException("Current directional lighting requires its implemented mode-two or mode-six camera branch.");

        var directions = camera.PrepareMode6Directions();
        int first = camera.SourceHighBitSwappedRows ? 0 : 1, second = 1 - first;
        var quaternions = initial.CurrentQuaternions.ToArray();
        quaternions[first] = directions.First; quaternions[second] = directions.Second;
        var lights = lighting.Directional.ToArray();
        for (int index = 0; index < lights.Length; index++)
            lights[index] = lights[index] with { Direction = DirectionalLightSource.RotatePreparedQuaternion(quaternions[index]) };
        Vector4 hemisphereDirection = DirectionalLightSource.RotatePreparedQuaternion(hemisphereQuaternion, true);
        lights[first] = lights[first] with { Color = camera.FirstColor };
        lights[second] = lights[second] with { Color = camera.SecondColor };
        if (mixThirdColor)
            lights[2] = lights[2] with
            {
                Color = CameraDirectionalLightColorMixing.ApplyPreparedMode6ThirdColor(
                    lights[0].Color, lights[2].Color, camera.ThirdLightIntensity, camera.ThirdLightMix)
            };
        var current = WithCurrentLighting(lighting, lights, new(lighting.ShadowModes.Receive, 2));
        var exposure = new LightExposureSettings(mode, camera.SourceHighBitSwappedRows ? (byte)0 : (byte)2,
            new(camera.SunBaseline.X, camera.SunBaseline.Y, camera.SunBaseline.Z));
        return new(current, exposure, initial.ReceiverBias, initial.PatternModifier,
            camera.SunBaseline, camera.MoonBaseline, initial.HeaderCasterPlane, camera.Fog,
            Array.AsReadOnly(quaternions), hemisphereDirection, camera.LensFlareSourceVector,
            initial.CasterPlaneForLocalLight, initial.CasterPlaneForReflectiveShadowMap,
            true, true, camera.SourceHighBitSwappedRows, mixThirdColor, initial.ContactOcclusion);
    }

    private static HdrLightState WithCurrentLighting(HdrLightState source,
        DeferredDirectionalLight[] lights, HdrShadowModes modes) => new(lights,
        source.Ambient, source.Modifier, source.Diffuse, source.DiffuseIntensity,
        source.Specular, source.SpecularIntensity, source.Hemisphere, modes);

    private static Vector4 Four(ReadOnlySpan<byte> bytes, int at) => new(
        BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + 4)..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + 8)..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + 12)..]));
    private static Vector3 Three(ReadOnlySpan<byte> bytes, int at) => new(
        BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + 4)..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[(at + 8)..]));
}
