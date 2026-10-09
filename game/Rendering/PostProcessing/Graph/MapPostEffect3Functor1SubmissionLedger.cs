using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using San14.Data;

/// <summary>Module-owned history of actual accepted source submissions. Mint
/// methods are internal typed actual-owner gates. Public completed records,
/// AL bytes and booleans cannot create receipts. Historical verification never
/// calls disposed executors, old view leases or texture readiness APIs.</summary>
public sealed class MapPostEffect3Functor1SubmissionLedger
{
    private static readonly ConditionalWeakTable<MapPostEffect3Functor1ModuleState,ModuleHistory> modules=new();
    private sealed class ModuleHistory{internal readonly object Sync=new();internal readonly Dictionary<long,MapPostEffect3Functor1SubmissionLedger> Frames=[];}
    private readonly ModuleHistory moduleHistory;
    private readonly MapPostEffect3WorldControlledFactoryFrame frame;
    private readonly MapPostEffect3Functor1WrapperRecipe[] recipes;
    private readonly Dictionary<(int Pass,int Loop,MapPostEffect3Functor1DeviceReceiptKind Kind),HistoricalReceipt> receipts=[];
    private readonly List<HistoricalReceipt> ordered=[];
    private readonly Dictionary<int,MipChain> mipChains=[];
    private MapPostEffect3Functor1SubmissionLedger(ModuleHistory moduleHistory,MapPostEffect3WorldControlledFactoryFrame frame)
    {this.moduleHistory=moduleHistory;this.frame=frame;recipes=frame.WrapperDirectory.Wrappers.ToArray();
        FrameNumber=frame.Camera.FrameNumber;ContextOwnerIdentity=frame.Camera.State.OwnerIdentity;ModuleSourceSha256=frame.Camera.State.SourceSha256;}
    internal static MapPostEffect3Functor1SubmissionLedger CreateOwned(MapPostEffect3WorldControlledFactoryFrame frame)
    {
        frame.RequireCurrent();frame.CpuInvocation.RequireOwnedModule(frame.CpuModule);
        var module=modules.GetValue(frame.CpuModule,_=>new());lock(module.Sync)
        {if(module.Frames.ContainsKey(frame.Camera.FrameNumber))throw new InvalidOperationException("This exact module frame already owns a historical submission ledger.");
            var result=new MapPostEffect3Functor1SubmissionLedger(module,frame);module.Frames.Add(result.FrameNumber,result);
            //Only current and immediately preceding source invocation may
            //retain actual authority. Descriptive evidence is not authority;
            //an evicted old receipt cannot admit a new read or new join.
            MapPostEffect3Functor1HistoryWindow.Prune(module.Frames,result.FrameNumber);
            return result;}
    }
    internal static void AbortUndeliveredFrame(MapPostEffect3Functor1ModuleState actualModule,long frameNumber)
    {
        if(!modules.TryGetValue(actualModule,out var module))return;
        lock(module.Sync)
        {
            if(!module.Frames.TryGetValue(frameNumber,out var ledger))return;
            if(ledger.ordered.Count!=0||ledger.receipts.Count!=0||ledger.mipChains.Count!=0)
                throw new InvalidOperationException("An actually submitted ledger cannot be cancelled as an undelivered construction.");
            module.Frames.Remove(frameNumber);
        }
    }
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public string ModuleSourceSha256{get;}
    public bool ProvesWholeFunctorCompletion=>false;
    public bool ProvesFrontendEnd=>false;
    public bool ProvesCurrentTextureReadiness=>false;
    public bool CommitsHistorySelectors=>false;
    public IReadOnlyList<IMapPostEffect3Functor1ActualDeviceReceipt> HistoricalReceipts
    {get{lock(moduleHistory.Sync)return Array.AsReadOnly(ordered.Cast<IMapPostEffect3Functor1ActualDeviceReceipt>().ToArray());}}
    public IReadOnlyList<int> IncompleteLensAfterPasses
    {get{lock(moduleHistory.Sync)return Array.AsReadOnly(mipChains.Where(p=>!p.Value.Complete).Select(p=>p.Key).Order().ToArray());}}
    /// <summary>Read-only historical check. This is neither directory Seal nor
    /// device Join, and grants no pixel/resource capability after retirement.</summary>
    public void RequireHistoricalAuthority()
    {lock(moduleHistory.Sync){MapPostEffect3Functor1HistoryWindow.RequireOwned(moduleHistory.Frames,FrameNumber,this);
        foreach(var r in ordered)r.RequireActualForSource(recipes[r.SourcePassIndex-45]);}}
    internal void RequireFrontend(MapPostEffect3WorldNativeFullscreenFrontendOwner actual)
    {frame.RequireCurrent();if(!ReferenceEquals(actual,frame.Frontend)||!ReferenceEquals(actual.World.Bank,frame.World.Bank)||
        !moduleHistory.Frames.TryGetValue(FrameNumber,out var current)||!ReferenceEquals(current,this))
        throw new InvalidDataException("Historical submission issuer is not this exact module frame frontend.");}
    internal void RequireEyePrepared(MapPostEffect3EyeAdaptation2Frame actual)
    {if(!ReferenceEquals(actual,frame.Eye))throw new InvalidDataException("Eye historical source must be this exact wrapper preparation.");}
    internal sealed class PendingSubmission
    {
        internal PendingSubmission(MapPostEffect3Functor1SubmissionLedger owner,MapPostEffect3Functor1WrapperRecipe recipe,int loop,
            object executor,object actualCommand,string evidence)
            =>(Owner,Recipe,Loop,Executor,ActualCommand,Evidence)=(owner,recipe,loop,executor,actualCommand,evidence);
        internal readonly MapPostEffect3Functor1SubmissionLedger Owner;
        internal readonly MapPostEffect3Functor1WrapperRecipe Recipe;
        internal readonly int Loop;
        internal readonly object Executor,ActualCommand;
        internal readonly string Evidence;
    }
    private MapPostEffect3Functor1WrapperRecipe RequireRecipe(int pass,int loop,object prepared)
    {
        if(pass is <45 or >116)throw new InvalidDataException("Historical submission has no source Functor1 wrapper.");
        var recipe=recipes[pass-45];
        if(!ReferenceEquals(recipe.SourcePreparedIdentity,prepared)||loop<0||loop>=recipe.ExpectedSuccessfulDeviceIterationCount||
            receipts.ContainsKey((pass,loop,MapPostEffect3Functor1DeviceReceiptKind.FactoryExecuteAndAutomaticRelease)))
            throw new InvalidDataException("Historical submission is foreign, duplicate, mask rejected or outside the actual source loop.");
        return recipe;
    }
    internal static void RequireActualIdentity(object? latest,object actual)
    {ArgumentNullException.ThrowIfNull(actual);if(latest is null||!ReferenceEquals(latest,actual))
        throw new InvalidDataException("Public completed values or copies cannot substitute for the actual executor's latest submitted object.");}
    // Source scatter submits the indirect command even when the actual CS46
    // append counter is zero. Its live prefix/SRV identity still applies, but
    // no buffer texel is read and a zero-byte range is not a readability grant.
    private static void RequireHistoricalSpriteReadRange(IMapPostEffect3ComputeBufferLease actual,uint actualPrefixBytes)
    {actual.RequireCurrent();if(actualPrefixBytes!=0)actual.RequireReadRange(0,actualPrefixBytes);}
    private PendingSubmission Prepare(MapPostEffect3WorldNativeFullscreenFrontendOwner issuer,int pass,int loop,object prepared,
        object executor,object command,string evidence)
    {RequireFrontend(issuer);lock(moduleHistory.Sync){var recipe=RequireRecipe(pass,loop,prepared);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        return new(this,recipe,loop,executor,command,evidence);}}
    internal PendingSubmission CaptureDepthGraphics(MapPostEffect3WorldNativeFullscreenFrontendOwner issuer,
        MapPostEffect3DepthOfField2GraphicsCoverageReceipt coverage)
    {
        coverage.RequireCurrentSubmission();var owner=coverage.Owner;coverage.Source.Before.RequirePrepared(frame.DepthOfField);
        if(!ReferenceEquals(owner.Prepared,frame.DepthOfField)||!ReferenceEquals(owner.Bank,frame.World.Bank))
            throw new InvalidDataException("DOF history uses another actual prepared owner.");
        RequireActualIdentity(owner.Graphics.LastCompletedPass,coverage.Completed);
        var program=owner.CaptureLastHistoricalProgram(coverage.Completed);
        frame.DirectoryCursor?.RequireActualVariant(owner.Graphics.Library,program,coverage.Completed.SourcePassIndex);
        CheckGraphics(coverage.Completed,frame.DepthOfField,frame.DepthOfField.ConstantBuffer);
        return Prepare(issuer,coverage.Completed.SourcePassIndex,0,frame.DepthOfField,owner,coverage.Completed,coverage.Source.SourceEvidence);
    }
    internal PendingSubmission CaptureDepthCompute(MapPostEffect3WorldNativeFullscreenFrontendOwner issuer,
        MapPostEffect3DepthOfField2BeforeBindings before,MapPostEffect3ComputeExecutor executor,
        MapPostEffect3ComputeCompletedPass completed)
    {
        before.RequirePrepared(frame.DepthOfField);RequireActualIdentity(executor.LastCompletedPass,completed);
        if(!ReferenceEquals(executor.Bank,frame.World.Bank)||completed.SourcePassIndex!=before.SourcePassIndex||completed.SourcePassIndex is not(57 or 58))
            throw new InvalidDataException("DOF historical compute is not the actual source57/58 owner.");
        CheckCompute(completed,frame.DepthOfField.ConstantBuffer);
        frame.DirectoryCursor?.RequireOrdinaryActualProgram(executor.Library,completed.SourcePassIndex,completed.SourceNullShaderResources);
        return Prepare(issuer,completed.SourcePassIndex,0,frame.DepthOfField,executor,completed,before.SourceEvidence);
    }
    internal PendingSubmission CaptureDepthScatter(MapPostEffect3WorldNativeFullscreenFrontendOwner issuer,
        MapPostEffect3DepthOfField2ScatterExecutor executor,MapPostEffect3FullscreenCompletedPass completed)
    {
        RequireActualIdentity(executor.LastCompletedPass,completed);
        var input=executor.LastInputs??throw new InvalidDataException("Actual scatter inputs absent.");
        var copy=executor.LastStagingCopy??throw new InvalidDataException("Actual native staging copy absent.");
        input.Before.RequirePrepared(frame.DepthOfField);input.ActualSpritePrefix.RequireCurrent();
        input.ActualArguments.RequireReadRange(0,32);
        RequireHistoricalSpriteReadRange(input.ActualSpriteRead,input.ActualSpritePrefix.ReadableByteCount);
        if(!ReferenceEquals(executor.Bank,frame.World.Bank)||!ReferenceEquals(input.Prepared,frame.DepthOfField)||
            !ReferenceEquals(input.TextureParameters,frame.Textures)||!ReferenceEquals(input.ActualSpritePrefix.Owner.Prepared,frame.DepthOfField)||
            completed.SourcePassIndex!=input.Before.SourcePassIndex||completed.SourcePassIndex is not(62 or 63)||copy.FrameNumber!=FrameNumber||
            copy.SourcePassIndex!=completed.SourcePassIndex||copy.SourceSide28!=input.Before.ActualStagingSide28||copy.ByteCount!=32||
            copy.ActualArgumentsStorageRid!=input.ActualArguments.StorageRid||copy.ActualArgumentsWriterRevision!=input.ActualArguments.UploadRevision||
            copy.ActualArgumentsPhysicalSide!=input.ActualArguments.InstanceIndex||!copy.ActualStagingStorageRid.IsValid||copy.ActualStagingWriteRevision<=0)
            throw new InvalidDataException("DOF historical scatter lacks exact prefix, source indirect arguments, native clear and actual staging copy.");
        CheckGraphics(completed,frame.DepthOfField,frame.DepthOfField.ConstantBuffer);
        frame.DirectoryCursor?.RequireOrdinaryActualProgram(executor.Library,completed.SourcePassIndex,completed.SourceNullShaderResources);
        return Prepare(issuer,completed.SourcePassIndex,0,frame.DepthOfField,executor,completed,input.Before.SourceEvidence);
    }
    internal PendingSubmission CaptureLens(MapPostEffect3WorldNativeFullscreenFrontendOwner issuer,
        MapPostEffect3LensFlare2ExecutionOwner executor,MapPostEffect3LensFlare2GpuPassSource source,
        MapPostEffect3FullscreenCompletedPass? graphics,MapPostEffect3ComputeCompletedPass? compute,
        IReadOnlyList<IMapPostEffect3ComputeSamplerLease> actualBeforeSamplers)
    {
        source.RequireCurrentTextureTable(frame.Textures);
        if(!ReferenceEquals(executor.Bank,frame.World.Bank)||!ReferenceEquals(source.Recipe.ConstantBuffer,frame.Lens.ConstantBuffer))
            throw new InvalidDataException("Lens historical submission belongs to another exact prepared owner.");
        foreach(var sampler in actualBeforeSamplers)sampler.RequireCurrent();
        if(graphics is not null&&compute is null)
        {RequireActualIdentity(executor.Graphics.LastCompletedPass,graphics);CheckGraphics(graphics,frame.Lens,frame.Lens.ConstantBuffer);
            frame.DirectoryCursor?.RequireOrdinaryActualProgram(executor.Programs,graphics.SourcePassIndex,graphics.SourceNullShaderResources);
            return Prepare(issuer,graphics.SourcePassIndex,0,frame.Lens,executor,graphics,source.SourceEvidence);}
        if(compute is not null&&graphics is null)
        {RequireActualIdentity(executor.Compute.LastCompletedPass,compute);CheckCompute(compute,frame.Lens.ConstantBuffer);
            frame.DirectoryCursor?.RequireOrdinaryActualProgram(executor.Programs,compute.SourcePassIndex,compute.SourceNullShaderResources);
            return Prepare(issuer,compute.SourcePassIndex,0,frame.Lens,executor,compute,source.SourceEvidence);}
        throw new InvalidDataException("Lens historical submission needs its actual single graphics or compute command.");
    }
    internal PendingSubmission CaptureEyeCompute(MapPostEffect3WorldNativeFullscreenFrontendOwner issuer,
        MapPostEffect3EyeAdaptation2Frame actualFrame,MapPostEffect3EyeBeforeBindings before,
        MapPostEffect3ComputeExecutor executor,MapPostEffect3ComputeCompletedPass completed)
    {
        RequireActualIdentity(executor.LastCompletedPass,completed);
        if(!ReferenceEquals(actualFrame,frame.Eye)||!ReferenceEquals(executor.Bank,frame.World.Bank)||
            completed.SourcePassIndex!=before.Iteration.SourcePassIndex||!before.Iteration.IsCompute||!frame.Eye.OrderedIterations.Contains(before.Iteration))
            throw new InvalidDataException("Eye history does not match its actual prepared iteration.");
        CheckCompute(completed,frame.Exposure);
        frame.DirectoryCursor?.RequireOrdinaryActualProgram(executor.Library,completed.SourcePassIndex,completed.SourceNullShaderResources);
        return Prepare(issuer,completed.SourcePassIndex,before.Iteration.NativeLoopIndex,frame.Eye,executor,completed,before.SourceEvidence);
    }
    internal PendingSubmission CaptureEyeGraphics(MapPostEffect3WorldNativeFullscreenFrontendOwner issuer,
        MapPostEffect3EyeBeforeBindings before,MapPostEffect3EyeBloomExecutionOwner executor,
        MapPostEffect3FullscreenCompletedPass completed)
    {
        RequireActualIdentity(executor.LastCompleted,completed);RequireActualIdentity(executor.Graphics.LastCompletedPass,completed);
        if(!ReferenceEquals(executor.Bank,frame.World.Bank)||before.Iteration.SourcePassIndex!=completed.SourcePassIndex||
            before.Iteration.IsCompute||!frame.Eye.OrderedIterations.Contains(before.Iteration))throw new InvalidDataException("Eye history iteration differs.");
        var cbs=before.BloomLoopConstants is{} loop?new[]{frame.Eye.BloomMainConstants,loop}:new[]{frame.Eye.BloomMainConstants};
        CheckGraphics(completed,frame.Eye,cbs);
        var program=executor.CaptureLastHistoricalProgram(completed,frame.Eye,before);
        frame.DirectoryCursor?.RequireActualVariant(executor.Graphics.Library,program,completed.SourcePassIndex);
        frame.EyeCloneCatalog?.RequireCompletedBindings(before,completed);
        return Prepare(issuer,completed.SourcePassIndex,before.Iteration.NativeLoopIndex,frame.Eye,executor,completed,before.SourceEvidence);
    }
    internal PendingSubmission CaptureComposite(MapPostEffect3WorldNativeFullscreenFrontendOwner issuer,
        MapPostEffect3Composite2GraphicsCoverageReceipt coverage)
    {
        coverage.RequireCurrentSubmission();RequireActualIdentity(coverage.Owner.Executor.LastCompletedPass,coverage.Completed);
        if(!ReferenceEquals(coverage.Owner.Frame,frame.Composite)||!ReferenceEquals(coverage.Owner.Bank,frame.World.Bank)||
            !ReferenceEquals(coverage.Recipe.Before.Frame,frame.Composite))throw new InvalidDataException("Composite historical preparation differs.");
        CheckGraphics(coverage.Completed,frame.Composite,frame.Composite.Prepared.ConstantBuffer);
        var program=coverage.Owner.CaptureLastHistoricalProgram(coverage.Completed);
        frame.DirectoryCursor?.RequireActualVariant(coverage.Owner.Executor.Library,program,coverage.Completed.SourcePassIndex);
        return Prepare(issuer,coverage.Completed.SourcePassIndex,0,frame.Composite,coverage.Owner,coverage.Completed,coverage.Recipe.Before.SourceEvidence);
    }
    internal void AcceptAfterActualRelease(PendingSubmission accepted)
    {
        lock(moduleHistory.Sync)
        {
            if(!ReferenceEquals(accepted.Owner,this))throw new InvalidDataException("Foreign historical acceptance candidate.");
            var recipe=RequireRecipe(accepted.Recipe.SourcePassIndex,accepted.Loop,accepted.Recipe.SourcePreparedIdentity!);
            if(!ReferenceEquals(recipe,accepted.Recipe))throw new InvalidDataException("Historical acceptance recipe changed.");
            var receipt=new HistoricalReceipt(this,recipe,accepted.Loop,MapPostEffect3Functor1DeviceReceiptKind.FactoryExecuteAndAutomaticRelease,
                new ActualSubmissionIdentity(accepted.Executor,accepted.ActualCommand),accepted.Evidence);
            receipts.Add((receipt.SourcePassIndex,receipt.NativeLoopIndex,receipt.Kind),receipt);ordered.Add(receipt);
            frame.DirectoryCursor?.AcceptActualIteration(receipt);
        }
    }
    internal sealed record PendingMip(MapPostEffect3Functor1SubmissionLedger Owner,MapPostEffect3LensFlare2ExecutionOwner Executor,
        MapPostEffect3LensFlare2MipCompletedDraw Completed);
    private sealed class MipChain(MapPostEffect3LensFlare2ExecutionOwner executor,MapPostEffect3LensFlare2MipPlan plan)
    {internal readonly MapPostEffect3LensFlare2ExecutionOwner Executor=executor;internal readonly MapPostEffect3LensFlare2MipPlan Plan=plan;
        internal readonly List<MapPostEffect3LensFlare2MipCompletedDraw> Mips=[];internal bool Complete;}
    internal PendingMip CaptureLensMip(MapPostEffect3WorldNativeFullscreenFrontendOwner issuer,
        MapPostEffect3LensFlare2ExecutionOwner executor,MapPostEffect3LensFlare2MipCompletedDraw completed)
    {
        RequireFrontend(issuer);completed.RequireCurrentSubmission();RequireActualIdentity(executor.LastCompletedMip,completed);
        var plan=completed.Plan;plan.RequireCurrentTextureTable(frame.Textures);
        lock(moduleHistory.Sync)
        {
            int pass=plan.SourcePassIndex;
            if(!ReferenceEquals(executor.Bank,frame.World.Bank)||pass is not(45 or 49)||!recipes[pass-45].RequiresLensAfterDeviceJoin||
                !receipts.TryGetValue((pass,0,MapPostEffect3Functor1DeviceReceiptKind.FactoryExecuteAndAutomaticRelease),out var main)||
                main.Submission.Executor!=executor||completed.WriterRevision<=0||completed.StorageRid.Id==0)throw new InvalidDataException("Lens After history lacks its accepted main command and actual stock draw.");
            int next=mipChains.TryGetValue(pass,out var chain)?chain.Mips.Count:0;
            if(chain is not null&&(chain.Complete||!ReferenceEquals(chain.Executor,executor)||!ReferenceEquals(chain.Plan,plan))||
                next>=plan.Steps.Count||!ReferenceEquals(plan.Steps[next],completed.Step)||completed.Step.SourceMipLevel!=next||completed.Step.DestinationMipLevel!=next+1)
                throw new InvalidDataException("Lens After historical acceptance is duplicate, incomplete or out of original mip order.");
            return new(this,executor,completed);
        }
    }
    internal void AcceptMipAfterActualRelease(PendingMip accepted)
    {
        lock(moduleHistory.Sync)
        {
            if(!ReferenceEquals(accepted.Owner,this))throw new InvalidDataException("Foreign Lens After acceptance.");
            var completed=accepted.Completed;var plan=completed.Plan;int pass=plan.SourcePassIndex;
            if(!mipChains.TryGetValue(pass,out var chain)){chain=new(accepted.Executor,plan);mipChains.Add(pass,chain);}
            if(chain.Complete||!ReferenceEquals(chain.Plan,plan)||!ReferenceEquals(chain.Executor,accepted.Executor)||
                completed.Step.SourceMipLevel!=chain.Mips.Count)throw new InvalidDataException("Lens After acceptance changed source order.");
            chain.Mips.Add(completed);
            if(chain.Mips.Count!=plan.Steps.Count)return;
            if(chain.Mips.Count!=plan.ActualMipCount-1)throw new InvalidDataException("Lens After did not accept its complete native mip chain.");
            var recipe=recipes[pass-45];
            var receipt=new HistoricalReceipt(this,recipe,0,MapPostEffect3Functor1DeviceReceiptKind.LensAfterMipChain,
                new ActualSubmissionIdentity(accepted.Executor,chain.Mips.ToArray()),plan.SourceEvidence+";all actual original stock mip submissions accepted in order");
            receipts.Add((pass,0,receipt.Kind),receipt);ordered.Add(receipt);chain.Complete=true;
            frame.DirectoryCursor?.AcceptCompleteLensAfter(receipt);
        }
    }
    private void CheckGraphics(MapPostEffect3FullscreenCompletedPass completed,object prepared,params MapPostEffect3ParameterConstantBuffer[] cbs)
    {
        if(completed.SourcePassIndex is <45 or >116||!ReferenceEquals(recipes[completed.SourcePassIndex-45].SourcePreparedIdentity,prepared))
            throw new InvalidDataException("Historical graphics preparation differs from the exact source wrapper.");
        if(completed.FrameNumber!=FrameNumber||completed.ContextOwnerIdentity!=ContextOwnerIdentity||!completed.WasPixelDrawSubmitted||
            completed.Writes.Count==0)throw new InvalidDataException("Historical graphics lacks a real submitted source command/target.");
        var pass=frame.Camera.Allocation.Source.Passes[completed.SourcePassIndex];
        foreach(var p in pass.ProgramReferences.Where(p=>p.StageIndex is not null))
            if(p.StageCode is 0 or 1&&frame.Camera.Allocation.Source.GetProgram(p.StageCode,p.StageIndex!.Value).Sha256!=
                (p.StageCode==0?completed.PixelProgramSha256:completed.VertexProgramSha256))throw new InvalidDataException("Historical original graphics shader differs.");
        CheckCbs(completed.ConstantBufferReads,cbs);
    }
    private void CheckCompute(MapPostEffect3ComputeCompletedPass completed,params MapPostEffect3ParameterConstantBuffer[] cbs)
    {
        var p=frame.Camera.Allocation.Source.Passes[completed.SourcePassIndex].ProgramReferences.Single(p=>p.StageCode==5);
        if(completed.FrameNumber!=FrameNumber||completed.ContextOwnerIdentity!=ContextOwnerIdentity||
            completed.ProgramSha256!=frame.Camera.Allocation.Source.GetProgram(5,p.StageIndex!.Value).Sha256||
            completed.Dispatch!=frame.Camera.Allocation.Passes[completed.SourcePassIndex].DispatchExtent||completed.OrderedEffectiveBindings.Count==0)
            throw new InvalidDataException("Historical compute original program/dispatch/binding differs.");
        CheckCbs(completed.ConstantBufferReads,cbs);
    }
    private static void CheckCbs(IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> reads,
        IReadOnlyList<MapPostEffect3ParameterConstantBuffer> packets)
    {foreach(var cb in reads){var known=packets.SingleOrDefault(p=>p.Declaration.Index==cb.ParameterIndex);
        if(known is not null&&(!cb.HasCompleteWriterContents||cb.CompleteBytesSha256!=Hash(known.Bytes.Span)))
            throw new InvalidDataException("Historical command differs from its actual original CPU CB writer.");}}
    // Submission checks borrow the live inputs before execution. Only command
    // and program identities are retained after their release.
    internal static void RequireGraphicsInputs(MapPostEffect3FullscreenFrameInputs actual)
    {
        actual.CurrentDrawContext.RequireCurrentForPass(actual.SourceDraw.SourcePassIndex);
        foreach(var sampler in actual.CurrentSamplers)sampler.RequireCurrent();
    }
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexString(SHA256.HashData(b));
    private sealed class ActualSubmissionIdentity(object executor,object command)
    {internal readonly object Executor=executor,Command=command;}
    private sealed class HistoricalReceipt:IMapPostEffect3Functor1ActualDeviceReceipt
    {
        private readonly MapPostEffect3Functor1SubmissionLedger owner;
        private readonly MapPostEffect3Functor1WrapperRecipe recipe;
        internal HistoricalReceipt(MapPostEffect3Functor1SubmissionLedger owner,MapPostEffect3Functor1WrapperRecipe recipe,
            int loop,MapPostEffect3Functor1DeviceReceiptKind kind,ActualSubmissionIdentity submission,string evidence)
        {this.owner=owner;this.recipe=recipe;NativeLoopIndex=loop;Kind=kind;Submission=submission;
            SourceEvidence=evidence+";closed actual acceptance history;no current readiness/End/wholeFunctor/selector commit";}
        internal ActualSubmissionIdentity Submission{get;}
        public long FrameNumber=>owner.FrameNumber;
        public string ContextOwnerIdentity=>owner.ContextOwnerIdentity;
        public string ModuleSourceSha256=>owner.ModuleSourceSha256;
        public int SourcePassIndex=>recipe.SourcePassIndex;
        public int NativeLoopIndex{get;}
        public MapPostEffect3Functor1DeviceReceiptKind Kind{get;}
        public object SourcePreparedIdentity=>recipe.SourcePreparedIdentity!;
        public object ActualSubmissionIdentity=>Submission;
        public string SourceEvidence{get;}
        public void RequireActualForSource(MapPostEffect3Functor1WrapperRecipe actual)
        {lock(owner.moduleHistory.Sync){if(!ReferenceEquals(actual,recipe)||!ReferenceEquals(actual.SourcePreparedIdentity,SourcePreparedIdentity)||
            !owner.receipts.TryGetValue((SourcePassIndex,NativeLoopIndex,Kind),out var accepted)||!ReferenceEquals(accepted,this)||
            !owner.moduleHistory.Frames.TryGetValue(FrameNumber,out var current)||!ReferenceEquals(current,owner))
            throw new InvalidDataException("Historical receipt has no exact original accepted command/recipe/module identity.");}}
    }
}
