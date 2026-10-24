using System.Collections.ObjectModel;
using San14.Core;
using San14.Data;

/// <summary>Explicit presentation seconds. The source action0 clip repeats
/// its declared duration; each owned pose initialization starts its own epoch.
/// This input never reads a process clock or infers an original current action.</summary>
public sealed record NativeMapArmyAnimationTime(float CallerSeconds,
    ArmyAnimationClockPolicy ClipPolicy,string CallerSource)
{
    internal void Validate()
    {
        if(!float.IsFinite(CallerSeconds)||CallerSeconds<0||ClipPolicy!=ArmyAnimationClockPolicy.RepeatDeclaredDuration||
            string.IsNullOrWhiteSpace(CallerSource))
            throw new InvalidDataException("Native Army population requires labelled explicit nonnegative seconds and the owned source action0 repeat policy.");
    }
}

public sealed record NativeMapArmyFrameInput(GameState World,NativeMapArmyAnimationTime Animation);

/// <summary>Detached logical prefix for one exact accepted target/Camera
/// context. Later deployment, movement or disband cannot change this frame.</summary>
public sealed record NativeMapArmyPreparedFrame(NativeMapFrameContext Context,string SourceRevision,
    int WorldPeriodNumber,NativeMapArmyAnimationTime Animation,
    IReadOnlyDictionary<int,RemakeArmyVisualFrame> Ordinary,
    IReadOnlyDictionary<int,LoadedArmyModel> Models,
    IReadOnlyDictionary<int,RemakeArmyFlagFrame> Flags,RemakeArmyFlagCurrentFrame? SourceMaterials,
    IReadOnlyList<PlayableArmyVisualFailure> Unresolved)
{
    public int OrdinaryActors=>Ordinary.Values.Sum(frame=>frame.Publications.Count);
    public int FlagActors=>Flags.Values.Sum(frame=>frame.Actors.Count);
    public bool PreserveUnitMarkers=>true;
    public bool ProcessStateFidelityVerified=>false;
    public NativeArmyCpuPrimitivePopulation? CpuPrimitivePopulation { get; internal set; }
}

public sealed record NativeMapArmyPopulationStatistics(long AcceptedFrames,long FailedFrames,
    int OrdinaryActors,int FlagActors,int OrdinaryDraws,int FlagDraws,int ProgramCount,int GeometryCount,
    long DrawCreationCount,int UnavailableFlagComponents);
public sealed record NativeMapArmyPopulationAcceptedFrame(NativeMapArmyPreparedFrame Prepared,
    IReadOnlyList<RemakeArmyFlagRenderStatus> FlagStatuses,NativeMapArmyPopulationStatistics Statistics)
{
    public IReadOnlyList<DeferredSourceMode16Candidate>? CurrentViewFamilyCandidates { get; init; }
    public ModelGeometrySubmittedFrame? OrdinaryGeometrySubmission { get; init; }
    public ModelGeometrySubmittedFrame? FlagGeometrySubmission { get; init; }
    public NativeArmyCurrentAdmission? NativePrimitiveAdmission { get; init; }
    public ArmyWorldQueuesCompletedPublication? WorldQueuesCompletedPublication { get; init; }
}

