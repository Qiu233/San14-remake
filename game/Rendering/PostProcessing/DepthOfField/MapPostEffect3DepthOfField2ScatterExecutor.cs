using Godot;
using San14.Data;

public sealed record MapPostEffect3DepthOfField2ScatterInputs(
    MapPostEffect3ParameterState State,
    MapPostEffect3ParameterDepthOfField2Update Prepared,
    MapPostEffect3DepthOfField2BeforeBindings Before,
    MapPostEffect3TextureParameterState TextureParameters,
    MapPostEffect3DepthOfField2TargetView ActualTargetView,
    IMapPostEffect3FullscreenDrawContextLease ActualPostBindingContext,
    MapPostEffect3DepthOfField2SpritePrefix ActualSpritePrefix,
    IMapPostEffect3ComputeBufferLease ActualSpriteRead,
    IMapPostEffect3ComputeBufferLease ActualArguments,int ActualCb18Instance)
{
    public MapPostEffect3DepthOfField2RetainedAccumulation? PriorDestination{get;init;}
}

public sealed record MapPostEffect3DepthOfField2StagingCopyReceipt(long FrameNumber,int SourcePassIndex,
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
public sealed partial class MapPostEffect3DepthOfField2ScatterExecutor:IAsyncDisposable,IMapPostEffect3DepthOfField2ScatterSubmissionSource
{
    public MapPostEffect3ProgramLibrary Library{get;}
    public PostProcessResourceBank Bank{get;}
    public MapPostEffect3FullscreenCompletedPass? LastCompletedPass{get;private set;}
    public MapPostEffect3DepthOfField2StagingCopyReceipt? LastStagingCopy{get;private set;}
    public MapPostEffect3DepthOfField2ScatterInputs? LastInputs{get;private set;}
    private readonly Rid[] staging;
    private readonly SemaphoreSlim serial=new(1,1);
    private bool retiring,disposed;
    private NativeMapBorrowerRetirementPackage? pendingDraw;
    private sealed class DrawResources(IDisposable bankScope,MapPostEffect3DepthOfField2ScatterInputs input):IAsyncDisposable
    {
        public RenderDeviceResources Resources{get;}=new("DOF2 scatter RD resource creation failed.");
        private IDisposable? scope=bankScope;
        public async ValueTask DisposeAsync()
        {
            if(Resources.Count!=0)await Resources.DisposeAsync();
            GC.KeepAlive(input);
            scope?.Dispose();scope=null;
        }
    }
    private MapPostEffect3DepthOfField2ScatterExecutor(MapPostEffect3ProgramLibrary library,
        PostProcessResourceBank bank,Rid[] staging)=>(Library,Bank,this.staging)=(library,bank,staging);
    public static async Task<MapPostEffect3DepthOfField2ScatterExecutor> BuildBorrowedAsync(
        MapPostEffect3ProgramLibrary library,PostProcessResourceBank bank)
    {
        if(library.SourceSha256!=bank.Source.Source.Sha256)throw new InvalidDataException("DOF2 scatter library and bank differ.");
        return await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            var pair=new Rid[2];var partial=new RenderDeviceResources("DOF2 source staging32B allocation failed.");
            try
            {
                for(int i=0;i<2;i++)pair[i]=partial.Own(rd.StorageBufferCreate(32));
                // Successful construction transfers both handles to the
                // executor's staging array and its existing per-side retirement.
                return new MapPostEffect3DepthOfField2ScatterExecutor(library,bank,pair);
            }
            catch(Exception failure)
            {partial.ReleaseAfterConstructionFailure(rd,failure,"DOF2-scatter-staging");throw;}
        });
    }
    public async Task<MapPostEffect3FullscreenCompletedPass> DrawAsync(MapPostEffect3DepthOfField2ScatterInputs input)
    {
        await serial.WaitAsync();DrawResources? resources=null;Exception? drawFailure=null;
        MapPostEffect3FullscreenCompletedPass? completed=null;
        try
        {
            ObjectDisposedException.ThrowIf(retiring,this);await DrainPendingDrawCoreAsync();
            LastCompletedPass=null;LastStagingCopy=null;LastInputs=null;
            var prepared=input.Prepared;var before=input.Before;var table=input.TextureParameters;
            int pass=before.SourcePassIndex;var view=input.ActualTargetView;var prefix=input.ActualSpritePrefix;
            var acceptedContext=input.ActualPostBindingContext;
            var acceptedRaster=MapFinalOutputRasterSnapshot.Capture(acceptedContext.ActualRaster);
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
            Require();resources=new(await Bank.AcquireBufferExecutionScopeAsync(),input);
            var compiled=await Library.GetCompiledPassAsync(pass);
            var vertex=compiled.Stages.Single(s=>s.Source.StageCode==1);var pixel=compiled.Stages.Single(s=>s.Source.StageCode==0);
            if(compiled.Kind!=MapPostEffect3CompiledPassKind.Graphics||compiled.StockScreenVertex is not null||
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
            MapPostEffect3FullscreenWriteReceipt? write=null;
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                Require();
                Rid Own(Rid rid)=>resources.Resources.Own(rid);
                if(cb.UploadRevision!=cbRevision||cb.CompleteBytesSha256!=expected||!rd.TextureIsValid(view.TargetViewRid)||
                    !input.ActualArguments.StorageRid.IsValid)throw new InvalidDataException("DOF2 actual scatter owners changed before submission.");
                var actualTexture=Bank.RequireTexture(view.ActualPointer.ParameterIndex,view.ActualPointer.Reference.InstanceIndex);
                if(actualTexture.StorageRid!=view.StorageRid||actualTexture.NativeFormatViews[10]!=view.TargetViewRid||actualTexture.Shape.MipLevels!=1||
                    view.Extent!=new Vector2I((int)actualTexture.Shape.Width,(int)actualTexture.Shape.Height))
                    throw new InvalidDataException("DOF2 source scatter target metadata changed.");
                Rid framebuffer=Own(rd.FramebufferCreate(new Godot.Collections.Array<Rid>{view.TargetViewRid}));
                if(!rd.FramebufferIsValid(framebuffer))throw new InvalidDataException("DOF2 actual scatter framebuffer is invalid.");
                Rid Uniforms(MapPostEffect3CompiledStage stage)
                {
                    if(stage.Bindings.Bindings.Count==0)return default;
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var binding in stage.Bindings.Bindings)
                        {
                            var row=Bank.Source.Passes[pass].ActiveBindings.Last(r=>r.RegisterIndex==binding.Source.BindPoint&&
                                (r.StageMask&(stage.Source.StageCode==1?2:4))!=0&&r.Role==(binding.DescriptorKind=="UniformBuffer"?
                                    MapPostEffect3BindingRole.ConstantBufferRead:MapPostEffect3BindingRole.ShaderResourceRead));
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
                    // Enable the floor(target0*Viewport) scissor.
                    //The admitted source rectangle is exactly the full target.
                    rd.DrawListEnableScissor(list,new Rect2I(Vector2I.Zero,view.Extent));
                    rd.DrawListDrawIndirect(list,false,input.ActualArguments.StorageRid,checked((uint)before.NativeIndirectArgumentByteOffset),1,16);
                }
                finally{rd.DrawListEnd();}
                write=new(0,view.ActualPointer.ParameterIndex,view.ActualPointer.ParameterIndex,view.ActualPointer.Reference.InstanceIndex,
                    view.StorageRid,view.TargetViewRid,10,view.Extent,0,revision){PriorDestinationPixelsWereValidated=true};return true;
            });
            Require();if(cb.UploadRevision!=cbRevision)throw new InvalidDataException("DOF2 scatter CB18 changed during the source draw.");
            return completed=new(prepared.FrameNumber,Bank.Source.ContextOwnerIdentity,pass,vertex.Source.Sha256,pixel.Source.Sha256,
                Array.AsReadOnly(Bank.Source.Passes[pass].ActiveBindings.ToArray()),Array.AsReadOnly(new[]{write??throw new InvalidDataException("DOF2 scatter was not submitted.")}),
                Array.AsReadOnly(new[]{new MapPostEffect3ComputeConstantBufferReadReceipt(18,input.ActualCb18Instance,cb.StorageRid,cbRevision,true,expected)}),
                context.ActualViewport,new Rect2I(Vector2I.Zero,view.Extent),raster,before.SourceEvidence+
                (before.RequiresNativeClearMaskAndColorOverride?";actual-native-Bind-full-RT-clearMask15/color0;":
                    ";actual-native-Bind-no-clear;"+input.PriorDestination!.SourceEvidence+";")+
                "source0x50-staging32B-copy;source0x40032-indirect-B5-offset0/16/TriangleStrip;"+prefix.SourceEvidence){WasPixelDrawSubmitted=true};
        }
        catch(Exception failure){drawFailure=failure;throw;}
        finally
        {
            try
            {
                if(resources is not null)
                {
                    var nested=(drawFailure as NativeMapBorrowerConstructionException)?.RetainedPackage;
                    try
                    {
                        if(nested is not null)pendingDraw=NativeMapBorrowerRetirementPackage.CreatePending("DOF2-scatter-draw",[resources],nested);
                        if(pendingDraw is not null)await DrainPendingDrawCoreAsync();
                        else await resources.DisposeAsync();
                    }
                    catch(Exception cleanup)
                    {
                        if(pendingDraw is null)
                        {
                            pendingDraw=NativeMapBorrowerRetirementPackage.CreatePending("DOF2-scatter-draw",[resources]);
                            pendingDraw!.RetainFailure();
                        }
                        throw new NativeMapBorrowerConstructionException(drawFailure??cleanup,cleanup,pendingDraw!);
                    }
                }
                if(completed is not null){LastInputs=input;LastCompletedPass=completed;}
            }
            finally{serial.Release();}
        }
    }
    internal async Task DrainPendingAsync()
    {
        await serial.WaitAsync();
        try{await DrainPendingDrawCoreAsync();}
        finally{serial.Release();}
    }
    private async Task DrainPendingDrawCoreAsync()
    {pendingDraw=await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pendingDraw);}
    public async ValueTask DisposeAsync()
    {
        retiring=true;await serial.WaitAsync();
        try
        {
            if(disposed)return;
            await DrainPendingDrawCoreAsync();
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>
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
