using System.Buffers.Binary;

namespace San14.Data;

public enum OriginalMapPostEffect3Functor1FactoryCallback { Prepare,Initialize }
public sealed record OriginalMapPostEffect3Functor1ChildReturnObservation(uint SourceRva,uint? ReturnedValue);

/// <summary>Original wrapper return aggregation. Explicit child observations
/// are CPU observations or oracle fixtures, not device completion authority.
/// A null child means the call has not returned; the wrapper has no AL yet.</summary>
public sealed class OriginalMapPostEffect3Functor1FactoryReturnTrace
{
    private OriginalMapPostEffect3Functor1FactoryReturnTrace(int factory,OriginalMapPostEffect3Functor1FactoryCallback callback,
        IReadOnlyList<OriginalMapPostEffect3Functor1ChildReturnObservation> children,byte? al,string evidence,string locale)
        =>(FactoryIndex,Callback,Children,SourceReturnAl,SourceEvidence,Locale)=(factory,callback,children,al,evidence,locale);
    public int FactoryIndex{get;}
    public string Locale{get;}
    public OriginalMapPostEffect3Functor1FactoryCallback Callback{get;}
    public IReadOnlyList<OriginalMapPostEffect3Functor1ChildReturnObservation> Children{get;}
    public byte? SourceReturnAl{get;}
    public bool FunctionReturned=>SourceReturnAl is not null;
    public string SourceEvidence{get;}
    public bool ProvesGpuCompletion=>false;
    public static OriginalMapPostEffect3Functor1FactoryReturnTrace Read(int factory,OriginalMapPostEffect3Functor1FactoryCallback callback,
        IReadOnlyList<OriginalMapPostEffect3Functor1ChildReturnObservation> children,string actualCpuObservationEvidence,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(children);ArgumentException.ThrowIfNullOrWhiteSpace(actualCpuObservationEvidence);
        uint[] rvas=SourceChildren(factory,callback,locale);
        if(!children.Select(c=>c.SourceRva).SequenceEqual(rvas))
            throw new InvalidDataException("Factory CPU return requires exact original child call order.");
        bool incomplete=false;
        foreach(var child in children)
        {
            if(incomplete&&child.ReturnedValue is not null)
                throw new InvalidDataException("A later child cannot return before the preceding unreturned native call.");
            incomplete|=child.ReturnedValue is null;
        }
        byte? result=null;
        if(!incomplete)
        {
            byte A(int i)=>unchecked((byte)children[i].ReturnedValue!.Value);
            result=callback==OriginalMapPostEffect3Functor1FactoryCallback.Prepare?
                factory switch{6=>(byte)(A(0)&1),7 or 8=>(byte)((A(0)&1)&A(1)),9=>(byte)((A(0)&1)&A(1)&A(2)),_=>throw new InvalidDataException()}:
                factory switch{6 or 7=>(byte)(A(0)&1),8=>(byte)((A(1)&1)&A(2)),9=>(byte)1,_=>throw new InvalidDataException()};
        }
        return new(factory,callback,Array.AsReadOnly(children.ToArray()),result,actualCpuObservationEvidence+
            ";source native wrapper aggregation;aliases/map results do not become GPU completion",locale);
    }
    internal static uint[] SourceChildren(int factory,OriginalMapPostEffect3Functor1FactoryCallback callback,string locale="TC")
        =>CanonicalChildren(factory,callback).Select(r=>checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(
            locale,"OriginalMapPostEffect3Functor1FactoryReturnSource.cs",checked((int)r)))).ToArray();
    private static uint[] CanonicalChildren(int factory,OriginalMapPostEffect3Functor1FactoryCallback callback)
        =>(factory,callback) switch{
            (6,OriginalMapPostEffect3Functor1FactoryCallback.Prepare)=>[0x114C650],
            (6,OriginalMapPostEffect3Functor1FactoryCallback.Initialize)=>[0x114CA40],
            (7,OriginalMapPostEffect3Functor1FactoryCallback.Prepare)=>[0x1156E20,0x1156550],
            (7,OriginalMapPostEffect3Functor1FactoryCallback.Initialize)=>[0x1157230],
            (8,OriginalMapPostEffect3Functor1FactoryCallback.Prepare)=>[0x1159710,0x11594D0],
            (8,OriginalMapPostEffect3Functor1FactoryCallback.Initialize)=>[0x1157B70,0x1159A40,0x114AFF0],
            (9,OriginalMapPostEffect3Functor1FactoryCallback.Prepare)=>[0x115BC10,0x115C890,0x115CD10],
            (9,OriginalMapPostEffect3Functor1FactoryCallback.Initialize)=>[0x115CF80],
            _=>throw new NotSupportedException("Factory return source covers only Functor1 factory6..9 Prepare/Init.")};
}