/// <summary>Independent current-owned Army stage beside the static model
/// population. It borrows the scene's actual five MRT/depth target and compiler;
/// the two source Army consumers own their reusable programs/geometry/images.
/// BeforeFrame captures one source prefix. AfterTerrain submits ordinary actors
/// followed by flags without clearing or retiring another owner's draws.</summary>
public sealed partial class NativeMapArmyPopulationRenderer:IAsyncDisposable
{
    private readonly string _revision;
    private readonly LoadedScenario _source;
    private readonly LoadedMap _map;
    private readonly PlayableArmyVisualAdapter _adapter;
    private RemakeArmySceneVisuals _ordinary;
    private RemakeArmyFlagVisuals _flags;
    private readonly RemakeArmyFlagCurrentSource _materials;
    private readonly RemakeArmyNativeRenderer _ordinaryRenderer;
    private readonly RemakeArmyFlagNativeRenderer _flagRenderer;
    private readonly Func<NativeMapFrameContext,NativeMapArmyFrameInput> _input;
    private readonly string _graphUri,_graphSha;
    private readonly Dictionary<NativeMapFrameContext,NativeMapArmyPreparedFrame> _prepared=new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<NativeMapFrameContext,NativeArmyCurrentAdmission> _nativeAdmissions=new(ReferenceEqualityComparer.Instance);
    private readonly NativeArmyPrimitiveAdmissionOwner _primitiveOwner=NativeArmyPrimitiveAdmissionOwner.CreateFreshNoExternalPrimitiveAssignments();
    private GameState? _lastWorld;
    private IReadOnlyDictionary<int,RemakeArmyVisualFrame> _ordinarySnapshot=new ReadOnlyDictionary<int,RemakeArmyVisualFrame>(new Dictionary<int,RemakeArmyVisualFrame>());
    private IReadOnlyDictionary<int,LoadedArmyModel> _modelsSnapshot=new ReadOnlyDictionary<int,LoadedArmyModel>(new Dictionary<int,LoadedArmyModel>());
    private IReadOnlyDictionary<int,RemakeArmyFlagFrame> _flagsSnapshot=new ReadOnlyDictionary<int,RemakeArmyFlagFrame>(new Dictionary<int,RemakeArmyFlagFrame>());
    private RemakeArmyFlagCurrentFrame? _materialSnapshot;
    private IReadOnlyList<PlayableArmyVisualFailure> _unresolved=[];
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();private bool _retiring;private int _operations;
    private TaskCompletionSource? _drained;private Task? _disposal;
    private NativeMapBorrowerRetirementPackage? _finalRetirement;
    private long _acceptedFrames,_failedFrames;
    public NativeMapArmyPopulationAcceptedFrame? LastAcceptedFrame{get;private set;}
    public string? LastFailure{get;private set;}
    public NativeMapArmyPopulationStatistics Statistics=>LastAcceptedFrame is { WorldQueuesCompletedPublication: not null } worldAccepted?
        worldAccepted.Statistics with { AcceptedFrames=_acceptedFrames,FailedFrames=_failedFrames }:new(_acceptedFrames,_failedFrames,
        LastAcceptedFrame?.Prepared.OrdinaryActors??0,LastAcceptedFrame?.Prepared.FlagActors??0,
        _ordinaryRenderer.DrawCount,_flagRenderer.DrawCount,_ordinaryRenderer.ProgramCount+_flagRenderer.ProgramCount,
        _ordinaryRenderer.GeometryCount+_flagRenderer.GeometryCount,
        _ordinaryRenderer.DrawCreationCount+_flagRenderer.DrawCreationCount,_flagRenderer.Statuses.Count(s=>!s.Submitted));

    private NativeMapArmyPopulationRenderer(Vkd3dShaderCompiler compiler,GameResourceRepository repository,
        string revision,LoadedScenario source,LoadedMap map,MapWorldProjection projection,LoadedHeightField height,
        Func<NativeMapFrameContext,NativeMapArmyFrameInput> currentInput)
    {
        _revision=revision;_source=source;_map=map;_input=currentInput;
        _adapter=PlayableArmyVisualAdapter.Load(repository,map,source.Snapshot.Locale);
        _ordinary=new(repository,source.Snapshot.Locale,projection,height);_flags=new(repository);
        _materials=new(repository,source,map);
        _ordinaryRenderer=new(repository,compiler);_flagRenderer=new(repository,compiler);
        _worldQueueGraphics=new(compiler);
        var graph=repository.LoadRenderGraph("system",0x024496EE).Source;
        (_graphUri,_graphSha)=(graph.ResourceUri,graph.DecodedSha256);
    }

