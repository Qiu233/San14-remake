using Godot;
using San14.Data;

/// <summary>Actual Functor1 procedural fullscreen execution. Counted explicit
/// VS/PS remain selected; the old embedded stock VS is never substituted.
/// Source capabilities provide target inheritance, samplers, viewport, scissor
/// and raster. Receipts describe submitted writes, never complete contents.</summary>
public sealed partial class MapPostEffect3FullscreenGraphicsExecutor:IAsyncDisposable
{
    public MapPostEffect3ProgramLibrary Library{get;}
    public PostProcessResourceBank Bank{get;}
    public MapPostEffect3FullscreenCompletedPass? LastCompletedPass{get;private set;}
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();
    private int accepted;private bool retiring;private TaskCompletionSource? drained;private Task? disposal;
    private MapPostEffect3FullscreenGraphicsExecutor(MapPostEffect3ProgramLibrary library,
        PostProcessResourceBank bank)=>(Library,Bank)=(library,bank);
    public static MapPostEffect3FullscreenGraphicsExecutor Create(
        MapPostEffect3ProgramLibrary borrowedLibrary,PostProcessResourceBank borrowedBank)
    {
        ArgumentNullException.ThrowIfNull(borrowedLibrary);ArgumentNullException.ThrowIfNull(borrowedBank);
        if(borrowedLibrary.SourceSha256!=borrowedBank.Source.Source.Sha256)
            throw new InvalidDataException("Fullscreen library and bank belong to different original modules.");
        return new(borrowedLibrary,borrowedBank);
    }
    private sealed record Accepted(MapPostEffect3FullscreenFrameInputs Input,ModelRasterInputs Raster,
        Rect2I Viewport,Rect2I? Scissor,IReadOnlyDictionary<int,MapPostEffect3ResolvedTextureParameter> References,
        IReadOnlyList<MapPostEffect3Binding> EffectiveBindings,
        IReadOnlyList<MapPostEffect3NullShaderResource> Nulls,Action Validate);