/// <summary>The port's typed current cached records. Native114AFF0 does not
/// inspect type/count to return AL: its nonnull stack output addresses return
/// literal1. Typed checks here admit safe current records for the leaf writers;
/// they are stronger than that native return test and confer no native address.</summary>
public sealed record OriginalMapPostEffect3Functor1CachedScalarReturn(string Name,int NativeCachedControllerOffset,
    OriginalMapPostEffect3ParameterDeclaration ActualDeclaration,ReadOnlyMemory<byte> ActualValue)
{
    public byte SourceGetterReturnAl=>1;
    public bool SourceGetterHasNonNullStackOutputAddress=>true;
    public bool ProvesOriginalProcessPointer=>false;
}

/// <summary>Closed ported CPU Prepare/Init return recipe, joined to the exact
/// current invocation, scalar cache, completed writer packets and Init aliases.
/// Source return1 alone does not prove shape allocation, CB upload or pixels.</summary>
public sealed class OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe
{
    private readonly OriginalMapPostEffect3Functor1ModuleInvocation invocation;
    private readonly Action requirePrepared;
    internal OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe(OriginalMapPostEffect3Functor1ModuleInvocation invocation,
        int factory,object prepared,Action requirePrepared,IReadOnlyList<OriginalMapPostEffect3Functor1CachedScalarReturn> cache,
        IReadOnlyList<OriginalMapPostEffect3ParameterConstantBuffer> packets,
        OriginalMapPostEffect3Functor1FactoryReturnTrace prepare,OriginalMapPostEffect3Functor1FactoryReturnTrace initialize,string evidence)
        =>(this.invocation,FactoryIndex,SourcePreparedIdentity,this.requirePrepared,CachedScalarRecords,ActualCpuConstantBuffers,
            PrepareTrace,InitializeTrace,SourceEvidence)=(invocation,factory,prepared,requirePrepared,cache,packets,prepare,initialize,evidence);
    public int FactoryIndex{get;}
    public long FrameNumber=>invocation.FrameNumber;
    public string ContextOwnerIdentity=>invocation.State.OwnerIdentity;
    public object SourcePreparedIdentity{get;}
    public IReadOnlyList<OriginalMapPostEffect3Functor1CachedScalarReturn> CachedScalarRecords{get;}
    public IReadOnlyList<OriginalMapPostEffect3ParameterConstantBuffer> ActualCpuConstantBuffers{get;}
    public OriginalMapPostEffect3Functor1FactoryReturnTrace PrepareTrace{get;}
    public OriginalMapPostEffect3Functor1FactoryReturnTrace InitializeTrace{get;}
    public byte SourcePrepareReturnAl=>PrepareTrace.SourceReturnAl!.Value;
    public byte SourceInitializeReturnAl=>InitializeTrace.SourceReturnAl!.Value;
    public string Locale=>PrepareTrace.Locale;
    public uint SourcePrepareRva=>OriginalMapPostEffect3PassStateReader.FactoriesForLocale(Locale)[FactoryIndex].PrepareHook;
    public uint SourceInitializeRva=>OriginalMapPostEffect3PassStateReader.FactoriesForLocale(Locale)[FactoryIndex].InitializeHook;
    public string SourceEvidence{get;}
    public bool ProvesNativeProcessCallbackObservation=>false;
    public bool ProvesGpuCompletion=>false;
    public bool ProvesGpuAllocation=>false;
    public bool ProvesConstantBufferUpload=>false;
    public void RequireCurrent()
    {
        invocation.RequireCurrent();requirePrepared();
        foreach(var record in CachedScalarRecords)
            if(!invocation.State.RequireValue(record.Name).Span.SequenceEqual(record.ActualValue.Span))
                throw new InvalidDataException("Factory CPU return cache changed: "+record.Name);
    }
    /// <summary>Records these source-derived bytes in the native6/7/8/9 CPU
    /// lifecycle cursor. This performs no GPU command or independent Begin.</summary>
    public void RecordIntoInvocation(OriginalMapPostEffect3Functor1ModuleInvocation actualInvocation)
    {
        if(!ReferenceEquals(invocation,actualInvocation))throw new InvalidDataException("Factory CPU return belongs to another exact invocation.");
        RequireCurrent();actualInvocation.RecordFactoryPrepareInitializeReturns(FactoryIndex,SourcePrepareReturnAl,SourceInitializeReturnAl);
    }
}

