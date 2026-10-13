using Godot;
using San14.Data;

public enum SkyScenePhase { Idle, SkyBase, BasePreparation, Filter, Faulted }

public sealed record SkySceneProgramSelection(int Sky2Combination,
    Sky2Operation Sky2Operation, int CopyCombination, int FilterCombination,
    int? BlendCombination)
{
    public int? LightCombination { get; init; }
    public uint? Sky2ColorWriteMask { get; init; }
    public Sky2TextureBindingSnapshot TextureBindings { get; init; } = null!;
}

public sealed record SkySceneProgress(SkyScenePhase Phase,
    uint GenerationIndex, uint NextSkyFace, uint FilterFlat, long CompletedGenerations,
    uint? LastCompletedIndex, uint WorkUnits, uint SubmittedDraws, uint AdditionalProducerCalls);

public sealed record SkySceneBlendSurfaceInputs(uint SurfaceId,
    ReadOnlyMemory<byte> FaceMatrix, ReadOnlyMemory<byte> SamplerA,
    ReadOnlyMemory<byte> SamplerB);

public sealed record SkySceneBlendReuse(int CachedDraws,int CreatedDraws,int ReusedDraws);

public sealed record SkySceneFaceContext(uint Face,
    CubeRenderTextureDevice PrimaryWork, CubeRenderTextureDevice LightWork,
    CubeRenderDeviceSurface Target)
{
    /// <summary>E40540 side faces require the caller's original1196CA0
    /// contribution viewport preparation. Face3 retains the full viewport;
    /// face2 never calls that stage. This flag is not a guessed rectangle.</summary>
    public bool ContributionRequiresPreparedViewport => Face is 0 or 1 or 4 or 5;
}

public sealed record SkySceneFaceProducer(string SourceResourceUri,
    Func<SkySceneFaceContext, Task> SubmitAsync);

/// <summary>Explicit E40540 branches. Null means the caller selected that
/// branch as absent, not that an unknown native source was replaced by zero.
/// Producer resources stay caller-owned until scene retirement.</summary>
public sealed record SkySceneBaseStages(SkySceneFaceProducer? Stars,
    bool Sky2Enabled, SkySceneFaceProducer? Scene,
    SkySceneFaceProducer? Cloud, SkySceneFaceProducer? Contribution);

public enum SkySceneBaseObservationStage { AfterSky2, AfterCloud }
public sealed record SkySceneBaseObservation(uint GenerationIndex, long CompletedGenerations,
    SkySceneBaseObservationStage Stage, SkySceneFaceContext FaceContext);

public sealed record SkySceneOptionalLightInputs(
    IReadOnlyList<SkyLightDrawInputs> SixFaces,
    ReadOnlyMemory<byte> VertexTemplate, ReadOnlyMemory<byte> PixelTemplate,
    SkyLightDeviceInput GgxLut, ReadOnlyMemory<byte> CubeSampler,
    string CubeSourceUri, string PreservedFinalMipProvenance);

/// <summary>Scene-owned original Sky2 / cube-copy / filter / interpolation
/// resources. A caller chooses source combinations and supplies complete
/// per-face state and samplers. Optional light requires its native pass12
/// program, full packets, LUT and retained final P mip explicitly.
/// Native time/dirty/active selection and the diffuse writer are not guessed.</summary>
public sealed partial class SkyScene : IAsyncDisposable
{
    public LoadedSkyAmbientResources Source { get; }
    public SkySceneProgramSelection Selection { get; private set; }
    public Sky2Program Sky2Program => sky2Resources.Program;
    public SkyCubeProgram CopyProgram { get; }
    public SkyCubeProgram FilterProgram { get; }
    public SkyCubeBlendProgram? BlendProgram { get; }
    public SkyLightProgram? LightProgram { get; }
    public CubeRenderTextureDevice PrimaryWork { get; }
    public CubeRenderTextureDevice LightWork { get; }
    public IReadOnlyList<CubeRenderTextureDevice> SpecularTargets { get; }
    public IReadOnlyList<SkyFilterGeneratedSamples> GeneratedFilterSamples { get; }
    public SkySceneBlendReuse? LastCompletedBlendReuse { get; private set; }
    public IReadOnlyList<NativeMapStageTiming> LastAdvanceTimings { get; private set; } =
        Array.Empty<NativeMapStageTiming>();
    // Source metadata is retained, but this unclosed writer is never silently
    // supplied as an empty or specular-derived deferred input.
    public bool DiffuseGenerated => false;

