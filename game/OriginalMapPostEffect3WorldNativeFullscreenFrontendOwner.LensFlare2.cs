using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner
{
    private LensNativeBeforeState? actualLensBefore;
    private OriginalMapPostEffect3LensFlare2GpuPassSource? pendingLensAfterSource;
    private OriginalMapPostEffect3LensFlare2MipPlan? actualLensAfterPlan;
    private LensAfterStock? actualLensAfterStock;
    private int nextLensAfterMip;

    /// <summary>Retain both real PS7/8 manager setters, including on CS
    /// passes whose shader descriptors do not consume either sampler.</summary>
    public void AcceptLensFlare2NativeBefore(OriginalMapPostEffect3LensFlare2GpuPassSource source,
        IOriginalMapPostEffect3ComputeSamplerLease actualPixelLinear7,
        IOriginalMapPostEffect3ComputeSamplerLease actualPixelPoint8)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(actualPixelLinear7);
        ArgumentNullException.ThrowIfNull(actualPixelPoint8);RequireCurrent();RequireReleasedBeforeNextBind();
        source.RequireCurrentTextureTable(World.TextureParameters);
        actualPixelLinear7.RequireCurrent();actualPixelPoint8.RequireCurrent();
        if(source.FrameNumber!=World.Context.FrameNumber||source.ContextOwnerIdentity!=World.Bank.Source.ContextOwnerIdentity||
            source.SourceSha256!=World.Bank.Source.Source.Sha256||source.SourcePassIndex is <45 or >49||
            actualPixelLinear7.FrameNumber!=source.FrameNumber||actualPixelPoint8.FrameNumber!=source.FrameNumber||
            actualPixelLinear7.ContextOwnerIdentity!=source.ContextOwnerIdentity||actualPixelPoint8.ContextOwnerIdentity!=source.ContextOwnerIdentity||
            actualPixelLinear7.NativeStageOrdinal!=2||actualPixelPoint8.NativeStageOrdinal!=2||
            actualPixelLinear7.SourceRegister!=7||actualPixelPoint8.SourceRegister!=8||
            !actualPixelLinear7.SamplerRid.IsValid||!actualPixelPoint8.SamplerRid.IsValid)
            throw new InvalidDataException("LensFlare2 native Before needs this actual source45..49 and both current PS7/8 manager setters.");
        directoryCursor?.EnterSelectedBefore(source.SourcePassIndex,0);
        actualLensBefore=new(source,actualPixelLinear7,actualPixelPoint8);
        directoryCursor?.RecordBeforeReturned(source);
    }

    public async Task<IOriginalMapPostEffect3FullscreenDrawContextLease> BindLensFlare2DiaphragmAsync(
        OriginalMapPostEffect3LensFlare2GpuPassSource source)
    {
        RequireLensBefore(source);var state=OriginalMapPostEffect3PassStateReader.Read(World.Bank.Source.Source,Source.Locale)[source.SourcePassIndex];
        if(current is not null||actualDofCompute is not null||source.SourcePassIndex!=45||source.Recipe.RequiresGenericComputeDispatch||
            state.Factory.Index!=6||state.NativeFlags!=0||state.AutomaticExecution!=OriginalMapPostEffect3AutomaticExecution.SpecificFactoryGraphics)
            throw new InvalidDataException("LensFlare2 diaphragm Bind requires its selected original VS1/PS4 graphics command.");
        var binding=new NativeContext(this,45,checked(++revision),source.SourceEvidence+";actual-LF2-kind3-RT0-Bind;explicit-VS1/PS4");
        current=binding;
        try
        {
            var target=await World.Bank.CaptureLensFlare2DiaphragmTargetAsync(World.TextureParameters,source,binding);
            RequireLensBefore(source);binding.BindActualTarget(target);directoryCursor?.RecordBindReturned(source,binding);return binding;
        }
        catch{current=null;throw;}
    }

    /// <summary>Select compute mode0 with its actual counted resources. No
    /// color target row is pushed. Mode changes clear the four target slots;
    /// the preceding standard command has already released/reset its row.</summary>
    public void BindLensFlare2Compute(OriginalMapPostEffect3LensFlare2GpuPassSource source)
    {
        RequireLensBefore(source);var state=OriginalMapPostEffect3PassStateReader.Read(World.Bank.Source.Source,Source.Locale)[source.SourcePassIndex];
        if(current is not null||actualDofCompute is not null||actualLensBefore!.ComputeBound||source.SourcePassIndex is <46 or >49||
            !source.Recipe.RequiresGenericComputeDispatch||state.Factory.Index!=6||
            state.AutomaticExecution!=OriginalMapPostEffect3AutomaticExecution.ComputeDispatch)
            throw new InvalidDataException("LensFlare2 compute Bind requires its actual original46..49 CS command and released graphics row.");
        actualLensBefore.ComputeBound=true;revision=checked(revision+1);
        directoryCursor?.RecordBindReturned(source,actualLensBefore);
    }

    public void AcceptLensFlare2Pass(OriginalMapPostEffect3LensFlare2ExecutionOwner actualExecutor,
        OriginalMapPostEffect3LensFlare2GpuPassSource source,
        OriginalMapPostEffect3FullscreenCompletedPass? graphics,OriginalMapPostEffect3ComputeCompletedPass? compute)
    {
        ArgumentNullException.ThrowIfNull(actualExecutor);RequireLensBefore(source);
        if(!ReferenceEquals(actualExecutor.Bank,World.Bank))
            throw new InvalidDataException("LensFlare2 Release belongs to another actual module bank.");
        var history=submissionLedger?.CaptureLens(this,actualExecutor,source,graphics,compute,
            [actualLensBefore!.Linear7,actualLensBefore.Point8]);
        if(source.SourcePassIndex==45)
        {
            if(compute is not null||graphics is null||actualLensBefore!.ComputeBound||
                !ReferenceEquals(actualExecutor.Graphics.LastCompletedPass,graphics)||
                graphics.PixelProgramSha256!=source.ProgramSha256||graphics.VertexProgramSha256!=source.VertexProgramSha256||
                graphics.ConstantBufferReads is not [{ParameterIndex:17,HasCompleteWriterContents:true} cb]||
                cb.CompleteBytesSha256!=source.ConstantBufferBytesSha256)
                throw new InvalidDataException("LensFlare2 graphics Release requires the original45 shader and actual current CB17 submission.");
            ReleaseActualCompleted(graphics);
        }
        else
        {
            if(graphics is not null||compute is null||!actualLensBefore!.ComputeBound||
                !ReferenceEquals(actualExecutor.Compute.LastCompletedPass,compute)||compute.SourcePassIndex!=source.SourcePassIndex||
                compute.FrameNumber!=source.FrameNumber||compute.ContextOwnerIdentity!=source.ContextOwnerIdentity||
                compute.ProgramSha256!=source.ProgramSha256||compute.Dispatch!=source.ActualDispatch||
                compute.OrderedEffectiveBindings.Count==0||compute.ConstantBufferReads.Any(cb=>cb.ParameterIndex==17&&
                    (!cb.HasCompleteWriterContents||cb.CompleteBytesSha256!=source.ConstantBufferBytesSha256)))
                throw new InvalidDataException("LensFlare2 compute Release requires its actual selected original shader/dispatch/resource receipt.");
            //Original1163170 emits its CS automatic command, then the
            //standard1163080 wrapper clears every cached register/RT slot.
        }
        actualLensBefore=null;
        if(source.Recipe.RequiresAfterMipGeneration)
        {pendingLensAfterSource=source;actualLensAfterPlan=null;nextLensAfterMip=0;}
        if(history is not null)submissionLedger!.AcceptAfterActualRelease(history);
    }

    /// <summary>Original114BEA0 After: reset registers, disable RT0 blend,
    /// choose actual physical wrapper+58 mip, then ScreenBegin and stock
    /// rectangle draw. This context never selects a procedural module VS.</summary>
    public async Task<IOriginalMapPostEffect3LensFlare2AfterStockLease> BindLensFlare2AfterMipAsync(
        OriginalMapPostEffect3LensFlare2MipPlan plan,OriginalMapPostEffect3LensFlare2MipStep step)
    {
        ArgumentNullException.ThrowIfNull(plan);ArgumentNullException.ThrowIfNull(step);RequireCurrent();
        plan.RequireCurrentTextureTable(World.TextureParameters);
        var source=pendingLensAfterSource??throw new InvalidOperationException("Original LensFlare2 After needs its actual preceding45/49 command.");
        source.RequireCurrentTextureTable(World.TextureParameters);
        if(current is not null||actualDofCompute is not null||actualLensBefore is not null||actualLensAfterStock is not null||
            plan.FrameNumber!=source.FrameNumber||plan.ContextOwnerIdentity!=source.ContextOwnerIdentity||plan.SourceSha256!=source.SourceSha256||
            plan.SourcePassIndex!=source.SourcePassIndex||plan.ActualTexture.ParameterIndex!=source.OutputParameterIndex||
            !plan.RequiresNativeRegisterResetBeforeMipGeneration||plan.ActualMipCount<2||
            nextLensAfterMip>=plan.Steps.Count||!ReferenceEquals(plan.Steps[nextLensAfterMip],step)||
            actualLensAfterPlan is not null&&!ReferenceEquals(actualLensAfterPlan,plan))
            throw new InvalidDataException("LensFlare2 After Bind needs its exact next original physical mip step after the released producer.");
        if(step.SourceMipLevel!=nextLensAfterMip||step.DestinationMipLevel!=nextLensAfterMip+1)
            throw new InvalidDataException("LensFlare2 After source/destination mip order changed.");
        actualLensAfterPlan=plan;
        if(nextLensAfterMip==0)directoryCursor?.BeginLensAfterResourcesReset(plan.SourcePassIndex);
        var texture=World.Bank.RequireTexture(plan.ActualTexture.ParameterIndex,plan.ActualTexture.Reference.InstanceIndex);
        using var scope=await World.Bank.AcquireBufferExecutionScopeAsync();
        var view=await World.Bank.RequireLensFlare2ViewAsync(texture,(uint)step.DestinationMipLevel,texture.Shape.Formats.RenderTargetFormat0);
        plan.RequireCurrentTextureTable(World.TextureParameters);
        if(view.FirstMipLevel!=step.DestinationMipLevel||view.MipLevelCount!=1||
            view.Extent!=new Vector3I(checked((int)step.DestinationWidth),checked((int)step.DestinationHeight),1))
            throw new InvalidDataException("LensFlare2 native RT mip rectangle differs from its actual device view extent.");
        var binding=new NativeContext(this,plan.SourcePassIndex,checked(++revision),plan.SourceEvidence+
            ";114BEA0-stock-After/1163460-reset/959740-color-only/959250-ScreenBegin;not-module-explicit-VS");
        current=binding;
        try
        {
            var metadata=new LensAfterTarget(this,plan,step,view);binding.BindActualTarget(metadata);
            var stock=new LensAfterStock(this,plan,step,binding,metadata);actualLensAfterStock=stock;return stock;
        }
        catch{current=null;throw;}
    }

    public void AcceptLensFlare2AfterMip(OriginalMapPostEffect3LensFlare2ExecutionOwner actualExecutor,
        OriginalMapPostEffect3LensFlare2MipCompletedDraw completed)
    {
        ArgumentNullException.ThrowIfNull(actualExecutor);ArgumentNullException.ThrowIfNull(completed);RequireCurrent();
        var stock=actualLensAfterStock??throw new InvalidOperationException("No original stock After mip row is bound.");
        stock.RequireCurrentAfterMip(completed.Plan,completed.Step);completed.RequireCurrentSubmission();
        if(!ReferenceEquals(actualExecutor.Bank,World.Bank)||!ReferenceEquals(actualExecutor.LastCompletedMip,completed)||
            completed.StorageRid!=stock.Target.StorageRid||completed.Side!=stock.Target.InstanceIndex||completed.WriterRevision<=0)
            throw new InvalidDataException("LensFlare2 After Release requires its actual original stock submission and physical mip writer.");
        var history=submissionLedger?.CaptureLensMip(this,actualExecutor,completed);
        LastRestoredNativeState=stock.Context.NativeState;
        lastReleasedGraphicsPass=completed.Plan.SourcePassIndex;
        //95A7B0 restores depth/cull/clip and95AB00 pops the actual mip
        //target.114BEA0 restores its saved RT0 blend enable after all mips.
        current=null;actualLensAfterStock=null;nextLensAfterMip++;
        if(nextLensAfterMip==completed.Plan.Steps.Count)
        {pendingLensAfterSource=null;actualLensAfterPlan=null;nextLensAfterMip=0;}
        if(history is not null)submissionLedger!.AcceptMipAfterActualRelease(history);
    }

    private void RequireLensBefore(OriginalMapPostEffect3LensFlare2GpuPassSource source)
    {
        ArgumentNullException.ThrowIfNull(source);RequireCurrent();source.RequireCurrentTextureTable(World.TextureParameters);
        var before=actualLensBefore??throw new InvalidOperationException("Original LensFlare2 Bind needs its actual PS7/8 Before setters.");
        if(!ReferenceEquals(before.Source,source))throw new InvalidDataException("LensFlare2 selected source changed after native Before.");
        before.Linear7.RequireCurrent();before.Point8.RequireCurrent();
    }
    private sealed class LensNativeBeforeState(OriginalMapPostEffect3LensFlare2GpuPassSource source,
        IOriginalMapPostEffect3ComputeSamplerLease linear,IOriginalMapPostEffect3ComputeSamplerLease point)
    {
        internal OriginalMapPostEffect3LensFlare2GpuPassSource Source{get;}=source;
        internal IOriginalMapPostEffect3ComputeSamplerLease Linear7{get;}=linear;
        internal IOriginalMapPostEffect3ComputeSamplerLease Point8{get;}=point;
        internal bool ComputeBound{get;set;}
    }
    private sealed record LensAfterStock(OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner Owner,
        OriginalMapPostEffect3LensFlare2MipPlan Plan,OriginalMapPostEffect3LensFlare2MipStep Step,
        NativeContext Context,LensAfterTarget Target):IOriginalMapPostEffect3LensFlare2AfterStockLease
    {
        public Vector2 CurrentScreenScale29E0=>new(Owner.World.CurrentStock.CurrentScreenScale29E0.X,Owner.World.CurrentStock.CurrentScreenScale29E0.Y);
        public Color CurrentColorScale29F0=>new(Owner.World.CurrentStock.CurrentColorScale29F0.X,Owner.World.CurrentStock.CurrentColorScale29F0.Y,
            Owner.World.CurrentStock.CurrentColorScale29F0.Z,Owner.World.CurrentStock.CurrentColorScale29F0.W);
        public uint CurrentStockAlphaTestWord=>Owner.Source.ModuleDisabledStockAlphaTestWord;
        public bool CurrentCustomStockVertexShaderIsAbsent=>Owner.World.CurrentStock.CurrentCustomVertexShaderIsAbsent;
        public IOriginalMapPostEffect3FullscreenDrawContextLease ActualDrawContext=>Context;
        public string SourceEvidence=>Plan.SourceEvidence+";"+Context.SourceEvidence+";"+Owner.World.CurrentStock.SourceProvenance+
            ";retained-actual-stock29E0/29F0/B0;1161C00-alphaOff->95E970-vATest2860=FF7FFFFF;959250/95A370/95A7B0-stock-screen-helper";
        public void RequireCurrentAfterMip(OriginalMapPostEffect3LensFlare2MipPlan plan,OriginalMapPostEffect3LensFlare2MipStep step)
        {
            Owner.RequireCurrent();Plan.RequireCurrentTextureTable(Owner.World.TextureParameters);Owner.World.CurrentStock.ValidateCurrent();
            Context.RequireCurrentForPass(Plan.SourcePassIndex);Target.RequireCurrentForPass(Plan.SourcePassIndex);
            if(!ReferenceEquals(Owner.actualLensAfterStock,this)||!ReferenceEquals(plan,Plan)||!ReferenceEquals(step,Step)||
                !ReferenceEquals(Owner.actualLensAfterPlan,Plan)||Owner.Source.ModuleStockAlphaTestEnabled)
                throw new InvalidDataException("LensFlare2 stock After state was released or changed while the original helper draw was queued.");
        }
    }
    private sealed record LensAfterTarget(OriginalMapPostEffect3WorldNativeFullscreenFrontendOwner Owner,
        OriginalMapPostEffect3LensFlare2MipPlan Plan,OriginalMapPostEffect3LensFlare2MipStep Step,
        OriginalMapPostEffect3ComputeMipView View):IOriginalMapPostEffect3FullscreenSelectedMipTargetLease
    {
        public long FrameNumber=>Plan.FrameNumber;public string ContextOwnerIdentity=>Plan.ContextOwnerIdentity;
        public string OwnedFrontendIdentity=>Owner.World.CurrentStock.OwnedFrontendIdentity;
        public string ViewIdentity=>Owner.World.CurrentStock.ViewIdentity;
        public int? ParameterIndex=>Plan.ActualTexture.ParameterIndex;public int InstanceIndex=>Plan.ActualTexture.Reference.InstanceIndex;
        public string ProducerOwnerIdentity=>Plan.ActualTexture.Reference.ProducerOwnerIdentity;
        public Rid StorageRid=>View.StorageRid;public Rid TargetViewRid=>View.ViewRid;public uint NativeViewFormat=>View.NativeViewFormat;
        public RenderingDevice.DataFormat DeviceFormat=>View.DeviceFormat;public Vector2I Extent=>new(View.Extent.X,View.Extent.Y);
        public uint NativeFirstMipLevel=>View.FirstMipLevel;public uint NativeMipLevelCount=>View.MipLevelCount;
        public string NativeMipSelectionEvidence=>Plan.SourceEvidence+";actual-physical-wrapper+58/native-mip="+Step.DestinationMipLevel;
        public string SourceEvidence=>NativeMipSelectionEvidence+";allocation/view-metadata-only;stock-executor-supplies-pixels-and-writer";
        public void RequireCurrentForPass(int pass)
        {
            Owner.RequireCurrent();Plan.RequireCurrentTextureTable(Owner.World.TextureParameters);
            var texture=Owner.World.Bank.RequireTexture(Plan.ActualTexture.ParameterIndex,InstanceIndex);
            if(pass!=Plan.SourcePassIndex||texture.StorageRid!=StorageRid||!TargetViewRid.IsValid||
                View.FirstMipLevel!=Step.DestinationMipLevel||View.MipLevelCount!=1)
                throw new InvalidDataException("LensFlare2 actual physical mip target changed.");
        }
        public void RequireSourceSelectedTarget(int pass,int register)
        {RequireCurrentForPass(pass);if(register!=0)throw new InvalidDataException("Original LensFlare2 After only selects RT0.");}
    }
}
