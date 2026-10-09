using Godot;
using San14.Data;

/// <summary>Two retained source DOF weight textures, matching E21B80. Each
/// setup uploads the exact E1F4E0 CPU LUT into the selected texture. The alpha
/// view adapts native A8_UNORM onto R8 storage without changing shader reads.</summary>
public sealed class MapFinalOutputDofWeightOwner:IAsyncDisposable
{
    public LoadedMapFinalOutputResources Source { get; }
    public int ActiveTextureIndex { get; private set; }
    public long SuccessfulDrawCount { get; private set; }
    public long UploadCount { get; private set; }
    public long AcceptedFrameNumber { get; private set; }
    public MapFinalOutputDofWeightPacket? LastPacket { get; private set; }
    /// <summary>Native96AFC0 resets its complete key then installs1415/0030
    /// for one mip. INPUT kind5 retains this producer key during SPK binding.</summary>
    public ReadOnlyMemory<byte> NativeSamplerKey=>Convert.FromHexString(MapFinalOutputDofWeight.ProducerSamplerKeyHex);
    public byte[] SamplerDescriptor=>RenderSamplerSourceAdapters.DecodeKey(Source.StockScreen.Native,NativeSamplerKey.Span).EncodeD3D11Descriptor();
    private readonly Rid[] _textures,_views;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();
    private int _accepted;private bool _retiring;private Task? _disposal;private TaskCompletionSource? _drained;
    private MapFinalOutputDofWeightOwner(LoadedMapFinalOutputResources source,Rid[] textures,Rid[] views)
        =>(Source,_textures,_views)=(source,textures,views);

