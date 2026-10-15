using Godot;
using San14.Data;

/// <summary>One live controlled F1 module, independently of a World frame.
/// Retains real physical resources and source CPU handlers. Managed scope
/// restoration is separate from unobserved original backend raw bytes.</summary>
public sealed class MapPostEffect3WorldControlledModuleOwner:IAsyncDisposable
{
    private readonly GameResourceRepository repository;
    private readonly bool persistent;
    private MapPostEffect3WorldControlledFactoryFrame? current,previous;
    private MapPostEffect3WorldControlledPortReturnReceipt? returned;
    private readonly Dictionary<long,MapPostEffect3WorldControlledPortReturnReceipt> returns=[];
    private MapPostEffect3DepthOfField2ModuleResources? dof;
    private MapPostEffect3Functor1GhostRegistryOwner? ghost;
    private bool disposed,failed,retiring;
    private readonly SemaphoreSlim retirement=new(1,1);
    private readonly List<IAsyncDisposable> invocationOwners=[];
    private NativeMapBorrowerRetirementPackage? invocationRetirement;
    internal NativeMapBorrowerRetirementPackage? PendingInvocationRetirement=>invocationRetirement;
    internal MapPostEffect3WorldControlledStaticSources StaticSources{get;}
    internal MapPostEffect3Functor1FrontendCacheOwner Cache{get;}
    public PostProcessResourceBank Bank{get;}
    public MapPostEffect3Functor1ModuleState CpuModule{get;private set;}=null!;
    public MapPostEffect3ControlledApplyHistoryState ApplyHistory{get;private set;}=null!;
    public MapPostEffect3SourceSamplerOwner Manager{get;private set;}=null!;
    public MapPostEffect3WorldControlledPortReturnReceipt? LastReturned=>returned;
    public bool RetainsAcrossFrames=>persistent;
    internal uint RetainedContextWord1C48{get;private set;}
    internal uint RetainedDepthCompare1C44{get;private set;}
    internal uint RetainedMatchingDepthTargetCount1C40{get;private set;}
    internal MapPostEffect3WorldControlledModuleOwner(GameResourceRepository repo,
        PostProcessResourceBank bank,bool persistent,string locale)
    {
        repository=repo;Bank=bank;this.persistent=persistent;
        StaticSources=MapPostEffect3WorldControlledStaticSources.Load(repo,locale);
        Cache=new(StaticSources.Cursor);
    }
    internal async Task InitializeAsync(MapPostEffect3OwnedControlledCameraFrame camera,
        MapPostEffect3FreshOwnedAllocationSides sides)
    {
        RequireLive();
        if(camera.CaptureAcceptedCamera().ExecutableLocale!=StaticSources.Native.Locale)throw new InvalidDataException("Controlled module needs the same executable locale as its Camera.");
        if(CpuModule is not null)throw new InvalidOperationException("Controlled module constructor cannot run twice.");
        var dofSource=StaticSources.DepthOfField;
        var eye=StaticSources.Eye;
        CpuModule=MapPostEffect3Functor1ModuleState.ConstructFreshOwned(Bank.Source,sides,
            StaticSources.BeginCounter,StaticSources.Exit,
            dofSource.ConstructFreshController(camera.State,Bank.Source),eye.ConstructFreshController(Bank.Source.Source,Bank.Source.ContextOwnerIdentity));
        ApplyHistory=StaticSources.ApplyHistory.ConstructFreshOwned(CpuModule);
        var nativeFrontend=StaticSources.NativeFrontend;
        RetainedContextWord1C48=BitConverter.SingleToUInt32Bits(nativeFrontend.Fresh.FreshContext1C48);
        RetainedDepthCompare1C44=nativeFrontend.OrdinaryBindNativeDepthCompare;
        RetainedMatchingDepthTargetCount1C40=nativeFrontend.Fresh.FreshMatchingDepthTargetCount;
        Manager=await MapPostEffect3SourceSamplerOwner.BuildAsync(
            StaticSources.Native,Bank.Source.ContextOwnerIdentity);
        ghost=await MapPostEffect3Functor1GhostRegistryOwner.CreateAsync(camera,
            StaticSources.Lifetime);
    }
    internal async Task BeginGhostRegistryAsync(MapPostEffect3OwnedControlledCameraFrame camera)
    {RequireLive();await (ghost??throw new InvalidDataException("Actual controller828 owner absent.")).AssignCurrentAsync(camera);}
    internal Godot.Rid GhostStorageRid=>ghost?.StorageRid??default;
    internal object GhostIdentity=>ghost?.ControllerIdentity??throw new InvalidDataException("Controller828 owner absent.");
    internal void RequireGhostCurrent(MapPostEffect3OwnedControlledCameraFrame camera)
    {RequireLive();ghost!.RequireAssigned(camera);}
    internal void Attach(MapPostEffect3WorldControlledFactoryFrame frame)
    {
        RequireLive();
        if(current is not null||!ReferenceEquals(frame.CpuModule,CpuModule)||!ReferenceEquals(frame.World.Bank,Bank))
            throw new InvalidDataException("Controlled module already has an invocation or received foreign owners.");
        current=frame;
        MapPostEffect3Functor1HistoryWindow.Prune(returns,frame.Camera.FrameNumber);
    }
    internal MapPostEffect3DepthOfField2ModuleResources AcquireDof(
        MapPostEffect3WorldControlledFactoryFrame frame,MapPostEffect3ProgramLibrary library)
    {
        RequireFrame(frame);dof??=MapPostEffect3DepthOfField2ModuleResources.CreateBorrowed(library,Bank,CpuModule);
        if(!ReferenceEquals(dof.Library,library))throw new InvalidDataException("Retained module cannot replace its actual program library.");
        return dof;
    }
    internal MapPostEffect3DepthOfField2ExternalExtent CaptureRetainedShapeExtent(
        MapPostEffect3Functor1ModuleInvocation invocation,MapPostEffect3WorldStockGraphicsSourceOwner world)
    {
        _=RequirePreviousFrontendReturn(invocation,world);
        return (dof??throw new InvalidDataException("Retained58 has no actual earlier DOF module resource owner."))
            .CaptureCurrentShapeExtent(invocation,world.TextureParameters);
    }
    internal MapPostEffect3WorldControlledFactoryFrame RequirePreviousForNext(
        MapPostEffect3WorldControlledPreparation.CurrentFrame next)
    {
        RequireLive();
        if(!persistent||current is not null||previous is null||returned is null||!previous.CpuInvocation.IsControllerReturned||
            next.FrameNumber!=previous.Camera.FrameNumber+1||!ReferenceEquals(next.Allocation,Bank.Source))
            throw new InvalidDataException("Next controlled invocation requires its disposed frame and exact preceding closed managed module return.");
        returned.RequireHistoricalAuthority();return previous;
    }
    /// <summary>Use the same Preparation clock and actual Host/Bank. The
    /// unchanged current optics profile preserves AF/A1 and consumes the real
    /// preceding Apply bit23 postwriter. Changed optics keep their source gate.</summary>
    public async Task<MapPostEffect3WorldControlledFactoryFrame> BuildNextFrameAsync(
        MapSourceCreatedWorldPlayableHost host,MapPostEffect3WorldControlledPreparation.CurrentFrame next,
        MapPostEffect3StaticLutTextureOwner hdr)
    {
        var prior=RequirePreviousForNext(next);
        hdr.RequireCurrent();
        var selected=hdr.CaptureSourceSelectedHdrMetadata();
        if(!ReferenceEquals(selected.Lookup,next.HdrLookup)||selected.View.FrameNumber!=next.FrameNumber)
            throw new InvalidDataException("Next selected HDR wrapper is not this actual current source/device owner.");
        try
        {
            byte argument5=ApplyHistory.ReadNextControllerArgument5(CpuModule,prior.Camera.FrameNumber);
            var camera=next.PrepareActualRetained(selected,prior.Camera,CpuModule,argument5,ApplyHistory.SourceEvidence);
            return await MapPostEffect3WorldControlledFactoryFrame.BuildRetainedAsync(repository,host,next,camera,hdr,this,prior);
        }
        catch{AbortUndeliveredConstruction(next.FrameNumber);throw;}
    }
    internal void RequireFrame(MapPostEffect3WorldControlledFactoryFrame frame)
    {RequireLive();if(!ReferenceEquals(current,frame))throw new InvalidDataException("Controlled invocation is not this actual module's current borrow.");}
    internal MapPostEffect3WorldControlledPortReturnReceipt RequirePreviousFrontendReturn(
        MapPostEffect3Functor1ModuleInvocation invocation,
        MapPostEffect3WorldStockGraphicsSourceOwner world)
    {
        RequireLive();invocation.RequireOwnedModule(CpuModule);invocation.RequireCurrentTextureTable(world.TextureParameters);
        if(!persistent||!returns.TryGetValue(invocation.FrameNumber-1,out var actual)||
            !ReferenceEquals(world.Bank,Bank)||!ReferenceEquals(actual.Scope.Invocation,actual.Invocation))
            throw new InvalidDataException("Retained frontend requires the same module's immediately preceding closed actual return.");
        actual.RequireHistoricalAuthority();
        return actual;
    }
    private void RequireLive()
    {ObjectDisposedException.ThrowIf(disposed||retiring,this);Bank.RequireTexture(61,0);if(failed)throw new InvalidOperationException("Controlled module contains an unreturned failure.");}
    internal void Detach(MapPostEffect3WorldControlledFactoryFrame frame)
    {
        if(!ReferenceEquals(current,frame))throw new InvalidDataException("Foreign controlled frame cannot release this module.");
        if(returned is null||returned.FrameNumber!=frame.Camera.FrameNumber)failed=true;
        previous=frame;current=null;
    }
    internal void MarkFailure()=>failed=true;
    internal T OwnInvocation<T>(MapPostEffect3WorldControlledFactoryFrame frame,T owner) where T:IAsyncDisposable
    {
        retirement.Wait();
        try
        {
            RequireFrame(frame);
            if(invocationRetirement is not null)throw new InvalidOperationException("Previous invocation retirement must finish before accepting another owner.");
            invocationOwners.Add(owner);return owner;
        }
        finally{retirement.Release();}
    }
    internal void RetainConstructionFailure(MapPostEffect3WorldControlledFactoryFrame frame,
        NativeMapBorrowerConstructionException failure)
    {
        retirement.Wait();
        try
        {
            if(!ReferenceEquals(current,frame)||invocationRetirement is not null)
                throw new InvalidOperationException("Construction cleanup belongs to the active invocation before retirement.");
            invocationOwners.Add(failure.RetainedPackage);
        }
        finally{retirement.Release();}
    }
    internal async Task RetireInvocationOwnerAsync(IAsyncDisposable owner)
    {
        await retirement.WaitAsync();
        try
        {
            if(invocationRetirement is not null||invocationOwners.Count==0||!ReferenceEquals(invocationOwners[^1],owner))
                throw new InvalidOperationException("Invocation consumers must retire before their dependencies.");
            await owner.DisposeAsync();invocationOwners.RemoveAt(invocationOwners.Count-1);
        }
        finally{retirement.Release();}
    }
    internal async Task RetireInvocationOwnersAsync()
    {
        await retirement.WaitAsync();
        try{await RetireInvocationOwnersCoreAsync();}
        finally{retirement.Release();}
    }
    private async Task RetireInvocationOwnersCoreAsync()
    {
        if(invocationOwners.Count==0&&invocationRetirement is null)return;
        invocationRetirement??=new("Post-module-invocation-retire/"+Guid.NewGuid().ToString("N"),
            [..invocationOwners.AsEnumerable().Reverse()]);
        invocationRetirement=await NativeMapBorrowerRetirementPackage.DrainPendingAsync(invocationRetirement);
        invocationOwners.Clear();
    }
    internal void AbortUndeliveredConstruction(long frameNumber)
    {
        //Construction has submitted no device command. Cancel only this
        //undelivered borrow/empty ledger; source Return/flip/A1 stay untouched.
        failed=true;
        if(current is not null)
        {
            if(current.Camera.FrameNumber!=frameNumber)
                throw new InvalidDataException("Construction abort cannot detach another active invocation.");
            current=null;
        }
        if(CpuModule is not null)
            MapPostEffect3Functor1SubmissionLedger.AbortUndeliveredFrame(CpuModule,frameNumber);
    }
    internal async Task AbortConstructionAsync()
    {failed=true;current=null;await DisposeAsync();}
    public MapPostEffect3WorldControlledPortReturnReceipt CompletePortLifecycle(
        MapPostEffect3WorldControlledFactoryFrame frame,MapPostEffect3Functor1DirectoryReturnedReceipt directory)
    {
        RequireFrame(frame);frame.RequireActualDirectoryReturned(directory);frame.Frontend.RequireNoPendingForDirectoryReturn();
        directory.SelectedDeviceJoin.RequireActualReceipts();frame.SubmissionLedger.RequireHistoricalAuthority();
        if(returned?.FrameNumber==frame.Camera.FrameNumber)throw new InvalidOperationException("Module exit is once only.");
        try
        {
            var exposure=Bank.CaptureRetainedControlledExposure(frame,directory);
            var source=CpuModule.ExitSource;
            var finish=frame.CpuInvocation.ApplyFinishCandidate(source.CaptureFinishCandidate(directory.CpuDirectoryReturned));
            //Each actual list returned and retired its temporary sets/views
            //before the directory/history join. This restores the owned port
            //scope; it does not invent native backend bytes or a GPU fence.
            var scope=frame.Frontend.EndManagedModule(Cache);
            var end=frame.CpuInvocation.RecordFrontendEnd(source.CaptureFrontendEnd(finish));
            var flip=frame.CpuInvocation.ApplyHistoryFlip(end);
            frame.Frontend.RestoreManagedOuterScope(scope);
            var outer=frame.CpuInvocation.RecordOuterRestoration(source.CaptureOuterRestoration(flip));
            ghost!.CleanupReturned(frame.Camera);
            var controller=frame.CpuInvocation.ApplyControllerReturnAfterSourceCleanup(source.CaptureControllerCleanup(outer));
            ApplyHistory.ApplyReturnedSingleMainCall(controller,frame.Camera);
            returned=new(this,frame,directory,controller,scope,exposure);returns.Add(returned.FrameNumber,returned);
            MapPostEffect3Functor1HistoryWindow.Prune(returns,returned.FrameNumber);
            return returned;
        }
        catch{failed=true;throw;}
    }
    internal void RequireReturnedHistory(MapPostEffect3WorldControlledPortReturnReceipt receipt)
    {
        MapPostEffect3Functor1HistoryWindow.RequireOwned(returns,receipt.FrameNumber,receipt);
        if(!receipt.Invocation.IsControllerReturned)
            throw new InvalidDataException("Managed return is outside this module's retained actual authority window.");
        receipt.Ledger.RequireHistoricalAuthority();
    }
    internal IMapPostEffect3ComputeMipTextureLease CapturePriorExposure(
        MapPostEffect3WorldControlledFactoryFrame frame,int pass,object consumer,
        MapPostEffect3ShaderReadFootprint? footprint=null)
    {
        RequireFrame(frame);
        if(returned is null||returned.FrameNumber>=frame.Camera.FrameNumber)
            throw new InvalidDataException("Current frame has no closed preceding actual exposure writer.");
        returned.RequireHistoricalAuthority();
        return Bank.CaptureRetainedControlledExposureRead(frame,returned.Exposure,pass,consumer,footprint);
    }
    public async ValueTask DisposeAsync()
    {
        await retirement.WaitAsync();
        try
        {
            if(disposed)return;
            await RetireInvocationOwnersCoreAsync();
            if(current is not null)throw new InvalidOperationException("Dispose the active controlled frame before its module.");
            retiring=true;
            // Retire consumers before their dependencies. On failure keep
            // the failed owner and all later dependencies for a retry; an
            // early disposed flag must not hide a partially live generation.
            if(dof is not null){await dof.DisposeAsync();dof=null;}
            if(ghost is not null){await ghost.DisposeAsync();ghost=null;}
            if(Manager is not null){await Manager.DisposeAsync();Manager=null!;}
            CpuModule?.Dispose();StaticSources.Retire();disposed=true;
        }
        finally{retirement.Release();}
    }
}

