using Godot;
using San14.Data;

/// <summary>Current owned node137 fields. The shader selection proves its own
/// accepted Camera/frame; mutable CommonView fields remain explicit producers.</summary>
public sealed record DeferredAuxiliaryGgxFrame(DeferredF103SelectedSource AcceptedSelection,
    DeferredTransparencyImageSelection Transparency,uint CurrentContextFlags3F0,
    uint CurrentFilterType90,int CurrentSampleCount94,NativeCameraTangentDispatch TangentDispatch,
    string CurrentCommonViewProvenance);

public sealed record DeferredAuxiliaryGaussianFrame(DeferredF103SelectedSource AcceptedSelection,
    DeferredTransparencyImageSelection Transparency,uint CurrentContextFlags3F0,
    uint CurrentGaussianSize94,float CurrentDispersion9C,string CurrentCommonViewProvenance,
    IDeferredAuxiliaryExponential? Exponential=null);

/// <summary>Runs actual original auxiliary filter programs into actual native
/// target mips. Requires the selected target's current mip0 writer to have
/// succeeded; allocation or a guessed default cannot replace that prerequisite.</summary>
public sealed class DeferredAuxiliaryPixelProducer : IAsyncDisposable
{
    public LoadedDeferredAuxiliaryPixelSourceRecipes Source { get; }
    public LoadedDeferredSamplerNativeSource NativeSamplers { get; }
    private readonly IReadOnlyDictionary<uint,DeferredAuxiliaryPixelProgram> ggxPrograms;
    private readonly IReadOnlyDictionary<(uint FileId,int Combination),DeferredAuxiliaryPixelProgram> gaussianPrograms;
    private readonly Dictionary<string,Rid> samplers=new(StringComparer.Ordinal);
    private readonly RenderDeviceResources samplerResources=new("Device rejected an auxiliary filter sampler.");
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly object retirementGate=new();
    private volatile bool retiring;
    private Task? disposal;
    private NativeMapBorrowerRetirementPackage? finalRetirement;
    private IAsyncDisposable? pendingDraw;
    private DeferredAuxiliaryTargetBorrow? pendingSourceView;

    private DeferredAuxiliaryPixelProducer(LoadedDeferredAuxiliaryPixelSourceRecipes source,
        LoadedDeferredSamplerNativeSource native,Dictionary<uint,DeferredAuxiliaryPixelProgram> programs,
        Dictionary<(uint,int),DeferredAuxiliaryPixelProgram> gaussian)
        =>(Source,NativeSamplers,ggxPrograms,gaussianPrograms)=(source,native,programs,gaussian);

    public static async Task<DeferredAuxiliaryPixelProducer> BuildAsync(
        LoadedDeferredAuxiliaryPixelSourceRecipes source,LoadedDeferredSamplerNativeSource native,
        Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(native);ArgumentNullException.ThrowIfNull(compiler);
        var programs=new Dictionary<uint,DeferredAuxiliaryPixelProgram>();
        var gaussian=new Dictionary<(uint,int),DeferredAuxiliaryPixelProgram>();
        try
        {
            foreach(uint type in new uint[]{2,3})
            {
                var pass=source.FilterShader.ResolveFilterPass(type,0,0);
                programs.Add(type,await DeferredAuxiliaryPixelProgram.BuildAsync(source.FilterShader.Shader,pass.Combination.Index,compiler));
            }
            foreach(uint size in Enumerable.Range(1,7).Select(value=>(uint)value))
            foreach(uint passIndex in new uint[]{0,1})
            {
                var pass=source.FilterShader.ResolveFilterPass(1,size,passIndex);
                gaussian.Add((source.FilterShader.Shader.Source.FileId,pass.Combination.Index),
                    await DeferredAuxiliaryPixelProgram.BuildAsync(source.FilterShader.Shader,pass.Combination.Index,compiler));
            }
            foreach(uint passIndex in new uint[]{0,1})
            {
                var pass=source.DepthFilterShader.ResolveFilterPass(1,3,passIndex);
                gaussian.Add((source.DepthFilterShader.Shader.Source.FileId,pass.Combination.Index),
                    await DeferredAuxiliaryPixelProgram.BuildAsync(source.DepthFilterShader.Shader,pass.Combination.Index,compiler));
            }
            return new(source,native,programs,gaussian);
        }
        catch(Exception failure)
        {
            var pending=NativeMapBorrowerRetirementPackage.CreatePending("Auxiliary-filters-partial",
                programs.Values.Concat(gaussian.Values),
                (failure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pending);}
            catch(Exception retirementFailure)
            {throw new NativeMapBorrowerConstructionException(failure,retirementFailure,pending!);}
            throw;
        }
    }

