using Godot;
using San14.Data;

/// <summary>Device lifetime of one actual factory7 handler. Shape pixels,
/// staging copies and indirect counters retain their original writer frame
/// and revision. A CPU Finish/Return never initializes any of these resources.</summary>
public sealed partial class MapPostEffect3DepthOfField2ModuleResources:IAsyncDisposable
{
    public MapPostEffect3ProgramLibrary Library{get;}
    public PostProcessResourceBank Bank{get;}
    public MapPostEffect3Functor1ModuleState? CpuModule{get;}
    public string ContextOwnerIdentity=>Bank.Source.ContextOwnerIdentity;
    public long? LastCompletedFrameNumber{get;private set;}
    public string? UnreturnedDeviceFailure{get;private set;}
    private readonly SemaphoreSlim frames=new(1,1);
    private MapPostEffect3DepthOfField2ExecutionOwner? active;
    private long lastBorrowedFrame;
    private bool retiring,disposed;
    private readonly Dictionary<int,IMapPostEffect3ComputeBufferLease> argumentContents=[];
    private readonly Dictionary<(int,int),MapPostEffect3DeviceBuffer> physicalBuffers=[];
    internal ShapeStorage? Shape{get;private set;}
    internal MapPostEffect3DepthOfField2ScatterExecutor? Scatter{get;private set;}

    internal sealed class ShapeStorage(Rid rid,uint side,MapPostEffect3TextureParameterReference reference,
        MapPostEffect3TextureParameterState table)
    {
        internal readonly Rid Rid=rid;
        internal readonly uint Side=side;
        internal readonly MapPostEffect3TextureParameterReference Reference=reference;
        internal MapPostEffect3TextureParameterState LastTable=table;
        internal long Revision,WriterFrameNumber;
        internal bool Complete;
        internal int Pass;
        internal string? VertexProgramSha256,PixelProgramSha256;
    }
    private MapPostEffect3DepthOfField2ModuleResources(MapPostEffect3ProgramLibrary library,
        PostProcessResourceBank bank,MapPostEffect3Functor1ModuleState? cpuModule)
        =>(Library,Bank,CpuModule)=(library,bank,cpuModule);

