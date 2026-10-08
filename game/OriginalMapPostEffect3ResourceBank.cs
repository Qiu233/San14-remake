using System.Collections.ObjectModel;
using System.Security.Cryptography;
using Godot;
using San14.Data;

public sealed record OriginalMapPostEffect3DeviceTexture(OriginalMapPostEffect3TextureAllocation Source,
    OriginalMapPostEffect3TextureShape Shape,int InstanceIndex,Rid StorageRid,
    RenderingDevice.DataFormat StorageFormat,IReadOnlyDictionary<uint,Rid> NativeFormatViews)
{
    // Allocation alone never establishes readable contents or a chosen mip.
    public bool ContentsInitializedByAllocation=>false;
}

public sealed partial record OriginalMapPostEffect3DeviceBuffer(OriginalMapPostEffect3BufferAllocation Source,
    OriginalMapPostEffect3BufferShape Shape,int InstanceIndex,Rid StorageRid,RenderingDevice.DataFormat DeviceTexelFormat,
    uint TexelCount)
{
    // Structured/raw buffers retain their byte addressing in the translated
    // shader; the uint view is vkd3d's actual interface, not a typed source claim.
    public bool ContentsInitializedByAllocation=>false;
    public bool NativeAllowsShaderRead=>Shape.CreatesShaderResourceView;
    public bool NativeAllowsUnorderedAccess=>Shape.CreatesUnorderedAccessView;
    public bool RequiresIndirectArgumentAdapter=>(Shape.MiscFlags&0x10)!=0;
    public bool SupportsIndirectArgumentBinding{get;init;}
    public bool UsesStorageBuffer{get;init;}
    internal Rid PreparedShaderReadRid{get;set;}
    public Rid ShaderReadRid=>UsesStorageBuffer?PreparedShaderReadRid:StorageRid;
}

public sealed class OriginalMapPostEffect3DeviceConstantBuffer
{
    internal OriginalMapPostEffect3DeviceConstantBuffer(OriginalMapPostEffect3ConstantBufferAllocation source,
        int instance,Rid storage)=>(Source,InstanceIndex,StorageRid)=(source,instance,storage);
    public OriginalMapPostEffect3ConstantBufferAllocation Source{get;}
    public int InstanceIndex{get;}
    public Rid StorageRid{get;}
    public bool HasCompleteWriterContents{get;internal set;}
    public long? CompleteFrameNumber{get;internal set;}
    public uint? WriterRva{get;internal set;}
    public string? CompleteBytesSha256{get;internal set;}
    public long? PassScopedFrameNumber{get;internal set;}
    public int? PassScopedPassIndex{get;internal set;}
    public int PassKnownSourceByteCount{get;internal set;}
    public long UploadRevision{get;internal set;}
}

/// <summary>Fresh owned 3SPK allocations, independent of pass execution. External
/// slots stay absent; double-sided declarations stay distinct. Consumers must
/// retire views/uniform sets/pipelines before this bank. A zero-filled backend
/// allocation is never treated as a native complete CB or scalar initializer.</summary>
public sealed partial class OriginalMapPostEffect3ResourceBank:IAsyncDisposable
{
    public OriginalMapPostEffect3AllocationPlan Source{get;}
    public IReadOnlyDictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceTexture>> Textures{get;}
    public IReadOnlyDictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceBuffer>> Buffers{get;}
    public IReadOnlyDictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceConstantBuffer>> ConstantBuffers{get;}
    public int OwnedRidCount{get;private set;}
    public bool IsCompleteForExecution=>false;
    private readonly List<Rid> owned;
    private readonly bool allowReadback;
    private readonly Dictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceTexture>> textureRows;
    private readonly Dictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceBuffer>> bufferRows;
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();
    private bool retiring;private int accepted;private TaskCompletionSource? drained;private Task? disposal;