    public async Task ProduceGaussianSpecularMipsAsync(DeferredAuxiliaryGaussianFrame current,
        IReadOnlyList<DeferredAuxiliaryTargetDevice> targets)
    {
        ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(targets);
        await ProduceAsync(current.AcceptedSelection,current.CurrentCommonViewProvenance,targets,null,
            ()=>DeferredAuxiliaryFilterPreparation.Gaussian(Source,current));
    }

    /// <summary>Main depth must be the successful current main-depth view.
    /// Other native modes still require their actual source branch.</summary>
    public async Task ProduceBlurredDepthAsync(DeferredF103SelectedSource selection,uint currentContextFlags3F0,
        uint currentDepthFilterType,RenderTargetDeviceTexture currentMainDepth,
        IReadOnlyList<DeferredAuxiliaryTargetDevice> targets,string currentDepthProducerProvenance,
        IDeferredAuxiliaryExponential? exponential=null)
    {
        ArgumentNullException.ThrowIfNull(selection);ArgumentNullException.ThrowIfNull(currentMainDepth);
        ArgumentNullException.ThrowIfNull(targets);
        await ProduceAsync(selection,currentDepthProducerProvenance,targets,currentMainDepth,
            ()=>DeferredAuxiliaryFilterPreparation.Depth(Source,selection,currentContextFlags3F0,
                currentDepthFilterType,currentDepthProducerProvenance,exponential));
    }

    internal Task ProducePreparedAsync(DeferredAuxiliaryFilterPreparation preparation,
        IReadOnlyList<DeferredAuxiliaryTargetDevice> targets,RenderTargetDeviceTexture? mainDepth=null)
    {
        if(!ReferenceEquals(preparation.Source,Source))
            throw new InvalidDataException("Prepared filter belongs to another imported source.");
        if((preparation.Operation==DeferredAuxiliaryFilterOperation.GaussianDepth)!=(mainDepth is not null))
            throw new InvalidDataException("Only a prepared depth operation requires the current main-depth view.");
        return ProduceAsync(preparation.Selection,preparation.ProducerProvenance,targets,mainDepth,()=>preparation);
    }

    private async Task ProduceAsync(DeferredF103SelectedSource selection,string producer,
        IReadOnlyList<DeferredAuxiliaryTargetDevice> targets,RenderTargetDeviceTexture? mainDepth,
        Func<DeferredAuxiliaryFilterPreparation> prepare)
    {
        await gate.WaitAsync();
        try
        {
            await DrainPendingDrawCoreAsync();
            ObjectDisposedException.ThrowIf(retiring,this);
            ValidateFrame(selection,producer);
            if(mainDepth is not null)ValidateMainDepth(mainDepth);
            var preparation=prepare();
            if(preparation.Operation is DeferredAuxiliaryFilterOperation.GgxSpecular2 or DeferredAuxiliaryFilterOperation.GgxSpecular3)
                await RunGgxAsync(preparation,targets);
            else await RunGaussianAsync(selection,preparation.Writes,targets,mainDepth,producer);
        }
        finally{gate.Release();}
    }

