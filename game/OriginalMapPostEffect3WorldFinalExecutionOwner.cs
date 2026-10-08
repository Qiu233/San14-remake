using San14.Data;
using San14.Data.Formats;

public sealed record OriginalMapPostEffect3WorldFinalCompleted(long FrameNumber,string ContextOwnerIdentity,
    IReadOnlyList<OriginalMapPostEffect3MiddlePassResult> Middle,
    OriginalMapPostEffect3LensCompletedExecution Lens,
    OriginalMapPostEffect3StockGraphicsCompletedPass Composite,
    OriginalRenderOutputPresentation Presentation,string SourceEvidence)
{
    public bool CompletesSourceLensAndComposite=>true;
    public bool CommitsHostHistory=>false;
    public OriginalMapPostEffect3RetainedCpuExit? RetainedCpuExit{get;init;}
}

/// <summary>Actual same-World continuation to the original Composite44 output.
/// Borrows the first World bank/scene and owns only its Lens/graphics borrowers.
/// Real source-disabled passes remain recorded; failed attempts cannot publish
/// an output. No old A1 result, fixture image or caller effect flag is used.</summary>
public sealed class OriginalMapPostEffect3WorldFinalExecutionOwner:IAsyncDisposable
{
    private readonly OriginalMapPostEffect3FirstWorldExecutionOwner first;
    private readonly OriginalMapPostEffect3StockGraphicsExecutor graphics;
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();
    private bool accepted,retiring;private Task? disposal;
    private OriginalMapPostEffect3ShaftExecutionOwner? ownedShaft;
    private OriginalMapPostEffect3ShaftExecutionOwner? actualFinishedShaft;
    public OriginalMapPostEffect3LensExecutionOwner Lens{get;}
    public OriginalMapPostEffect3MiddleExecutor? ActualMiddle{get;private set;}
    public OriginalMapPostEffect3WorldFinalCompleted? LastCompleted{get;private set;}
    private OriginalMapPostEffect3WorldFinalExecutionOwner(OriginalMapPostEffect3FirstWorldExecutionOwner firstWorld,
        OriginalMapPostEffect3LensExecutionOwner lens)
        =>(first,Lens,graphics)=(firstWorld,lens,OriginalMapPostEffect3StockGraphicsExecutor.Create(firstWorld.Programs,firstWorld.Bank));

    public static async Task<OriginalMapPostEffect3WorldFinalExecutionOwner> BuildBorrowedAsync(
        OriginalGameRepository actualRepository,OriginalMapPostEffect3FirstWorldExecutionOwner actualFirstWorld)
    {
        ArgumentNullException.ThrowIfNull(actualRepository);ArgumentNullException.ThrowIfNull(actualFirstWorld);
        var manager=OriginalDeferredSamplerSources.Load(actualRepository.SourceDirectory,actualFirstWorld.ExecutableLocale);
        var reset=OriginalMapPostEffect3PassRegisterResetSource.Load(actualRepository,actualFirstWorld.ExecutableLocale);
        var lens=await OriginalMapPostEffect3LensExecutionOwner.CreateBorrowedAsync(
            actualFirstWorld.Programs,actualFirstWorld.Bank,manager,reset);
        try{return new(actualFirstWorld,lens);}catch{await lens.DisposeAsync();throw;}
    }

    /// <summary>One call after the real first source Shaft submission reaches
    /// Shaft7, all Middle passes, Lens and the source final consumer44.</summary>
    public Task<OriginalMapPostEffect3WorldFinalCompleted> CompleteAfterActualFirstShaftAsync()
        =>AcceptAsync(CompleteAfterActualFirstShaftCoreAsync);

    private async Task<OriginalMapPostEffect3WorldFinalCompleted> CompleteAfterActualFirstShaftCoreAsync()
    {
        var prefix=first.CurrentPrefix??throw new InvalidDataException("Actual first World prefix is unavailable.");
        lock(lifetime)
        {ObjectDisposedException.ThrowIf(retiring,this);}
        if(ownedShaft is not null){await ownedShaft.DisposeAsync();ownedShaft=null;}
        ownedShaft=OriginalMapPostEffect3ShaftExecutionOwner.Create(first);
        await ownedShaft.ContinueAfterActualFirstShaftAsync(prefix);
        if(ownedShaft.LastCompletedFamily is null)await ownedShaft.CompleteActualStockPass7FromOwnedSourceAsync();
        return await CompleteAfterFinishedShaftCoreAsync(ownedShaft);
    }

    public Task<OriginalMapPostEffect3WorldFinalCompleted> CompleteAfterFinishedShaftAsync(
        OriginalMapPostEffect3ShaftExecutionOwner actualShaft)
        =>AcceptAsync(()=>CompleteAfterFinishedShaftCoreAsync(actualShaft));