    public static MapPostEffect3DepthOfField2ModuleResources CreateBorrowed(
        MapPostEffect3ProgramLibrary programs,PostProcessResourceBank bank,
        MapPostEffect3Functor1ModuleState actualCpuModule)
    {
        ArgumentNullException.ThrowIfNull(actualCpuModule);ValidateSource(programs,bank);
        if(!ReferenceEquals(actualCpuModule.Allocation,bank.Source)||actualCpuModule.HasUnreturnedFailure)
            throw new InvalidDataException("DOF2 device module requires its exact live Functor1 allocation and CPU handler.");
        return new(programs,bank,actualCpuModule);
    }
    internal static MapPostEffect3DepthOfField2ModuleResources CreateFrameOwned(
        MapPostEffect3ProgramLibrary programs,PostProcessResourceBank bank)
    {ValidateSource(programs,bank);return new(programs,bank,null);}
    private static void ValidateSource(MapPostEffect3ProgramLibrary programs,PostProcessResourceBank bank)
    {
        ArgumentNullException.ThrowIfNull(programs);ArgumentNullException.ThrowIfNull(bank);
        if(programs.SourceSha256!=bank.Source.Source.Sha256)
            throw new InvalidDataException("DOF2 device module programs and bank have different original source payloads.");
        // A real owned bank allocation also checks bank lifetime. Shape58 is
        // external and cannot itself serve as a bank-liveness capability.
        bank.RequireTexture(59,0);
    }
    internal void ValidateCurrentSource(MapPostEffect3Functor1ModuleInvocation invocation,
        MapPostEffect3ParameterDepthOfField2Update prepared,MapPostEffect3TextureParameterState table,
        MapPostEffect3ProgramLibrary programs,PostProcessResourceBank bank,
        IReadOnlyDictionary<int,int> cbSides,IReadOnlyDictionary<int,int> bufferSides)
    {
        ObjectDisposedException.ThrowIf(retiring||disposed,this);
        if(CpuModule is null||!ReferenceEquals(Library,programs)||!ReferenceEquals(Bank,bank)||UnreturnedDeviceFailure is not null)
            throw new InvalidDataException("DOF2 retained device resources require their original bank/program instances and resolved module generation.");
        invocation.RequireOwnedModule(CpuModule);invocation.RequireCurrentDepthOfField2Prepared(prepared,table);
        if(cbSides.Any(s=>!invocation.ConstantBufferInstances.TryGetValue(s.Key,out int side)||side!=s.Value)||
            bufferSides.Any(s=>!invocation.BufferInstances.TryGetValue(s.Key,out int side)||side!=s.Value))
            throw new InvalidDataException("DOF2 retained device work must select the actual current Functor1 CB/buffer side tables.");
    }
    internal async Task BorrowAsync(MapPostEffect3DepthOfField2ExecutionOwner owner,
        MapPostEffect3Functor1ModuleInvocation? cpuInvocation,IReadOnlyDictionary<int,int> cbSides,
        IReadOnlyDictionary<int,int> bufferSides)
    {
        ObjectDisposedException.ThrowIf(retiring||disposed,this);await frames.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(retiring||disposed,this);
            if(active is not null||UnreturnedDeviceFailure is not null||owner.Prepared.FrameNumber<=lastBorrowedFrame||
                !ReferenceEquals(owner.Bank,Bank)||!ReferenceEquals(owner.Graphics.Library,Library))
                throw new InvalidDataException("DOF2 module resources require one later frame on the same physical bank and device programs.");
            if(CpuModule is not null)
                ValidateCurrentSource(cpuInvocation??throw new InvalidDataException("DOF2 retained frame lacks its exact Functor1 invocation."),
                    owner.Prepared,owner.TextureParameters,Library,Bank,cbSides,bufferSides);
            foreach(var side in bufferSides)
            {
                var buffer=Bank.RequireBuffer(side.Key,side.Value);var key=(side.Key,side.Value);
                if(physicalBuffers.TryGetValue(key,out var actual)&&(!ReferenceEquals(buffer,actual)||buffer.StorageRid!=actual.StorageRid))
                    throw new InvalidDataException("DOF2 retained physical buffer owner was replaced.");
                physicalBuffers[key]=buffer;
            }
            active=owner;lastBorrowedFrame=owner.Prepared.FrameNumber;
        }
        catch{frames.Release();throw;}
    }
    internal void RequireActive(MapPostEffect3DepthOfField2ExecutionOwner owner)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        Bank.RequireTexture(59,0);
        if(!ReferenceEquals(active,owner)||UnreturnedDeviceFailure is not null)
            throw new InvalidDataException("DOF2 device resource borrow is stale or has an unreturned failure.");
        foreach(var item in physicalBuffers)
        {
            var actual=Bank.RequireBuffer(item.Key.Item1,item.Key.Item2);
            if(!ReferenceEquals(actual,item.Value)||actual.StorageRid!=item.Value.StorageRid)
                throw new InvalidDataException("DOF2 module's physical buffer owner changed.");
        }
    }
    internal async Task<MapPostEffect3DepthOfField2ScatterExecutor> RequireScatterAsync(
        MapPostEffect3DepthOfField2ExecutionOwner owner)
    {RequireActive(owner);return Scatter??=await MapPostEffect3DepthOfField2ScatterExecutor.BuildBorrowedAsync(Library,Bank);}
    internal Task<MapPostEffect3DepthOfField2StagingReadbackReceipt> CaptureActiveStagingReadbackAsync(
        MapPostEffect3DepthOfField2ExecutionOwner owner,Action requireCurrentSource)
    {
        void RequireSource()
        {
            RequireActive(owner);requireCurrentSource();
            if(owner.CurrentSourcePassIndex!=57||!owner.Prepared.RequiresSpriteCountReadback)
                throw new InvalidDataException("Actual source staging map is permitted only in current Count0 Before57.");
        }
        RequireSource();
        return (Scatter??throw new InvalidDataException("Count0 Before57 has no retained actual staging owner."))
            .ReadRetainedStagingAsync(owner.Prepared,RequireSource);
    }
    internal void AdoptShape(MapPostEffect3DepthOfField2ExecutionOwner owner,ShapeStorage shape)
    {RequireActive(owner);if(Shape is not null)throw new InvalidDataException("DOF2 actual shape allocation cannot silently replace retained contents.");Shape=shape;}
    internal void PublishArguments(MapPostEffect3DepthOfField2ExecutionOwner owner,
        MapPostEffect3ComputeCompletedPass completed,IMapPostEffect3ComputeBufferLease actual)
    {
        RequireActive(owner);actual.RequireReadRange(0,32);
        if(!ReferenceEquals(owner.Compute.LastCompletedPass,completed)||completed.SourcePassIndex is not(57 or 58)||
            completed.FrameNumber!=owner.Prepared.FrameNumber||actual.FrameNumber!=completed.FrameNumber||actual.ParameterIndex!=5||
            actual.ContextOwnerIdentity!=ContextOwnerIdentity||actual.ByteCount!=32||
            Bank.RequireBuffer(5,actual.InstanceIndex).StorageRid!=actual.StorageRid||
            !completed.BufferWrites.Any(w=>w.ParameterIndex==5&&w.InstanceIndex==actual.InstanceIndex&&
                w.StorageRid==actual.StorageRid&&w.WriterRevision==actual.UploadRevision))
            throw new InvalidDataException("DOF2 retained indirect arguments need the actual latest CS45/46 full32B publication.");
        argumentContents[actual.InstanceIndex]=actual;
    }
    internal void RecordCompleted(MapPostEffect3DepthOfField2ExecutionOwner owner,
        MapPostEffect3DepthOfField2CompletedExecution completed)
    {
        RequireActive(owner);
        if(!ReferenceEquals(completed.Prepared,owner.Prepared)||!completed.ProvesAllSelectedPassesWereSubmitted)
            throw new InvalidDataException("DOF2 module resource completion needs every actually selected command.");
        LastCompletedFrameNumber=completed.Prepared.FrameNumber;
    }
    internal void RecordFailure(MapPostEffect3DepthOfField2ExecutionOwner owner,string evidence)
    {if(ReferenceEquals(active,owner))UnreturnedDeviceFailure=evidence;}
    internal void Release(MapPostEffect3DepthOfField2ExecutionOwner owner)
    {if(!ReferenceEquals(active,owner))throw new InvalidDataException("DOF2 frame released a foreign module borrow.");active=null;frames.Release();}

    /// <summary>Original writer frame is retained. This lease is never relabeled
    /// as next-frame readiness, and becomes stale when its physical B5 is written.</summary>
    public IMapPostEffect3ComputeBufferLease CaptureRetainedArgumentContents(int actualPhysicalSide)
    {
        ObjectDisposedException.ThrowIf(retiring||disposed,this);
        if(UnreturnedDeviceFailure is not null||!argumentContents.TryGetValue(actualPhysicalSide,out var actual))
            throw new InvalidDataException("DOF2 physical side has no retained actual full32B counter publication.");
        actual.RequireReadRange(0,32);return actual;
    }
    public MapPostEffect3DepthOfField2PhysicalRetentionSnapshot CapturePhysicalRetention()
    {
        ObjectDisposedException.ThrowIf(retiring||disposed,this);
        Bank.RequireTexture(59,0);
        if(active is not null)throw new InvalidOperationException("Capture DOF2 physical retention after the frame borrow has drained.");
        var copies=Scatter?.CaptureRetainedStagingCopies()??[];
        return new(this,Shape?.Rid??default,Shape?.Side??0,Shape?.Revision??0,Shape?.WriterFrameNumber??0,
            Shape?.Complete??false,Shape?.Reference,Array.AsReadOnly(copies.ToArray()),LastCompletedFrameNumber,
            Shape?.VertexProgramSha256,Shape?.PixelProgramSha256);
    }
    /// <summary>Reads the actual older source-selected staging side. This
    /// endpoint supplies counts only, without publishing any target pixels.</summary>
    public async Task<MapPostEffect3DepthOfField2StagingReadbackReceipt> CaptureCurrentStagingReadbackAsync(
        MapPostEffect3Functor1ModuleInvocation actualInvocation,
        MapPostEffect3ParameterDepthOfField2Update actualPrepared,
        MapPostEffect3TextureParameterState actualTable)
    {
        void RequireSource()
        {
            ObjectDisposedException.ThrowIf(retiring||disposed,this);
            if(CpuModule is null||UnreturnedDeviceFailure is not null)
                throw new InvalidDataException("DOF2 staged read needs its resolved retained actual module.");
            actualInvocation.RequireOwnedModule(CpuModule);
            actualInvocation.RequireCurrentDepthOfField2Prepared(actualPrepared,actualTable);
        }
        RequireSource();await frames.WaitAsync();
        try
        {
            RequireSource();
            if(active is not null)throw new InvalidOperationException("DOF2 external source staging map must precede its current frame borrow.");
            return await (Scatter??throw new NotSupportedException("DOF2 native staging has no original earlier GPU copy owner."))
                .ReadRetainedStagingAsync(actualPrepared,RequireSource);
        }
        finally{frames.Release();}
    }
    internal void RequireSnapshot(MapPostEffect3DepthOfField2PhysicalRetentionSnapshot snapshot)
    {
        ObjectDisposedException.ThrowIf(retiring||disposed,this);
        Bank.RequireTexture(59,0);
        if(active is not null||UnreturnedDeviceFailure is not null||snapshot.ShapeStorageRid!=(Shape?.Rid??default)||
            snapshot.ShapeContentsRevision!=(Shape?.Revision??0)||snapshot.ShapeWriterFrameNumber!=(Shape?.WriterFrameNumber??0)||
            snapshot.ShapeHasActualCompletePixels!=(Shape?.Complete??false)||snapshot.LastCompletedComponentFrameNumber!=LastCompletedFrameNumber||
            snapshot.ShapeWriterVertexProgramSha256!=Shape?.VertexProgramSha256||snapshot.ShapeWriterPixelProgramSha256!=Shape?.PixelProgramSha256||
            !ReferenceEquals(snapshot.ActualShapeReference,Shape?.Reference)||
            !(Scatter?.CaptureRetainedStagingCopies()??[]).SequenceEqual(snapshot.StagingCopies))
            throw new InvalidDataException("DOF2 physical retention snapshot was changed, borrowed or retired.");
        foreach(var counter in argumentContents.Values)counter.RequireReadRange(0,32);
    }
    public async ValueTask DisposeAsync()
    {
        await frames.WaitAsync();
        try
        {
            if(disposed)return;
            if(active is not null)throw new InvalidOperationException("Retire the active DOF2 execution borrow before its module resources.");
            retiring=true;
            if(Scatter is not null){await Scatter.DisposeAsync();Scatter=null;}
            if(Shape is{} shape)
            {
                // Keep the physical owner through a failed release. Revoking
                // its CPU wrapper loan does not retire the live device RID.
                if(shape.LastTable.ReadKnownNullablePointer(58) is not null&&
                    ReferenceEquals(shape.LastTable.Require(58).Reference,shape.Reference))
                    shape.LastTable.ClearExternal(58,"retiring actual module-lifetime DOF2 shape owner");
                if(shape.Rid.IsValid)
                    await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(shape.Rid);return true;});
                Shape=null;
            }
            argumentContents.Clear();physicalBuffers.Clear();accumulation.Clear();disposed=true;
        }
        finally{frames.Release();}
    }
}

