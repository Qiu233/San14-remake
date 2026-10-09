using San14.Data;

public sealed record SkyCloudColorFacePacket(ReadOnlyMemory<byte> VertexConstants,
    ReadOnlyMemory<byte> PixelConstants, uint MaximumRaySamples);

/// <summary>Owns native VCLOUD pass0 and six cached cloud-over-WORK draws.
/// WORK and textures remain borrowed; dispose this before the owning SkyScene.
/// A missing source noise group produces no stage, following D77B10 skip.</summary>
public sealed class SkyCloudColorProducer : IAsyncDisposable
{
    public SkyCloudProgram Program { get; }
    public CubeRenderTextureDevice Work { get; }
    public bool NoiseGroupPresent { get; }
    private readonly IReadOnlyList<SkyCloudColorDraw> _faces;
    private readonly IReadOnlyList<IAsyncDisposable> _owned;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetimeGate = new();
    private bool _retiring;
    private Task? _disposal;

    private SkyCloudColorProducer(SkyCloudProgram program,
        CubeRenderTextureDevice work, bool present,
        IReadOnlyList<SkyCloudColorDraw> faces, List<IAsyncDisposable> owned)
        => (Program, Work, NoiseGroupPresent, _faces, _owned) = (program, work, present, faces, owned.AsReadOnly());

    public static async Task<SkyCloudColorProducer> BuildAsync(LoadedShaderContainer source,
        int explicitCombination, Vkd3dShaderCompiler compiler, CubeRenderTextureDevice work,
        IReadOnlyList<Sky2DeviceInput>? explicitNoiseAndTables,
        IReadOnlyList<SkyCloudColorFacePacket> explicitSixFaces, uint explicitColorWriteMask)
    {
        ArgumentNullException.ThrowIfNull(work); ArgumentNullException.ThrowIfNull(explicitSixFaces);
        var owned = new List<IAsyncDisposable>();
        try
        {
            var program = await SkyCloudProgram.BuildAsync(source, explicitCombination,
                compiler, SkyCloudOperation.Color); owned.Add(program);
            var faces = new List<SkyCloudColorDraw>();
            if (explicitNoiseAndTables is not null)
            {
                var packets = Snapshot(program, explicitSixFaces);
                for (uint face = 0; face < 6; face++)
                {
                    var p = packets[(int)face];
                    var draw = await SkyCloudColorDraw.BuildAsync(program, work,
                        SkySceneSchedule.SurfaceId(work.Source.LevelNum, face, 0),
                        p.VertexConstants, p.PixelConstants, p.MaximumRaySamples,
                        explicitNoiseAndTables, explicitColorWriteMask);
                    faces.Add(draw); owned.Add(draw);
                }
            }
            return new(program, work, explicitNoiseAndTables is not null, faces.AsReadOnly(), owned);
        }
        catch { for (int i = owned.Count - 1; i >= 0; i--) await owned[i].DisposeAsync(); throw; }
    }

    public async Task UpdateFrameAsync(IReadOnlyList<SkyCloudColorFacePacket> explicitSixFaces)
    {
        var packets = NoiseGroupPresent ? Snapshot(Program, explicitSixFaces) : Array.Empty<SkyCloudColorFacePacket>();
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

    public SkySceneFaceProducer? CreateSceneStage()
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        return NoiseGroupPresent ? new(Program.Source.Source.ResourceUri + "#native-pass=0", SubmitFaceAsync) : null;
    }

    private async Task SubmitFaceAsync(SkySceneFaceContext context)
    {
        if (!ReferenceEquals(context.PrimaryWork, Work) || context.Face >= 6 ||
            context.Target != Work.Surfaces[(int)SkySceneSchedule.SurfaceId(Work.Source.LevelNum, context.Face, 0)])
            throw new InvalidDataException("Cloud scene stage belongs to another WORK resource or surface.");
        await _gate.WaitAsync();
        try { ObjectDisposedException.ThrowIf(_retiring, this); await _faces[(int)context.Face].DrawAsync(); }
        finally { _gate.Release(); }
    }

    private static SkyCloudColorFacePacket[] Snapshot(SkyCloudProgram program,
        IReadOnlyList<SkyCloudColorFacePacket> explicitSixFaces)
    {
        ArgumentNullException.ThrowIfNull(explicitSixFaces);
        if (explicitSixFaces.Count != 6) throw new InvalidDataException("Cloud WORK generation needs six explicit face packets.");
        return explicitSixFaces.Select(p =>
        {
            var validated = SkyCloudConstants.ReadColor(program.VertexBindings.SourceDefinitions,
                program.PixelBindings.SourceDefinitions, p.VertexConstants.Span, p.PixelConstants.Span, p.MaximumRaySamples);
            return new SkyCloudColorFacePacket(validated.VertexBytes, validated.PixelBytes, validated.MaximumRaySamples);
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
