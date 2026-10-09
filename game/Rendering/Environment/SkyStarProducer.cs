using San14.Data;

public sealed record SkyStarFacePacket(ReadOnlyMemory<byte> VertexConstants,
    ReadOnlyMemory<byte> GeometryConstants, ReadOnlyMemory<byte> PixelConstants);
public sealed record SkyStarNativeGate(bool ExplicitPassReady, float Parameter15C0, float Parameter15C4)
{
    public bool AllowsDraw
    {
        get
        {
            if (!float.IsFinite(Parameter15C0) || !float.IsFinite(Parameter15C4))
                throw new InvalidDataException("Star native draw gate is outside the supported finite domain.");
            return ExplicitPassReady && Parameter15C0 != 0 && Parameter15C4 >= .5f;
        }
    }
}

/// <summary>Six cached original Star draws, borrowed WORK/source texture and
/// explicit native gate. Missing geometry or zero count produces no scene
/// stage; a closed native gate submits no GPU draw. Dispose before SkyScene.</summary>
public sealed class SkyStarProducer : IAsyncDisposable
{
    public SkyStarProgram Program { get; }
    public CubeRenderTextureDevice Work { get; }
    public bool HasGeometry { get; }
    public bool UsesAnalyticPixel { get; }
    public SkyStarNativeGate Gate { get; private set; }
    private readonly IReadOnlyList<SkyStarDraw> _faces;
    private readonly IReadOnlyList<IAsyncDisposable> _owned;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetimeGate = new();
    private bool _retiring;
    private Task? _disposal;
    private SkyStarProducer(SkyStarProgram program, CubeRenderTextureDevice work,
        bool hasGeometry, SkyStarNativeGate gate, List<SkyStarDraw> faces, List<IAsyncDisposable> owned, bool analytic)
        => (Program, Work, HasGeometry, Gate, _faces, _owned, UsesAnalyticPixel) = (program, work, hasGeometry, gate, faces.AsReadOnly(), owned.AsReadOnly(), analytic);

    public static async Task<SkyStarProducer> BuildAsync(LoadedShaderContainer source, int explicitCombination,
        Vkd3dShaderCompiler compiler, CubeRenderTextureDevice work, SkyStarGeometry? geometry,
        IReadOnlyList<SkyStarFacePacket> explicitSixFaces, Sky2DeviceInput? explicitStarTexture,
        uint explicitColorWriteMask, SkyStarRasterState explicitRaster, SkyStarNativeGate explicitGate)
    {
        ArgumentNullException.ThrowIfNull(work); ArgumentNullException.ThrowIfNull(explicitGate);
        _ = explicitGate.AllowsDraw; var owned = new List<IAsyncDisposable>();
        try
        {
            var program = await SkyStarProgram.BuildAsync(source, explicitCombination, compiler); owned.Add(program);
            var faces = new List<SkyStarDraw>(); bool hasGeometry = geometry is { PointCount: > 0 };
            if (hasGeometry)
            {
                var packets = Snapshot(program, explicitSixFaces);
                if (explicitStarTexture is null) foreach (var packet in packets) SkyStarPixelAdapter.ValidateAnalyticPacket(packet.PixelConstants.Span);
                for (uint face = 0; face < 6; face++)
                {
                    var p = packets[(int)face]; var draw = await SkyStarDraw.BuildAsync(program, work,
                        SkySceneSchedule.SurfaceId(work.Source.LevelNum, face, 0), geometry!,
                        p.VertexConstants, p.GeometryConstants, p.PixelConstants, explicitStarTexture, explicitColorWriteMask, explicitRaster);
                    faces.Add(draw); owned.Add(draw);
                }
            }
            return new(program, work, hasGeometry, explicitGate, faces, owned, explicitStarTexture is null);
        }
        catch { for (int i = owned.Count - 1; i >= 0; i--) await owned[i].DisposeAsync(); throw; }
    }
    public async Task UpdateFrameAsync(IReadOnlyList<SkyStarFacePacket> explicitSixFaces, SkyStarNativeGate explicitGate)
    {
        ArgumentNullException.ThrowIfNull(explicitGate); _ = explicitGate.AllowsDraw;
        var packets = HasGeometry ? Snapshot(Program, explicitSixFaces) : Array.Empty<SkyStarFacePacket>();
        if (UsesAnalyticPixel) foreach (var packet in packets) SkyStarPixelAdapter.ValidateAnalyticPacket(packet.PixelConstants.Span);
        await _gate.WaitAsync();
        try { ObjectDisposedException.ThrowIf(_retiring, this); for (int i = 0; i < packets.Length; i++)
                await _faces[i].UpdateConstantsAsync(packets[i].VertexConstants, packets[i].GeometryConstants, packets[i].PixelConstants);
            Gate = explicitGate; }
        finally { _gate.Release(); }
    }
    public SkySceneFaceProducer? CreateSceneStage()
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        return HasGeometry ? new(Program.Source.Source.ResourceUri + "#native-pass=0", SubmitAsync) : null;
    }
    private async Task SubmitAsync(SkySceneFaceContext context)
    {
        if (!ReferenceEquals(context.PrimaryWork, Work) || context.Face >= 6 ||
            context.Target != Work.Surfaces[(int)SkySceneSchedule.SurfaceId(Work.Source.LevelNum, context.Face, 0)])
            throw new InvalidDataException("Star scene stage belongs to another WORK resource or surface.");
        await _gate.WaitAsync();
        try { ObjectDisposedException.ThrowIf(_retiring, this); if (Gate.AllowsDraw) await _faces[(int)context.Face].DrawAsync(); }
        finally { _gate.Release(); }
    }
    private static SkyStarFacePacket[] Snapshot(SkyStarProgram program, IReadOnlyList<SkyStarFacePacket> packets)
    {
        ArgumentNullException.ThrowIfNull(packets);
        if (packets.Count != 6) throw new InvalidDataException("Star WORK generation requires six explicit face packets.");
        return packets.Select(p => { var v = SkyStarConstants.Read(program.VertexDefinitions, program.GeometryDefinitions,
                program.PixelBindings.SourceDefinitions, p.VertexConstants.Span, p.GeometryConstants.Span, p.PixelConstants.Span);
            return new SkyStarFacePacket(v.VertexBytes, v.GeometryBytes, v.PixelBytes); }).ToArray();
    }
    public ValueTask DisposeAsync() { lock (_lifetimeGate) { _retiring = true; return new(_disposal ??= DisposeCoreAsync()); } }
    private async Task DisposeCoreAsync()
    { await _gate.WaitAsync(); try { for (int i = _owned.Count - 1; i >= 0; i--) await _owned[i].DisposeAsync(); } finally { _gate.Release(); } }
}
