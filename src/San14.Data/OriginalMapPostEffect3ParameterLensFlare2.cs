using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record OriginalMapPostEffect3LensFlare2ExternalExtent(
    OriginalMapPostEffect3ResolvedTextureParameter ActualTexture,ushort Height42,string SourceEvidence);

public sealed class OriginalMapPostEffect3LensFlare2Inputs
{
    private readonly OriginalMapPostEffect3ParameterState state;
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private readonly OriginalMapPostEffect3ResolvedTextureParameter output;
    internal OriginalMapPostEffect3LensFlare2Inputs(OriginalMapPostEffect3ParameterState state,
        OriginalMapPostEffect3TextureParameterState table,OriginalMapPostEffect3ResolvedTextureParameter output,
        ushort height,uint contextWord,string evidence)
        =>(this.state,this.table,this.output,OutputHeight42,ActualContext1C48Word,SourceEvidence)=
            (state,table,output,height,contextWord,evidence);
    public long FrameNumber=>state.FrameNumber;
    public string ContextOwnerIdentity=>state.OwnerIdentity;
    public ushort OutputHeight42{get;}
    public uint ActualContext1C48Word{get;}
    public OriginalMapPostEffect3ResolvedTextureParameter ActualOutputTexture=>output;
    public string SourceEvidence{get;}
    internal void RequireCurrent(OriginalMapPostEffect3ParameterState actualState,OriginalMapPostEffect3AllocationPlan allocation)
    {
        if(!ReferenceEquals(state,actualState)||!ReferenceEquals(table.Allocation,allocation)||
            table.FrameNumber!=state.FrameNumber||table.ContextOwnerIdentity!=state.OwnerIdentity)
            throw new InvalidDataException("LensFlare2 native output/context is not from this owned source invocation.");
        table.ValidateCurrent(output);
    }
    internal void RequireCurrentTable(OriginalMapPostEffect3TextureParameterState actualTable)
    {if(!ReferenceEquals(table,actualTable))throw new InvalidDataException("LensFlare2 After does not belong to the captured native texture table.");}
}

/// <summary>The original Before's eight bit gates, retaining counted directory
/// order. It does not submit samplers, draws, mip copies or GPU work.</summary>
public sealed record OriginalMapPostEffect3LensFlare2PassSelection(uint NativeMaskF4,
    uint NativeGhostSpriteCountD0,IReadOnlyList<int> OrderedFactory6Passes);

public sealed class OriginalMapPostEffect3LensFlare2PassRecipe
{
    private readonly byte[] draw;
    private readonly OriginalMapPostEffect3ParameterLensFlare2Update update;
    internal OriginalMapPostEffect3LensFlare2PassRecipe(OriginalMapPostEffect3ParameterLensFlare2Update update,
        int pass,uint ghosts,byte[] draw,bool temporary)
        =>(this.update,SourcePassIndex,NativeGhostSpriteCountD0,this.draw,RequiresTemporaryBlendOverride)=(update,pass,ghosts,draw,temporary);
    public int SourcePassIndex{get;}
    public long FrameNumber=>update.FrameNumber;
    public string ContextOwnerIdentity=>update.OwnerIdentity;
    public string SourceSha256=>update.ConstantBuffer.SourceSha256;
    public string SourceEvidence=>update.SourceEvidence;
    public OriginalMapPostEffect3ParameterConstantBuffer ConstantBuffer=>update.ConstantBuffer;
    public uint NativeGhostSpriteCountD0{get;}
    public ReadOnlyMemory<byte> NativeDrawCommand=>draw.ToArray();
    public bool RequiresGenericComputeDispatch=>SourcePassIndex is >=46 and <=49;
    public bool RequiresTemporaryBlendOverride{get;}
    public uint? TemporaryNativeColorStateF4=>RequiresTemporaryBlendOverride?0x111100u:null;
    public byte? TemporaryNativeRt0BlendEnable1C0=>RequiresTemporaryBlendOverride?(byte)1:null;
    public bool RestoreActualPreviousBlendState=>RequiresTemporaryBlendOverride;
    public bool RequiresAfterMipGeneration=>SourcePassIndex is 45 or 49;
    public IReadOnlyList<int> PixelSamplerRegisters=>Array.AsReadOnly(new[]{7,8});
    public string Locale=>update.ConstantBuffer.Locale;
    public uint NativeBeforeRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3ParameterLensFlare2.cs",0x114B450));
    public uint NativeExecuteRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3ParameterLensFlare2.cs",0x114C350));
    public uint NativeAfterRva=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3ParameterLensFlare2.cs",0x114BEA0));
    public bool ProvesGpuExecution=>false;
    public void RequireCurrent()=>update.RequireCurrent();
}

