using Godot;
using San14.Data;
using San14.Data.Formats;

public sealed record DeferredAuxiliaryDeviceSurface(DeferredAuxiliarySurface Source,
    Rid TargetView, Rid Framebuffer);
public sealed record DeferredAuxiliaryProductionReceipt(long FrameNumber, string ProducerProvenance,
    string GraphResourceUri, string GraphSha256, int WriterNodeIndex, uint WriterFactoryClassId,
    string SourceScope, uint TextureObjectId, IReadOnlyList<uint> SurfaceIds)
{
    public LoadedRenderGraph? SourceGraph { get; init; }
    public DeferredRefractionSourceNullExecution? SourceNullRefractionExecution { get; init; }
    public static DeferredAuxiliaryProductionReceipt FromSourceWriter(LoadedRenderGraph graph, int nodeIndex,
        long frameNumber, string producerProvenance, string sourceScope, uint textureObjectId,
        IReadOnlyList<uint> surfaces)
    {
        ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(surfaces);
        if((uint)nodeIndex>=graph.Document.Nodes.Count)throw new InvalidDataException("Original auxiliary writer node is absent.");
        return new(frameNumber,producerProvenance,graph.Source.ResourceUri,graph.Source.DecodedSha256,nodeIndex,
            graph.Document.Nodes[nodeIndex].Call.FactoryClassId,sourceScope,textureObjectId,Array.AsReadOnly(surfaces.ToArray()))
            { SourceGraph=graph };
    }
}

/// <summary>A read lease protects an ordered draw, then a separate lifetime
/// dependency retains its views across cached draws. Allocation is not content.</summary>
public sealed class DeferredAuxiliaryTargetBorrow : IDisposable
{
    private Action? release;
    private Action? completeRead;
    public RenderTextureDefinition Source { get; }
    public Vector2I ExplicitExtent { get; }
    public Rid StorageRid { get; }
    public Rid ShaderViewRid { get; }
    public RenderingDevice.DataFormat StorageFormat { get; }
    public RenderingDevice.DataFormat ShaderFormat { get; }
    public RenderingDevice.DataFormat TargetFormat { get; }
    public uint MipCount { get; }
    public uint MostDetailedMip { get; }
    public uint ViewMipCount { get; }
    public long FrameNumber { get; }
    public string ProducerProvenance { get; }
    public IReadOnlyList<DeferredAuxiliaryProductionReceipt> Receipts { get; }
    internal DeferredAuxiliaryTargetBorrow(DeferredAuxiliaryTargetDevice owner, long frame,
        string provenance, IReadOnlyList<DeferredAuxiliaryProductionReceipt> receipts,
        Vector2I extent,Rid view,uint firstMip,uint viewMipCount,Action onCompleteRead, Action onRelease)
    {
        Source=owner.Source; ExplicitExtent=extent; StorageRid=owner.StorageRid;
        ShaderViewRid=view; StorageFormat=owner.StorageFormat; ShaderFormat=owner.ShaderFormat;
        TargetFormat=owner.TargetFormat; MipCount=owner.MipCount; MostDetailedMip=firstMip;
        ViewMipCount=viewMipCount; FrameNumber=frame; ProducerProvenance=provenance; Receipts=receipts;
        completeRead=onCompleteRead;release=onRelease;
    }
    public void CompleteRead() => Interlocked.Exchange(ref completeRead,null)?.Invoke();
    public void Dispose(){CompleteRead();Interlocked.Exchange(ref release,null)?.Invoke();}
}

