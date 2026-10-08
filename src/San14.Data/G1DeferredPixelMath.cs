using System.Numerics;

namespace San14.Data;

public readonly record struct G1DeferredPackedMaterial(uint PackedW, uint PackedZ,
    uint MaterialTableIndex, float LowWUnit, float HighZUnit);
public readonly record struct G1DeferredMaterialTableWord(uint RawValue, int RimIndex, int RampIndex);
public readonly record struct G1DeferredPrimaryLobe(Vector3 Fresnel, float Distribution,
    float Visibility, float NormalDotLight);
public readonly record struct G1DeferredOcclusion(float SpecularOcclusion,
    float DirectDiffuseFactor, float DirectSpecularFactor);

/// <summary>Finite-input subexpressions of original 9D8E811F VS0/PS1.
/// Texels, matrices and branch inputs are explicit; no SRV attachment, active pass,
/// unsupported material branch or final map lighting is inferred. Algebraic projections
/// do not claim cross-device bit identity with a refactoringAllowed shader.</summary>
public static class G1DeferredPixelMath
{
    public const string VertexProgramSha256 = "C51D71A59DC89E4B23B68395180A41473B35914AD33BCE80C92AF636A379757D";
    public const string PixelProgramSha256 = "B01248B54B5D09A98D850D4627520F78808EA14C1CF090F3C804753E78F1884E";
    public const uint UnitByteScaleBits = 0x3B808081;
    public const uint DistributionEpsilonBits = 0x38D1B717;

    /// <summary>VS0 writes this same float4 to position and TEXCOORD; these are not UVs.</summary>
    public static Vector4 FullscreenVertex(uint vertexId) =>
        new((float)(vertexId >> 1) * 4f - 1f, (float)(vertexId & 1u) * -4f + 1f, 0f, 1f);

    /// <summary>PS1 t14.z/w packing for finite UNORM inputs. No additional material-index mask.</summary>
    public static G1DeferredPackedMaterial DecodePackedMaterial(Vector4 sample)
    {
        Check(sample);
        if (sample.Z < 0f || sample.Z > 1f || sample.W < 0f || sample.W > 1f)
            throw new InvalidDataException("Deferred packed material needs supported UNORM z/w input.");
        uint a = (uint)(sample.W * 65535f), b = (uint)(sample.Z * 65535f);
        float scale = BitConverter.UInt32BitsToSingle(UnitByteScaleBits);
        return new(a, b, a >> 8, (a & 255u) * scale, (b >> 8) * scale);
    }

    /// <summary>PS1 t25 word; preserve the whole word and do not assign one meaning to shared flags.</summary>
    public static G1DeferredMaterialTableWord DecodeMaterialTableWord(uint word) =>
        new(word, (int)Math.Min((word >> 8) & 255u, 15u), (int)((word >> 16) & 255u));

    /// <summary>PS1 234..250: zero has positive sign; preserve negative z after folding.</summary>
    public static Vector3 DecodeOctahedral(Vector2 encoded)
    {
        if (!Finite(encoded) || encoded.X < 0 || encoded.X > 1 || encoded.Y < 0 || encoded.Y > 1)
            throw new InvalidDataException("Deferred octahedral input must be finite UNORM.");
        float x = encoded.X * 2f - 1f, y = encoded.Y * 2f - 1f;
        float z = (1f - MathF.Abs(x)) - MathF.Abs(y);
        if (z < 0f)
        {
            float foldedX = (x >= 0f ? 1f : -1f) * (1f - MathF.Abs(y));
            y = (y >= 0f ? 1f : -1f) * (1f - MathF.Abs(x));
            x = foldedX;
        }
        return Normalize(new(x, y, z));
    }

    /// <summary>PS1 mV2W rows0..2.xyz. The source does not normalize after this transform.</summary>
    public static Vector3 TransformViewNormal(Vector3 viewNormal, Matrix4x4 viewToWorld)
    {
        Check(viewNormal);
        Vector3 r0 = new(viewToWorld.M11, viewToWorld.M12, viewToWorld.M13);
        Vector3 r1 = new(viewToWorld.M21, viewToWorld.M22, viewToWorld.M23);
        Vector3 r2 = new(viewToWorld.M31, viewToWorld.M32, viewToWorld.M33);
        Check(r0); Check(r1); Check(r2);
        Vector3 result = r1 * viewNormal.Y + r0 * viewNormal.X;
        result = r2 * viewNormal.Z + result;
        Check(result);
        return result;
    }

    /// <summary>PS1 226..230: supplied source clip xy/depth and row_major mP2W, then divide by w.</summary>
    public static Vector3 ReconstructWorldPosition(Vector2 sourceClipXY, float depth, Matrix4x4 projectionToWorld)
    {
        if (!Finite(sourceClipXY) || !float.IsFinite(depth))
            throw new InvalidDataException("Deferred reconstruction has nonfinite clip/depth input.");
        Vector4 r0 = new(projectionToWorld.M11, projectionToWorld.M12, projectionToWorld.M13, projectionToWorld.M14);
        Vector4 r1 = new(projectionToWorld.M21, projectionToWorld.M22, projectionToWorld.M23, projectionToWorld.M24);
        Vector4 r2 = new(projectionToWorld.M31, projectionToWorld.M32, projectionToWorld.M33, projectionToWorld.M34);
        Vector4 r3 = new(projectionToWorld.M41, projectionToWorld.M42, projectionToWorld.M43, projectionToWorld.M44);
        Check(r0); Check(r1); Check(r2); Check(r3);
        Vector4 world = sourceClipXY.Y * r1 + sourceClipXY.X * r0;
        world = depth * r2 + world;
        world += r3;
        Check(world);
        if (world.W == 0f) throw new InvalidDataException("Deferred reconstruction has zero homogeneous w.");
        Vector3 result = new(world.X / world.W, world.Y / world.W, world.Z / world.W);
        Check(result);
        return result;
    }

