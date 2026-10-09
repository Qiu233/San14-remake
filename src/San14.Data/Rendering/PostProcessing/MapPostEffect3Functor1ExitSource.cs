namespace San14.Data;

/// <summary>Pinned native Main/Functor1 lifecycle. All returned receipts are
/// CPU source protocol only; none grants GPU or presentation completion.</summary>
public sealed class LoadedMapPostEffect3Functor1ExitSource
{
    internal LoadedMapPostEffect3Functor1ExitSource(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public bool ProvesGpuCompletion=>false;
    public MapPostEffect3Functor1SourceReceipt CaptureFrontendBegin(MapPostEffect3Functor1ModuleInvocation invocation)
        =>invocation.CaptureCandidate(this,MapPostEffect3Functor1SourceOperation.FrontendBeginCandidate,null);
    public MapPostEffect3Functor1SourceReceipt CaptureFinishCandidate(MapPostEffect3Functor1SourceReceipt directory)
        =>directory.Invocation.CaptureCandidate(this,MapPostEffect3Functor1SourceOperation.FinishCandidate,directory);
    public MapPostEffect3Functor1SourceReceipt CaptureFrontendEnd(MapPostEffect3Functor1SourceReceipt finish)
        =>finish.Invocation.CaptureCandidate(this,MapPostEffect3Functor1SourceOperation.FrontendEndCandidate,finish);
    public MapPostEffect3Functor1SourceReceipt CaptureOuterRestoration(MapPostEffect3Functor1SourceReceipt flip)
        =>flip.Invocation.CaptureCandidate(this,MapPostEffect3Functor1SourceOperation.OuterRestorationCandidate,flip);
    public MapPostEffect3Functor1SourceReceipt CaptureControllerCleanup(MapPostEffect3Functor1SourceReceipt returned)
        =>returned.Invocation.CaptureCandidate(this,MapPostEffect3Functor1SourceOperation.ControllerCleanupCandidate,returned);
}

public static class MapPostEffect3Functor1ExitSource
{
    public static LoadedMapPostEffect3Functor1ExitSource Load(GameResourceRepository repository,string locale="TC")
        =>RenderSamplerSourceAdapters.ReadPostEffect3Functor1ExitSource(
            RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3Functor1ExitSource ReadPostEffect3Functor1ExitSource(
        LoadedDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Functor1 exit source requires a verified SC/TC profile.");

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;"+";render-rules="+native.RenderRuleIdentity+
            ";whole72-before-Finish6/7/8/9;-count10;-End/-pairs/-outerRestore"+
            "-bit0/-buffer4-cleanup/-A1;CPU-source-protocol-only",native.Locale);
    }
}
