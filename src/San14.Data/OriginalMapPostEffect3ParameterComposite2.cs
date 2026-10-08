using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>The current native wrapper+40/+42 fields. External extents must
/// come from the actual selected wrapper; dimensions alone select no asset.</summary>
public sealed record OriginalMapPostEffect3Composite2ExternalExtent(
    OriginalMapPostEffect3ResolvedTextureParameter ActualTexture,ushort Width40,ushort Height42,string SourceEvidence);

public sealed class OriginalMapPostEffect3Composite2Inputs
{
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private readonly OriginalMapPostEffect3ParameterState capturedState;
    private readonly OriginalMapPostEffect3ResolvedTextureParameter? scene,bloom;
    private readonly int sceneIndex,bloomIndex;
    private readonly long? sceneNullRevision,bloomNullRevision;
    private OriginalMapPostEffect3Composite2ExecutionFrame? actualInitialization;
    internal OriginalMapPostEffect3Composite2Inputs(OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3TextureParameterState table,
        int si,int bi,OriginalMapPostEffect3ResolvedTextureParameter? scene,OriginalMapPostEffect3ResolvedTextureParameter? bloom,
        ushort sw,ushort sh,ushort bw,ushort bh,uint contextWord,string evidence)
    {
        capturedState=state;this.table=table;sceneIndex=si;bloomIndex=bi;this.scene=scene;this.bloom=bloom;
        sceneNullRevision=scene is null?table.CaptureKnownNullRevision(si):null;
        bloomNullRevision=bloom is null?table.CaptureKnownNullRevision(bi):null;
        SceneWidth40=sw;SceneHeight42=sh;BloomWidth40=bw;BloomHeight42=bh;
        ActualContext1C48Word=contextWord;SourceEvidence=evidence;
    }
    public long FrameNumber=>table.FrameNumber;
    public string ContextOwnerIdentity=>table.ContextOwnerIdentity;
    public bool SceneWrapperPresent=>scene is not null;
    public bool BloomWrapperPresent=>bloom is not null;
    public ushort SceneWidth40{get;}
    public ushort SceneHeight42{get;}
    public ushort BloomWidth40{get;}
    public ushort BloomHeight42{get;}
    public uint ActualContext1C48Word{get;}
    public string SourceEvidence{get;}
    internal OriginalMapPostEffect3TextureParameterState CurrentTextureTable=>table;
    internal void AcceptActualInitialization(OriginalMapPostEffect3ParameterState state,
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3Composite2ExecutionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if(actualInitialization is not null||sceneIndex!=79||!ReferenceEquals(capturedState,state)||
            !ReferenceEquals(table.Allocation,allocation)||!ReferenceEquals(frame.State,state)||
            !ReferenceEquals(frame.Allocation,allocation)||!ReferenceEquals(frame.CurrentTextureTable,table)||
            frame.FrameNumber!=FrameNumber||frame.OwnerIdentity!=ContextOwnerIdentity||!frame.HasAppliedInitialization)
            throw new InvalidDataException("Composite2 current scene migration requires its actual completed same-frame Init.");
        // Validate source-preserved Bloom independently. Scene79 changed
        // legally in Init; its pre-Init snapshot still describes CB22's sizes.
        if(bloom is null)table.ValidateKnownNullRevision(bloomIndex,bloomNullRevision!.Value);else table.ValidateCurrent(bloom);
        _=frame.ValidateInitializationOutput79(table);
        actualInitialization=frame;
    }
    internal void RequireCurrent(OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation)
    {
        if(!ReferenceEquals(capturedState,state)||!ReferenceEquals(table.Allocation,allocation)||FrameNumber!=state.FrameNumber||
            ContextOwnerIdentity!=state.OwnerIdentity||table.SourceSha256!=state.SourceSha256)
            throw new InvalidDataException("Composite2 current native wrapper/context belongs to another invocation.");
        if(actualInitialization is not null)_=actualInitialization.ValidateInitializationOutput79(table);
        else if(scene is null)table.ValidateKnownNullRevision(sceneIndex,sceneNullRevision!.Value);else table.ValidateCurrent(scene);
        if(bloom is null)table.ValidateKnownNullRevision(bloomIndex,bloomNullRevision!.Value);else table.ValidateCurrent(bloom);
    }
}

/// <summary>Source115CF80's mask gates directory entries. This is not a claim
/// that the other 72 Functor1 directory passes execute unconditionally.</summary>
public sealed record OriginalMapPostEffect3Composite2PassSelection(uint NativeMask64,
    IReadOnlyList<int> OrderedFactory9Passes,int FinalCompositePass,int? FxaaPass)
{
    public bool RequiresHdrLutRemake=>FinalCompositePass==107;
    public int LastSelectedPass=>FxaaPass??FinalCompositePass;
}

