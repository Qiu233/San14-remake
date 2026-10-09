using System.Runtime.CompilerServices;

namespace San14.Data;

public readonly record struct OriginalArmyGraphicsMotionFrame(float Milliseconds936440,
    int UpdateInterval32C510);

/// <summary>Two-mode animation cadence. The active FPSManager mode is
/// supplied by the caller; this leaf has no wall clock or scheduling policy.</summary>
public sealed class OriginalArmyGraphicsMotionClockSource
{
    readonly float[] frameDeltas;
    readonly uint[] fps;

    public static OriginalArmyGraphicsMotionClockSource Load(OriginalGameRepository repository,
        string language) => LoadSourceDirectory(repository.SourceDirectory, language);

    /// <summary>Uses the project's cadence rules; no source directory is read.</summary>
    public static OriginalArmyGraphicsMotionClockSource LoadSourceDirectory(string sourceDirectory,
        string language)
    {
        RenderRuleIdentity.For("army-motion-clock", language);
        return new(ArmyGraphicsRules.FpsModeDeltas.ToArray(), ArmyGraphicsRules.FpsModeRates.ToArray());
    }

    public OriginalArmyGraphicsMotionClockSource(ReadOnlySpan<float> frameDeltaSource,
        ReadOnlySpan<uint> fpsSource)
    {
        if (frameDeltaSource.Length != 2 || fpsSource.Length != 2)
            throw new InvalidDataException("Army motion requires the complete two-mode source delta and FPS tables.");
        frameDeltas = frameDeltaSource.ToArray();
        fps = fpsSource.ToArray();
    }

    /// <summary>936440 preserves delta * 1000 as float. The slow motion caller
    /// truncates before multiplying interval; the fast caller truncates after
    /// multiplying its skip/mobility factor.</summary>
    public OriginalArmyGraphicsMotionFrame Resolve(uint actualFpsMode) =>
        new(Product(frameDeltas[actualFpsMode], 1000f), fps[actualFpsMode] == 30 ? 3 : 2);

    [MethodImpl(MethodImplOptions.NoInlining)] static float Product(float a, float b) => a * b;
}