/// <summary>Closed port lifetime, source CPU exit and actual historical
/// submissions. Does not claim equality of unavailable original raw fields.</summary>
public sealed class MapPostEffect3WorldControlledPortReturnReceipt
{
    private readonly MapPostEffect3WorldControlledModuleOwner owner;
    internal readonly MapPostEffect3Functor1ModuleInvocation Invocation;
    internal readonly MapPostEffect3Functor1SubmissionLedger Ledger;
    internal readonly MapPostEffect3RetainedControlledExposureSnapshot Exposure;
    internal MapPostEffect3WorldControlledPortReturnReceipt(MapPostEffect3WorldControlledModuleOwner owner,
        MapPostEffect3WorldControlledFactoryFrame frame,MapPostEffect3Functor1DirectoryReturnedReceipt directory,
        MapPostEffect3Functor1SourceReceipt controller,MapPostEffect3ManagedModuleScope scope,
        MapPostEffect3RetainedControlledExposureSnapshot exposure)
    {
        this.owner=owner;Invocation=frame.CpuInvocation;Ledger=frame.SubmissionLedger;Exposure=exposure;
        FrameNumber=frame.Camera.FrameNumber;SourceControllerReturn=controller;Scope=scope;
        HistoricalSubmissionCount=directory.SelectedDeviceJoin.ActualReceipts.Count;
        ApplyHistoryBit23AfterReturn=owner.ApplyHistory.Bit23;
    }
    public long FrameNumber{get;}
    public int HistoricalSubmissionCount{get;}
    public MapPostEffect3Functor1SourceReceipt SourceControllerReturn{get;}
    public MapPostEffect3ManagedModuleScope Scope{get;}
    public bool ApplyHistoryBit23AfterReturn{get;}
    public bool ProvesManagedFrontendEnd=>true;
    public bool ProvesNativeRawStateRestoration=>false;
    public bool ProvesNextFramePixelsReady=>false;
    public void RequireHistoricalAuthority()=>owner.RequireReturnedHistory(this);
}
