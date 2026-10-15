using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The thirteen fog components left to the CPU initialization producer.
/// Background and mode are supplied by the main-light constructor.</summary>
public readonly record struct MainLightCpuFogInitialization(
    float Start, float End, float PlanetRadius, Vector3 RayleighCoefficients,
    float Density, Vector3 MieCoefficients, float Anisotropy, float Pivot, float Blend);

/// <summary>Explicit numeric inputs for the independent main-light CPU backend.
/// Default preserves the project's zero initialization policy; it does not
/// represent observed values from an original allocation.</summary>
public readonly record struct MainLightCpuInitialization(
    MainLightCpuFogInitialization Fog, ShadowReceiverBias ReceiverBias)
{
    public static MainLightCpuInitialization Default => default;
}

/// <summary>Construct a main light without loading shaders or creating a
/// consumer-admission owner. Compatibility storage has defined project values;
/// the shared constructor supplies authored fields and actual resource lookups.</summary>
public static class MainLightCpuConstruction
{
    public static MainLightConstructionPacket Create(LoadedMainLightBindingSource source,
        uint contextFlags3E4, IReadOnlyList<MainLightConstructionResourceBinding> actualResourceBindings,
        MainLightCpuInitialization initialization)
    {
        // Only the named numeric inputs have producers here. The remaining
        // compatibility reservations, including the fog tail, probe, rain and
        // indirect colors, use project zero storage. New consumers of those
        // fields need their own actual producers, not inferred original values.
        byte[] interchange = new byte[MainLightConstruction.LightDataBytes];
        var fog = initialization.Fog;
        Put(interchange, 0x164, fog.Start);
        Put(interchange, 0x168, fog.End);
        Put(interchange, 0x16C, fog.PlanetRadius);
        Put(interchange, 0x170, fog.RayleighCoefficients);
        Put(interchange, 0x17C, fog.Density);
        Put(interchange, 0x180, fog.MieCoefficients);
        Put(interchange, 0x18C, fog.Anisotropy);
        Put(interchange, 0x190, fog.Pivot);
        Put(interchange, 0x194, fog.Blend);
        // Bias consumers intentionally inspect ordered comparisons and raw
        // floating-point bits. Their branch checks stay with those consumers.
        ShadowReceiverBiasEncoding.Write(initialization.ReceiverBias, interchange.AsSpan(0xF24, 8));
        return MainLightConstruction.Apply(source, interchange,
            new(contextFlags3E4,
                "project-owned-main-light-cpu-initialization;fog=explicit;receiver-bias=explicit;reserved=zero;native-retained-values-unresolved",
                actualResourceBindings));
    }

    private static void Put(Span<byte> destination, int offset, float value)
        => BinaryPrimitives.WriteSingleLittleEndian(destination[offset..], value);

    private static void Put(Span<byte> destination, int offset, Vector3 value)
    {
        Put(destination, offset, value.X);
        Put(destination, offset + 4, value.Y);
        Put(destination, offset + 8, value.Z);
    }
}
