namespace San14.Data;

/// <summary>Factory2/3 use the shared native enabled-byte Before hook, then
/// the standard automatic CS wrapper. The actual initialized frame supplies
/// the predicate and complete constant buffers; no effect setting is inferred
/// from a pass name or an allocated image.</summary>
public sealed class MapPostEffect3MiddleExecutionRecipe
{
    private readonly MapPostEffect3OwnedFreshCameraFrame frame;
    private readonly MapPostEffect3OwnedFactoryInitialization initialization;
    private MapPostEffect3MiddleExecutionRecipe(
        MapPostEffect3OwnedFreshCameraFrame frame,
        MapPostEffect3OwnedFactoryInitialization initialization,
        MapPostEffect3PassState pass,bool enabled,
        IReadOnlyList<MapPostEffect3ParameterConstantBuffer> buffers,
        IReadOnlyList<MapPostEffect3ParameterConstantBufferBinding> bindings)
    {
        this.frame=frame;this.initialization=initialization;Pass=pass;
        ExecutesPass=enabled;ConstantBuffers=buffers;ConstantBufferBindings=bindings;
    }
    public long FrameNumber=>frame.FrameNumber;
    public string ContextOwnerIdentity=>frame.OwnerIdentity;
    public string SourceSha256=>frame.Allocation.Source.Sha256;
    public int SourcePassIndex=>Pass.Pass.Index;
    public MapPostEffect3PassState Pass{get;}
    public bool ExecutesPass{get;}
    public IReadOnlyList<MapPostEffect3ParameterConstantBuffer> ConstantBuffers{get;}
    public IReadOnlyList<MapPostEffect3ParameterConstantBufferBinding> ConstantBufferBindings{get;}
    public bool CommitsFunctorHistory=>false;
    public string SourceEvidence=>";standard-wrapper;-before-enabled-byte"+
        $"factory{Pass.Factory.Index}/pass{SourcePassIndex};module="+SourceSha256+
        ";actual-initialized-frame="+ContextOwnerIdentity;

    public static MapPostEffect3MiddleExecutionRecipe Prepare(
        MapPostEffect3OwnedFreshCameraFrame actualFrame,int passIndex)
    {
        ArgumentNullException.ThrowIfNull(actualFrame);actualFrame.RequireCurrent();
        if(passIndex is <8 or >19)throw new ArgumentOutOfRangeException(nameof(passIndex));
        var init=actualFrame.RequirePreparedFactories();
        var pass=MapPostEffect3PassStateReader.Read(actualFrame.Allocation.Source)[passIndex];
        int expectedFactory=passIndex<15?2:3;
        if(pass.Factory.Index!=expectedFactory||pass.Factory.BeforeHook!=MapPostEffect3Hook.BeforeMiddle||
            pass.Factory.ExecuteHook!=MapPostEffect3Hook.AlwaysTrue||pass.Factory.AfterHook!=MapPostEffect3Hook.AlwaysTrue||
            pass.AutomaticExecution!=MapPostEffect3AutomaticExecution.ComputeDispatch||
            pass.Factory.PassWrapper!=MapPostEffect3PassWrapper.Standard)
            throw new NotSupportedException("Selected middle pass differs from the original shared Before/automatic CS protocol.");
        bool enabled=expectedFactory==2?init.DepthOfField.BeforeHookReturn:init.MotionBlur.BeforeHookAllowsPasses;
        IReadOnlyList<MapPostEffect3ParameterConstantBuffer> buffers=expectedFactory==2?
            init.DepthOfField.ConstantBuffer is{} cb?Array.AsReadOnly(new[]{cb}):Array.Empty<MapPostEffect3ParameterConstantBuffer>():
            init.MotionBlur.ConstantBuffers;
        var bindings=enabled?MapPostEffect3ParameterConstantBuffers.PreparePassBindings(
            actualFrame.Allocation,passIndex,actualFrame.FrameNumber,buffers):
            Array.Empty<MapPostEffect3ParameterConstantBufferBinding>();
        return new(actualFrame,init,pass,enabled,buffers,bindings);
    }

    public void RequireCurrent()
    {
        frame.RequireCurrent();
        if(!ReferenceEquals(frame.RequirePreparedFactories(),initialization))
            throw new InvalidDataException("Middle execution recipe belongs to another factory initialization.");
    }
}
