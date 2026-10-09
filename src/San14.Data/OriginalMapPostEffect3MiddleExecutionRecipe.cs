namespace San14.Data;

/// <summary>Factory2/3 use the shared native enabled-byte Before hook, then
/// the standard automatic CS wrapper. The actual initialized frame supplies
/// the predicate and complete constant buffers; no effect setting is inferred
/// from a pass name or an allocated image.</summary>
public sealed class OriginalMapPostEffect3MiddleExecutionRecipe
{
    private readonly OriginalMapPostEffect3OwnedFreshCameraFrame frame;
    private readonly OriginalMapPostEffect3OwnedFactoryInitialization initialization;
    private OriginalMapPostEffect3MiddleExecutionRecipe(
        OriginalMapPostEffect3OwnedFreshCameraFrame frame,
        OriginalMapPostEffect3OwnedFactoryInitialization initialization,
        OriginalMapPostEffect3PassState pass,bool enabled,
        IReadOnlyList<OriginalMapPostEffect3ParameterConstantBuffer> buffers,
        IReadOnlyList<OriginalMapPostEffect3ParameterConstantBufferBinding> bindings)
    {
        this.frame=frame;this.initialization=initialization;Pass=pass;
        ExecutesPass=enabled;ConstantBuffers=buffers;ConstantBufferBindings=bindings;
    }
    public long FrameNumber=>frame.FrameNumber;
    public string ContextOwnerIdentity=>frame.OwnerIdentity;
    public string SourceSha256=>frame.Allocation.Source.Sha256;
    public int SourcePassIndex=>Pass.Pass.Index;
    public OriginalMapPostEffect3PassState Pass{get;}
    public bool ExecutesPass{get;}
    public IReadOnlyList<OriginalMapPostEffect3ParameterConstantBuffer> ConstantBuffers{get;}
    public IReadOnlyList<OriginalMapPostEffect3ParameterConstantBufferBinding> ConstantBufferBindings{get;}
    public bool CommitsFunctorHistory=>false;
    public string SourceEvidence=>";standard-wrapper;-before-enabled-byte"+
        $"factory{Pass.Factory.Index}/pass{SourcePassIndex};module="+SourceSha256+
        ";actual-initialized-frame="+ContextOwnerIdentity;

    public static OriginalMapPostEffect3MiddleExecutionRecipe Prepare(
        OriginalMapPostEffect3OwnedFreshCameraFrame actualFrame,int passIndex)
    {
        ArgumentNullException.ThrowIfNull(actualFrame);actualFrame.RequireCurrent();
        if(passIndex is <8 or >19)throw new ArgumentOutOfRangeException(nameof(passIndex));
        var init=actualFrame.RequirePreparedFactories();
        var pass=OriginalMapPostEffect3PassStateReader.Read(actualFrame.Allocation.Source)[passIndex];
        int expectedFactory=passIndex<15?2:3;
        if(pass.Factory.Index!=expectedFactory||pass.Factory.BeforeHook!=OriginalMapPostEffect3Hook.BeforeMiddle||
            pass.Factory.ExecuteHook!=OriginalMapPostEffect3Hook.AlwaysTrue||pass.Factory.AfterHook!=OriginalMapPostEffect3Hook.AlwaysTrue||
            pass.AutomaticExecution!=OriginalMapPostEffect3AutomaticExecution.ComputeDispatch||
            pass.Factory.PassWrapper!=OriginalMapPostEffect3PassWrapper.Standard)
            throw new NotSupportedException("Selected middle pass differs from the original shared Before/automatic CS protocol.");
        bool enabled=expectedFactory==2?init.DepthOfField.BeforeHookReturn:init.MotionBlur.BeforeHookAllowsPasses;
        IReadOnlyList<OriginalMapPostEffect3ParameterConstantBuffer> buffers=expectedFactory==2?
            init.DepthOfField.ConstantBuffer is{} cb?Array.AsReadOnly(new[]{cb}):Array.Empty<OriginalMapPostEffect3ParameterConstantBuffer>():
            init.MotionBlur.ConstantBuffers;
        var bindings=enabled?OriginalMapPostEffect3ParameterConstantBuffers.PreparePassBindings(
            actualFrame.Allocation,passIndex,actualFrame.FrameNumber,buffers):
            Array.Empty<OriginalMapPostEffect3ParameterConstantBufferBinding>();
        return new(actualFrame,init,pass,enabled,buffers,bindings);
    }

    public void RequireCurrent()
    {
        frame.RequireCurrent();
        if(!ReferenceEquals(frame.RequirePreparedFactories(),initialization))
            throw new InvalidDataException("Middle execution recipe belongs to another factory initialization.");
    }
}
