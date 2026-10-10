namespace San14.Data;

public sealed class PreparedCameraLightingPacket
{
    private readonly byte[] bytes;
    internal PreparedCameraLightingPacket(ReadOnlySpan<byte> retainedPrefix, SkyLightingState lighting, bool clearAbsentTableFog = false)
    { Lighting = lighting; bytes = SkyLightingEncoding.Apply(retainedPrefix, lighting, clearAbsentTableFog); }
    public ReadOnlyMemory<byte> Prefix => bytes;
    public SkyLightingState Lighting { get; }
    public SkylightDirections Directions => new(Lighting.Sun.Direction, Lighting.Moon.Direction);
    public SkylightBaseColors BaseColors => new(Lighting.Sun.BaseColor, Lighting.Moon.BaseColor);
    public uint State => SkyLightingEncoding.EncodeOrder(Lighting.Transition);
    public float Elapsed => Lighting.Transition.Elapsed;
}

/// <summary>Absent-table lighting compatibility entry point. Retained celestial math is shared.</summary>
public static class PreparedCameraLighting
{
    public const int PrefixByteLength = 0x3C0;

    /// <summary>C3BF60 zeroes the complete embedded Camera before C3F000.
    /// This applies that constructor seed only to the lighting-owned fields in
    /// an explicit caller prefix; native pointers, lens and other fields remain
    /// caller-owned. It does not reset an already running Camera implicitly.</summary>
    public static byte[] InitializeConstructorLightingFields(ReadOnlySpan<byte> explicitPrefix)
    {
        return SkyLightingEncoding.InitializeConstructorLighting(explicitPrefix);
    }

    public static PreparedCameraLightingPacket PrepareWithoutTransmittanceTable(
        CameraLightingController controller, SkylightParameters resolvedParameters,
        ReadOnlySpan<byte> priorPrefix, float frameDelta)
    {
        ArgumentNullException.ThrowIfNull(controller); ArgumentNullException.ThrowIfNull(resolvedParameters);
        if (priorPrefix.Length != PrefixByteLength)
            throw new InvalidDataException("Prepared Camera lighting requires an explicit complete prior prefix.");
        var prior = SkyLightingEncoding.Decode(priorPrefix);
        SkyLightingTransition.ValidateInputs(controller, prior, frameDelta);
        var directions = SkylightDirectionMath.Calculate(resolvedParameters);
        var colors = SkylightColorMath.Prepare(controller.SunHueSaturation, controller.MoonHueSaturation);
        var lighting = SkyLightingTransition.Prepare(controller, directions, colors, colors, prior, frameDelta);
        return new(priorPrefix, lighting, clearAbsentTableFog: true);
    }
}