    public Task<MapPostEffect3FullscreenCompletedPass> DrawAsync(int sourcePassIndex,
        MapPostEffect3FullscreenFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.SourceDraw);
        ArgumentNullException.ThrowIfNull(input.TextureParameters);ArgumentNullException.ThrowIfNull(input.CurrentDrawContext);
        var cpu=RenderThreadSchedulingDiagnostics.BeginCpuTrace("FullscreenPrepare",sourcePassIndex);
        var source=input.SourceDraw;var table=input.TextureParameters;var context=input.CurrentDrawContext;
        source.RequireCurrent();context.RequireCurrentForPass(sourcePassIndex);
        if(sourcePassIndex!=source.SourcePassIndex||sourcePassIndex is <45 or >116||source.FrameNumber<=0||
            source.SourceSha256!=Library.SourceSha256||source.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
            table.SourceSha256!=source.SourceSha256||table.ContextOwnerIdentity!=source.ContextOwnerIdentity||
            table.FrameNumber!=source.FrameNumber||table.Allocation.ContextWidth!=Bank.Source.ContextWidth||
            table.Allocation.ContextHeight!=Bank.Source.ContextHeight||
            context.FrameNumber!=source.FrameNumber||context.ContextOwnerIdentity!=source.ContextOwnerIdentity||
            string.IsNullOrWhiteSpace(context.OwnedFrontendIdentity)||string.IsNullOrWhiteSpace(context.ViewIdentity)||
            string.IsNullOrWhiteSpace(context.SourceEvidence))
            throw new InvalidDataException("Fullscreen draw needs one current prepared factory and native frontend attempt.");
        if(source.ExecutableLocale!=Library.ExecutableLocale)
            throw new InvalidDataException("Fullscreen source command belongs to another executable locale.");
        cpu?.Mark("01-source-and-context-current");
        var state=MapPostEffect3PassStateReader.Read(Library.Source,Library.ExecutableLocale)[sourcePassIndex];
        if(state.AutomaticExecution!=MapPostEffect3AutomaticExecution.SpecificFactoryGraphics||
            state.UsesStockScreenShaderBinding||state.Pass.ProgramReferences.Where(p=>p.StageIndex is not null)
                .OrderBy(p=>p.StageCode).ToArray() is not [{StageCode:0},{StageCode:1}])
            throw new NotSupportedException("Fullscreen executor requires the actual explicit factory VS/PS path.");
        cpu?.Mark("02-static-pass-state");
        var textures=input.CurrentTextures.ToArray();var samplers=input.CurrentSamplers.ToArray();
        var targets=input.CurrentTargets.OrderBy(t=>t.SourceTargetRegister).ToArray();var buffers=input.CurrentBuffers.ToArray();
        var overrides=input.TextureOverrides.ToArray();
        foreach(var setter in overrides)
        {
            setter.RequireCurrent();
            if(setter.SourcePassIndex!=sourcePassIndex||setter.FrameNumber!=source.FrameNumber||
                setter.ContextOwnerIdentity!=source.ContextOwnerIdentity||setter.SourceSha256!=source.SourceSha256||
                !textures.Contains(setter.Texture))
                throw new InvalidDataException("Fullscreen Before setter differs from its current source pass or texture capability.");
        }
        RequireBeforeOverrides(source,overrides);
        var native=Bank.Source.Passes[sourcePassIndex].ActiveBindings.Select(b=>b with{RawBytes=b.RawBytes.ToArray()})
            .Concat(overrides.Select(o=>o.SourceSetter)).ToArray();
        if(native.Any(b=>b.Role==MapPostEffect3BindingRole.UnorderedAccessReadWrite))
            throw new NotSupportedException("Fullscreen graphics UAV writes require their native graphics adapter.");
        var nulls=MapPostEffect3NullShaderResource.CapturePassReads(Bank.Source,table,sourcePassIndex,0)
            .Where(n=>!overrides.Any(o=>o.SourceSetter.RegisterIndex==n.SourceRegister)).ToArray();
        var references=textures.Select(t=>t.ParameterIndex).Concat(targets.Where(t=>t.Target.ParameterIndex is not null)
            .Select(t=>t.Target.ParameterIndex!.Value)).Distinct().ToDictionary(i=>i,table.Require);
        var raster=MapFinalOutputRasterSnapshot.Capture(context.ActualRaster);
        var viewport=context.ActualViewport;var scissor=context.ActualScissor;
        var textureKeys=textures.Select(TextureKey).ToArray();var samplerKeys=samplers.Select(SamplerKey).ToArray();
        var targetKeys=targets.Select(t=>TargetKey(t.Target)).ToArray();var bufferKeys=buffers.Select(BufferKey).ToArray();
        var contextKey=(context.FrameNumber,context.ContextOwnerIdentity,context.OwnedFrontendIdentity,context.ViewIdentity,
            context.ActualViewport,context.ActualViewportMinimumDepth,context.ActualViewportMaximumDepth,context.ActualScissor,context.SourceEvidence);
        var snapshot=input with{CurrentTextures=Array.AsReadOnly(textures),CurrentSamplers=Array.AsReadOnly(samplers),
            CurrentTargets=Array.AsReadOnly(targets),CurrentBuffers=Array.AsReadOnly(buffers),
            ConstantBufferInstances=new Dictionary<int,int>(input.ConstantBufferInstances),TextureOverrides=Array.AsReadOnly(overrides)};
        cpu?.Mark("03-current-input-snapshot");
        void Validate()
        {
            source.RequireCurrent();context.RequireCurrentForPass(sourcePassIndex);
            if(contextKey!=(context.FrameNumber,context.ContextOwnerIdentity,context.OwnedFrontendIdentity,context.ViewIdentity,
                context.ActualViewport,context.ActualViewportMinimumDepth,context.ActualViewportMaximumDepth,context.ActualScissor,context.SourceEvidence)||!SameRaster(raster,context.ActualRaster))
                throw new InvalidDataException("Fullscreen current frontend draw state changed while submission was queued.");
            for(int i=0;i<textures.Length;i++)
            {textures[i].RequireCurrent();if(!textureKeys[i].SequenceEqual(TextureKey(textures[i])))throw new InvalidDataException("Fullscreen texture capability changed.");}
            for(int i=0;i<samplers.Length;i++)
            {samplers[i].RequireCurrent();if(!samplerKeys[i].SequenceEqual(SamplerKey(samplers[i])))throw new InvalidDataException("Fullscreen sampler capability changed.");}
            for(int i=0;i<targets.Length;i++)
            {targets[i].Target.RequireCurrentForPass(sourcePassIndex);if(!targetKeys[i].SequenceEqual(TargetKey(targets[i].Target)))throw new InvalidDataException("Fullscreen target capability changed.");}
            for(int i=0;i<buffers.Length;i++)
            {buffers[i].RequireCurrent();if(!bufferKeys[i].SequenceEqual(BufferKey(buffers[i])))throw new InvalidDataException("Fullscreen buffer capability changed.");}
            foreach(var reference in references.Values)table.ValidateCurrent(reference);
            foreach(var setter in overrides)setter.RequireCurrent();foreach(var n in nulls)n.ValidateCurrent();
        }
        Validate();lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}
        cpu?.Mark("04-pre-submit-validation");
        return DrawAcceptedAsync(sourcePassIndex,new(snapshot,raster,viewport,scissor,references,
            Array.AsReadOnly(native),Array.AsReadOnly(nulls),Validate));
    }

    private async Task<MapPostEffect3FullscreenCompletedPass> DrawAcceptedAsync(int index,Accepted actual)
    {
        var cpu=RenderThreadSchedulingDiagnostics.BeginCpuTrace("FullscreenAccepted",index);
        await serial.WaitAsync();var owned=new List<Rid>();IDisposable? bankScope=null;
        cpu?.Mark("01-serial-wait");
        try
        {
            LastCompletedPass=null;actual.Validate();var input=actual.Input;var source=input.SourceDraw;
            cpu?.Mark("02-accepted-validation");
            bankScope=await Bank.AcquireBufferExecutionScopeAsync();
            cpu?.Mark("03-bank-scope");
            var compiled=await Library.GetCompiledPassAsync(index,actual.Nulls);
            cpu?.Mark("04-compiled-program");
            var vertex=compiled.Stages.Single(s=>s.Source.StageCode==1);var pixel=compiled.Stages.Single(s=>s.Source.StageCode==0);
            if(compiled.Kind!=MapPostEffect3CompiledPassKind.Graphics||compiled.StockScreenVertex is not null||
                compiled.Stages.Count!=2||vertex.RequiresZeroBaseDraw||
                vertex.Bindings.Interface.StageVariables.Any(v=>v.StorageClass==1&&v.Location is not null)||
                vertex.Bindings.Interface.StageVariables.Any(v=>v.StorageClass==1&&v.BuiltIn==43)||
                compiled.Stages.SelectMany(s=>s.Bindings.Bindings).Any(d=>d.DescriptorKind is not("UniformBuffer" or "Texture" or "TextureBuffer" or "Sampler")))
                throw new NotSupportedException("Selected source fullscreen VS/PS requires another native geometry or descriptor adapter.");
            var native=actual.EffectiveBindings;var targets=input.CurrentTargets;var raster=actual.Raster;
            RequireTargets(index,actual);
            if(raster.DepthTest||raster.DepthWrite||raster.NativeStencil is not null)
                throw new NotSupportedException("Actual fullscreen depth/stencil target state requires its selected native attachment adapter.");
            var mapped=new Dictionary<(uint Stage,string Kind,uint Register),Rid>();
            var sampled=new Dictionary<(uint Stage,uint Register),IMapPostEffect3ComputeTextureLease>();
            var cbIndices=native.Where(b=>b.Role==MapPostEffect3BindingRole.ConstantBufferRead).Select(b=>b.ParameterIndex).Distinct().Order().ToArray();
            if(!input.ConstantBufferInstances.Keys.Order().SequenceEqual(cbIndices))
                throw new InvalidDataException("Fullscreen CB sides must retain every exact counted source setter.");
            var activeCbs=new HashSet<int>();var usedTextures=new HashSet<int>();var usedBuffers=new HashSet<int>();
            foreach(var stage in compiled.Stages)
            foreach(var d in stage.Bindings.Bindings)
            {
                uint code=stage.Source.StageCode;byte mask=code==1?(byte)2:(byte)4;
                if(d.Device.DescriptorSet!=(code==1?1u:0u))throw new InvalidDataException("Fullscreen translated stage set differs.");
                if(d.DescriptorKind=="Sampler")
                {
                    var sampler=input.CurrentSamplers.SingleOrDefault(s=>s.NativeStageOrdinal==(code==1?0:2)&&s.SourceRegister==d.Source.BindPoint)
                        ??throw new InvalidDataException("Fullscreen shader sampler lacks its actual current native source selection.");
                    if(sampler.FrameNumber!=source.FrameNumber||sampler.ContextOwnerIdentity!=source.ContextOwnerIdentity||
                        !sampler.SamplerRid.IsValid||string.IsNullOrWhiteSpace(sampler.SourceEvidence))
                        throw new InvalidDataException("Fullscreen source sampler belongs to another attempt.");
                    mapped.Add((code,d.DescriptorKind,d.Source.BindPoint),sampler.SamplerRid);continue;
                }
                var row=native.LastOrDefault(b=>(b.StageMask&mask)!=0&&b.RegisterIndex==d.Source.BindPoint&&
                    b.Role==(d.DescriptorKind=="UniformBuffer"?MapPostEffect3BindingRole.ConstantBufferRead:
                        MapPostEffect3BindingRole.ShaderResourceRead)&&
                    b.Table==(d.DescriptorKind=="UniformBuffer"?MapPostEffect3BindingTable.ConstantBufferParameters:
                        d.DescriptorKind=="TextureBuffer"?MapPostEffect3BindingTable.BufferParameters:
                        MapPostEffect3BindingTable.TextureParameters))
                    ??throw new NotSupportedException("Fullscreen shader reads an implicit source register without a proved Before setter.");
                if(row.SliceSelector is not(-1 or 0))throw new NotSupportedException("Fullscreen source array slices need their native adapter.");
                if(d.DescriptorKind=="UniformBuffer")
                {activeCbs.Add(row.ParameterIndex);continue;}
                if(d.DescriptorKind=="TextureBuffer")
                {
                    var buffer=input.CurrentBuffers.Single(b=>b.ParameterIndex==row.ParameterIndex);
                    buffer.RequireReadRange(0,buffer.ByteCount);var allocation=Bank.RequireBuffer(buffer.ParameterIndex,buffer.InstanceIndex);
                    if(buffer.FrameNumber!=source.FrameNumber||buffer.ContextOwnerIdentity!=source.ContextOwnerIdentity||
                        buffer.StorageRid!=allocation.StorageRid||buffer.ShaderReadRid!=allocation.ShaderReadRid||!buffer.ShaderReadRid.IsValid||
                        d.Source.Dimension!=1||d.Source.ReturnType!=4||buffer.DeviceTexelFormat!=RenderingDevice.DataFormat.R32Uint)
                        throw new InvalidDataException("Fullscreen buffer input differs from its actual native typed read owner.");
                    mapped.Add((code,d.DescriptorKind,d.Source.BindPoint),buffer.ShaderReadRid);usedBuffers.Add(row.ParameterIndex);continue;
                }
                var texture=input.CurrentTextures.Single(t=>t.ParameterIndex==row.ParameterIndex);
                RequireTextureOwner(texture,actual.References[row.ParameterIndex]);
                if(texture.FrameNumber!=source.FrameNumber||texture.ContextOwnerIdentity!=source.ContextOwnerIdentity||
                    !texture.StorageRid.IsValid||!texture.ShaderViewRid.IsValid||string.IsNullOrWhiteSpace(texture.SourceEvidence))
                    throw new InvalidDataException("Fullscreen source texture is from another actual attempt.");
                if(texture is IMapPostEffect3ComputeSelectedMipTextureLease{IsSourceSelectedMipView:true} selected)
                {selected.RequireSourceSelectedView(index,d.Source.BindPoint);selected.RequireReadableMipRange(selected.NativeFirstMipLevel,selected.NativeMipLevelCount);}
                sampled.Add((code,d.Source.BindPoint),texture);mapped.Add((code,d.DescriptorKind,d.Source.BindPoint),texture.ShaderViewRid);
                usedTextures.Add(row.ParameterIndex);
            }
            if(!usedTextures.Order().SequenceEqual(input.CurrentTextures.Select(t=>t.ParameterIndex).Order())||
                !usedBuffers.Order().SequenceEqual(input.CurrentBuffers.Select(b=>b.ParameterIndex).Order())||
                !compiled.Stages.SelectMany(s=>s.Bindings.Bindings.Where(d=>d.DescriptorKind=="Sampler")
                    .Select(d=>(Stage:s.Source.StageCode==1?0:2,Register:d.Source.BindPoint))).Order()
                    .SequenceEqual(input.CurrentSamplers.Select(s=>(Stage:s.NativeStageOrdinal,Register:s.SourceRegister)).Order()))
                throw new InvalidDataException("Fullscreen inputs must contain exactly the actual shader-visible current resources.");
            cpu?.Mark("05-shader-descriptor-mapping");
            var sourceCbRequirements=MapPostEffect3ParameterBindingRequirements.Read(Bank.Source,index);
            foreach(int inactive in cbIndices.Where(i=>!activeCbs.Contains(i)))
            {
                var declarations=sourceCbRequirements.Where(r=>r.ParameterIndex==inactive&&r.StageCode is 0 or 1).ToArray();
                if(declarations.Length==0||declarations.Any(r=>r.ShaderDeclaredByteCount is not null))
                    throw new NotSupportedException("A native CB setter omitted by translation still needs its original DXBC declaration/writer proof.");
            }
            var cbs=activeCbs.ToDictionary(i=>i,i=>Bank.RequireConstantBufferForPass(i,input.ConstantBufferInstances[i],index,source.FrameNumber));
            var revisions=cbs.ToDictionary(p=>p.Key,p=>p.Value.UploadRevision);
            var cbReads=Array.AsReadOnly(cbs.Select(p=>new MapPostEffect3ComputeConstantBufferReadReceipt(p.Key,
                p.Value.InstanceIndex,p.Value.StorageRid,p.Value.UploadRevision,p.Value.HasCompleteWriterContents,p.Value.CompleteBytesSha256)).ToArray());
            foreach(var stage in compiled.Stages)
            foreach(var d in stage.Bindings.Bindings.Where(d=>d.DescriptorKind=="UniformBuffer"))
            {
                byte mask=stage.Source.StageCode==1?(byte)2:(byte)4;
                var row=native.Last(b=>b.Role==MapPostEffect3BindingRole.ConstantBufferRead&&(b.StageMask&mask)!=0&&b.RegisterIndex==d.Source.BindPoint);
                mapped.Add((stage.Source.StageCode,d.DescriptorKind,d.Source.BindPoint),cbs[row.ParameterIndex].StorageRid);
            }
            cpu?.Mark("06-cb-requirements-and-binding");
            void ValidateCbsAndReads()
            {
                foreach(var p in cbs)
                    if(!ReferenceEquals(p.Value,Bank.RequireConstantBufferForPass(p.Key,input.ConstantBufferInstances[p.Key],index,source.FrameNumber))||
                        p.Value.UploadRevision!=revisions[p.Key])throw new InvalidDataException("Fullscreen native CB changed during submission.");
                foreach(var expected in source.RequiredConstantBufferBytesSha256)
                    if(!cbs.TryGetValue(expected.Key,out var cb)||!cb.HasCompleteWriterContents||cb.CompleteBytesSha256!=expected.Value)
                        throw new InvalidDataException("Fullscreen source iteration differs from the actual uploaded Eye constant-buffer bytes.");
                foreach(var read in sampled)
                    if(read.Value is IMapPostEffect3SourceTextureReadFootprintLease footprint)
                        footprint.RequireReadableSourceFootprint(index,read.Key.Register,compiled.Stages.Single(s=>s.Source.StageCode==read.Key.Stage).Source.Sha256,cbReads);
            }
            ValidateCbsAndReads();var writes=new List<MapPostEffect3FullscreenWriteReceipt>();
            cpu?.Mark("07-current-cb-and-footprint-validation");
            var textureReads=new List<MapPostEffect3FullscreenTextureReadReceipt>();bool writeStarted=false;
            Rect2I? clippedScissor=actual.Scissor is{} nativeScissor?nativeScissor.Intersection(actual.Viewport):null;
            bool emptyPixelDomain=clippedScissor is{} clipped&&(clipped.Size.X<=0||clipped.Size.Y<=0);
            var renderingTask=TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                try
                {
                    Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Fullscreen RD allocation failed.");owned.Add(rid);return rid;}
                    actual.Validate();ValidateCbsAndReads();
                    // Godot4.7.2 DrawListEnableScissor returns without changing
                    // state for an empty rectangle. Skip this read-only VS/PS
                    // pixel domain before invalidating any target contents.
                    if(emptyPixelDomain)return true;
                    foreach(var target in targets)
                    {
                        RequireDeviceTarget(rd,target.Target,index,target.SourceTargetRegister);
                        if(raster.Attachments[target.SourceTargetRegister].EnableBlend)
                        {
                            if(target.Target is not IMapPostEffect3StockGraphicsReadableTargetLease readable)
                                throw new InvalidDataException("Fullscreen blending needs completed contents of the actual destination mip.");
                            readable.RequireReadableForPass(index);
                        }
                    }
                    foreach(var stage in compiled.Stages)
                    foreach(var d in stage.Bindings.Bindings.Where(d=>d.DescriptorKind=="Texture"))
                    {
                        var read=sampled[(stage.Source.StageCode,d.Source.BindPoint)];
                        RequireDeviceTexture(rd,read,d,index,targets);using var shape=rd.TextureGetFormat(read.ShaderViewRid);
                        var selected=read as IMapPostEffect3ComputeSelectedMipTextureLease;
                        textureReads.Add(new(stage.Source.StageCode,d.Source.BindPoint,read.ParameterIndex,
                            actual.References[read.ParameterIndex].Reference.PhysicalAllocationParameterIndex,read.InstanceIndex,
                            read.StorageRid,read.ShaderViewRid,read.NativeViewFormat,read.DeviceFormat,read.Extent,
                            selected is{IsSourceSelectedMipView:true}?selected.NativeFirstMipLevel:0,
                            selected is{IsSourceSelectedMipView:true}?selected.NativeMipLevelCount:shape.Mipmaps,
                            read is IMapPostEffect3SourceTextureReadFootprintLease,read.SourceEvidence));
                    }
                    Rid framebuffer=Own(rd.FramebufferCreate(new Godot.Collections.Array<Rid>(targets.Select(t=>t.Target.TargetViewRid))));
                    if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("Fullscreen current target framebuffer is invalid.");
                    Rid Uniforms(MapPostEffect3CompiledStage stage)
                    {
                        if(stage.Bindings.Bindings.Count==0)return default;
                        var uniforms=new Godot.Collections.Array<RDUniform>();
                        try
                        {
                            foreach(var d in stage.Bindings.Bindings)
                            {
                                var uniform=new RDUniform{Binding=checked((int)d.Device.Binding!.Value),UniformType=d.DescriptorKind switch{
                                    "UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"Texture"=>RenderingDevice.UniformType.Texture,
                                    "TextureBuffer"=>RenderingDevice.UniformType.TextureBuffer,"Sampler"=>RenderingDevice.UniformType.Sampler,
                                    _=>throw new NotSupportedException()}};
                                uniforms.Add(uniform);uniform.AddId(mapped[(stage.Source.StageCode,d.DescriptorKind,d.Source.BindPoint)]);
                            }
                            uint set=stage.Source.StageCode==1?1u:0u;Rid rid=Own(rd.UniformSetCreate(uniforms,compiled.ShaderRid,set));
                            if(!rd.UniformSetIsValid(rid))throw new InvalidDataException("Fullscreen original stage uniform set is invalid.");return rid;
                        }
                        finally{foreach(var uniform in uniforms)uniform.Dispose();}
                    }
                    Rid vs=Uniforms(vertex),ps=Uniforms(pixel);
                    using var rasterState=new RDPipelineRasterizationState{CullMode=raster.Cull,FrontFace=raster.FrontFace};
                    using var multi=new RDPipelineMultisampleState();
                    using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false,DepthCompareOperator=raster.DepthCompare};
                    var blendAttachments=new Godot.Collections.Array<RDPipelineColorBlendStateAttachment>();
                    try
                    {
                        foreach(var a in raster.Attachments)blendAttachments.Add(new(){EnableBlend=a.EnableBlend,SrcColorBlendFactor=a.SourceColor,
                            DstColorBlendFactor=a.DestinationColor,ColorBlendOp=a.ColorOperation,SrcAlphaBlendFactor=a.SourceAlpha,
                            DstAlphaBlendFactor=a.DestinationAlpha,AlphaBlendOp=a.AlphaOperation,WriteR=a.WriteR,WriteG=a.WriteG,WriteB=a.WriteB,WriteA=a.WriteA});
                        using var blend=new RDPipelineColorBlendState{Attachments=blendAttachments};
                        Rid pipeline=Own(rd.RenderPipelineCreate(compiled.ShaderRid,rd.FramebufferGetFormat(framebuffer),-1,
                            source.DevicePrimitive,rasterState,multi,depth,blend));
                        if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Fullscreen original explicit VS/PS pipeline failed.");
                        foreach(var target in targets)
                        {
                            var lease=target.Target;uint mip=(lease as IMapPostEffect3FullscreenSelectedMipTargetLease)?.NativeFirstMipLevel??0;
                            int? physical=lease.ParameterIndex is{} parameter?actual.References[parameter].Reference.PhysicalAllocationParameterIndex:null;
                            long? revision=physical is{} owner?Bank.InvalidateTextureAfterShaderWrite(owner,lease.InstanceIndex,mip,source.FrameNumber,index):
                                lease is IMapPostEffect3FullscreenExternalWritableTargetLease writable?
                                    writable.InvalidateContentsBeforePixelWrite(index):null;
                            writes.Add(new(target.SourceTargetRegister,lease.ParameterIndex,physical,lease.InstanceIndex,lease.StorageRid,
                                lease.TargetViewRid,lease.NativeViewFormat,lease.Extent,mip,revision)
                                {PriorDestinationPixelsWereValidated=raster.Attachments[target.SourceTargetRegister].EnableBlend});
                        }
                        writeStarted=true;
                        long list=rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.DefaultAll,
                            region:new Rect2(actual.Viewport.Position,actual.Viewport.Size));
                        try
                        {
                            rd.DrawListBindRenderPipeline(list,pipeline);
                            if(vs.IsValid)rd.DrawListBindUniformSet(list,vs,1);if(ps.IsValid)rd.DrawListBindUniformSet(list,ps,0);
                            // Native coordinates are target absolute. Godot adds
                            // the viewport origin to its scissor coordinates.
                            if(clippedScissor is{} scissor)rd.DrawListEnableScissor(list,
                                new Rect2I(scissor.Position-actual.Viewport.Position,scissor.Size));
                            else rd.DrawListDisableScissor(list);
                            rd.DrawListDraw(list,false,source.InstanceCount,source.VertexCount);
                        }
                        finally{rd.DrawListEnd();}
                    }
                    finally{foreach(var a in blendAttachments)a.Dispose();}
                    return true;
                }
                finally
                {
                    for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);
                    owned.Clear();
                }
            });
            await renderingTask;
            RenderThreadSchedulingDiagnostics.CallerResumed(renderingTask);
            cpu?.Mark("08-render-callback-and-resume");
            if(!writeStarted&&!emptyPixelDomain)throw new InvalidDataException("Fullscreen source command did not reach actual submission.");
            actual.Validate();ValidateCbsAndReads();
            cpu?.Mark("09-post-submit-validation");
            return LastCompletedPass=new(source.FrameNumber,source.ContextOwnerIdentity,index,vertex.Source.Sha256,pixel.Source.Sha256,
                native,Array.AsReadOnly(writes.ToArray()),cbReads,actual.Viewport,actual.Scissor,raster,
                source.SourceEvidence+";"+input.CurrentDrawContext.SourceEvidence+
                ";counted-VS/PS-selection;DXBC-procedural-triangle3;"+
                (writeStarted?"actual-RD-submission;":"empty-native-scissor-pixel-domain;no-target-invalidation-or-pixel-draw;")+
                "no-full-coverage/history-claim")
                {TextureReads=Array.AsReadOnly(textureReads.ToArray()),WasPixelDrawSubmitted=writeStarted,
                    SourceNullShaderResources=actual.Nulls};
        }
        finally
        {
            try{if(owned.Count!=0)await FreeAsync(owned);}
            finally{bankScope?.Dispose();serial.Release();lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();
                cpu?.Mark("10-receipt-and-release");}
        }
    }
    private static Task<bool> FreeAsync(IReadOnlyList<Rid> owned)=>TerrainDeviceTexture.OnRenderThreadAsync(rd=>
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
