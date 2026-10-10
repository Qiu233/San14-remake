using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public readonly record struct HdrHemisphereLighting(Vector4 UpperDiffuse, Vector4 LowerDiffuse,
    Vector4 UpperSpecular, Vector4 LowerSpecular)
{
    internal HdrHemisphereLighting Scale(float factor) => new(
        HdrLightModifier.ScaleRgb(UpperDiffuse, factor), HdrLightModifier.ScaleRgb(LowerDiffuse, factor),
        HdrLightModifier.ScaleRgb(UpperSpecular, factor), HdrLightModifier.ScaleRgb(LowerSpecular, factor));
}

public sealed record HdrPreparedLighting(DeferredLightingInputs Lighting, HdrHemisphereLighting Hemisphere);

public readonly record struct HdrShadowModes(byte Receive, byte Local);

/// <summary>Decoded current light values. No resource bytes or encoded offsets
/// participate in selecting a modifier or preparing directional lighting.</summary>
public sealed class HdrLightState
{
    internal HdrLightState(DeferredDirectionalLight[] directional, Vector4 ambient,
        HdrLightModifier modifier, Vector4 diffuse, float diffuseIntensity,
        Vector4 specular, float specularIntensity, HdrHemisphereLighting hemisphere, HdrShadowModes shadowModes)
    {
        Directional = Array.AsReadOnly(directional); Ambient = ambient; Modifier = modifier;
        Diffuse = diffuse; DiffuseIntensity = diffuseIntensity;
        Specular = specular; SpecularIntensity = specularIntensity;
        Hemisphere = hemisphere; ShadowModes = shadowModes;
    }

    public IReadOnlyList<DeferredDirectionalLight> Directional { get; }
    public Vector4 Ambient { get; }
    public HdrLightModifier Modifier { get; }
    public Vector4 Diffuse { get; }
    public float DiffuseIntensity { get; }
    public Vector4 Specular { get; }
    public float SpecularIntensity { get; }
    public HdrHemisphereLighting Hemisphere { get; }
    public HdrShadowModes ShadowModes { get; }

    public HdrPreparedLighting PrepareLighting(HdrLightState inner, bool suppressAmbient)
    {
        var modifier = inner.Modifier.Present ? inner.Modifier : Modifier;
        bool apply = modifier.RequiresAdjustment(suppressAmbient) && modifier.Present;
        var lights = new DeferredDirectionalLight[Directional.Count];
        for (int i = 0; i < lights.Length; i++)
        {
            var light = Directional[i];
            lights[i] = apply ? light with
                { Color = HdrLightModifier.ScaleRgb(light.Color, modifier.DirectionalFactor(i)) } : light;
        }
        Vector4 ambient = Ambient;
        if (apply && !suppressAmbient) ambient = HdrLightModifier.ScaleRgb(ambient, modifier.AmbientFactor);
        var hemisphere = apply ? Hemisphere.Scale(modifier.HemisphereFactor) : Hemisphere;
        if (suppressAmbient) ambient = new(0, 0, 0, ambient.W);

        int count = lights.Length;
        while (count > 1)
        {
            var color = lights[count - 1].Color;
            if (color.X > 0 || color.Y > 0 || color.Z > 0) break;
            count--;
        }
        return new(new(Array.AsReadOnly(lights[..count]), ambient), hemisphere);
    }

    public DeferredAmbientInputs PrepareAmbient(float mapOcclusionRate) => new(mapOcclusionRate,
        Scale(Diffuse, DiffuseIntensity), Scale(Specular, SpecularIntensity));

    private static Vector4 Scale(Vector4 value, float intensity) => new(
        Multiply(intensity, value.X), Multiply(intensity, value.Y), Multiply(intensity, value.Z), value.W);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float Multiply(float first, float second) => first * second;
}
