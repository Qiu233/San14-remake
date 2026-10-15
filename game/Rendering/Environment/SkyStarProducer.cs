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
    private readonly NativeMapBorrowerRetirementPackage retirement;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetimeGate = new();
    private bool _retiring;
    private bool needsCurrentFrame;
    private Task? _disposal;
    private SkyStarProducer(SkyStarProgram program, CubeRenderTextureDevice work,
        bool hasGeometry, SkyStarNativeGate gate, List<SkyStarDraw> faces, List<IAsyncDisposable> owned, bool analytic)
    {
        (Program, Work, HasGeometry, Gate, _faces, UsesAnalyticPixel) =
            (program, work, hasGeometry, gate, faces.AsReadOnly(), analytic);
        retirement = Retirement(owned);
    }

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
                var deviceGeometry = await SkyStarGeometryDevice.BuildAsync(geometry!);
                owned.Add(deviceGeometry);
                for (uint face = 0; face < 6; face++)
                {
                    var p = packets[(int)face]; var draw = await SkyStarDraw.BuildAsync(program, work,
                        SkySceneSchedule.SurfaceId(work.Source.LevelNum, face, 0), deviceGeometry,
                        p.VertexConstants, p.GeometryConstants, p.PixelConstants, explicitStarTexture, explicitColorWriteMask, explicitRaster);
                    faces.Add(draw); owned.Add(draw);
                }
            }
            return new(program, work, hasGeometry, explicitGate, faces, owned, explicitStarTexture is null);
        }
        catch (Exception constructionFailure)
        {
            var partial = Retirement(owned,
                constructionFailure is NativeMapBorrowerConstructionException nested ? nested.RetainedPackage : null);
            try { await partial.DisposeAsync(); }
            catch (Exception retirementFailure)
            {
                partial.RetainFailure();
                throw new NativeMapBorrowerConstructionException(constructionFailure, retirementFailure, partial);
            }
            throw;
        }
    }
    public async Task UpdateFrameAsync(IReadOnlyList<SkyStarFacePacket> explicitSixFaces, SkyStarNativeGate explicitGate)
    {
        ArgumentNullException.ThrowIfNull(explicitGate); _ = explicitGate.AllowsDraw;
        var packets = HasGeometry ? Snapshot(Program, explicitSixFaces) : Array.Empty<SkyStarFacePacket>();
        if (UsesAnalyticPixel) foreach (var packet in packets) SkyStarPixelAdapter.ValidateAnalyticPacket(packet.PixelConstants.Span);
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            needsCurrentFrame = true;
            for (int i = 0; i < packets.Length; i++)
                await _faces[i].UpdateConstantsAsync(packets[i].VertexConstants, packets[i].GeometryConstants, packets[i].PixelConstants);
            Gate = explicitGate;
            needsCurrentFrame = false;
        }
        finally { _gate.Release(); }
    }
    public SkySceneFaceProducer? CreateSceneStage()
    {
        RequireCurrentFrame();
        return HasGeometry ? new(Program.Source.Source.ResourceUri + "#native-pass=0", SubmitAsync) : null;
    }
    private void RequireCurrentFrame()
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        if (needsCurrentFrame)
            throw new InvalidOperationException("Star draws require all six actual current face packets.");
    }
    private async Task SubmitAsync(SkySceneFaceContext context)
    {
        if (!ReferenceEquals(context.PrimaryWork, Work) || context.Face >= 6 ||
            context.Target != Work.Surfaces[(int)SkySceneSchedule.SurfaceId(Work.Source.LevelNum, context.Face, 0)])
            throw new InvalidDataException("Star scene stage belongs to another WORK resource or surface.");
        await _gate.WaitAsync();
        try { RequireCurrentFrame(); if (Gate.AllowsDraw) await _faces[(int)context.Face].DrawAsync(); }
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
    private static NativeMapBorrowerRetirementPackage Retirement(List<IAsyncDisposable> owned,
        NativeMapBorrowerRetirementPackage? partialChild = null)
        => new("Star-producer/" + Guid.NewGuid().ToString("N"), [partialChild, .. owned.AsEnumerable().Reverse()]);

    public ValueTask DisposeAsync()
    {
        lock (_lifetimeGate)
        {
            _retiring = true;
            if (_disposal is null || _disposal.IsFaulted) _disposal = DisposeCoreAsync();
            return new(_disposal);
        }
    }
    private async Task DisposeCoreAsync()
    { await _gate.WaitAsync(); try { await retirement.DisposeAsync(); } finally { _gate.Release(); } }
}
