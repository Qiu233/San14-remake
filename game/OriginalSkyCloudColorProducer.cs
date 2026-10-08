using San14.Data;

public sealed record OriginalSkyCloudColorFacePacket(ReadOnlyMemory<byte> VertexConstants,
    ReadOnlyMemory<byte> PixelConstants, uint MaximumRaySamples);

/// <summary>Owns native VCLOUD pass0 and six cached cloud-over-WORK draws.
/// WORK and textures remain borrowed; dispose this before the owning SkyScene.
/// A missing source noise group produces no stage, following D77B10 skip.</summary>
public sealed class OriginalSkyCloudColorProducer : IAsyncDisposable
{
    public OriginalSkyCloudProgram Program { get; }
    public OriginalCubeRenderTextureDevice Work { get; }
    public bool NoiseGroupPresent { get; }
    private readonly IReadOnlyList<OriginalSkyCloudColorDraw> _faces;
    private readonly IReadOnlyList<IAsyncDisposable> _owned;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetimeGate = new();
    private bool _retiring;
    private Task? _disposal;

    private OriginalSkyCloudColorProducer(OriginalSkyCloudProgram program,
        OriginalCubeRenderTextureDevice work, bool present,
        IReadOnlyList<OriginalSkyCloudColorDraw> faces, List<IAsyncDisposable> owned)
        => (Program, Work, NoiseGroupPresent, _faces, _owned) = (program, work, present, faces, owned.AsReadOnly());

    public static async Task<OriginalSkyCloudColorProducer> BuildAsync(LoadedShaderContainer source,
        int explicitCombination, Vkd3dShaderCompiler compiler, OriginalCubeRenderTextureDevice work,
        IReadOnlyList<OriginalSky2DeviceInput>? explicitNoiseAndTables,
        IReadOnlyList<OriginalSkyCloudColorFacePacket> explicitSixFaces, uint explicitColorWriteMask)
    {
        ArgumentNullException.ThrowIfNull(work); ArgumentNullException.ThrowIfNull(explicitSixFaces);
        var owned = new List<IAsyncDisposable>();
        try
        {
            var program = await OriginalSkyCloudProgram.BuildAsync(source, explicitCombination,
                compiler, OriginalSkyCloudOperation.Color); owned.Add(program);
            var faces = new List<OriginalSkyCloudColorDraw>();
            if (explicitNoiseAndTables is not null)
            {
                var packets = Snapshot(program, explicitSixFaces);
                for (uint face = 0; face < 6; face++)
                {
                    var p = packets[(int)face];
                    var draw = await OriginalSkyCloudColorDraw.BuildAsync(program, work,
                        OriginalSkySceneSchedule.SurfaceId(work.Source.LevelNum, face, 0),
                        p.VertexConstants, p.PixelConstants, p.MaximumRaySamples,
                        explicitNoiseAndTables, explicitColorWriteMask);
                    faces.Add(draw); owned.Add(draw);
                }
            }
            return new(program, work, explicitNoiseAndTables is not null, faces.AsReadOnly(), owned);
        }
        catch { for (int i = owned.Count - 1; i >= 0; i--) await owned[i].DisposeAsync(); throw; }
    }

    public async Task UpdateFrameAsync(IReadOnlyList<OriginalSkyCloudColorFacePacket> explicitSixFaces)
    {
        var packets = NoiseGroupPresent ? Snapshot(Program, explicitSixFaces) : Array.Empty<OriginalSkyCloudColorFacePacket>();
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            for (int face = 0; face < packets.Length; face++)
                await _faces[face].UpdateConstantsAsync(packets[face].VertexConstants,
                    packets[face].PixelConstants, packets[face].MaximumRaySamples);
        }
        finally { _gate.Release(); }
    }

    public OriginalSkySceneFaceProducer? CreateSceneStage()
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        return NoiseGroupPresent ? new(Program.Source.Source.ResourceUri + "#native-pass=0", SubmitFaceAsync) : null;
    }

    private async Task SubmitFaceAsync(OriginalSkySceneFaceContext context)
    {
        if (!ReferenceEquals(context.PrimaryWork, Work) || context.Face >= 6 ||
            context.Target != Work.Surfaces[(int)OriginalSkySceneSchedule.SurfaceId(Work.Source.LevelNum, context.Face, 0)])
            throw new InvalidDataException("Cloud scene stage belongs to another WORK resource or surface.");
        await _gate.WaitAsync();
        try { ObjectDisposedException.ThrowIf(_retiring, this); await _faces[(int)context.Face].DrawAsync(); }
        finally { _gate.Release(); }
    }

    private static OriginalSkyCloudColorFacePacket[] Snapshot(OriginalSkyCloudProgram program,
        IReadOnlyList<OriginalSkyCloudColorFacePacket> explicitSixFaces)
    {
        ArgumentNullException.ThrowIfNull(explicitSixFaces);
        if (explicitSixFaces.Count != 6) throw new InvalidDataException("Cloud WORK generation needs six explicit face packets.");
        return explicitSixFaces.Select(p =>
        {
            var validated = OriginalSkyCloudConstants.ReadColor(program.VertexBindings.OriginalDefinitions,
                program.PixelBindings.OriginalDefinitions, p.VertexConstants.Span, p.PixelConstants.Span, p.MaximumRaySamples);
            return new OriginalSkyCloudColorFacePacket(validated.VertexBytes, validated.PixelBytes, validated.MaximumRaySamples);
        }).ToArray();
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