    private readonly IReadOnlyList<IAsyncDisposable> _owned;
    private Sky2Resources sky2Resources;
    private IReadOnlyList<Sky2Draw> _skyFaces => sky2Resources.Faces;
    private bool sky2NeedsCurrentFrame;
    private readonly Dictionary<(uint Target, uint Surface), SkyCubeDraw> _baseCopies;
    private readonly Dictionary<uint, SkyCubeDraw> _workMipCopies;
    private readonly Dictionary<(uint Target, uint Surface), SkyCubeDraw> _filters;
    private readonly IReadOnlyList<SkyCubeDraw> _lightSnapshotCopies;
    // A scene owns three rotating input pairs and its fixed four cubes/program.
    // Changing a sampler recipe replaces its draw; matrices/scalars are current
    // constant-buffer writes. At most three pairs times all declared surfaces.
    private readonly Dictionary<(uint InputA,uint InputB,uint Surface),SkyCubeBlendDraw> _blendDraws=new();
    // Creation immediately transfers ownership here, including a replacement
    // whose preceding draw or its own cleanup may fail to retire.
    private readonly HashSet<SkyCubeBlendDraw> _blendDrawOwners=new();
    private IReadOnlyList<SkyLightDraw> _lightFaces = Array.Empty<SkyLightDraw>();
    private readonly bool[] _ready = new bool[4];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifetimeGate = new();
    private Task? _disposal;
    private bool _retiring;
    private SkyScenePhase _phase;
    private uint _generationIndex, _nextSkyFace, _filterFlat;
    private long _completedGenerations;
    private Func<SkySceneBaseObservation, Task>? _baseObserver;
    private Func<SkySceneFilterInputObservation,bool>? _filterInputSelector;
    private Func<SkySceneFilterInputObservation,Task>? _filterInputObserver;
    private Func<SkySceneFilterInputObservation,Task>? _filterOutputObserver;
    private SkySceneFilterInputObservation? _activeFilterInputObservation;

