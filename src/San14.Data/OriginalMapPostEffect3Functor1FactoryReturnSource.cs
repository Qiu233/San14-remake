using System.Buffers.Binary;

namespace San14.Data;

public enum OriginalMapPostEffect3Functor1FactoryCallback { Prepare,Initialize }
public sealed record OriginalMapPostEffect3Functor1ChildReturnObservation(OriginalMapPostEffect3ChildOperation Operation,uint? ReturnedValue);

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
        var operations=SourceChildren(factory,callback,locale);
        if(!children.Select(c=>c.Operation).SequenceEqual(operations))
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
    internal static OriginalMapPostEffect3ChildOperation[] SourceChildren(int factory,
        OriginalMapPostEffect3Functor1FactoryCallback callback,string locale="TC")=>(factory,callback) switch{
            (6,OriginalMapPostEffect3Functor1FactoryCallback.Prepare)=>[OriginalMapPostEffect3ChildOperation.LensConstants],
            (6,OriginalMapPostEffect3Functor1FactoryCallback.Initialize)=>[OriginalMapPostEffect3ChildOperation.LensTextures],
            (7,OriginalMapPostEffect3Functor1FactoryCallback.Prepare)=>[OriginalMapPostEffect3ChildOperation.DofShape,OriginalMapPostEffect3ChildOperation.DofConstants],
            (7,OriginalMapPostEffect3Functor1FactoryCallback.Initialize)=>[OriginalMapPostEffect3ChildOperation.DofInitialize],
            (8,OriginalMapPostEffect3Functor1FactoryCallback.Prepare)=>[OriginalMapPostEffect3ChildOperation.ExposureConstants,OriginalMapPostEffect3ChildOperation.BloomConstants],
            (8,OriginalMapPostEffect3Functor1FactoryCallback.Initialize)=>[OriginalMapPostEffect3ChildOperation.BloomLevels,OriginalMapPostEffect3ChildOperation.EyeInitialize,OriginalMapPostEffect3ChildOperation.CachedScalarLookup],
            (9,OriginalMapPostEffect3Functor1FactoryCallback.Prepare)=>[OriginalMapPostEffect3ChildOperation.CompositeConstants,OriginalMapPostEffect3ChildOperation.ToneInfo,OriginalMapPostEffect3ChildOperation.LightInfo],
            (9,OriginalMapPostEffect3Functor1FactoryCallback.Initialize)=>[OriginalMapPostEffect3ChildOperation.CompositeInitialize],
            _=>throw new NotSupportedException("Factory callback requires a supported current operation family.")};
}

/// <summary>The port's typed current cached records. Native does not
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
            Packet(invocation,table,actualPrepared.ConstantBuffer,17,96,OriginalMapPostEffect3ConstantBufferWriter.Lens2);}
        Require();return Make(invocation,6,actualPrepared,Require,OriginalMapPostEffect3ApplyInitialScalars.LensDefaults,[actualPrepared.ConstantBuffer]);
    }
    public OriginalMapPostEffect3Functor1FactoryCpuReturnedRecipe CaptureDepthOfFieldReturned(
        OriginalMapPostEffect3Functor1ModuleInvocation invocation,OriginalMapPostEffect3TextureParameterState table,
        OriginalMapPostEffect3ParameterDepthOfField2Update actualPrepared)
    {
        void Require(){Validate(invocation,table);invocation.RequireCurrentDepthOfField2Prepared(actualPrepared,table);
            Packet(invocation,table,actualPrepared.ConstantBuffer,18,144,OriginalMapPostEffect3ConstantBufferWriter.DepthOfField2);}
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
            actualPrepared.RequireCurrent();Packet(invocation,table,actualPrepared.BloomMainConstants,21,32,OriginalMapPostEffect3ConstantBufferWriter.BloomMain);}
        Require();var exposure=actualExposurePrepared??eyeSource.PrepareExposure(actualPrepared,doubleLog,OriginalNativeLogDispatch.Sse);
        Packet(invocation,table,exposure,24,64,OriginalMapPostEffect3ConstantBufferWriter.Exposure);
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
            Packet(invocation,table,actualPrepared.Prepared.ConstantBuffer,22,208,OriginalMapPostEffect3ConstantBufferWriter.Composite2);
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
        OriginalMapPostEffect3ParameterConstantBuffer packet,int index,int count,OriginalMapPostEffect3ConstantBufferWriter writer)
    {if(packet.FrameNumber!=invocation.FrameNumber||packet.OwnerIdentity!=table.ContextOwnerIdentity||packet.SourceSha256!=table.SourceSha256||
        !ReferenceEquals(packet.Declaration,table.Allocation.ConstantBufferParameters[index])||packet.Declaration.Index!=index||
        packet.Bytes.Length!=count||packet.Locale!=Locale||packet.Writer!=writer)throw new InvalidDataException("Factory CPU writer packet source/identity differs.");}
}

public static class OriginalMapPostEffect3Functor1FactoryReturnSource
{
    public static LoadedOriginalMapPostEffect3Functor1FactoryReturnSource Load(OriginalGameRepository repository,
        LoadedOriginalDeferredSamplerNativeSource native,LoadedOriginalMapPostEffect3EyeAdaptation2Source eye,
        LoadedOriginalNativeDoubleLogSource log)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(eye);ArgumentNullException.ThrowIfNull(log);
        if(native.Locale is not ("TC" or "SC")||eye.Locale!=native.Locale||log.Locale!=native.Locale)
            throw new InvalidDataException("Factory return rules require matching supported locales.");
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

        return new(native.ResourceUri+$";{native.Locale}:paired-source-profile;"+";render-rules="+native.RenderRuleIdentity+
            "factory6;9 actual Prepare/Init return aggregation;-nonnull-stack-output-literal1;CPU source port only",
            actualEye??OriginalMapPostEffect3ParameterEyeAdaptation2.Load(repository,native.Locale),actualLog??LoadedOriginalNativeDoubleLogSource.Load(repository,native.Locale));
    }
}