public sealed class OriginalMapPostEffect3ParameterComposite2Update
{
    private readonly OriginalMapPostEffect3ParameterState state;
    private readonly OriginalMapPostEffect3AllocationPlan allocation;
    private readonly OriginalMapPostEffect3Composite2Inputs inputs;
    private readonly IReadOnlyDictionary<string,byte[]> scalars;
    internal OriginalMapPostEffect3ParameterComposite2Update(OriginalMapPostEffect3ParameterState state,
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3Composite2Inputs inputs,
        IReadOnlyDictionary<string,byte[]> scalars,OriginalMapPostEffect3ParameterConstantBuffer cb,bool cropped,
        OriginalMapPostEffect3Composite2PassSelection selection,string evidence)
        =>(this.state,this.allocation,this.inputs,this.scalars,ConstantBuffer,UsesCroppedViewport200,PassSelection,SourceEvidence)=
            (state,allocation,inputs,scalars,cb,cropped,selection,evidence);
    public long FrameNumber=>state.FrameNumber;
    public string OwnerIdentity=>state.OwnerIdentity;
    public OriginalMapPostEffect3ParameterConstantBuffer ConstantBuffer{get;}
    public bool UsesCroppedViewport200{get;}
    public uint NativeCapability1F0=>5;
    public OriginalMapPostEffect3Composite2PassSelection PassSelection{get;}
    public string SourceEvidence{get;}
    public bool ProvesGpuExecution=>false;
    internal void RequireOwnedLifecycle(OriginalMapPostEffect3ParameterState actualState,
        OriginalMapPostEffect3AllocationPlan actualAllocation,OriginalMapPostEffect3TextureParameterState actualTable)
    {
        if(!ReferenceEquals(state,actualState)||!ReferenceEquals(allocation,actualAllocation)||
            !ReferenceEquals(inputs.CurrentTextureTable,actualTable))
            throw new InvalidDataException("Composite2 lifecycle belongs to another actual prepared scalar/allocation/texture bank.");
        RequireCurrent();
    }
    public void AcceptActualInitialization(OriginalMapPostEffect3Composite2ExecutionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if(!ReferenceEquals(frame.OriginalUpdate,this))
            throw new InvalidDataException("Composite2 Init belongs to another prepared constant-buffer update.");
        foreach(var s in scalars)
            if(!state.RequireValue(s.Key).Span.SequenceEqual(s.Value))
                throw new InvalidDataException("Composite2 scalar changed between original Prepare and Init: "+s.Key);
        inputs.AcceptActualInitialization(state,allocation,frame);
    }
    public void RequireCurrent()
    {
        inputs.RequireCurrent(state,allocation);
        foreach(var s in scalars)
            if(!state.RequireValue(s.Key).Span.SequenceEqual(s.Value))
                throw new InvalidDataException("Composite2 scalar changed after native Prepare: "+s.Key);
    }
}

public sealed class LoadedOriginalMapPostEffect3Composite2Source
{
    internal LoadedOriginalMapPostEffect3Composite2Source(string evidence,string locale="TC")=>(SourceEvidence,Locale)=(evidence,locale);
    public string Locale{get;}
    public string SourceEvidence{get;}

