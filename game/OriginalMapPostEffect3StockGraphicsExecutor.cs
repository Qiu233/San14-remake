using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>Actual currently selected single color target. Unlike sampled-input
/// leases, this does not require initialized pixels. The producer must prove the
/// original target setter/inheritance for this exact pass, frontend and attempt.</summary>
public interface IOriginalMapPostEffect3StockGraphicsTargetLease
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    string OwnedFrontendIdentity{get;}
    string ViewIdentity{get;}
    int? ParameterIndex{get;}
    int InstanceIndex{get;}
    string ProducerOwnerIdentity{get;}
    Rid StorageRid{get;}
    Rid TargetViewRid{get;}
    uint NativeViewFormat{get;}
    RenderingDevice.DataFormat DeviceFormat{get;}
    Vector2I Extent{get;}
    string SourceEvidence{get;}
    void RequireCurrentForPass(int sourcePassIndex);
}

/// <summary>Separate capability when native blending reads destination pixels.
/// Target allocation or selecting its current view does not prove those pixels.</summary>
public interface IOriginalMapPostEffect3StockGraphicsReadableTargetLease:IOriginalMapPostEffect3StockGraphicsTargetLease
{
    void RequireReadableForPass(int sourcePassIndex);
}

public sealed record OriginalMapPostEffect3StockGraphicsFrameInputs(long FrameNumber,
    OriginalMapCopy97CurrentStockPacket CurrentStock,
    OriginalMapPostEffect3TextureParameterState TextureParameters,
    IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> CurrentTextures,
    IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> CurrentSamplers,
    IReadOnlyDictionary<int,int> ConstantBufferInstances,
    IOriginalMapPostEffect3StockGraphicsTargetLease CurrentTarget,Rect2I ActualViewport,
    float ActualContext1C48,OriginalModelRasterInputs SourceRaster,string CurrentDrawContextEvidence);

public sealed record OriginalMapPostEffect3StockGraphicsCompletedPass(long FrameNumber,string ContextOwnerIdentity,
    int SourcePassIndex,string PixelProgramSha256,string StockVertexProgramSha256,
    IReadOnlyList<int> OrderedNativeBindingRows,Rid TargetStorageRid,Rid TargetViewRid,
    int? TargetParameterIndex,int TargetInstanceIndex,ReadOnlyMemory<byte> StockVertexConstants,
    ReadOnlyMemory<byte> StockQuad,string SourceEvidence)
{
    public bool EstablishesCompleteResourceContents=>false;
    public bool CommitsFunctorHistory=>false;
}

