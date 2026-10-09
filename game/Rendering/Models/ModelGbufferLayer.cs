using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Explicit image/view and exact native sampler recipe. Source stage
/// roles and proxy image selectors are supplied by a verified source resolver.</summary>
public sealed record ModelGbufferTextureUse(string DescriptorKind, uint SourceBindPoint,
    LoadedTextureContainer Source, int ImageIndex, uint NativeViewFormat, ReadOnlyMemory<byte> SamplerDescriptor52);
public sealed record ModelGbufferStagePacket(IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> Constants,
    IReadOnlyList<ModelGbufferTextureUse> OwnedTextures,
    IReadOnlyList<ModelDeviceBinding> BorrowedResources);
public sealed record ModelGbufferInstancePacket(string InstanceKey, LoadedModelGeometry Model,
    LoadedShaderContainer Shader, int ExplicitCombinationIndex, int DrawIndex,
    ModelGbufferStagePacket Vertex, ModelGbufferStagePacket Pixel,
    ModelRasterInputs Raster)
{
    public ArmySkinVertexProfile? ArmySkinProfile { get; init; }
}

/// <summary>Scene owner for shared original model programs, raw geometry,
/// exact texture mip uploads and native samplers. Frame-dependent pipelines
/// are replaced when the supplied terrain framebuffer changes. Every camera,
/// matrix, material, stage role, variant and raster value remains explicit.</summary>
public sealed class ModelGbufferLayer : IAsyncDisposable
{
    private sealed record ProgramKey(string Identity,int Combination,ArmySkinVertexProfile? ArmyProfile);
    private sealed record GeometryKey(string Identity,string Inputs);
    private sealed record TextureKey(string Identity,int Image,uint View);
    private sealed record DrawValue(ModelShaderProgram Program,ModelGeometryDevice Geometry,
        int DrawIndex,RenderTargetFrame Frame,ModelRasterInputs Raster,
        ModelDeviceBinding[] VertexResources,ModelDeviceBinding[] PixelResources,ModelDraw Draw);
    private sealed record PendingDraw(string Key,DrawValue Value,ModelStageInputs Vertex,
        ModelStageInputs Pixel,bool Reused);
    private readonly Vkd3dShaderCompiler _compiler;
    private readonly bool _ownsCompiler;
    private readonly Dictionary<ProgramKey,ModelShaderProgram> _programs=[];
    private readonly Dictionary<GeometryKey,ModelGeometryDevice> _geometry=[];
    private readonly Dictionary<(string Model,string Vertex),ModelGeometryDevice> _geometryViews=[];
    private readonly Dictionary<string,LoadedRdbAsset> _sourceSnapshots=new(StringComparer.Ordinal);
    private readonly Dictionary<string,LoadedModelGeometry> _modelSnapshots=new(StringComparer.Ordinal);
    private readonly Dictionary<string,LoadedShaderContainer> _shaderSnapshots=new(StringComparer.Ordinal);
    private readonly Dictionary<string,LoadedTextureContainer> _textureSnapshots=new(StringComparer.Ordinal);
    private readonly object _sourceGate=new();
    private static readonly ConditionalWeakTable<LoadedRdbAsset,SourceKey> SourceKeys=new();
    private sealed record SourceKey(string Identity);
    private readonly Dictionary<TextureKey,ModelTextureDevice> _textures=[];
    private readonly Dictionary<string,Rid> _samplers=[];
    private Dictionary<string,DrawValue> _draws=[];
    private readonly Dictionary<int,HashSet<string>> _queueDrawKeys=[];
    private RenderTargetFrame? _queueCacheTarget;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetimeGate=new();
    private int _acceptedOperations;
    private bool _retiring;
    private Task? _disposal;
    private TaskCompletionSource? _drained;
    public long AcceptedFrameCount { get; private set; }
    public int ProgramCount=>_programs.Count;
    public int GeometryCount=>_geometry.Count;
    public int TextureCount=>_textures.Count;
    public int SamplerCount=>_samplers.Count;
    public int DrawCount=>_draws.Count;
    public long DrawCreationCount { get; private set; }
    public ModelConstantUpdateStatistics LastConstantUpdates { get; private set; }
    public long ConstantBufferUpdateCount { get; private set; }
    public long ConstantBufferUpdatedBytes { get; private set; }
    public long FrameBatchDispatchCount { get; private set; }
    public ModelGeometrySubmittedFrame? LastSubmittedFrame { get; private set; }