    /// <summary>Native getters resolve named slots first. Source-owned wrapper
    /// dimensions derive from the actual physical alias; unknown external
    /// wrappers need their own current extent fields. Known null is retained.</summary>
    public OriginalMapPostEffect3Composite2Inputs CaptureCurrentInputs(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3TextureParameterState table,uint actualContext1C48Word,
        IReadOnlyList<OriginalMapPostEffect3Composite2ExternalExtent> actualExternalExtents,
        OriginalMapPostEffect3ParameterAliasMode actualNativeAliasMode,string sourceEvidence)
    {
        ArgumentNullException.ThrowIfNull(table);ArgumentNullException.ThrowIfNull(actualExternalExtents);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);Validate(state,allocation);
        state.BindOwnedAllocation(allocation);
        if(!ReferenceEquals(table.Allocation,allocation)||table.FrameNumber!=state.FrameNumber)
            throw new InvalidDataException("Composite2 texture getters require this actual invocation table.");
        int si=Texture("CMP2InputColor"),bi=Texture("PRECMP2InputBloom");
        var a=Read(si);var b=Read(bi);
        return new(state,table,si,bi,a.Item1,b.Item1,a.Item2,a.Item3,b.Item2,b.Item3,actualContext1C48Word,
            SourceEvidence+";"+sourceEvidence);
        int Texture(string n)=>state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,n,actualNativeAliasMode)
            ??throw new NotSupportedException("Composite2 actual named texture is absent: "+n);
        (OriginalMapPostEffect3ResolvedTextureParameter?,ushort,ushort) Read(int index)
        {
            if(table.ReadKnownNullablePointer(index) is null)return(null,1,1);
            var resolved=table.Require(index);
            if(resolved.Reference.PhysicalAllocationParameterIndex is int physical)
            {
                var native=allocation.TextureAllocations.Single(t=>t.Parameter.Index==physical);
                return(resolved,native.Width16,native.Height16);
            }
            var extents=actualExternalExtents.Where(e=>e.ActualTexture.ParameterIndex==index).ToArray();
            if(extents.Length!=1||string.IsNullOrWhiteSpace(extents[0].SourceEvidence))
                throw new NotSupportedException("Composite2 external wrapper has no unique actual extent producer.");
            table.ValidateCurrent(extents[0].ActualTexture);
            if(extents[0].ActualTexture!=resolved)throw new InvalidDataException("Composite2 wrapper extent identity differs.");
            return(resolved,extents[0].Width40,extents[0].Height42);
        }
    }

    /// <summary>115BC10's complete208B, capability5 branch. All48 factory9
    /// default lookups must be present and written; smaller/uninitialized
    /// native capabilities cannot provide a complete CB22 packet.</summary>
    public OriginalMapPostEffect3ParameterComposite2Update Prepare(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3Composite2Inputs actualInputs)
    {
        Validate(state,allocation);ArgumentNullException.ThrowIfNull(actualInputs);
        actualInputs.RequireCurrent(state,allocation);
        if(!Sse.IsSupported)throw new NotSupportedException("Composite2 CVTTSS2SI requires the source SSE backend.");
        var values=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var d in OriginalMapPostEffect3ApplyCompleteInitialScalars.Composite2Defaults)
        {
            int i=state.FindScalarIndex(d.Name)??throw new NotSupportedException("Factory9 capability5 lookup missing: "+d.Name);
            var declaration=state.Declarations[i];
            if(declaration.NativeValueType!=d.NativeType||declaration.ElementCount!=d.ElementCount)
                throw new InvalidDataException("Factory9 typed scalar declaration differs: "+d.Name);
            values.Add(d.Name,state.RequireValue(d.Name).ToArray());
        }
        int cbIndex=state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.ConstantBuffer,"cbComposite2")
            ??throw new NotSupportedException("Source cbComposite2 declaration is absent.");
        var cb=allocation.ConstantBufferParameters[cbIndex];
        if(cbIndex!=22||cb.NameHash!=0xD2C4852D||cb.ByteCount!=208||cb.Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("Actual cbComposite2 CB22/208B declaration differs.");
        float sw=actualInputs.SceneWidth40,sh=actualInputs.SceneHeight42,bw=actualInputs.BloomWidth40,bh=actualInputs.BloomHeight42;
        float gamma=F("GammaValue"),inverseGamma=gamma>0?Div(1,gamma):1;
        bool effectiveFxaa=(I("EnableFXAA")&255)!=0||(I("EnableRadialBlur")!=0&&F("RadialBlurAmount")>0);
        var vp=values["Viewport"];var last=values["CompositeLastViewport"];
        bool cropped=VF(vp,2)!=1||VF(vp,3)!=1;
        if(cropped&&!actualInputs.BloomWrapperPresent)
            throw new NotSupportedException("Native cropped208B branch dereferences the actual Bloom wrapper without a null guard.");
        byte[] bytes=new byte[208];
        P(0,sw);P(4,sh);P(8,Div(1,sw));P(12,Div(1,sh));
        C(16,"ScreenWhAspect");C(20,"LdrLutIntensity");P(24,I("EnableAutoExposure")>0?-1:F("UserExposureScale"));C(28,"UserWhitePoint");
        P(32,Add(Mul(F("LensFlareProjPosFovScale",0),.5f),.5f));
        P(36,Sub(.5f,Mul(F("LensFlareProjPosFovScale",1),.5f)));
        P(40,F("LensFlareProjPosFovScale",2)>0&&F("LensFlareProjPosFovScale",3)>0&&F("LensFlareIntensity")>0?1:0);P(44,0);
        C(48,"TinyChromaAberraAmount");P(52,Math.Clamp(I("TinyChromaAberraType"),0,3)*3);
        C(56,"LensFlareIntensity");C(60,"LensFlareThreshold");
        P(64,Div(1,bw));P(68,Div(1,bh));C(72,"BloomSampleScale");P(76,Div(F("BloomIntensity"),I("EnableAutoExposure")>0?1:F("UserExposureScale")));
        C(80,"LimbDarkenningInfo");C(84,"LimbDarkenningInfo",1);P(88,Div(1,F("LimbDarkenningInfo",2)));C(92,"LimbDarkenningLinFade");
        C(96,"FXAAEdgeThreshold");C(100,"FXAAEdgeThresholdMin");C(104,"FXAAEdgeSharpness");P(108,I("EnableFXAA")!=0?F("FXAAPixelRange"):-1);
        P(112,effectiveFxaa?1:inverseGamma);P(116,inverseGamma);W(120,actualInputs.ActualContext1C48Word);P(124,0);
        P(128,Sub(Add(Mul(Add(F("RadialBlurCenter"),1),VF(last,2)),Add(VF(last,0),VF(last,0))),1));
        P(132,Sub(Add(Mul(Sub(1,F("RadialBlurCenter",1)),VF(last,3)),Add(VF(last,1),VF(last,1))),1));
        P(136,effectiveFxaa?1:F("HdrTvScale"));C(140,"HdrTvScale");P(144,I("EnableRadialBlur")!=0?F("RadialBlurAmount"):0);
        P(148,Sub(Mul(F("RadialBlurDistanceExp"),.5f),.5f));P(152,I("RadialBlurMaxSampleNum"));C(156,"RadialBlurWeightDecay");
        C(160,"FXAAQualityEdgeThreshold");C(164,"FXAAQualityEdgeThresholdMin");C(168,"FXAAQualitySubpixSharpness");
        P(172,Add(unchecked(I("FXAAQualityLevel")*10+I("FXAAQualityPerformance")),.5f));
        float scaleX=Div(VF(vp,2),VF(last,2)),scaleY=Div(VF(vp,3),VF(last,3));
        P(176,Sub(VF(vp,0),Mul(scaleX,VF(last,0))));P(180,Sub(VF(vp,1),Mul(scaleY,VF(last,1))));P(184,scaleX);P(188,scaleY);
        P(192,cropped?MaximumUv(VF(vp,2),bw):1);P(196,cropped?MaximumUv(VF(vp,3),bh):1);
        uint mode=BinaryPrimitives.ReadUInt32LittleEndian(allocation.Source.Bytes.Span.Slice(0x44,4));
        P(200,cropped&&actualInputs.SceneWrapperPresent&&mode>=1?MaximumUv(VF(vp,2),sw):1);
        P(204,cropped&&actualInputs.SceneWrapperPresent&&mode>=1?MaximumUv(VF(vp,3),sh):1);
        var selected=Select(state,allocation.Source,values,cropped,mode);
        return new(state,allocation,actualInputs,values,new(state,cb,checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3ParameterComposite2.cs",0x115BC10)),bytes,Locale),cropped,selected,SourceEvidence);
        float F(string n,int word=0)=>VF(values[n],word);
        int I(string n)=>BinaryPrimitives.ReadInt32LittleEndian(values[n]);
        void P(int offset,float v)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset),v);
        void W(int offset,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset),v);
        void C(int offset,string n,int word=0)=>values[n].AsSpan(word*4,4).CopyTo(bytes.AsSpan(offset));
    }

    private static OriginalMapPostEffect3Composite2PassSelection Select(OriginalMapPostEffect3ParameterState state,
        OriginalMapPostEffect3Container module,
        IReadOnlyDictionary<string,byte[]> values,bool cropped,uint mode)
    {
        int I(string n)=>BinaryPrimitives.ReadInt32LittleEndian(values[n]);
        float F(string n)=>VF(values[n],0);
        uint mask=(I("EnableSeparateHdrLut")&255)!=0?0xC00u:4u;
        if(I("EnableBloom")!=0&&F("BloomIntensity")>0)
        {
            mask|=cropped?(I("QualityLevel")>=9?0x1000u:0x2000u):(I("QualityLevel")>=9?1u:2u);
            if(F("CrossFilterWeight")>0)mask|=cropped?0x1C000u:0x380u;
        }
        // Prepare's radial enable uses COMISS >0; Init independently uses
        // UCOMISS !=0 (NaN sets unordered). Preserve this actual distinction.
        bool fxaa=(I("EnableFXAA")&255)!=0||(I("EnableRadialBlur")!=0&&F("RadialBlurAmount")!=0);
        int? fxaaPass=null;
        if(fxaa)
        {
            int type=I("FXAAType");
            if(type is >=0 and <=3)
            {
                bool viewport=cropped&&mode>=1;
                mask|=viewport?(0x20000u<<type):(8u<<type);
                fxaaPass=(viewport?113:109)+type;
            }
        }
        // TC table1A8FC60 stores cache-name order, independently of the
        // counted module directory. Resolve those original names first.
        string[] factoryNames=["AddBloom9SamplePS","AddBloom5SamplePS","ComposeLastPS",
            "ApplyFxaaPS","ApplyFxaaRepairPS","ApplyFxaaConsolePS","ApplyFxaaQualityPS",
            "CrossFilter0PS","CrossFilter1PS","CrossFilter2PS","RemakeHdrLutCS","ComposeLastforRemakePS",
            "AddBloom9Sample_ForViewportPS","AddBloom5Sample_ForViewportPS",
            "CrossFilter0_ForViewportPS","CrossFilter1_ForViewportPS","CrossFilter2_ForViewportPS",
            "ApplyFxaa_ForViewPortPS","ApplyFxaaRepair_ForViewPortPS","ApplyFxaaConsole_ForViewPortPS","ApplyFxaaQuality_ForViewPortPS"];
        int[] expectedDirectory=[98,100,108,109,110,111,112,92,94,96,106,107,99,101,93,95,97,113,114,115,116];
        int[] factoryPasses=factoryNames.Select((name,ordinal)=>
        {
            var matches=module.ReadFunctorPasses(1).Where(p=>p.NameHash==OriginalMapPostEffect3ParameterState.PrimaryNameHash(name)).ToArray();
            if(matches.Length!=1||matches[0].Index!=expectedDirectory[ordinal])
                throw new InvalidDataException("Composite2 native cache name/directory mapping changed: "+name);
            return matches[0].Index;
        }).ToArray();
        var selected=new HashSet<int>();for(int i=0;i<factoryPasses.Length;i++)if((mask&(1u<<i))!=0)selected.Add(factoryPasses[i]);
        // Actual dispatcher iterates source directory order, not constructor
        // pass-name cache order. Remake106 therefore precedes Compose107.
        int[] ordered=state.SourceSha256==OriginalMapFinalOutputPostEffect3Source.PayloadSha256?
            selected.Order().ToArray():throw new InvalidDataException("Composite2 counted source changed.");
        return new(mask,Array.AsReadOnly(ordered),(mask&0xC00)!=0?107:108,fxaaPass);
    }
    private static void Validate(OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);
        if(state.SourceSha256!=allocation.Source.Sha256||state.OwnerIdentity!=allocation.ContextOwnerIdentity||state.FrameNumber<=0)
            throw new InvalidDataException("Composite2 scalar/allocation source identity differs.");
    }
    private static float VF(ReadOnlySpan<byte> bytes,int word)=>BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(word*4,4));
    private static float MaximumUv(float fraction,float extent)=>Div(Sub(Sse.ConvertToInt32WithTruncation(Vector128.CreateScalar(Mul(fraction,extent))),.5f),extent);
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Add(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Sub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Mul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>a/b;
}

