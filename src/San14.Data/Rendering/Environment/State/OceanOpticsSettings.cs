using System.Numerics;

namespace San14.Data;

/// <summary>Current PB2 base material and refraction controls. Authored albedo
/// alpha is retained here; the PB2 shader receives roughness in its fourth lane.
/// Animation, bubble layers and camera transforms have separate owners.</summary>
public sealed record OceanOpticsSettings(float RefractionIndex, float RefractionTransparency,
    Vector4 Albedo, Vector3 Reflectance, float Roughness, float DiffuseTransparency);
