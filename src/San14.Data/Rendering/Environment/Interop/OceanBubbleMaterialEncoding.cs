using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The remake's OBM1 bubble author packet, shared by initial preparation
/// and current submission. It contains semantic settings rather than constant
/// buffers, source Camera storage, animation frames or retained runtime points.</summary>
public static class OceanBubbleMaterialEncoding
{
    public const int ByteLength = 284;
    public const string NativeFilename = "ocean-bubble-material.bin";

    public static void Write(OceanBubbleMaterialSettings settings, Span<byte> destination)
    {
        if (destination.Length < ByteLength)
            throw new ArgumentException("Ocean bubble material requires a complete OBM1 packet.", nameof(destination));
        BinaryPrimitives.WriteUInt32LittleEndian(destination, 0x314D424Fu); // OBM1
        var surface = settings.Surface;
        var inner = settings.Inner;
        var shore = settings.Shore;
        var surfaceBand = settings.SurfaceBand;
        var innerBand = settings.InnerBand;
        var shoreParameters = settings.ShoreParameters;
        var shoaling = settings.Shoaling;
        var spawn = settings.Spawn;
        ReadOnlySpan<float> values =
        [
            surface.Color.X, surface.Color.Y, surface.Color.Z, surface.Color.W,
            surface.Reflectance.X, surface.Reflectance.Y, surface.Reflectance.Z, surface.Roughness,
            surface.AlphaThreshold, surface.NormalThreshold,
            inner.Color.X, inner.Color.Y, inner.Color.Z, inner.Color.W,
            inner.Reflectance.X, inner.Reflectance.Y, inner.Reflectance.Z, inner.Roughness,
            inner.AlphaThreshold, inner.NormalThreshold,
            shore.Color.X, shore.Color.Y, shore.Color.Z, shore.Color.W,
            shore.Reflectance.X, shore.Reflectance.Y, shore.Reflectance.Z, shore.Roughness,
            shore.AlphaThreshold, shore.NormalThreshold,
            surfaceBand.BeginPhase, surfaceBand.Length, surfaceBand.FadingScale, surfaceBand.BanishingScale,
            innerBand.BeginPhase, innerBand.Length, innerBand.FadingScale, innerBand.BanishingScale,
            shoreParameters.DisplacementScale, shoreParameters.NormalScale,
            shoreParameters.GlossinessDamping, shoreParameters.BanishingScale,
            shoaling.Scale, shoaling.TransparencyDamping.X, shoaling.TransparencyDamping.Y, shoaling.TransparencyDamping.Z,
            settings.Depth, settings.Scattering.X, settings.Scattering.Y, settings.Scattering.Z, settings.Scattering.W,
            settings.SurfaceScale.X, settings.SurfaceScale.Y, settings.InnerScale.X, settings.InnerScale.Y,
            settings.ShoreScale.X, settings.ShoreScale.Y, settings.HorizontalScale.X, settings.HorizontalScale.Y,
            settings.AnimationSpeeds.X, settings.AnimationSpeeds.Y, settings.AnimationSpeeds.Z,
            spawn.PointScale, spawn.EmissionPeriod, spawn.SpeedMultiplier, spawn.Amplitude, spawn.EnvelopeDuration,
            settings.MaskScales.X, settings.MaskScales.Y, settings.GeneratorPeriod,
        ];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(4 + index * 4, 4), values[index]);
    }

    public static void WriteNativeInputs(string directory, OceanBubbleMaterialSettings settings)
    {
        byte[] bytes = new byte[ByteLength];
        Write(settings, bytes);
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, NativeFilename), bytes);
    }
}