    public static async Task<MapFinalOutputDofWeightOwner> BuildAsync(LoadedMapFinalOutputResources source)
    {
        MapFinalOutputDofWeight.ValidateSource(source);
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var storage=new Rid[2];var views=new Rid[2];
            try
            {
                for(int i=0;i<2;i++)
                {
                    using var format=new RDTextureFormat{Format=RenderingDevice.DataFormat.R8Unorm,
                        TextureType=RenderingDevice.TextureType.Type2D,Width=MapFinalOutputDofWeight.Width,
                        Height=1,Depth=1,ArrayLayers=1,Mipmaps=1,
                        UsageBits=RenderingDevice.TextureUsageBits.SamplingBit|RenderingDevice.TextureUsageBits.CanUpdateBit};
                    using var plain=new RDTextureView();storage[i]=rd.TextureCreate(format,plain);
                    if(!storage[i].IsValid)throw new InvalidDataException("Native DOF weight storage allocation failed.");
                    // Native format15 tables resolve DXGI65/A8_UNORM; sourcePS0
                    // reads t2.w. A default R8 view would return an alpha of1.
                    using var alpha=new RDTextureView{SwizzleR=RenderingDevice.TextureSwizzle.Zero,
                        SwizzleG=RenderingDevice.TextureSwizzle.Zero,SwizzleB=RenderingDevice.TextureSwizzle.Zero,
                        SwizzleA=RenderingDevice.TextureSwizzle.R};
                    views[i]=rd.TextureCreateShared(alpha,storage[i]);
                    if(!views[i].IsValid)throw new InvalidDataException("Native DOF weight alpha sampled view allocation failed.");
                }
                return new MapFinalOutputDofWeightOwner(source,storage,views);
            }
            catch{for(int i=1;i>=0;i--){if(views[i].IsValid)rd.FreeRid(views[i]);if(storage[i].IsValid)rd.FreeRid(storage[i]);}throw;}
        });
    }

    /// <summary>The lease pins its uploaded image through the DOFMerge draw.
    /// Pass its Binding under SPK texture index30 and its CommitDrawSuccess as
    /// the runner's successful-pass callback, then dispose after that draw.</summary>
    public Task<PreparedWeight> PrepareAndBorrowAsync(NativeMapFrameContext frame,
        MapFinalOutputDofWeightPacket packet)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(packet);
        if(frame.FrameNumber!=packet.FrameNumber||frame.PreparedCamera!=packet.Camera||
            frame.TerrainFrame.Source.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
            frame.TerrainFrame.Source.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256)
            throw new InvalidDataException("DOF weight packet differs from the accepted scene frame/Camera/source graph.");
        return PrepareAndBorrowAsync(packet);
    }

    /// <summary>Explicit prepared source entry point. Failed setup/draw keeps
    /// the active texture index; only CommitDrawSuccess applies E1D69E's toggle.</summary>
    public Task<PreparedWeight> PrepareAndBorrowAsync(MapFinalOutputDofWeightPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if(packet.Bytes.Length!=512||packet.FrameNumber<=0||
            packet.SourceResourceUri!=MapFinalOutputDofWeight.SourceUri(Source))
            throw new InvalidDataException("DOF weight packet does not belong to this source producer.");
        lock(_lifetime){ObjectDisposedException.ThrowIf(_retiring,this);_accepted++;}
        return PrepareAcceptedAsync(packet);
    }
    private async Task<PreparedWeight> PrepareAcceptedAsync(MapFinalOutputDofWeightPacket packet)
    {
        bool acquired=false;
        try
        {
            await _gate.WaitAsync();acquired=true;
            if(packet.FrameNumber<=AcceptedFrameNumber)
                throw new InvalidDataException("DOF weight requires a new accepted frame after each successful draw.");
            int index=ActiveTextureIndex;
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                if(!rd.TextureIsValid(_textures[index])||!rd.TextureIsValid(_views[index])||
                    rd.TextureUpdate(_textures[index],0,packet.Bytes.ToArray())!=Error.Ok)
                    throw new InvalidDataException("Native DOF weight active texture upload failed.");
                return true;
            });
            UploadCount++;
            return new PreparedWeight(this,index,packet,new("Texture",0,_views[index],_textures[index],packet.SourceResourceUri));
        }
        catch{if(acquired)_gate.Release();ReleaseAccepted();throw;}
    }
    private void ReleaseAccepted()
    {lock(_lifetime){_accepted--;if(_retiring&&_accepted==0)_drained?.TrySetResult();}}

    public sealed class PreparedWeight:IAsyncDisposable
    {
        private readonly MapFinalOutputDofWeightOwner _owner;
        private readonly MapFinalOutputDofWeightPacket _packet;
        private readonly object _sync=new();private bool _committed,_disposed;
        internal PreparedWeight(MapFinalOutputDofWeightOwner owner,int index,
            MapFinalOutputDofWeightPacket packet,ModelDeviceBinding binding)
            =>(_owner,TextureIndex,_packet,Binding)=(owner,index,packet,binding);
        public const int SpkTextureIndex=30;
        public int TextureIndex { get; }
        public ModelDeviceBinding Binding { get; }
        public ReadOnlyMemory<byte> NativeSamplerKey=>_owner.NativeSamplerKey;
        public byte[] SamplerDescriptor=>_owner.SamplerDescriptor;
        public void CommitDrawSuccess()
        {
            lock(_sync)
            {
                ObjectDisposedException.ThrowIf(_disposed,this);
                if(_committed||_owner.ActiveTextureIndex!=TextureIndex)
                    throw new InvalidDataException("DOF weight draw commit is duplicated or differs from its active source texture.");
                _owner.ActiveTextureIndex=1-TextureIndex;
                _owner.SuccessfulDrawCount++;_owner.AcceptedFrameNumber=_packet.FrameNumber;
                _owner.LastPacket=_packet;_committed=true;
            }
        }
        public ValueTask DisposeAsync()
        {
            lock(_sync){if(_disposed)return ValueTask.CompletedTask;_disposed=true;
                _owner._gate.Release();_owner.ReleaseAccepted();return ValueTask.CompletedTask;}
        }
    }

    public ValueTask DisposeAsync()
    {lock(_lifetime){_retiring=true;return new(_disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {
        Task pending;lock(_lifetime)pending=_accepted==0?Task.CompletedTask:
            (_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        await pending;await _gate.WaitAsync();
        try{await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {for(int i=1;i>=0;i--){rd.FreeRid(_views[i]);rd.FreeRid(_textures[i]);}return true;});}
        finally{_gate.Release();}
    }
}
