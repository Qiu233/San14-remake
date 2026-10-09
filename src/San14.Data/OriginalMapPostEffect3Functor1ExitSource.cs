namespace San14.Data;

/// <summary>Pinned native Main/Functor1 lifecycle. All returned receipts are
/// CPU source protocol only; none grants GPU or presentation completion.</summary>
public sealed class LoadedOriginalMapPostEffect3Functor1ExitSource
{
    internal LoadedOriginalMapPostEffect3Functor1ExitSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public bool ProvesGpuCompletion=>false;
    public OriginalMapPostEffect3Functor1SourceReceipt CaptureFrontendBegin(OriginalMapPostEffect3Functor1ModuleInvocation invocation)
        =>invocation.CaptureCandidate(this,OriginalMapPostEffect3Functor1SourceOperation.FrontendBeginCandidate,null);
    public OriginalMapPostEffect3Functor1SourceReceipt CaptureFinishCandidate(OriginalMapPostEffect3Functor1SourceReceipt directory)
        =>directory.Invocation.CaptureCandidate(this,OriginalMapPostEffect3Functor1SourceOperation.FinishCandidate,directory);
    public OriginalMapPostEffect3Functor1SourceReceipt CaptureFrontendEnd(OriginalMapPostEffect3Functor1SourceReceipt finish)
        =>finish.Invocation.CaptureCandidate(this,OriginalMapPostEffect3Functor1SourceOperation.FrontendEndCandidate,finish);
    public OriginalMapPostEffect3Functor1SourceReceipt CaptureOuterRestoration(OriginalMapPostEffect3Functor1SourceReceipt flip)
        =>flip.Invocation.CaptureCandidate(this,OriginalMapPostEffect3Functor1SourceOperation.OuterRestorationCandidate,flip);
    public OriginalMapPostEffect3Functor1SourceReceipt CaptureControllerCleanup(OriginalMapPostEffect3Functor1SourceReceipt returned)
        =>returned.Invocation.CaptureCandidate(this,OriginalMapPostEffect3Functor1SourceOperation.ControllerCleanupCandidate,returned);
}

public static class OriginalMapPostEffect3Functor1ExitSource
{
    public static LoadedOriginalMapPostEffect3Functor1ExitSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadPostEffect3Functor1ExitSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3Functor1ExitSource ReadPostEffect3Functor1ExitSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Functor1 exit source requires a verified SC/TC profile.");

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+";render-rules="+native.RenderRuleIdentity+
            ";TC:1164140-whole72-before-Finish6/7/8/9;1155280-count10;1146848-End/1146851-pairs/1146856-outerRestore;"+
            "E22C22-bit0/E22C29-buffer4-cleanup/E22C8E-A1;CPU-source-protocol-only",native.Locale);
    }
}