/// <summary>Owns actual2D native auxiliary storage, whole-range shader view and
/// per-mip source surfaces. Content is completed by an explicit original graph
/// writer; consumers hold leases across cached draws before reverse retirement.</summary>
public sealed class DeferredAuxiliaryTargetDevice : IAsyncDisposable
{
    public DeferredAuxiliaryTargetSource Construction { get; }
    public RenderTextureDefinition Source => Construction.Texture;
    public Vector2I ExplicitExtent => new(Construction.Extent.Width,Construction.Extent.Height);
    public Rid StorageRid { get; }
    public Rid ShaderViewRid { get; }
    public RenderingDevice.DataFormat StorageFormat { get; }
    public RenderingDevice.DataFormat ShaderFormat { get; }
    public RenderingDevice.DataFormat TargetFormat { get; }
    public uint MipCount => Construction.MipCount;
    public uint MostDetailedMip => Construction.MostDetailedMip;
    public uint ViewMipCount => Construction.ViewMipCount;
    public IReadOnlyList<DeferredAuxiliaryDeviceSurface> Surfaces { get; }
    private readonly RenderDeviceResources resources;
    private readonly object gate=new();
    private bool retiring,producing;
    private int borrows,reading,operations;
    private TaskCompletionSource? drained;
    private Task? disposal;
    private long? producedFrame;
    private string? producedProvenance;
    private readonly HashSet<uint> completed=[];
    private readonly Dictionary<uint,int> readersByMip=[];
    private readonly List<DeferredAuxiliaryProductionReceipt> receipts=[];

    private DeferredAuxiliaryTargetDevice(DeferredAuxiliaryTargetSource source, Rid storage, Rid shader,
        RenderingDevice.DataFormat storageFormat, RenderingDevice.DataFormat shaderFormat,
        RenderingDevice.DataFormat targetFormat, List<DeferredAuxiliaryDeviceSurface> surfaces, RenderDeviceResources resources)
        => (Construction,StorageRid,ShaderViewRid,StorageFormat,ShaderFormat,TargetFormat,Surfaces,this.resources)=
            (source,storage,shader,storageFormat,shaderFormat,targetFormat,surfaces.AsReadOnly(),resources);

