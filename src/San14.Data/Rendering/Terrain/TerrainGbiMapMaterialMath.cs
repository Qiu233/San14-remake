using System.Numerics;

namespace San14.Data;

/// <summary>Original PS18 plane weights and distance blend. Normal is the raw interpolated VS normal.</summary>
public static class TerrainGbiMapMaterialMath
{
    // X means YZ projection, Y means XZ, Z means XY.
    public static Vector3 PlaneWeights(Vector3 sourceNormal, float threshold)
    {
        if (!float.IsFinite(sourceNormal.X) || !float.IsFinite(sourceNormal.Y) || !float.IsFinite(sourceNormal.Z) ||
            !float.IsFinite(threshold)) throw new InvalidDataException("Nonfinite PS18 cliff input.");
        Vector3 a = Vector3.Abs(sourceNormal);
        float m = MathF.Max(MathF.Max(a.Z, a.Y), a.X);
        if (threshold + BitConverter.Int32BitsToSingle(0x38D1B717) >= m)
        {
            if (m == a.X) return Vector3.UnitX;
            if (m == a.Y) return Vector3.UnitY;
            return Vector3.UnitZ;
        }
        Vector3 w = Vector3.Max(a - new Vector3(threshold), Vector3.Zero);
        w = Vector3.Normalize(w);
        return w / ((w.Y + w.X) + w.Z);
    }

    public static TerrainGbiMaterialSample BlendPlanes(Vector3 weights,
        Func<int, TerrainGbiMaterialSample> samplePlane)
    {
        ArgumentNullException.ThrowIfNull(samplePlane);
        var result = new TerrainGbiMaterialSample(Vector4.Zero, Vector4.Zero, Vector4.Zero);
        for (int i = 0; i < 3; i++)
        {
            float w = i == 0 ? weights.X : i == 1 ? weights.Y : weights.Z;
            if (!float.IsFinite(w) || w < 0) throw new InvalidDataException("Invalid PS18 plane weight.");
            if (w == 0) continue;
            TerrainGbiMaterialSample sample = samplePlane(i);
            result = new(result.AlbedoOcclusion + sample.AlbedoOcclusion * w,
                result.NormalHeight + sample.NormalHeight * w,
                result.ReflectanceLinearRoughness + sample.ReflectanceLinearRoughness * w);
        }
        return result;
    }

    public static float FarWeight(Vector3 position, Vector3 eye, Vector2 range)
    {
        float distance = Vector3.Distance(position, eye);
        if (!float.IsFinite(distance) || !float.IsFinite(range.X) || !float.IsFinite(range.Y) || range.Y <= range.X)
            throw new InvalidDataException("Invalid PS18 distance blend input.");
        return distance >= range.X ? Math.Clamp((distance - range.X) / (range.Y - range.X), 0, 1) : 0;
    }

    public static TerrainGbiMaterialSample BlendFar(TerrainGbiMaterialSample detail,
        TerrainGbiMaterialSample far, float weight)
    {
        if (!float.IsFinite(weight) || weight < 0 || weight > 1) throw new InvalidDataException("Invalid PS18 distance blend weight.");
        // PS18 uses detail + (far-detail)*weight, rather than a reversed weighted sum.
        Vector3 normal = new(detail.NormalHeight.X, detail.NormalHeight.Y, detail.NormalHeight.Z);
        Vector3 farNormal = new(far.NormalHeight.X, far.NormalHeight.Y, far.NormalHeight.Z);
        return new(detail.AlbedoOcclusion + (far.AlbedoOcclusion - detail.AlbedoOcclusion) * weight,
            new Vector4(normal + (farNormal - normal) * weight, detail.NormalHeight.W),
            detail.ReflectanceLinearRoughness + (far.ReflectanceLinearRoughness - detail.ReflectanceLinearRoughness) * weight);
    }
}
