using System.Numerics;

namespace San14.Data;

/// <summary>Samples supplied in the space returned by the three original SRVs.</summary>
public readonly record struct TerrainGbiMaterialSample(Vector4 AlbedoOcclusion,
    Vector4 NormalHeight, Vector4 ReflectanceLinearRoughness);

/// <summary>Original control RGB, in corner order 00, 10, 01, 11.</summary>
public readonly record struct TerrainGbiControlQuad(Vector3 C00, Vector3 C10, Vector3 C01, Vector3 C11);

public readonly record struct TerrainGbiSurfaceResult(Vector4 AlbedoOcclusion,
    Vector3 EncodedNormal, Vector4 ReflectanceLinearRoughness, float BlendAmount);

public readonly record struct TerrainGbiControlCoordinates(Vector2 Fraction,
    Vector2 C00, Vector2 C10, Vector2 C01, Vector2 C11);

/// <summary>The five PS13 material targets, before lighting; tool switches off.</summary>
public readonly record struct TerrainGbiBufferResult(Vector4 Target0, Vector4 Target1,
    Vector4 Target2, Vector4 Target3, Vector4 Target4);

/// <summary>
/// Finite-input reference math for DC52FD1C PS13 and the height sum in DS141.
/// Receives sampled texels explicitly. It neither selects that program for a scene
/// nor invents physical layers, sampler states, coordinate transforms or lighting.
/// GPU refactoring and exceptional arithmetic are not claimed bit-identical.
/// </summary>
public static class TerrainGbiMaterialMath
{
    public const string PixelProgramSha256 = "ABCF4B97C00C6FE13D73A0D72EC65905560419648449A55F88302FA893F7C8BB";
    public const string DomainProgramSha256 = "A502DEC11CF21DE7BFF2DEE604C3251DF14C5316101CF5675B9AF787F0BEA644";
    public const int MissingSecondaryIndex = 255;
    public const uint MaterialIdScaleBits = 0x37800080;

    /// <summary>Normalized control channel * 255, then original ftoi truncation.</summary>
    public static int DecodePaletteIndex(float channel)
    {
        if (!float.IsFinite(channel) || channel < 0 || channel > 1)
            throw new InvalidDataException("GBI control index is outside finite UNORM input.");
        return (int)(channel * 255f);
    }

    /// <summary>PS13 register inputs remain explicit; no presumed map UV convention.</summary>
    public static TerrainGbiControlCoordinates ControlCoordinates(Vector2 phase, float frequency,
        float count, Vector2 controlUv)
    {
        Vector2 scaled = phase * frequency;
        if (!float.IsFinite(scaled.X) || !float.IsFinite(scaled.Y) || !float.IsFinite(count) || count <= 1 ||
            !float.IsFinite(controlUv.X) || !float.IsFinite(controlUv.Y))
            throw new InvalidDataException("GBI control coordinates have unsupported inputs.");
        Vector2 fraction = new(scaled.X - MathF.Floor(scaled.X), scaled.Y - MathF.Floor(scaled.Y));
        float g = 1f / (count - 1f);
        return new(fraction, controlUv + new Vector2(-fraction.X, -fraction.Y) * g,
            controlUv + new Vector2(1f - fraction.X, -fraction.Y) * g,
            controlUv + new Vector2(-fraction.X, 1f - fraction.Y) * g,
            controlUv + new Vector2(1f - fraction.X, 1f - fraction.Y) * g);
    }

    public static bool IsDiscarded(float centerControlRed, Func<int, float> scaleU)
    {
        ArgumentNullException.ThrowIfNull(scaleU);
        float value = scaleU(DecodePaletteIndex(centerControlRed));
        if (!float.IsFinite(value)) throw new InvalidDataException("GBI ScaleU is nonfinite.");
        return value <= 1f;
    }

    public static Vector4 CornerWeights(Vector2 fraction)
    {
        if (!float.IsFinite(fraction.X) || !float.IsFinite(fraction.Y) ||
            fraction.X < 0 || fraction.X >= 1 || fraction.Y < 0 || fraction.Y >= 1)
            throw new InvalidDataException("GBI cell fraction must be in [0,1).");
        float x = fraction.X, y = fraction.Y;
        return new((1f - y) * (1f - x), (1f - y) * x, (1f - x) * y, y * x);
    }