public sealed class MapPostEffect3DepthOfField2PhysicalRetentionSnapshot
{
    private readonly MapPostEffect3DepthOfField2ModuleResources owner;
    internal MapPostEffect3DepthOfField2PhysicalRetentionSnapshot(MapPostEffect3DepthOfField2ModuleResources owner,
        Rid shape,uint side,long revision,long writerFrame,bool complete,MapPostEffect3TextureParameterReference? reference,
        IReadOnlyList<MapPostEffect3DepthOfField2StagingCopyReceipt> copies,long? completedFrame,string? vertexSha,string? pixelSha)
        =>(this.owner,ShapeStorageRid,ShapeSide,ShapeContentsRevision,ShapeWriterFrameNumber,ShapeHasActualCompletePixels,
            ActualShapeReference,StagingCopies,LastCompletedComponentFrameNumber,ShapeWriterVertexProgramSha256,ShapeWriterPixelProgramSha256)=
            (owner,shape,side,revision,writerFrame,complete,reference,copies,completedFrame,vertexSha,pixelSha);
    public Rid ShapeStorageRid{get;}
    public uint ShapeSide{get;}
    public long ShapeContentsRevision{get;}
    public long ShapeWriterFrameNumber{get;}
    public bool ShapeHasActualCompletePixels{get;}
    public string? ShapeWriterVertexProgramSha256{get;}
    public string? ShapeWriterPixelProgramSha256{get;}
    public uint? ShapeNativeFlags46=>ActualShapeReference is null?null:0x403;
    public uint? ShapeNativeMipCount45=>ActualShapeReference is null?null:1;
    public uint? ShapeNativeViewFormat=>ActualShapeReference is null?null:10;
    public RenderingDevice.DataFormat? ShapeDeviceFormat=>ActualShapeReference is null?null:RenderingDevice.DataFormat.R16G16B16A16Sfloat;
    public MapPostEffect3TextureParameterReference? ActualShapeReference{get;}
    public IReadOnlyList<MapPostEffect3DepthOfField2StagingCopyReceipt> StagingCopies{get;}
    public long? LastCompletedComponentFrameNumber{get;}
    public bool CommitsFunctorHistory=>false;
    public bool ProvesNextFramePixelsReady=>false;
    public void RequirePhysicalCurrent()=>owner.RequireSnapshot(this);
}
