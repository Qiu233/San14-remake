using System.Collections.Concurrent;
using San14.Data;

/// <summary>Actual World factory dependencies. A Host borrows these owners;
/// the package retires that Host before releasing geometry, Sky and lighting.</summary>
public sealed record SceneViewNativeMapWorldDependencies(
    Vkd3dShaderCompiler Compiler, MapSourceCreatedDirectionalLightingDeviceOwner Lighting,
    SceneViewNativeMapWorldSkySourceOwner Sky,
    NativeMapModelPopulationRenderer Population, NativeMapArmyPopulationRenderer Army,
    MapSourceCreatedPlayableSceneSource SceneSource, MapMaterialCachePrepared Materials,
    SceneViewNativeMapWorldF103SourceOwner WorldSource,
    MapSourceCreatedWorldPlayableCurrentSources Current,
    Func<SceneViewNativeMapFrameCapture, MapSourceCreatedPlayableTerrainFrame> PrepareTerrainFrame,
    Func<SceneViewNativeMapFrameCapture, SceneViewNativeMapResizeInputs> PrepareResize,
    Action<NativeMapFrameContext> CommitAcceptedFrame) : IAsyncDisposable
{
    public NativeMapSceneStages? Preceding { get; init; }
    public WorldGeometryStageLifetimeOwner? GeometryLifetime { get; init; }
    public SceneViewNativeMapWorldGeometrySourceOwner? GeometrySource { get; init; }
    public MapSourceCreatedWorldPlayableCurrentSources? OrdinaryCurrent { get; init; }
    public MapExposureOwner? Exposure { get; init; }
    public Func<NativeMapFrameContext,SceneViewNativeMapFrameCapture>? CaptureCurrentFrame { get; init; }
    /// <summary>Exact node1 source installer policy. A caller-provided
    /// resolvedTargets argument is an explicit diagnostic allocation override
    /// and does not inherit this policy's native-source proof.</summary>
    public MapSceneProxySourcePolicy? SourceSceneProxyPolicy { get; init; }
    private readonly SemaphoreSlim gate = new(1, 1);
    private int retiredStage;
    private bool hostIssued,hostRetired;
    private IAsyncDisposable? failedHostBorrowers;

    public async Task<SceneViewNativeMapWorldPackage> BuildHostAsync(
        SceneViewNativeMapFrameCapture firstCapture, bool allowReadback = false,
        Action<DeferredEarlySourcePixelFactory>? configureEarlyDiagnostics = null,
        RenderTextureResolutionFrame? resolvedTargets = null,
        Func<MapSourceCreatedWorldPlayableHost,Task>? prepareFirstWorldConsumers = null,
        Func<MapSourceCreatedWorldPlayableHost,NativeMapFrameContext,
            MapFinalOutputProductionPrepared,Task>? afterWorldExposureBeforeFinal = null)
    {
        ArgumentNullException.ThrowIfNull(firstCapture);
        await gate.WaitAsync();
        SceneViewNativeMapWorldPackage package;
        MapPostEffect3WorldRenderLoop? ordinaryLoop=null;
        try
        {
            if(retiredStage!=0 || hostIssued)
                throw new InvalidOperationException("World dependency generation can build one Host before retirement.");
            hostIssued=true;
            try
            {
                if(prepareFirstWorldConsumers is null&&afterWorldExposureBeforeFinal is null)
                    ordinaryLoop=new(this,CaptureCurrentFrame??
                        throw new InvalidDataException("Ordinary World renderer has no owned current SceneView capture producer."));
                var host=await MapSourceCreatedWorldPlayableHost.BuildAsync(Repository,
                    Compiler,SceneSource,Lighting,Sky.Device,Population,Army,Materials,
                    WorldSource.Bootstrap.First,WorldSource.BootstrapEarly,ordinaryLoop is null ? Current : OrdinaryCurrent??Current,PrepareCapturedTerrainFrame(firstCapture),
                    Preceding,Exposure,allowReadback,configureEarlyDiagnostics,resolvedTargets,prepareFirstWorldConsumers,
                    afterWorldExposureBeforeFinal,resolvedTargets is null ? SourceSceneProxyPolicy : null,ordinaryLoop);
                package=new SceneViewNativeMapWorldPackage(host,this);
            }
            catch(Exception failure)
            {
                failedHostBorrowers=failure is NativeMapBorrowerConstructionException incomplete
                    ? incomplete.RetainedPackage : null;
                hostRetired=failedHostBorrowers is null;
                if(hostRetired)GeometrySource?.ReleaseAfterHostRetirement();
                throw;
            }
        }
        finally { gate.Release(); }
        try { package.CommitAcceptedOutput(); return package; }
        catch { await package.DisposeAsync(); throw; }
    }

    // Repository identity is owned by the factory, not recovered from paths
    // embedded in any retained report or from a running original process.
    public required GameResourceRepository Repository { get; init; }
    internal void NotifyHostRetired()=>hostRetired=true;

    internal MapSourceCreatedPlayableTerrainFrame PrepareCapturedTerrainFrame(
        SceneViewNativeMapFrameCapture capture)
    {
        var request=PrepareTerrainFrame(capture)??throw new InvalidDataException("World captured terrain producer returned no frame.");
        if(request.Camera!=PreparedCameraSceneSource.ReadCompletePrefix(capture.MainCamera.Camera.Prefix.Span)||
            request.Batches.Count!=capture.TerrainBatches.Count||request.Batches.Where((batch,index)=>
                batch.Coarse!=capture.TerrainBatches[index].Coarse||
                !batch.Patches.SequenceEqual(capture.TerrainBatches[index].Patches)).Any())
            throw new InvalidDataException("World producer changed its exact captured Camera or source terrain patch set.");
        return request;
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            if(failedHostBorrowers is not null)
            {
                await failedHostBorrowers.DisposeAsync();failedHostBorrowers=null;hostRetired=true;
                GeometrySource?.ReleaseAfterHostRetirement();
            }
            if(hostIssued&&!hostRetired)
                throw new InvalidOperationException("Retire the World Host package before its borrowed dependencies.");
            if(retiredStage==0)
            {
                if(GeometrySource is not null)await GeometrySource.DisposeAsync();
                else if(GeometryLifetime is not null)await GeometryLifetime.DisposeAsync();
                await Army.DisposeAsync();retiredStage=1;
            }
            if(retiredStage==1){await Population.DisposeAsync();retiredStage=2;}
            if(retiredStage==2){await Sky.DisposeAsync();retiredStage=3;}
            if(retiredStage==3){if(Exposure is not null)await Exposure.DisposeAsync();retiredStage=4;}
            if(retiredStage==4){await Lighting.DisposeAsync();retiredStage=5;}
            if(retiredStage==5){Compiler.Dispose();retiredStage=6;}
        }
        finally { gate.Release(); }
    }
}

