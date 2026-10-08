using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalSkylightProperty(string Name, uint NameHash, int SourceCpuOffset,
    int RenderStructOffset, uint ConstructorDefaultBits);

/// <summary>Original Ortho/Perspective sky inputs, independent of simulation dates and camera lens settings.</summary>
public sealed record OriginalSkylightParameters(float MonthDay, float DateTime, float Longitude,
    float Latitude, float Altitude, float NorthDir, float MoonAge)
{
    public static IReadOnlyList<OriginalSkylightProperty> Properties { get; } = Array.AsReadOnly(new[]
    {
        new OriginalSkylightProperty("SkylightMonthDay", 0x085B6991, 0xAC, 0x200, 0x3F800000),
        new OriginalSkylightProperty("SkylightDateTime", 0x1EB4D0F0, 0xB0, 0x204, 0xBF800000),
        new OriginalSkylightProperty("SkylightLongitude", 0x4170AF46, 0xB4, 0x208, 0x00000000),
        new OriginalSkylightProperty("SkylightLatitude", 0xFEEDD601, 0xB8, 0x20C, 0x00000000),
        new OriginalSkylightProperty("SkylightAltitude", 0xE599A7F7, 0xBC, 0x210, 0x41000000),
        new OriginalSkylightProperty("SkylightNorthDir", 0xF3554D1D, 0xC0, 0x214, 0x00000000),
        new OriginalSkylightProperty("SkylightMoonAge", 0xBF2E4097, 0xC4, 0x218, 0x3F000000)
    });

    /// <summary>Explicit original constructor seed, before source properties and runtime setters.</summary>
    public static OriginalSkylightParameters ConstructorDefaults() => new(1, -1, 0, 0, 8, 0, 0.5f);

    /// <summary>Seven contiguous float32 source fields AC..C7, or render fields 200..21B, without conversion.</summary>
    public static OriginalSkylightParameters ReadSourceFields(ReadOnlySpan<byte> fields)
    {
        if (fields.Length != 28)
            throw new InvalidDataException("Original Skylight requires exactly seven float32 source fields.");
        Span<float> values = stackalloc float[7];
        for (int i = 0; i < values.Length; i++)
            values[i] = BinaryPrimitives.ReadSingleLittleEndian(fields.Slice(i * 4, 4));
        return FromValues(values);
    }

    /// <summary>Apply this object's seven known properties to an explicit prior source state.
    /// The caller resolves DOK inheritance and retains the full object and resource provenance.</summary>
    public OriginalSkylightParameters ApplyProperties(IEnumerable<DokProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        Span<float> values = stackalloc float[] { MonthDay, DateTime, Longitude, Latitude, Altitude, NorthDir, MoonAge };
        Span<bool> seen = stackalloc bool[7];
        seen.Clear();
        foreach (DokProperty property in properties)
        {
            int index = -1;
            for (int i = 0; i < Properties.Count; i++)
                if (Properties[i].NameHash == property.NameHash) { index = i; break; }
            if (index < 0) continue;
            if (seen[index] || property.TypeCode != 8 || property.ElementCount != 1 ||
                property.ElementSize != 4 || property.RawValue is not { Length: 4 })
                throw new InvalidDataException($"Original {Properties[index].Name} property requires one unique type-8 float32 value.");
            seen[index] = true;
            values[index] = BinaryPrimitives.ReadSingleLittleEndian(property.RawValue);
        }
        return FromValues(values);
    }

    private static OriginalSkylightParameters FromValues(ReadOnlySpan<float> values)
    {
        for (int i = 0; i < values.Length; i++)
            if (!float.IsFinite(values[i]))
                throw new InvalidDataException($"Original {Properties[i].Name} input is not finite.");
        // In particular, DateTime=-1 and negative zero remain source values.
        return new(values[0], values[1], values[2], values[3], values[4], values[5], values[6]);
    }
}