    private async Task<OriginalMapPostEffect3WorldFinalCompleted> CompleteAfterFinishedShaftCoreAsync(
        OriginalMapPostEffect3ShaftExecutionOwner actualShaft)
    {
        ArgumentNullException.ThrowIfNull(actualShaft);
        actualFinishedShaft=actualShaft;
        var prefix=first.CurrentPrefix??throw new InvalidDataException("Actual first World prefix is unavailable.");
        var middle=OriginalMapPostEffect3MiddleExecutor.Create(first.Compute,prefix.Parameters,prefix.Textures,first.CurrentRetainedInvocation??throw new InvalidDataException("Actual module invocation is absent."));
        var nativeSamplers=actualShaft.CreateMiddleSamplerContinuation(middle);
        var inputs=OriginalMapPostEffect3WorldMiddleInputBuilder.Create(middle,prefix,actualShaft,nativeSamplers);
        ActualMiddle=middle;
        while(middle.NextSourcePassIndex<=19)await inputs.ExecuteNextAsync();
        return await CompleteFromFinishedMiddleCoreAsync(middle);
    }

    public Task<OriginalMapPostEffect3WorldFinalCompleted> CompleteFromFinishedMiddleAsync(
        OriginalMapPostEffect3MiddleExecutor actualMiddle)
        =>AcceptAsync(()=>CompleteFromFinishedMiddleCoreAsync(actualMiddle));

    public Task<OriginalMapPostEffect3WorldFinalCompleted> CompleteFromFinishedMiddleAsync(
        OriginalMapPostEffect3MiddleExecutor actualMiddle,OriginalMapPostEffect3ShaftExecutionOwner actualShaft)
        =>AcceptAsync(()=>
        {
            actualFinishedShaft=actualShaft??throw new ArgumentNullException(nameof(actualShaft));
            return CompleteFromFinishedMiddleCoreAsync(actualMiddle);
        });