    public static Vector3 ViewDirection(Vector3 worldPosition, Vector3 sourceEye)
    {
        Check(worldPosition); Check(sourceEye);
        return Normalize(sourceEye - worldPosition);
    }

    /// <summary>Light0 is unconditional; original nDirLit gates lights1/2/3 with >0/>1/>2.</summary>
    public static bool UsesDirectionalLight(int lightIndex, int rawNDirLit)
    {
        if (lightIndex is < 0 or > 3) throw new InvalidDataException("Deferred directional slot is outside the four source slots.");
        return lightIndex == 0 || rawNDirLit > lightIndex - 1;
    }

    /// <summary>PS1 primary ordinary front lobe only. Pass L=-litDir, u=2*G10.w.
    /// This kernel excludes the second lobe, backside, transmission, ramp and energy mixing.</summary>
    public static G1DeferredPrimaryLobe EvaluatePrimaryLobe(Vector3 normal, Vector3 viewDirection,
        Vector3 directionToLight, Vector3 reflectance, float roughness)
    {
        Check(normal); Check(viewDirection); Check(directionToLight); Check(reflectance);
        if (!float.IsFinite(roughness) || roughness < 0 || roughness > 1 ||
            reflectance.X < 0 || reflectance.X > 1 || reflectance.Y < 0 || reflectance.Y > 1 || reflectance.Z < 0 || reflectance.Z > 1)
            throw new InvalidDataException("Deferred primary lobe has unsupported roughness or reflectance.");
        float nl = Vector3.Dot(normal, directionToLight);
        if (!float.IsFinite(nl) || nl < 0)
            throw new InvalidDataException("Deferred primary projection does not cover the backside branch.");
        Vector3 half = Normalize(viewDirection + directionToLight);
        float v = Vector3.Dot(viewDirection, half), h = Vector3.Dot(normal, half);
        float oneMinusV = 1f - Saturate(v);
        float fifth = oneMinusV * oneMinusV;
        fifth *= fifth;
        fifth *= oneMinusV;
        Vector3 fresnel = reflectance + (Vector3.One - reflectance) * fifth;
        float u2 = roughness * roughness, u4 = u2 * u2, u8 = u4 * u4;
        float denominator = MathF.Max(h * h * (u8 - 1f) + 1f,
            BitConverter.UInt32BitsToSingle(DistributionEpsilonBits));
        float distribution = u8 / ((denominator * denominator) * 4f);
        float visibilityDenominator = v * (1f - u4 * 0.5f) + u4 * 0.5f;
        float visibility = 1f / (visibilityDenominator * visibilityDenominator);
        Check(fresnel);
        if (!float.IsFinite(distribution) || !float.IsFinite(visibility))
            throw new InvalidDataException("Deferred primary lobe has an exceptional denominator.");
        return new(fresnel, distribution, visibility, Saturate(nl));
    }

    /// <summary>PS1 source s=saturate((a+nv)^2+a-1), then the two direct AO factors.</summary>
    public static G1DeferredOcclusion EvaluateOcclusion(float albedoOcclusion, float normalDotView,
        float lightOcclusion, Vector2 sourceRates)
    {
        if (!float.IsFinite(albedoOcclusion) || !float.IsFinite(normalDotView) ||
            !float.IsFinite(lightOcclusion) || !Finite(sourceRates))
            throw new InvalidDataException("Deferred occlusion has nonfinite source inputs.");
        float sum = albedoOcclusion + Saturate(normalDotView);
        float s = Saturate(sum * sum + albedoOcclusion - 1f);
        float diffuse = 1f + lightOcclusion * sourceRates.X * (albedoOcclusion - 1f);
        float specular = 1f + lightOcclusion * sourceRates.Y * (s - 1f);
        if (!float.IsFinite(diffuse) || !float.IsFinite(specular))
            throw new InvalidDataException("Deferred occlusion factors overflowed.");
        return new(s, diffuse, specular);
    }

    /// <summary>PS1 2512, before intervening ramp/hatching operations.</summary>
    public static Vector3 CombineDiffuseAndAdditive(Vector3 diffuseAccumulator, Vector3 diffuseColor, Vector3 additiveColor)
    {
        Check(diffuseAccumulator); Check(diffuseColor); Check(additiveColor);
        Vector3 result = diffuseAccumulator * diffuseColor + additiveColor;
        Check(result);
        return result;
    }

    /// <summary>PS1 2586..2587, after caller supplies intervening operations and before fog. No RGB saturate.</summary>
    public static Vector3 AddRimAndSpecular(Vector3 colorAfterInterveningOperations, Vector3 rim, Vector3 specular)
    {
        Check(colorAfterInterveningOperations); Check(rim); Check(specular);
        Vector3 result = colorAfterInterveningOperations + rim;
        result += specular;
        Check(result);
        return result;
    }

    private static float Saturate(float value) => Math.Clamp(value, 0f, 1f);
    private static Vector3 Normalize(Vector3 value)
    {
        Check(value);
        float squared = Vector3.Dot(value, value);
        if (!float.IsFinite(squared) || squared <= 0f)
            throw new InvalidDataException("Deferred vector normalization has an exceptional length.");
        Vector3 result = value * (1f / MathF.Sqrt(squared));
        Check(result);
        return result;
    }
    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    private static void Check(Vector3 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            throw new InvalidDataException("Deferred vector has a nonfinite component.");
    }
    private static void Check(Vector4 value)
    {
        Check(new Vector3(value.X, value.Y, value.Z));
        if (!float.IsFinite(value.W)) throw new InvalidDataException("Deferred vector has a nonfinite component.");
    }
}
