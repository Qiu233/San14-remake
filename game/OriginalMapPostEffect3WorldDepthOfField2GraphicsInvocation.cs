using San14.Data;

/// <summary>Actual current World inputs and frontend for ordinary factory7
/// draws and sprite execution. Bank/shape targets and actual indirect draws
/// remain with their native owners.</summary>
public sealed class OriginalMapPostEffect3WorldDepthOfField2GraphicsInvocation
    :IOriginalMapPostEffect3DepthOfField2SpriteInvocationLease,IOriginalMapPostEffect3DepthOfField2FreshExposureInvocationLease,
     IOriginalMapPostEffect3DepthOfField2RetainedExposureInvocationLease
{
    private readonly OriginalMapPostEffect3WorldControlledFactoryFrame frame;
    private readonly Action<string>? checkpoint;
    private readonly LoadedOriginalMapPostEffect3EyeExposureInitializationSource? exposureInitialization;
    public OriginalMapPostEffect3WorldDepthOfField2GraphicsInvocation(OriginalMapPostEffect3WorldControlledFactoryFrame actualFrame,
        Action<string>? actualSubmissionCheckpoint=null,
        LoadedOriginalMapPostEffect3EyeExposureInitializationSource? actualExposureInitialization=null)
    {ArgumentNullException.ThrowIfNull(actualFrame);actualFrame.RequireCurrent();frame=actualFrame;checkpoint=actualSubmissionCheckpoint;
     exposureInitialization=actualExposureInitialization;}
    public long FrameNumber=>frame.Camera.FrameNumber;
    public string ContextOwnerIdentity=>frame.Camera.State.OwnerIdentity;
    public string SourceSha256=>frame.Camera.State.SourceSha256;
    public IReadOnlyDictionary<int,int> ActualBufferInstances=>frame.AllocationSides.BufferInstances;
    public string SourceEvidence=>frame.Frontend.SourceEvidence+";actual-World-DOF2-inputs-and-write-generations";
    public bool HasRetainedExposure=>frame.Begin.Counter>1;
    public IOriginalMapPostEffect3ComputeMipTextureLease CaptureRetainedExposure(OriginalMapPostEffect3DepthOfField2GraphicsSource actualConsumer)
    {RequireCurrent(frame.DepthOfField,frame.Textures);return (frame.ModuleLifetime??throw new InvalidDataException("Retained module absent."))
        .CapturePriorExposure(frame,actualConsumer.SourcePassIndex,actualConsumer);}
    public OriginalMapPostEffect3DepthOfField2FreshUndefinedExposureSource CaptureFreshUndefinedExposureSource(
        OriginalMapPostEffect3DepthOfField2GraphicsSource actualConsumer)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);actualConsumer.Before.RequirePrepared(frame.DepthOfField);
        return OriginalMapPostEffect3DepthOfField2ExposureInitializationSource.Prepare(
            exposureInitialization??throw new InvalidDataException("Auto first DOF requires its explicit original owned61 null-initial-data source; no seed or guessed prior exposure is accepted."),
            actualConsumer,frame.Textures,frame.Begin,frame.ApplyViewSlot);
    }
    public void RequireCurrent(OriginalMapPostEffect3ParameterDepthOfField2Update update,
        OriginalMapPostEffect3TextureParameterState table)
    {
        frame.RequireCurrent();
        if(!ReferenceEquals(update,frame.DepthOfField)||!ReferenceEquals(table,frame.Textures))
            throw new InvalidDataException("DOF2 World invocation belongs to another prepared factory or pointer table.");
        update.RequireCurrentTextureTable(table);
    }
    public OriginalMapPostEffect3DepthOfField2TargetView CaptureCurrentExternalTargetView(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,int register,
        OriginalMapPostEffect3ResolvedTextureParameter pointer)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        return frame.SceneColor.CaptureDepthOfField2TargetView(before,register,pointer);
    }
    public Task<IOriginalMapPostEffect3FullscreenDrawContextLease> ApplyBeforeAndBindGraphicsAsync(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,IReadOnlyList<OriginalMapPostEffect3DepthOfField2TargetView> targets)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        return Task.FromResult(frame.Frontend.BindDepthOfField2(before,targets));
    }
    public IOriginalMapPostEffect3StockGraphicsTargetLease CaptureCurrentExternalTarget(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,OriginalMapPostEffect3DepthOfField2TargetView view,
        IOriginalMapPostEffect3FullscreenDrawContextLease context)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        return frame.SceneColor.CaptureDepthOfField2Target(before,view,context);
    }
    public Task<IOriginalMapPostEffect3ComputeTextureLease> CaptureCurrentExternalTextureReadAsync(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,OriginalMapPostEffect3Binding binding)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);before.RequireCurrent();
        if(binding.Role!=OriginalMapPostEffect3BindingRole.ShaderResourceRead)
            throw new InvalidDataException("DOF2 actual external capability requires its counted source SRV setter.");
        IOriginalMapPostEffect3ComputeTextureLease read=binding.ParameterIndex==1
            ?frame.World.LeaseInputDepth(frame.DepthSource,1)
            :frame.SceneColor.CaptureCurrentRead(binding.ParameterIndex);
        read.RequireCurrent();return Task.FromResult(read);
    }
    public IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> CaptureCurrentSamplerLeases(
        OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        IReadOnlyList<OriginalMapPostEffect3DepthOfField2SamplerRegister> registers)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);before.RequireCurrent();
        var result=new List<IOriginalMapPostEffect3ComputeSamplerLease>();
        foreach(var register in registers)
        {
            if(register.NativeStageOrdinal!=2||register.SourceRegister is not (7 or 8))
                throw new NotSupportedException("DOF2 Before only selects the actual original PS7/PS8 manager samplers.");
            var selection=new Selection(this,before,register);
            result.Add(new Sampler(selection,frame.Manager.LeaseSelected(selection,2)));
        }
        return result.AsReadOnly();
    }
    public Task AcceptCompletedGraphicsAsync(OriginalMapPostEffect3DepthOfField2GraphicsCoverageReceipt coverage)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);coverage.RequireCurrentSubmission();
        if(!ReferenceEquals(coverage.Owner.Bank,frame.World.Bank))
            throw new InvalidDataException("DOF2 completion belongs to another actual module bank.");
        if(coverage.ActualTargets.Any(t=>t.Target is IOriginalMapPostEffect3FullscreenExternalWritableTargetLease&&
            t.Target.ProducerOwnerIdentity==frame.World.Context.OutputFrame.Textures[0x064B746B].EffectiveSource.OwnerIdentity))
            frame.SceneColor.AcceptDepthOfField2Coverage(coverage);
        frame.Frontend.AcceptDepthOfField2Graphics(coverage);
        checkpoint?.Invoke("controlled-DOF2-source"+coverage.Completed.SourcePassIndex+"-completed");return Task.CompletedTask;
    }
    public Task ApplyBeforeAndBindComputeAsync(OriginalMapPostEffect3DepthOfField2BeforeBindings before)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        frame.Frontend.BindDepthOfField2Compute(before);return Task.CompletedTask;
    }
    public Task AcceptCompletedComputeAsync(OriginalMapPostEffect3DepthOfField2BeforeBindings before,
        OriginalMapPostEffect3ComputeExecutor compute,OriginalMapPostEffect3ComputeCompletedPass completed)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        frame.Frontend.AcceptDepthOfField2Compute(before,compute,completed);
        checkpoint?.Invoke("controlled-DOF2-source"+completed.SourcePassIndex+"-completed");return Task.CompletedTask;
    }
    public Task AcceptCompletedScatterAsync(OriginalMapPostEffect3DepthOfField2ScatterExecutor scatter,
        OriginalMapPostEffect3FullscreenCompletedPass completed)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        frame.Frontend.AcceptDepthOfField2Scatter(scatter,completed);
        checkpoint?.Invoke("controlled-DOF2-source"+completed.SourcePassIndex+"-completed");return Task.CompletedTask;
    }
    private sealed record Selection(OriginalMapPostEffect3WorldDepthOfField2GraphicsInvocation Owner,
        OriginalMapPostEffect3DepthOfField2BeforeBindings Before,OriginalMapPostEffect3DepthOfField2SamplerRegister Register)
        :IOriginalMapPostEffect3ManagerSamplerSelection
    {
        public long FrameNumber=>Owner.FrameNumber;public string ContextOwnerIdentity=>Owner.ContextOwnerIdentity;
        public int NativeStageOrdinal=>Register.NativeStageOrdinal;public uint SourceRegister=>Register.SourceRegister;
        public OriginalMapPostEffect3ManagerSamplerKind Kind=>SourceRegister==7?OriginalMapPostEffect3ManagerSamplerKind.Linear:
            OriginalMapPostEffect3ManagerSamplerKind.Point;
        public string SourceEvidence=>Before.SourceEvidence+";original-DOF2-Before-PS7/8-manager-selection";
        public void RequireCurrent(){Owner.RequireCurrent(Owner.frame.DepthOfField,Owner.frame.Textures);Before.RequireCurrent();}
    }
    private sealed record Sampler(Selection Selection,IOriginalMapPostEffect3ComputeSamplerLease Actual)
        :IOriginalMapPostEffect3DepthOfField2ManagerSamplerLease
    {
        public long FrameNumber=>Actual.FrameNumber;public string ContextOwnerIdentity=>Actual.ContextOwnerIdentity;
        public int NativeStageOrdinal=>Actual.NativeStageOrdinal;public uint SourceRegister=>Actual.SourceRegister;
        public Godot.Rid SamplerRid=>Actual.SamplerRid;public string SourceEvidence=>Actual.SourceEvidence;
        public OriginalMapPostEffect3ManagerSamplerKind ManagerKind=>Selection.Kind;
        public void RequireCurrent(){Selection.RequireCurrent();Actual.RequireCurrent();}
        public void RequireOriginalBeforeSetter(OriginalMapPostEffect3DepthOfField2BeforeBindings before)
        {RequireCurrent();if(!ReferenceEquals(before,Selection.Before))throw new InvalidDataException("DOF2 sampler belongs to another actual Before.");}
    }
}