public delegate Task<SceneViewNativeMapWorldDependencies> SceneViewNativeMapWorldDependencyFactory(
    SceneViewNativeMapLoadInputs loaded,
    Func<NativeMapFrameContext,SceneViewNativeMapFrameCapture> captureAcceptedFrame);

/// <summary>Owns the complete accepted World Host and its borrowed dependency
/// generation. Source-time commits follow accepted presentation, never merely
/// a successful Sky or geometry substage.</summary>
public sealed class SceneViewNativeMapWorldPackage : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string,SceneViewNativeMapWorldPackage> failed=new();
    private readonly SemaphoreSlim gate=new(1,1);
    private bool retiring,hostRetired,dependenciesRetired;
    private NativeMapFrameContext? committed;
    public string OwnerIdentity { get; }="World-Host-package:"+Guid.NewGuid().ToString("N");
    public MapSourceCreatedWorldPlayableHost Host { get; }
    public SceneViewNativeMapWorldDependencies Dependencies { get; }
    public NativeMapSurface Surface=>Host.Surface;
    public static IReadOnlyList<SceneViewNativeMapWorldPackage> FailedRetirements=>failed.Values.ToArray();
    internal SceneViewNativeMapWorldPackage(MapSourceCreatedWorldPlayableHost host,
        SceneViewNativeMapWorldDependencies dependencies)=>(Host,Dependencies)=(host,dependencies);

    internal void CommitAcceptedOutput()
    {
        var accepted=Host.Scene.LastCompletedContext??throw new InvalidDataException("World Host has no complete native frame.");
        if(!Host.HasAcceptedOutput || !ReferenceEquals(Host.Session.CurrentFrame?.Context,accepted) ||
            !Host.HasCompletedCurrentFinal(accepted) || ReferenceEquals(committed,accepted))
            throw new InvalidDataException("World source-time commit requires a new fully accepted Host presentation.");
        Dependencies.CommitAcceptedFrame(accepted);
        Dependencies.GeometrySource?.CompleteActualFrameResourceLifetime(accepted);committed=accepted;
    }

    public async Task RenderFrameAsync(SceneViewNativeMapFrameCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);await gate.WaitAsync();
        bool attempted=false;
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var terrain=Dependencies.PrepareCapturedTerrainFrame(capture);attempted=true;
            await Host.RenderFrameAsync(terrain);
            CommitAcceptedOutput();
        }
        catch
        {
            if(attempted && Dependencies.GeometrySource is {} geometry)await geometry.AbortFailedFrameAsync(Host);
            throw;
        }
        finally { gate.Release(); }
    }

    /// <summary>Between fully accepted requests, replace only the loaded
    /// seasonal asset set. Keeps this Host/Scene/Surface, current Main/View,
    /// clocks, Sky work/phases and post module/Bank/history alive. Actual
    /// Main resource-refresh and next-frame submission remain caller work.</summary>
    public async Task<SceneViewNativeMapWorldSeasonReplacement> InstallSeasonResourcesAsync(
        SceneViewNativeMapFrameCapture actualCapture)
    {
        ArgumentNullException.ThrowIfNull(actualCapture);await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(committed is null||!ReferenceEquals(Host.Scene.LastCompletedContext,committed)||
                !Host.HasCompletedCurrentFinal(committed))
                throw new InvalidDataException("Season resources can replace only after this same Host's complete source output and request drained.");
            var next=await SceneViewNativeMapWorldSeasonReplacement.PrepareAsync(Dependencies,actualCapture,
                Dependencies.WorldSource.RequireCurrent(committed).First.Context);
            next.Install(Dependencies);return next;
        }
        finally{gate.Release();}
    }

    public async Task ResizeTargetsAsync(SceneViewNativeMapFrameCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);await gate.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);var next=Dependencies.PrepareResize(capture);
            await Host.ResizeTargetsAsync(next.TerrainExtents,next.OutputExtents);
        }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            retiring=true;
            if(!hostRetired){await Host.DisposeAsync();hostRetired=true;Dependencies.NotifyHostRetired();}
            if(!dependenciesRetired){await Dependencies.DisposeAsync();dependenciesRetired=true;}
            failed.TryRemove(OwnerIdentity,out _);
        }
        catch { failed.TryAdd(OwnerIdentity,this);throw; }
        finally { gate.Release(); }
    }
}

