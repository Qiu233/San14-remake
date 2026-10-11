using San14.Data;

public sealed record SkyPatternUpdate(uint Index, bool SkippedMissingNoiseGroup,
    uint Draws, long CompletedUpdates);

/// <summary>Owns the original four R8 targets and selected VCLOUD pass5.
/// Caller selection/packets drive updates; a null noise group preserves the
/// original D77B10 skip, leaving outputs unavailable instead of making a
/// fabricated white/black fallback. Pattern output is a cloud mask, not sky RGB.</summary>
public sealed class SkyPatternProducer : IAsyncDisposable
{
    public SkyCloudProgram Program { get; }
    public IReadOnlyList<SkyPatternMapDevice> Targets { get; }
    public bool NoiseGroupPresent { get; }
    private readonly IReadOnlyList<SkyCloudPatternDraw> _draws;
    private readonly IReadOnlyList<IAsyncDisposable> _owned;
    private readonly bool[] _ready = new bool[4];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetimeGate = new();
    private bool _retiring;
    private Task? _disposal;
    private long _completedUpdates;

    private SkyPatternProducer(SkyCloudProgram program,
        IReadOnlyList<SkyPatternMapDevice> targets,
        IReadOnlyList<SkyCloudPatternDraw> draws, bool present, List<IAsyncDisposable> owned)
        => (Program, Targets, _draws, NoiseGroupPresent, _owned) = (program, targets, draws, present, owned.AsReadOnly());

    public static async Task<SkyPatternProducer> BuildAsync(LoadedSkyAmbientResources ambient,
        LoadedShaderContainer cloudSource, int explicitCombination, Vkd3dShaderCompiler compiler,
        IReadOnlyList<Sky2DeviceInput>? explicitNoiseGroup,
        ReadOnlyMemory<byte> vertexConstants, ReadOnlyMemory<byte> pixelConstants,
        uint maximumRaySamples, bool allowReadback = false)
    {
        byte[] vertex = vertexConstants.ToArray(), pixel = pixelConstants.ToArray();
        return await BuildCoreAsync(ambient,
            () => SkyCloudProgram.BuildAsync(cloudSource, explicitCombination, compiler), explicitNoiseGroup,
            layout => layout.ReadMaterial(vertex, pixel), maximumRaySamples, allowReadback);
    }

    public static Task<SkyPatternProducer> BuildAsync(LoadedSkyAmbientResources ambient,
        SkyCloudPatternProgramSelection selection, Vkd3dShaderCompiler compiler,
        IReadOnlyList<Sky2DeviceInput>? explicitNoiseGroup, SkyCloudPatternMaterial material,
        uint maximumRaySamples, bool allowReadback = false)
        => BuildCoreAsync(ambient, () => SkyCloudProgram.BuildAsync(selection, compiler), explicitNoiseGroup,
            _ => material, maximumRaySamples, allowReadback);

    private static async Task<SkyPatternProducer> BuildCoreAsync(LoadedSkyAmbientResources ambient,
        Func<Task<SkyCloudProgram>> createProgram, IReadOnlyList<Sky2DeviceInput>? explicitNoiseGroup,
        Func<SkyCloudPatternMaterialLayout, SkyCloudPatternMaterial> prepareMaterial,
        uint maximumRaySamples, bool allowReadback)
    {
        ArgumentNullException.ThrowIfNull(ambient);
        if (ambient.PatternTargets.Count != 4) throw new InvalidDataException("Cloud producer requires four ordered source PatternMap targets.");
        var owned = new List<IAsyncDisposable>();
        try
        {
            var program = await createProgram(); owned.Add(program);
            var targets = new List<SkyPatternMapDevice>();
            foreach (var source in ambient.PatternTargets)
            { var target = await SkyPatternMapDevice.BuildAsync(source, allowReadback); targets.Add(target); owned.Add(target); }
            var draws = new List<SkyCloudPatternDraw>();
            if (explicitNoiseGroup is not null)
            {
                var material = prepareMaterial(program.PatternLayout);
                foreach (var target in targets)
                {
                    var draw = await SkyCloudPatternDraw.BuildAsync(program, target, material,
                        maximumRaySamples, explicitNoiseGroup);
                    draws.Add(draw); owned.Add(draw);
                }
            }
            return new(program, targets.AsReadOnly(), draws.AsReadOnly(), explicitNoiseGroup is not null, owned);
        }
        catch { for (int i = owned.Count - 1; i >= 0; i--) await owned[i].DisposeAsync(); throw; }
    }

    public Task<SkyPatternUpdate> RenderAsync(uint explicitPatternIndex,
        ReadOnlyMemory<byte> vertexConstants, ReadOnlyMemory<byte> pixelConstants, uint maximumRaySamples)
    {
        byte[] vertex = vertexConstants.ToArray(), pixel = pixelConstants.ToArray();
        return RenderCoreAsync(explicitPatternIndex, () => Program.PatternLayout.ReadMaterial(vertex, pixel), maximumRaySamples);
    }

    public Task<SkyPatternUpdate> RenderAsync(uint explicitPatternIndex,
        SkyCloudPatternMaterial material, uint maximumRaySamples)
        => RenderCoreAsync(explicitPatternIndex, () => material, maximumRaySamples);

    private async Task<SkyPatternUpdate> RenderCoreAsync(uint explicitPatternIndex,
        Func<SkyCloudPatternMaterial> prepareMaterial, uint maximumRaySamples)
    {
        if (explicitPatternIndex >= 4) throw new ArgumentOutOfRangeException(nameof(explicitPatternIndex));
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            if (!NoiseGroupPresent) return new(explicitPatternIndex, true, 0, _completedUpdates);
            var draw = _draws[(int)explicitPatternIndex];
            await draw.UpdateMaterialAsync(prepareMaterial(), maximumRaySamples);
            _ready[explicitPatternIndex] = false;
            await draw.DrawAsync(); _ready[explicitPatternIndex] = true; _completedUpdates++;
            return new(explicitPatternIndex, false, 1, _completedUpdates);
        }
        finally { _gate.Release(); }
    }

    public SkyPatternMapDevice GetCompletedPattern(uint index)
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        if (index >= 4 || !_ready[index]) throw new InvalidOperationException("Cloud PatternMap has no completed update.");
        return Targets[(int)index];
    }
    public ValueTask DisposeAsync()
    { lock (_lifetimeGate) { _retiring = true; return new(_disposal ??= DisposeCoreAsync()); } }
    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync();
        try { for (int i = _owned.Count - 1; i >= 0; i--) await _owned[i].DisposeAsync(); }
        finally { _gate.Release(); }
    }
}