    /// <summary>
    /// Blends all channels of A/B with normal-height alpha. Sharpness belongs to B.
    /// beta is the blue-channel sum of the whole quad, not this corner's blue.
    /// </summary>
    public static TerrainGbiMaterialSample HeightBlend(TerrainGbiMaterialSample a,
        TerrainGbiMaterialSample b, float beta, float secondarySharpness)
    {
        Check(a); Check(b);
        if (!float.IsFinite(beta) || beta < 0 || beta > 1 ||
            !float.IsFinite(secondarySharpness) || secondarySharpness < 0 || secondarySharpness >= 1)
            throw new InvalidDataException("GBI height blend needs finite beta [0,1] and sharpness [0,1).");
        float qA = a.NormalHeight.W;
        float qB = (beta - 0.5f) * 2f + b.NormalHeight.W;
        float threshold = MathF.Max(qB, qA) - (1f - secondarySharpness);
        float wA = MathF.Max(qA - threshold, 0f);
        float wB = MathF.Max(qB - threshold, 0f);
        float sum = wB + wA;
        if (!float.IsFinite(sum) || sum <= 0)
            throw new InvalidDataException("GBI height blend has unsupported exceptional weights.");
        // Original: B*wB, mad A*wA, then divide. Do not replace with beta lerp.
        return new(Combine(a.AlbedoOcclusion, b.AlbedoOcclusion, wA, wB, sum),
            Combine(a.NormalHeight, b.NormalHeight, wA, wB, sum),
            Combine(a.ReflectanceLinearRoughness, b.ReflectanceLinearRoughness, wA, wB, sum));
    }

