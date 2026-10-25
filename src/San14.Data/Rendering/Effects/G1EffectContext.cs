using System.Numerics;

namespace San14.Data;

/// <summary>The actual effect host configuration. A zero particle budget
/// disables the shared arena; it is not an unlimited allocation request.</summary>
public readonly record struct G1EffectContextConfiguration(int ParticleArenaBytes, bool InitializeNoiseTables)
{
    public static G1EffectContextConfiguration ApplicationDefault => new(32 * 1024 * 1024, true);
}

/// <summary>The shared effect host's CPU context. Keep one owner across its
/// source-ordered effect instances, rather than constructing a random stream
/// for each effect. Noise construction consumes the same owned stream.</summary>
public sealed partial class G1EffectContext : IDisposable
{
    private readonly float[] scalarNoise;
    private readonly Vector4[] vectorNoise;
    private bool disposed;

    private G1EffectContext(G1EffectContextConfiguration configuration, G1EffectArena arena)
    {
        Configuration = configuration;
        Arena = arena;
        // The source's initialized data value is one. Its first noise setup
        // explicitly resets this to zero separately for the two tables.
        Random = new(1);
        scalarNoise = configuration.InitializeNoiseTables ? new float[256] : [];
        vectorNoise = configuration.InitializeNoiseTables ? new Vector4[256] : [];
        if (configuration.InitializeNoiseTables)
        {
            Random = new(0);
            for (int index = 0; index < scalarNoise.Length; index++) scalarNoise[index] = Random.NextUnit();
            Random = new(0);
            InitializeVectorNoise();
        }
    }

    public G1EffectContextConfiguration Configuration { get; }
    public G1EffectRandom Random { get; }
    public G1EffectArena Arena { get; }
    public ReadOnlyMemory<float> ScalarNoise => scalarNoise;
    public ReadOnlyMemory<Vector4> VectorNoise => vectorNoise;
    public ReadOnlyMemory<byte> Permutation => NoisePermutation;

    public static bool TryCreate(G1EffectContextConfiguration configuration, out G1EffectContext? context)
    {
        context = null;
        if (!G1EffectArena.TryCreate(configuration.ParticleArenaBytes, out var arena)) return false;
        try { context = new(configuration, arena!); return true; }
        catch (OutOfMemoryException) { arena!.Dispose(); return false; }
    }

    public static bool TryCreate(out G1EffectContext? context) =>
        TryCreate(G1EffectContextConfiguration.ApplicationDefault, out context);

    private void InitializeVectorNoise()
    {
        float sumX = 0, sumY = 0, sumZ = 0, sumW = 0;
        for (int index = 0; index < vectorNoise.Length; index++)
        {
            float radius = G1EffectShapeMath.CubeRoot(Random.NextUnit());
            var angle = G1EffectShapeMath.SinCos(Random.NextUnit() * 6.2831855f);
            float height = Random.NextSigned();
            float radial = MathF.Sqrt(1 - height * height);
            float x = radial * angle.Sine * radius;
            float y = radial * angle.Cosine * radius;
            float z = height * radius, w = radius * 0;
            vectorNoise[index] = new(x, y, z, w);
            // The original adds each generated component twice before its
            // correction pass. Preserve this rather than idealizing the mean.
            sumX = NativeFloat.Add(NativeFloat.Add(sumX, x), x);
            sumY = NativeFloat.Add(NativeFloat.Add(sumY, y), y);
            sumZ = NativeFloat.Add(NativeFloat.Add(sumZ, z), z);
            sumW = NativeFloat.Add(NativeFloat.Add(sumW, w), w);
        }
        float meanX = sumX * 0.00390625f, meanY = sumY * 0.00390625f;
        float meanZ = sumZ * 0.00390625f, meanW = sumW * 0.00390625f;
        for (int index = 0; index < vectorNoise.Length; index++)
        {
            var value = vectorNoise[index];
            vectorNoise[index] = new(value.X - meanX, value.Y - meanY, value.Z - meanZ, value.W - meanW);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        Arena.Dispose();
        disposed = true;
    }
}
