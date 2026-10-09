namespace San14.Data;

/// <summary>Project rules for graphics allocation, cadence and initial material
/// parameters. Model definitions, clips and materials still come from resources.</summary>
public static class ArmyGraphicsRules
{
    public static IReadOnlyList<OriginalArmyGraphicsPoolBank> PoolBanks { get; } =
        Array.AsReadOnly(new OriginalArmyGraphicsPoolBank[]
        {
            new(0, 2000, 1, 0),
            new(1, 1000, 1, 0),
            new(2, 200, 1, 0),
            new(3, 200, 1, 0),
            new(4, 50, 1, 0),
            new(5, 50, 1, 0),
            new(6, 50, 1, 0),
            new(7, 50, 1, 0),
            new(8, 50, 1, 0),
            new(9, 50, 1, 0),
            new(10, 50, 1, 0)
        });

    public static IReadOnlyList<uint> FpsModeRates { get; } = Array.AsReadOnly(new uint[] { 30, 60 });

    // The 60 FPS delta is one float step below a rounded 1/60. Preserve the
    // cadence arithmetic independently of the wall clock and frame scheduler.
    public static IReadOnlyList<float> FpsModeDeltas { get; } =
        Array.AsReadOnly(new[] { 0.03333333507180214f, 0.01666666567325592f });

    public const float RegularMaterialSpecularW = 2f;
    public const float RenderTargetMaterialSpecularW = 1f;

    public static bool UsesProjectedShadow(int category) => category switch
    {
        >= 0 and <= 10 => false,
        >= 11 and <= 58 => true,
        _ => throw new NotSupportedException($"No graphics rule is defined for category {category}.")
    };
}
