using Godot;
using San14.Data;

public sealed record OriginalMapPostEffect3DepthOfField2ScatterInputs(
    OriginalMapPostEffect3ParameterState State,
    OriginalMapPostEffect3ParameterDepthOfField2Update Prepared,
    OriginalMapPostEffect3DepthOfField2BeforeBindings Before,
    OriginalMapPostEffect3TextureParameterState TextureParameters,
    OriginalMapPostEffect3DepthOfField2TargetView ActualTargetView,
    IOriginalMapPostEffect3FullscreenDrawContextLease ActualPostBindingContext,
    OriginalMapPostEffect3DepthOfField2SpritePrefix ActualSpritePrefix,
    IOriginalMapPostEffect3ComputeBufferLease ActualSpriteRead,
    IOriginalMapPostEffect3ComputeBufferLease ActualArguments,int ActualCb18Instance)
{
    public OriginalMapPostEffect3DepthOfField2RetainedAccumulation? PriorDestination{get;init;}
}

public sealed record OriginalMapPostEffect3DepthOfField2StagingCopyReceipt(long FrameNumber,int SourcePassIndex,
    uint SourceSide28,Rid ActualArgumentsStorageRid,Rid ActualStagingStorageRid,uint ByteCount,string SourceEvidence)
{
    public int ActualArgumentsPhysicalSide{get;init;}
    public long ActualArgumentsWriterRevision{get;init;}
    public long ActualStagingWriteRevision{get;init;}
}