    private OriginalMapPostEffect3ResourceBank(OriginalMapPostEffect3AllocationPlan source,
        Dictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceTexture>> textures,
        Dictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceBuffer>> buffers,
        Dictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceConstantBuffer>> constants,List<Rid> owned,bool readback)
    {Source=source;textureRows=textures;Textures=new ReadOnlyDictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceTexture>>(textureRows);
     bufferRows=buffers;Buffers=new ReadOnlyDictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceBuffer>>(bufferRows);
     ConstantBuffers=new ReadOnlyDictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceConstantBuffer>>(constants);
     this.owned=owned;allowReadback=readback;OwnedRidCount=owned.Count;}

    public static Task<OriginalMapPostEffect3ResourceBank> BuildAsync(OriginalMapPostEffect3AllocationPlan source,
        bool allowReadback=false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.ResizeOnly)throw new NotSupportedException("A fresh 3SPK resource bank cannot replace fixed or CB owners during resize-only.");
        if(source.Source.Sha256!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256||
            Convert.ToHexString(SHA256.HashData(source.Source.Bytes.Span))!=source.Source.Sha256)
            throw new InvalidDataException("3SPK bank requires the complete pinned native source payload.");
        var container=OriginalMapPostEffect3ContainerReader.Read(source.Source.Bytes.Span);
        if(container.Sha256!=source.Source.Sha256)throw new InvalidDataException("3SPK resource source changed before allocation.");
        var plan=OriginalMapPostEffect3AllocationReader.Read(container,source.ContextWidth,source.ContextHeight,source.ContextOwnerIdentity);
        return BuildDeviceResourcesAsync(plan,allowReadback);
    }

    private static Task<OriginalMapPostEffect3ResourceBank> BuildDeviceResourcesAsync(
        OriginalMapPostEffect3AllocationPlan plan,bool allowReadback)
    {
        var shapes=plan.TextureAllocations.ToDictionary(a=>a.Parameter.Index,OriginalMapPostEffect3AllocationShapeReader.Read);
        var bufferShapes=plan.BufferAllocations.ToDictionary(a=>a.Parameter.Index,OriginalMapPostEffect3AllocationBufferShapeReader.Read);
        return OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var owned=new List<Rid>();
            Rid Own(Rid rid)
            {if(!rid.IsValid)throw new InvalidDataException("RenderingDevice rejected an actual 3SPK allocation/view.");owned.Add(rid);return rid;}
            var textures=new Dictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceTexture>>();
            var buffers=new Dictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceBuffer>>();
            var constants=new Dictionary<int,IReadOnlyList<OriginalMapPostEffect3DeviceConstantBuffer>>();
            try
            {
                foreach(var allocation in plan.TextureAllocations.Where(a=>a.CreateInThisInvocation))
                {
                    var shape=shapes[allocation.Parameter.Index];var native=shape.Formats;
                    var viewCodes=new[]{native.ShaderResourceFormat0,native.ShaderResourceFormat1,
                        shape.HasRenderTargetPath?native.RenderTargetFormat0:0,
                        shape.HasRenderTargetPath?native.RenderTargetFormat1:0,
                        shape.AllowsUnorderedAccess?native.UnorderedAccessFormat:0}.Where(f=>f!=0).Distinct().ToArray();
                    uint primary=shape.HasRenderTargetPath?native.RenderTargetFormat0:native.ShaderResourceFormat0;
                    var storage=ViewFormat(primary);
                    var usage=RenderingDevice.TextureUsageBits.SamplingBit;
                    if(shape.HasRenderTargetPath)usage|=RenderingDevice.TextureUsageBits.ColorAttachmentBit;
                    if(shape.AllowsUnorderedAccess)usage|=RenderingDevice.TextureUsageBits.StorageBit;
                    if(allowReadback)usage|=RenderingDevice.TextureUsageBits.CanCopyFromBit|RenderingDevice.TextureUsageBits.CanCopyToBit;
                    if(!rd.TextureIsFormatSupportedForUsage(storage,usage))
                        throw new NotSupportedException($"Device does not support actual 3SPK texture{shape.ParameterIndex} format/usage.");
                    var instances=new List<OriginalMapPostEffect3DeviceTexture>();
                    for(int instance=0;instance<allocation.InstanceCount;instance++)
                    {
                        using var format=new RDTextureFormat{Format=storage,Width=shape.Width,Height=shape.Height,
                            Depth=shape.Depth,ArrayLayers=1,Mipmaps=checked((uint)shape.MipLevels),
                            TextureType=shape.Dimension==OriginalMapPostEffect3TextureDimension.Texture3D?
                                RenderingDevice.TextureType.Type3D:RenderingDevice.TextureType.Type2D,
                            Samples=RenderingDevice.TextureSamples.Samples1,UsageBits=usage};
                        foreach(var code in viewCodes)format.AddShareableFormat(ViewFormat(code));
                        using var baseView=new RDTextureView();Rid image=Own(rd.TextureCreate(format,baseView));
                        var views=new Dictionary<uint,Rid>();
                        foreach(uint code in viewCodes)
                        {
                            if(ViewFormat(code)==storage){views.Add(code,image);continue;}
                            using var view=new RDTextureView{FormatOverride=ViewFormat(code)};
                            views.Add(code,Own(rd.TextureCreateShared(view,image)));
                        }
                        instances.Add(new(allocation,shape,instance,image,storage,new ReadOnlyDictionary<uint,Rid>(views)));
                    }
                    textures.Add(allocation.Parameter.Index,instances.AsReadOnly());
                }
                foreach(var allocation in plan.BufferAllocations.Where(a=>a.CreateInThisInvocation))
                {
                    var p=allocation.Parameter;var shape=bufferShapes[p.Index];
                    // 1147C40/94B250 uses a distinct typed-buffer table. Texture
                    // native code6 means DXGI10 here, not texture table code6.
                    (uint stride,RenderingDevice.DataFormat format)=shape.Dimension switch{
                        OriginalMapPostEffect3BufferDimension.Typed=>shape.ViewFormat switch{
                            10=>(8u,RenderingDevice.DataFormat.R16G16B16A16Sfloat),
                            42=>(4u,RenderingDevice.DataFormat.R32Uint),
                            _=>throw new NotSupportedException("3SPK typed buffer format is outside the recovered native table.")},
                        OriginalMapPostEffect3BufferDimension.Structured or OriginalMapPostEffect3BufferDimension.Raw
                            =>(4u,RenderingDevice.DataFormat.R32Uint),
                        _=>throw new NotSupportedException("3SPK buffer creator kind is unsupported.")};
                    if(allocation.ByteCount==0||allocation.ByteCount%stride!=0||
                        shape.Dimension==OriginalMapPostEffect3BufferDimension.Structured&&
                            (shape.StructureByteStride==0||allocation.ByteCount%shape.StructureByteStride!=0))
                        throw new InvalidDataException("3SPK source byte extent does not fit its typed/structured buffer view.");
                    var instances=new List<OriginalMapPostEffect3DeviceBuffer>();
                    for(int instance=0;instance<allocation.InstanceCount;instance++)
                    {
                        uint count=allocation.ByteCount/stride;
                        bool eyeHistogram=p.Index==8&&shape.Dimension==OriginalMapPostEffect3BufferDimension.Typed&&
                            shape.ViewFormat==42&&shape.ViewElementCount==64&&shape.ByteCount==256&&
                            shape.CreatesShaderResourceView&&shape.CreatesUnorderedAccessView;
                        bool dofSprites=IsOriginalDofSpriteStorageAllocation(allocation,shape);
                        bool dofIndirect=IsOriginalDofSpriteIndirectAllocation(allocation,shape);
                        // Exact Eye8 and DOF6/7 use physical uint SSBOs. The
                        // DOF source adapter packs all four half components;
                        // the bank keeps their original eight-byte texels.
                        bool storage=shape.CreatesUnorderedAccessView&&
                            ((shape.Dimension is OriginalMapPostEffect3BufferDimension.Raw or OriginalMapPostEffect3BufferDimension.Structured)||eyeHistogram||dofSprites);
                        Rid rid=Own(storage?rd.StorageBufferCreate(allocation.ByteCount,Array.Empty<byte>(),
                            dofIndirect?RenderingDevice.StorageBufferUsage.Indirect:0):rd.TextureBufferCreate(count,format));
                        instances.Add(new(allocation,shape,instance,rid,format,count)
                            {UsesStorageBuffer=storage,SupportsIndirectArgumentBinding=dofIndirect});
                    }
                    buffers.Add(p.Index,instances.AsReadOnly());
                }
                foreach(var allocation in plan.ConstantBufferAllocations.Where(a=>a.CreateInThisInvocation))
                {
                    uint bytes=allocation.Parameter.ByteCount;
                    if(bytes%16!=0)throw new NotSupportedException("3SPK native CB is not a positive16-byte-aligned allocation.");
                    var instances=new List<OriginalMapPostEffect3DeviceConstantBuffer>();
                    for(int instance=0;instance<allocation.InstanceCount;instance++)
                        instances.Add(new(allocation,instance,Own(rd.UniformBufferCreate(bytes))));
                    constants.Add(allocation.Parameter.Index,instances.AsReadOnly());
                }
                return new OriginalMapPostEffect3ResourceBank(plan,textures,buffers,constants,owned,allowReadback);
            }
            catch{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);throw;}
        });
    }

    // Exact DXGI view codes returned by the installed native format callbacks.
    private static RenderingDevice.DataFormat ViewFormat(uint dxgi)=>dxgi switch{
        10=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,
        21=>RenderingDevice.DataFormat.D32SfloatS8Uint,
        26=>RenderingDevice.DataFormat.B10G11R11UfloatPack32,
        28=>RenderingDevice.DataFormat.R8G8B8A8Unorm,29=>RenderingDevice.DataFormat.R8G8B8A8Srgb,
        30=>RenderingDevice.DataFormat.R8G8B8A8Uint,
        34=>RenderingDevice.DataFormat.R16G16Sfloat,41=>RenderingDevice.DataFormat.R32Sfloat,
        42=>RenderingDevice.DataFormat.R32Uint,49=>RenderingDevice.DataFormat.R8G8Unorm,
        50=>RenderingDevice.DataFormat.R8G8Uint,54=>RenderingDevice.DataFormat.R16Sfloat,
        61=>RenderingDevice.DataFormat.R8Unorm,
        87=>RenderingDevice.DataFormat.B8G8R8A8Unorm,
        91=>RenderingDevice.DataFormat.B8G8R8A8Srgb,
        _=>throw new NotSupportedException($"3SPK native typed view DXGI{dxgi} has no explicit device format.")};

    public OriginalMapPostEffect3DeviceTexture RequireTexture(int index,int instance)
    {lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);return Require(Textures,index,instance,"texture");}}
    public OriginalMapPostEffect3DeviceBuffer RequireBuffer(int index,int instance)
    {lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);return Require(Buffers,index,instance,"buffer");}}
    public OriginalMapPostEffect3DeviceBuffer RequireBufferForIndirectArguments(int index,int instance)
    {
        var buffer=RequireBuffer(index,instance);
        throw new NotSupportedException(buffer.RequiresIndirectArgumentAdapter?
            "Native3SPK raw buffer requires a shared indirect-capable adapter before argument execution.":
            "Native3SPK buffer declaration does not expose an indirect-argument source role.");
    }
    public OriginalMapPostEffect3DeviceConstantBuffer RequireCurrentConstantBuffer(int index,int instance,long frame)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);var current=Require(ConstantBuffers,index,instance,"CB");
            if(!current.HasCompleteWriterContents||current.CompleteFrameNumber!=frame)
                throw new InvalidDataException("3SPK CB has no complete writer contents for the requested frame.");
            return current;
        }
    }
    public OriginalMapPostEffect3DeviceConstantBuffer RequireConstantBufferForPass(int index,int instance,int pass,long frame)
    {
        var requirements=OriginalMapPostEffect3ParameterBindingRequirements.Read(Source,pass)
            .Where(r=>r.ParameterIndex==index).ToArray();
        if(requirements.Length==0)throw new InvalidDataException("Selected3SPK pass does not bind this CB declaration.");
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);var target=Require(ConstantBuffers,index,instance,"CB");
            if(!(target.HasCompleteWriterContents&&target.CompleteFrameNumber==frame)&&
                !(target.PassScopedFrameNumber==frame&&target.PassScopedPassIndex==pass))
                throw new InvalidDataException("3SPK CB has no selected-pass writer proof for the requested frame.");
            return target;
        }
    }
    private static T Require<T>(IReadOnlyDictionary<int,IReadOnlyList<T>> table,int index,int instance,string role)
    {
        if(!table.TryGetValue(index,out var values))
            throw new InvalidDataException($"3SPK {role}{index} is not an owned allocation; no external fallback is implied.");
        if((uint)instance>=values.Count)throw new ArgumentOutOfRangeException(nameof(instance));return values[instance];
    }

    public Task UploadCompleteConstantBufferAsync(OriginalMapPostEffect3ParameterConstantBuffer packet,int instance)
    {
        ArgumentNullException.ThrowIfNull(packet);byte[] bytes=packet.Bytes.ToArray();
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var target=Require(ConstantBuffers,packet.Declaration.Index,instance,"CB");
            if(packet.SourceSha256!=Source.Source.Sha256||packet.OwnerIdentity!=Source.ContextOwnerIdentity||
                packet.FrameNumber<0||packet.WriterRva==0||bytes.Length!=target.Source.Parameter.ByteCount||
                !packet.Declaration.RawBytes.Span.SequenceEqual(target.Source.Parameter.RawBytes.Span))
                throw new InvalidDataException("3SPK CB writer belongs to another source/owner/declaration or incomplete extent.");
            accepted++;return UploadAcceptedAsync(packet,bytes,target);
        }
    }
    private async Task UploadAcceptedAsync(OriginalMapPostEffect3ParameterConstantBuffer packet,byte[] bytes,
        OriginalMapPostEffect3DeviceConstantBuffer target)
    {
        await serial.WaitAsync();
        try
        {
            if((target.CompleteFrameNumber is long previous&&packet.FrameNumber<previous)||
                (target.PassScopedFrameNumber is long scoped&&packet.FrameNumber<scoped))
                throw new InvalidDataException("3SPK CB upload cannot replace a newer complete frame.");
            var renderingTask=OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {var result=rd.BufferUpdate(target.StorageRid,0,checked((uint)bytes.Length),bytes);
             if(result!=Error.Ok)throw new InvalidDataException("Device rejected a complete3SPK CB writer upload.");return true;});
            await renderingTask;
            OriginalRenderThreadSchedulingDiagnostics.CallerResumed(renderingTask);
            lock(lifetime){target.CompleteFrameNumber=packet.FrameNumber;target.WriterRva=packet.WriterRva;
                target.UploadRevision=checked(target.UploadRevision+1);
                target.CompleteBytesSha256=Convert.ToHexString(SHA256.HashData(bytes));target.HasCompleteWriterContents=true;
                target.PassScopedFrameNumber=null;target.PassScopedPassIndex=null;target.PassKnownSourceByteCount=0;}
        }
        finally{serial.Release();lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();}
    }

    public Task UploadForPassAsync(OriginalMapPostEffect3ConstantBufferUpload upload,int instance)
    {
        ArgumentNullException.ThrowIfNull(upload);byte[] bytes=upload.BackendBytes.ToArray();bool[] known=upload.KnownSourceBytes.ToArray();
        var expected=OriginalMapPostEffect3ParameterBindingRequirements.Read(Source,upload.PassIndex)
            .Where(r=>r.ParameterIndex==upload.Declaration.Index).ToArray();
        static object Row(OriginalMapPostEffect3ParameterBindingRequirement r)=>
            (r.SourceRowIndex,r.ParameterIndex,r.StageCode,r.RegisterIndex,r.Program.Sha256,
                r.ShaderDeclaredName,r.ShaderDeclaredByteCount,string.Join(';',r.UsedVariables.Select(v=>$"{v.Name}/{v.Offset}/{v.ByteCount}")));
        if(expected.Length==0||!expected.Select(Row).SequenceEqual(upload.Requirements.Select(Row))||known.Length!=bytes.Length)
            throw new InvalidDataException("3SPK CB upload differs from this exact selected-pass interface.");
        foreach(var r in expected)foreach(var value in r.UsedVariables)
            for(int i=value.Offset;i<value.Offset+value.ByteCount;i++)
                if(i>=known.Length||!known[i])throw new InvalidDataException("Selected3SPK program reads an unknown CB byte.");
        for(int i=0;i<known.Length;i++)if(!known[i]&&bytes[i]!=0)
            throw new InvalidDataException("3SPK unknown padding is not the explicit zero backend representation.");
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);var target=Require(ConstantBuffers,upload.Declaration.Index,instance,"CB");
            if(upload.SourceSha256!=Source.Source.Sha256||upload.OwnerIdentity!=Source.ContextOwnerIdentity||upload.FrameNumber<0||
                bytes.Length!=target.Source.Parameter.ByteCount||!upload.Declaration.RawBytes.Span.SequenceEqual(target.Source.Parameter.RawBytes.Span))
                throw new InvalidDataException("3SPK pass CB upload belongs to another source/owner/declaration.");
            accepted++;return UploadPassAcceptedAsync(upload,bytes,known.Count(v=>v),target);
        }
    }
    private async Task UploadPassAcceptedAsync(OriginalMapPostEffect3ConstantBufferUpload upload,byte[] bytes,int known,
        OriginalMapPostEffect3DeviceConstantBuffer target)
    {
        await serial.WaitAsync();
        try
        {
            if((target.CompleteFrameNumber is long complete&&upload.FrameNumber<complete)||
                (target.PassScopedFrameNumber is long previous&&upload.FrameNumber<previous))
                throw new InvalidDataException("3SPK pass CB upload cannot replace a newer writer frame.");
            var renderingTask=OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {var result=rd.BufferUpdate(target.StorageRid,0,checked((uint)bytes.Length),bytes);
             if(result!=Error.Ok)throw new InvalidDataException("Device rejected a selected-pass3SPK CB upload.");return true;});
            await renderingTask;
            OriginalRenderThreadSchedulingDiagnostics.CallerResumed(renderingTask);
            lock(lifetime)
            {
                target.UploadRevision=checked(target.UploadRevision+1);
                target.HasCompleteWriterContents=false;target.CompleteFrameNumber=null;target.WriterRva=null;target.CompleteBytesSha256=null;
                target.PassScopedFrameNumber=upload.FrameNumber;target.PassScopedPassIndex=upload.PassIndex;target.PassKnownSourceByteCount=known;
            }
        }
        finally{serial.Release();lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();}
    }

    public ValueTask DisposeAsync()
    {
        lock(lifetime)
        {
            if(disposal is not null)return new(disposal);retiring=true;
            Task pending=accepted==0?Task.CompletedTask:(drained=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            return new(disposal=RetireAsync(pending));
        }
    }
    private async Task RetireAsync(Task pending)
    {
        await pending;await serial.WaitAsync();
        try{await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);return true;});OwnedRidCount=0;}
        finally{serial.Release();}
    }
}
