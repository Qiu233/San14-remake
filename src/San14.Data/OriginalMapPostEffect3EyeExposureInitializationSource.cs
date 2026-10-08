using System.Security.Cryptography;

namespace San14.Data;

/// <summary>The original fresh owned61 creator supplies no initial pixels.
/// This grants the exact first CS52 texel0 load of unspecified device contents;
/// it does not grant readable initialized history or a known initial value.</summary>
public sealed class OriginalMapPostEffect3FreshUndefinedEyeExposureSource
{
    private readonly OriginalMapPostEffect3EyeComputeWriteCoverage coverage;
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private readonly OriginalMapPostEffect3ResolvedTextureParameter pointer;
    private readonly OriginalMapPostEffect3BeginFunctorCounterReceipt begin;
    internal OriginalMapPostEffect3FreshUndefinedEyeExposureSource(
        LoadedOriginalMapPostEffect3EyeExposureInitializationSource source,
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3EyeComputeWriteCoverage coverage,
        OriginalMapPostEffect3TextureParameterState table,OriginalMapPostEffect3BeginFunctorCounterReceipt begin,
        OriginalMapPostEffect3ApplyViewSlotSource view)
    {
        Source=source;Allocation=allocation;this.coverage=coverage;this.table=table;
        this.begin=begin;ActualApplyView=view;pointer=table.Require(61);
    }
    public LoadedOriginalMapPostEffect3EyeExposureInitializationSource Source{get;}
    public OriginalMapPostEffect3AllocationPlan Allocation{get;}
    public OriginalMapPostEffect3ApplyViewSlotSource ActualApplyView{get;}
    public OriginalMapPostEffect3BeginFunctorCounterReceipt ActualBegin=>begin;
    public long FrameNumber=>coverage.FrameNumber;
    public string ContextOwnerIdentity=>coverage.ContextOwnerIdentity;
    public string SourceSha256=>coverage.SourceSha256;
    public string ProgramSha256=>coverage.ProgramSha256;
    public string ConstantBufferBytesSha256=>coverage.RequiredConstantBufferBytesSha256[24];
    public int ParameterIndex=>61;
    public int InstanceIndex=>0;
    public uint NativeViewFormat=>41;
    public bool PriorTexelLoadExecutes=>true;
    public bool PriorPixelsHaveKnownContents=>false;
    public bool EstablishesCompleteResourceContents=>false;
    public bool EstablishesGpuCompletion=>false;
    public OriginalMapPostEffect3EyeComputeWriteCoverage WriteCoverage=>coverage;
    public string SourceEvidence=>Source.SourceEvidence+";"+begin.SourceEvidence+";"+ActualApplyView.SourceEvidence+
        ";fresh-owned61-original-initialData-null;actual-first-source104-positive-adaptation-load-texel0;unspecified-prior/no-ready";

    public void RequireCurrent()
    {
        Source.ValidateFor(Allocation);coverage.RequireCurrent();begin.RequireCurrent();
        table.ValidateCurrent(pointer);
        if(!ReferenceEquals(table.Allocation,Allocation)||table.FrameNumber!=FrameNumber||
            table.ContextOwnerIdentity!=ContextOwnerIdentity||table.SourceSha256!=SourceSha256||
            begin.Counter!=1||begin.SelectedFunctorIndex!=1||begin.ModuleOwnerIdentity!=ContextOwnerIdentity||
            begin.ModuleSha256!=SourceSha256||ActualApplyView.Index!=0||ActualApplyView.FrameNumber!=FrameNumber||
            coverage.SourcePassIndex!=104||coverage.ProgramSha256!=LoadedOriginalMapPostEffect3EyeExposureInitializationSource.Cs52Sha256||
            !coverage.PriorExposureTexelLoadExecutes||!coverage.RequiresPriorExposureTexelContents||
            coverage.AllowsFreshUninitializedExposureStorage||coverage.ParameterIndex!=61||
            coverage.TextureMipLevel!=0||coverage.TextureWidth!=2||coverage.TextureHeight!=1||
            coverage.ActualDispatch!=new OriginalMapPostEffect3DispatchExtent(1,1,1)||
            coverage.LocalSize!=new OriginalMapPostEffect3DispatchExtent(1,1,1)||
            pointer.SlotRevision!=0||pointer.Reference.PhysicalAllocationParameterIndex!=61||
            pointer.Reference.InstanceIndex!=0||pointer.Reference.HasSourceSelectedMipView||
            pointer.Reference.ProducerOwnerIdentity!=ContextOwnerIdentity+"/texture61/side0")
            throw new InvalidDataException("Undefined exposure load is not the exact first Main View0 owned61 source invocation.");
    }
    public void RequireCurrentTextureTable(OriginalMapPostEffect3TextureParameterState actualTable)
    {
        if(!ReferenceEquals(actualTable,table))
            throw new InvalidDataException("Undefined exposure source needs its exact frozen native pointer table.");
        RequireCurrent();
    }
}