    private void ValidateMainDepth(RenderTargetDeviceTexture currentMainDepth)
    {
        var nativeMainDepth=RenderTextureFormatMath.Resolve(Source.MainDepth);
        var mainDepthFormat=nativeMainDepth.TargetViewFormat switch
        {20=>RenderingDevice.DataFormat.D32SfloatS8Uint,40=>RenderingDevice.DataFormat.D32Sfloat,
            _=>throw new InvalidDataException("Source main depth has no exact supported depth-aspect adapter.")};
        if(currentMainDepth.Source.Object.ObjectId!=Source.MainDepth.Object.ObjectId||
            currentMainDepth.Source.Source.Source.ResourceUri!=Source.MainDepth.Source.Source.ResourceUri||
            currentMainDepth.Source.Source.Source.DecodedSha256!=Source.MainDepth.Source.Source.DecodedSha256||
            currentMainDepth.Source with{Source=Source.MainDepth.Source,Object=Source.MainDepth.Object}!=Source.MainDepth||
            currentMainDepth.ExplicitExtent!=new Vector2I(Source.BlurredDepth.NativeFrameExtent.Width,Source.BlurredDepth.NativeFrameExtent.Height)||
            currentMainDepth.StorageFormat!=mainDepthFormat||currentMainDepth.ShaderFormat!=mainDepthFormat||
            currentMainDepth.TargetFormat!=mainDepthFormat)
            throw new InvalidDataException("Original depth Gaussian requires its actual source main-depth owner.");
    }

    private void ValidateFrame(DeferredF103SelectedSource selection,string currentProducer)
    {

        if(selection.Camera.ExecutableLocale!=NativeSamplers.Locale||string.IsNullOrWhiteSpace(currentProducer)||
            selection.SourcePlan.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
            selection.SourcePlan.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256)
            throw new InvalidDataException("Original auxiliary filter requires its accepted source Camera/graph and explicit current producer.");
    }