    private ModelGbufferLayer(Vkd3dShaderCompiler compiler,bool ownsCompiler)
        =>(_compiler,_ownsCompiler)=(compiler,ownsCompiler);

    /// <summary>The borrowed compiler remains alive until layer retirement,
    /// allowing on-demand source programs when cities change visual category.</summary>
    public static ModelGbufferLayer Create(Vkd3dShaderCompiler borrowedCompiler)
    { ArgumentNullException.ThrowIfNull(borrowedCompiler);return new(borrowedCompiler,false); }
    public static ModelGbufferLayer CreateWithOwnedCompiler()=>new(ShaderCompilerRuntime.Open(),true);

    /// <summary>Call within the native scene's accepted frame callback, after
    /// terrain and before deferred. This preserves all terrain color/depth
    /// contents. Borrowed stage resources and the frame outlive cached draws;
    /// call RetireTargetAsync before independently freeing the frame.</summary>
    public Task<ModelGeometrySubmittedFrame> RenderAsync(RenderTargetFrame terrainFrame,
        IReadOnlyList<ModelGbufferInstancePacket> explicitInstances)
        => RenderSnapshotAsync(terrainFrame, explicitInstances, null);

    /// <summary>Preserves this caller's real six MRT/E13 contents and submits
    /// packets in the supplied order. Native queue ordering is certified by
    /// the Army queue owner, never inferred by this device layer.</summary>
    public Task<ModelGeometrySubmittedFrame> RenderArmyQueueAsync(Army48TargetBinding actualArmyTarget,
        int sourceQueueNode, IReadOnlyList<ModelGbufferInstancePacket> actualOrderedInstances, Action requireActualCurrentQueue)
    {
        ArgumentNullException.ThrowIfNull(actualArmyTarget); actualArmyTarget.RequireCurrent();
        if(sourceQueueNode is not(49 or 50))throw new InvalidDataException("Army retained queue cache requires original node49/50.");
        ArgumentNullException.ThrowIfNull(requireActualCurrentQueue); requireActualCurrentQueue();
        return RenderSnapshotAsync(actualArmyTarget.Frame, actualOrderedInstances, actualArmyTarget, requireActualCurrentQueue, sourceQueueNode);
    }

    /// <summary>Retains the separate original static queue54/55 draw caches
    /// on one actual source node53 five-MRT target. The caller certifies its
    /// source order/current scope; the ordinary five-output program stays exact.</summary>
    public Task<ModelGeometrySubmittedFrame> RenderOrdinaryWorldQueueAsync(RenderTargetFrame actualSource53,
        int sourceQueueNode, IReadOnlyList<ModelGbufferInstancePacket> actualOrderedInstances,
        Action requireActualCurrentQueue)
    {
        ArgumentNullException.ThrowIfNull(actualSource53); ArgumentNullException.ThrowIfNull(requireActualCurrentQueue);
        if(sourceQueueNode is not(54 or 55) || actualSource53.Source.Node.Index!=53 || actualSource53.Source.Flag!=0x46 ||
            actualSource53.ColorViews.Count!=5 || actualSource53.Source.DepthObjectNameHash!=0xE13DDB4F ||
            actualSource53.Source.DepthSurfaceId!=0 || actualSource53.Source.Graph.Source.DecodedSha256!=ArmyWorldGraphicsTargetSource.GraphSha256)
            throw new InvalidDataException("Static retained queue cache requires original node53 five MRT/E13 and queue54/55.");
        requireActualCurrentQueue();
        return RenderSnapshotAsync(actualSource53,actualOrderedInstances,null,requireActualCurrentQueue,sourceQueueNode);
    }

