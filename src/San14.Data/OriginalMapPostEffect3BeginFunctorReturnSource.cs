namespace San14.Data;

/// <summary>Exact original frontend Begin and nonempty Functor0 return
/// branches. The frontend helper always returns AL1 after its context writes;
/// the complete module call returns the final literal-true Finish result.
/// These source results are not GPU execution or presentation completion.</summary>
public sealed class LoadedOriginalMapPostEffect3BeginFunctorReturnSource
{
    internal LoadedOriginalMapPostEffect3BeginFunctorReturnSource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)=>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string Locale=>Native.Locale;
    public string SourceEvidence { get; }
    public OriginalMapPostEffect3FrontendBeginSourceResult CaptureCurrentFrontendBegin(
        OriginalMapPostEffect3RetainedModuleInvocation invocation)
    {
        ArgumentNullException.ThrowIfNull(invocation);invocation.RequireCurrent();
        if(invocation.Begin.SelectedFunctorIndex!=0||invocation.Begin.ModuleSha256!=OriginalMapFinalOutputPostEffect3Source.PayloadSha256)
            throw new NotSupportedException("Literal frontend Begin receipt requires this exact owned Functor0 invocation.");
        return new(invocation.Begin,SourceEvidence);
    }
    public void ValidateCompletedFunctor0(OriginalMapPostEffect3RetainedCpuExit exit)
    {
        ArgumentNullException.ThrowIfNull(exit);exit.RequireCurrent();
        if(!exit.AppliedSourceHistoryFlip)
            throw new InvalidDataException("Source module AL1 requires its successful nonempty Functor0 exit.");
    }
}

public sealed class OriginalMapPostEffect3FrontendBeginSourceResult
{
    internal OriginalMapPostEffect3FrontendBeginSourceResult(OriginalMapPostEffect3BeginFunctorCounterReceipt begin,string evidence)
        =>(Begin,SourceEvidence)=(begin,evidence);
    public OriginalMapPostEffect3BeginFunctorCounterReceipt Begin { get; }
    public string SourceEvidence { get; }
    public bool SourceLiteralReturn=>true;
    public bool ProvesGpuExecution=>false;
    internal void ValidateFor(OriginalMapPostEffect3RetainedModuleInvocation invocation)
    {
        invocation.RequireCurrent();Begin.RequireCurrent();
        if(!ReferenceEquals(invocation.Begin,Begin))
            throw new InvalidDataException("Frontend Begin return belongs to another module invocation.");
    }
}

public static class OriginalMapPostEffect3BeginFunctorReturnSource
{
    public static LoadedOriginalMapPostEffect3BeginFunctorReturnSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadPostEffect3BeginFunctorReturnSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3BeginFunctorReturnSource ReadPostEffect3BeginFunctorReturnSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {

        return new(native,native.RenderRuleIdentity+
            ";full-context-save/CPU1BF0;all-branches-join-literalAL1"+
            "-call/-test;-nonempty45-Functor0;-AL1"+
            "-source-finish-v28=-;-return-final-AL"+
            "-preserveSIL/-frontendEnd/-pairflip/-returnSIL");
    }
}