    private async Task<OriginalMapPostEffect3WorldFinalCompleted> AcceptAsync(Func<Task<OriginalMapPostEffect3WorldFinalCompleted>> run)
    {
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);if(accepted)throw new InvalidOperationException("World final continuation is already running.");accepted=true;}
        await serial.WaitAsync();
        try{return await run();}
        catch
        {
            if(first.CurrentRetainedInvocation is {IsExited:false} invocation)
                try{invocation.MarkFailedOrUnresolvedWriter();}catch(InvalidOperationException){ }
            throw;
        }
        finally{lock(lifetime)accepted=false;serial.Release();}
    }

    private async Task<OriginalMapPostEffect3WorldFinalCompleted> CompleteFromFinishedMiddleCoreAsync(
        OriginalMapPostEffect3MiddleExecutor actualMiddle)
    {
        ArgumentNullException.ThrowIfNull(actualMiddle);
            var shaft=actualFinishedShaft??throw new InvalidDataException("Actual full Functor0 requires its retained same-World Shaft owner, not only a middle receipt.");
            LastCompleted=null;ActualMiddle=actualMiddle;
            var prefix=first.CurrentPrefix??throw new InvalidDataException("Actual first World prefix is unavailable.");
            prefix.RequireCurrent();actualMiddle.RequireFinishedMiddlePasses();
            var invocation=first.CurrentRetainedInvocation??throw new InvalidDataException("Actual module invocation is absent.");
            invocation.RequireModule(first.Bank.Source);invocation.RequireInitializedFrame(prefix.Parameters);
            var init=prefix.Parameters.RequirePreparedFactories();
            var sourceInputs=new OriginalMapPostEffect3WorldLensSourceInputs(first,prefix,actualMiddle,Lens);
            var bufferSides=Enumerable.Range(0,4).ToDictionary(i=>i,i=>invocation.BufferInstances[i]);
            var lensFrame=new OriginalMapPostEffect3LensExecutionFrame(prefix.Parameters.State,prefix.Parameters.Lens,init.Lens,
                prefix.Textures,invocation.ConstantBufferInstances,bufferSides,sourceInputs){ActualFinishedMiddle=actualMiddle};
            var completedLens=await Lens.ExecuteAsync(lensFrame);
            if(!ReferenceEquals(Lens.LastCompleted,completedLens))throw new InvalidDataException("Lens continuation did not finish this actual source frame.");
            sourceInputs.RequireCurrent();
            var composite=init.Composite;
            if(composite.ConstantBuffer.Declaration.Index!=16||composite.ConstantBuffer.Bytes.Length!=160)
                throw new InvalidDataException("Actual Composite source constants are not the original160B CB16.");
            int cbSide=invocation.ConstantBufferInstances[16];
            await first.Bank.UploadCompleteConstantBufferAsync(composite.ConstantBuffer,cbSide);
            var compiled=await first.Programs.GetCompiledPassAsync(44);
            var pixel=compiled.Stages.Single(s=>s.Source.StageCode==0);
            var neededSamplers=pixel.Bindings.Bindings.Where(b=>b.DescriptorKind=="Sampler").Select(b=>b.Source.BindPoint).ToHashSet();
            // Original Composite Before installs manager7/8. No wrapper
            // sampler is invented for an undeclared or absent source register.
            if(neededSamplers.Any(s=>s is not(7 or 8)))
                throw new NotSupportedException("Actual Composite declares another native sampled-wrapper sampler producer.");
            var textureIndices=first.Bank.Source.Passes[44].ActiveBindings.Where(b=>
                b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&
                (b.StageMask&4)!=0).Select(b=>b.ParameterIndex).Distinct()
                .Where(i=>prefix.Textures.ReadKnownNullablePointer(i) is not null).ToArray();
            var textures=textureIndices.Select(i=>i==5?
                (IOriginalMapPostEffect3ComputeTextureLease)first.Bank.CaptureToneInfoSourceReadLease(
                    OriginalMapPostEffect3ToneInfoReadFootprint.PrepareComposite(first.Bank.Source,composite),prefix.Textures):
                ReadCompositeTexture(sourceInputs,prefix,composite,i)).ToArray();
            var draw=first.NativeSamplers.CaptureOwnedDrawContext(prefix);
            var input=draw.PrepareComposite44Inputs(Array.AsReadOnly(textures),
                Array.Empty<IOriginalMapPostEffect3ComputeSamplerLease>(),Lens.Samplers,new Dictionary<int,int>{{16,cbSide}});
            input=input with{CurrentSamplers=Array.AsReadOnly(input.CurrentSamplers.Where(s=>neededSamplers.Contains(s.SourceRegister)).ToArray())};
            foreach(var sampler in input.CurrentSamplers)
            {
                if(sampler is not IOriginalMapPostEffect3SourceMipSamplerLease actualNative)
                    throw new InvalidDataException("Composite explicit source LOD needs its actual native manager sampler recipe.");
                actualNative.RequireSourceMipSampling(sampler.SourceRegister==7?0x14u:0u,0,0,0x7F7FFFFF);
            }
            var completed=await first.CompositeParent.DrawComposite44Async(prefix.FrontendParentBinding,graphics,input);
            var presentation=first.CompositeParent.CompletedPresentation??throw new InvalidDataException("Actual Composite44 did not publish its completed source output.");
            await presentation.RefreshAsync();sourceInputs.RequireCurrent();
            var retained=first.CompleteRetainedAfterActualFunctor0(prefix,shaft,actualMiddle,Lens,completedLens,graphics,completed);
            return LastCompleted=new(prefix.Context.FrameNumber,first.Bank.Source.ContextOwnerIdentity,
                Array.AsReadOnly(actualMiddle.CompletedPasses.ToArray()),completedLens,completed,presentation,
                completedLens.SourceEvidence+";"+completed.SourceEvidence+";actual-source-Composite44-output;retained-CPU-source-exit/pair-flip;no-Host-history-commit")
                {RetainedCpuExit=retained};
    }

    private IOriginalMapPostEffect3ComputeTextureLease ReadCompositeTexture(
        OriginalMapPostEffect3WorldLensSourceInputs inputs,OriginalMapPostEffect3FirstWorldPreparedPrefix prefix,
        OriginalMapPostEffect3ParameterCompositeUpdate composite,int parameter)
    {
        var reference=prefix.Textures.Require(parameter);
        if(reference.Reference.PhysicalAllocationParameterIndex is not null)
        {
            var native=first.Bank.Source.Passes[44].ActiveBindings.Last(b=>
                b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&b.ParameterIndex==parameter&&
                b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&(b.StageMask&4)!=0);
            var footprint=OriginalMapPostEffect3CompositeTextureReadFootprint.Prepare(first.Bank.Source,composite,native.RegisterIndex);
            return first.Bank.CaptureCompositeSourceMipReadLease(footprint,prefix.Textures);
        }
        return inputs.ReadActualTexture(parameter);
    }

    public ValueTask DisposeAsync()
    {
        lock(lifetime){if(disposal is not null)return new(disposal);retiring=true;return new(disposal=RetireAsync());}
    }
    private async Task RetireAsync()
    {
        await serial.WaitAsync();
        try{await graphics.DisposeAsync();await Lens.DisposeAsync();if(ownedShaft is not null)await ownedShaft.DisposeAsync();LastCompleted=null;}
        finally{serial.Release();}
    }
}
