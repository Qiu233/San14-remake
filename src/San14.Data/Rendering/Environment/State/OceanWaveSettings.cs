using System.Numerics;

namespace San14.Data;

/// <summary>Current horizontal wind and statistical water-wave controls.
/// Physical grid extent and random state belong to their own owners.</summary>
public sealed record OceanWaveSettings(Vector2 Wind, float Amplitude,
    float ChoppyFactor, float SmallWaveSuppressionLength, float Gravity);