    private async Task RunGaussianAsync(DeferredF103SelectedSource selection,
        IReadOnlyList<DeferredAuxiliaryFilterWrite> writes,IReadOnlyList<DeferredAuxiliaryTargetDevice> targets,
        RenderTargetDeviceTexture? mainDepth,string currentProducer)
    {
        long frame=selection.Current.FrameNumber;string provenance=selection.Current.CurrentProducerProvenance;
        foreach(var write in writes)
        {
            bool depth=write.TargetTextureId is 0xD50F0811 or 0x18C86C69;
            uint shaderFile=(depth?Source.DepthFilterShader:Source.FilterShader).Shader.Source.FileId;
            var program=gaussianPrograms[(shaderFile,write.Program.Combination.Index)];
            var matches=targets.Where(device=>device.Source.Object.ObjectId==write.TargetTextureId).Distinct().ToArray();
            if(matches.Length!=1)throw new InvalidDataException("Original Gaussian pass needs its actual target/scratch owner.");
            var target=matches[0];ValidateTargetOwner(target);
            // The first horizontal source write initializes the entire fixed
            // scratch. Later smaller viewports retain that same-frame border.
            if(write.PassOrdinal==0&&write.Viewport!=target.Construction.Extent)
                throw new InvalidDataException("Original Gaussian scratch lacks its first full source write.");
            DeferredAuxiliaryTargetBorrow? sourceView=null;
            try
            {
                RenderTextureDefinition sourceTexture;Rid input,owner;
                if(write.SourceTextureId==Source.MainDepth.Object.ObjectId)
                {
                    if(mainDepth is null||write.SourceMip!=0)throw new InvalidDataException("Actual main-depth Gaussian input is not supplied.");
                    sourceTexture=mainDepth.Source;input=mainDepth.ShaderViewRid;owner=mainDepth.StorageRid;
                }
                else
                {
                    var sources=targets.Where(device=>device.Source.Object.ObjectId==write.SourceTextureId).Distinct().ToArray();
                    if(sources.Length!=1)throw new InvalidDataException("Original Gaussian pass requires its completed previous actual source mip.");
                    ValidateTargetOwner(sources[0]);
                    sourceView=await sources[0].BorrowMipAsync(frame,provenance,write.SourceMip);
                    sourceTexture=sourceView.Source;input=sourceView.ShaderViewRid;owner=sourceView.StorageRid;
                }
                byte[] descriptor=RenderSamplerSourceAdapters.DecodeKey(NativeSamplers,
                    RenderSamplerSourceAdapters.FinalRenderTextureKey(NativeSamplers,sourceTexture)).EncodeD3D11Descriptor();
                Rid sampler=await GetSamplerAsync(descriptor);
                var inputs=new DeferredDeviceBinding[]{
                    new("Texture",0,input,owner,sourceTexture.Source.Source.ResourceUri+"#"+sourceTexture.Object.ObjectId.ToString("X8")+"/mip"+write.SourceMip),
                    new("Sampler",0,sampler,null,NativeSamplers.ResourceUri+"#actual-parent-key/"+Convert.ToHexString(descriptor))};
                var constants=new Dictionary<(uint,uint),ReadOnlyMemory<byte>>{{(0,0),write.PixelConstantBuffer0}};
                if(!write.VertexConstantBuffer0.IsEmpty)constants.Add((1,0),write.VertexConstantBuffer0);
                DeferredAuxiliaryPixelDraw draw;
                try
                {
                    draw=await DeferredAuxiliaryPixelDraw.BuildAsync(program,target,write.TargetMip,
                        constants,inputs,(int)write.VertexCount,RenderingDevice.RenderPrimitive.Triangles,
                        sourceMipViews:sourceView is null?null:new Dictionary<uint,DeferredAuxiliaryTargetBorrow>{{0,sourceView}},
                        sourceViewport:write.Viewport);
                }
                catch(NativeMapBorrowerConstructionException failure)
                {
                    pendingDraw=failure.RetainedPackage;
                    pendingSourceView=sourceView;sourceView=null;
                    pendingSourceView?.CompleteRead();
                    throw;
                }
                pendingDraw=draw;pendingSourceView=sourceView;sourceView=null;
                Exception? productionFailure=null;
                try
                {
                    var receipt=DeferredAuxiliaryProductionReceipt.FromSourceWriter(Source.Graph,Source.Node.Index,frame,provenance,
                        "OriginalNode137GaussianSourceShader;pass="+write.PassOrdinal+";sourceMip="+write.SourceMip+
                        ";program="+program.PixelTranslation.SourceProgramSha256+";numeric="+write.NumericProfile+
                        ";nativeRoundingVerified="+write.NativeRoundingVerified+";currentSource="+currentProducer,target.Source.Object.ObjectId,[write.TargetMip]);
                    await target.ProduceAsync(receipt,(rd,surfaces)=>
                    {
                        if(surfaces.Count!=1||surfaces[0].Source.MipLevel!=write.TargetMip)
                            throw new InvalidDataException("Original Gaussian callback selected another target mip.");
                        draw.Submit(rd);
                    });
                }
                catch(Exception failure){productionFailure=failure;throw;}
                finally{await FinishDrawAsync(productionFailure);}
            }
            finally{sourceView?.Dispose();}
        }
    }

    public async Task ProduceSpecularMipsAsync(DeferredAuxiliaryGgxFrame current,
        IReadOnlyList<DeferredAuxiliaryTargetDevice> targets)
    {
        ArgumentNullException.ThrowIfNull(current);ArgumentNullException.ThrowIfNull(targets);
        await ProduceAsync(current.AcceptedSelection,current.CurrentCommonViewProvenance,targets,null,
            ()=>DeferredAuxiliaryFilterPreparation.Ggx(Source,current));
    }

