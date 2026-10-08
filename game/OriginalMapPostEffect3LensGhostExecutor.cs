using Godot;
using San14.Data;

/// <summary>The native Lens descriptor30/38 pointer is a separately selected
/// current resource. It cannot be inferred from texture table slot or name.</summary>
public sealed record OriginalMapPostEffect3LensDescriptorTextureInput(int DescriptorOffset,
    IOriginalMapPostEffect3ComputeTextureLease Texture,string ActualDescriptorSlotEvidence);

public sealed record OriginalMapPostEffect3LensGhostFrameInputs(
    OriginalMapPostEffect3TextureParameterState TextureParameters,
    IOriginalMapPostEffect3ComputeBufferLease CurrentSpriteBuffer,
    IOriginalMapPostEffect3ComputeTextureLease CurrentStarBurst,
    IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> CurrentPixelSamplers,
    IReadOnlyList<OriginalMapPostEffect3LensDescriptorTextureInput> CurrentDescriptorTextures,
    int ConstantBufferInstance,IOriginalMapPostEffect3StockGraphicsReadableTargetLease CurrentTarget,
    Rect2I ActualViewport,RenderingDevice.RenderPrimitive ActualSourcePrimitive,
    OriginalModelRasterInputs ActualInheritedRaster,string ActualInheritedDrawStateEvidence);

public sealed record OriginalMapPostEffect3LensGhostCompletedDraw(long FrameNumber,string ContextOwnerIdentity,
    uint VertexCount,uint InstanceCount,Rid TargetStorageRid,string SourceEvidence)
{
    public bool EstablishesCompleteResourceContents=>false;
    public bool CommitsFunctorHistory=>false;
    public OriginalMapPostEffect3LensGhostTextureWrite? TextureWrite{get;init;}
}