    public static NativeMapArmyPopulationRenderer CreateForPlayableOwnedSource(Vkd3dShaderCompiler borrowedCompiler,
        GameResourceRepository repository,string sourceRevision,LoadedScenario source,LoadedMap map,
        MapWorldProjection projection,LoadedHeightField height,
        Func<NativeMapFrameContext,NativeMapArmyFrameInput> currentInput)
    {
        ArgumentNullException.ThrowIfNull(borrowedCompiler);ArgumentNullException.ThrowIfNull(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRevision);ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(map);ArgumentNullException.ThrowIfNull(projection);ArgumentNullException.ThrowIfNull(height);
        ArgumentNullException.ThrowIfNull(currentInput);
        if(source.Metadata.FormatMarker!=0x66||source.Snapshot.Locale is not("SC" or"TC"))
            throw new InvalidDataException("Native Army population requires the complete source CE scenario/locale.");
        return new(borrowedCompiler,repository,sourceRevision,source,map,projection,height,currentInput);
    }

    /// <summary>Compose these borrowed stages with the static population and
    /// other source stages. BeforeFrame must run before the first terrain await.</summary>
    public NativeMapSceneStages Stages=>new(
        AfterTerrain:async context=>{await PrepareAndDrawAsync(context);},
        BeforeTargetRetirement:BeforeTargetRetirementAsync,
        BeforeFrame:async context=>{await CaptureBeforeFrameAsync(context);});

    public Task<NativeMapArmyPreparedFrame> CaptureBeforeFrameAsync(NativeMapFrameContext context)
    {ArgumentNullException.ThrowIfNull(context);Accept();return CaptureAcceptedAsync(context);}
    private async Task<NativeMapArmyPreparedFrame> CaptureAcceptedAsync(NativeMapFrameContext context)
    {
        await _gate.WaitAsync();
        try
        {
            ValidateContext(context);
            if(_prepared.TryGetValue(context,out var retained))return retained;
            var input=_input(context)??throw new InvalidDataException("Current owned Army frame input is unavailable.");
            ArgumentNullException.ThrowIfNull(input.World);ArgumentNullException.ThrowIfNull(input.Animation);input.Animation.Validate();
            var world=input.World;
            PrepareLogicalWorld(world);
            var frame=new NativeMapArmyPreparedFrame(context,_revision,world.PeriodNumber,input.Animation,
                _ordinarySnapshot,_modelsSnapshot,_flagsSnapshot,_materialSnapshot,_unresolved);
            // Inventory publication precedes every target/GPU await. It keeps
            // both native Army families and each canonical source draw.
            if(frame.Unresolved.Count==0)frame.CpuPrimitivePopulation=_primitiveOwner.Capture(frame);
            // The parent scene serializes attempts. A prior terrain failure
            // can skip AfterTerrain; its unconsumed snapshot is now abandoned.
            _prepared.Clear();
            _nativeAdmissions.Clear();
            _worldQueuePreparation=null;_worldAcceptedPreparation=null;
            _prepared.Add(context,frame);return frame;
        }
        finally{_gate.Release();Complete();}
    }

    public async Task<ArmyCurrentPrimitivePublication> PrepareCurrentPrimitivePublicationAsync(
        NativeMapArmyPreparedFrame prepared,MapSourceCreatedLightingPacket lighting,
        MapCommonRenderState common,NativeArmyCurrentEligibility? currentEligibility=null)
    {
        ArgumentNullException.ThrowIfNull(prepared);Accept();await _gate.WaitAsync();
        try
        {
            if(!_prepared.TryGetValue(prepared.Context,out var current)||!ReferenceEquals(current,prepared)||prepared.CpuPrimitivePopulation is not{}population)
                throw new InvalidDataException("Army CPU publication requires the exact unconsumed BeforeFrame snapshot.");
            currentEligibility??=population.Actors.Count==0?
                new(prepared.Context,new Dictionary<string,bool>(),"actual-owned-Army-population-empty"):
                throw new InvalidDataException("Current Army visibility must be published before native View counters.");
            return _primitiveOwner.PrepareCurrent(population,lighting,common,currentEligibility);
        }
        finally{_gate.Release();Complete();}
    }

