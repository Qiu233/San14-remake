using San14.Data;

/// <summary>Live source45..116 directory cursor. Rejected entries execute
/// actual source Select/setters/Reset before the next selected Before. Actual
/// selected completions come only from the closed accepted submission ledger.</summary>
public sealed class MapPostEffect3Functor1FrontendCursor
{
    private readonly MapPostEffect3WorldControlledFactoryFrame frame;
    private readonly LoadedMapPostEffect3Functor1FrontendCursorSource source;
    private readonly MapPostEffect3Functor1ShaderCatalog catalog;
    internal MapPostEffect3Functor1GhostMetadataOwner GhostMetadata{get;}
    internal MapPostEffect3Functor1FrontendCacheOwner Cache{get;}
    private readonly List<MapPostEffect3Functor1CpuCallbackAttempt> attempts=[];
    private MapPostEffect3Functor1ShaderCatalog.Entry? selected;
    private object? actualBefore;
    private object? actualBind;
    private int next=45,loop;
    private byte selectionAl,beforeAl;
    private bool failed,lensAfterReset,actualVariantCaptured,actualIterationAccepted;
    private MapPostEffect3Functor1DirectoryReturnedReceipt? returned;
    internal MapPostEffect3Functor1FrontendCursor(MapPostEffect3WorldControlledFactoryFrame frame,
        LoadedMapPostEffect3Functor1FrontendCursorSource source,MapPostEffect3Functor1ShaderCatalog catalog,
        LoadedMapPostEffect3Functor1FrontendLifetimeSource lifetime)
    {
        this.frame=frame;this.source=source;this.catalog=catalog;
        Cache=frame.ModuleLifetime?.Cache??new(source);
        GhostMetadata=new(frame,lifetime);
        if(frame.WrapperDirectory.Wrappers.Any(r=>r.ExpectedSuccessfulDeviceIterationCount>0&&
            frame.Camera.Allocation.Passes[r.SourcePassIndex].ActiveBindings.Any(b=>
                b.Table==MapPostEffect3BindingTable.BufferParameters&&b.ParameterIndex==4)))
            throw new NotSupportedException("Selected B4 needs its real controller828 GPU producer/SRV; this cursor only owns the source metadata loan.");
        var clones=frame.EyeCloneCatalog??throw new InvalidDataException("Source directory requires its actual source85/86 array clone wrapper catalog.");
        clones.Source.RequireCurrentTextureTable(frame.Textures);
        if(!ReferenceEquals(clones.Source.Frame,frame.Eye)||!ReferenceEquals(clones.Bank,frame.World.Bank))
            throw new InvalidDataException("Source Eye clone catalog belongs to another actual prepared frame/bank.");
    }
    public int NextSourcePassIndex=>next;
    public int? ActiveSourcePassIndex=>selected?.Recipe.SourcePassIndex;
    public bool HasUnreturnedFailure=>failed;
    public bool HasDirectoryReturned=>returned is not null;
    internal MapPostEffect3Functor1DirectoryReturnedReceipt ReturnedReceipt=>returned??
        throw new InvalidDataException("Actual source directory has not returned.");
    public bool ProvesActualFrontendEnd=>false;
    public bool CommitsFunctorHistory=>false;
    internal MapPostEffect3Functor1EyeCloneCatalog RequireEyeCloneCatalog()
    {var actual=frame.EyeCloneCatalog??throw new InvalidDataException("Current source Eye clone catalog is absent.");
     actual.Source.RequireCurrentTextureTable(frame.Textures);return actual;}
    internal MapPostEffect3ProgramLibrary Library=>catalog.Library;
    private void RequireExecuting()
    {
        frame.RequireCurrent();
        if(failed||returned is not null)throw new InvalidOperationException("Source directory cursor failed or already returned.");
    }
    internal void EnterSelectedBefore(int pass,int nativeLoop)
    {
        RequireExecuting();
        try
        {
            if(selected is null)
            {
                if(nativeLoop!=0||pass<next)throw new InvalidDataException("Selected Before violates original directory/repeat order.");
                while(next<pass)RunSkippedWrapper();
                if(next!=pass||next>116)throw new InvalidDataException("Selected source wrapper is outside this cursor.");
                Select(frame.WrapperDirectory.Wrappers[next-45]);
                if(selected!.Recipe.ExpectedSuccessfulDeviceIterationCount==0)
                    throw new InvalidDataException("Actual Before attempted a mask rejected or empty wrapper.");
            }
            if(selected.Recipe.SourcePassIndex!=pass||nativeLoop!=loop||actualBefore is not null||actualBind is not null||
                loop>=selected.Recipe.ExpectedSuccessfulDeviceIterationCount)
                throw new InvalidDataException("Selected Before does not follow the exact current wrapper/iteration.");
        }
        catch(Exception e){Fail(e);throw;}
    }
    private void Select(MapPostEffect3Functor1WrapperRecipe recipe)
    {
        var entry=catalog.RequireEntry(recipe);selectionAl=entry.EvaluateActualSelectionAl();
        if(selectionAl==0)throw new NotSupportedException("Actual native CS/PS shader-wrapper selection failed.");
        PostPipelineMode mode=entry.SourceCsWrapper is not null?PostPipelineMode.Compute:PostPipelineMode.Graphics;
        Cache.SelectHeader(entry,mode);selected=entry;loop=0;lensAfterReset=false;attempts.Clear();
        foreach(var setter in source.ReadCountedSetters(frame.Camera.Allocation,recipe.SourcePassIndex))
            Cache.SetCounted(setter,frame.CaptureDirectoryMetadata(setter.Binding));

    }
    private void RunSkippedWrapper()
    {
        var recipe=frame.WrapperDirectory.Wrappers[next-45];
        if(recipe.ExpectedSuccessfulDeviceIterationCount!=0)
            throw new InvalidDataException("A selected source wrapper cannot be skipped while advancing to a later Before: "+next);
        Select(recipe);
        if(recipe.NativeWrapperLoopBound>0)
        {
            byte result=source.ReadReturningBefore(recipe);
            if(result!=0)throw new InvalidDataException("Source mask rejected Before did not return AL0.");
            attempts.Add(new(result,null,null,null));
        }

        ResetFinalizeReturn();
    }
    internal void RecordBeforeReturned(object actualPacket,MapPostEffect3EyeBeforeBindings? eyeBefore=null)
    {
        RequireExecuting();var recipe=selected?.Recipe??throw new InvalidDataException("Before has no actual source Select.");
        if(actualBefore is not null)throw new InvalidDataException("Source Before returned twice.");
        beforeAl=source.ReadReturningBefore(recipe,recipe.FactoryIndex==8?frame.Eye:null,eyeBefore);
        if(beforeAl==0)throw new InvalidDataException("Selected actual CPU Before could not bind this iteration.");
        actualBefore=actualPacket;
        //Both original manager carriers are real native Before SRV setters,
        //independent of whether the eventual shader declares sampler7/8.
        Cache.SetBeforeTexture(2,7,frame.CaptureDirectoryCarrier(true));
        Cache.SetBeforeTexture(2,8,frame.CaptureDirectoryCarrier(false));
    }
    internal void ApplyDepthBefore(MapPostEffect3DepthOfField2BeforeBindings before)
    {
        foreach(var setter in before.OrderedDynamicBindings)
        {
            var actual=frame.CaptureDirectoryTextureMetadata(setter.ActualTexture.ParameterIndex);
            if(setter.Kind==MapPostEffect3DepthOfField2DynamicBindingKind.PixelTexture)
                Cache.SetBeforeTexture(2,setter.NativeRegisterIndex,actual);
            else Cache.SetBeforeTarget(setter.NativeRegisterIndex,actual,-1,-1);
        }
    }
    internal void ApplyEyeBefore(MapPostEffect3EyeBeforeBindings before)
    {
        if(before.RequiresDynamicPixelTextureBinding&&before.SourceTextureParameterIndex is{} input)
        {
            var clones=RequireEyeCloneCatalog();
            Cache.SetBeforeTexture(2,0,clones.Metadata(clones.RequireDynamicSource(before),input));
        }
        if(before.RequiresDynamicTargetBinding&&before.TargetTextureParameterIndex is{} output)
            Cache.SetBeforeTarget(0,frame.CaptureDirectoryTextureMetadata(output),before.TargetMipLevel!.Value,-1);
    }
    internal void ApplyCompositeBefore(MapPostEffect3Composite2BeforeBindings before)
    {if(before.ClearsNativeTarget0)Cache.SetBeforeTarget(0,null,-1,-1);}
    internal void RecordBindReturned(object actualPacket,object actualBinding)
    {
        RequireExecuting();
        if(!ReferenceEquals(actualBefore,actualPacket)||actualBind is not null)
            throw new InvalidDataException("Actual Bind did not return for the exact current Before packet.");
        actualBind=actualBinding;

    }
    internal void RequireActualVariant(MapPostEffect3ProgramLibrary actualLibrary,MapPostEffect3CompiledPass actualProgram,int pass)
    {
        RequireExecuting();
        if(selected is null||selected.Recipe.SourcePassIndex!=pass||actualBind is null||actualVariantCaptured||!ReferenceEquals(actualLibrary,catalog.Library))
            throw new InvalidDataException("Actual device program is not bound to this source cursor/library/Before.");
        catalog.RequireActualVariant(selected,actualProgram);
        actualVariantCaptured=true;

    }
    internal void RequireOrdinaryActualProgram(MapPostEffect3ProgramLibrary actualLibrary,int pass,
        IReadOnlyList<MapPostEffect3NullShaderResource> actualNulls)
    {
        if(actualNulls.Count!=0)throw new NotSupportedException("This actual compute/scatter owner needs its exact null backend variant capture.");
        var actual=selected?.ActualOrdinaryProgram??throw new InvalidDataException("Actual ordinary program has no source Select.");
        RequireActualVariant(actualLibrary,actual,pass);
    }
    internal void AcceptActualIteration(IMapPostEffect3Functor1ActualDeviceReceipt actual)
    {
        RequireExecuting();var recipe=selected?.Recipe??throw new InvalidDataException("Actual acceptance has no selected source wrapper.");
        actual.RequireActualForSource(recipe);
        if(actual.Kind!=MapPostEffect3Functor1DeviceReceiptKind.FactoryExecuteAndAutomaticRelease||
            actual.SourcePassIndex!=recipe.SourcePassIndex||actual.NativeLoopIndex!=loop||actualBefore is null||actualBind is null||
            !actualVariantCaptured||actualIterationAccepted)
            throw new InvalidDataException("Actual device acceptance differs from this source Before/Bind/repeat.");
        actualIterationAccepted=true;

        if(recipe.RequiresLensAfterDeviceJoin)return; //After is still pending.
        FinishIteration(recipe.SourceAfterReturnAlAfterReturning);
    }
    internal void BeginLensAfterResourcesReset(int pass)
    {
        RequireExecuting();if(selected?.Recipe.SourcePassIndex!=pass||!selected.Recipe.RequiresLensAfterDeviceJoin||actualBind is null||
            !actualIterationAccepted||lensAfterReset)
            throw new InvalidDataException("Lens After reset lacks its exact selected producer.");
        Cache.ResetResources();lensAfterReset=true;
    }
    internal void AcceptCompleteLensAfter(IMapPostEffect3Functor1ActualDeviceReceipt actual)
    {
        RequireExecuting();var recipe=selected?.Recipe??throw new InvalidDataException("Lens After has no source wrapper.");
        actual.RequireActualForSource(recipe);
        if(!lensAfterReset||!actualIterationAccepted||actual.Kind!=MapPostEffect3Functor1DeviceReceiptKind.LensAfterMipChain||
            actual.SourcePassIndex!=recipe.SourcePassIndex||!recipe.RequiresLensAfterDeviceJoin)
            throw new InvalidDataException("Lens After lacks its complete actual accepted chain.");
        FinishIteration(recipe.SourceAfterReturnAlAfterReturning);
    }
    private void FinishIteration(byte after)
    {
        var recipe=selected!.Recipe;
        attempts.Add(new(beforeAl,source.ReadBindReturnAfterReturning(),recipe.SourceExecuteReturnAlAfterReturning,after));

        actualBefore=null;actualBind=null;actualVariantCaptured=false;actualIterationAccepted=false;loop++;
        if(recipe.FactoryIndex==8&&loop<recipe.NativeWrapperLoopBound&&after!=0)return;
        ResetFinalizeReturn();
    }
    private void ResetFinalizeReturn()
    {
        var recipe=selected!.Recipe;Cache.ResetPass();
        byte finalize=source.ReturnFinalize(recipe);
        var trace=MapPostEffect3Functor1WrapperControlFlow.Read(recipe,new(selectionAl,Array.AsReadOnly(attempts.ToArray()),
            source.SourceEvidence+";actual-catalog/metadata-cache/callback-return-boundaries;selected-device-authority-separate"));
        if(!trace.FunctionReturned||trace.SourceFunctionReturnAl!=finalize)throw new InvalidDataException("Actual source finalize/CPU return differs.");
        frame.WrapperDirectory.RecordSourceFunctionReturn(recipe,trace);

        next++;selected=null;attempts.Clear();loop=0;
    }
    public MapPostEffect3Functor1DirectoryReturnedReceipt CompleteDirectoryReturnedAndJoinHistory()
    {
        if(returned is not null){RequireReturned(returned);return returned;}
        RequireExecuting();
        try
        {
            if(selected is not null||actualBefore is not null||actualBind is not null)
                throw new InvalidOperationException("The actual selected wrapper/After did not return.");
            frame.Frontend.RequireNoPendingForDirectoryReturn();
            while(next<=116)RunSkippedWrapper();
            if(Cache.OutstandingLoans!=0||Cache.CurrentPassDescriptor is not null||frame.WrapperDirectory.CpuSourceReturns.Count!=72)
                throw new InvalidDataException("Source directory still has unreset loans or missing CPU returns.");
            frame.SubmissionLedger.RequireHistoricalAuthority();
            var cpu=frame.WrapperDirectory.SealCpuDirectoryReturned();
            var device=frame.WrapperDirectory.JoinActualSelectedDeviceReceipts(frame.SubmissionLedger.HistoricalReceipts);
            return returned=new(this,frame,cpu,device,source.SourceEvidence);
        }
        catch(Exception e){Fail(e);throw;}
    }
    internal void RequireReturned(MapPostEffect3Functor1DirectoryReturnedReceipt actual)
    {
        if(!ReferenceEquals(actual,returned)||failed||next!=117||selected is not null||Cache.OutstandingLoans!=0)
            throw new InvalidDataException("Directory return receipt is not this exact closed cursor.");
        frame.CpuInvocation.RequireCurrentExitedDirectory(actual.CpuDirectoryReturned,frame.Textures);
        actual.SelectedDeviceJoin.RequireActualReceipts();frame.SubmissionLedger.RequireHistoricalAuthority();
    }
    private void Fail(Exception e)
    {
        if(failed)return;
        failed=true;
        frame.CpuInvocation.MarkUnreturnedFailure("actual-source-cursor-unreturned;next="+next+";"+e.Message);
    }
    internal void RetireMetadataLoansForFrameDisposal()
    {
        //Exceptional/owner disposal is not original End or controller Return.
        Cache.ResetResources();
        if(returned is null&&!failed)
        {
            failed=true;
            try{frame.CpuInvocation.MarkUnreturnedFailure("source-directory-owner-disposed-before-return;next="+next);}
            catch(InvalidOperationException){/* Another actual owner already marked this CPU generation unresolved. */}
        }
    }
}

public sealed class MapPostEffect3Functor1DirectoryReturnedReceipt
{
    private readonly MapPostEffect3Functor1FrontendCursor owner;
    internal MapPostEffect3Functor1DirectoryReturnedReceipt(MapPostEffect3Functor1FrontendCursor owner,
        MapPostEffect3WorldControlledFactoryFrame frame,MapPostEffect3Functor1SourceReceipt cpu,
        MapPostEffect3Functor1SelectedDeviceJoin device,string evidence)
        =>(this.owner,FrameNumber,CpuDirectoryReturned,SelectedDeviceJoin,SourceEvidence)=
            (owner,frame.Camera.FrameNumber,cpu,device,evidence);
    public long FrameNumber{get;}
    public MapPostEffect3Functor1SourceReceipt CpuDirectoryReturned{get;}
    public MapPostEffect3Functor1SelectedDeviceJoin SelectedDeviceJoin{get;}
    public string SourceEvidence{get;}
    public bool ProvesActualFrontendEnd=>false;
    public bool CommitsFunctorHistory=>false;
    public void RequireCurrent()=>owner.RequireReturned(this);
}
