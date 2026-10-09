using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>The caller owns the real frontend and its native Bind/After state.
/// Component fixtures must identify themselves in their source evidence.
/// </summary>
public interface IMapPostEffect3LensFlare2InvocationLease
{
    void RequireCurrent();
    void AcceptNativeBefore(MapPostEffect3LensFlare2GpuPassSource source,
        IMapPostEffect3ComputeSamplerLease actualPixelLinear7,IMapPostEffect3ComputeSamplerLease actualPixelPoint8);
    Task<IMapPostEffect3FullscreenDrawContextLease> BindDiaphragmAsync(MapPostEffect3LensFlare2GpuPassSource source);
    void AcceptCompletedPass(MapPostEffect3LensFlare2GpuPassSource source,
        MapPostEffect3FullscreenCompletedPass? graphics,MapPostEffect3ComputeCompletedPass? compute);
    Task<IMapPostEffect3LensFlare2AfterStockLease> BindAfterMipAsync(
        MapPostEffect3LensFlare2MipPlan plan,MapPostEffect3LensFlare2MipStep step);
    void AcceptCompletedAfterMip(MapPostEffect3LensFlare2MipCompletedDraw completed);
}

/// <summary>Current original stock screen state after native register reset,
/// blend disable, color-only target Begin and source single-mip PS0 binding.
/// </summary>
public interface IMapPostEffect3LensFlare2AfterStockLease
{
    Vector2 CurrentScreenScale29E0{get;}
    Color CurrentColorScale29F0{get;}
    uint CurrentStockAlphaTestWord{get;}
    bool CurrentCustomStockVertexShaderIsAbsent{get;}
    IMapPostEffect3FullscreenDrawContextLease ActualDrawContext{get;}
    string SourceEvidence{get;}
    void RequireCurrentAfterMip(MapPostEffect3LensFlare2MipPlan plan,MapPostEffect3LensFlare2MipStep step);
}

public sealed class MapPostEffect3LensFlare2MipCompletedDraw
{
    internal MapPostEffect3LensFlare2MipCompletedDraw(MapPostEffect3LensFlare2MipPlan plan,
        MapPostEffect3LensFlare2MipStep step,int side,Rid storage,long revision,string evidence,Action validate)
        =>(Plan,Step,Side,StorageRid,WriterRevision,SourceEvidence,this.validate)=(plan,step,side,storage,revision,evidence,validate);
    private readonly Action validate;
    public MapPostEffect3LensFlare2MipPlan Plan{get;}
    public MapPostEffect3LensFlare2MipStep Step{get;}
    public int Side{get;}
    public Rid StorageRid{get;}
    public long WriterRevision{get;}
    public string SourceEvidence{get;}
    internal string HistoricalSubmissionJson{get;init;}=string.Empty;
    public void RequireCurrentSubmission()=>validate();
}

public sealed record MapPostEffect3LensFlare2CompletedInitialization(long FrameNumber,string ContextOwnerIdentity,
    IReadOnlyList<int> OrderedSelectedPasses,IReadOnlyList<MapPostEffect3FullscreenCompletedPass> Graphics,
    IReadOnlyList<MapPostEffect3ComputeCompletedPass> Compute,IReadOnlyList<MapPostEffect3LensFlare2MipCompletedDraw> AfterMips,
    string SourceEvidence);

