using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record G1EffectRuntimeMissing(int SystemIndex, int ModuleIndex,
    int RecordFileOffset, string Operation, string Reason);

/// <summary>One dynamically prepared effect, using the context shared by the
/// real effect host. Normal resources supply declarations; live owners supply
/// transforms, clock and any direct-particle backend.</summary>
public sealed class G1EffectInstance : IG1EffectClockConsumer, IDisposable
{
    private sealed record SystemRuntime(G1EffectSystem Declaration,
        G1EffectSystemTimeline Timeline, G1EffectModuleRuntime[] Modules);
    private readonly G1EffectContext context;
    private readonly Func<G1EffectGenericRuntime, IG1EffectDirectParticleStorage?>? directStorageFactory;
    private readonly float?[] parameters = new float?[12];
    private readonly List<G1EffectRuntimeMissing> missing = [];
    private SystemRuntime[] systems = [];
    private float opacity = 1;
    private bool initialized, disposed, stopping;

    public G1EffectInstance(LoadedMapEffectResources resources, G1EffectContext context,
        Matrix4x4 worldTransform,
        Func<G1EffectGenericRuntime, IG1EffectDirectParticleStorage?>? directStorageFactory = null)
    {
        ArgumentNullException.ThrowIfNull(resources); ArgumentNullException.ThrowIfNull(context);
        Resources = resources; this.context = context; WorldTransform = worldTransform;
        this.directStorageFactory = directStorageFactory;
    }

    public LoadedMapEffectResources Resources { get; }
    public G1EffectWorldClock Clock { get; } = new();
    public Matrix4x4 WorldTransform { get; private set; }
    public IReadOnlyList<G1EffectRuntimeMissing> Missing => missing.AsReadOnly();
    public bool IsInitialized => initialized;
    public bool HasPendingDirectStorage => initialized && Generics.Any(generic =>
        generic.Declaration.UsesDirectBirth && !generic.HasDirectStorage);
    public bool IsFinished => initialized && missing.Count == 0 && !HasPendingDirectStorage && systems.All(system =>
        system.Timeline.IsFinished(system.Modules.Select(module => module.IsAlive).ToArray()));
    /// <summary>Actual mutable generics, for attaching an implemented direct
    /// storage backend. Attachments must outlive the instance's use of them.</summary>
    public IEnumerable<G1EffectGenericRuntime> Generics => systems.SelectMany(system => system.Modules)
        .SelectMany(module => module.Generics);

    public void SetTransform(Matrix4x4 transform) => WorldTransform = transform;

    public void SetParameter(int slot, float value)
    {
        if ((uint)slot >= 12) throw new ArgumentOutOfRangeException(nameof(slot));
        parameters[slot] = value;
        foreach (var module in systems.SelectMany(system => system.Modules)) module.SetParameter(slot, value);
    }

    /// <summary>Original packed-color override. Zero leaves the current user
    /// parameters unchanged, as does the map effect's original setter path.</summary>
    public void SetColourPacked(uint colour)
    {
        if (colour == 0) return;
        for (int lane = 0; lane < 4; lane++) SetParameter(8 + lane, (byte)(colour >> (8 * lane)) / 255f);
    }

    public void BeginStop()
    {
        stopping = true;
        foreach (var module in systems.SelectMany(system => system.Modules)) module.BeginStop();
    }

    public bool Advance(G1EffectClockInput input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return Clock.Advance(input, this);
    }

    public void RequestReset() => Clock.RequestReset();

    void IG1EffectClockConsumer.Reset()
    {
        ReleaseSystems(); initialized = false; missing.Clear(); opacity = 1; stopping = false;
    }

    void IG1EffectClockConsumer.Initialize()
    {
        var prepared = new List<SystemRuntime>();
        try
        {
            foreach (var declaration in Resources.Effect.Effect.Systems)
            {
                var modules = new List<G1EffectModuleRuntime>();
                try
                {
                    foreach (var module in declaration.Modules)
                    {
                        var runtime = new G1EffectModuleRuntime(module, declaration.Index, context, ReportMissing);
                        modules.Add(runtime);
                        if (directStorageFactory is not null)
                            foreach (var generic in runtime.Generics)
                                if (generic.Declaration.UsesDirectBirth && directStorageFactory(generic) is { } storage)
                                    generic.AttachDirectStorage(storage);
                        for (int slot = 0; slot < parameters.Length; slot++)
                            if (parameters[slot] is { } value) runtime.SetParameter(slot, value);
                        runtime.SetOpacity(opacity);
                        if (stopping) runtime.BeginStop();
                    }
                    prepared.Add(new(declaration, new(declaration), modules.ToArray()));
                }
                catch { foreach (var module in modules) module.Dispose(); throw; }
            }
            systems = prepared.ToArray(); initialized = true;
            foreach (var system in systems)
            {
                Matrix4x4 world = SystemWorld(system.Declaration);
                foreach (var module in system.Modules) module.Prewarm(world);
            }
        }
        catch { foreach (var system in prepared) foreach (var module in system.Modules) module.Dispose(); throw; }
    }

    void IG1EffectClockConsumer.Step(float seconds)
    {
        foreach (var system in systems)
        {
            // Completion is tested before this step's module updates. A live
            // emitter at time zero is not mistaken for an empty finished pool.
            if (system.Timeline.IsFinished(system.Modules.Select(module => module.IsAlive).ToArray()))
            { system.Timeline.Disable(); continue; }
            if (!system.Timeline.Advance(seconds)) continue;
            Matrix4x4 world = SystemWorld(system.Declaration);
            foreach (var module in system.Modules) module.Advance(seconds, world);
        }
    }

    void IG1EffectClockConsumer.SetOpacity(float value)
    {
        opacity = value;
        foreach (var module in systems.SelectMany(system => system.Modules)) module.SetOpacity(value);
    }

    public IEnumerable<G1EffectParticleRenderSource> EnumerateRenderSources()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (var system in systems)
            if (system.Timeline.Enabled && system.Timeline.Started)
                foreach (var module in system.Modules)
                    foreach (var source in module.EnumerateRenderSources(Resources)) yield return source;
    }

    public G1EffectDrawBatch BuildDrawBatch(G1EffectDrawCamera camera, int bucketCount,
        int queueCapacity, int dynamicCapacity32, int rowCapacity) =>
        G1EffectDrawWriter.Build(camera, EnumerateRenderSources(), bucketCount,
            queueCapacity, dynamicCapacity32, rowCapacity);

    private Matrix4x4 SystemWorld(G1EffectSystem declaration)
    {
        var b = declaration.Matrix.Span;
        if (b.Length != 64) throw new InvalidDataException("Effect system transform requires sixteen floats.");
        float F(int offset) => BinaryPrimitives.ReadSingleLittleEndian(declaration.Matrix.Span[offset..]);
        var local = new Matrix4x4(F(0),F(4),F(8),F(12),F(16),F(20),F(24),F(28),
            F(32),F(36),F(40),F(44),F(48),F(52),F(56),F(60));
        return G1EffectDistribution.Multiply(local, WorldTransform);
    }

    private void ReportMissing(G1EffectRuntimeMissing item) => missing.Add(item);
    private void ReleaseSystems()
    { foreach (var system in systems) foreach (var module in system.Modules) module.Dispose(); systems = []; }
    public void Dispose()
    { if (disposed) return; ReleaseSystems(); disposed = true; }
}