public sealed record OriginalMapPostEffect3LensFlare2MipStep(int SourceMipLevel,int DestinationMipLevel,
    int NativeHandlerViewOffset,uint DestinationWidth,uint DestinationHeight);

/// <summary>Original v38 resource preparation and After45/49 requests. The
/// caller's actual wrapper46/48 snapshot is explicit; view creation and each
/// completed GPU mip write need their own device receipts.</summary>
public sealed class OriginalMapPostEffect3LensFlare2MipPlan
{
    private readonly OriginalMapPostEffect3TextureParameterState table;
    private readonly OriginalMapPostEffect3ParameterLensFlare2Update update;
    internal OriginalMapPostEffect3LensFlare2MipPlan(OriginalMapPostEffect3ParameterLensFlare2Update update,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3ResolvedTextureParameter actualTexture,int pass,ushort word46,ushort word48,
        IReadOnlyList<OriginalMapPostEffect3LensFlare2MipStep> steps,string evidence)
        =>(this.update,this.table,ActualTexture,SourcePassIndex,ActualWrapperWord46,OriginalWrapperWord48,Steps,SourceEvidence)=
            (update,table,actualTexture,pass,word46,word48,steps,evidence);
    public OriginalMapPostEffect3ResolvedTextureParameter ActualTexture{get;}
    public int SourcePassIndex{get;}
    public long FrameNumber=>table.FrameNumber;
    public string ContextOwnerIdentity=>table.ContextOwnerIdentity;
    public string SourceSha256=>table.SourceSha256;
    public ushort ActualWrapperWord46{get;}
    public ushort OriginalWrapperWord48{get;}
    public ushort RequiredWrapperWord48=>unchecked((ushort)((OriginalWrapperWord48&0x3FF)|0x9400));
    public int ActualMipCount=>(ActualWrapperWord46>>10)&31;
    public IReadOnlyList<OriginalMapPostEffect3LensFlare2MipStep> Steps{get;}
    public string SourceEvidence{get;}
    public bool RequiresNativeRegisterResetBeforeMipGeneration=>true;
    public bool ProvesViewCreation=>false;
    public bool ProvesGpuCompletion=>false;
    public void ValidateCurrent(){update.RequireCurrentTextureTable(table);table.ValidateCurrent(ActualTexture);}
    public void RequireCurrentTextureTable(OriginalMapPostEffect3TextureParameterState actualTable)
    {
        if(!ReferenceEquals(table,actualTable))throw new InvalidDataException("LensFlare2 After mip plan belongs to another actual texture table.");
        ValidateCurrent();
    }
}