public sealed class LoadedOriginalMapPostEffect3Functor1FactoryReturnSource
{
    private readonly LoadedOriginalMapPostEffect3EyeAdaptation2Source eyeSource;
    private readonly LoadedOriginalNativeDoubleLogSource doubleLog;
    internal LoadedOriginalMapPostEffect3Functor1FactoryReturnSource(string evidence,LoadedOriginalMapPostEffect3EyeAdaptation2Source eye,
        LoadedOriginalNativeDoubleLogSource log)=>(SourceEvidence,eyeSource,doubleLog)=(evidence,eye,log);
    public string Locale=>eyeSource.Locale;
    public string SourceEvidence{get;}
    public OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe CaptureLensReturned(
        OriginalMapPostEffect3Functor1ModuleInvocation invocation,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3ParameterLensFlare2Update actualPrepared)
    {
        void Require(){Validate(invocation,table);actualPrepared.RequireCurrentScalarState(invocation.State);actualPrepared.RequireCurrentTextureTable(table);
            Packet(invocation,table,actualPrepared.ConstantBuffer,17,96,0x114C650);}
        Require();return Make(invocation,6,actualPrepared,Require,OriginalMapPostEffect3ApplyInitialScalars.LensDefaults,[actualPrepared.ConstantBuffer]);
    }
    public OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe CaptureDepthOfFieldReturned(
        OriginalMapPostEffect3Functor1ModuleInvocation invocation,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3ParameterDepthOfField2Update actualPrepared)
    {
        void Require(){Validate(invocation,table);invocation.RequireCurrentDepthOfField2Prepared(actualPrepared,table);
            Packet(invocation,table,actualPrepared.ConstantBuffer,18,144,0x1156550);}
        Require();return Make(invocation,7,actualPrepared,Require,OriginalMapPostEffect3ApplyCompleteInitialScalars.DepthOfField2Defaults,[actualPrepared.ConstantBuffer]);
    }
    /// <summary>Completes original Prepare's separately required CB24 writer,
    /// with source binary64 gray log and binary32 denominator. CB21 alone was
    /// never complete factory8 Prepare. The returned packet still needs upload.</summary>
    public OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe CaptureEyeReturned(
        OriginalMapPostEffect3Functor1ModuleInvocation invocation,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3EyeAdaptation2Frame actualPrepared,OriginalMapPostEffect3ParameterConstantBuffer? actualExposurePrepared=null)
    {
        void Require(){Validate(invocation,table);invocation.Owner.EyeController.RequireCurrent(actualPrepared);
            if(!ReferenceEquals(actualPrepared.State,invocation.State)||!ReferenceEquals(actualPrepared.Allocation,table.Allocation))
                throw new InvalidDataException("Eye CPU return requires its exact current source prepared state.");
            actualPrepared.RequireCurrent();Packet(invocation,table,actualPrepared.BloomMainConstants,21,32,0x11594D0);}
        Require();var expectedExposure=eyeSource.PrepareExposure(actualPrepared,doubleLog,OriginalNativeLogDispatch.Sse);
        var exposure=actualExposurePrepared??expectedExposure;
        Packet(invocation,table,exposure,24,64,0x1159710);
        if(!exposure.Bytes.Span.SequenceEqual(expectedExposure.Bytes.Span))
            throw new InvalidDataException("Eye factory Prepare return requires its complete current source binary64/binary32 Exposure CB24 writer packet.");
        var output=table.ReadKnownNullablePointer(74) is null?null:table.Require(74);
        long? nullRevision=output is null?table.CaptureKnownNullRevision(74):null;
        var alias=actualPrepared.InitializeOutputAlias;
        if(alias.SourceParameterIndex is int from)
        {if(!ReferenceEquals(table.ReadKnownNullablePointer(from),output?.Reference))throw new InvalidDataException("Eye Init output74 did not copy its actual disabled input68.");}
        else if(output is null||output.Reference.PhysicalAllocationParameterIndex!=alias.PhysicalTextureParameterIndex||
            output.Reference.NativeFirstMipLevel!=0||output.Reference.NativeMipLevelCount!=1||
            output.Reference.InstanceIndex!=table.Require(alias.PhysicalTextureParameterIndex!.Value).Reference.InstanceIndex)
            throw new InvalidDataException("Eye Init output74 is not its actual current85/86 cloned mip0 wrapper.");
        void RequireInitialized(){Require();if(output is null)table.ValidateKnownNullRevision(74,nullRevision!.Value);else table.ValidateCurrent(output);}
        return Make(invocation,8,actualPrepared,RequireInitialized,OriginalMapPostEffect3ApplyInitialScalars.ExposureDefaults,
            [exposure,actualPrepared.BloomMainConstants],actualPrepared.LevelCount);
    }
    public OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe CaptureCompositeReturned(
        OriginalMapPostEffect3Functor1ModuleInvocation invocation,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3Composite2ExecutionFrame actualPrepared)
    {
        void Require(){Validate(invocation,table);actualPrepared.Prepared.RequireOwnedLifecycle(invocation.State,table.Allocation,table);
            actualPrepared.RequireInitialized();actualPrepared.RequireTable(table);
            Packet(invocation,table,actualPrepared.Prepared.ConstantBuffer,22,208,0x115BC10);
            if(actualPrepared.RequiresCrossFilterConstants||actualPrepared.RequiresHdrRemakeConstants)
                throw new NotSupportedException("Factory return admission currently covers the actual zero CrossFilter/no separate HDR LUT branch; positive optional writers need their own mapped-byte return join.");}
        Require();return Make(invocation,9,actualPrepared,Require,OriginalMapPostEffect3ApplyCompleteInitialScalars.Composite2Defaults,
            [actualPrepared.Prepared.ConstantBuffer]);
    }
    private OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe Make(OriginalMapPostEffect3Functor1ModuleInvocation invocation,
        int factory,object prepared,Action require,IReadOnlyList<OriginalMapPostEffect3ApplyInitialDefault> definitions,
        OriginalMapPostEffect3ParameterConstantBuffer[] packets,int eyeLevels=0)
    {
        var state=invocation.State;var cache=new List<OriginalMapPostEffect3Functor1CachedScalarReturn>();
        foreach(var d in definitions)
        {
            int index=state.FindScalarIndex(d.PrimaryHash)??throw new InvalidDataException("Factory cached record is absent: "+d.Name);
            var declaration=state.Declarations[index];var value=state.RequireValue(d.Name).ToArray();
            if(declaration.NativeValueType!=d.NativeType||declaration.ElementCount!=d.ElementCount||declaration.ValueByteCount!=value.Length||
                value.Length!=d.Value.Length||d.CachedControllerOffset<=0)
                throw new InvalidDataException("Factory cached record typed declaration differs: "+d.Name);
            cache.Add(new(d.Name,d.CachedControllerOffset,declaration,value));
        }
        // For admitted current nonnull cached records each leaf AND fold is1.
        // Shape's no-change early return, Exposure's epilogue and Composite
        // Init's epilogue are independently source literal1. No allocation,
        // alias setter or map AL is substituted into these native folds.
        OriginalMapPostEffect3Functor1FactoryReturnTrace Trace(OriginalMapPostEffect3Functor1FactoryCallback callback)
        {
            var children=OriginalMapPostEffect3Functor1FactoryReturnTrace.SourceChildren(factory,callback,Locale)
                .Select((r,i)=>new OriginalMapPostEffect3Functor1ChildReturnObservation(r,
                    factory==8&&callback==OriginalMapPostEffect3Functor1FactoryCallback.Initialize&&i==0?(uint)eyeLevels:1u)).ToArray();
            return OriginalMapPostEffect3Functor1FactoryReturnTrace.Read(factory,callback,children,SourceEvidence+
                ";actual typed current cached records plus proved nonnull native stack outputs;source leaf return fold/literal;completed CPU writer ports",Locale);
        }
        return new(invocation,factory,prepared,require,Array.AsReadOnly(cache.ToArray()),Array.AsReadOnly(packets),
            Trace(OriginalMapPostEffect3Functor1FactoryCallback.Prepare),Trace(OriginalMapPostEffect3Functor1FactoryCallback.Initialize),SourceEvidence);
    }
    private static void Validate(OriginalMapPostEffect3Functor1ModuleInvocation invocation,OriginalMapPostEffect3TextureParameterState table)
    {ArgumentNullException.ThrowIfNull(invocation);ArgumentNullException.ThrowIfNull(table);invocation.RequireCurrentTextureTable(table);
        if(!ReferenceEquals(invocation.Owner.Allocation,table.Allocation)||table.FrameNumber!=invocation.FrameNumber||
            table.ContextOwnerIdentity!=invocation.State.OwnerIdentity||table.SourceSha256!=invocation.State.SourceSha256)
            throw new InvalidDataException("Factory return requires its current source module allocation/frame/table.");}
    private void Packet(OriginalMapPostEffect3Functor1ModuleInvocation invocation,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3ParameterConstantBuffer packet,int index,int count,uint writer)
    {if(packet.FrameNumber!=invocation.FrameNumber||packet.OwnerIdentity!=table.ContextOwnerIdentity||packet.SourceSha256!=table.SourceSha256||
        !ReferenceEquals(packet.Declaration,table.Allocation.ConstantBufferParameters[index])||packet.Declaration.Index!=index||
        packet.Bytes.Length!=count||packet.Locale!=Locale||packet.WriterRva!=checked((uint)OriginalMapPostEffect3NativeSourceProfile.CodeRva(Locale,"OriginalMapPostEffect3Functor1FactoryReturnSource.cs",checked((int)writer))))throw new InvalidDataException("Factory CPU writer packet source/identity differs.");}
}

