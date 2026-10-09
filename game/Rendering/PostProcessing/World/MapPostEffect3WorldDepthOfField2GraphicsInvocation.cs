using San14.Data;

/// <summary>Actual current World inputs and frontend for ordinary factory7
/// draws and sprite execution. Bank/shape targets and actual indirect draws
/// remain with their native owners.</summary>
public sealed class MapPostEffect3WorldDepthOfField2GraphicsInvocation
    :IMapPostEffect3DepthOfField2SpriteInvocationLease,IMapPostEffect3DepthOfField2FreshExposureInvocationLease,
     IMapPostEffect3DepthOfField2RetainedExposureInvocationLease
{
    private readonly MapPostEffect3WorldControlledFactoryFrame frame;
    private readonly Action<string>? checkpoint;
    private readonly LoadedMapPostEffect3EyeExposureInitializationSource? exposureInitialization;
    public MapPostEffect3WorldDepthOfField2GraphicsInvocation(MapPostEffect3WorldControlledFactoryFrame actualFrame,
        Action<string>? actualSubmissionCheckpoint=null,
        LoadedMapPostEffect3EyeExposureInitializationSource? actualExposureInitialization=null)
    {ArgumentNullException.ThrowIfNull(actualFrame);actualFrame.RequireCurrent();frame=actualFrame;checkpoint=actualSubmissionCheckpoint;
     exposureInitialization=actualExposureInitialization;}
    public long FrameNumber=>frame.Camera.FrameNumber;
    public string ContextOwnerIdentity=>frame.Camera.State.OwnerIdentity;
    public string SourceSha256=>frame.Camera.State.SourceSha256;
    public IReadOnlyDictionary<int,int> ActualBufferInstances=>frame.AllocationSides.BufferInstances;
    public string SourceEvidence=>frame.Frontend.SourceEvidence+";actual-World-DOF2-inputs-and-write-generations";
    public bool HasRetainedExposure=>frame.Begin.Counter>1;
    public IMapPostEffect3ComputeMipTextureLease CaptureRetainedExposure(MapPostEffect3DepthOfField2GraphicsSource actualConsumer)
    {RequireCurrent(frame.DepthOfField,frame.Textures);return (frame.ModuleLifetime??throw new InvalidDataException("Retained module absent."))
        .CapturePriorExposure(frame,actualConsumer.SourcePassIndex,actualConsumer);}
    public MapPostEffect3DepthOfField2FreshUndefinedExposureSource CaptureFreshUndefinedExposureSource(
        MapPostEffect3DepthOfField2GraphicsSource actualConsumer)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);actualConsumer.Before.RequirePrepared(frame.DepthOfField);
        return MapPostEffect3DepthOfField2ExposureInitializationSource.Prepare(
            exposureInitialization??throw new InvalidDataException("Auto first DOF requires its explicit original owned61 null-initial-data source; no seed or guessed prior exposure is accepted."),
            actualConsumer,frame.Textures,frame.Begin,frame.ApplyViewSlot);
    }
    public void RequireCurrent(MapPostEffect3ParameterDepthOfField2Update update,
        MapPostEffect3TextureParameterState table)
    {
        frame.RequireCurrent();
        if(!ReferenceEquals(update,frame.DepthOfField)||!ReferenceEquals(table,frame.Textures))
            throw new InvalidDataException("DOF2 World invocation belongs to another prepared factory or pointer table.");
        update.RequireCurrentTextureTable(table);
    }
    public MapPostEffect3DepthOfField2TargetView CaptureCurrentExternalTargetView(
        MapPostEffect3DepthOfField2BeforeBindings before,int register,
        MapPostEffect3ResolvedTextureParameter pointer)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        return frame.SceneColor.CaptureDepthOfField2TargetView(before,register,pointer);
    }
    public Task<IMapPostEffect3FullscreenDrawContextLease> ApplyBeforeAndBindGraphicsAsync(
        MapPostEffect3DepthOfField2BeforeBindings before,IReadOnlyList<MapPostEffect3DepthOfField2TargetView> targets)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        return Task.FromResult(frame.Frontend.BindDepthOfField2(before,targets));
    }
    public IMapPostEffect3StockGraphicsTargetLease CaptureCurrentExternalTarget(
        MapPostEffect3DepthOfField2BeforeBindings before,MapPostEffect3DepthOfField2TargetView view,
        IMapPostEffect3FullscreenDrawContextLease context)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        return frame.SceneColor.CaptureDepthOfField2Target(before,view,context);
    }
    public Task<IMapPostEffect3ComputeTextureLease> CaptureCurrentExternalTextureReadAsync(
        MapPostEffect3DepthOfField2BeforeBindings before,MapPostEffect3Binding binding)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);before.RequireCurrent();
        if(binding.Role!=MapPostEffect3BindingRole.ShaderResourceRead)
            throw new InvalidDataException("DOF2 actual external capability requires its counted source SRV setter.");
        IMapPostEffect3ComputeTextureLease read=binding.ParameterIndex==1
            ?frame.World.LeaseInputDepth(frame.DepthSource,1)
            :frame.SceneColor.CaptureCurrentRead(binding.ParameterIndex);
        read.RequireCurrent();return Task.FromResult(read);
    }
    public IReadOnlyList<IMapPostEffect3ComputeSamplerLease> CaptureCurrentSamplerLeases(
        MapPostEffect3DepthOfField2BeforeBindings before,
        IReadOnlyList<MapPostEffect3DepthOfField2SamplerRegister> registers)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);before.RequireCurrent();
        var result=new List<IMapPostEffect3ComputeSamplerLease>();
        foreach(var register in registers)
        {
            if(register.NativeStageOrdinal!=2||register.SourceRegister is not (7 or 8))
                throw new NotSupportedException("DOF2 Before only selects the actual original PS7/PS8 manager samplers.");
            var selection=new Selection(this,before,register);
            result.Add(new Sampler(selection,frame.Manager.LeaseSelected(selection,2)));
        }
        return result.AsReadOnly();
    }
    public Task AcceptCompletedGraphicsAsync(MapPostEffect3DepthOfField2GraphicsCoverageReceipt coverage)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);coverage.RequireCurrentSubmission();
        if(!ReferenceEquals(coverage.Owner.Bank,frame.World.Bank))
            throw new InvalidDataException("DOF2 completion belongs to another actual module bank.");
        if(coverage.ActualTargets.Any(t=>t.Target is IMapPostEffect3FullscreenExternalWritableTargetLease&&
            t.Target.ProducerOwnerIdentity==frame.World.Context.OutputFrame.Textures[0x064B746B].EffectiveSource.OwnerIdentity))
            frame.SceneColor.AcceptDepthOfField2Coverage(coverage);
        frame.Frontend.AcceptDepthOfField2Graphics(coverage);
        checkpoint?.Invoke("controlled-DOF2-source"+coverage.Completed.SourcePassIndex+"-completed");return Task.CompletedTask;
    }
    public Task ApplyBeforeAndBindComputeAsync(MapPostEffect3DepthOfField2BeforeBindings before)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        frame.Frontend.BindDepthOfField2Compute(before);return Task.CompletedTask;
    }
    public Task AcceptCompletedComputeAsync(MapPostEffect3DepthOfField2BeforeBindings before,
        MapPostEffect3ComputeExecutor compute,MapPostEffect3ComputeCompletedPass completed)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        frame.Frontend.AcceptDepthOfField2Compute(before,compute,completed);
        checkpoint?.Invoke("controlled-DOF2-source"+completed.SourcePassIndex+"-completed");return Task.CompletedTask;
    }
    public Task AcceptCompletedScatterAsync(MapPostEffect3DepthOfField2ScatterExecutor scatter,
        MapPostEffect3FullscreenCompletedPass completed)
    {
        RequireCurrent(frame.DepthOfField,frame.Textures);
        frame.Frontend.AcceptDepthOfField2Scatter(scatter,completed);
        checkpoint?.Invoke("controlled-DOF2-source"+completed.SourcePassIndex+"-completed");return Task.CompletedTask;
    }
    private sealed record Selection(MapPostEffect3WorldDepthOfField2GraphicsInvocation Owner,
        MapPostEffect3DepthOfField2BeforeBindings Before,MapPostEffect3DepthOfField2SamplerRegister Register)
        :IMapPostEffect3ManagerSamplerSelection
    {
        public long FrameNumber=>Owner.FrameNumber;public string ContextOwnerIdentity=>Owner.ContextOwnerIdentity;
        public int NativeStageOrdinal=>Register.NativeStageOrdinal;public uint SourceRegister=>Register.SourceRegister;
        public MapPostEffect3ManagerSamplerKind Kind=>SourceRegister==7?MapPostEffect3ManagerSamplerKind.Linear:
            MapPostEffect3ManagerSamplerKind.Point;
        public string SourceEvidence=>Before.SourceEvidence+";original-DOF2-Before-PS7/8-manager-selection";
        public void RequireCurrent(){Owner.RequireCurrent(Owner.frame.DepthOfField,Owner.frame.Textures);Before.RequireCurrent();}
    }
    private sealed record Sampler(Selection Selection,IMapPostEffect3ComputeSamplerLease Actual)
        :IMapPostEffect3DepthOfField2ManagerSamplerLease
    {
        public long FrameNumber=>Actual.FrameNumber;public string ContextOwnerIdentity=>Actual.ContextOwnerIdentity;
        public int NativeStageOrdinal=>Actual.NativeStageOrdinal;public uint SourceRegister=>Actual.SourceRegister;
        public Godot.Rid SamplerRid=>Actual.SamplerRid;public string SourceEvidence=>Actual.SourceEvidence;
        public MapPostEffect3ManagerSamplerKind ManagerKind=>Selection.Kind;
        public void RequireCurrent(){Selection.RequireCurrent();Actual.RequireCurrent();}
        public void RequireSelectedBeforeSetter(MapPostEffect3DepthOfField2BeforeBindings before)
        {RequireCurrent();if(!ReferenceEquals(before,Selection.Before))throw new InvalidDataException("DOF2 sampler belongs to another actual Before.");}
    }
}