/// <summary>Strongly owns incomplete factory generations after a failed
/// teardown. A retry resumes the first failed borrower before dependencies.</summary>
public sealed class SceneViewNativeMapWorldPartialRetirementPackage : IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string,SceneViewNativeMapWorldPartialRetirementPackage> failed=new();
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly IAsyncDisposable?[] borrowers;
    private Vkd3dShaderCompiler? compiler;
    private int stage;
    public string OwnerIdentity { get; }
    public static IReadOnlyList<SceneViewNativeMapWorldPartialRetirementPackage> FailedPackages=>failed.Values.ToArray();
    internal SceneViewNativeMapWorldPartialRetirementPackage(string owner,Vkd3dShaderCompiler? compiler,
        params IAsyncDisposable?[] borrowers)=>(OwnerIdentity,this.compiler,this.borrowers)=(owner,compiler,borrowers);
    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try
        {
            while(stage<borrowers.Length){if(borrowers[stage] is { } owner)await owner.DisposeAsync();++stage;}
            if(stage==borrowers.Length){compiler?.Dispose();compiler=null;++stage;}
            failed.TryRemove(OwnerIdentity,out _);
        }
        catch { failed.TryAdd(OwnerIdentity,this);throw; }
        finally { gate.Release(); }
    }
}

public sealed class SceneViewNativeMapWorldConstructionException : InvalidOperationException
{
    public Exception ConstructionFailure { get; }
    public Exception RetirementFailure { get; }
    public SceneViewNativeMapWorldPartialRetirementPackage RetainedPackage { get; }
    internal SceneViewNativeMapWorldConstructionException(Exception construction,Exception retirement,
        SceneViewNativeMapWorldPartialRetirementPackage package)
        :base("World factory construction failed; partial owners remain retained for ordered retirement retry.",construction)
        =>(ConstructionFailure,RetirementFailure,RetainedPackage)=(construction,retirement,package);
}
