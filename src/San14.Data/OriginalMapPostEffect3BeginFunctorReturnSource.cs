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
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Functor0 return executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Functor0 return decoded text");
        void Check(int rva,int count,string hash)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3BeginFunctorReturnSource.cs",rva,count,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,count),actual.Sha,"Functor0 return source window");
        }
        Check(0x1161C00,0x140,"7B5ED06ABB60CB3CC3119BCBADD4313FF8BF8C40E7767A10EC10B4D9F4C911D4");
        Check(0x11466F0,0x191,"85310F484A3DFDC7EADC6D730715807AFEC4D515F4CECC96561343B647348784");
        Check(0x1164140,0x16F,"42AAAE035916B91AF03ADF1C3A99CB1596F9A566E6466C864D7C12E40803538E");
        Check(0xBF9280,3,"E18AACCE29AFFCDDDAA5F641AC1EC12552E0FE9D066E6408941B1792818B8619");
        return new(native,native.ResourceUri+"@"+native.ExecutableSha256+
            ";TC:1161C00-full-context-save/CPU1BF0;all-branches-join1161D33-literalAL1;"+
            "1146823-call/1146828-test;1164140-nonempty45-Functor0;1164278-AL1/"+
            "1164285-source-finish-v28=BF9280-B001C3;11642A0-return-final-AL;"+
            "1146845-preserveSIL/1146848-frontendEnd/1146851-pairflip/1146860-returnSIL");
    }
}