/// <summary>Concrete factory4 custom GhostSprite draw, borrowing the actual
/// VS0/PS2 and bank. Vertex/instance IDs supply geometry; native draw4×N uses
/// no vertex buffer or indirect argument buffer. Destination contents and the
/// produced sprite read range remain mandatory independent capabilities.</summary>
public sealed class OriginalMapPostEffect3LensGhostExecutor
{
    private readonly OriginalMapPostEffect3ProgramLibrary library;
    private readonly OriginalMapPostEffect3ResourceBank bank;
    public OriginalMapPostEffect3ResourceBank Bank=>bank;
    public OriginalMapPostEffect3LensGhostCompletedDraw? LastCompletedDraw{get;private set;}
    private readonly SemaphoreSlim serial=new(1,1);
    private OriginalMapPostEffect3LensGhostExecutor(OriginalMapPostEffect3ProgramLibrary library,
        OriginalMapPostEffect3ResourceBank bank)=>(this.library,this.bank)=(library,bank);
    public static OriginalMapPostEffect3LensGhostExecutor Create(
        OriginalMapPostEffect3ProgramLibrary borrowedLibrary,OriginalMapPostEffect3ResourceBank borrowedBank)
    {
        ArgumentNullException.ThrowIfNull(borrowedLibrary);ArgumentNullException.ThrowIfNull(borrowedBank);
        if(borrowedLibrary.SourceSha256!=borrowedBank.Source.Source.Sha256)
            throw new InvalidDataException("Lens Ghost library/bank source differs.");
        return new(borrowedLibrary,borrowedBank);
    }
    public async Task<OriginalMapPostEffect3LensGhostCompletedDraw> DrawAsync(
        OriginalMapPostEffect3LensExecutionRecipe recipe,OriginalMapPostEffect3LensGhostFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(recipe);ArgumentNullException.ThrowIfNull(input);
        if(!recipe.ExecutesPass||recipe.SourcePassIndex!=34||recipe.GhostDraw is not{} ghost||
            recipe.SourceSha256!=library.SourceSha256||recipe.ContextOwnerIdentity!=bank.Source.ContextOwnerIdentity||
            recipe.FrameNumber<=0||ghost.VertexCount!=4||ghost.StartVertex!=0||ghost.StartInstance!=0||ghost.UsesIndirectArgumentBuffer||
            ghost.NativePrimitiveCode!=4||ghost.NativeD3DPrimitiveTopology!=5||ghost.TemporaryDepthTestEnabled)
            throw new NotSupportedException("Lens Ghost needs the positively prepared current factory4 direct draw recipe.");
        if(input.TextureParameters.FrameNumber!=recipe.FrameNumber||input.TextureParameters.SourceSha256!=recipe.SourceSha256||
            input.TextureParameters.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||
            input.CurrentSpriteBuffer.ParameterIndex!=ghost.SpriteBufferParameterIndex||input.CurrentStarBurst.ParameterIndex!=36||
            input.CurrentTarget.ParameterIndex!=41||input.ActualSourcePrimitive!=RenderingDevice.RenderPrimitive.TriangleStrips||
            string.IsNullOrWhiteSpace(input.ActualInheritedDrawStateEvidence)||input.ActualViewport.Position.X<0||input.ActualViewport.Position.Y<0||
            input.ActualViewport.Size.X<=0||input.ActualViewport.Size.Y<=0)
            throw new InvalidDataException("Lens Ghost current resources/viewport/inherited topology are not the exact source draw inputs.");
        // Native1153244 command40031 selects opcode31/topology4.
        // Handler958980->962320->964ED0 maps table14BEA98[4] to
        // D3D topology5(TriangleStrip). Raster cull remains inherited.
        var raster=OriginalMapFinalOutputRasterSnapshot.Capture(input.ActualInheritedRaster);
        // Native1153201 temporarily disables depth testing regardless of the
        // inherited frontend1BF value. Its preserved depth-write setting has
        // no effect while depth is disabled; the actual target has no depth
        // attachment. Stencil still requires its own positively closed state.
        if(raster.NativeStencil is not null||raster.Attachments.Count!=1)
            throw new NotSupportedException("Lens Ghost inherited stencil or multiple targets requires a separate verified adapter.");
        var samplers=input.CurrentPixelSamplers.ToArray();var descriptors=input.CurrentDescriptorTextures.ToArray();
        var sprite=input.CurrentSpriteBuffer;var star=input.CurrentStarBurst;var target=input.CurrentTarget;
        OriginalMapPostEffect3LensGhostTextureWrite? textureWrite=null;
        var spriteKey=(sprite.FrameNumber,sprite.ContextOwnerIdentity,sprite.ParameterIndex,sprite.InstanceIndex,sprite.StorageRid,sprite.ShaderReadRid,
            sprite.DeviceTexelFormat,sprite.ByteCount,sprite.UploadRevision,sprite.SourceEvidence);
        var starKey=(star.FrameNumber,star.ContextOwnerIdentity,star.ParameterIndex,star.InstanceIndex,star.StorageRid,star.ShaderViewRid,star.NativeViewFormat,star.DeviceFormat,star.Extent,star.SourceEvidence);
        var targetKey=(target.FrameNumber,target.ContextOwnerIdentity,target.InstanceIndex,target.StorageRid,target.TargetViewRid,
            target.NativeViewFormat,target.DeviceFormat,target.Extent,target.SourceEvidence);
        var samplerKeys=samplers.Select(s=>(s.FrameNumber,s.ContextOwnerIdentity,s.NativeStageOrdinal,s.SourceRegister,s.SamplerRid,s.SourceEvidence)).ToArray();
        var descriptorKeys=descriptors.Select(d=>(d.DescriptorOffset,d.ActualDescriptorSlotEvidence,d.Texture.FrameNumber,d.Texture.ContextOwnerIdentity,
            d.Texture.StorageRid,d.Texture.ShaderViewRid,d.Texture.NativeViewFormat,d.Texture.Extent,d.Texture.SourceEvidence)).ToArray();
        var starReference=input.TextureParameters.Require(36);var targetReference=input.TextureParameters.Require(41);
        void Validate()
        {
            sprite.RequireCurrent();sprite.RequireReadRange(0,ghost.RequiredSpriteReadBytes);star.RequireCurrent();
            target.RequireCurrentForPass(34);
            if(textureWrite is null)target.RequireReadableForPass(34);
            input.TextureParameters.ValidateCurrent(starReference);input.TextureParameters.ValidateCurrent(targetReference);
            if(spriteKey!=(sprite.FrameNumber,sprite.ContextOwnerIdentity,sprite.ParameterIndex,sprite.InstanceIndex,sprite.StorageRid,sprite.ShaderReadRid,
                sprite.DeviceTexelFormat,sprite.ByteCount,sprite.UploadRevision,sprite.SourceEvidence)||
                starKey!=(star.FrameNumber,star.ContextOwnerIdentity,star.ParameterIndex,star.InstanceIndex,star.StorageRid,star.ShaderViewRid,star.NativeViewFormat,star.DeviceFormat,star.Extent,star.SourceEvidence)||
                targetKey!=(target.FrameNumber,target.ContextOwnerIdentity,target.InstanceIndex,target.StorageRid,target.TargetViewRid,target.NativeViewFormat,target.DeviceFormat,target.Extent,target.SourceEvidence))
                throw new InvalidDataException("Lens Ghost capability changed while its draw was queued.");
            if(sprite.FrameNumber!=recipe.FrameNumber||star.FrameNumber!=recipe.FrameNumber||target.FrameNumber!=recipe.FrameNumber||
                sprite.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||star.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||target.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||
                sprite.DeviceTexelFormat!=RenderingDevice.DataFormat.R32Uint||sprite.ByteCount<ghost.RequiredSpriteReadBytes||!sprite.ShaderReadRid.IsValid||
                bank.RequireBuffer(2,sprite.InstanceIndex).StorageRid!=sprite.StorageRid||star.StorageRid==target.StorageRid)
                throw new InvalidDataException("Lens Ghost does not own the required actual same-frame read ranges and distinct output storage.");
            foreach(var pair in new[]{(Reference:starReference.Reference,Storage:star.StorageRid,View:star.ShaderViewRid,Format:star.NativeViewFormat,Side:star.InstanceIndex),
                (Reference:targetReference.Reference,Storage:target.StorageRid,View:target.TargetViewRid,Format:target.NativeViewFormat,Side:target.InstanceIndex)})
            {
                if(pair.Reference.PhysicalAllocationParameterIndex is not{} physical||pair.Reference.InstanceIndex!=pair.Side)
                    throw new NotSupportedException("Lens Ghost source alias requires its exact current owned physical allocation.");
                var actual=bank.RequireTexture(physical,pair.Side);
                if(actual.StorageRid!=pair.Storage||!actual.NativeFormatViews.TryGetValue(pair.Format,out var view)||view!=pair.View)
                    throw new InvalidDataException("Lens Ghost resource is not its current source bank native view.");
            }
            for(int i=0;i<samplers.Length;i++)
            {
                var s=samplers[i];s.RequireCurrent();
                if(samplerKeys[i]!=(s.FrameNumber,s.ContextOwnerIdentity,s.NativeStageOrdinal,s.SourceRegister,s.SamplerRid,s.SourceEvidence)||
                    s.FrameNumber!=recipe.FrameNumber||s.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||s.NativeStageOrdinal!=2)
                    throw new InvalidDataException("Lens Ghost original pixel sampler selection changed.");
            }
            for(int i=0;i<descriptors.Length;i++)
            {
                var d=descriptors[i];d.Texture.RequireCurrent();
                if(descriptorKeys[i]!=(d.DescriptorOffset,d.ActualDescriptorSlotEvidence,d.Texture.FrameNumber,d.Texture.ContextOwnerIdentity,
                    d.Texture.StorageRid,d.Texture.ShaderViewRid,d.Texture.NativeViewFormat,d.Texture.Extent,d.Texture.SourceEvidence)||
                    d.Texture.FrameNumber!=recipe.FrameNumber||d.Texture.ContextOwnerIdentity!=recipe.ContextOwnerIdentity||
                    d.Texture.StorageRid==target.StorageRid||string.IsNullOrWhiteSpace(d.ActualDescriptorSlotEvidence))
                    throw new InvalidDataException("Lens Ghost descriptor pointer producer changed or aliases its target.");
            }
        }
        Validate();await serial.WaitAsync();var owned=new List<Rid>();IDisposable? bankScope=null;
        try
        {
            LastCompletedDraw=null;
            bankScope=await bank.AcquireBufferExecutionScopeAsync();
            var compiled=await library.GetCompiledPassAsync(34);
            var vertex=compiled.Stages.Single(s=>s.Source.StageCode==1);var pixel=compiled.Stages.Single(s=>s.Source.StageCode==0);
            if(compiled.Kind!=OriginalMapPostEffect3CompiledPassKind.Graphics||compiled.StockScreenVertex is not null||
                vertex.Source.StageIndex!=0||pixel.Source.StageIndex!=2||
                vertex.Bindings.Interface.StageVariables.Any(v=>v.StorageClass==1&&v.Location is not null)||
                vertex.Bindings.Bindings.Any(d=>d.DescriptorKind is not ("TextureBuffer" or "UniformBuffer"))||
                pixel.Bindings.Bindings.Any(d=>d.DescriptorKind is not ("Texture" or "UniformBuffer" or "Sampler")))
                throw new NotSupportedException("Lens Ghost counted VS0/PS2 has an unsupported actual device interface.");
            var cb=bank.RequireConstantBufferForPass(6,input.ConstantBufferInstance,34,recipe.FrameNumber);long revision=cb.UploadRevision;
            void ValidateCb()
            {if(!ReferenceEquals(cb,bank.RequireConstantBufferForPass(6,input.ConstantBufferInstance,34,recipe.FrameNumber))||cb.UploadRevision!=revision)
                throw new InvalidDataException("Lens Ghost source CB changed during actual drawing.");}
            var mapped=new Dictionary<(uint Stage,string Kind,uint Register),Rid>();
            foreach(var b in recipe.OrderedEffectiveBindings)
            {
                foreach(uint stage in new uint[]{0,1})
                {
                    if((b.StageMask&(stage==1?2:4))==0)continue;
                    if(b.Role==OriginalMapPostEffect3BindingRole.ConstantBufferRead)
                    {if(b.ParameterIndex!=6)throw new NotSupportedException("Lens Ghost has an unsupported native CB owner.");mapped[(stage,"UniformBuffer",(uint)b.RegisterIndex)]=cb.StorageRid;}
                    else if(b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&b.Table==OriginalMapPostEffect3BindingTable.BufferParameters)
                    {if(b.ParameterIndex!=2)throw new NotSupportedException("Lens Ghost has an unsupported native buffer read.");mapped[(stage,"TextureBuffer",(uint)b.RegisterIndex)]=sprite.ShaderReadRid;}
                    else if(b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead)
                    {if(b.ParameterIndex!=36)throw new NotSupportedException("Lens Ghost has an unsupported native sampled texture.");mapped[(stage,"Texture",(uint)b.RegisterIndex)]=star.ShaderViewRid;}
                }
            }
            foreach(var submission in recipe.DescriptorTextureSubmissions.Where(s=>s.NativeStageCode==2))
            {
                var actual=descriptors.Where(d=>d.DescriptorOffset==submission.DescriptorTextureOffset).ToArray();
                bool used=pixel.Bindings.Bindings.Any(d=>d.DescriptorKind=="Texture"&&d.Source.BindPoint==submission.RegisterIndex);
                if(used&&actual.Length!=1)throw new NotSupportedException("Lens Ghost shader reads descriptor30/38 without its exact current pointer producer.");
                if(actual.Length==1)mapped[(0,"Texture",(uint)submission.RegisterIndex)]=actual[0].Texture.ShaderViewRid;
            }
            var wanted=pixel.Bindings.Bindings.Where(d=>d.DescriptorKind=="Sampler").Select(d=>d.Source.BindPoint).Order();
            if(!wanted.SequenceEqual(samplers.Select(s=>s.SourceRegister).Order()))
                throw new InvalidDataException("Lens Ghost needs exactly the actual original PS sampler register selections.");
            foreach(var s in samplers)mapped.Add((0,"Sampler",s.SourceRegister),s.SamplerRid);
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("Lens Ghost RD allocation failed.");owned.Add(rid);return rid;}
                Validate();ValidateCb();
                if(!rd.TextureIsValid(target.TargetViewRid)||input.ActualViewport.End.X>target.Extent.X||input.ActualViewport.End.Y>target.Extent.Y)
                    throw new InvalidDataException("Lens Ghost current target/view region is invalid.");
                Rid framebuffer=Own(rd.FramebufferCreate([target.TargetViewRid]));
                Rid Set(OriginalMapPostEffect3CompiledStage stage)
                {
                    if(stage.Bindings.Bindings.Count==0)return default;
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var d in stage.Bindings.Bindings)
                        {
                            if(!mapped.TryGetValue((stage.Source.StageCode,d.DescriptorKind,d.Source.BindPoint),out var rid))
                                throw new NotSupportedException("Lens Ghost selected shader requires an unproved implicit resource.");
                            if(d.Device.DescriptorSet!=(stage.Source.StageCode==1?1u:0u))throw new InvalidDataException("Lens Ghost shader stage set changed.");
                            if(d.DescriptorKind=="Texture")
                            {
                                if(!rd.TextureIsValid(rid))throw new InvalidDataException("Lens Ghost sampled view retired.");
                                using var shape=rd.TextureGetFormat(rid);
                                if(d.Source.Dimension!=4||d.Source.ReturnType!=5||shape.TextureType!=RenderingDevice.TextureType.Type2D||
                                    shape.ArrayLayers!=1||!shape.UsageBits.HasFlag(RenderingDevice.TextureUsageBits.SamplingBit)||
                                    shape.Format.ToString().Contains("Uint",StringComparison.Ordinal)||shape.Format.ToString().Contains("Sint",StringComparison.Ordinal))
                                    throw new NotSupportedException("Lens Ghost sampled resource differs from its native floating Texture2D interface.");
                            }
                            var u=new RDUniform{Binding=checked((int)d.Device.Binding!.Value),UniformType=d.DescriptorKind switch{
                                "UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"TextureBuffer"=>RenderingDevice.UniformType.TextureBuffer,
                                "Texture"=>RenderingDevice.UniformType.Texture,"Sampler"=>RenderingDevice.UniformType.Sampler,_=>throw new NotSupportedException()}};
                            uniforms.Add(u);u.AddId(rid);
                        }
                        Rid set=Own(rd.UniformSetCreate(uniforms,compiled.ShaderRid,stage.Source.StageCode==1?1u:0u));
                        if(!rd.UniformSetIsValid(set))throw new InvalidDataException("Lens Ghost actual uniform set failed.");return set;
                    }
                    finally{foreach(var u in uniforms)u.Dispose();}
                }
                Rid psSet=Set(pixel),vsSet=Set(vertex);
                //1153201 disables frontend1BF depth test. Cull(frontendE8)
                // is inherited; the source Ghost factory does not replace it.
                using var rasterState=new RDPipelineRasterizationState{CullMode=raster.Cull,FrontFace=raster.FrontFace};
                using var multi=new RDPipelineMultisampleState();using var depth=new RDPipelineDepthStencilState{EnableDepthTest=false,EnableDepthWrite=false};
                var mask=raster.Attachments.Single();
                // Native00114400 decodes through966330/94AF20 to
                // SrcAlpha/One/Add for RGB and alpha. Eye00111100 uses a
                // different source factor; its One/One formula cannot be reused.
                using var attachment=new RDPipelineColorBlendStateAttachment{EnableBlend=true,
                    SrcColorBlendFactor=RenderingDevice.BlendFactor.SrcAlpha,DstColorBlendFactor=RenderingDevice.BlendFactor.One,ColorBlendOp=RenderingDevice.BlendOperation.Add,
                    SrcAlphaBlendFactor=RenderingDevice.BlendFactor.SrcAlpha,DstAlphaBlendFactor=RenderingDevice.BlendFactor.One,AlphaBlendOp=RenderingDevice.BlendOperation.Add,
                    WriteR=mask.WriteR,WriteG=mask.WriteG,WriteB=mask.WriteB,WriteA=mask.WriteA};
                using var blend=new RDPipelineColorBlendState{Attachments=[attachment]};
                Rid pipeline=Own(rd.RenderPipelineCreate(compiled.ShaderRid,rd.FramebufferGetFormat(framebuffer),-1,
                    input.ActualSourcePrimitive,rasterState,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("Lens Ghost original custom pipeline failed.");
                if(ghost.InstanceCount!=0)
                {
                    // The bank proves the complete prior destination under
                    // its lock and invalidates old readiness before any draw.
                    // Failed submission leaves the new revision unreadable.
                    textureWrite=bank.BeginLensGhostPreservingTextureWrite(recipe,41,target.InstanceIndex);
                    long list=rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.DefaultAll,region:new Rect2(input.ActualViewport.Position,input.ActualViewport.Size));
                    try{rd.DrawListBindRenderPipeline(list,pipeline);if(psSet.IsValid)rd.DrawListBindUniformSet(list,psSet,0);
                        if(vsSet.IsValid)rd.DrawListBindUniformSet(list,vsSet,1);rd.DrawListDraw(list,false,ghost.InstanceCount,ghost.VertexCount);}
                    finally{rd.DrawListEnd();}
                }
                return true;
            });
            Validate();ValidateCb();
            var completed=new OriginalMapPostEffect3LensGhostCompletedDraw(recipe.FrameNumber,recipe.ContextOwnerIdentity,ghost.VertexCount,ghost.InstanceCount,target.StorageRid,
                recipe.SourceEvidence+";"+input.ActualInheritedDrawStateEvidence+";actual-RD-source-direct-instanced-draw;no-indirect/no-complete-coverage-claim")
                {TextureWrite=textureWrite};
            LastCompletedDraw=completed;return completed;
        }
        finally
        {
            try{if(owned.Count!=0)await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);return true;});}
            catch{LastCompletedDraw=null;throw;}
            finally{bankScope?.Dispose();serial.Release();}
        }
    }
}
