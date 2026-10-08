using Godot;
using San14.Data;

/// <summary>Complete current final draw context, separate from image identities
/// that the actual accepted producer chain supplies.</summary>
public sealed record OriginalMapFinalOutputSceneDrawPacket(
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> VertexConstants,
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> PixelConstants,ReadOnlyMemory<byte> Quad,
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> SamplerDescriptors,
    OriginalModelRasterInputs Raster,string NativeContextSource)
{
    public OriginalMapFinalOutputStockCopyPacket? OutputCopy { get; init; }
}

/// <summary>The coherent accepted native camera drives both actual producers.
/// ExtraFinalTextures supplies the actual native Hex/Bloom/Star/Flare/Shaft/LUT
/// producers by SPK texture index. No optional image or temporary alias is
/// inferred from zero effect weights. Dynamic pass bytes remain explicit.</summary>
public sealed record OriginalMapFinalOutputSceneFrameInputs(
    OriginalMapExposurePacket Exposure,OriginalMapFinalOutputDofWeightPacket DofWeight,
    IReadOnlyDictionary<int,OriginalMapFinalOutputPassDrawPacket> ProducerDraws,
    IReadOnlyDictionary<int,OriginalModelDeviceBinding> ExtraFinalTextures,
    OriginalMapFinalOutputSceneDrawPacket FinalDraw,string NativeContextSource);

/// <summary>Actual node73 Scene/depth → original DOF/Radial TEMP programs →
/// original PS28 → explicitly selected original Output attachment. This owns
/// every compiled draw and TEMP target, plus the native CPU weight producer.
/// Exposure may be borrowed from the shared actual node1 owner. All accepted
/// work drains before retirement; final descriptor sets retire before TEMP
/// images and the parent scene remains borrowed for the whole callback.</summary>
public sealed partial class OriginalMapFinalOutputSceneChain:IAsyncDisposable
{
    public LoadedOriginalMapFinalOutputResources Source { get; }
    public OriginalMapFinalOutputPassGraph Graph { get; }
    public OriginalMapFinalOutputPassOwner Producers { get; }
    public OriginalMapFinalOutputOwner Final { get; }
    public OriginalMapFinalOutputDofWeightOwner DofWeight { get; }
    public OriginalMapExposureOwner Exposure { get; }
    public OriginalMapFinalOutputFullEffectsOwner? CompleteEffects { get; private set; }
    public OriginalRenderOutputPresentation? Presentation=>Final.Presentation;
    public IReadOnlyList<int> RequiredExtraFinalTextureIndices { get; private set; }
    public IReadOnlyList<uint> RequiredFinalSamplerRegisters=>Array.AsReadOnly(Final.Program.PixelBindings.Bindings.Where(b=>b.DescriptorKind=="Sampler").Select(b=>b.Source.BindPoint).Order().ToArray());
    public long CompletedFrameCount { get; private set; }
    public OriginalMapFinalOutputPassDeviceFrame? LatestProducerFrame { get; private set; }
    private readonly bool _ownsExposure;
    private readonly SemaphoreSlim _gate=new(1,1);
    private readonly object _lifetime=new();private int _accepted;private bool _retiring;
    private Task? _disposal;private TaskCompletionSource? _drained;
    private OriginalMapFinalOutputSceneChain(LoadedOriginalMapFinalOutputResources source,
        OriginalMapFinalOutputPassGraph graph,OriginalMapFinalOutputPassOwner producers,
        OriginalMapFinalOutputOwner final,OriginalMapFinalOutputDofWeightOwner weight,
        OriginalMapExposureOwner exposure,bool ownsExposure)
    {
        (Source,Graph,Producers,Final,DofWeight,Exposure,_ownsExposure)=(source,graph,producers,final,weight,exposure,ownsExposure);
        var produced=producers.Plan.Steps.SelectMany(p=>p.Outputs).Select(o=>o.TextureIndex).Concat(new[]{0,14,29,30}).ToHashSet();
        var live=final.Program.PixelBindings.Bindings.Where(b=>b.DescriptorKind=="Texture").Select(b=>b.Source.BindPoint).ToHashSet();
        RequiredExtraFinalTextureIndices=Array.AsReadOnly(graph.Passes[28].Inputs.Where(i=>live.Contains(i.TextureRegister)).Select(i=>i.TextureIndex).Where(i=>!produced.Contains(i)).Distinct().Order().ToArray());
    }

