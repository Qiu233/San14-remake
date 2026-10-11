using System.Numerics;

namespace San14.Data;

/// <summary>Construction settings for the persistent water grid. Dimensions
/// describe wave samples, independently of the render target dimensions.</summary>
public sealed record OceanGridSettings(uint Columns, uint Rows, Vector2 Size,
    float Cycle, uint DeclaredLod, float MaximumDistance);