    private Task<ModelGeometrySubmittedFrame> RenderSnapshotAsync(RenderTargetFrame terrainFrame,
        IReadOnlyList<ModelGbufferInstancePacket> explicitInstances, Army48TargetBinding? army48,
        Action? requireActualCurrentQueue = null, int? sourceQueueNode = null)
    {
        ArgumentNullException.ThrowIfNull(terrainFrame);ArgumentNullException.ThrowIfNull(explicitInstances);
        AcceptOperation();
        LastSubmittedFrame=null;
        // Take mutable CB/sampler/list inputs before the caller can reuse them.
        try
        {
            ModelGbufferInstancePacket[] snapshot;
            lock(_sourceGate)
            {
                var verified=new HashSet<(string Identity,byte[] Bytes)>();
                snapshot=explicitInstances.Select(p=>Snapshot(p,verified)).ToArray();
            }
            if(snapshot.Select(p=>p.InstanceKey).Distinct(StringComparer.Ordinal).Count()!=snapshot.Length)
                throw new InvalidDataException("Original model layer requires unique instance/draw keys within a frame.");
            return RenderAcceptedAsync(terrainFrame,snapshot,army48,requireActualCurrentQueue,sourceQueueNode);
        }
        catch{CompleteOperation();throw;}
    }

    private async Task<ModelGeometrySubmittedFrame> RenderAcceptedAsync(RenderTargetFrame frame,
        ModelGbufferInstancePacket[] instances, Army48TargetBinding? army48, Action? requireActualCurrentQueue,
        int? sourceQueueNode)
    {
        await _gate.WaitAsync();
        try
        {
            army48?.RequireCurrent();
            requireActualCurrentQueue?.Invoke();
            if(frame.ColorViews.Count!=(army48 is null?5:6))throw new InvalidDataException("Original model G-buffer attachment count differs from its selected five-MRT or Army node48 contract.");
            var target=new ModelDrawTarget(frame.Framebuffer,frame.ColorViews,frame.DepthView.IsValid?frame.DepthView:null,
                frame.Textures.Values.Select(t=>t.StorageRid).Distinct().ToArray(),frame.Source.Graph.Source.ResourceUri);
            var next=new List<PendingDraw>();
            try
            {
                foreach(var instance in instances)
                {
                    string cacheKey=sourceQueueNode is int currentQueue
                        ?"source-queue/"+currentQueue+"/"+instance.InstanceKey:instance.InstanceKey;
                    var program=await ProgramAsync(instance);
                    if(army48 is not null)Army48GraphicsContract.RequireProgram(program);
                    var drawTarget=army48 is null?target:
                        await frame.PrepareArmyDeviceDrawTargetAsync(army48,program.ColorOutputLocations);
                    var geometry=await GeometryAsync(instance.Model,program,instance.DrawIndex,instance.ArmySkinProfile);
                    var vertex=await StageAsync(instance.Vertex);var pixel=await StageAsync(instance.Pixel);
                    ValidateConstants(program.VertexBindings,vertex.Constants);ValidateConstants(program.PixelBindings,pixel.Constants);
                    bool same=_draws.TryGetValue(cacheKey,out var old) && ReferenceEquals(old.Program,program) &&
                        ReferenceEquals(old.Geometry,geometry) && ReferenceEquals(old.Frame,frame) && old.DrawIndex==instance.DrawIndex &&
                        SameRaster(old.Raster,instance.Raster) && old.VertexResources.SequenceEqual(vertex.Resources) && old.PixelResources.SequenceEqual(pixel.Resources);
                    DrawValue value;
                    if(same)value=old!;
                    else
                    {
                        var draw=army48 is null
                            ?await ModelDraw.BuildAsync(program,geometry,instance.DrawIndex,drawTarget,vertex,pixel,instance.Raster)
                            :await ModelDraw.BuildArmy48Async(program,geometry,instance.DrawIndex,drawTarget,vertex,pixel,instance.Raster,army48);
                        DrawCreationCount++;
                        value=new(program,geometry,instance.DrawIndex,frame,instance.Raster,vertex.Resources.ToArray(),pixel.Resources.ToArray(),draw);
                    }
                    next.Add(new(cacheKey,value,vertex,pixel,same));
                }
                var updates=next.Where(p=>p.Reused).Select(p=>(p.Value.Draw,
                    Constants:p.Value.Draw.SnapshotUpdate(p.Vertex.Constants,p.Pixel.Constants))).ToArray();
                var written=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
                {
                    army48?.RequireCurrent();
                    requireActualCurrentQueue?.Invoke();
                    if(!rd.FramebufferIsValid(frame.Framebuffer))throw new InvalidDataException("Original model batch target is no longer live.");
                    foreach(var item in next)
                    {
                        item.Value.Draw.RequireDrawLive();
                        if(army48 is not null)frame.RequireArmyDeviceDrawTarget(item.Value.Draw.Target,item.Value.Draw.Program.ColorOutputLocations);
                        else if(item.Value.Draw.Target.Framebuffer!=frame.Framebuffer)
                            throw new InvalidDataException("Original model batch mixes framebuffer identities.");
                    }
                    ModelConstantUpdateStatistics total=default;
                    foreach(var update in updates)
                        total+=update.Draw.UpdateOnRenderThread(rd,update.Constants.Vertex,update.Constants.Pixel);
                    if(next.Count!=0)
                    {
                        // Every source resource is disjoint from all target
                        // parents, and order is unchanged. One render pass can
                        // preserve the terrain and execute the whole collection.
                        // Keep exact primitive order. A different source PS
                        // output mask selects a different retained subpass over
                        // the same physical views; consecutive equal masks
                        // retain the normal single-pass batch.
                        Rid framebuffer=default;long list=0;bool listOpen=false;
                        try
                        {
                            foreach(var item in next)
                            {
                                Rid selected=item.Value.Draw.Target.Framebuffer;
                                if(!listOpen||selected!=framebuffer)
                                {
                                    if(listOpen){rd.DrawListEnd();listOpen=false;}
                                    list=rd.DrawListBegin(selected,RenderingDevice.DrawFlags.DefaultAll,[],1,0);
                                    framebuffer=selected;listOpen=true;
                                }
                                item.Value.Draw.DrawInList(rd,list);
                            }
                        }
                        finally{if(listOpen)rd.DrawListEnd();}
                    }
                    return total;
                });
                LastConstantUpdates=written;ConstantBufferUpdateCount+=written.BufferUpdates;
                ConstantBufferUpdatedBytes+=written.UpdatedBytes;FrameBatchDispatchCount++;
            }
            catch { foreach(var item in next.Where(p=>!p.Reused).Reverse())await item.Value.Draw.DisposeAsync();throw; }
            var prior=_draws;
            if(sourceQueueNode is int queue)
            {
                var combined=ReferenceEquals(_queueCacheTarget,frame)
                    ?new Dictionary<string,DrawValue>(prior,StringComparer.Ordinal):new(StringComparer.Ordinal);
                if(!ReferenceEquals(_queueCacheTarget,frame))_queueDrawKeys.Clear();
                if(_queueDrawKeys.TryGetValue(queue,out var previousKeys))
                    foreach(string key in previousKeys)combined.Remove(key);
                foreach(var item in next)combined[item.Key]=item.Value;
                _queueDrawKeys[queue]=next.Select(p=>p.Key).ToHashSet(StringComparer.Ordinal);
                _queueCacheTarget=frame;_draws=combined;
            }
            else
            { _queueDrawKeys.Clear();_queueCacheTarget=null;_draws=next.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal); }
            var retained=_draws.Values.ToHashSet(ReferenceEqualityComparer.Instance);
            foreach(var item in prior.Values.Where(p=>!retained.Contains(p)).Reverse())await item.Draw.DisposeAsync();
            AcceptedFrameCount++;
            return LastSubmittedFrame=new(this,frame,AcceptedFrameCount,instances);
        }
        finally{_gate.Release();CompleteOperation();}
    }

    private async Task<ModelShaderProgram> ProgramAsync(ModelGbufferInstancePacket instance)
    {
        var key=new ProgramKey(SourceIdentity(instance.Shader.Source),instance.ExplicitCombinationIndex,instance.ArmySkinProfile);
        if(_programs.TryGetValue(key,out var found))return found;
        var value=await ModelShaderProgram.BuildAsync(instance.Shader,instance.ExplicitCombinationIndex,_compiler,instance.ArmySkinProfile);
        try
        {
            if(!value.ColorOutputLocations.SequenceEqual(new uint[]{0,1,2,3,4}) ||
                value.PixelBindings.Interface.StageVariables.Where(v=>v.StorageClass==3).Any(v=>
                    v.Type is not {Kind:"vector",ComponentCount:4,ComponentType:{Kind:"float",Bits:32}}))
                throw new InvalidDataException("Original model layer source program differs from the five float4 G-buffer contract.");
            _programs.Add(key,value);return value;
        }
        catch{await value.DisposeAsync();throw;}
    }

    private async Task<ModelGeometryDevice> GeometryAsync(LoadedModelGeometry source,ModelShaderProgram program,
        int drawIndex,ArmySkinVertexProfile? armyProfile)
    {
        var viewKey=(SourceIdentity(source.Source),program.VertexAdapter.AdaptedSha256);
        if(_geometryViews.TryGetValue(viewKey,out var cached))return cached;
        // Only canonical geometry from the exact source bytes participates in
        // input resolution; mutable caller metadata cannot alter cached uploads.
        var canonical=G1ModelGeometryReader.Read(source.Source.Bytes,source.Source.Bytes.Length);
        var resolution=ModelVertexInputs.Resolve(canonical,program.VertexDeviceTransportSignatures,program.VertexBindings.Interface);
        var singleInstanceStreams=armyProfile is null?null:ArmySkinVertexAdapter.SingleInstanceSourceStreams(canonical,drawIndex);
        byte[] encoded=System.Text.Encoding.UTF8.GetBytes(string.Join(';',resolution.Bindings.OrderBy(b=>b.LayoutIndex).ThenBy(b=>b.Location)
            .Select(b=>$"{b.LayoutIndex}/{b.SourceSemanticCode}/{b.Location}"))+";instance:"+
            string.Join(',',singleInstanceStreams is null?Enumerable.Empty<int>():singleInstanceStreams.Order()));
        var key=new GeometryKey(SourceIdentity(source.Source),Convert.ToHexString(SHA256.HashData(encoded)));
        if(_geometry.TryGetValue(key,out var found)){_geometryViews.Add(viewKey,found);return found;}
        var packet=ModelDeviceGeometry.Prepare(source.Source.Bytes,source.Source.DecodedSha256,resolution.Bindings,singleInstanceStreams);
        var value=await ModelGeometryDevice.BuildAsync(source.Source.ResourceUri,packet);
        _geometry.Add(key,value);_geometryViews.Add(viewKey,value);return value;
    }

    private async Task<ModelStageInputs> StageAsync(ModelGbufferStagePacket packet)
    {
        var inputs=new List<ModelDeviceBinding>(packet.BorrowedResources);
        foreach(var use in packet.OwnedTextures)
        {
            if(use.DescriptorKind=="Texture")
            {
                var key=new TextureKey(SourceIdentity(use.Source.Source),use.ImageIndex,use.NativeViewFormat);
                if(!_textures.TryGetValue(key,out var texture))
                {texture=await ModelTextureDevice.BuildAsync(use.Source,use.ImageIndex,use.NativeViewFormat);_textures.Add(key,texture);}
                inputs.Add(new("Texture",use.SourceBindPoint,texture.TextureRid,texture.TextureRid,texture.SourceResourceUri));
            }
            else if(use.DescriptorKind=="Sampler")
            {
                byte[] bytes=use.SamplerDescriptor52.ToArray();
                if(bytes.Length!=52)throw new InvalidDataException("Original model owned sampler requires its complete native52B descriptor.");
                string key=Convert.ToHexString(SHA256.HashData(bytes));
                if(!_samplers.TryGetValue(key,out var sampler))
                {sampler=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>DeviceSampler.Create(rd,bytes));_samplers.Add(key,sampler);}
                inputs.Add(new("Sampler",use.SourceBindPoint,sampler,null,use.Source.Source.ResourceUri+"#sampler/"+key));
            }
            else throw new InvalidDataException("Original model owned image use must be an explicit Texture or Sampler source declaration.");
        }
        return new(packet.Constants,Array.AsReadOnly(inputs.ToArray()));
    }

    private ModelGbufferInstancePacket Snapshot(ModelGbufferInstancePacket packet,
        HashSet<(string Identity,byte[] Bytes)> verified)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if(string.IsNullOrWhiteSpace(packet.InstanceKey)||packet.Model is null||packet.Shader is null||packet.Raster is null||packet.Raster.Attachments is null)
            throw new InvalidDataException("Original model frame instance has missing source identities or raster fields.");
        var modelSource=SourceSnapshot(packet.Model.Source,verified);string modelKey=SourceIdentity(modelSource);
        if(!_modelSnapshots.TryGetValue(modelKey,out var model))
            _modelSnapshots.Add(modelKey,model=new(G1ModelGeometryReader.Read(modelSource.Bytes,modelSource.Bytes.LongLength),modelSource));
        var shaderSource=SourceSnapshot(packet.Shader.Source,verified);string shaderKey=SourceIdentity(shaderSource);
        if(!_shaderSnapshots.TryGetValue(shaderKey,out var shader))
            _shaderSnapshots.Add(shaderKey,shader=new(G2ShaderContainerReader.Read(shaderSource.Bytes),shaderSource));
        ModelGbufferStagePacket Stage(ModelGbufferStagePacket input)
        {
            ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.Constants);
            ArgumentNullException.ThrowIfNull(input.OwnedTextures);ArgumentNullException.ThrowIfNull(input.BorrowedResources);
            return new(input.Constants.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray()),
                Array.AsReadOnly(input.OwnedTextures.Select(t=>
                {
                    var source=SourceSnapshot(t.Source.Source,verified);string key=SourceIdentity(source);
                    if(!_textureSnapshots.TryGetValue(key,out var texture))
                        _textureSnapshots.Add(key,texture=new(G1TextureContainerReader.Read(source.Bytes),source));
                    return t with{Source=texture,SamplerDescriptor52=t.SamplerDescriptor52.ToArray()};
                }).ToArray()),
                Array.AsReadOnly(input.BorrowedResources.ToArray()));
        }
        var vertex=Stage(packet.Vertex);var pixel=Stage(packet.Pixel);
        if(packet.ArmySkinProfile is { } armyProfile)
        {
            var canonical=model.Geometry;
            if(ArmySkinVertexAdapter.Select(canonical,packet.DrawIndex)!=armyProfile||
                !vertex.Constants.TryGetValue(0,out var armyVertex))
                throw new InvalidDataException("Army backend input transport differs from its exact source layout or complete vertex buffer.");
            ArmySkinVertexAdapter.ValidateGates(armyVertex.Span,armyProfile);
        }
        return packet with{Model=model,Shader=shader,Vertex=vertex,Pixel=pixel,
            Raster=packet.Raster with{Attachments=Array.AsReadOnly(packet.Raster.Attachments.ToArray())}};
    }

    private LoadedRdbAsset SourceSnapshot(LoadedRdbAsset source,HashSet<(string Identity,byte[] Bytes)> verified)
    {
        string key=SourceIdentity(source);
        if(!_sourceSnapshots.TryGetValue(key,out var owned))
        {
            byte[] bytes=source.Bytes.ToArray();
            if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(source.DecodedSha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Original model cached source bytes differ from their declared full revision.");
            _sourceSnapshots.Add(key,owned=source with{Bytes=bytes});
        }
        if(verified.Add((key,source.Bytes))&&!source.Bytes.AsSpan().SequenceEqual(owned.Bytes))
            throw new InvalidDataException("Caller changed an immutable model/program/texture source after cache creation.");
        return owned;
    }
    private static string SourceIdentity(LoadedRdbAsset source)=>SourceKeys.GetValue(source,static s=>new(string.Join("|",
        s.ResourceUri,s.DecodedSha256,s.FileId.ToString("X8"),s.Bytes.LongLength,s.IndexSource.ResourceUri,
        s.IndexSource.PhysicalPath,s.IndexSource.Sha256,s.OverrideSource?.ResourceUri,s.OverrideSource?.PhysicalPath,s.OverrideSource?.Sha256))).Identity;

    private static void ValidateConstants(TranslatedShaderBindings bindings,IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> input)
    {
        var cbs=bindings.Bindings.Where(b=>b.DescriptorKind=="UniformBuffer").ToArray();
        if(cbs.Length!=input.Count||cbs.Any(b=>!input.TryGetValue(b.Source.BindPoint,out var bytes)||bytes.Length!=b.ConstantBuffer!.ByteSize))
            throw new InvalidDataException("Original model layer requires each complete selected source CB before updating the frame.");
    }
    private static bool SameRaster(ModelRasterInputs a,ModelRasterInputs b)=>a.Cull==b.Cull&&a.FrontFace==b.FrontFace&&
        a.DepthTest==b.DepthTest&&a.DepthWrite==b.DepthWrite&&a.DepthCompare==b.DepthCompare&&a.Attachments.SequenceEqual(b.Attachments)&&
        (a.NativeStencil is null ? b.NativeStencil is null : a.NativeStencil.SamePipeline(b.NativeStencil));

    /// <summary>Retires framebuffer-dependent draws while retaining shared
    /// source programs/geometry/textures. Await before the caller frees targets
    /// or borrowed resources; the next accepted frame constructs fresh draws.</summary>
    public Task RetireTargetAsync()
    {AcceptOperation();return RetireTargetAcceptedAsync();}
    private async Task RetireTargetAcceptedAsync()
    {
        await _gate.WaitAsync();
        try{LastSubmittedFrame=null;foreach(var value in _draws.Values.Reverse())await value.Draw.DisposeAsync();_draws.Clear();_queueDrawKeys.Clear();_queueCacheTarget=null;}
        finally{_gate.Release();CompleteOperation();}
    }

    private void AcceptOperation()
    {lock(_lifetimeGate){ObjectDisposedException.ThrowIf(_retiring,this);_acceptedOperations++;}}
    private void CompleteOperation()
    {lock(_lifetimeGate){if(--_acceptedOperations==0&&_retiring)_drained?.TrySetResult();}}
    public ValueTask DisposeAsync()
    {
        lock(_lifetimeGate)
        {
            if(_disposal is not null)return new(_disposal);
            _retiring=true;
            LastSubmittedFrame=null;
            Task pending=_acceptedOperations==0?Task.CompletedTask:(_drained=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);_disposal=completion.Task;
            _=DisposeCoreAsync(pending,completion);return new(_disposal);
        }
    }
    private async Task DisposeCoreAsync(Task pending,TaskCompletionSource completion)
    {
        try
        {
            await pending;await _gate.WaitAsync();
            try
            {
                foreach(var value in _draws.Values.Reverse())await value.Draw.DisposeAsync();_draws.Clear();_queueDrawKeys.Clear();_queueCacheTarget=null;
                foreach(var value in _geometry.Values.Reverse())await value.DisposeAsync();_geometry.Clear();_geometryViews.Clear();
                foreach(var value in _programs.Values.Reverse())await value.DisposeAsync();_programs.Clear();
                foreach(var value in _textures.Values.Reverse())await value.DisposeAsync();_textures.Clear();
                await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{foreach(var rid in _samplers.Values.Reverse())rd.FreeRid(rid);return true;});_samplers.Clear();
                lock(_sourceGate){_textureSnapshots.Clear();_shaderSnapshots.Clear();_modelSnapshots.Clear();_sourceSnapshots.Clear();}
                if(_ownsCompiler)_compiler.Dispose();
            }
            finally{_gate.Release();}
            completion.TrySetResult();
        }
        catch(Exception error){completion.TrySetException(error);}
    }
}