    /// <summary>Numeric-only one chosen generation. The predicate may skip
    /// backend startup without readback; the selected callback runs after all
    /// actual base/contribution/light/mip preparation, before its first filter.
    /// Do not call scene methods or change its inputs while this gate is held.</summary>
    public async Task ConfigureFilterInputObservationOnceAsync(
        Func<SkySceneFilterInputObservation,bool> selectGeneration,
        Func<SkySceneFilterInputObservation,Task> observe,
        Func<SkySceneFilterInputObservation,Task>? observeAfterFirstDrawReturned=null)
    {
        ArgumentNullException.ThrowIfNull(selectGeneration);ArgumentNullException.ThrowIfNull(observe);
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring,this);
            if(_phase!=SkyScenePhase.Idle||_completedGenerations!=0||_filterInputObserver is not null)
                throw new InvalidOperationException("Final WORK observation must be configured once before any generation.");
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                using var format=rd.TextureGetFormat(PrimaryWork.TextureRid);
                if((format.UsageBits&RenderingDevice.TextureUsageBits.CanCopyFromBit)==0)
                    throw new InvalidOperationException("Final WORK observation needs explicit source readback allocation.");
                return true;
            });
            _filterInputSelector=selectGeneration;_filterInputObserver=observe;_filterOutputObserver=observeAfterFirstDrawReturned;
        }
        finally{_gate.Release();}
    }
    internal void RequireCurrentFilterInputObservation(SkySceneFilterInputObservation actual)
    {
        ObjectDisposedException.ThrowIf(_retiring,this);
        if(!ReferenceEquals(_activeFilterInputObservation,actual)||_phase!=SkyScenePhase.Filter||
            _filterFlat!=6||_nextSkyFace!=6||_generationIndex!=actual.GenerationIndex||
            _completedGenerations!=actual.CompletedGenerations||!ReferenceEquals(actual.FilterDraw.InputOwner,PrimaryWork)||
            !ReferenceEquals(actual.FilterDraw.Program,FilterProgram))
            throw new InvalidOperationException("Filter numeric observation is only current inside this exact first-filter callback boundary.");
        actual.FilterDraw.RequireCurrentDiagnosticBindings();
    }

    /// <summary>Install an explicit numeric observer before the first WORK
    /// generation. It runs between actual source draws, under this owner's
    /// gate; it must not call scene methods or change the borrowed targets.
    /// Normal game construction installs no observer.</summary>
    public async Task ConfigureBaseObservationAsync(Func<SkySceneBaseObservation, Task> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            if (_phase != SkyScenePhase.Idle || _completedGenerations != 0 || _baseObserver is not null)
                throw new InvalidOperationException("Sky base observation must be configured once before any generation.");
            await TerrainDeviceTexture.OnRenderThreadAsync(rd =>
            {
                using var format = rd.TextureGetFormat(PrimaryWork.TextureRid);
                if ((format.UsageBits & RenderingDevice.TextureUsageBits.CanCopyFromBit) == 0)
                    throw new InvalidOperationException("Sky base observation requires explicit source readback allocation.");
                return true;
            });
            _baseObserver = observer;
        }
        finally { _gate.Release(); }
    }
    private uint? _lastCompletedIndex;
    private SkySceneBaseStages? _stages;

    private SkyScene(LoadedSkyAmbientResources source,
        SkySceneProgramSelection selection, Sky2Program sky,
        SkyCubeProgram copy, SkyCubeProgram filter,
        SkyCubeBlendProgram? blend, CubeRenderTextureDevice work,
        CubeRenderTextureDevice lightWork, IReadOnlyList<CubeRenderTextureDevice> targets,
        IReadOnlyList<SkyFilterGeneratedSamples> generated,
        IReadOnlyList<Sky2Draw> skyFaces,
        Dictionary<(uint, uint), SkyCubeDraw> baseCopies,
        Dictionary<uint, SkyCubeDraw> workMipCopies,
        Dictionary<(uint, uint), SkyCubeDraw> filters,
        SkyLightProgram? lightProgram, IReadOnlyList<SkyCubeDraw> lightCopies,
        List<IAsyncDisposable> owned)
    {
        sky2Resources = new(sky, skyFaces);
        (Source, Selection, CopyProgram, FilterProgram, BlendProgram,
            PrimaryWork, LightWork, SpecularTargets, GeneratedFilterSamples,
            _baseCopies, _workMipCopies, _filters, _owned) =
            (source, selection, copy, filter, blend, work, lightWork, targets,
                generated, baseCopies, workMipCopies, filters, owned.AsReadOnly());
        (LightProgram, _lightSnapshotCopies) = (lightProgram, lightCopies);
    }

    /// <summary>Allocates once and caches all supported generation draws.
    /// Input textures remain borrowed until scene retirement. Log dispatch,
    /// all source combinations, samplers, templates and six initial face
    /// packets are explicit; no diagnostic texture is created here.</summary>
    public static async Task<SkyScene> BuildAsync(
        LoadedSkyAmbientResources ambient, LoadedShaderContainer skySource,
        SkySceneProgramSelection selection, Vkd3dShaderCompiler compiler,
        IReadOnlyList<Sky2DrawInputs> explicitSixFaceInputs,
        ReadOnlyMemory<byte> explicitVertexTemplate, ReadOnlyMemory<byte> explicitPixelTemplate,
        IReadOnlyList<Sky2DeviceInput> explicitTextures,
        ReadOnlyMemory<byte> explicitCopySampler, ReadOnlyMemory<byte> explicitFilterSampler,
        NativeLogTables logTables, NativeLogDispatch logDispatch,
        bool allowReadback = false)
    {
        ArgumentNullException.ThrowIfNull(ambient); ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(logTables); ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(explicitTextures);
        ArgumentNullException.ThrowIfNull(selection.TextureBindings);selection.TextureBindings.RequireCurrent();
        ArgumentNullException.ThrowIfNull(explicitSixFaceInputs);
        if (ambient.SpecularTargets.Count != 4 || explicitSixFaceInputs.Count != 6 ||
            explicitCopySampler.Length != 52 || explicitFilterSampler.Length != 52)
            throw new InvalidDataException("Sky scene requires four source specular declarations, six face packets and two complete samplers.");
        if (selection.Sky2ColorWriteMask is null or > 15)
            throw new InvalidDataException("Sky scene requires the explicit retained native Sky2 write mask.");
        var first = ambient.SpecularTargets[0];
        // This is the bounded native Level7 E46810 chain. Supporting other
        // dimensions later requires an explicit schedule profile.
        if (first.Width != 128 || first.LevelNum != 7 || ambient.SpecularTargets.Any(t =>
            t.Width != first.Width || t.LevelNum != first.LevelNum ||
            t.PixelFormat != first.PixelFormat || t.ColorSpace != first.ColorSpace ||
            t.OutputColorSpace != first.OutputColorSpace || t.MinMipLevel != 0 || t.MultiSample != 0))
            throw new InvalidDataException("Sky scene requires the verified 128-square, seven-mip source specular profile.");
        byte[] copySampler = explicitCopySampler.ToArray(), filterSampler = explicitFilterSampler.ToArray();
        byte[] vertexTemplate = explicitVertexTemplate.ToArray(), pixelTemplate = explicitPixelTemplate.ToArray();
        var owned = new List<IAsyncDisposable>();
        try
        {
            var sky = await Sky2Program.BuildAsync(skySource, selection.Sky2Combination,
                compiler, selection.Sky2Operation, selection.TextureBindings); owned.Add(sky);
            var copy = await SkyCubeProgram.BuildAsync(ambient.Shader,
                selection.CopyCombination, compiler, SkyCubeOperation.Copy); owned.Add(copy);
            var filter = await SkyCubeProgram.BuildAsync(ambient.Shader,
                selection.FilterCombination, compiler, SkyCubeOperation.Filter); owned.Add(filter);
            if(copy.SourceSampleSelection.SampleCount!=filter.SourceSampleSelection.SampleCount)
                throw new InvalidDataException("Sky copy/filter combinations must share the original requested sample count.");
            SkyCubeBlendProgram? blend = null;
            if (selection.BlendCombination is int blendCombination)
            { blend = await SkyCubeBlendProgram.BuildAsync(ambient.Shader, blendCombination, compiler); owned.Add(blend); }
            SkyLightProgram? lightProgram = null;
            if (selection.LightCombination is int lightCombination)
            { lightProgram = await SkyLightProgram.BuildAsync(ambient.Shader, lightCombination, compiler); owned.Add(lightProgram); }
            var facePackets = SnapshotFaces(sky, explicitSixFaceInputs, vertexTemplate, pixelTemplate);
            var targets = new List<CubeRenderTextureDevice>();
            foreach (var declaration in ambient.SpecularTargets)
            { var target = await CubeRenderTextureDevice.BuildAsync(declaration, allowReadback); targets.Add(target); owned.Add(target); }
            // 1197F90 derives two separate working resources from the first
            // cube's exact dimensions/format; these are not extra DOK objects.
            var work = await CubeRenderTextureDevice.BuildAsync(first, allowReadback); owned.Add(work);
            var lightWork = await CubeRenderTextureDevice.BuildAsync(first, allowReadback); owned.Add(lightWork);
            var views = new List<CubeShaderView>();
            for (uint mip = 0; mip < first.LevelNum; mip++)
            { var view = await CubeShaderView.BuildAsync(work, mip, 1); views.Add(view); owned.Add(view); }
            var lightCopies = new List<SkyCubeDraw>();
            if (lightProgram is not null)
            {
                var lightViews = new List<CubeShaderView>();
                for (uint mip = 0; mip < first.LevelNum; mip++)
                { var view = await CubeShaderView.BuildAsync(lightWork, mip, 1); lightViews.Add(view); owned.Add(view); }
                foreach (var step in SkySceneSchedule.LightSnapshot(first.LevelNum))
                {
                    bool snapshotBase = step.Operation == SkySceneCopyOperation.LightSnapshotBase;
                    var draw = await SkyCubeDraw.BuildAsync(copy, snapshotBase ? work : lightWork,
                        lightWork, step.DestinationSurfaceId, copySampler,
                        sourceView: (snapshotBase ? views : lightViews)[(int)step.SourceSingleMip!.Value]);
                    lightCopies.Add(draw); owned.Add(draw);
                }
            }
            var generated = new List<SkyFilterGeneratedSamples>();
            var sampleBuffers = new Dictionary<uint, SkyFilterSampleBuffers>();
            for (uint mip = 1; mip < first.LevelNum; mip++)
            {
                var samples = SkyFilterSampleMath.GenerateMip((uint)filter.FilterSampleCount,
                    checked((ushort)first.Width), first.LevelNum, mip, logTables, logDispatch,
                    (uint)filter.FilterSampleCount);
                generated.Add(samples);
                var buffers = await SkyFilterSampleBuffers.BuildAsync(samples.Packet);
                sampleBuffers.Add(mip, buffers); owned.Add(buffers);
            }
            var faces = new List<Sky2Draw>();
            for (uint face = 0; face < 6; face++)
            {
                var draw = await Sky2Draw.BuildAsync(sky, work,
                    SkySceneSchedule.SurfaceId(first.LevelNum, face, 0),
                    facePackets[(int)face].Vertex.Bytes,facePackets[(int)face].Pixel.Bytes,explicitTextures,
                    new Sky2NativeComposition(selection.Sky2ColorWriteMask.Value));
                faces.Add(draw); owned.Add(draw);
            }
            var bases = new Dictionary<(uint, uint), SkyCubeDraw>();
            var mipCopies = new Dictionary<uint, SkyCubeDraw>();
            foreach (var step in SkySceneSchedule.BasePreparation(first.LevelNum))
            {
                if (step.Operation == SkySceneCopyOperation.WorkMip)
                {
                    var draw = await SkyCubeDraw.BuildAsync(copy, work, work,
                        step.DestinationSurfaceId, copySampler, sourceView: views[(int)step.SourceSingleMip!.Value]);
                    mipCopies.Add(step.DestinationSurfaceId, draw); owned.Add(draw);
                }
                else for (uint target = 0; target < 3; target++)
                {
                    var draw = await SkyCubeDraw.BuildAsync(copy, work, targets[(int)target],
                        step.DestinationSurfaceId, copySampler);
                    bases.Add((target, step.DestinationSurfaceId), draw); owned.Add(draw);
                }
            }
            var filters = new Dictionary<(uint, uint), SkyCubeDraw>();
            foreach (var step in SkySceneSchedule.Filter(first.LevelNum, 6, uint.MaxValue))
                for (uint target = 0; target < 3; target++)
                {
                    var draw = await SkyCubeDraw.BuildAsync(filter, work, targets[(int)target],
                        step.DestinationSurfaceId, filterSampler, sampleBuffers[step.Mip]);
                    filters.Add((target, step.DestinationSurfaceId), draw); owned.Add(draw);
                }
            // Sky2 has a replaceable lifetime; the other resources stay scene-owned.
            owned.Remove(sky);
            foreach (var face in faces) owned.Remove(face);
            return new(ambient, selection, sky, copy, filter, blend, work, lightWork,
                targets.AsReadOnly(), generated.AsReadOnly(), faces.AsReadOnly(), bases, mipCopies, filters,
                lightProgram, lightCopies.AsReadOnly(), owned);
        }
        catch
        { for (int i = owned.Count - 1; i >= 0; i--) await owned[i].DisposeAsync(); throw; }
    }

    /// <summary>Starts an explicit generation without drawing. All face
    /// packets are validated before any buffer update. A slot becomes
    /// unavailable until its entire generation completes.</summary>
    public Task BeginGenerationAsync(uint explicitGenerationIndex,
        IReadOnlyList<Sky2DrawInputs> explicitSixFaceInputs,
        SkySceneBaseStages explicitStages, bool optionalLightBranch)
    {
        if (optionalLightBranch)
            throw new NotSupportedException("Optional light needs explicit native pass12 packets, LUT and preserved P last mip.");
        return BeginGenerationAsync(explicitGenerationIndex, explicitSixFaceInputs, explicitStages,
            (SkySceneOptionalLightInputs?)null);
    }

    /// <summary>Consume a fully prepared named-source frame. Its preparation
    /// does not choose the native pass/variant or active generation slot.</summary>
    public Task BeginGenerationAsync(uint explicitGenerationIndex,
        SkyScenePreparedFrame explicitPreparedFrame,
        SkySceneBaseStages explicitStages, SkySceneOptionalLightInputs? explicitLight)
    {
        ArgumentNullException.ThrowIfNull(explicitPreparedFrame);
        return BeginGenerationAsync(explicitGenerationIndex, explicitPreparedFrame.DrawPackets,
            explicitStages, explicitLight);
    }

    public async Task BeginGenerationAsync(uint explicitGenerationIndex,
        IReadOnlyList<Sky2DrawInputs> explicitSixFaceInputs,
        SkySceneBaseStages explicitStages, SkySceneOptionalLightInputs? explicitLight)
    {
        ArgumentNullException.ThrowIfNull(explicitStages);
        foreach (var producer in new[] { explicitStages.Stars, explicitStages.Scene,
            explicitStages.Cloud, explicitStages.Contribution })
            if (producer is not null && (string.IsNullOrWhiteSpace(producer.SourceResourceUri) || producer.SubmitAsync is null))
                throw new InvalidDataException("Every enabled sky face producer requires explicit provenance and submission.");
        if (explicitGenerationIndex >= 3)
            throw new ArgumentOutOfRangeException(nameof(explicitGenerationIndex));
        if (explicitLight is not null && (LightProgram is null || explicitLight.SixFaces.Count != 6 ||
            explicitLight.GgxLut.SourceBindPoint != 0 || explicitLight.GgxLut.SourceDimension != 4 ||
            explicitLight.CubeSampler.Length != 52 || string.IsNullOrWhiteSpace(explicitLight.CubeSourceUri) ||
            string.IsNullOrWhiteSpace(explicitLight.PreservedFinalMipProvenance)))
            throw new InvalidDataException("Optional light needs a chosen native program, six packets, LUT, cube sampler and retained P final-mip provenance.");
        if (explicitLight is not null)
            explicitLight = explicitLight with { SixFaces = explicitLight.SixFaces.Select(p =>
                new SkyLightDrawInputs(p.Vertex, p.Pixel with { Scattering = p.Pixel.Scattering.ToArray() })).ToArray(),
                VertexTemplate = explicitLight.VertexTemplate.ToArray(), PixelTemplate = explicitLight.PixelTemplate.ToArray(),
                CubeSampler = explicitLight.CubeSampler.ToArray(),
                GgxLut = explicitLight.GgxLut with { SamplerDescriptor = explicitLight.GgxLut.SamplerDescriptor.ToArray() } };
        // Use existing draw templates for their verified padding. Only named
        // fields are updated, retaining exactly the initial template padding.
        var packets = SnapshotFaces(Sky2Program, explicitSixFaceInputs,
            _skyFaces[0].VertexConstantBytes, _skyFaces[0].PixelConstantBytes);
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            if (_phase is not (SkyScenePhase.Idle or SkyScenePhase.Faulted))
                throw new InvalidOperationException("Finish the current sky generation before beginning another.");
            var lightFaces = new List<SkyLightDraw>();
            try
            {
                if (explicitLight is not null)
                    for (uint face = 0; face < 6; face++)
                        lightFaces.Add(await SkyLightDraw.BuildAsync(LightProgram!, PrimaryWork,
                            SkySceneSchedule.SurfaceId(PrimaryWork.Source.LevelNum, face, 0),
                            explicitLight.SixFaces[(int)face], explicitLight.VertexTemplate, explicitLight.PixelTemplate,
                            [explicitLight.GgxLut, new(3, 9, LightWork.TextureRid, LightWork.TextureRid,
                                explicitLight.CubeSampler, explicitLight.CubeSourceUri)]));
                foreach (var pair in packets.Select((p, i) => (p, i)))
                    await _skyFaces[pair.i].UpdatePreparedConstantsAsync(pair.p.Vertex.Bytes,pair.p.Pixel.Bytes);
            }
            catch
            { for (int i = lightFaces.Count - 1; i >= 0; i--) await lightFaces[i].DisposeAsync(); _phase = SkyScenePhase.Faulted; throw; }
            for (int i = _lightFaces.Count - 1; i >= 0; i--) await _lightFaces[i].DisposeAsync();
            _lightFaces = lightFaces.AsReadOnly();
            sky2NeedsCurrentFrame = false;
            _ready[explicitGenerationIndex] = false;
            _generationIndex = explicitGenerationIndex; _nextSkyFace = 0; _filterFlat = 6;
            _stages = explicitStages;
            _phase = SkyScenePhase.SkyBase;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Runs supported work in exact source order. Six generated base
    /// faces precede the one-unit preparation call; remaining filter work
    /// resumes using native mip-major flat progress. Caller budget is explicit.
    /// This wrapper does not implement original wall-clock/dirty selection.</summary>
    public async Task<SkySceneProgress> AdvanceAsync(uint maximumWorkUnits)
    {
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            if (sky2NeedsCurrentFrame)
                throw new InvalidOperationException("Replacement Sky2 requires a complete current generation before submitting work.");
            if (_phase == SkyScenePhase.Faulted)
                throw new InvalidOperationException("A failed sky generation requires explicit restart.");
            uint used = 0, draws = 0, producerCalls = 0;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            var phaseMilliseconds=new double[3];
            try
            {
                while (used < maximumWorkUnits && _phase != SkyScenePhase.Idle)
                {
                    double started=timer.Elapsed.TotalMilliseconds;
                    int timingStage=_phase==SkyScenePhase.SkyBase?0:
                        _phase==SkyScenePhase.BasePreparation?1:2;
                    if (_phase == SkyScenePhase.SkyBase)
                    {
                        var context = new SkySceneFaceContext(_nextSkyFace, PrimaryWork, LightWork,
                            PrimaryWork.Surfaces[(int)SkySceneSchedule.SurfaceId(PrimaryWork.Source.LevelNum, _nextSkyFace, 0)]);
                        // E40540 clears every base face before composition.
                        // Native also clears its bound depth attachment; this
                        // color-only WORK adapter has no depth attachment.
                        await ClearBaseFaceAsync(context.Target);
                        // Face3 jumps directly to the later pass12 branch.
                        if (_nextSkyFace != 3)
                        {
                            if (_stages!.Stars is { } stars)
                            { await stars.SubmitAsync(context); producerCalls++; }
                            if (_stages.Sky2Enabled)
                            {
                                await _skyFaces[(int)_nextSkyFace].DrawAsync(); draws++;
                                if (_baseObserver is not null) await _baseObserver(new(_generationIndex,
                                    _completedGenerations, SkySceneBaseObservationStage.AfterSky2, context));
                            }
                            if (_stages.Scene is { } scene) { await scene.SubmitAsync(context); producerCalls++; }
                            if (_stages.Cloud is { } cloud)
                            {
                                await cloud.SubmitAsync(context); producerCalls++;
                                if (_baseObserver is not null) await _baseObserver(new(_generationIndex,
                                    _completedGenerations, SkySceneBaseObservationStage.AfterCloud, context));
                            }
                        }
                        // E40955 face2 jumps past11973B0. Side-face viewport
                        // restriction stays with the explicitly sourced stage.
                        if (_nextSkyFace != 2 && _stages!.Contribution is { } contribution)
                        { await contribution.SubmitAsync(context); producerCalls++; }
                        _nextSkyFace++; used++;
                        if (_nextSkyFace == 6) _phase = SkyScenePhase.BasePreparation;
                    }
                    else if (_phase == SkyScenePhase.BasePreparation)
                    {
                        if (_lightFaces.Count != 0)
                        {
                            foreach (var draw in _lightSnapshotCopies) { await draw.DrawAsync(); draws++; }
                            foreach (var draw in _lightFaces) { await draw.DrawAsync(); draws++; }
                        }
                        var copies=SkySceneSchedule.BasePreparation(PrimaryWork.Source.LevelNum)
                            .Select(step=>step.Operation == SkySceneCopyOperation.WorkMip ?
                                _workMipCopies[step.DestinationSurfaceId] :
                                _baseCopies[(_generationIndex, step.DestinationSurfaceId)]).ToArray();
                        await SkyCubeDraw.DrawOrderedBatchAsync(copies);
                        draws+=checked((uint)copies.Length);
                        used++; _phase = SkyScenePhase.Filter;
                    }
                    else
                    {
                        var step = SkySceneSchedule.Filter(PrimaryWork.Source.LevelNum, _filterFlat, 1).Single();
                        var filterDraw=_filters[(_generationIndex, step.DestinationSurfaceId)];
                        Func<SkySceneFilterInputObservation,Task>? outputObserver=null;
                        if(_filterFlat==6&&_filterInputObserver is{} observer)
                        {
                            var observation=new SkySceneFilterInputObservation(this,_generationIndex,
                                _completedGenerations,_filterFlat,filterDraw,_stages!,_lightFaces.Count);
                            if(_filterInputSelector!(observation))
                            {
                                //One actual chosen generation, even if its readback fails.
                                outputObserver=_filterOutputObserver;_filterOutputObserver=null;
                                _filterInputObserver=null;_filterInputSelector=null;_activeFilterInputObservation=observation;
                                try{await observer(observation);}finally{_activeFilterInputObservation=null;}
                            }
                        }
                        await filterDraw.DrawAsync();
                        if(outputObserver is not null)
                        {
                            var observation=new SkySceneFilterInputObservation(this,_generationIndex,
                                _completedGenerations,_filterFlat,filterDraw,_stages!,_lightFaces.Count,true);
                            _activeFilterInputObservation=observation;
                            try{await outputObserver(observation);}finally{_activeFilterInputObservation=null;}
                        }
                        _filterFlat++; used++; draws++;
                        if (_filterFlat == 6u * PrimaryWork.Source.LevelNum)
                        {
                            _ready[_generationIndex] = true; _lastCompletedIndex = _generationIndex;
                            _completedGenerations++; _phase = SkyScenePhase.Idle;
                        }
                    }
                    phaseMilliseconds[timingStage]+=timer.Elapsed.TotalMilliseconds-started;
                }
                double elapsed=0;
                string[] names=["base-face-submission","copy-preparation-submission","filter-submission"];
                LastAdvanceTimings=Array.AsReadOnly(phaseMilliseconds.Select((value,index)=>
                    new NativeMapStageTiming(names[index],value,elapsed+=value)).ToArray());
                return Progress(used, draws, producerCalls);
            }
            catch
            { _ready[_generationIndex] = false; _phase = SkyScenePhase.Faulted; throw; }
        }
        finally { _gate.Release(); }
    }

    /// <summary>Borrow a fully generated original target. It remains owned by
    /// the scene; complete all consumer draws before restarting that same slot
    /// or retiring the scene.</summary>
    public CubeRenderTextureDevice GetCompletedSpecular(uint index)
    {
        ObjectDisposedException.ThrowIf(_retiring, this);
        if (index >= 4 || !_ready[index])
            throw new InvalidOperationException("Requested sky specular source has no complete generation.");
        return SpecularTargets[(int)index];
    }

    /// <summary>Uses the original (k+1)%3, (k+2)%3 to source-index3 mapping.
    /// Each complete face/mip matrix and sampler remains explicit, including
    /// inherited MinLOD. It does not choose the native time/output branch.</summary>
    public async Task BlendAsync(uint explicitGenerationIndex, float explicitInterpolation,
        IReadOnlyList<SkySceneBlendSurfaceInputs> explicitSurfaces)
    {
        var indices = SkySceneSchedule.BlendIndices(explicitGenerationIndex);
        if (!float.IsFinite(explicitInterpolation) || explicitSurfaces.Count != SpecularTargets[3].Surfaces.Count)
            throw new InvalidDataException("Sky blend requires a finite scalar and every declared surface exactly once.");
        var packets = explicitSurfaces.Select(p => p with { FaceMatrix = p.FaceMatrix.ToArray(),
            SamplerA = p.SamplerA.ToArray(), SamplerB = p.SamplerB.ToArray() }).ToArray();
        if (packets.Select(p => p.SurfaceId).Distinct().Count() != packets.Length ||
            packets.Any(p => p.SurfaceId >= packets.Length || p.FaceMatrix.Length != 64 || p.SamplerA.Length != 52 || p.SamplerB.Length != 52))
            throw new InvalidDataException("Sky blend surface, matrix and sampler inventory is incomplete.");
        foreach(var packet in packets)
            SkyCubeBlendDraw.ValidateConstants(explicitInterpolation,packet.FaceMatrix.Span);
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            if (BlendProgram is null) throw new NotSupportedException("No original blend source combination was supplied.");
            if (!_ready[indices.InputA] || !_ready[indices.InputB])
                throw new InvalidOperationException("Both original blend inputs must have complete generations.");
            var draws = new List<(SkyCubeBlendDraw Draw,SkySceneBlendSurfaceInputs Packet)>();
            int createdDraws=0;
            // Allocate every missing/current binding before changing the target.
            // Cubes, framebuffer surfaces and program cannot change inside this
            // scene; a new resource generation gets a new scene and cache.
            foreach(var packet in packets)
            {
                var key=(indices.InputA,indices.InputB,packet.SurfaceId);
                if(!_blendDraws.TryGetValue(key,out var draw)||
                    !draw.SourceSamplerDescriptorA.AsSpan().SequenceEqual(packet.SamplerA.Span)||
                    !draw.SourceSamplerDescriptorB.AsSpan().SequenceEqual(packet.SamplerB.Span))
                {
                    var replacement=await SkyCubeBlendDraw.BuildAsync(BlendProgram,
                        SpecularTargets[(int)indices.InputA], SpecularTargets[(int)indices.InputB],
                        SpecularTargets[3], packet.SurfaceId, explicitInterpolation,
                        packet.FaceMatrix, packet.SamplerA, packet.SamplerB);
                    _blendDrawOwners.Add(replacement);
                    try
                    {
                        if(draw is not null){await draw.DisposeAsync();_blendDrawOwners.Remove(draw);}
                    }
                    catch
                    {
                        await replacement.DisposeAsync();_blendDrawOwners.Remove(replacement);
                        throw;
                    }
                    _blendDraws[key]=replacement;
                    draw=replacement;
                    createdDraws++;
                }
                draws.Add((draw,packet));
            }
            _ready[3] = false;
            foreach(var entry in draws)
                await entry.Draw.DrawCurrentAsync(explicitInterpolation,entry.Packet.FaceMatrix);
            _ready[3] = true;
            LastCompletedBlendReuse=new(_blendDraws.Count,createdDraws,draws.Count-createdDraws);
        }
        finally { _gate.Release(); }
    }

    private SkySceneProgress Progress(uint units, uint draws, uint producers) => new(_phase,
        _generationIndex, _nextSkyFace, _filterFlat, _completedGenerations, _lastCompletedIndex, units, draws, producers);

    /// <summary>Inspect the current generation without submitting GPU work.</summary>
    public async Task<SkySceneProgress> GetProgressAsync()
    {
        await _gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_retiring, this);
            return Progress(0, 0, 0);
        }
        finally { _gate.Release(); }
    }

    private static Task ClearBaseFaceAsync(CubeRenderDeviceSurface target)
        => TerrainDeviceTexture.OnRenderThreadAsync(rd =>
        {
            if (!rd.FramebufferIsValid(target.Framebuffer))
                throw new InvalidDataException("Sky base target is no longer live.");
            rd.DrawListBegin(target.Framebuffer, RenderingDevice.DrawFlags.ClearColorAll, [new Color(0, 0, 0, 0)]);
            rd.DrawListEnd(); return true;
        });

    private static (Sky2ConstantPacket Vertex,Sky2ConstantPacket Pixel)[] SnapshotFaces(Sky2Program program,
        IReadOnlyList<Sky2DrawInputs> faces, ReadOnlyMemory<byte> vertexTemplate,
        ReadOnlyMemory<byte> pixelTemplate)
    {
        ArgumentNullException.ThrowIfNull(faces);
        program.RequireCurrent();
        if (faces.Count != 6 || faces.Any(p => p is null))
            throw new InvalidDataException("Sky scene requires six complete explicit face packets.");
        return faces.Select(face =>
        {
            var vertex=program.VertexConstants.WriteVertex(vertexTemplate.Span,face.Vertex);
            var pixel=program.PixelConstants.WritePixel(pixelTemplate.Span,face.Pixel);
            program.ValidateNullTexturePacket(pixel.Bytes);
            return (vertex,pixel);
        }).ToArray();
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetimeGate)
        {
            _retiring = true;
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)_disposal=DisposeCoreAsync();
            return new(_disposal);
        }
    }

    private async Task DisposeCoreAsync()
    {
        await _gate.WaitAsync();
        try
        {
            // Cached blend bindings borrow the scene's cubes and program.
            // Retire them before any of those dependencies can be released.
            foreach(var draw in _blendDrawOwners.Reverse().ToArray())
            {await draw.DisposeAsync();_blendDrawOwners.Remove(draw);}
            _blendDraws.Clear();
            for (int i = _lightFaces.Count - 1; i >= 0; i--) await _lightFaces[i].DisposeAsync();
            await sky2Resources.DisposeAsync();
            for (int i = _owned.Count - 1; i >= 0; i--) await _owned[i].DisposeAsync();
        }
        finally { _gate.Release(); }
    }
}
