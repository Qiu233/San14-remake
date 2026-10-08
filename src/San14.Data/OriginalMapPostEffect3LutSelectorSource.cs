namespace San14.Data;

/// <summary>The exact native context50.v88 source of the owned Main Q.
/// This is not a sample of the original process's active render context.</summary>
public sealed class LoadedOriginalMapPostEffect3LutSelectorSource
{
    internal LoadedOriginalMapPostEffect3LutSelectorSource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)
        =>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3OwnedMainLutSelector ResolveOwnedMain(OriginalMainMapContextCurrentPacket actualCurrentContext)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentContext);
        actualCurrentContext.Source.Validate(actualCurrentContext);
        var host=actualCurrentContext.Source.Host;
        if(actualCurrentContext.FrameNumber<=0||host.Locale!=Native.Locale||
            host.ExecutableSha256!=Native.ExecutableSha256||host.ContextOwnerVtableRva!=(uint)
                OriginalMapPostEffect3NativeSourceProfile.DataRva(Native.Locale,"OriginalMapPostEffect3LutSelectorSource.cs",0x13C4ED0))
            throw new NotSupportedException("LUT selector requires the actual same-locale source-pinned Main Q/current packet.");
        return new(this,actualCurrentContext);
    }
}

/// <summary>BF8B50 returns zero because this exact Q.v80 is C00170/literalfalse.
/// No external selector value, locale selection or first declaration is used.</summary>
public sealed class OriginalMapPostEffect3OwnedMainLutSelector
{
    private readonly OriginalMainMapContextCurrentPacket current;
    internal OriginalMapPostEffect3OwnedMainLutSelector(LoadedOriginalMapPostEffect3LutSelectorSource native,
        OriginalMainMapContextCurrentPacket current)=>(Native,this.current)=(native,current);
    public LoadedOriginalMapPostEffect3LutSelectorSource Native{get;}
    public uint Selector=>0;
    public long FrameNumber=>current.FrameNumber;
    public string ContextOwnerIdentity=>current.Source.OwnedHostIdentity;
    public bool ProvesOriginalActiveContext=>false;
    public bool ProvesGpuTextureReady=>false;
    public string SourceEvidence=>Native.SourceEvidence+";"+current.SourceProvenance;
    public void ValidateFor(OriginalMainMapContextCurrentPacket actualCurrentContext)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentContext);current.Source.Validate(actualCurrentContext);
        if(!ReferenceEquals(current,actualCurrentContext))
            throw new InvalidDataException("LUT selector belongs to another actual Main current packet.");
        _=Native.ResolveOwnedMain(actualCurrentContext);
    }
}

public static class OriginalMapPostEffect3LutSelectorSource
{
    public static LoadedOriginalMapPostEffect3LutSelectorSource Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadPostEffect3LutSelectorSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3LutSelectorSource ReadPostEffect3LutSelectorSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        const string sourceScope="OriginalMapPostEffect3LutSelectorSource.cs";
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"LUT selector executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"LUT selector decoded text");
        void Check(int rva,int count,string sha,string role)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,sourceScope,rva,count,sha);
            VerifyHash(text.AsSpan(actual.Rva-start,count),actual.Sha,role);
        }
        Check(0xBF8B50,0x39,"77D9A45D266B3C771291426991ED02F99B9AD05D333D07690D51A4734FDA4E56","Q.v88 LUT selector body");
        Check(0xC00170,3,"01CB47D078B4841B8408EC4FE278EFA83115C6F6E101972987507D2B2B57DCF0","Q.v80 literalfalse");
        int vtable=OriginalMapPostEffect3NativeSourceProfile.DataRva(native.Locale,sourceScope,0x13C4ED0);
        int falseLeaf=OriginalMapPostEffect3NativeSourceProfile.CodeRva(native.Locale,sourceScope,0xC00170);
        int selector=OriginalMapPostEffect3NativeSourceProfile.CodeRva(native.Locale,sourceScope,0xBF8B50);
        if(U64(image.Raw(vtable+0x80,8),0)!=0x140000000UL+(uint)falseLeaf||
            U64(image.Raw(vtable+0x88,8),0)!=0x140000000UL+(uint)selector)
            throw new InvalidDataException("Native Main Q v80/v88 linkage differs from the original selector source.");
        return new(native,native.ResourceUri+"@"+native.ExecutableSha256+
            $";{native.Locale}:QVT{vtable:X8}.v88={selector:X8}/v80={falseLeaf:X8}-literalALfalse;TC-reference-role:BF8B81-EAXzero;"+
            "E8F49B-context50/E23937-v88;owned-Main-current-context-only;not-original-active-Q-observation");
    }
}