    public async Task<ArmyCurrentPrimitivePublication> PrepareCurrentEmptyWorldPrimitivePublicationAsync(
        NativeMapArmyPreparedFrame prepared, MapSourceCreatedLightingPacket lighting,
        MapCommonRenderState common, MapWorldKind1CurrentQueue actualWorldQueue)
    {
        ArgumentNullException.ThrowIfNull(prepared); Accept(); await _gate.WaitAsync();
        try
        {
            if (!_prepared.TryGetValue(prepared.Context, out var current) || !ReferenceEquals(current, prepared) ||
                prepared.CpuPrimitivePopulation is not { } population)
                throw new InvalidDataException("World Army publication requires this exact unconsumed BeforeFrame snapshot.");
            return _primitiveOwner.PrepareCurrentEmptyWorld(population, lighting, common, actualWorldQueue);
        }
        finally { _gate.Release(); Complete(); }
    }

    public async Task<NativeArmyCurrentAdmission> BindCurrentPrimitiveAdmissionAsync(
        NativeMapArmyPreparedFrame prepared,ArmyCurrentPrimitivePublication publication,
        MapSourceCreatedStaticPrimitiveAdmissionPlan staticAdmission)
    {
        ArgumentNullException.ThrowIfNull(prepared);Accept();await _gate.WaitAsync();
        try
        {
            if(!_prepared.TryGetValue(prepared.Context,out var current)||!ReferenceEquals(current,prepared)||prepared.CpuPrimitivePopulation is not{}population)
                throw new InvalidDataException("Army native admission requires the exact unconsumed BeforeFrame snapshot.");
            var admission=_primitiveOwner.BindCurrentFrame(population,publication,staticAdmission);
            _nativeAdmissions[prepared.Context]=admission;return admission;
        }
        finally{_gate.Release();Complete();}
    }

    // This CPU transaction is completed before any target-dependent work.
    // Source owners are shared only through immutable loaded resources; their
    // logical counters, anchors and pools are forked and committed together.
    private void PrepareLogicalWorld(GameState world)
    {
            if(!PlayableSkeleton.IsPlayableRuleset(world.RulesetId)||world.ScenarioKey!=_source.ScenarioSource.ResourceUri||
                world.MapSha256!=_map.Source.Sha256||world.ContentHashes.GetValueOrDefault(_source.ScenarioSource.ResourceUri)!=_source.ScenarioSource.Sha256||
                world.ContentHashes.GetValueOrDefault(_source.FixedDataSource.ResourceUri)!=_source.FixedDataSource.Sha256)
                throw new InvalidDataException("Current Army population differs from its coherent source revision; retire and rebuild the source owner.");
            if(!ReferenceEquals(world,_lastWorld))
            {
                var ordinaryCandidate=_ordinary.ForkLogicalState();var flagCandidate=_flags.ForkLogicalState();
                try
                {
                    var plan=PrepareCurrentPosePlan(_adapter,ordinaryCandidate,world);
                    flagCandidate.Prepare(ordinaryCandidate.Frames);
                    var sourceMaterials=flagCandidate.Frames.Count==0?null:_materials.Prepare(world,flagCandidate.Frames.Keys);
                    var ordinary=new ReadOnlyDictionary<int,RemakeArmyVisualFrame>(ordinaryCandidate.Frames.ToDictionary(p=>p.Key,p=>p.Value));
                    var models=new ReadOnlyDictionary<int,LoadedArmyModel>(ordinaryCandidate.Models.ToDictionary(p=>p.Key,p=>p.Value));
                    var flags=new ReadOnlyDictionary<int,RemakeArmyFlagFrame>(flagCandidate.Frames.ToDictionary(p=>p.Key,p=>p.Value));
                    // Commit RNG counters, pose initialization, pool identities
                    // and every exported field together. A rejected capture
                    // cannot influence the next successful heading/reset.
                    _ordinary.Dispose();_flags.Dispose();_ordinary=ordinaryCandidate;_flags=flagCandidate;
                    (_ordinarySnapshot,_modelsSnapshot,_flagsSnapshot,_materialSnapshot,_unresolved,_lastWorld)=
                        (ordinary,models,flags,sourceMaterials,Array.AsReadOnly(plan.Unresolved.ToArray()),world);
                }
                catch{ordinaryCandidate.Dispose();flagCandidate.Dispose();throw;}
            }
    }

