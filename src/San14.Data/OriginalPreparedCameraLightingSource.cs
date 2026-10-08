using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalCameraLightingController(uint DeclaredTransmittanceTable,
    float TransitionSpan, float TransitionSuppression, float SunMargin,
    Vector2 SunHueSaturation, Vector2 MoonHueSaturation,
    float TimeSpanPerDay, int AutoDateTimeStepType);

public sealed record LoadedPreparedCameraLightingSource(LoadedCameraSkylight Source,
    OriginalCameraLightingController Controller);

/// <summary>Exact Camera source settings. No selection of an active Camera,
/// no inference that a declared zero table key is the current runtime pointer.</summary>
public static class OriginalPreparedCameraLightingSource
{
    /// <summary>The exact known C409C0 source-to-prepared input copies.
    /// Resolved parameters/controller are explicit; this neither advances a
    /// clock nor resolves the runtime table pointer outside the 3C0 prefix.</summary>
    public static byte[] ApplyResolvedInputsToPrefix(OriginalCameraLightingController controller,
        OriginalSkylightParameters resolvedParameters, ReadOnlySpan<byte> explicitPrefix)
    {
        ArgumentNullException.ThrowIfNull(controller); ArgumentNullException.ThrowIfNull(resolvedParameters);
        if (explicitPrefix.Length != OriginalPreparedCameraLighting.PrefixByteLength)
            throw new InvalidDataException("Original Camera source input copy requires an explicit 3C0-byte prefix.");
        float[] skylight = [resolvedParameters.MonthDay, resolvedParameters.DateTime, resolvedParameters.Longitude,
            resolvedParameters.Latitude, resolvedParameters.Altitude, resolvedParameters.NorthDir, resolvedParameters.MoonAge];
        (int Offset, float Value)[] controllerFloats = [(0x220, controller.TimeSpanPerDay),
            (0x288, controller.TransitionSpan), (0x28C, controller.TransitionSuppression), (0x290, controller.SunMargin),
            (0x29C, controller.SunHueSaturation.X), (0x2A0, controller.SunHueSaturation.Y),
            (0x2A4, controller.MoonHueSaturation.X), (0x2A8, controller.MoonHueSaturation.Y)];
        foreach (float value in skylight.Concat(controllerFloats.Select(x => x.Value)))
            if (!float.IsFinite(value)) throw new InvalidDataException("Original Camera source input copy requires finite explicit float values.");
        byte[] result = explicitPrefix.ToArray();
        for (int i = 0; i < skylight.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(0x200 + i * 4, 4), skylight[i]);
        foreach (var pair in controllerFloats) BinaryPrimitives.WriteSingleLittleEndian(result.AsSpan(pair.Offset, 4), pair.Value);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(0x3A0, 4), controller.AutoDateTimeStepType);
        return result;
    }

    public static LoadedPreparedCameraLightingSource ReadPerspective(LoadedObjects resource, uint objectId)
    {
        LoadedCameraSkylight source = OriginalCameraSkylightSource.ReadPerspective(resource, objectId);
        try { return new(source, ReadController(source.Camera)); }
        catch (InvalidDataException error)
        { throw new InvalidDataException($"{resource.Source.ResourceUri} Camera {objectId:X8}: {error.Message}", error); }
    }

    public static OriginalCameraLightingController ReadController(DokObject camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (camera.Kind != DokObjectKind.Inline || camera.ClassId != OriginalCameraSkylightSource.PerspectiveCameraClassId)
            throw new InvalidDataException("Original lighting controller requires an inline Perspective Camera; resolve reference inheritance explicitly.");
        return new(
            BinaryPrimitives.ReadUInt32LittleEndian(Property(camera, 0x063D4BE7, 5, 1).RawValue),
            Float(Property(camera, 0x7C80D02B, 8, 1), 0),
            Float(Property(camera, 0x484B95EB, 8, 1), 0),
            Float(Property(camera, 0x97D65DD2, 8, 1), 0),
            Pair(Property(camera, 0xC4DB66F5, 8, 2)),
            Pair(Property(camera, 0xA5B5D92A, 8, 2)),
            Float(Property(camera, 0xC579866B, 8, 1), 0),
            BinaryPrimitives.ReadInt32LittleEndian(Property(camera, 0xBE47DAC5, 4, 1).RawValue));
    }

    private static DokProperty Property(DokObject camera, uint hash, byte type, uint count)
    {
        DokProperty[] matches = camera.Properties.Where(p => p.NameHash == hash).ToArray();
        if (matches.Length != 1 || matches[0].TypeCode != type || matches[0].ElementCount != count ||
            matches[0].ElementSize != 4 || matches[0].RawValue is not { } raw || raw.Length != checked((int)count * 4))
            throw new InvalidDataException($"Original Camera lighting property {hash:X8} requires one unique type-{type} value with {count} float/int words.");
        return matches[0];
    }

    private static float Float(DokProperty property, int index)
    {
        float value = BinaryPrimitives.ReadSingleLittleEndian(property.RawValue.AsSpan(index * 4, 4));
        if (!float.IsFinite(value)) throw new InvalidDataException($"Original Camera lighting property {property.NameHash:X8} is not finite.");
        return value;
    }
    private static Vector2 Pair(DokProperty property) => new(Float(property, 0), Float(property, 1));
}