public static class OriginalMapPostEffect3ParameterComposite2
{
    public static LoadedOriginalMapPostEffect3Composite2Source Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadPostEffect3Composite2Source(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3Composite2Source ReadPostEffect3Composite2Source(LoadedOriginalDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Composite2 complete factory9 ABI requires a verified SC/TC profile.");
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Composite2 executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);VerifyHash(text,native.DecodedTextSha256,"Composite2 decoded text");
        { var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3ParameterComposite2.cs",0x115BC10,3193,"596E664684FE32A2E0B64E2067056C5C0E05D44F174EE44096E163663586F326");
          VerifyHash(text.AsSpan(actual.Rva-start,3193),actual.Sha,"Composite2 complete CB22 writer"); }
        { var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3ParameterComposite2.cs",0x115CF80,914,"285998F6627605B1C1F238E7F057219F8F953968D8D5CFF1AF98B7234DCD1338");
          VerifyHash(text.AsSpan(actual.Rva-start,914),actual.Sha,"Composite2 complete pass selector"); }
        { var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3ParameterComposite2.cs",0x114AFF0,104,"5ADA83A1940629D571D7FB8BC71738944DF90865833779FC6C8D557F183C3F74");
          VerifyHash(text.AsSpan(actual.Rva-start,104),actual.Sha,"Actual typed cached scalar getter"); }
        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+";TC:115BC10-complete208B-capability5/115CF80-mask;"+
            "actual114AFF0-value=record10;actualD5C240-wrapper40/42;descriptor58=file44;"+
            "sourceCMP2InputColor/PRECMP2InputBloom;capability5-all48typedlookups;"+
            "cbComposite2-CB22;no-GPU-or-current-original-controller-claim",native.Locale);
    }
}