public sealed class OriginalMapPostEffect3ParameterLensFlare2Update
{
    private readonly OriginalMapPostEffect3ParameterState state;
    private readonly OriginalMapPostEffect3AllocationPlan allocation;
    private readonly OriginalMapPostEffect3LensFlare2Inputs inputs;
    private readonly IReadOnlyDictionary<string,byte[]> scalars;
    internal OriginalMapPostEffect3ParameterLensFlare2Update(OriginalMapPostEffect3ParameterState state,
        OriginalMapPostEffect3AllocationPlan allocation,OriginalMapPostEffect3LensFlare2Inputs inputs,
        IReadOnlyDictionary<string,byte[]> scalars,OriginalMapPostEffect3ParameterConstantBuffer cb,
        OriginalMapPostEffect3LensFlare2PassSelection selection,string evidence)
        =>(this.state,this.allocation,this.inputs,this.scalars,ConstantBuffer,PassSelection,SourceEvidence)=
            (state,allocation,inputs,scalars,cb,selection,evidence);
    public long FrameNumber=>state.FrameNumber;
    public string OwnerIdentity=>state.OwnerIdentity;
    public OriginalMapPostEffect3ParameterConstantBuffer ConstantBuffer{get;}
    public OriginalMapPostEffect3LensFlare2PassSelection PassSelection{get;}
    public string SourceEvidence{get;}
    public bool ProvesGpuExecution=>false;
    public void RequireCurrent()
    {
        inputs.RequireCurrent(state,allocation);
        foreach(var s in scalars)if(!state.RequireValue(s.Key).Span.SequenceEqual(s.Value))
            throw new InvalidDataException("LensFlare2 scalar changed after Init/Prepare: "+s.Key);
    }
    internal void RequireCurrentTextureTable(OriginalMapPostEffect3TextureParameterState table)
    {
        RequireCurrent();inputs.RequireCurrentTable(table);
        if(!ReferenceEquals(table.Allocation,allocation)||table.FrameNumber!=FrameNumber||table.ContextOwnerIdentity!=OwnerIdentity)
            throw new InvalidDataException("LensFlare2 After texture table is from another actual allocation/frame.");
    }
    public void RequireCurrentScalarState(OriginalMapPostEffect3ParameterState actualState)
    {
        if(!ReferenceEquals(state,actualState))throw new InvalidDataException("LensFlare2 source return requires the exact scalar state captured by Prepare.");
        RequireCurrent();
    }
    public OriginalMapPostEffect3LensFlare2PassRecipe PreparePass(int sourcePassIndex)
    {
        RequireCurrent();
        if(!PassSelection.OrderedFactory6Passes.Contains(sourcePassIndex))
            throw new InvalidDataException("LensFlare2 Before rejects this source pass in the actual current mask.");
        byte[] draw;
        bool ghosts=sourcePassIndex is 51 or 52;
        if(sourcePassIndex is 45 or 50)draw=Words(0x3002D,0,3);
        else if(ghosts)draw=Words(0x40031,4,PassSelection.NativeGhostSpriteCountD0,0,0);
        else draw=[];
        return new(this,sourcePassIndex,PassSelection.NativeGhostSpriteCountD0,draw,ghosts);
    }
    private static byte[] Words(params uint[] words)
    {byte[] bytes=new byte[words.Length*4];for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),words[i]);return bytes;}
}

public sealed class LoadedOriginalMapPostEffect3LensFlare2Source
{
    private readonly float fixedSampleScreenScale,heightFactor,heightNumerator;
    internal LoadedOriginalMapPostEffect3LensFlare2Source(float fixedScale,float factor,float numerator,string evidence,string locale="TC")
        =>(fixedSampleScreenScale,heightFactor,heightNumerator,SourceEvidence,Locale)=(fixedScale,factor,numerator,evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public string RuleVersion=>OriginalMapPostEffect3ParameterLensFlare2.RuleVersion;

    public OriginalMapPostEffect3LensFlare2Inputs CaptureCurrentInputs(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3TextureParameterState table,uint actualContext1C48Word,
        IReadOnlyList<OriginalMapPostEffect3LensFlare2ExternalExtent> actualExternalExtents,string sourceEvidence)
    {
        Validate(state,allocation);ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(actualExternalExtents);ArgumentException.ThrowIfNullOrWhiteSpace(sourceEvidence);
        if(!ReferenceEquals(table.Allocation,allocation)||table.FrameNumber!=state.FrameNumber)
            throw new InvalidDataException("LensFlare2 output lookup requires this owned invocation table.");
        int index=state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.Texture,"LF2OutputColor")
            ??throw new InvalidDataException("Native LF2OutputColor primary declaration is absent.");
        if(index!=57)throw new InvalidDataException("Native LensFlare2 output declaration index changed.");
        var output=table.Require(index);ushort height;
        if(output.Reference.PhysicalAllocationParameterIndex is int physical)
            height=allocation.TextureAllocations.Single(t=>t.Parameter.Index==physical).Height16;
        else
        {
            var candidates=actualExternalExtents.Where(e=>e.ActualTexture==output).ToArray();
            if(candidates.Length!=1||string.IsNullOrWhiteSpace(candidates[0].SourceEvidence))
                throw new NotSupportedException("LensFlare2 selected external output has no actual wrapper42 producer.");
            table.ValidateCurrent(candidates[0].ActualTexture);height=candidates[0].Height42;
        }
        return new(state,table,output,height,actualContext1C48Word,SourceEvidence+";"+sourceEvidence);
    }