/// <summary>Actual RenderingDevice execution of the three source automatic
/// stock-quad passes0/7/44. It admits a proved absent current custom stock VS,
/// complete source32B stock CB/24B vertices, actual sampler leases and explicit
/// current target inheritance. No factory predicates, implicit sampler defaults,
/// custom VS, depth/stencil, multi-target, UAV or source history are inferred.</summary>
public sealed class OriginalMapPostEffect3StockGraphicsExecutor:IAsyncDisposable
{
    public OriginalMapPostEffect3ProgramLibrary Library{get;}
    public OriginalMapPostEffect3ResourceBank Bank{get;}
    public OriginalMapPostEffect3StockGraphicsCompletedPass? LastCompletedPass{get;private set;}
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();private int accepted;private bool retiring;
    private TaskCompletionSource? drained;private Task? disposal;
    private OriginalMapPostEffect3StockGraphicsExecutor(OriginalMapPostEffect3ProgramLibrary library,
        OriginalMapPostEffect3ResourceBank bank)=>(Library,Bank)=(library,bank);
    public static OriginalMapPostEffect3StockGraphicsExecutor Create(
        OriginalMapPostEffect3ProgramLibrary borrowedLibrary,OriginalMapPostEffect3ResourceBank borrowedBank)
    {
        ArgumentNullException.ThrowIfNull(borrowedLibrary);ArgumentNullException.ThrowIfNull(borrowedBank);
        if(borrowedLibrary.SourceSha256!=borrowedBank.Source.Source.Sha256)
            throw new InvalidDataException("Stock graphics library/bank belong to different original modules.");
        return new(borrowedLibrary,borrowedBank);
    }
    private sealed record Accepted(OriginalMapPostEffect3StockGraphicsFrameInputs Input,
        IReadOnlyDictionary<int,OriginalMapPostEffect3ResolvedTextureParameter> References,
        Action ValidateCapabilities,IReadOnlyList<OriginalMapPostEffect3NullShaderResource> NullShaderResources);
    public Task<OriginalMapPostEffect3StockGraphicsCompletedPass> DrawAsync(int sourcePassIndex,
        OriginalMapPostEffect3StockGraphicsFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.CurrentStock);
        ArgumentNullException.ThrowIfNull(input.TextureParameters);ArgumentNullException.ThrowIfNull(input.CurrentTarget);
        if(sourcePassIndex is not (0 or 7 or 44))
            throw new NotSupportedException("Only actual original automatic stock graphics passes0/7/44 are admitted.");
        if(input.FrameNumber<=0||input.CurrentStock.FrameNumber!=input.FrameNumber||
            input.TextureParameters.FrameNumber!=input.FrameNumber||input.TextureParameters.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
            input.TextureParameters.SourceSha256!=Bank.Source.Source.Sha256||!float.IsFinite(input.ActualContext1C48)||
            string.IsNullOrWhiteSpace(input.CurrentDrawContextEvidence))
            throw new InvalidDataException("Stock draw inputs need one accepted current source attempt and explicit native draw fields.");
        input.CurrentStock.ValidateCurrent();
        var nulls=OriginalMapPostEffect3NullShaderResource.CapturePassReads(Bank.Source,input.TextureParameters,sourcePassIndex,0);
        if(!input.CurrentStock.CurrentCustomVertexShaderIsAbsent)
            throw new NotSupportedException("The current source custom stock VS B0 is present; embedded stock selection is unavailable.");
        var textures=input.CurrentTextures.ToArray();var samplers=input.CurrentSamplers.ToArray();
        // Capture immutable identity words synchronously; retain the original
        // capability objects and recheck them before/after actual submission.
        var textureKeys=textures.Select(t=>(t.FrameNumber,t.ContextOwnerIdentity,t.ParameterIndex,t.InstanceIndex,
            t.ShaderViewRid,t.StorageRid,t.NativeViewFormat,t.DeviceFormat,t.Extent,t.SourceEvidence,
            ExternalOwner:(t as IOriginalMapPostEffect3ComputeExternalTextureLease)?.ProducerOwnerIdentity)).ToArray();
        var samplerKeys=samplers.Select(s=>(s.FrameNumber,s.ContextOwnerIdentity,s.NativeStageOrdinal,s.SourceRegister,s.SamplerRid,s.SourceEvidence)).ToArray();
        var target=input.CurrentTarget;
        var targetKey=(target.FrameNumber,target.ContextOwnerIdentity,target.OwnedFrontendIdentity,target.ViewIdentity,
            target.ParameterIndex,target.InstanceIndex,target.ProducerOwnerIdentity,target.StorageRid,target.TargetViewRid,
            target.NativeViewFormat,target.DeviceFormat,target.Extent,target.SourceEvidence);
        var references=textures.Select(t=>t.ParameterIndex).Concat(target.ParameterIndex is{} p?[p]:Array.Empty<int>()).Distinct()
            .ToDictionary(i=>i,i=>input.TextureParameters.Require(i));
        var snapshot=input with{CurrentTextures=Array.AsReadOnly(textures),CurrentSamplers=Array.AsReadOnly(samplers),
            ConstantBufferInstances=new Dictionary<int,int>(input.ConstantBufferInstances),
            SourceRaster=OriginalMapFinalOutputRasterSnapshot.Capture(input.SourceRaster)};
        void Validate()
        {
            snapshot.CurrentStock.ValidateCurrent();target.RequireCurrentForPass(sourcePassIndex);
            if(snapshot.SourceRaster.Attachments.Any(a=>a.EnableBlend))
            {
                if(target is not IOriginalMapPostEffect3StockGraphicsReadableTargetLease readable)
                    throw new NotSupportedException("Native stock blending needs its actual completed destination-content capability.");
                readable.RequireReadableForPass(sourcePassIndex);
            }
            if(targetKey!=(target.FrameNumber,target.ContextOwnerIdentity,target.OwnedFrontendIdentity,target.ViewIdentity,
                target.ParameterIndex,target.InstanceIndex,target.ProducerOwnerIdentity,target.StorageRid,target.TargetViewRid,
                target.NativeViewFormat,target.DeviceFormat,target.Extent,target.SourceEvidence))
                throw new InvalidDataException("Current source target capability changed while stock draw was queued.");
            for(int i=0;i<textures.Length;i++)
            {
                var t=textures[i];t.RequireCurrent();
                if(textureKeys[i]!=(t.FrameNumber,t.ContextOwnerIdentity,t.ParameterIndex,t.InstanceIndex,t.ShaderViewRid,
                    t.StorageRid,t.NativeViewFormat,t.DeviceFormat,t.Extent,t.SourceEvidence,
                    (t as IOriginalMapPostEffect3ComputeExternalTextureLease)?.ProducerOwnerIdentity))
                    throw new InvalidDataException("Current sampled texture capability changed while stock draw was queued.");
            }
            for(int i=0;i<samplers.Length;i++)
            {
                var s=samplers[i];s.RequireCurrent();
                if(samplerKeys[i]!=(s.FrameNumber,s.ContextOwnerIdentity,s.NativeStageOrdinal,s.SourceRegister,s.SamplerRid,s.SourceEvidence))
                    throw new InvalidDataException("Current source sampler capability changed while stock draw was queued.");
            }
            foreach(var receipt in references.Values)snapshot.TextureParameters.ValidateCurrent(receipt);
            foreach(var n in nulls)n.ValidateCurrent();
        }
        Validate();lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}
        return DrawAcceptedAsync(sourcePassIndex,new(snapshot,references,Validate,nulls));
    }
    private async Task<OriginalMapPostEffect3StockGraphicsCompletedPass> DrawAcceptedAsync(int index,Accepted acceptedInput)
    {
        await serial.WaitAsync();var owned=new List<Rid>();
        try
        {
            LastCompletedPass=null;var input=acceptedInput.Input;acceptedInput.ValidateCapabilities();
            var links=Bank.Source.Passes[index];var protocol=OriginalMapPostEffect3PassStateReader.Read(Library.Source,Library.ExecutableLocale)[index];
            if(protocol.AutomaticExecution!=OriginalMapPostEffect3AutomaticExecution.StockScreenQuad||!protocol.UsesStockScreenShaderBinding||
                links.Pass.ProgramReferences.Where(r=>r.StageIndex is not null).ToArray() is not [{StageCode:0}])
                throw new InvalidDataException("Selected original pass is not the actual automatic stock PS route.");
            var compiled=await Library.GetCompiledPassAsync(index,acceptedInput.NullShaderResources);
            var stock=compiled.StockScreenVertex??throw new NotSupportedException("Actual embedded stock VS source was not compiled for this pass.");
            if(compiled.Kind!=OriginalMapPostEffect3CompiledPassKind.Graphics||compiled.Stages.Count!=1||
                stock.EmbeddedSource.Locale!=input.CurrentStock.Source.OwnedFrontendSource.Locale)
                throw new InvalidDataException("Stock graphics does not use its exact original embedded VS/count-selected PS.");
            var pixel=compiled.Stages.Single();var native=links.ActiveBindings.ToArray();
            if(native.Any(r=>r.Table==OriginalMapPostEffect3BindingTable.BufferParameters||r.Role==OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite)||
                native.Any(r=>r.MipSelector!=-1||r.SliceSelector!=-1))
                throw new NotSupportedException("Stock graphics live buffers/UAVs/explicit mip or slice selection need their source executors.");
            var reads=native.Where(r=>r.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&(r.StageMask&4)!=0).ToArray();
            var renderTargets=native.Where(r=>r.Role==OriginalMapPostEffect3BindingRole.RenderTargetWrite).ToArray();
            var cbRows=native.Where(r=>r.Role==OriginalMapPostEffect3BindingRole.ConstantBufferRead).ToArray();
            var target=input.CurrentTarget;var raster=input.SourceRaster;var viewport=input.ActualViewport;
            if(target.FrameNumber!=input.FrameNumber||target.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
                target.OwnedFrontendIdentity!=input.CurrentStock.OwnedFrontendIdentity||target.ViewIdentity!=input.CurrentStock.ViewIdentity||
                !target.StorageRid.IsValid||!target.TargetViewRid.IsValid||string.IsNullOrWhiteSpace(target.ProducerOwnerIdentity)||
                string.IsNullOrWhiteSpace(target.SourceEvidence)||target.DeviceFormat!=ViewFormat(target.NativeViewFormat)||
                viewport.Position!=Vector2I.Zero||viewport.Size!=target.Extent||target.Extent.X<=0||target.Extent.Y<=0)
                throw new NotSupportedException("Stock graphics needs its actual same-frame full origin-zero viewport and live current target capability.");
            if(renderTargets.Any(r=>r.RegisterIndex!=0||r.ParameterIndex!=target.ParameterIndex))
                throw new InvalidDataException("Counted source RT0 binding differs from the actual selected target lease.");
            if(raster.DepthTest||raster.DepthWrite||raster.NativeStencil is not null||raster.Attachments.Count!=1)
                throw new NotSupportedException("This stock executor requires an explicit source single-color depth/stencil-disabled raster.");
            if(target.ParameterIndex is{} ti)ValidateOwner(ti,target.InstanceIndex,target.StorageRid,target.TargetViewRid,
                target.NativeViewFormat,target.ProducerOwnerIdentity,acceptedInput.References,true);
            var nullRegisters=acceptedInput.NullShaderResources.Select(n=>n.SourceRegister).ToHashSet();
            var nullIndices=acceptedInput.NullShaderResources.Where(n=>n.ParameterIndex is not null).Select(n=>n.ParameterIndex!.Value).ToHashSet();
            if(!input.CurrentTextures.Select(t=>t.ParameterIndex).Order().SequenceEqual(reads.Select(r=>r.ParameterIndex).Where(i=>!nullIndices.Contains(i)).Distinct().Order())||
                input.CurrentTextures.Any(t=>t.FrameNumber!=input.FrameNumber||t.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
                    !t.StorageRid.IsValid||!t.ShaderViewRid.IsValid||t.Extent.Z!=1||t.Extent.X<=0||t.Extent.Y<=0||
                    t.DeviceFormat!=ViewFormat(t.NativeViewFormat)||t.StorageRid==target.StorageRid||string.IsNullOrWhiteSpace(t.SourceEvidence)))
                throw new InvalidDataException("Stock pass needs every exact current source sampled2D input with no target feedback.");
            foreach(var t in input.CurrentTextures)ValidateOwner(t.ParameterIndex,t.InstanceIndex,t.StorageRid,t.ShaderViewRid,
                t.NativeViewFormat,(t as IOriginalMapPostEffect3ComputeExternalTextureLease)?.ProducerOwnerIdentity,acceptedInput.References,false);
            var declarations=pixel.Bindings.Bindings;
            if(declarations.Any(d=>d.Device.DescriptorSet!=0||d.DescriptorKind is not ("UniformBuffer" or "Texture" or "Sampler"))||
                stock.Bindings.Bindings is not [{DescriptorKind:"UniformBuffer",ConstantBuffer.ByteSize:32}]||
                stock.Bindings.Bindings[0].Device.DescriptorSet!=1||stock.Bindings.Bindings[0].Source.BindPoint!=0)
                throw new NotSupportedException("Stock graphics shader interface requires an unsupported source resource kind.");
            if(!input.CurrentSamplers.Select(s=>s.SourceRegister).Order().SequenceEqual(declarations.Where(d=>d.DescriptorKind=="Sampler").Select(d=>d.Source.BindPoint).Order())||
                input.CurrentSamplers.Any(s=>s.FrameNumber!=input.FrameNumber||s.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||s.NativeStageOrdinal!=2||
                    !s.SamplerRid.IsValid||string.IsNullOrWhiteSpace(s.SourceEvidence)))
                throw new InvalidDataException("Stock pass needs its exact source-selected sampler capabilities; no default sampler is inferred.");
            int[] cbIndices=cbRows.Select(r=>r.ParameterIndex).Distinct().Order().ToArray();
            if(!input.ConstantBufferInstances.Keys.Order().SequenceEqual(cbIndices))
                throw new InvalidDataException("Stock pass CB sides differ from all counted native bindings.");
            var cbs=cbIndices.ToDictionary(i=>i,i=>Bank.RequireConstantBufferForPass(i,input.ConstantBufferInstances[i],index,input.FrameNumber));
            var revisions=cbs.ToDictionary(p=>p.Key,p=>p.Value.UploadRevision);
            var cbReadReceipts=Array.AsReadOnly(cbs.Select(pair=>
                new OriginalMapPostEffect3ComputeConstantBufferReadReceipt(pair.Key,pair.Value.InstanceIndex,
                    pair.Value.StorageRid,pair.Value.UploadRevision,pair.Value.HasCompleteWriterContents,
                    pair.Value.CompleteBytesSha256)).ToArray());
            void ValidateSourceTextureReadFootprints()
            {
                foreach(var row in reads.GroupBy(r=>r.RegisterIndex).Select(group=>group.Last()))
                    if(!nullRegisters.Contains(checked((uint)row.RegisterIndex)))
                    if(input.CurrentTextures.Single(t=>t.ParameterIndex==row.ParameterIndex) is
                        IOriginalMapPostEffect3SourceTextureReadFootprintLease footprint)
                        footprint.RequireReadableSourceFootprint(index,checked((uint)row.RegisterIndex),
                            pixel.Source.Sha256,cbReadReceipts);
            }
            void ValidateConstantBuffers()
            {
                foreach(int i in cbIndices)
                    if(!ReferenceEquals(cbs[i],Bank.RequireConstantBufferForPass(i,input.ConstantBufferInstances[i],index,input.FrameNumber))||
                        cbs[i].UploadRevision!=revisions[i])throw new InvalidDataException("Stock native CB changed while drawing was queued or submitted.");
                ValidateSourceTextureReadFootprints();
            }
            ValidateConstantBuffers();
            var mapped=new Dictionary<(string,uint),Rid>();
            foreach(var row in native.Where(r=>(r.StageMask&4)!=0))
            {
                if(row.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead)
                {
                    if(nullRegisters.Contains(checked((uint)row.RegisterIndex)))continue;
                    mapped[("Texture",(uint)row.RegisterIndex)]=input.CurrentTextures.Single(t=>t.ParameterIndex==row.ParameterIndex).ShaderViewRid;
                }
                else if(row.Role==OriginalMapPostEffect3BindingRole.ConstantBufferRead)
                    mapped[("UniformBuffer",(uint)row.RegisterIndex)]=cbs[row.ParameterIndex].StorageRid;
            }
            foreach(var sampler in input.CurrentSamplers)mapped.Add(("Sampler",sampler.SourceRegister),sampler.SamplerRid);
            foreach(var d in declarations)if(!mapped.ContainsKey((d.DescriptorKind,d.Source.BindPoint)))
                throw new NotSupportedException("Stock PS reads an implicit current resource without an actual source lease.");
            byte[] vertex=OriginalDeferredAuxiliaryDepthCopySource.BuildStockVertexConstants(viewport.Size.X,viewport.Size.Y,
                input.CurrentStock.CurrentScreenScale29E0.X,input.CurrentStock.CurrentScreenScale29E0.Y,input.CurrentStock.CurrentColorScale29F0);
            byte[] quad=BuildQuad(viewport.Size,input.ActualContext1C48);
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Stock graphics RD allocation failed.");owned.Add(rid);return rid;}
                acceptedInput.ValidateCapabilities();ValidateConstantBuffers();
                if(!rd.TextureIsValid(target.StorageRid)||!rd.TextureIsValid(target.TargetViewRid))
                    throw new InvalidDataException("Current stock color target retired before drawing.");
                using(var shape=rd.TextureGetFormat(target.TargetViewRid))
                    if(shape.TextureType!=RenderingDevice.TextureType.Type2D||shape.ArrayLayers!=1||shape.Mipmaps!=1||
                        shape.Width!=target.Extent.X||shape.Height!=target.Extent.Y||shape.Format!=target.DeviceFormat||
                        !shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.ColorAttachmentBit))
                        throw new InvalidDataException("Current source stock target shape/format/usage differs from its native lease.");
                foreach(var d in declarations.Where(d=>d.DescriptorKind=="Texture"))
                {
                    var row=reads.Last(r=>r.RegisterIndex==d.Source.BindPoint);
                    var lease=input.CurrentTextures.Single(t=>t.ParameterIndex==row.ParameterIndex);
                    if(!rd.TextureIsValid(lease.StorageRid)||!rd.TextureIsValid(lease.ShaderViewRid))
                        throw new InvalidDataException("Current stock sampled input retired before drawing.");
                    using var shape=rd.TextureGetFormat(lease.ShaderViewRid);
                    bool effectiveViewMatches=shape.Format==lease.DeviceFormat;
                    if(!effectiveViewMatches&&lease is IOriginalMapPostEffect3SharedSampledViewLease shared)
                    {
                        shared.RequireSharedSampledViewProof().RequireCurrentNativeView(lease,rd,shape);
                        effectiveViewMatches=true;
                    }
                    bool depth=lease.DeviceFormat.ToString() is "D32Sfloat" or "D32SfloatS8Uint";
                    uint returnType=!depth&&lease.DeviceFormat.ToString().Contains("Uint",StringComparison.Ordinal)?4u:
                        lease.DeviceFormat.ToString().Contains("Sint",StringComparison.Ordinal)?3u:5u;
                    if(d.Source.Dimension!=4||d.Source.ReturnType!=returnType||shape.TextureType!=RenderingDevice.TextureType.Type2D||
                        shape.ArrayLayers!=1||shape.Width!=lease.Extent.X||shape.Height!=lease.Extent.Y||!effectiveViewMatches||
                        !shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit))
                        throw new InvalidDataException($"Stock pass{index} t{d.Source.BindPoint}/parameter{lease.ParameterIndex} input differs: sourceDim={d.Source.Dimension} returnType={d.Source.ReturnType}/{returnType}; device={shape.TextureType}/{shape.Format} leaseFormat={lease.DeviceFormat}; extent={shape.Width}x{shape.Height}/{lease.Extent}; layers={shape.ArrayLayers}; usage={shape.UsageBits}.");
                }
                Rid framebuffer=Own(rd.FramebufferCreate([target.TargetViewRid]));
                if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("Stock source framebuffer is invalid.");
                Rid vertexCb=Own(rd.UniformBufferCreate(32,vertex));
                Rid Set(OriginalTranslatedShaderBindings layout,uint stage)
                {
                    if(layout.Bindings.Count==0)return default;
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var d in layout.Bindings)
                        {
                            var u=new RDUniform{Binding=checked((int)d.Device.Binding!.Value),UniformType=d.DescriptorKind switch{
                                "UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"Texture"=>RenderingDevice.UniformType.Texture,
                                "Sampler"=>RenderingDevice.UniformType.Sampler,_=>throw new NotSupportedException()}};
                            uniforms.Add(u);u.AddId(stage==1?vertexCb:mapped[(d.DescriptorKind,d.Source.BindPoint)]);
                        }
                        Rid set=Own(rd.UniformSetCreate(uniforms,compiled.ShaderRid,stage));
                        if(!rd.UniformSetIsValid(set))throw new InvalidDataException("Stock stage uniform set is invalid.");return set;
                    }
                    finally{foreach(var u in uniforms)u.Dispose();}
                }
                Rid psSet=Set(pixel.Bindings,0),vsSet=Set(stock.Bindings,1);
                var attrs=new Godot.Collections.Array<RDVertexAttribute>();long format;
                try
                {
                    var inputs=stock.Bindings.Interface.StageVariables.Where(v=>v.StorageClass==1&&v.Location is not null).OrderBy(v=>v.Location).ToArray();
                    if(!inputs.Select(v=>v.Location).SequenceEqual(new uint?[]{0,1,2})||
                        !inputs.Select(v=>v.Type.ComponentCount).SequenceEqual(new uint?[]{3,4,2}))
                        throw new InvalidDataException("Stock source24B vertex interface differs from position3/color4/uv2.");
                    attrs.Add(new(){Location=0,Offset=0,Stride=24,Format=RenderingDevice.DataFormat.R32G32B32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex});
                    attrs.Add(new(){Location=1,Offset=12,Stride=24,Format=RenderingDevice.DataFormat.R8G8B8A8Unorm,Frequency=RenderingDevice.VertexFrequency.Vertex});
                    attrs.Add(new(){Location=2,Offset=16,Stride=24,Format=RenderingDevice.DataFormat.R32G32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex});
                    format=rd.VertexFormatCreate(attrs);
                }
                finally{foreach(var attribute in attrs)attribute.Dispose();}
                Rid vb=Own(rd.VertexBufferCreate(96,quad));
                Rid vertices=Own(rd.VertexArrayCreate(4,format,new Godot.Collections.Array<Rid>{vb,vb,vb}));
                using var rasterState=new RDPipelineRasterizationState{CullMode=raster.Cull,FrontFace=raster.FrontFace};
                using var multi=new RDPipelineMultisampleState();
                using var depthState=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                var a=raster.Attachments.Single();
                using var attachment=new RDPipelineColorBlendStateAttachment{EnableBlend=a.EnableBlend,SrcColorBlendFactor=a.SourceColor,
                    DstColorBlendFactor=a.DestinationColor,ColorBlendOp=a.ColorOperation,SrcAlphaBlendFactor=a.SourceAlpha,
                    DstAlphaBlendFactor=a.DestinationAlpha,AlphaBlendOp=a.AlphaOperation,WriteR=a.WriteR,WriteG=a.WriteG,WriteB=a.WriteB,WriteA=a.WriteA};
                using var blend=new RDPipelineColorBlendState{Attachments=[attachment]};
                Rid pipeline=Own(rd.RenderPipelineCreate(compiled.ShaderRid,rd.FramebufferGetFormat(framebuffer),format,
                    RenderingDevice.RenderPrimitive.TriangleStrips,rasterState,multi,depthState,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Stock original VS/PS pipeline failed.");
                long list=rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.DefaultAll);
                try
                {
                    rd.DrawListBindRenderPipeline(list,pipeline);rd.DrawListBindVertexArray(list,vertices);
                    if(psSet.IsValid)rd.DrawListBindUniformSet(list,psSet,0);rd.DrawListBindUniformSet(list,vsSet,1);
                    rd.DrawListDraw(list,false,1,4);
                }
                finally{rd.DrawListEnd();}
                return true;
            });
            acceptedInput.ValidateCapabilities();
            ValidateConstantBuffers();
            await FreeAsync(owned);owned.Clear();
            return LastCompletedPass=new(input.FrameNumber,Bank.Source.ContextOwnerIdentity,index,pixel.Source.Sha256,
                stock.EmbeddedSource.Vertex.Sha256,Array.AsReadOnly(native.Select(r=>r.SourceRowIndex).ToArray()),target.StorageRid,
                target.TargetViewRid,target.ParameterIndex,target.InstanceIndex,vertex,quad,
                input.CurrentStock.SourceProvenance+";"+target.SourceEvidence+";"+input.CurrentDrawContextEvidence+
                ";current-stock-B0-selection;actual-RD-triangle-strip4;no-history-or-full-content-claim");
        }
        finally
        {
            try{if(owned.Count!=0)await FreeAsync(owned);}
            finally{serial.Release();lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();}
        }
    }
    private void ValidateOwner(int index,int instance,Rid storage,Rid view,uint nativeFormat,string? externalOwner,
        IReadOnlyDictionary<int,OriginalMapPostEffect3ResolvedTextureParameter> references,bool renderTarget)
    {
        var reference=references[index].Reference;
        if(reference.InstanceIndex!=instance)throw new InvalidDataException("Stock native alias refers to another actual side.");
        if(reference.PhysicalAllocationParameterIndex is{} physical)
        {
            var actual=Bank.RequireTexture(physical,instance);
            if(actual.StorageRid!=storage||!actual.NativeFormatViews.TryGetValue(nativeFormat,out var selected)||selected!=view||
                renderTarget&&(!actual.Shape.HasRenderTargetPath||nativeFormat!=actual.Shape.Formats.RenderTargetFormat0&&nativeFormat!=actual.Shape.Formats.RenderTargetFormat1))
                throw new InvalidDataException("Stock native alias is not this exact physical allocation and selected source view.");
        }
        else if(externalOwner!=reference.ProducerOwnerIdentity)
            throw new InvalidDataException("Stock external alias differs from the exact source wrapper producer.");
    }
    private static byte[] BuildQuad(Vector2I extent,float z)
    {
        float[][] rows=[[0,0,0,0],[extent.X,0,1,0],[0,extent.Y,0,1],[extent.X,extent.Y,1,1]];byte[] bytes=new byte[96];
        for(int i=0;i<4;i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24),rows[i][0]);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+4),rows[i][1]);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+8),z);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*24+12),uint.MaxValue);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+16),rows[i][2]);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+20),rows[i][3]);
        }
        return bytes;
    }
    private static RenderingDevice.DataFormat ViewFormat(uint native)=>native switch
    {
        2=>RenderingDevice.DataFormat.R32G32B32A32Sfloat,10=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,
        26=>RenderingDevice.DataFormat.B10G11R11UfloatPack32,28=>RenderingDevice.DataFormat.R8G8B8A8Unorm,
        29=>RenderingDevice.DataFormat.R8G8B8A8Srgb,30=>RenderingDevice.DataFormat.R8G8B8A8Uint,
        34=>RenderingDevice.DataFormat.R16G16Sfloat,40=>RenderingDevice.DataFormat.D32Sfloat,
        41=>RenderingDevice.DataFormat.R32Sfloat,42=>RenderingDevice.DataFormat.R32Uint,
        49=>RenderingDevice.DataFormat.R8G8Unorm,50=>RenderingDevice.DataFormat.R8G8Uint,
        54=>RenderingDevice.DataFormat.R16Sfloat,61=>RenderingDevice.DataFormat.R8Unorm,
        87=>RenderingDevice.DataFormat.B8G8R8A8Unorm,91=>RenderingDevice.DataFormat.B8G8R8A8Srgb,
        20 or 21=>RenderingDevice.DataFormat.D32SfloatS8Uint,
        _=>throw new NotSupportedException("Stock native image view format has no admitted device representation.")
    };
    private static Task<bool> FreeAsync(IReadOnlyList<Rid> owned)=>OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
    {for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);return true;});
    public ValueTask DisposeAsync()
    {
        lock(lifetime){if(disposal is not null)return new(disposal);retiring=true;
            Task pending=accepted==0?Task.CompletedTask:(drained=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            return new(disposal=RetireAsync(pending));}
    }
    private async Task RetireAsync(Task pending)
    {await pending;await serial.WaitAsync();try{LastCompletedPass=null;}finally{serial.Release();}}
}
