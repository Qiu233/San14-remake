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
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Functor1 exit executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Functor1 exit decoded text");
        void Check(int rva,int length,string sha)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3Functor1ExitSource.cs",rva,length,sha);
            VerifyHash(text.AsSpan(actual.Rva-start,length),actual.Sha,"Functor1 exit source");
        }
        Check(0x11466F0,401,"85310F484A3DFDC7EADC6D730715807AFEC4D515F4CECC96561343B647348784");
        Check(0x1161C00,320,"7B5ED06ABB60CB3CC3119BCBADD4313FF8BF8C40E7767A10EC10B4D9F4C911D4");
        Check(0x1164140,367,"42AAAE035916B91AF03ADF1C3A99CB1596F9A566E6466C864D7C12E40803538E");
        Check(0x1155280,15,"E5CFE291762B06858BD508B730799BAD87F2F4EF3CEC2DFD138C24498A1A6528");
        Check(0xBF9280,3,"E18AACCE29AFFCDDDAA5F641AC1EC12552E0FE9D066E6408941B1792818B8619");
        Check(0x1162D70,778,"89CFF5F56C21626863F66154DEAA52AB7C78260203B62CEB9FED7F09263E03D5");
        Check(0x114AB40,170,"A14D90049C410CB301A3457F4169C8C7CBF2EAA1E98DF120366EF7FF717213E6");
        Check(0x11462D0,852,"F54BDF419F593973C7EFDF9A21E1BF0BA6FE39D5228D102A827D7DB3953A0CA3");
        Check(0xE22860,1107,"725BF0BA2FDECED538B0BCCBFCE380097FCD760EBB0F330DD17CE42FD36C0C11");
        foreach(var factory in OriginalMapPostEffect3PassStateReader.FactoriesForLocale(native.Locale).Skip(6))
            if(System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(image.Raw(checked((int)factory.Vtable+0x28),8))!=
                0x140000000UL+factory.FinishHook)
                throw new InvalidDataException("Functor1 native Finish vtable join changed.");
        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+"@"+native.ExecutableSha256+
            ";TC:1164140-whole72-before-Finish6/7/8/9;1155280-count10;1146848-End/1146851-pairs/1146856-outerRestore;"+
            "E22C22-bit0/E22C29-buffer4-cleanup/E22C8E-A1;CPU-source-protocol-only",native.Locale);
    }
}