    public static async Task<OriginalMapFinalOutputSceneChain> BuildAsync(
        LoadedOriginalMapFinalOutputResources source,Vkd3dShaderCompiler compiler,
        IReadOnlyDictionary<int,OriginalMapFinalOutputPassViewSelection> originalTempViews,
        OriginalMapFinalOutputTargetRoute targetRoute,bool runDistantDofPasses,
        OriginalMapExposureOwner? sharedExposureProducer=null,bool allowReadback=false,
        OriginalMapFinalOutputDeadColorCorrection? sourceDeadColorCorrection=null)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(compiler);
        var graph=OriginalMapFinalOutputPassGraph.Read(source);
        var plan=graph.CreateDofAndRadialProducerPlan(runDistantDofPasses);
        OriginalMapFinalOutputPassOwner? producers=null;OriginalMapFinalOutputOwner? final=null;
        OriginalMapFinalOutputDofWeightOwner? weight=null;OriginalMapExposureOwner? exposure=sharedExposureProducer;
        try
        {
            producers=await OriginalMapFinalOutputPassOwner.BuildAsync(plan,compiler,originalTempViews,allowReadback);
            final=await OriginalMapFinalOutputOwner.BuildAsync(source,OriginalMapFinalOutputResources.SpkProgram(source,7,28),3,4,compiler,targetRoute,sourceDeadColorCorrection);
            weight=await OriginalMapFinalOutputDofWeightOwner.BuildAsync(source);
            if(exposure is null)exposure=await OriginalMapExposureOwner.BuildAsync(source.StockScreen,compiler);
            if(exposure.Source.SourceResourceUri!=source.StockScreen.SourceResourceUri)
                throw new InvalidDataException("Final chain Exposure owner differs from its original node1 producer.");
            return new(source,graph,producers,final,weight,exposure,sharedExposureProducer is null);
        }
        catch
        {
            if(final is not null)await final.DisposeAsync();if(producers is not null)await producers.DisposeAsync();
            if(weight is not null)await weight.DisposeAsync();if(sharedExposureProducer is null&&exposure is not null)await exposure.DisposeAsync();throw;
        }
    }

    /// <summary>Instantiate the exact source Host false route using the proved
    /// TEMP/KEEP creator's ColorSpace0 and inherited OutputColorSpace2 views.
    /// The standalone original-declared Output remains the explicitly owned
    /// parent; this does not infer a live original proxy or effect pass gate.</summary>
    public static Task<OriginalMapFinalOutputSceneChain> BuildSourceHostAsync(
        LoadedOriginalMapFinalOutputResources source,Vkd3dShaderCompiler compiler,
        bool runDistantDofPasses,OriginalMapExposureOwner? sharedExposureProducer=null,bool allowReadback=false,
        OriginalMapFinalOutputDeadColorCorrection? sourceDeadColorCorrection=null)
    {
        var graph=OriginalMapFinalOutputPassGraph.Read(source);var plan=graph.CreateDofAndRadialProducerPlan(runDistantDofPasses);
        var views=plan.Steps.SelectMany(p=>p.Outputs).Select(o=>o.TextureIndex).Distinct().ToDictionary(i=>i,i=>
        {
            var formats=graph.ResolveTempFormats(i);
            return new OriginalMapFinalOutputPassViewSelection(i,formats.TargetViewFormat,formats.ShaderViewFormat,
                source.PostEffect.Source.ResourceUri+"#SPK/TEMP-creator;native=C01FF0,C0203D,C0206C;ColorSpace0/OutputColorSpace2");
        });
        return BuildAsync(source,compiler,views,OriginalMapFinalOutputTargetRoute.SourceHostParentOutput,
            runDistantDofPasses,sharedExposureProducer,allowReadback,sourceDeadColorCorrection);
    }

    public Task<OriginalRenderOutputPresentation> RenderAsync(OriginalNativeMapFrameContext frame,
        OriginalMapFinalOutputSceneFrameInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(inputs);
        if(CompleteEffects is not null)throw new InvalidOperationException("Use RenderCompleteSourceAsync for the complete source effects chain.");
        var snapshot=Snapshot(inputs);Accept();return RenderAcceptedAsync(frame,snapshot);
    }
    private async Task<OriginalRenderOutputPresentation> RenderAcceptedAsync(OriginalNativeMapFrameContext frame,
        OriginalMapFinalOutputSceneFrameInputs inputs)
    {
        await _gate.WaitAsync();
        try
        {
            if(frame.FrameNumber<=0||frame.FrameNumber!=inputs.Exposure.FrameNumber||frame.FrameNumber!=inputs.DofWeight.FrameNumber||
                frame.PreparedCamera is null||frame.PreparedCamera!=inputs.DofWeight.Camera||
                frame.OutputFrame.Source.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
                frame.OutputFrame.Source.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256||
                !frame.OutputFrame.Textures.TryGetValue(0x064B746B,out var scene)||
                !frame.OutputFrame.Textures.TryGetValue(0xE13DDB4F,out var depth))
                throw new InvalidDataException("Final chain requires the accepted source node73 Scene/depth and the same camera/frame for Exposure and DOF.");
            var exposure=await Exposure.PrepareAndBorrowAsync(frame,inputs.Exposure);
            await using var weight=await DofWeight.PrepareAndBorrowAsync(frame,inputs.DofWeight);
            var external=new Dictionary<int,OriginalModelDeviceBinding>
            {
                [0]=new("Texture",0,scene.ShaderViewRid,scene.StorageRid,Source.SceneTexture.Source.Source.ResourceUri+"#object/064B746B;accepted-node73"),
                [14]=exposure,[29]=new("Texture",0,depth.ShaderViewRid,depth.StorageRid,Source.TextureObjects.Source.ResourceUri+"#object/E13DDB4F;accepted-node73"),
                [30]=weight.Binding
            };
            var draws=inputs.ProducerDraws.ToDictionary(p=>p.Key,p=>p.Value);
            if(!draws.TryGetValue(0,out var merge)||merge.DrawSucceeded is not null)
                throw new InvalidDataException("Final chain requires DOFMerge pass0 with its source weight commit owned by this chain.");
            draws[0]=merge with{DrawSucceeded=weight.CommitDrawSuccess};
            var produced=await Producers.RenderAsync(new(frame.FrameNumber,scene.ExplicitExtent.X,scene.ExplicitExtent.Y,external,draws,inputs.NativeContextSource));
            var textures=produced.AvailableTextures.ToDictionary(p=>p.Key,p=>p.Value);
            foreach(var extra in inputs.ExtraFinalTextures)
                if(!textures.TryAdd(extra.Key,extra.Value))throw new InvalidDataException("Final chain external effect image replaces an actual accepted producer.");
            var live=Final.Program.PixelBindings.Bindings.Where(b=>b.DescriptorKind=="Texture").Select(b=>b.Source.BindPoint).ToHashSet();
            var required=Graph.Passes[28].Inputs.Where(i=>live.Contains(i.TextureRegister)).ToArray();
            if(required.Any(i=>!textures.ContainsKey(i.TextureIndex))||inputs.ExtraFinalTextures.Keys.Any(i=>!required.Any(r=>r.TextureIndex==i)))
                throw new InvalidDataException("Final chain requires every remaining original final image producer by its exact SPK index.");
            var images=required.Select(i=>textures[i.TextureIndex] with{SourceBindPoint=i.TextureRegister}).ToArray();
            var draw=inputs.FinalDraw;
            var packet=new OriginalMapFinalOutputFramePacket(frame.FrameNumber,draw.VertexConstants,draw.PixelConstants,draw.Quad,
                images.Single(i=>i.SourceBindPoint==3),images.Single(i=>i.SourceBindPoint==4),
                images.Where(i=>i.SourceBindPoint is not(3 or 4)).ToArray(),draw.SamplerDescriptors,draw.Raster,draw.NativeContextSource)
                {OutputCopy=draw.OutputCopy};
            var presentation=await Final.RenderAsync(frame,packet);
            LatestProducerFrame=produced;CompletedFrameCount++;return presentation;
        }
        finally{_gate.Release();Complete();}
    }

    private OriginalMapFinalOutputSceneFrameInputs Snapshot(OriginalMapFinalOutputSceneFrameInputs input)
    {
        if(string.IsNullOrWhiteSpace(input.NativeContextSource)||string.IsNullOrWhiteSpace(input.FinalDraw.NativeContextSource))
            throw new InvalidDataException("Final chain requires explicit accepted native context provenance.");
        if(input.ExtraFinalTextures.Count!=RequiredExtraFinalTextureIndices.Count||
            RequiredExtraFinalTextureIndices.Any(i=>!input.ExtraFinalTextures.TryGetValue(i,out var image)||
                image is not{DescriptorKind:"Texture",OwnerTexture:{IsValid:true}}||!image.Resource.IsValid||string.IsNullOrWhiteSpace(image.SourceResourceUri))||
            input.ProducerDraws.Count!=Producers.Plan.Steps.Count||Producers.Plan.Steps.Any(p=>!input.ProducerDraws.ContainsKey(p.Index)))
            throw new InvalidDataException("Final chain needs every exact remaining final producer and selected upstream draw before accepting work.");
        static IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> Bytes(IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> values)
            =>values.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
        static OriginalModelRasterInputs Raster(OriginalModelRasterInputs r)=>r with{Attachments=Array.AsReadOnly(r.Attachments.ToArray())};
        var draws=input.ProducerDraws.ToDictionary(p=>p.Key,p=>p.Value with{
            VertexConstants=Bytes(p.Value.VertexConstants),PixelConstants=Bytes(p.Value.PixelConstants),Quad=p.Value.Quad.ToArray(),
            SamplerDescriptors=Bytes(p.Value.SamplerDescriptors),Raster=Raster(p.Value.Raster)});
        var final=input.FinalDraw with{VertexConstants=Bytes(input.FinalDraw.VertexConstants),PixelConstants=Bytes(input.FinalDraw.PixelConstants),
            Quad=input.FinalDraw.Quad.ToArray(),SamplerDescriptors=Bytes(input.FinalDraw.SamplerDescriptors),Raster=Raster(input.FinalDraw.Raster),
            OutputCopy=input.FinalDraw.OutputCopy is null?null:OriginalMapFinalOutputTransfer.Snapshot(input.FinalDraw.OutputCopy)};
        if(Final.Program.DeadColorCorrection is not null)
        {
            if(!final.PixelConstants.TryGetValue(0,out var pixelCb))throw new InvalidDataException("Source-dead final chain requires its complete current CB.");
            Final.Program.DeadColorCorrection.ValidateFrame(pixelCb.Span);
        }
        return input with{Exposure=input.Exposure.CopyAcceptedSnapshot(),DofWeight=input.DofWeight.CopyAcceptedSnapshot(),
            ProducerDraws=draws,ExtraFinalTextures=input.ExtraFinalTextures.ToDictionary(p=>p.Key,p=>p.Value),FinalDraw=final};
    }
    public Task RetireTargetAsync(){Accept();return RetireAcceptedAsync();}
    private async Task RetireAcceptedAsync(){await _gate.WaitAsync();try{await RetireCoreAsync();}finally{_gate.Release();Complete();}}
    private async Task RetireCoreAsync(){await Final.RetireTargetAsync();await Producers.RetireTargetAsync();if(CompleteEffects is not null)await CompleteEffects.RetireTargetAsync();LatestProducerFrame=null;}
    private void Accept(){lock(_lifetime){ObjectDisposedException.ThrowIf(_retiring,this);_accepted++;}}
    private void Complete(){lock(_lifetime){if(--_accepted==0&&_retiring)_drained?.TrySetResult();}}
    public ValueTask DisposeAsync(){lock(_lifetime){_retiring=true;return new(_disposal??=DisposeCoreAsync());}}
    private async Task DisposeCoreAsync()
    {
        Task wait;lock(_lifetime){wait=_accepted==0?Task.CompletedTask:(_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;}
        await wait;await _gate.WaitAsync();
        try{await Final.DisposeAsync();await Producers.DisposeAsync();if(CompleteEffects is not null)await CompleteEffects.DisposeAsync();await DofWeight.DisposeAsync();if(_ownsExposure)await Exposure.DisposeAsync();LatestProducerFrame=null;}
        finally{_gate.Release();}
    }
}