public static class OriginalMapPostEffect3Functor1FactoryReturnSource
{
    public static LoadedOriginalMapPostEffect3Functor1FactoryReturnSource Load(OriginalGameRepository repository,
        LoadedOriginalDeferredSamplerNativeSource native,LoadedOriginalMapPostEffect3EyeAdaptation2Source eye,
        LoadedOriginalNativeDoubleLogSource log)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(eye);ArgumentNullException.ThrowIfNull(log);
        if(native.Locale is not ("TC" or "SC")||eye.Locale!=native.Locale||log.Locale!=native.Locale||eye.FloatLogTables.ExecutableSha256!=native.ExecutableSha256||
            log.ExecutableSha256!=native.ExecutableSha256||log.DecodedTextSha256!=native.DecodedTextSha256||
            !StringComparer.OrdinalIgnoreCase.Equals(eye.FloatLogTables.PhysicalPath,native.PhysicalPath)||
            !StringComparer.OrdinalIgnoreCase.Equals(log.PhysicalPath,native.PhysicalPath)||
            !StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(native.PhysicalPath),repository.SourceDirectory))
            throw new InvalidDataException("Factory return static dependencies belong to another verified native generation.");
        return OriginalDeferredSamplerSources.ReadPostEffect3Functor1FactoryReturnSource(native,repository,eye,log);
    }
    public static LoadedOriginalMapPostEffect3Functor1FactoryReturnSource Load(OriginalGameRepository repository,string locale="TC")
        =>OriginalDeferredSamplerSources.ReadPostEffect3Functor1FactoryReturnSource(OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale),repository);
}
public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3Functor1FactoryReturnSource ReadPostEffect3Functor1FactoryReturnSource(
        LoadedOriginalDeferredSamplerNativeSource native,OriginalGameRepository repository,
        LoadedOriginalMapPostEffect3EyeAdaptation2Source? actualEye=null,LoadedOriginalNativeDoubleLogSource? actualLog=null)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("Factory6..9 callback source requires a verified SC/TC profile.");
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Factory return executable");
        var image=new NativeImage(exe);byte[] code=image.DecodeText(out int start);VerifyHash(code,native.DecodedTextSha256,"Factory return decoded text");
        foreach(var w in new (int Rva,int Count,string Sha)[]{
            (0x114AFF0,104,"5ADA83A1940629D571D7FB8BC71738944DF90865833779FC6C8D557F183C3F74"),
            (0x114C640,16,"EBC6CA1A12271C7F394E99573DDF5641B71A965C63EC6DE2682C3A9982C2A883"),
            (0x114B500,19,"496A66D25B7DE133648D19F5890942F5FC5CD27CA0C3A1DBE8A836E107DFB3F0"),
            (0x114C650,993,"1B18DBBD5B6C01947A1ED2DE50ED8716AECA811AE23F8B30B1C9835A382EEEBE"),
            (0x114CA40,477,"B4253EA48BE03A2EB1AC120E24393532B7A70433C74D61DBFEDBBF4DB8E165F9"),
            (0x11564F0,92,"DEF1AE69E191147150010E2DA15A20E7C8030FB5D67A0189788F68C367BA1AF4"),
            (0x1155140,144,"A91075936307414BFC7CCC80E0AF09450E55A4C81447BC6003FF874558461F4D"),
            (0x1156E20,1031,"646B7724461BC4CE08787B6A21E38BE57C201D176EBB7DB9CEEB135F52C9C584"),
            (0x1156550,2250,"A1B8A7C6DB94A02252C53C0F5DB45613D16962629BE43F5BCA8AF89AFB36B956"),
            (0x1157230,989,"9316214803F0B784502ECDE7A1DE9FD1E0B0332CD88FC71C2345A4A05962896C"),
            (0x11592A0,77,"0A4C930DF8FA80E75594ED6A9F5D242C677E4AA6F635001BBABA5C638A22BD34"),
            (0x1158040,285,"D599B07D01DCBA0AD1D3DB4EACD9CFC663BC286CDEF5DB51768A497369524169"),
            (0x1159710,803,"63416EB2CE28B34E5B4B2EE122970B9CEE5602D3EDFD6F50A9BBD3CAF13CB93E"),
            (0x11594D0,572,"BBC72D3C6F65DB0A72C237F0BD7B5C345FE16F9C2A329D2496D80DA4F3D41302"),
            (0x1157B70,313,"634705891C1EBD635BB0466DC0637871C5D98465E5EC0043A9BEED15C001EB73"),
            (0x1159A40,354,"B3CF97C17D3D1D6D79506A8F6542C63172F34D084A961C192FB7CC89C8DFBE9C"),
            (0x115BBA0,111,"0DD8BE850DF8BFD7C2C9CE69462D22EB372811F497E9D6DD6FB5BE11353CB618"),
            (0x115A020,328,"56C71AFF9A466B55B7C288DEA4970D294D440D5A8FFA2C927838A88F63828BF8"),
            (0x115BC10,3193,"596E664684FE32A2E0B64E2067056C5C0E05D44F174EE44096E163663586F326"),
            (0x115C890,1149,"8120D9A4B8B41AFBE167AA83B88AB661F27DB2DD3974A1287924D984756C4064"),
            (0x115CD10,624,"07C28C4375D05A0CA0D48202CED42C826D1C64DA82336E1761113BEDFC88869B"),
            (0x115CF80,914,"285998F6627605B1C1F238E7F057219F8F953968D8D5CFF1AF98B7234DCD1338")})
            { var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3Functor1FactoryReturnSource.cs",w.Rva,w.Count,w.Sha);
              VerifyHash(code.AsSpan(actual.Rva-start,w.Count),actual.Sha,"Factory native return/leaf source "+w.Rva.ToString("X")); }
        foreach(var f in OriginalMapPostEffect3PassStateReader.FactoriesForLocale(native.Locale).Skip(6))
        foreach(var slot in new (int Offset,uint Rva)[]{(0x18,f.PrepareHook),(0x20,f.InitializeHook)})
            if(BinaryPrimitives.ReadUInt64LittleEndian(image.Raw(checked((int)f.Vtable+slot.Offset),8))!=0x140000000UL+slot.Rva)
                throw new InvalidDataException("Factory native Prepare/Init vtable identity changed.");
        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+"@"+native.ExecutableSha256+
            ";factory6..9 actual Prepare/Init return aggregation;114AFF0-nonnull-stack-output-literal1;CPU source port only",
            actualEye??OriginalMapPostEffect3ParameterEyeAdaptation2.Load(repository,native.Locale),actualLog??LoadedOriginalNativeDoubleLogSource.Load(repository,native.Locale));
    }
}