/// <summary>Borrowed actual bank/programs; owned exact source samplers and
/// GPU executors. Executes selected original45..49 and every After45/49 mip.
/// Active50..52 require separate halo/ghost primitive owners and reject here.
/// Completing initialization never claims a complete Functor or World frame.
/// </summary>
public sealed class MapPostEffect3LensFlare2ExecutionOwner:IAsyncDisposable
{
    public PostProcessResourceBank Bank{get;}
    public MapPostEffect3ProgramLibrary Programs{get;}
    public MapPostEffect3ComputeExecutor Compute{get;}
    public MapPostEffect3FullscreenGraphicsExecutor Graphics{get;}
    public LoadedMapPostEffect3LensFlare2Source Source{get;}
    public MapPostEffect3LensFlare2CompletedInitialization? LastCompleted{get;private set;}
    public MapPostEffect3LensFlare2MipCompletedDraw? LastCompletedMip{get;private set;}
    private readonly LoadedDeferredSamplerNativeSource native;
    private readonly MapPostEffect3SourceSamplerOwner managers;
    private readonly DeferredAuxiliaryPixelProgram stock;
    private readonly LoadedMapPostEffect3PassRegisterResetSource reset;
    private readonly Dictionary<string,Rid> stockSamplers=[];
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();private bool retiring;private int accepted;
    private TaskCompletionSource? drained;private Task? disposal;
    private MapPostEffect3LensFlare2ExecutionOwner(PostProcessResourceBank bank,MapPostEffect3ProgramLibrary programs,
        LoadedMapPostEffect3LensFlare2Source source,LoadedDeferredSamplerNativeSource native,
        MapPostEffect3SourceSamplerOwner managers,DeferredAuxiliaryPixelProgram stock,
        LoadedMapPostEffect3PassRegisterResetSource reset)
    {
        Bank=bank;Programs=programs;Source=source;this.native=native;this.managers=managers;this.stock=stock;this.reset=reset;
        Compute=MapPostEffect3ComputeExecutor.Create(programs,bank);Graphics=MapPostEffect3FullscreenGraphicsExecutor.Create(programs,bank);
    }
    public static async Task<MapPostEffect3LensFlare2ExecutionOwner> BuildBorrowedAsync(GameResourceRepository repository,
        PostProcessResourceBank bank,MapPostEffect3ProgramLibrary programs,Vkd3dShaderCompiler borrowedCompiler,string locale="TC")
        =>await BuildCoreAsync(repository,bank,programs,borrowedCompiler,locale,null);
    internal static Task<MapPostEffect3LensFlare2ExecutionOwner> BuildForCurrentControlledFrameAsync(
        GameResourceRepository repository,MapPostEffect3WorldControlledFactoryFrame frame,
        MapPostEffect3ProgramLibrary programs,Vkd3dShaderCompiler compiler)
    {
        frame.RequireCurrent();
        return BuildCoreAsync(repository,frame.World.Bank,programs,compiler,frame.Eye.Locale,frame.ModuleLifetime!.StaticSources);
    }
    private static async Task<MapPostEffect3LensFlare2ExecutionOwner> BuildCoreAsync(GameResourceRepository repository,
        PostProcessResourceBank bank,MapPostEffect3ProgramLibrary programs,Vkd3dShaderCompiler borrowedCompiler,
        string locale,MapPostEffect3WorldControlledStaticSources? sources)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(bank);ArgumentNullException.ThrowIfNull(programs);
        if(programs.SourceSha256!=bank.Source.Source.Sha256)throw new InvalidDataException("LensFlare2 bank/program source differs.");
        sources?.RequireRepository(repository);
        var native=sources?.Native??RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale);
        var source=sources?.Lens??MapPostEffect3ParameterLensFlare2.Load(repository,locale);
        var reset=sources?.RegisterReset??MapPostEffect3PassRegisterResetSource.Load(repository,locale);
        if(native.Locale!=locale||source.Locale!=locale)throw new InvalidDataException("LensFlare2 needs the current module original executable locale.");
        var managers=await MapPostEffect3SourceSamplerOwner.BuildAsync(native,bank.Source.ContextOwnerIdentity);
        try
        {
            var stockSource=sources?.LensAfterStock??DeferredAuxiliaryStockCopySource.ReadCurrent(repository.SourceDirectory,locale);
            var stock=await DeferredAuxiliaryPixelProgram.BuildStockCopyAsync(stockSource,borrowedCompiler);
            return new(bank,programs,source,native,managers,stock,reset);
        }
        catch{await managers.DisposeAsync();throw;}
    }
    public Task<MapPostEffect3LensFlare2CompletedInitialization> ExecuteInitializationAsync(
        MapPostEffect3ParameterLensFlare2Update update,MapPostEffect3TextureParameterState table,
        int actualConstantBuffer17Side,IMapPostEffect3LensFlare2InvocationLease actualInvocation)
    {
        ArgumentNullException.ThrowIfNull(update);ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(actualInvocation);
        actualInvocation.RequireCurrent();update.RequireCurrent();
        if(!ReferenceEquals(table.Allocation,Bank.Source)||table.FrameNumber!=update.FrameNumber||table.ContextOwnerIdentity!=update.OwnerIdentity)
            throw new InvalidDataException("LensFlare2 initialization requires its actual owned module invocation.");
        if(update.PassSelection.OrderedFactory6Passes.Any(p=>p is <45 or >49))
            throw new NotSupportedException("Actual LensFlare2 halo/ghost selection requires its separate original primitive owners.");
        foreach(int pass in update.PassSelection.OrderedFactory6Passes)
            _=MapPostEffect3LensFlare2GpuPassSource.Prepare(Bank.Source,update,table,pass);
        if(!Bank.ConstantBuffers.TryGetValue(17,out var cbs)||(uint)actualConstantBuffer17Side>=cbs.Count)
            throw new InvalidDataException("LensFlare2 current CB17 side is absent.");
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}
        return ExecuteAcceptedAsync(update,table,actualConstantBuffer17Side,actualInvocation);
    }
    private async Task<MapPostEffect3LensFlare2CompletedInitialization> ExecuteAcceptedAsync(
        MapPostEffect3ParameterLensFlare2Update update,MapPostEffect3TextureParameterState table,int side,
        IMapPostEffect3LensFlare2InvocationLease invocation)
    {
        await serial.WaitAsync();var graphics=new List<MapPostEffect3FullscreenCompletedPass>();
        var compute=new List<MapPostEffect3ComputeCompletedPass>();var mips=new List<MapPostEffect3LensFlare2MipCompletedDraw>();
        try
        {
            LastCompleted=null;LastCompletedMip=null;await Bank.UploadCompleteConstantBufferAsync(update.ConstantBuffer,side);
            foreach(int pass in update.PassSelection.OrderedFactory6Passes)
            {
                RequireLive();invocation.RequireCurrent();var source=MapPostEffect3LensFlare2GpuPassSource.Prepare(Bank.Source,update,table,pass);
                invocation.AcceptNativeBefore(source,LeaseManager(source,7),LeaseManager(source,8));
                MapPostEffect3FullscreenCompletedPass? g=null;MapPostEffect3ComputeCompletedPass? c=null;
                if(pass==45)
                {
                    var context=await invocation.BindDiaphragmAsync(source);var target=await Bank.CaptureLensFlare2DiaphragmTargetAsync(table,source,context);
                    var compiled=await Programs.GetCompiledPassAsync(pass);
                    var samplers=compiled.Stages.SelectMany(s=>s.Bindings.Bindings.Where(b=>b.DescriptorKind=="Sampler")
                        .Select(b=>(s.Source.StageCode,b.Source.BindPoint))).Select(p=>
                            p.StageCode==0&&p.BindPoint is 7 or 8?LeaseManager(source,p.BindPoint):
                            throw new NotSupportedException("LensFlare2 diaphragm shader has an unsupported selected native sampler.")).ToArray();
                    var inputs=new MapPostEffect3FullscreenFrameInputs(MapPostEffect3FullscreenSourceDraw.CaptureLensFlare2(source),
                        table,[],samplers,new Dictionary<int,int>{{17,side}},[new(0,target)],context);
                    g=await Graphics.DrawAsync(pass,inputs);Bank.PublishLensFlare2DiaphragmCoverage(source,Graphics,g);graphics.Add(g);
                }
                else
                {
                    var reads=source.InputTextureParameters.Select(i=>
                    {
                        var pointer=table.Require(i);if(pointer.Reference.PhysicalAllocationParameterIndex is not{} physical)
                            throw new NotSupportedException("LensFlare2 compute initialization requires its actual internal source texture.");
                        var texture=Bank.RequireTexture(physical,pointer.Reference.InstanceIndex);
                        return (IMapPostEffect3ComputeTextureLease)Bank.CaptureSourceWrittenTextureAliasLease(table,i,update.FrameNumber,0,(uint)texture.Shape.MipLevels);
                    }).ToArray();
                    var input=new MapPostEffect3ComputeFrameInputs(update.FrameNumber,reads,[],new Dictionary<int,int>{{17,side}},
                        new Dictionary<int,int>{{source.OutputParameterIndex,table.Require(source.OutputParameterIndex).Reference.InstanceIndex}})
                    {TextureParameters=table,NativeRegisterResetSource=reset,LensFlare2Execution=source};
                    c=await Compute.DispatchAsync(pass,input);Bank.PublishLensFlare2ComputeCoverage(source,Compute,c);compute.Add(c);
                }
                invocation.AcceptCompletedPass(source,g,c);
                if(pass is 45 or 49)
                {
                    int physical=pass==45?50:55;var pointer=table.Require(physical);var texture=Bank.RequireTexture(physical,pointer.Reference.InstanceIndex);
                    byte[] key=RenderSamplerSourceAdapters.RenderTextureKey(native,texture.Shape.MipLevels);
                    ushort word48=BinaryPrimitives.ReadUInt16LittleEndian(key);
                    var mipPlan=Source.PrepareAfterMipPlan(update,table,pass,checked((ushort)(texture.Shape.MipLevels<<10)),word48,
                        "actual-owned-bank-wrapper-shape/native-render-target-key");
                    foreach(var step in mipPlan.Steps)
                    {
                        var stockState=await invocation.BindAfterMipAsync(mipPlan,step);
                        var completed=await DrawAfterMipAsync(mipPlan,step,stockState);
                        LastCompletedMip=completed;Bank.PublishLensFlare2AfterMipCoverage(this,completed);
                        mips.Add(completed);invocation.AcceptCompletedAfterMip(completed);
                    }
                }
            }
            return LastCompleted=new(update.FrameNumber,update.OwnerIdentity,Array.AsReadOnly(update.PassSelection.OrderedFactory6Passes.ToArray()),
                graphics.AsReadOnly(),compute.AsReadOnly(),mips.AsReadOnly(),update.SourceEvidence+";source-selected-GPU-initialization-and-sequential-stock-After-mips;not-complete-Functor");
        }
        finally
        {
            serial.Release();lock(lifetime){accepted--;if(retiring&&accepted==0)drained?.TrySetResult();}
        }
    }
    private IMapPostEffect3ComputeSamplerLease LeaseManager(MapPostEffect3LensFlare2GpuPassSource source,uint register)
        =>managers.LeaseSelected(new ManagerSelection(this,source,register),2);
    private sealed record ManagerSelection(MapPostEffect3LensFlare2ExecutionOwner Owner,
        MapPostEffect3LensFlare2GpuPassSource Source,uint SourceRegister):IMapPostEffect3ManagerSamplerSelection
    {
        public long FrameNumber=>Source.FrameNumber;public string ContextOwnerIdentity=>Source.ContextOwnerIdentity;
        public int NativeStageOrdinal=>2;public MapPostEffect3ManagerSamplerKind Kind=>SourceRegister==7?
            MapPostEffect3ManagerSamplerKind.Linear:MapPostEffect3ManagerSamplerKind.Point;
        public string SourceEvidence=>Source.SourceEvidence+";PS7-linear/PS8-point-Before";
        public void RequireCurrent(){Owner.RequireLive();Source.RequireCurrent();if(SourceRegister is not(7 or 8))throw new InvalidDataException("LensFlare2 Before only setsPS7/8.");}
    }
    private void RequireLive(){lock(lifetime)ObjectDisposedException.ThrowIf(retiring&&accepted==0,this);}
    private async Task<MapPostEffect3LensFlare2MipCompletedDraw> DrawAfterMipAsync(
        MapPostEffect3LensFlare2MipPlan plan,MapPostEffect3LensFlare2MipStep step,IMapPostEffect3LensFlare2AfterStockLease state)
    {
        using var scope=await Bank.AcquireBufferExecutionScopeAsync();plan.ValidateCurrent();state.RequireCurrentAfterMip(plan,step);stock.RequireAvailable();
        var context=state.ActualDrawContext;context.RequireCurrentForPass(plan.SourcePassIndex);
        var raster=context.ActualRaster;var targetExtent=new Vector2I((int)step.DestinationWidth,(int)step.DestinationHeight);
        if(context.FrameNumber!=plan.FrameNumber||context.ContextOwnerIdentity!=plan.ContextOwnerIdentity||
            context.ActualViewport!=new Rect2I(Vector2I.Zero,targetExtent)||context.ActualViewportMinimumDepth!=0||context.ActualViewportMaximumDepth!=1||
            context.ActualScissor is{} sc&&(sc.Position.X>0||sc.Position.Y>0||sc.End.X<targetExtent.X||sc.End.Y<targetExtent.Y)||
            raster.DepthTest||raster.DepthWrite||raster.NativeStencil is not null||raster.Cull!=RenderingDevice.PolygonCullMode.Disabled||
            raster.Attachments is not [var color]||color.EnableBlend||!color.WriteR||!color.WriteG||!color.WriteB||!color.WriteA||
            !state.CurrentCustomStockVertexShaderIsAbsent||state.CurrentScreenScale29E0!=Vector2.One||state.CurrentColorScale29F0!=Colors.White||
            state.CurrentStockAlphaTestWord!=0xFF7FFFFFu)
            throw new NotSupportedException("LensFlare2 After requires actual complete stock-copy raster/scale/color/alpha-test state.");
        var acceptedRaster=MapFinalOutputRasterSnapshot.Capture(raster);
        var acceptedStock=(state.CurrentScreenScale29E0,state.CurrentColorScale29F0,state.CurrentStockAlphaTestWord,
            state.CurrentCustomStockVertexShaderIsAbsent,state.SourceEvidence,context.OwnedFrontendIdentity,context.ViewIdentity,
            context.ActualViewport,context.ActualViewportMinimumDepth,context.ActualViewportMaximumDepth,context.ActualScissor,context.SourceEvidence);
        void RequireAcceptedState()
        {
            RequireLive();plan.ValidateCurrent();state.RequireCurrentAfterMip(plan,step);context.RequireCurrentForPass(plan.SourcePassIndex);
            var current=context.ActualRaster;
            if(acceptedStock!=(state.CurrentScreenScale29E0,state.CurrentColorScale29F0,state.CurrentStockAlphaTestWord,
                state.CurrentCustomStockVertexShaderIsAbsent,state.SourceEvidence,context.OwnedFrontendIdentity,context.ViewIdentity,
                context.ActualViewport,context.ActualViewportMinimumDepth,context.ActualViewportMaximumDepth,context.ActualScissor,context.SourceEvidence)||
                current.Cull!=acceptedRaster.Cull||current.FrontFace!=acceptedRaster.FrontFace||current.DepthTest!=acceptedRaster.DepthTest||
                current.DepthWrite!=acceptedRaster.DepthWrite||current.DepthCompare!=acceptedRaster.DepthCompare||
                current.NativeStencil!=acceptedRaster.NativeStencil||!current.Attachments.SequenceEqual(acceptedRaster.Attachments))
                throw new InvalidDataException("LensFlare2 After native frontend/stock state changed while the original draw was queued.");
        }
        int physical=plan.ActualTexture.ParameterIndex,side=plan.ActualTexture.Reference.InstanceIndex;
        var texture=Bank.RequireTexture(physical,side);
        var prior=Bank.CaptureSourceWrittenTextureLease(physical,side,plan.FrameNumber,(uint)step.SourceMipLevel,1);
        var read=await Bank.RequireLensFlare2ViewAsync(texture,(uint)step.SourceMipLevel,texture.Shape.Formats.ShaderResourceFormat0);
        var write=await Bank.RequireLensFlare2ViewAsync(texture,(uint)step.DestinationMipLevel,texture.Shape.Formats.RenderTargetFormat0);
        byte[] key=RenderSamplerSourceAdapters.RenderTextureKey(native,texture.Shape.MipLevels);
        BinaryPrimitives.WriteUInt16LittleEndian(key,(ushort)((BinaryPrimitives.ReadUInt16LittleEndian(key)&0x3FF)|0x9400));
        string keyHex=Convert.ToHexString(key);
        if(!stockSamplers.TryGetValue(keyHex,out var sampler))
        {sampler=await TerrainDeviceTexture.OnRenderThreadAsync(rd=>DeviceSampler.Create(rd,RenderSamplerSourceAdapters.DecodeKey(native,key)));stockSamplers.Add(keyHex,sampler);}
        var quad=DeferredAuxiliaryStockQuad.FromCurrentViewport(targetExtent,state.CurrentScreenScale29E0,state.CurrentColorScale29F0,state.SourceEvidence);
        long revision=Bank.InvalidateTextureAfterShaderWrite(physical,side,(uint)step.DestinationMipLevel,plan.FrameNumber,plan.SourcePassIndex);
        await DrawStockAsync(read,write,sampler,quad,state,()=>{RequireAcceptedState();prior.RequireReadableMipRange((uint)step.SourceMipLevel,1);});
        return new(plan,step,side,texture.StorageRid,revision,plan.SourceEvidence+";"+state.SourceEvidence+
            ";actual-original-stock-VS/default3PS-draw6indices;sampler="+keyHex+";native-register-reset;not-hardware-generateMips",
            RequireAcceptedState){HistoricalSubmissionJson=System.Text.Json.JsonSerializer.Serialize(new{
                plan.FrameNumber,plan.ContextOwnerIdentity,plan.SourcePassIndex,step.SourceMipLevel,step.DestinationMipLevel,
                side,storage=texture.StorageRid.Id,revision,shader=stock.ShaderRid.Id,
                vertexSource=stock.VertexTranslation.SourceProgramSha256,vertexSpirv=stock.VertexAdapter.AdaptedSha256,
                pixelSource=stock.PixelTranslation.SourceProgramSha256,pixelSpirv=stock.PixelTranslation.SpirvSha256,
                readView=read.ViewRid.Id,read.FirstMipLevel,read.MipLevelCount,read.NativeViewFormat,read.Extent,
                targetView=write.ViewRid.Id,targetMip=write.FirstMipLevel,targetFormat=write.NativeViewFormat,targetExtent=write.Extent,
                sampler=sampler.Id,keyHex,
                vertexConstants=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(quad.VertexConstantBytes.Span)),
                alphaConstants=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(BitConverter.GetBytes(state.CurrentStockAlphaTestWord).Concat(new byte[12]).ToArray())),
                vertices=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(quad.VertexBytes.Span)),
                indices=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(quad.IndexBytes.Span)),indexCount=6,
                context.OwnedFrontendIdentity,context.ViewIdentity,context.ActualViewport,context.ActualScissor,
                context.ActualViewportMinimumDepth,context.ActualViewportMaximumDepth,raster=acceptedRaster,state.SourceEvidence})};
    }
    private Task DrawStockAsync(MapPostEffect3ComputeMipView read,MapPostEffect3ComputeMipView write,Rid sampler,
        DeferredAuxiliaryStockQuad quad,IMapPostEffect3LensFlare2AfterStockLease state,Action validate)
        =>TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            validate();var owned=new List<Rid>();Rid Own(Rid rid){if(!rid.IsValid)throw new InvalidDataException("LensFlare2 stock mip allocation failed.");owned.Add(rid);return rid;}
            try
            {
                using var vertex=new RDVertexAttribute{Location=0,Offset=0,Stride=24,Format=RenderingDevice.DataFormat.R32G32B32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex};
                using var color=new RDVertexAttribute{Location=1,Offset=12,Stride=24,Format=RenderingDevice.DataFormat.R8G8B8A8Unorm,Frequency=RenderingDevice.VertexFrequency.Vertex};
                using var uv=new RDVertexAttribute{Location=2,Offset=16,Stride=24,Format=RenderingDevice.DataFormat.R32G32Sfloat,Frequency=RenderingDevice.VertexFrequency.Vertex};
                long format=rd.VertexFormatCreate([vertex,color,uv]);Rid vb=Own(rd.VertexBufferCreate(96,quad.VertexBytes.ToArray()));
                Rid va=Own(rd.VertexArrayCreate(4,format,[vb,vb,vb]));Rid ib=Own(rd.IndexBufferCreate(6,RenderingDevice.IndexBufferFormat.Uint16,quad.IndexBytes.ToArray()));
                Rid ia=Own(rd.IndexArrayCreate(ib,0,6));Rid framebuffer=Own(rd.FramebufferCreate([write.ViewRid]));
                var buffers=new Dictionary<(uint,uint),Rid>{{(1,0),Own(rd.UniformBufferCreate(32,quad.VertexConstantBytes.ToArray()))},
                    {(0,0),Own(rd.UniformBufferCreate(16,BitConverter.GetBytes(state.CurrentStockAlphaTestWord).Concat(new byte[12]).ToArray()))}};
                var sets=new Dictionary<uint,Rid>();
                foreach(var stage in new[]{(Stage:0u,Bindings:stock.PixelBindings),(Stage:1u,Bindings:stock.VertexBindings)})
                {
                    var uniforms=new Godot.Collections.Array<RDUniform>();
                    try
                    {
                        foreach(var declaration in stage.Bindings.Bindings)
                        {
                            var type=declaration.DescriptorKind switch{"UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,
                                "Texture"=>RenderingDevice.UniformType.Texture,"Sampler"=>RenderingDevice.UniformType.Sampler,
                                _=>throw new NotSupportedException("LensFlare2 stock source descriptor is unsupported.")};
                            var uniform=new RDUniform{Binding=(int)declaration.Device.Binding!.Value,UniformType=type};
                            uniform.AddId(declaration.DescriptorKind=="UniformBuffer"?buffers[(stage.Stage,declaration.Source.BindPoint)]:
                                declaration.DescriptorKind=="Texture"?read.ViewRid:sampler);uniforms.Add(uniform);
                        }
                        sets.Add(stage.Stage,Own(rd.UniformSetCreate(uniforms,stock.ShaderRid,stage.Stage)));
                    }
                    finally{foreach(var uniform in uniforms)uniform.Dispose();}
                }
                using var raster=new RDPipelineRasterizationState{CullMode=RenderingDevice.PolygonCullMode.Disabled};
                using var multi=new RDPipelineMultisampleState();using var depth=new RDPipelineDepthStencilState();
                using var attachment=new RDPipelineColorBlendStateAttachment{EnableBlend=false};
                using var blend=new RDPipelineColorBlendState{Attachments=[attachment]};
                Rid pipeline=Own(rd.RenderPipelineCreate(stock.ShaderRid,rd.FramebufferGetFormat(framebuffer),format,
                    RenderingDevice.RenderPrimitive.Triangles,raster,multi,depth,blend));
                if(!rd.RenderPipelineIsValid(pipeline)||sets.Values.Any(s=>!rd.UniformSetIsValid(s)))
                    throw new InvalidDataException("Device rejected original LensFlare2 stock mip programs/bindings.");
                validate();long draw=rd.DrawListBegin(framebuffer,region:new Rect2(0,0,write.Extent.X,write.Extent.Y));
                try
                {
                    rd.DrawListBindRenderPipeline(draw,pipeline);foreach(var set in sets)rd.DrawListBindUniformSet(draw,set.Value,set.Key);
                    rd.DrawListBindVertexArray(draw,va);rd.DrawListBindIndexArray(draw,ia);rd.DrawListDraw(draw,true,1,0);
                }
                finally{rd.DrawListEnd();}
                validate();return true;
            }
            finally{for(int i=owned.Count-1;i>=0;i--)rd.FreeRid(owned[i]);}
        });
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
        try
        {
            await Graphics.DisposeAsync();await Compute.DisposeAsync();await managers.DisposeAsync();await stock.DisposeAsync();
            await TerrainDeviceTexture.OnRenderThreadAsync(rd=>{foreach(var sampler in stockSamplers.Values)rd.FreeRid(sampler);stockSamplers.Clear();return true;});
            LastCompleted=null;LastCompletedMip=null;
        }
        finally{serial.Release();}
    }
}