    public static Task<DeferredAuxiliaryTargetDevice> BuildAsync(DeferredAuxiliaryTargetSource source,
        bool allowReadback=false, bool allowGpuCopy=false)
    {
        DeferredAuxiliaryTargetSources.Validate(source);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var resources=new RenderDeviceResources("Device rejected an actual auxiliary allocation/view.");
            Rid Own(Rid rid)=>resources.Own(rid);
            try
            {
                var surfaces=new List<DeferredAuxiliaryDeviceSurface>();
                RenderTextureFormats native=RenderTextureFormatMath.Resolve(source.Texture);
                bool depth=native.TargetViewFormat==40;
                RenderingDevice.DataFormat target=Format(native.TargetViewFormat);
                //D3D R32_FLOAT depth SRV is Vulkan's sampled D32 depth aspect.
                RenderingDevice.DataFormat shader=depth&&native.ShaderViewFormat==41?target:Format(native.ShaderViewFormat);
                var usage=RenderingDevice.TextureUsageBits.SamplingBit | (depth?
                    RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit:RenderingDevice.TextureUsageBits.ColorAttachmentBit);
                if(allowReadback||allowGpuCopy)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit;
                if(allowGpuCopy)usage|=RenderingDevice.TextureUsageBits.CanCopyToBit;
                if(!rd.TextureIsFormatSupportedForUsage(target,usage))throw new NotSupportedException("Device cannot represent this exact auxiliary source format/usage.");
                using var format=new RDTextureFormat{Format=target,Width=(uint)source.Extent.Width,Height=(uint)source.Extent.Height,
                    Depth=1,ArrayLayers=1,Mipmaps=source.MipCount,TextureType=RenderingDevice.TextureType.Type2D,
                    Samples=RenderingDevice.TextureSamples.Samples1,UsageBits=usage};
                foreach(var value in new[]{target,shader}.Distinct())format.AddShareableFormat(value);
                using var baseView=new RDTextureView(); Rid storage=Own(rd.TextureCreate(format,baseView)); Rid sampled=storage;
                if(shader!=target){using var view=new RDTextureView{FormatOverride=shader};sampled=Own(rd.TextureCreateShared(view,storage));}
                foreach(var surface in source.Surfaces)
                {
                    using var view=new RDTextureView{FormatOverride=target};
                    Rid targetView=source.MipCount==1?storage:Own(rd.TextureCreateSharedFromSlice(view,storage,0,surface.MipLevel,1,RenderingDevice.TextureSliceType.Slice2D));
                    Rid framebuffer=Own(rd.FramebufferCreate([targetView]));
                    surfaces.Add(new(surface,targetView,framebuffer));
                }
                return new DeferredAuxiliaryTargetDevice(source,storage,sampled,target,shader,target,surfaces,resources);
            }
            catch(Exception failure)
            {
                resources.ReleaseAfterConstructionFailure(rd,failure,"Deferred-auxiliary-target-partial");
                throw;
            }
        });
    }

    public async Task ProduceAsync(DeferredAuxiliaryProductionReceipt receipt,
        Action<RenderingDevice,IReadOnlyList<DeferredAuxiliaryDeviceSurface>> originalGraphWriter)
    {
        ArgumentNullException.ThrowIfNull(receipt);ArgumentNullException.ThrowIfNull(originalGraphWriter);
        ValidateProductionGraph(receipt);
        uint[] ids=receipt.SurfaceIds?.ToArray()??throw new InvalidDataException("Auxiliary writer surfaces are unknown.");
        if(receipt.FrameNumber<0||string.IsNullOrWhiteSpace(receipt.ProducerProvenance)||string.IsNullOrWhiteSpace(receipt.SourceScope)||
            string.IsNullOrWhiteSpace(receipt.GraphResourceUri)||receipt.GraphSha256 is not {Length:64} hash||!hash.All(Uri.IsHexDigit)||
            receipt.WriterNodeIndex<0||receipt.WriterFactoryClassId==0||receipt.TextureObjectId!=Source.Object.ObjectId||
            ids.Length==0||ids.Distinct().Count()!=ids.Length||ids.Any(id=>id>=MipCount))
            throw new InvalidDataException("Auxiliary writer completion lacks exact frame, graph, source scope or native surfaces.");
        var snapshot=receipt with{SurfaceIds=Array.AsReadOnly(ids)};
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            bool nextGeneration=producedFrame!=snapshot.FrameNumber||producedProvenance!=snapshot.ProducerProvenance;
            if(producing || (nextGeneration ? reading!=0 : ids.Any(id=>readersByMip.GetValueOrDefault(id)!=0)))
                throw new InvalidOperationException("An auxiliary write overlaps an ordered source read or another write.");
            producing=true;operations++;
            if(nextGeneration){completed.Clear();receipts.Clear();}
            producedFrame=snapshot.FrameNumber;producedProvenance=snapshot.ProducerProvenance;
            foreach(uint id in ids)completed.Remove(id);
        }
        try
        {
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                ValidateDevice(rd);
                originalGraphWriter(rd,Array.AsReadOnly(ids.Select(id=>Surfaces[(int)id]).ToArray()));
                lock(gate){foreach(uint id in ids)completed.Add(id);receipts.Add(snapshot);}
                return true;
            });
        }
        finally{lock(gate){producing=false;operations--;SignalDrained();}}
    }

    /// <summary>Append a proved source-null mode16 execution after its exact
    /// preceding writer. Unlike a pixel writer, this never clears, draws or
    /// marks an unproduced surface ready. The roster authority is rechecked on
    /// the render thread before the ordered completion is published.</summary>
    internal async Task CompleteSourceMode16NoOpAsync(DeferredAuxiliaryProductionReceipt receipt,
        DeferredAuxiliaryProductionReceipt precedingWriter, Action requireCurrentSource)
    {
        ArgumentNullException.ThrowIfNull(receipt);ArgumentNullException.ThrowIfNull(precedingWriter);
        ArgumentNullException.ThrowIfNull(requireCurrentSource);
        ValidateProductionGraph(receipt);ValidateProductionGraph(precedingWriter);
        if(receipt.SourceNullRefractionExecution is null || Source.Object.ObjectId!=0xB6169FDB || receipt.TextureObjectId!=Source.Object.ObjectId ||
            receipt.WriterFactoryClassId!=0xFC64DCB6 || receipt.WriterNodeIndex is not(139 or 140) ||
            precedingWriter.WriterNodeIndex!=(receipt.WriterNodeIndex==139?97:139) ||
            precedingWriter.WriterFactoryClassId!=(receipt.WriterNodeIndex==139?0xFD1176C6u:0xFC64DCB6u) ||
            receipt.FrameNumber<=0 || receipt.FrameNumber!=precedingWriter.FrameNumber ||
            receipt.ProducerProvenance!=precedingWriter.ProducerProvenance ||
            receipt.GraphResourceUri!=precedingWriter.GraphResourceUri || receipt.GraphSha256!=precedingWriter.GraphSha256 ||
            string.IsNullOrWhiteSpace(receipt.SourceScope) || string.IsNullOrWhiteSpace(receipt.ProducerProvenance) ||
            !receipt.SurfaceIds.SequenceEqual(new uint[]{0}) || !precedingWriter.SurfaceIds.SequenceEqual(new uint[]{0}))
            throw new InvalidDataException("Source-null refraction completion requires the exact current node97→139→140 chain.");
        var snapshot=receipt with{SurfaceIds=Array.AsReadOnly(new uint[]{0})};
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(producing || readersByMip.GetValueOrDefault(0u)!=0)
                throw new InvalidOperationException("Source-null completion overlaps another target operation.");
            if(producedFrame!=snapshot.FrameNumber || producedProvenance!=snapshot.ProducerProvenance ||
                !completed.Contains(0) || receipts.Count==0 || !SameWriter(receipts[^1],precedingWriter))
                throw new InvalidDataException("Source-null geometry cannot complete an absent, overwritten or reordered preceding writer.");
            producing=true;operations++;
        }
        try
        {
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                ValidateDevice(rd);requireCurrentSource();
                lock(gate){receipts.Add(snapshot);}
                return true;
            });
        }
        finally{lock(gate){producing=false;operations--;SignalDrained();}}
    }
    private static bool SameWriter(DeferredAuxiliaryProductionReceipt a,
        DeferredAuxiliaryProductionReceipt b)
        =>a.FrameNumber==b.FrameNumber && a.ProducerProvenance==b.ProducerProvenance &&
          a.GraphResourceUri==b.GraphResourceUri && a.GraphSha256==b.GraphSha256 &&
          a.WriterNodeIndex==b.WriterNodeIndex && a.WriterFactoryClassId==b.WriterFactoryClassId &&
          a.SourceScope==b.SourceScope && a.TextureObjectId==b.TextureObjectId && a.SurfaceIds.SequenceEqual(b.SurfaceIds);
    private static void ValidateProductionGraph(DeferredAuxiliaryProductionReceipt receipt)
    {
        if(receipt.SourceGraph is not { } graph || graph.Source.OverrideSource is not null ||
            graph.Source.ResourceUri!=receipt.GraphResourceUri || graph.Source.DecodedSha256!=receipt.GraphSha256)
            throw new InvalidDataException("Auxiliary production has no exact original graph source.");
        if(graph.Document.GraphKind!=CompiledGraphKind.RenderGraph||
            (uint)receipt.WriterNodeIndex>=graph.Document.Nodes.Count||
            graph.Document.Nodes[receipt.WriterNodeIndex].Call.FactoryClassId!=receipt.WriterFactoryClassId)
            throw new InvalidDataException("Auxiliary writer identity differs from the original graph body.");
    }

    public Task<DeferredAuxiliaryTargetBorrow> BorrowAsync(long frameNumber,string producerProvenance)
        => BorrowRangeAsync(frameNumber,producerProvenance,MostDetailedMip,ViewMipCount,ShaderViewRid,ExplicitExtent);

    /// <summary>Exact native auxiliary firstMip/count1 view. Its sampler metadata
    /// remains the parent texture's seven-mip declaration and original key.</summary>
    public Task<DeferredAuxiliaryTargetBorrow> BorrowMipAsync(long frameNumber,string producerProvenance,uint mip)
    {
        if(mip>=MipCount)throw new ArgumentOutOfRangeException(nameof(mip));
        var surface=Surfaces[(int)mip];
        return BorrowRangeAsync(frameNumber,producerProvenance,mip,1,surface.TargetView,
            new(surface.Source.Extent.Width,surface.Source.Extent.Height));
    }

    private async Task<DeferredAuxiliaryTargetBorrow> BorrowRangeAsync(long frameNumber,string producerProvenance,
        uint firstMip,uint count,Rid view,Vector2I extent)
    {
        uint[] mips=Enumerable.Range((int)firstMip,(int)count).Select(mip=>(uint)mip).ToArray();
        IReadOnlyList<DeferredAuxiliaryProductionReceipt> captured;
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(producing||producedFrame!=frameNumber||producedProvenance!=producerProvenance||
                mips.Any(mip=>!completed.Contains(mip)))
                throw new InvalidDataException("Auxiliary shader view has no complete matching current producer generation.");
            borrows++;reading++;
            foreach(uint mip in mips)readersByMip[mip]=readersByMip.GetValueOrDefault(mip)+1;
            captured=Array.AsReadOnly(receipts.ToArray());
        }
        try
        {
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {ValidateDevice(rd);if(!rd.TextureIsValid(view))throw new InvalidDataException("Auxiliary mip source view has retired.");return true;});
            return new(this,frameNumber,producerProvenance,captured,extent,view,firstMip,count,()=>CompleteRead(mips),ReleaseBorrow);
        }
        catch{CompleteRead(mips);ReleaseBorrow();throw;}
    }
    private void ValidateDevice(RenderingDevice rd)
    {
        if(!rd.TextureIsValid(StorageRid)||!rd.TextureIsValid(ShaderViewRid))throw new InvalidDataException("Auxiliary source view has retired.");
        using var actual=rd.TextureGetFormat(ShaderViewRid);
        if(actual.Width!=(uint)ExplicitExtent.X||actual.Height!=(uint)ExplicitExtent.Y||actual.ArrayLayers!=1||
            actual.Mipmaps!=ViewMipCount||actual.Format!=ShaderFormat||actual.TextureType!=RenderingDevice.TextureType.Type2D||
            !actual.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
            throw new InvalidDataException("Actual auxiliary shader view differs from its native whole-mip recipe.");
    }
    private void ReleaseBorrow(){lock(gate){borrows--;SignalDrained();}}
    private void CompleteRead(uint[] mips)
    {
        lock(gate)
        {
            reading--;
            foreach(uint mip in mips)
            {
                int remaining=readersByMip[mip]-1;
                if(remaining==0)readersByMip.Remove(mip);else readersByMip[mip]=remaining;
            }
        }
    }
    private void SignalDrained(){if(retiring&&borrows==0&&operations==0)drained?.TrySetResult();}
    public ValueTask DisposeAsync()
    {
        lock(gate)
        {
            retiring=true;
            if(disposal is null||disposal.IsFaulted||disposal.IsCanceled)disposal=RetireAsync();
            return new(disposal);
        }
    }
    private async Task RetireAsync()
    {
        Task wait;lock(gate){wait=borrows==0&&operations==0?Task.CompletedTask:(drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;}
        await wait;
        await resources.DisposeAsync();
    }
    private static RenderingDevice.DataFormat Format(uint dxgi)=>dxgi switch
    {26=>RenderingDevice.DataFormat.B10G11R11UfloatPack32,41=>RenderingDevice.DataFormat.R32Sfloat,
        40=>RenderingDevice.DataFormat.D32Sfloat,_=>throw new InvalidDataException("Auxiliary source view format is outside the actual four-target recipe.")};
}