    /// <summary>
    /// sample receives (corner ordinal, palette ordinal); each caller controls its
    /// source-specific array/UV/gradient binding. Identical control RGB reuses the
    /// first corner result, as in PS13. No unknown 255-row tail is synthesized.
    /// </summary>
    public static TerrainGbiSurfaceResult EvaluateQuad(TerrainGbiControlQuad controls, Vector2 fraction,
        Func<int, int, TerrainGbiMaterialSample> sample, Func<int, float> blendSharpness)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(blendSharpness);
        Vector4 weights = CornerWeights(fraction);
        Vector3[] c = [controls.C00, controls.C10, controls.C01, controls.C11];
        foreach (Vector3 control in c)
            if (!Finite(control) || control.X < 0 || control.X > 1 || control.Y < 0 || control.Y > 1 ||
                control.Z < 0 || control.Z > 1)
                throw new InvalidDataException("GBI control quad is outside finite UNORM input.");
        float beta = Sum(c[0].Z, c[1].Z, c[2].Z, c[3].Z, weights);
        var result = new TerrainGbiMaterialSample[4];
        for (int i = 0; i < 4; i++)
        {
            int previous = Array.FindIndex(c, 0, i, control => control == c[i]);
            if (previous >= 0) { result[i] = result[previous]; continue; }
            int first = DecodePaletteIndex(c[i].X), second = DecodePaletteIndex(c[i].Y);
            if (second >= MissingSecondaryIndex) result[i] = sample(i, first);
            else if (c[i].Z > 0.99f) result[i] = sample(i, second);
            else result[i] = HeightBlend(sample(i, first), sample(i, second), beta, blendSharpness(second));
            Check(result[i]);
        }
        Vector4 albedo = Sum(result[0].AlbedoOcclusion, result[1].AlbedoOcclusion,
            result[2].AlbedoOcclusion, result[3].AlbedoOcclusion, weights);
        Vector4 normal = Sum(result[0].NormalHeight, result[1].NormalHeight,
            result[2].NormalHeight, result[3].NormalHeight, weights);
        Vector4 reflectance = Sum(result[0].ReflectanceLinearRoughness, result[1].ReflectanceLinearRoughness,
            result[2].ReflectanceLinearRoughness, result[3].ReflectanceLinearRoughness, weights);
        return new(albedo, new(normal.X, normal.Y, normal.Z), reflectance, beta);
    }

    /// <summary>PS13 normal: normalize(2*RGB-1), cross(T,N)*handedness for X.</summary>
    public static Vector3 TransformNormal(Vector3 encodedNormal, Vector3 normal, Vector3 tangent, float handedness)
    {
        if (!float.IsFinite(handedness)) throw new InvalidDataException("GBI handedness is nonfinite.");
        Vector3 nt = Normalize(encodedNormal * 2f - Vector3.One);
        Vector3 n = Normalize(normal), t = Normalize(tangent);
        // No final normalize: this is the original pre-G-buffer basis computation.
        return Vector3.Cross(t, n) * handedness * nt.X + t * nt.Y + n * nt.Z;
    }

    public static Vector2 EncodeOctahedral(Vector3 normal)
    {
        float sum = MathF.Abs(normal.X) + MathF.Abs(normal.Y) + MathF.Abs(normal.Z);
        if (!Finite(normal) || !float.IsFinite(sum) || sum <= 0)
            throw new InvalidDataException("GBI octahedral normal has unsupported exceptional length.");
        Vector3 q = normal / sum;
        Vector2 xy = q.Z >= 0 ? new(q.X, q.Y)
            : new((1f - MathF.Abs(q.Y)) * (q.X >= 0 ? 1f : -1f),
                (1f - MathF.Abs(q.X)) * (q.Y >= 0 ? 1f : -1f));
        return xy * 0.5f + new Vector2(0.5f);
    }

    /// <summary>
    /// Uses explicit original mWV row directions. Does not apply brightness,
    /// convert roughness for another renderer, or add a light to the material.
    /// </summary>
    public static TerrainGbiBufferResult EncodeMaterialBuffer(TerrainGbiSurfaceResult surface,
        Vector3 normal, Vector3 tangent, float handedness, Vector3 viewRow0, Vector3 viewRow1,
        Vector3 viewRow2, Vector4 specularScale, int materialId)
    {
        if (materialId < 0 || !Finite(viewRow0) || !Finite(viewRow1) || !Finite(viewRow2) ||
            !Finite(specularScale) || !Finite(surface.AlbedoOcclusion) || !Finite(surface.ReflectanceLinearRoughness))
            throw new InvalidDataException("GBI material-buffer input is unsupported.");
        Vector3 ns = TransformNormal(surface.EncodedNormal, normal, tangent, handedness);
        Vector3 t = Normalize(tangent), n = Normalize(normal);
        Vector3 orthogonalTangent = Normalize(t - Vector3.Dot(t, ns) * ns);
        Vector2 view = EncodeOctahedral(new(Vector3.Dot(viewRow0, ns), Vector3.Dot(viewRow1, ns), Vector3.Dot(viewRow2, ns)));
        Vector2 encodedN = EncodeOctahedral(n), encodedT = EncodeOctahedral(orthogonalTangent);
        uint packedId = unchecked((uint)(float)materialId << 8);
        Vector4 reflection = surface.ReflectanceLinearRoughness * specularScale;
        reflection.W *= 0.5f;
        return new(surface.AlbedoOcclusion,
            new(view, 0, packedId * BitConverter.UInt32BitsToSingle(MaterialIdScaleBits)),
            reflection, Vector4.Zero, new(encodedN.X, encodedN.Y, encodedT.X, encodedT.Y));
    }

    /// <summary>DS141: control R only, alpha*scale+bias per corner, then interpolate.</summary>
    public static float EvaluateHeight(Vector4 sampledAlpha, Vector4 scale, Vector4 bias, Vector2 fraction)
    {
        if (!Finite(sampledAlpha) || !Finite(scale) || !Finite(bias))
            throw new InvalidDataException("GBI displacement inputs are nonfinite.");
        Vector4 weights = CornerWeights(fraction);
        Vector4 heights = sampledAlpha * scale + bias;
        return Sum(heights.X, heights.Y, heights.Z, heights.W, weights);
    }

    /// <summary>DS141 largest absolute normal component: ties choose X, then Y.</summary>
    public static Vector2 DisplacementPlane(Vector3 position, Vector3 normal)
    {
        if (!Finite(position) || !Finite(normal)) throw new InvalidDataException("GBI projection inputs are nonfinite.");
        Vector3 absolute = Vector3.Abs(normal);
        if (absolute.X >= absolute.Y && absolute.X >= absolute.Z) return new(position.Y, position.Z);
        if (absolute.Y >= absolute.Z) return new(position.X, position.Z);
        return new(position.X, position.Y);
    }

    /// <summary>DS141 distance predicate and fade; direction is deliberately not normalized.</summary>
    public static Vector3 Displace(Vector3 position, Vector3 direction, float viewDepth,
        float cutoff, float fadeScale, float fadeMultiplier, float height)
    {
        if (!Finite(position) || !Finite(direction) || !float.IsFinite(viewDepth) || !float.IsFinite(cutoff) ||
            !float.IsFinite(fadeScale) || !float.IsFinite(fadeMultiplier) || !float.IsFinite(height))
            throw new InvalidDataException("GBI displacement inputs are nonfinite.");
        if (!(-viewDepth < cutoff)) return position;
        float fade = Math.Clamp((viewDepth * fadeScale + 1f) * fadeMultiplier, 0f, 1f);
        return position + direction * (fade * height);
    }

    private static Vector4 Combine(Vector4 a, Vector4 b, float wA, float wB, float sum) =>
        (b * wB + a * wA) / sum;
    private static float Sum(float c00, float c10, float c01, float c11, Vector4 w) =>
        ((c10 * w.Y + c00 * w.X) + c01 * w.Z) + c11 * w.W;
    private static Vector4 Sum(Vector4 c00, Vector4 c10, Vector4 c01, Vector4 c11, Vector4 w) =>
        ((c10 * w.Y + c00 * w.X) + c01 * w.Z) + c11 * w.W;
    private static void Check(TerrainGbiMaterialSample sample)
    {
        if (!Finite(sample.AlbedoOcclusion) || !Finite(sample.NormalHeight) || !Finite(sample.ReflectanceLinearRoughness))
            throw new InvalidDataException("GBI material sample is nonfinite.");
    }
    private static Vector3 Normalize(Vector3 value)
    {
        float length = value.LengthSquared();
        if (!Finite(value) || !float.IsFinite(length) || length <= 0)
            throw new InvalidDataException("GBI normal has unsupported exceptional length.");
        return value / MathF.Sqrt(length);
    }
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
    private static bool Finite(Vector4 v) => Finite(new Vector3(v.X, v.Y, v.Z)) && float.IsFinite(v.W);
}