    /// <summary>114CA40 Init plus complete114C650 CB17/96B Prepare. No finite
    /// clamps: fresh zero FOV legitimately produces reciprocal infinity.</summary>
    public OriginalMapPostEffect3ParameterLensFlare2Update Prepare(
        OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation,
        OriginalMapPostEffect3LensFlare2Inputs actualInputs)
    {
        Validate(state,allocation);ArgumentNullException.ThrowIfNull(actualInputs);actualInputs.RequireCurrent(state,allocation);
        var values=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var d in OriginalMapPostEffect3ApplyInitialScalars.LensDefaults)
        {
            int index=state.FindScalarIndex(d.PrimaryHash)??throw new InvalidDataException("LensFlare2 cached scalar lookup absent: "+d.Name);
            var decl=state.Declarations[index];
            if(decl.NativeValueType!=d.NativeType||decl.ElementCount!=d.ElementCount||decl.ValueByteCount!=d.Value.Length)
                throw new InvalidDataException("LensFlare2 cached scalar type/count differs: "+d.Name);
            values.Add(d.Name,state.RequireValue(d.Name).ToArray());
        }
        int ci=state.FindResourceIndex(OriginalMapPostEffect3ParameterResourceTable.ConstantBuffer,"cbFlare2")
            ??throw new InvalidDataException("LensFlare2 cbFlare2 declaration absent.");
        var cb=allocation.ConstantBufferParameters[ci];
        if(ci!=17||cb.NameHash!=0x2B541206||cb.ByteCount!=96||cb.Ownership!=OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration)
            throw new InvalidDataException("Native cbFlare2 CB17/96B source declaration differs.");
        float sample=Mul(F("LensFlareProjPosFovScale",3),F("StarBurstSampleScale"));
        sample=Mul(sample,I("Enable4KResFix")==1?fixedSampleScreenScale:Mul(F("ScreenHeight"),heightFactor));
        byte[] bytes=new byte[96];
        P(0,Div(1,sample));P(4,I("StopBladeCount"));C(8,"StopPolyRate");P(12,Div(heightNumerator,actualInputs.OutputHeight42));
        values["LensFlareProjPosFovScale"].CopyTo(bytes,16);
        C(32,"FlareScreenWhAspect");P(36,Div(1,F("FlareScreenWhAspect")));
        values["LensHaloInfo0"].CopyTo(bytes,48);values["HaloAberrationWavesRateRB"].CopyTo(bytes,64);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72),actualInputs.ActualContext1C48Word);
        values["AnamorphicCoatColor"].CopyTo(bytes,80);P(92,I("EnableAnamorphic")>0?1:-1);
        uint mask=I("ChangeFStop")!=0?(I("EnableExternalStarBurst")==0?31u:1u):0u;
        uint ghosts=unchecked((uint)I("GhostSpriteNum"));
        if(F("LensFlareProjPosFovScale",2)>0&&F("LensFlareProjPosFovScale",3)>0&&F("LensFlareIntensity")>0)
        {
            mask|=32;
            if(ghosts!=0)mask|=I("DiaphragmType")==0?64u:128u;
        }
        int[] passIndices=[45,46,47,48,49,50,51,52];uint[] hashes=[0xECBE7B50,0x0188A2F4,0xAAAACB4A,0x587F8A45,0x011A86CE,0x18432977,0xE5E4F7F7,0xFB3C5392];
        var selected=new List<int>();
        for(int i=0;i<8;i++)
        {
            var pass=allocation.Passes[passIndices[i]].Pass;
            if(BinaryPrimitives.ReadUInt32LittleEndian(pass.RawBytes.Span[8..])!=6||pass.NameHash!=hashes[i])
                throw new InvalidDataException("LensFlare2 eight native cached pass hashes differ.");
            if((mask&(1u<<i))!=0)selected.Add(passIndices[i]);
        }
        return new(state,allocation,actualInputs,values,new(state,cb,checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3ParameterLensFlare2.cs",0x114C650)),bytes,Locale),
            new(mask,ghosts,selected.AsReadOnly()),SourceEvidence);
        float F(string name,int word=0)=>BinaryPrimitives.ReadSingleLittleEndian(values[name].AsSpan(word*4));
        int I(string name)=>BinaryPrimitives.ReadInt32LittleEndian(values[name]);
        void P(int offset,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset),value);
        void C(int offset,string name)=>values[name].AsSpan(0,4).CopyTo(bytes.AsSpan(offset));
    }

    public OriginalMapPostEffect3LensFlare2MipPlan PrepareAfterMipPlan(
        OriginalMapPostEffect3ParameterLensFlare2Update actualPrepare,
        OriginalMapPostEffect3TextureParameterState table,int actualSelectedSourcePass,
        ushort actualWrapperWord46,ushort actualWrapperWord48,string actualWrapperSourceEvidence)
    {
        ArgumentNullException.ThrowIfNull(actualPrepare);ArgumentNullException.ThrowIfNull(table);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualWrapperSourceEvidence);actualPrepare.RequireCurrentTextureTable(table);
        if(actualPrepare.SourceEvidence!=SourceEvidence||actualPrepare.FrameNumber!=table.FrameNumber||
            actualPrepare.OwnerIdentity!=table.ContextOwnerIdentity)
            throw new InvalidDataException("LensFlare2 After uses a different current source owner/frame.");
        var pass=actualPrepare.PreparePass(actualSelectedSourcePass);
        if(!pass.RequiresAfterMipGeneration)throw new InvalidDataException("Native LensFlare2 After has no mip branch for this selected pass.");
        int index=actualSelectedSourcePass==45?50:55;
        var texture=table.Require(index);
        if(texture.Reference.PhysicalAllocationParameterIndex!=index)
            throw new NotSupportedException("LensFlare2 actual handler mip wrapper is not its source-owned declaration.");
        var allocation=table.Allocation.TextureAllocations.Single(t=>t.Parameter.Index==index);
        int count=(actualWrapperWord46>>10)&31;
        if(count is <1 or >12)throw new NotSupportedException("LensFlare2 original handler has eleven mip-view slots; actual wrapper mip count is unsupported.");
        var steps=new List<OriginalMapPostEffect3LensFlare2MipStep>();
        for(int mip=1;mip<count;mip++)steps.Add(new(mip-1,mip,
            (actualSelectedSourcePass==45?0x18:0x78)+8*(mip-1),Math.Max(1,allocation.Width>>mip),Math.Max(1,allocation.Height>>mip)));
        return new(actualPrepare,table,texture,actualSelectedSourcePass,actualWrapperWord46,actualWrapperWord48,steps.AsReadOnly(),
            SourceEvidence+";VT.v38=114B520;actual-wrapper46/48="+actualWrapperSourceEvidence+
            ";94AA60/968F40-source-singleMip-views;114BEA0-sourceMip0..count-2/destinationMip1..count-1;no-view-or-GPU-completion");
    }

    private static void Validate(OriginalMapPostEffect3ParameterState state,OriginalMapPostEffect3AllocationPlan allocation)
    {
        ArgumentNullException.ThrowIfNull(state);ArgumentNullException.ThrowIfNull(allocation);
        OriginalMapPostEffect3ParameterState.ValidateSource(allocation.Source);state.BindOwnedAllocation(allocation);
        if(state.FrameNumber<=0||state.OwnerIdentity!=allocation.ContextOwnerIdentity)
            throw new InvalidDataException("LensFlare2 requires this source-owned current scalar invocation.");
    }
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Mul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float Div(float a,float b)=>a/b;
}

public static class OriginalMapPostEffect3ParameterLensFlare2
{
    public const string ResourceUri="san14://rules/post/lens-flare2";
    public const string RuleVersion="lens-flare2-v1";
    internal const float FixedSampleScreenScale=270f;
    internal const float ScreenHeightFactor=.25f;
    internal const float OutputHeightNumerator=2f;

    public static LoadedOriginalMapPostEffect3LensFlare2Source Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadPostEffect3LensFlare2Source(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3LensFlare2Source ReadPostEffect3LensFlare2Source(LoadedOriginalDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("LensFlare2 rules support SC/TC.");
        return new(OriginalMapPostEffect3ParameterLensFlare2.FixedSampleScreenScale,
            OriginalMapPostEffect3ParameterLensFlare2.ScreenHeightFactor,
            OriginalMapPostEffect3ParameterLensFlare2.OutputHeightNumerator,
            OriginalMapPostEffect3ParameterLensFlare2.ResourceUri+"@"+
            OriginalMapPostEffect3ParameterLensFlare2.RuleVersion+$";locale={native.Locale};"+
            "owned50/55-mipViews;factory6-pass-mask;CB17/96B",native.Locale);
    }
}
