using System.Buffers.Binary;

namespace San14.Data;

/// <summary>Defined fourth components for the CPU backend's hemisphere
/// vectors. They participate in hemisphere preparation and remain opaque
/// transport components. Defaults preserve the project's existing zero-filled
/// backend; they do not represent observed original allocation values.</summary>
public readonly record struct DirectionalLightCpuInitialization(
    float UpperHemisphereFourth, float LowerHemisphereFourth)
{
    public static DirectionalLightCpuInitialization Default => new(0, 0);
}

/// <summary>Resource construction for the independent CPU numeric route.
/// Compatibility storage starts defined and zeroed; authored fields, resource
/// lookups and numeric preparation use the shared source constructor.
/// Shader selection and GPU consumer admission belong to their own routes.</summary>
public static class DirectionalLightCpuConstruction
{
    public static DirectionalLightConstructionPacket Create(
        MapSeasonDirectionalLightSourceSet source, uint objectId, uint contextFlags,
        IReadOnlyList<DirectionalLightResourceBinding> bindings,
        DirectionalLightCpuInitialization initialization)
    {
        if (!float.IsFinite(initialization.UpperHemisphereFourth) ||
            !float.IsFinite(initialization.LowerHemisphereFourth))
            throw new InvalidDataException("CPU hemisphere fourth components must be finite.");

        // These two retained components are actual numeric inputs. Other
        // compatibility reservations have the backend's defined zero values.
        // The shared constructor replaces every authored field and prepares
        // the hemisphere and classifiers. Provider and current fog writers
        // continue to run at their existing preparation boundaries.
        byte[] interchange = new byte[DirectionalLightConstruction.LightDataBytes];
        BinaryPrimitives.WriteSingleLittleEndian(interchange.AsSpan(0x10C), initialization.UpperHemisphereFourth);
        BinaryPrimitives.WriteSingleLittleEndian(interchange.AsSpan(0x11C), initialization.LowerHemisphereFourth);
        return DirectionalLightConstruction.Apply(source, objectId, interchange,
            new(contextFlags, "project-owned-directional-cpu-initialization;reserved=zero;hemisphere-fourth=explicit", bindings));
    }
}