    private async Task RunGgxAsync(DeferredAuxiliaryFilterPreparation preparation,
        IReadOnlyList<DeferredAuxiliaryTargetDevice> targets)
    {
        var selection=preparation.Selection;
        var writes=preparation.Writes;
        long frame=selection.Current.FrameNumber;string provenance=selection.Current.CurrentProducerProvenance;
        var sourceTarget=Source.SelectSpecular(preparation.Transparency);
        var devices=targets.Where(t=>t.Source.Object.ObjectId==sourceTarget.Texture.Object.ObjectId&&
            t.Source.Source.Source.ResourceUri==sourceTarget.Texture.Source.Source.ResourceUri&&
            t.Source.Source.Source.DecodedSha256==sourceTarget.Texture.Source.Source.DecodedSha256).Distinct().ToArray();
        if(devices.Length!=1)throw new InvalidDataException("Selected original specular output needs one actual target owner.");
        var target=devices[0];ValidateTargetOwner(target);
        if(target.Source with{Source=sourceTarget.Texture.Source,Object=sourceTarget.Texture.Object}!=sourceTarget.Texture)
            throw new InvalidDataException("Specular target differs from its selected original source declaration.");
        var program=ggxPrograms[preparation.GgxFilterType];program.RequireAvailable();
        byte[] nativeDescriptor=RenderSamplerSourceAdapters.DecodeKey(NativeSamplers,
            RenderSamplerSourceAdapters.FinalRenderTextureKey(NativeSamplers,target.Source)).EncodeD3D11Descriptor();
        Rid sampler=await GetSamplerAsync(nativeDescriptor);
        foreach(var write in writes)
        {
            if(write.Program.Combination.Index!=program.Combination!.Index||write.SourceTextureId!=target.Source.Object.ObjectId||
                write.TargetTextureId!=target.Source.Object.ObjectId||write.SourceMip==write.TargetMip)
                throw new InvalidDataException("Original GGX source schedule differs from its compiled program or disjoint native mip dependency.");
            DeferredAuxiliaryTargetBorrow? previous=await target.BorrowMipAsync(frame,provenance,write.SourceMip);
            try
            {
                var inputs=new DeferredDeviceBinding[]{
                    new("Texture",0,previous.ShaderViewRid,previous.StorageRid,target.Source.Source.Source.ResourceUri+"#"+target.Source.Object.ObjectId.ToString("X8")+"/mip"+write.SourceMip),
                    new("Sampler",0,sampler,null,NativeSamplers.ResourceUri+"#actual-parent-7mip-key/"+Convert.ToHexString(nativeDescriptor))};
                var constants=new Dictionary<(uint,uint),ReadOnlyMemory<byte>>{{(0,0),write.PixelConstantBuffer0}};
                DeferredAuxiliaryPixelDraw draw;
                try
                {
                    draw=await DeferredAuxiliaryPixelDraw.BuildAsync(program,target,write.TargetMip,
                        constants,inputs,(int)write.VertexCount,RenderingDevice.RenderPrimitive.Triangles,
                        sourceMipViews:new Dictionary<uint,DeferredAuxiliaryTargetBorrow>{{0,previous}});
                }
                catch(NativeMapBorrowerConstructionException failure)
                {
                    pendingDraw=failure.RetainedPackage;
                    pendingSourceView=previous;previous=null;
                    pendingSourceView.CompleteRead();
                    throw;
                }
                pendingDraw=draw;pendingSourceView=previous;previous=null;
                Exception? productionFailure=null;
                try
                {
                    var receipt=DeferredAuxiliaryProductionReceipt.FromSourceWriter(Source.Graph,Source.Node.Index,frame,provenance,
                        "OriginalNode137GgxSourceShader;filter="+preparation.GgxFilterType+";sourceMip="+write.SourceMip+
                        ";program="+program.PixelTranslation.SourceProgramSha256+";currentCommon="+preparation.ProducerProvenance,
                        target.Source.Object.ObjectId,[write.TargetMip]);
                    await target.ProduceAsync(receipt,(rd,surfaces)=>
                    {
                        if(surfaces.Count!=1||surfaces[0].Source.MipLevel!=write.TargetMip)
                            throw new InvalidDataException("Source GGX target callback selected another actual mip.");
                        draw.Submit(rd);
                    });
                }
                catch(Exception failure){productionFailure=failure;throw;}
                finally{await FinishDrawAsync(productionFailure);}
            }
            finally{previous?.Dispose();}
        }
    }

