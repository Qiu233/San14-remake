using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

public readonly record struct OriginalArmyGraphicsMotionFrame(float Milliseconds936440,
    int UpdateInterval32C510);

/// <summary>Once-imported two-mode source tables. The active FPSManager mode is
/// supplied by the caller; this leaf has no wall clock or scheduling policy.</summary>
public sealed class OriginalArmyGraphicsMotionClockSource
{
    readonly float[] frameDeltas;
    readonly uint[] fps;

    public static OriginalArmyGraphicsMotionClockSource Load(OriginalGameRepository repository,
        string language) => LoadSourceDirectory(repository.SourceDirectory, language);

    /// <summary>Read the pinned PE tables once at source preparation. Resolve
    /// thereafter consumes only the retained scalar arrays.</summary>
    public static OriginalArmyGraphicsMotionClockSource LoadSourceDirectory(string sourceDirectory,
        string language) => OriginalDeferredSamplerSources.ReadArmyGraphicsMotionClockTables(sourceDirectory, language);

    public OriginalArmyGraphicsMotionClockSource(ReadOnlySpan<byte> frameDeltaSource,
        ReadOnlySpan<byte> fpsSource)
    {
        if (frameDeltaSource.Length != 8 || fpsSource.Length != 8)
            throw new InvalidDataException("Army motion requires the complete two-mode source delta and FPS tables.");
        frameDeltas = [ReadFloat(frameDeltaSource), ReadFloat(frameDeltaSource[4..])];
        fps = [BinaryPrimitives.ReadUInt32LittleEndian(fpsSource),
            BinaryPrimitives.ReadUInt32LittleEndian(fpsSource[4..])];
    }

    /// <summary>936440 preserves delta * 1000 as float. The slow motion caller
    /// truncates before multiplying interval; the fast caller truncates after
    /// multiplying its skip/mobility factor.</summary>
    public OriginalArmyGraphicsMotionFrame Resolve(uint actualFpsMode) =>
        new(Product(frameDeltas[actualFpsMode], 1000f), fps[actualFpsMode] == 30 ? 3 : 2);

    static float ReadFloat(ReadOnlySpan<byte> bytes) =>
        BitConverter.UInt32BitsToSingle(BinaryPrimitives.ReadUInt32LittleEndian(bytes));
    [MethodImpl(MethodImplOptions.NoInlining)] static float Product(float a, float b) => a * b;
}

public static partial class OriginalDeferredSamplerSources
{
    internal static OriginalArmyGraphicsMotionClockSource ReadArmyGraphicsMotionClockTables(
        string sourceDirectory, string language)
    {
        var profile = Profile(language);
        byte[] exe = File.ReadAllBytes(Path.Combine(sourceDirectory, $"SAN14CE_{language}.exe"));
        VerifyHash(exe, profile.ExeHash, "Army graphics clock executable");
        var image = new NativeImage(exe);
        int fpsRva = language switch
        {
            "TC" => 0x1A8B5C8,
            "SC" => 0x1A8C628,
            _ => throw new InvalidDataException("Army graphics clock requires the pinned SC or TC source.")
        };
        return new(image.Raw(fpsRva + 8, 8), image.Raw(fpsRva, 8));
    }
}