    /// <summary>Complete source-created logical inventory before target
    /// construction. Bootstrap and the later BeforeFrame capture share these
    /// committed pose/pool identities; no GPU count supplies this inventory.</summary>
    public async Task<IReadOnlyList<NativeArmyWorldLogicalActor>> CaptureActualLogicalInventoryAsync(GameState actualWorld)
    {
        ArgumentNullException.ThrowIfNull(actualWorld);Accept();await _gate.WaitAsync();
        try
        {
            PrepareLogicalWorld(actualWorld);
            if(_unresolved.Count!=0)throw new InvalidDataException("Bootstrap Army source pose/pool inventory is incomplete: "+
                string.Join(";",_unresolved.Select(x=>x.ToString())));
            return _worldCpuPublication.CaptureActualLogicalInventory(actualWorld,_ordinarySnapshot,_modelsSnapshot,_flagsSnapshot);
        }
        finally{_gate.Release();Complete();}
    }

    // Select source descriptors once, then bind the actual prepared poses.
    // Binding replaces missing-pose placeholders while retaining source failures.
    internal static PlayableArmyVisualPlan PrepareCurrentPosePlan(
        PlayableArmyVisualAdapter adapter,RemakeArmySceneVisuals ordinaryCandidate,GameState world)
    {
        var descriptors=adapter.Resolve(world);
        var poses=ordinaryCandidate.Prepare(world,descriptors);
        return adapter.BindPoses(world,descriptors,poses);
    }

    public Task<NativeMapArmyPopulationAcceptedFrame> PrepareAndDrawAsync(NativeMapFrameContext context)
    {ArgumentNullException.ThrowIfNull(context);Accept();return DrawAcceptedAsync(context);}
    private async Task<NativeMapArmyPopulationAcceptedFrame> DrawAcceptedAsync(NativeMapFrameContext context)
    {
        await _gate.WaitAsync();
        try
        {
            ValidateContext(context);
            if(!_prepared.TryGetValue(context,out var frame))
                throw new InvalidDataException("Native Army population requires its exact BeforeFrame snapshot; no later current-state fallback is admitted.");
            _nativeAdmissions.TryGetValue(context,out var nativeAdmission);
            await _ordinaryRenderer.RenderAsync(context,frame.Ordinary,frame.Models,frame.Animation.CallerSeconds,nativeAdmission);
            await _flagRenderer.RenderAsync(context,frame.Flags,frame.SourceMaterials is{}materials?materials.Select:null,frame.Animation.CallerSeconds,nativeAdmission);
            _acceptedFrames++;LastFailure=null;
            var statuses=Array.AsReadOnly(_flagRenderer.Statuses.ToArray());
            var statistics=new NativeMapArmyPopulationStatistics(_acceptedFrames,_failedFrames,frame.OrdinaryActors,frame.FlagActors,
                _ordinaryRenderer.DrawCount,_flagRenderer.DrawCount,_ordinaryRenderer.ProgramCount+_flagRenderer.ProgramCount,
                _ordinaryRenderer.GeometryCount+_flagRenderer.GeometryCount,_ordinaryRenderer.DrawCreationCount+_flagRenderer.DrawCreationCount,
                statuses.Count(s=>!s.Submitted));
            var accepted=new NativeMapArmyPopulationAcceptedFrame(frame,statuses,statistics)
            {
                CurrentViewFamilyCandidates=_ordinaryRenderer.CurrentViewFamilyCandidates is { } ordinaryCandidates&&
                    _flagRenderer.CurrentViewFamilyCandidates is { } flagCandidates?
                    Array.AsReadOnly(ordinaryCandidates.Concat(flagCandidates).ToArray()):null,
                OrdinaryGeometrySubmission=_ordinaryRenderer.LastGeometrySubmission,
                FlagGeometrySubmission=_flagRenderer.LastGeometrySubmission,
                NativePrimitiveAdmission=nativeAdmission
            };
            nativeAdmission?.ValidateCurrentAccepted(accepted);
            return LastAcceptedFrame=accepted;
        }
        catch(Exception error){_failedFrames++;LastFailure=error.Message;throw;}
        finally{_prepared.Remove(context);_nativeAdmissions.Remove(context);_gate.Release();Complete();}
    }