    private Task<Rid> GetSamplerAsync(byte[] descriptor)
    {
        string key=Convert.ToHexString(descriptor);
        if(samplers.TryGetValue(key,out var existing))return Task.FromResult(existing);
        return TerrainDeviceTexture.OnRenderThreadAsync(rd=>
        {
            Rid created=samplerResources.Own(DeviceSampler.Create(rd,descriptor));samplers.Add(key,created);return created;
        });
    }

    private void ValidateTargetOwner(DeferredAuxiliaryTargetDevice target)
    {
        DeferredAuxiliaryTargetSources.Validate(target.Construction);
        var expected=Source.Targets.Concat(Source.Intermediates)
            .SingleOrDefault(recipe=>recipe.Texture.Object.ObjectId==target.Source.Object.ObjectId)
            ??throw new InvalidDataException("Auxiliary pixel owner is outside its exact original target recipes.");
        if(target.Source.Source.Source.ResourceUri!=expected.Texture.Source.Source.ResourceUri||
            target.Source.Source.Source.DecodedSha256!=expected.Texture.Source.Source.DecodedSha256||
            target.Source with{Source=expected.Texture.Source,Object=expected.Texture.Object}!=expected.Texture||
            target.Construction.NativeFrameExtent!=expected.NativeFrameExtent||target.Construction.Extent!=expected.Extent||
            target.Construction.MostDetailedMip!=expected.MostDetailedMip||target.Construction.ViewMipCount!=expected.ViewMipCount||
            !target.Construction.Surfaces.SequenceEqual(expected.Surfaces))
            throw new InvalidDataException("Auxiliary pixel target differs from its exact source declaration, frame extent or mip surfaces.");
    }
    internal async Task DrainPendingDrawAsync()
    {
        await gate.WaitAsync();
        try{await DrainPendingDrawCoreAsync();}
        finally{gate.Release();}
    }
    private async Task DrainPendingDrawCoreAsync()
    {
        if(pendingDraw is not null)await pendingDraw.DisposeAsync();
        pendingDraw=null;
        pendingSourceView?.Dispose();
        pendingSourceView=null;
    }
    private async Task FinishDrawAsync(Exception? productionFailure)
    {
        pendingSourceView?.CompleteRead();
        try{await DrainPendingDrawCoreAsync();}
        catch(Exception cleanup) when(productionFailure is not null)
        {throw new AggregateException("Auxiliary filter production and draw retirement failed.",productionFailure,cleanup);}
    }

    public ValueTask DisposeAsync()
    {
        lock(retirementGate)
        {
            retiring=true;
            return new(disposal is null||disposal.IsFaulted||disposal.IsCanceled?disposal=RetireAsync():disposal);
        }
    }
    private async Task RetireAsync()
    {
        await gate.WaitAsync();
        try
        {
            await DrainPendingDrawCoreAsync();
            finalRetirement ??= NativeMapBorrowerRetirementPackage.CreatePending("Auxiliary-filters-final",
                ggxPrograms.Values.Concat(gaussianPrograms.Values).Cast<IAsyncDisposable>().Prepend(samplerResources));
            finalRetirement = await NativeMapBorrowerRetirementPackage.DrainPendingAsync(finalRetirement);
        }
        finally{gate.Release();}
    }
}