/// <summary>Actual source procedural scatter62/63. Keeps original
/// VS9/10+PS12/13, RGBA16F typed sprite prefix and real indirect B5 offsets.
/// Source Before's clear-mask/color are executed at Bind before Execute's
/// scissor/blend. The two source staging buffers belong to this owner.</summary>
public sealed partial class OriginalMapPostEffect3DepthOfField2ScatterExecutor:IAsyncDisposable,IOriginalMapPostEffect3DepthOfField2ScatterSubmissionSource
{
    public OriginalMapPostEffect3ProgramLibrary Library{get;}
    public OriginalMapPostEffect3ResourceBank Bank{get;}
    public OriginalMapPostEffect3FullscreenCompletedPass? LastCompletedPass{get;private set;}
    public OriginalMapPostEffect3DepthOfField2StagingCopyReceipt? LastStagingCopy{get;private set;}
    public OriginalMapPostEffect3DepthOfField2ScatterInputs? LastInputs{get;private set;}
    private readonly Rid[] staging;
    private readonly SemaphoreSlim serial=new(1,1);
    private bool retiring,disposed;
    private OriginalMapPostEffect3DepthOfField2ScatterExecutor(OriginalMapPostEffect3ProgramLibrary library,
        OriginalMapPostEffect3ResourceBank bank,Rid[] staging)=>(Library,Bank,this.staging)=(library,bank,staging);
    public static async Task<OriginalMapPostEffect3DepthOfField2ScatterExecutor> BuildBorrowedAsync(
        OriginalMapPostEffect3ProgramLibrary library,OriginalMapPostEffect3ResourceBank bank)
    {
        if(library.SourceSha256!=bank.Source.Source.Sha256)throw new InvalidDataException("DOF2 scatter library and bank differ.");
        Rid[] staging=await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var pair=new Rid[2];
            try{for(int i=0;i<2;i++){pair[i]=rd.StorageBufferCreate(32);if(!pair[i].IsValid)throw new InvalidDataException("DOF2 source staging32B allocation failed.");}return pair;}
            catch{foreach(var rid in pair)if(rid.IsValid)rd.FreeRid(rid);throw;}
        });
        return new(library,bank,staging);
    }
    public async Task<OriginalMapPostEffect3FullscreenCompletedPass> DrawAsync(OriginalMapPostEffect3DepthOfField2ScatterInputs input)
    {
        await serial.WaitAsync();var owned=new List<Rid>();IDisposable? bankScope=null;
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);LastCompletedPass=null;LastStagingCopy=null;LastInputs=null;
            var prepared=input.Prepared;var before=input.Before;var table=input.TextureParameters;
            int pass=before.SourcePassIndex;var view=input.ActualTargetView;var prefix=input.ActualSpritePrefix;
            var acceptedContext=input.ActualPostBindingContext;
            var acceptedRaster=OriginalMapFinalOutputRasterSnapshot.Capture(acceptedContext.ActualRaster);
            var acceptedContextKey=(acceptedContext.FrameNumber,acceptedContext.ContextOwnerIdentity,acceptedContext.OwnedFrontendIdentity,
                acceptedContext.ViewIdentity,acceptedContext.ActualViewport,acceptedContext.ActualViewportMinimumDepth,
                acceptedContext.ActualViewportMaximumDepth,acceptedContext.ActualScissor,acceptedContext.SourceEvidence);
            void Require()
            {
                prepared.RequireCurrentScalarState(input.State);prepared.RequireCurrentTextureTable(table);before.RequirePrepared(prepared);prefix.RequireCurrent();
                input.ActualPostBindingContext.RequireCurrentForPass(pass);input.ActualArguments.RequireCurrent();input.ActualSpriteRead.RequireCurrent();
                table.ValidateCurrent(view.ActualPointer);
                var now=acceptedContext.ActualRaster;
                if(acceptedContextKey!=(acceptedContext.FrameNumber,acceptedContext.ContextOwnerIdentity,acceptedContext.OwnedFrontendIdentity,
                    acceptedContext.ViewIdentity,acceptedContext.ActualViewport,acceptedContext.ActualViewportMinimumDepth,
                    acceptedContext.ActualViewportMaximumDepth,acceptedContext.ActualScissor,acceptedContext.SourceEvidence)||
                    now.Cull!=acceptedRaster.Cull||now.FrontFace!=acceptedRaster.FrontFace||now.DepthTest!=acceptedRaster.DepthTest||
                    now.DepthWrite!=acceptedRaster.DepthWrite||now.DepthCompare!=acceptedRaster.DepthCompare||now.NativeStencil!=acceptedRaster.NativeStencil||
                    !now.Attachments.SequenceEqual(acceptedRaster.Attachments))
                    throw new InvalidDataException("DOF2 actual post-Bind frontend changed while its original scatter command was queued.");
                if(pass is not(62 or 63)||!before.RequiresNativeIndirectDraw||!before.RequiresSourceStagingBufferCopy||
                    before.NativeIndirectBufferParameterIndex!=5||before.NativeIndirectArgumentByteOffset!=(pass==62?0:16)||
                    before.ActualStagingSide28>1||
                    (before.RequiresNativeClearMaskAndColorOverride?
                        before.NativeClearMaskContext1C18!=15||before.NativeClearColorRgba8Context1C1C!=0||input.PriorDestination is not null:
                        input.PriorDestination is null)||
                    before.TemporaryNativeColorStateF4!=0x111100u||before.TemporaryNativeRt0BlendEnable1C0!=1||
                    !ReferenceEquals(prefix.Owner.Bank,Bank)||prefix.ParameterIndex!=(pass==62?7:6)||
                    input.ActualSpriteRead.ParameterIndex!=prefix.ParameterIndex||input.ActualSpriteRead.InstanceIndex!=prefix.InstanceIndex||
                    input.ActualSpriteRead.StorageRid!=prefix.StorageRid||input.ActualSpriteRead.UploadRevision!=prefix.WriterRevision||
                    input.ActualSpriteRead.DeviceTexelFormat!=RenderingDevice.DataFormat.R16G16B16A16Sfloat||
                    input.ActualSpriteRead.ByteCount!=prefix.CapacityByteCount||
                    input.ActualArguments.ParameterIndex!=5||input.ActualArguments.StorageRid!=prefix.ArgumentsStorageRid||
                    input.ActualArguments.UploadRevision!=prefix.ArgumentsWriterRevision||input.ActualArguments.ByteCount!=32||
                    view.SourceTargetRegister!=0||view.ActualPointer.ParameterIndex!=(pass==62?63:64)||
                    view.ActualPointer.Reference.PhysicalAllocationParameterIndex!=view.ActualPointer.ParameterIndex||
                    view.NativeViewFormat!=10||view.DeviceFormat!=RenderingDevice.DataFormat.R16G16B16A16Sfloat||
                    !input.State.RequireValue("Viewport").Span.SequenceEqual(Convert.FromHexString("00000000000000000000803F0000803F")))
                    throw new NotSupportedException("DOF2 scatter requires actual conditional-clear/preserved-destination, staging-copy, indirect and typed-prefix source lifecycle.");
                if(prefix.ReadableByteCount!=0)input.ActualSpriteRead.RequireReadRange(0,prefix.ReadableByteCount);
                input.ActualArguments.RequireReadRange(0,32);
            }
            Require();bankScope=await Bank.AcquireBufferExecutionScopeAsync();
            var compiled=await Library.GetCompiledPassAsync(pass);
            var vertex=compiled.Stages.Single(s=>s.Source.StageCode==1);var pixel=compiled.Stages.Single(s=>s.Source.StageCode==0);
            if(compiled.Kind!=OriginalMapPostEffect3CompiledPassKind.Graphics||compiled.StockScreenVertex is not null||
                vertex.Source.StageIndex!=(pass==62?9:10)||pixel.Source.StageIndex!=(pass==62?12:13)||
                vertex.Source.Sha256!="2BC0B1DFF48DB8BB5D0EE6669D9483A6DAFA46FDFCA5C81DEEBAFA60691D1344"||
                pixel.Source.Sha256!="0FCC7E890B7FC7882EFA3FE1BB358A812FF49D067ACA776D26B22CA9A7EFBB40"||
                compiled.Stages.SelectMany(s=>s.Bindings.Bindings).Any(b=>b.DescriptorKind is not("UniformBuffer" or "TextureBuffer")))
                throw new NotSupportedException("DOF2 procedural scatter selected another original VS/PS or interface.");
            var context=input.ActualPostBindingContext;var snapshot=acceptedRaster;
            if(context.FrameNumber!=prepared.FrameNumber||context.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
                context.ActualViewport!=new Rect2I(Vector2I.Zero,view.Extent)||context.ActualScissor is not null||
                context.ActualViewportMinimumDepth!=0||context.ActualViewportMaximumDepth!=1||
                snapshot.DepthTest||snapshot.DepthWrite||snapshot.NativeStencil is not null||snapshot.Attachments.Count!=1)
                throw new NotSupportedException("DOF2 source clear currently requires its actual full wrapper viewport, disabled inherited scissor and color-only post-Bind state.");
            var raster=snapshot with{Attachments=Array.AsReadOnly(snapshot.Attachments.Select(a=>a with{
                EnableBlend=true,SourceColor=RenderingDevice.BlendFactor.One,DestinationColor=RenderingDevice.BlendFactor.One,
                ColorOperation=RenderingDevice.BlendOperation.Add,SourceAlpha=RenderingDevice.BlendFactor.One,
                DestinationAlpha=RenderingDevice.BlendFactor.One,AlphaOperation=RenderingDevice.BlendOperation.Add}).ToArray())};
            var cb=Bank.RequireConstantBufferForPass(18,input.ActualCb18Instance,pass,prepared.FrameNumber);
            string expected=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(prepared.ConstantBuffer.Bytes.Span));
            long cbRevision=cb.UploadRevision;
            if(!cb.HasCompleteWriterContents||cb.CompleteBytesSha256!=expected)throw new InvalidDataException("DOF2 scatter actual CB18 differs from its source writer.");
            OriginalMapPostEffect3FullscreenWriteReceipt? write=null;
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                Require();
                Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("DOF2 scatter RD resource creation failed.");owned.Add(rid);return rid;}
                if(cb.UploadRevision!=cbRevision||cb.CompleteBytesSha256!=expected||!rd.TextureIsValid(view.TargetViewRid)||
                    !input.ActualArguments.StorageRid.IsValid)throw new InvalidDataException("DOF2 actual scatter owners changed before submission.");
                var actualTexture=Bank.RequireTexture(view.ActualPointer.ParameterIndex,view.ActualPointer.Reference.InstanceIndex);
                if(actualTexture.StorageRid!=view.StorageRid||actualTexture.NativeFormatViews[10]!=view.TargetViewRid||actualTexture.Shape.MipLevels!=1||
                    view.Extent!=new Vector2I((int)actualTexture.Shape.Width,(int)actualTexture.Shape.Height))
                    throw new InvalidDataException("DOF2 source scatter target metadata changed.");
                Rid framebuffer=Own(rd.FramebufferCreate(new Godot.Collections.Array<Rid>{view.TargetViewRid}));
                if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("DOF2 actual scatter framebuffer is invalid.");
                Rid Uniforms(OriginalMapPostEffect3CompiledStage stage)
                {
                    if(stage.Bindings.Bindings.Count==0)return default;
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var binding in stage.Bindings.Bindings)
                        {
                            var row=Bank.Source.Passes[pass].ActiveBindings.Last(r=>r.RegisterIndex==binding.Source.BindPoint&&
                                (r.StageMask&(stage.Source.StageCode==1?2:4))!=0&&r.Role==(binding.DescriptorKind=="UniformBuffer"?
                                    OriginalMapPostEffect3BindingRole.ConstantBufferRead:OriginalMapPostEffect3BindingRole.ShaderResourceRead));
                            if(binding.DescriptorKind=="UniformBuffer"&&row.ParameterIndex!=18||binding.DescriptorKind=="TextureBuffer"&&
                                (stage.Source.StageCode!=1||row.ParameterIndex!=prefix.ParameterIndex||binding.Source.ReturnType!=5))
                                throw new InvalidDataException("DOF2 scatter shader binding differs from its original CB/float4 sprite source.");
                            var uniform=new RDUniform{Binding=checked((int)binding.Device.Binding!.Value),UniformType=binding.DescriptorKind=="UniformBuffer"?
                                RenderingDevice.UniformType.UniformBuffer:RenderingDevice.UniformType.TextureBuffer};uniforms.Add(uniform);
                            uniform.AddId(binding.DescriptorKind=="UniformBuffer"?cb.StorageRid:input.ActualSpriteRead.ShaderReadRid);
                        }
                        Rid set=Own(rd.UniformSetCreate(uniforms,compiled.ShaderRid,stage.Source.StageCode==1?1u:0u));
                        if(!rd.UniformSetIsValid(set))throw new InvalidDataException("DOF2 original scatter uniform set is invalid.");return set;
                    }
                    finally{foreach(var uniform in uniforms)uniform.Dispose();}
                }
                Rid vs=Uniforms(vertex),ps=Uniforms(pixel);
                using var rs=new RDPipelineRasterizationState{CullMode=raster.Cull,FrontFace=raster.FrontFace};
                using var ms=new RDPipelineMultisampleState();using var depth=new RDPipelineDepthStencilState{DepthCompareOperator=raster.DepthCompare};
                var color=raster.Attachments[0];using var attachment=new RDPipelineColorBlendStateAttachment{EnableBlend=true,
                    SrcColorBlendFactor=color.SourceColor,DstColorBlendFactor=color.DestinationColor,ColorBlendOp=color.ColorOperation,
                    SrcAlphaBlendFactor=color.SourceAlpha,DstAlphaBlendFactor=color.DestinationAlpha,AlphaBlendOp=color.AlphaOperation,
                    WriteR=color.WriteR,WriteG=color.WriteG,WriteB=color.WriteB,WriteA=color.WriteA};
                using var blend=new RDPipelineColorBlendState{Attachments=new Godot.Collections.Array<RDPipelineColorBlendStateAttachment>{attachment}};
                Rid pipeline=Own(rd.RenderPipelineCreate(compiled.ShaderRid,rd.FramebufferGetFormat(framebuffer),-1,
                    RenderingDevice.RenderPrimitive.TriangleStrips,rs,ms,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline))throw new InvalidDataException("DOF2 original scatter pipeline failed.");
                // Source Before clears only when its real count flag is set.
                // Otherwise additive scatter needs the prior complete pixels
                // of this exact retained physical side. Validate before the
                // new writer invalidates that older capability.
                input.PriorDestination?.RequireFor(input);
                long revision=Bank.InvalidateTextureAfterShaderWrite(view.ActualPointer.ParameterIndex,
                    view.ActualPointer.Reference.InstanceIndex,0,prepared.FrameNumber,pass);
                //Native Bind executes its complete target clear before the
                //factory Execute staging copy and indirect draw command.
                if(before.RequiresNativeClearMaskAndColorOverride)
                {
                    rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.ClearColor0,
                        clearColorValues:new[]{new Color(0,0,0,0)},region:new Rect2(Vector2.Zero,view.Extent));
                    rd.DrawListEnd();
                }
                else if(revision!=checked(input.PriorDestination!.WriterRevision+1))
                    throw new InvalidDataException("DOF2 preserved destination changed before the new scatter writer.");
                uint side=before.ActualStagingSide28;
                if(rd.BufferCopy(input.ActualArguments.StorageRid,staging[side],0,0,32)!=Error.Ok)
                    throw new InvalidDataException("DOF2 original0x50 staging copy failed.");
                RecordActualStagingCopy(input,side,pass);
                long list=rd.DrawListBegin(framebuffer,RenderingDevice.DrawFlags.DefaultAll,region:new Rect2(Vector2.Zero,view.Extent));
                try
                {
                    rd.DrawListBindRenderPipeline(list,pipeline);if(vs.IsValid)rd.DrawListBindUniformSet(list,vs,1);if(ps.IsValid)rd.DrawListBindUniformSet(list,ps,0);
                    //1155CF0 enables its floor(target0*Viewport) scissor.
                    //The admitted source rectangle is exactly the full target.
                    rd.DrawListEnableScissor(list,new Rect2I(Vector2I.Zero,view.Extent));
                    rd.DrawListDrawIndirect(list,false,input.ActualArguments.StorageRid,checked((uint)before.NativeIndirectArgumentByteOffset),1,16);
                }
                finally{rd.DrawListEnd();}
                write=new(0,view.ActualPointer.ParameterIndex,view.ActualPointer.ParameterIndex,view.ActualPointer.Reference.InstanceIndex,
                    view.StorageRid,view.TargetViewRid,10,view.Extent,0,revision){PriorDestinationPixelsWereValidated=true};return true;
            });
            Require();if(cb.UploadRevision!=cbRevision)throw new InvalidDataException("DOF2 scatter CB18 changed during the source draw.");
            LastInputs=input;
            return LastCompletedPass=new(prepared.FrameNumber,Bank.Source.ContextOwnerIdentity,pass,vertex.Source.Sha256,pixel.Source.Sha256,
                Array.AsReadOnly(Bank.Source.Passes[pass].ActiveBindings.ToArray()),Array.AsReadOnly(new[]{write??throw new InvalidDataException("DOF2 scatter was not submitted.")}),
                Array.AsReadOnly(new[]{new OriginalMapPostEffect3ComputeConstantBufferReadReceipt(18,input.ActualCb18Instance,cb.StorageRid,cbRevision,true,expected)}),
                context.ActualViewport,new Rect2I(Vector2I.Zero,view.Extent),raster,before.SourceEvidence+
                (before.RequiresNativeClearMaskAndColorOverride?";actual-native-Bind-full-RT-clearMask15/color0;":
                    ";actual-native-Bind-no-clear;"+input.PriorDestination!.SourceEvidence+";")+
                "source0x50-staging32B-copy;source0x40032-indirect-B5-offset0/16/TriangleStrip;"+prefix.SourceEvidence){WasPixelDrawSubmitted=true};
        }
        finally
        {
            try{if(owned.Count!=0)await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);return true;});}
            finally{bankScope?.Dispose();serial.Release();}
        }
    }
    public async ValueTask DisposeAsync()
    {
        retiring=true;await serial.WaitAsync();
        try
        {
            if(disposed)return;
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                for(int i=0;i<staging.Length;i++)
                {if(!staging[i].IsValid)continue;rd.FreeRid(staging[i]);staging[i]=default;}
                return true;
            });
            LastCompletedPass=null;LastStagingCopy=null;LastInputs=null;disposed=true;
        }
        finally{serial.Release();}
    }
}