public sealed class LoadedOriginalMapPostEffect3EyeExposureInitializationSource
{
    internal const string Cs52Sha256="E2F1509897906E4C3C753EF5C8AF470AE3E0CD4666B82751B3D8529EFB276FE1";
    internal LoadedOriginalMapPostEffect3EyeExposureInitializationSource(LoadedOriginalDeferredSamplerNativeSource native,string evidence)=>(Native,SourceEvidence)=(native,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string Locale=>Native.Locale;
    public string SourceEvidence{get;}
    public bool NativeInitialDataIsNull=>true;
    public bool NativeClearsExposurePixels=>false;
    public bool InitialPixelValuesAreDefined=>false;
    public void ValidateFor(OriginalMapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        var a=allocation.TextureAllocations.Single(t=>t.Parameter.Index==61);
        var shape=OriginalMapPostEffect3AllocationShapeReader.Read(a);
        if(allocation.ResizeOnly||!a.CreateInThisInvocation||a.InstanceCount!=1||
            a.Parameter.Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration||
            !a.Parameter.RawBytes.Span.SequenceEqual(Convert.FromHexString(
                "FFFFFFFFD16C16AD00000700020000000100000001000000020000000100000000000000"))||
            shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||shape.Width!=2||shape.Height!=1||
            shape.Depth!=1||shape.MipLevels!=1||!shape.AllowsUnorderedAccess||!shape.HasRenderTargetPath||
            shape.Formats.ResourceFormat!=39||shape.Formats.ShaderResourceFormat0!=41||
            shape.Formats.RenderTargetFormat0!=41||shape.Formats.UnorderedAccessFormat!=41)
            throw new InvalidDataException("Undefined exposure creation needs exact source-owned61 single-side fixed2x1 R32_FLOAT.");
        int[] writers=allocation.Passes.Where(p=>p.ActiveBindings.Any(b=>
            b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&b.ParameterIndex==61&&
            b.Role is OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite or OriginalMapPostEffect3BindingRole.RenderTargetWrite))
            .Select(p=>p.Pass.Index).ToArray();
        if(!writers.SequenceEqual(new[]{104}))
            throw new InvalidDataException("Source exposure61 has another directory pixel writer.");
        _=OriginalMapPostEffect3Functor1FootprintSource.RequireProgram(allocation,104,5,52,Cs52Sha256);
    }
    public OriginalMapPostEffect3FreshUndefinedEyeExposureSource Prepare(
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3EyeComputeWriteCoverage coverage,
        OriginalMapPostEffect3TextureParameterState actualTable,OriginalMapPostEffect3BeginFunctorCounterReceipt actualBegin,
        OriginalMapPostEffect3ApplyViewSlotSource actualApplyView)
    {
        ArgumentNullException.ThrowIfNull(coverage);ArgumentNullException.ThrowIfNull(actualTable);
        ArgumentNullException.ThrowIfNull(actualBegin);ArgumentNullException.ThrowIfNull(actualApplyView);
        ValidateFor(allocation);
        var result=new OriginalMapPostEffect3FreshUndefinedEyeExposureSource(this,allocation,coverage,actualTable,actualBegin,actualApplyView);
        result.RequireCurrent();return result;
    }
}

public static class OriginalMapPostEffect3EyeExposureInitializationSource
{
    public static LoadedOriginalMapPostEffect3EyeExposureInitializationSource Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadEyeExposureInitializationSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}
public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3EyeExposureInitializationSource ReadEyeExposureInitializationSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Exposure initial-data executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);VerifyHash(text,native.DecodedTextSha256,"Exposure initial-data decoded text");
        void Pin(int rva,int count,string hash)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3EyeExposureInitializationSource.cs",rva,count,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,count),actual.Sha,"Exposure initial-data source window");
        }
        Pin(0x1146921,448,"9AD98FC1AAEE1C3A7DAEBBAD7A770C2633F5DA4DCA0BD102DF1CC448B5CD884F");
        Pin(0x1147D50,196,"69B4B79ECD2577C0970A8370A7A0BFD280B148C6C2DD5F557337471387F8FC6F");
        Pin(0x94F420,89,"D8438280AF1E23AFAD3240AF9159AE92D1822602B95082CF84EBDF84B0F20A58");
        Pin(0x94EA60,2494,"0DBE261643DECC6977C59E64DA1B31089D3BC9F47260B8541AE4AE50D5EC8A86");
        Pin(0x94C9D0,231,"FD44953F04C6AC33D365D462F3A8F2C511980522FEF78CF9ECDDFBD3FFBFB644");
        Pin(0x94C6E0,285,"9498A46D28D2BF5F0B1D1BFCCBB7CAD802756472703767E26A8CB45B68CD744E");
        Pin(0x9539B0,24,"523C6DBEBB49AB4ED0F1C6CA1EE17E62334D7988F8B777264E9989D07E3D50B4");
        Pin(0x96AFC0,302,"2605FA9EBE7FD31645C180D923FCA51DAB5FC9C4CADFCCB8C0438E87D5B0F456");
        Pin(0x95D440,228,"2A29EDE1A80D69C222AE8E685C403B5EF15ABCE06E0087E2FE033A48D9558DB7");
        Pin(0x96B250,131,"1C6EE59E4315A5D205590EF23AE184DC5C2894AC9AE8AA3B7E1989219EFB179B");
        Pin(0x11583E0,2495,"1A0B599569F1C1956E2E8B1477DEB9C8C9D8B3FC2B00A1C92B56C10D4E65C863");
        Pin(0x1158160,630,"46BDCBE8EA025C2B10FC0321B35D32990782AC23B3D2440854748CE283C9DBA5");
        Pin(0x1158040,285,"D599B07D01DCBA0AD1D3DB4EACD9CFC663BC286CDEF5DB51768A497369524169");
        Pin(0x11592A0,77,"0A4C930DF8FA80E75594ED6A9F5D242C677E4AA6F635001BBABA5C638A22BD34");
        return new(native,native.ResourceUri+";TC:1146926-owned61-flags7;1147D50-v20→94F420→94EA60→94C9D0;"+
            "94CA78-initialData=null→94C6E0→9539B0-device.v28;CPU-owner/view-header-clears-only;"+
            "factory8-v38=1158160-bloom85/86-mipviews-and69-alias/no61;source104-sole61-directory-writer;"+
            "first-CS52-positive-adaptation-reads-source-unspecified-u0(0,0);no-invented-clear/copy/initial-pixels");
    }
}