    private void ValidateContext(NativeMapFrameContext context)
    {
        if(context.FrameNumber<=0||context.PreparedCamera is null||context.TerrainFrame.ColorViews.Count!=5||!context.TerrainFrame.DepthView.IsValid||
            context.TerrainFrame.Source.Graph.Source.ResourceUri!=_graphUri||context.TerrainFrame.Source.Graph.Source.DecodedSha256!=_graphSha)
            throw new InvalidDataException("Native Army population requires the same accepted source Camera and complete original five-MRT/depth graph.");
        _=NativeMatrixMath.Invert(context.PreparedCamera.SourceWorldToView,out _);
        _=NativeMatrixMath.Invert(context.PreparedCamera.SourceWorldToProjection,out _);
    }

    public Task BeforeTargetRetirementAsync(NativeMapTargetContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock(_lifetime){if(_retiring)return DisposeAsync().AsTask();_operations++;}
        return RetireAcceptedAsync();
    }
    private async Task RetireAcceptedAsync()
    {
        await _gate.WaitAsync();
        try{await _worldQueueGraphics.RetireTargetAsync();_worldQueuePreparation=null;_worldAcceptedPreparation=null;await _ordinaryRenderer.RetireTargetAsync();await _flagRenderer.RetireTargetAsync();_prepared.Clear();_nativeAdmissions.Clear();LastAcceptedFrame=null;}
        finally{_gate.Release();Complete();}
    }
    private void Accept(){lock(_lifetime){ObjectDisposedException.ThrowIf(_retiring,this);_operations++;}}
    private void Complete(){lock(_lifetime){_operations--;if(_retiring&&_operations==0)_drained?.TrySetResult();}}
    public ValueTask DisposeAsync()
    {
        lock(_lifetime)
        {
            _retiring=true;
            if(_disposal is null||_disposal.IsFaulted||_disposal.IsCanceled)_disposal=DisposeCoreAsync();
            return new(_disposal);
        }
    }
    private async Task DisposeCoreAsync()
    {
        Task pending;lock(_lifetime)pending=_operations==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        await pending;
        _finalRetirement??=new("Army-population-retire/"+Guid.NewGuid().ToString("N"),
            _worldQueueGraphics,_ordinaryRenderer,_flagRenderer);
        await _finalRetirement.DisposeAsync();
        _worldQueuePreparation=null;_worldAcceptedPreparation=null;
        _ordinary.Dispose();_flags.Dispose();_prepared.Clear();_nativeAdmissions.Clear();_lastWorld=null;_materialSnapshot=null;LastAcceptedFrame=null;
        _ordinarySnapshot=new ReadOnlyDictionary<int,RemakeArmyVisualFrame>(new Dictionary<int,RemakeArmyVisualFrame>());
        _modelsSnapshot=new ReadOnlyDictionary<int,LoadedArmyModel>(new Dictionary<int,LoadedArmyModel>());
        _flagsSnapshot=new ReadOnlyDictionary<int,RemakeArmyFlagFrame>(new Dictionary<int,RemakeArmyFlagFrame>());_unresolved=[];
    }
}
