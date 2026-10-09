using System.Buffers.Binary;
using System.Security.Cryptography;
using Godot;
using San14.Data;

/// <summary>Borrowed actual input capability. RequireCurrent must validate the
/// producer's completed contents and live owner, not just an allocated RID.
/// The caller retains this capability and its GPU owners until Dispatch returns.</summary>
public interface IOriginalMapPostEffect3ComputeTextureLease
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    int ParameterIndex{get;}
    int InstanceIndex{get;}
    Rid ShaderViewRid{get;}
    Rid StorageRid{get;}
    uint NativeViewFormat{get;}
    RenderingDevice.DataFormat DeviceFormat{get;}
    Vector3I Extent{get;}
    string SourceEvidence{get;}
    void RequireCurrent();
}

/// <summary>The producer proves completed contents for the physical mip range
/// actually exposed to the shader. An allocated full-chain view or a completed
/// different mip does not establish this capability. The base lease retains
/// the full native view; explicit source selectors create temporary views.</summary>
public interface IOriginalMapPostEffect3ComputeMipTextureLease:IOriginalMapPostEffect3ComputeTextureLease
{
    void RequireReadableMipRange(uint firstMipLevel,uint mipLevelCount);
}

/// <summary>An original wrapper whose actual shader-view already limits its
/// physical mip range. This is a producer capability, not a caller binding
/// override. It must prove the selected native wrapper for this pass/register.</summary>
public interface IOriginalMapPostEffect3ComputeSelectedMipTextureLease:IOriginalMapPostEffect3ComputeMipTextureLease
{
    bool IsSourceSelectedMipView{get;}
    uint NativeFirstMipLevel{get;}
    uint NativeMipLevelCount{get;}
    string NativeMipSelectionEvidence{get;}
    void RequireSourceSelectedView(int sourcePassIndex,uint sourceRegister);
}

/// <summary>External table aliases additionally retain the exact source
/// wrapper owner copied by the native assignment, distinct from module owner.</summary>
public interface IOriginalMapPostEffect3ComputeExternalTextureLease:IOriginalMapPostEffect3ComputeTextureLease
{
    string ProducerOwnerIdentity{get;}
}

/// <summary>Actual source sampler selection, independent of CB/type4 rows.
/// No default addressing/filter or program-name inference is provided.</summary>
public interface IOriginalMapPostEffect3ComputeSamplerLease
{
    long FrameNumber{get;}
    string ContextOwnerIdentity{get;}
    int NativeStageOrdinal{get;}
    uint SourceRegister{get;}
    Rid SamplerRid{get;}
    string SourceEvidence{get;}
    void RequireCurrent();
}

public sealed record OriginalMapPostEffect3ComputeFrameInputs(long FrameNumber,
    IReadOnlyList<IOriginalMapPostEffect3ComputeTextureLease> CurrentTextures,
    IReadOnlyList<IOriginalMapPostEffect3ComputeSamplerLease> CurrentSamplers,
    IReadOnlyDictionary<int,int> ConstantBufferInstances,
    IReadOnlyDictionary<int,int> OutputTextureInstances)
{
    public OriginalMapPostEffect3TextureParameterState? TextureParameters{get;init;}
    internal IReadOnlyDictionary<int,OriginalMapPostEffect3ResolvedTextureParameter>? AcceptedTextureParameters{get;init;}
    internal IReadOnlyList<OriginalMapPostEffect3NullShaderResource> AcceptedNullShaderResources{get;init;}=Array.Empty<OriginalMapPostEffect3NullShaderResource>();
    public IReadOnlyList<IOriginalMapPostEffect3ComputeBufferLease> CurrentBuffers{get;init;}=Array.Empty<IOriginalMapPostEffect3ComputeBufferLease>();
    public IReadOnlyDictionary<int,int> OutputBufferInstances{get;init;}=new Dictionary<int,int>();
    public OriginalMapPostEffect3LensExecutionRecipe? LensExecution{get;init;}
    public LoadedOriginalMapPostEffect3PassRegisterResetSource? NativeRegisterResetSource{get;init;}
    /// <summary>Exact original CS38 value-influence proof. Its old UAV load
    /// still executes, but only this actual CB/source branch has no dependence
    /// on that unknown value. It grants no prior texture read capability.</summary>
    public OriginalMapPostEffect3ToneInfoRegionWriteCoverage? ToneInfoSourceValueInfluence{get;init;}
    /// <summary>Exact CS52/CB24 branch proof that the old exposure UAV load
    /// does not execute. AutoExposure=0 alone grants no such capability.</summary>
    public OriginalMapPostEffect3EyeComputeWriteCoverage? EyeExposureSourceValueInfluence{get;init;}
    /// <summary>Exact first source104 load of owned61's originally undefined
    /// allocation contents. This grants no initialized prior-pixel lease.</summary>
    public OriginalMapPostEffect3FreshUndefinedEyeExposureRead? FreshUndefinedEyeExposureRead{get;init;}
    public OriginalMapPostEffect3RetainedControlledExposureRead? RetainedControlledExposureRead{get;init;}
    /// <summary>Owned original LF2 pass source. Only its exact FFT program
    /// can establish source-initialized local storage; external inputs retain
    /// their completed producer requirements.</summary>
    public OriginalMapPostEffect3LensFlare2GpuPassSource? LensFlare2Execution{get;init;}
    public OriginalMapPostEffect3ParameterDepthOfField2Update? DepthOfField2Prepared{get;init;}
    public OriginalMapPostEffect3DepthOfField2BeforeBindings? DepthOfField2Before{get;init;}
}

/// <summary>Successful source dispatch/write submission. It does not establish
/// full-resource coverage, initialize padding/other mips, evaluate a factory
/// predicate, flip history or commit Host completion. Read consumers still need
/// a source-specific content capability for the portions they will actually read.</summary>
public sealed record OriginalMapPostEffect3ComputeWriteReceipt(int ParameterIndex,int InstanceIndex,
    Rid StorageRid,Rid ViewRid,uint NativeViewFormat,Vector3I Extent)
{
    public int PhysicalAllocationParameterIndex{get;init;}=ParameterIndex;
    public uint MipLevel{get;init;}
    public uint MipLevelCount{get;init;}=1;
    /// <summary>ViewRid is a historical dispatch identity when true. Future
    /// consumers must acquire their own view of StorageRid/MipLevel.</summary>
    public bool ViewRetiredAfterSubmission{get;init;}
    public long WriterRevision{get;init;}
}
public sealed record OriginalMapPostEffect3ComputeCompletedPass(long FrameNumber,string ContextOwnerIdentity,
    int SourcePassIndex,string ProgramSha256,OriginalMapPostEffect3DispatchExtent Dispatch,
    Vector3I ActualShaderLocalSize,IReadOnlyList<int> OrderedNativeBindingRows,
    IReadOnlyList<OriginalMapPostEffect3ComputeWriteReceipt> Writes,string SourceEvidence)
{
    public bool EstablishesCompleteResourceContents=>false;
    public bool CommitsFunctorHistory=>false;
    public IReadOnlyList<OriginalMapPostEffect3ComputeBufferWriteReceipt> BufferWrites{get;init;}=Array.Empty<OriginalMapPostEffect3ComputeBufferWriteReceipt>();
    public IReadOnlyList<OriginalMapPostEffect3ComputeBufferReadReceipt> BufferReads{get;init;}=Array.Empty<OriginalMapPostEffect3ComputeBufferReadReceipt>();
    public IReadOnlyList<OriginalMapPostEffect3ComputeConstantBufferReadReceipt> ConstantBufferReads{get;init;}=Array.Empty<OriginalMapPostEffect3ComputeConstantBufferReadReceipt>();
    /// <summary>Immutable copy of the source setters actually selected before
    /// submission, including separately proved Before overrides (-1 rows).</summary>
    public IReadOnlyList<OriginalMapPostEffect3Binding> OrderedEffectiveBindings{get;init;}=Array.Empty<OriginalMapPostEffect3Binding>();
    public IReadOnlyList<OriginalMapPostEffect3NullShaderResource> SourceNullShaderResources{get;init;}=Array.Empty<OriginalMapPostEffect3NullShaderResource>();
}
public sealed record OriginalMapPostEffect3ComputeBufferReadReceipt(int ParameterIndex,int InstanceIndex,Rid StorageRid,
    uint ByteCount,long UploadRevision,string? CompleteBytesSha256);
public sealed record OriginalMapPostEffect3ComputeConstantBufferReadReceipt(int ParameterIndex,int InstanceIndex,Rid StorageRid,
    long UploadRevision,bool HasCompleteWriterContents,string? CompleteBytesSha256);
public sealed record OriginalMapPostEffect3ComputeBufferWriteReceipt(int ParameterIndex,int InstanceIndex,Rid StorageRid,
    uint ByteCount,long WriterRevision,bool SelectedShaderDeclaresWrite)
{
    public bool EstablishesCompleteResourceContents=>false;
    public bool SupportsIndirectArgumentExecution=>false;
}

/// <summary>Actual isolated CS automatic executor. Uses the original selected
/// program, native counted binding order and dispatch extent. Currently admits
/// ordinary2D sampled/storage images, actual texel-buffer views and source-ready
/// owned CBs and exact ordinary2D mip views; graphics/custom/indirect draws,
/// counters, implicit CBs and unproved mip contents reject. Storage reads need
/// completed buffer input leases.
/// Owns pipelines/sets only and borrows resource owners.</summary>
public sealed class OriginalMapPostEffect3ComputeExecutor:IAsyncDisposable
{
    public OriginalMapPostEffect3ProgramLibrary Library{get;}
    public OriginalMapPostEffect3ResourceBank Bank{get;}
    public OriginalMapPostEffect3ComputeCompletedPass? LastCompletedPass{get;private set;}
    private readonly SemaphoreSlim serial=new(1,1);
    private readonly object lifetime=new();private int accepted;private bool retiring;
    private TaskCompletionSource? drained;private Task? disposal;
    private readonly Dictionary<(int Pass,Rid Shader),Rid> pipelines=[];
    private sealed record FrozenTexture(IOriginalMapPostEffect3ComputeTextureLease Producer,
        long FrameNumber,string ContextOwnerIdentity,int ParameterIndex,int InstanceIndex,
        Rid ShaderViewRid,Rid StorageRid,uint NativeViewFormat,RenderingDevice.DataFormat DeviceFormat,
        Vector3I Extent,string SourceEvidence,string? ExternalOwnerIdentity,bool IsSourceSelectedMipView,
        uint NativeFirstMipLevel,uint NativeMipLevelCount,string NativeMipSelectionEvidence):IOriginalMapPostEffect3ComputeExternalTextureLease,IOriginalMapPostEffect3ComputeSelectedMipTextureLease,
        IOriginalMapPostEffect3SharedSampledViewLease
    {
        public string ProducerOwnerIdentity=>ExternalOwnerIdentity??throw new NotSupportedException("External texture lease has no copied wrapper identity.");
        public OriginalMapPostEffect3SharedSampledViewProof RequireSharedSampledViewProof()
        {
            RequireCurrent();
            var proof=(Producer as IOriginalMapPostEffect3SharedSampledViewLease??
                throw new InvalidDataException("Accepted compute texture producer has no actual shared sampled view proof.")).RequireSharedSampledViewProof();
            if(proof.StorageRid!=StorageRid||proof.ShaderViewRid!=ShaderViewRid||
                proof.EffectiveShaderFormat!=DeviceFormat||NativeViewFormat!=91)
                throw new InvalidDataException("Accepted compute shared sampled view differs from its frozen source view/format.");
            return proof;
        }
        public void RequireReadableMipRange(uint first,uint count)
        {
            RequireCurrent();
            if(Producer is IOriginalMapPostEffect3ComputeMipTextureLease mip)mip.RequireReadableMipRange(first,count);
            else throw new NotSupportedException("Accepted texture producer does not prove this actual readable mip range.");
        }
        public void RequireSourceSelectedView(int sourcePass,uint register)
        {
            RequireCurrent();
            if(!IsSourceSelectedMipView||Producer is not IOriginalMapPostEffect3ComputeSelectedMipTextureLease selected)
                throw new NotSupportedException("No original producer establishes this selected mip wrapper.");
            selected.RequireSourceSelectedView(sourcePass,register);
        }
        public void RequireCurrent()
        {
            Producer.RequireCurrent();
            if(Producer.FrameNumber!=FrameNumber||Producer.ContextOwnerIdentity!=ContextOwnerIdentity||
                Producer.ParameterIndex!=ParameterIndex||Producer.InstanceIndex!=InstanceIndex||
                Producer.ShaderViewRid!=ShaderViewRid||Producer.StorageRid!=StorageRid||
                Producer.NativeViewFormat!=NativeViewFormat||Producer.DeviceFormat!=DeviceFormat||
                Producer.Extent!=Extent||Producer.SourceEvidence!=SourceEvidence||
                (Producer as IOriginalMapPostEffect3ComputeExternalTextureLease)?.ProducerOwnerIdentity!=ExternalOwnerIdentity||
                ((Producer as IOriginalMapPostEffect3ComputeSelectedMipTextureLease)?.IsSourceSelectedMipView??false)!=IsSourceSelectedMipView||
                ((Producer as IOriginalMapPostEffect3ComputeSelectedMipTextureLease)?.NativeFirstMipLevel??0)!=NativeFirstMipLevel||
                ((Producer as IOriginalMapPostEffect3ComputeSelectedMipTextureLease)?.NativeMipLevelCount??0)!=NativeMipLevelCount||
                ((Producer as IOriginalMapPostEffect3ComputeSelectedMipTextureLease)?.NativeMipSelectionEvidence??"")!=NativeMipSelectionEvidence)
                throw new InvalidDataException("Accepted3SPK texture lease changed while its dispatch was queued.");
        }
    }
    private sealed record FrozenSampler(IOriginalMapPostEffect3ComputeSamplerLease Producer,
        long FrameNumber,string ContextOwnerIdentity,int NativeStageOrdinal,uint SourceRegister,Rid SamplerRid,
        string SourceEvidence):IOriginalMapPostEffect3ComputeSamplerLease
    {
        public void RequireCurrent()
        {
            Producer.RequireCurrent();
            if(Producer.FrameNumber!=FrameNumber||Producer.ContextOwnerIdentity!=ContextOwnerIdentity||
                Producer.NativeStageOrdinal!=NativeStageOrdinal||Producer.SourceRegister!=SourceRegister||
                Producer.SamplerRid!=SamplerRid||Producer.SourceEvidence!=SourceEvidence)
                throw new InvalidDataException("Accepted3SPK sampler lease changed while its dispatch was queued.");
        }
    }
    private sealed record FrozenBuffer(IOriginalMapPostEffect3ComputeBufferLease Producer,long FrameNumber,
        string ContextOwnerIdentity,int ParameterIndex,int InstanceIndex,Rid StorageRid,Rid ShaderReadRid,RenderingDevice.DataFormat DeviceTexelFormat,
        uint ByteCount,long UploadRevision,string SourceEvidence):IOriginalMapPostEffect3ComputeBufferLease
    {
        public void RequireCurrent()
        {Producer.RequireCurrent();if(Producer.FrameNumber!=FrameNumber||Producer.ContextOwnerIdentity!=ContextOwnerIdentity||
            Producer.ParameterIndex!=ParameterIndex||Producer.InstanceIndex!=InstanceIndex||Producer.StorageRid!=StorageRid||Producer.ShaderReadRid!=ShaderReadRid||
            Producer.DeviceTexelFormat!=DeviceTexelFormat||Producer.ByteCount!=ByteCount||Producer.UploadRevision!=UploadRevision||
            Producer.SourceEvidence!=SourceEvidence)throw new InvalidDataException("Accepted original buffer lease changed while queued.");}
        public void RequireReadRange(uint offset,uint count){RequireCurrent();Producer.RequireReadRange(offset,count);}
    }
    private OriginalMapPostEffect3ComputeExecutor(OriginalMapPostEffect3ProgramLibrary library,
        OriginalMapPostEffect3ResourceBank bank)=>(Library,Bank)=(library,bank);

    public static OriginalMapPostEffect3ComputeExecutor Create(OriginalMapPostEffect3ProgramLibrary borrowedLibrary,
        OriginalMapPostEffect3ResourceBank borrowedBank)
    {
        ArgumentNullException.ThrowIfNull(borrowedLibrary);ArgumentNullException.ThrowIfNull(borrowedBank);
        if(borrowedLibrary.SourceSha256!=borrowedBank.Source.Source.Sha256)
            throw new InvalidDataException("3SPK compute library and allocation bank have different source identities.");
        return new(borrowedLibrary,borrowedBank);
    }

    public Task<OriginalMapPostEffect3ComputeCompletedPass> DispatchAsync(int sourcePassIndex,
        OriginalMapPostEffect3ComputeFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if(input.FrameNumber<=0||(uint)sourcePassIndex>=Bank.Source.Passes.Count)
            throw new ArgumentOutOfRangeException(nameof(sourcePassIndex));
        RequireDepthOfField2ComputeSource(sourcePassIndex,input);
        if(input.FreshUndefinedEyeExposureRead is{} freshExposure)
        {
            freshExposure.RequireCurrent();
            freshExposure.Source.RequireCurrentTextureTable(input.TextureParameters??
                throw new InvalidDataException("First undefined exposure load requires its actual texture table."));
            if(sourcePassIndex!=104||!ReferenceEquals(freshExposure.Bank,Bank)||freshExposure.FrameNumber!=input.FrameNumber||
                input.EyeExposureSourceValueInfluence is not null||input.ToneInfoSourceValueInfluence is not null||
                input.LensExecution is not null||input.LensFlare2Execution is not null||
                input.DepthOfField2Prepared is not null||input.DepthOfField2Before is not null)
                throw new InvalidDataException("First undefined exposure admission cannot supply another factory/source invocation.");
        }
        if(input.RetainedControlledExposureRead is{} retainedInput)
        {
            retainedInput.RequireReadableMipRange(0,1);
            if(sourcePassIndex!=104||retainedInput.Pass!=104||!ReferenceEquals(retainedInput.Snapshot.Bank,Bank)||
                retainedInput.FrameNumber!=input.FrameNumber||input.FreshUndefinedEyeExposureRead is not null||
                input.EyeExposureSourceValueInfluence is not null||input.ToneInfoSourceValueInfluence is not null||
                input.LensExecution is not null||input.LensFlare2Execution is not null||
                input.DepthOfField2Prepared is not null||input.DepthOfField2Before is not null)
                throw new InvalidDataException("Retained actual exposure cannot supply another source factory/proof.");
        }
        if(input.LensExecution is{} lens&&(lens.FrameNumber!=input.FrameNumber||lens.SourcePassIndex!=sourcePassIndex||
            lens.SourceSha256!=Bank.Source.Source.Sha256||lens.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||!lens.ExecutesPass))
            throw new InvalidDataException("Compute Lens recipe is another frame/pass/source owner or a native Before-skipped pass.");
        IReadOnlyList<OriginalMapPostEffect3NullShaderResource> nulls=input.TextureParameters is{} nullTable?
            OriginalMapPostEffect3NullShaderResource.CapturePassReads(Bank.Source,nullTable,sourcePassIndex,5):
            Array.Empty<OriginalMapPostEffect3NullShaderResource>();
        if(input.NativeRegisterResetSource is{} reset&&input.LensExecution is{} actualRecipe)
        {
            var resetTable=input.TextureParameters??throw new InvalidDataException("Native reset-null source requires this actual frame texture table.");
            nulls=Array.AsReadOnly(nulls.Concat(reset.CaptureUnassignedLensReads(Bank.Source,resetTable,actualRecipe)).ToArray());
        }
        var acceptedInput=input with{AcceptedNullShaderResources=nulls,CurrentTextures=Array.AsReadOnly(input.CurrentTextures.Select(t=>
            (IOriginalMapPostEffect3ComputeTextureLease)new FrozenTexture(t,t.FrameNumber,t.ContextOwnerIdentity,
                t.ParameterIndex,t.InstanceIndex,t.ShaderViewRid,t.StorageRid,t.NativeViewFormat,t.DeviceFormat,t.Extent,t.SourceEvidence,
                (t as IOriginalMapPostEffect3ComputeExternalTextureLease)?.ProducerOwnerIdentity,
                (t as IOriginalMapPostEffect3ComputeSelectedMipTextureLease)?.IsSourceSelectedMipView??false,
                (t as IOriginalMapPostEffect3ComputeSelectedMipTextureLease)?.NativeFirstMipLevel??0,
                (t as IOriginalMapPostEffect3ComputeSelectedMipTextureLease)?.NativeMipLevelCount??0,
                (t as IOriginalMapPostEffect3ComputeSelectedMipTextureLease)?.NativeMipSelectionEvidence??"")).ToArray()),
            CurrentSamplers=Array.AsReadOnly(input.CurrentSamplers.Select(s=>
                (IOriginalMapPostEffect3ComputeSamplerLease)new FrozenSampler(s,s.FrameNumber,s.ContextOwnerIdentity,
                    s.NativeStageOrdinal,s.SourceRegister,s.SamplerRid,s.SourceEvidence)).ToArray()),
            ConstantBufferInstances=new Dictionary<int,int>(input.ConstantBufferInstances),
            OutputTextureInstances=new Dictionary<int,int>(input.OutputTextureInstances),
            CurrentBuffers=Array.AsReadOnly(input.CurrentBuffers.Select(b=>(IOriginalMapPostEffect3ComputeBufferLease)
                new FrozenBuffer(b,b.FrameNumber,b.ContextOwnerIdentity,b.ParameterIndex,b.InstanceIndex,b.StorageRid,b.ShaderReadRid,
                    b.DeviceTexelFormat,b.ByteCount,b.UploadRevision,b.SourceEvidence)).ToArray()),
            OutputBufferInstances=new Dictionary<int,int>(input.OutputBufferInstances),
            AcceptedTextureParameters=input.TextureParameters is{} table?
                input.CurrentTextures.Select(t=>t.ParameterIndex).Concat(input.OutputTextureInstances.Keys).Distinct()
                    .ToDictionary(i=>i,i=>table.Require(i)):null};
        if(input.TextureParameters is{} state&&(state.FrameNumber!=input.FrameNumber||
            state.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||state.SourceSha256!=Bank.Source.Source.Sha256))
            throw new InvalidDataException("Accepted runtime texture table belongs to another current source/frame.");
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}
        return DispatchAcceptedAsync(sourcePassIndex,acceptedInput);
    }

    private async Task<OriginalMapPostEffect3ComputeCompletedPass> DispatchAcceptedAsync(int index,
        OriginalMapPostEffect3ComputeFrameInputs input)
    {
        await serial.WaitAsync();Rid set=default;IDisposable? bufferScope=null;
        var temporaryMipViews=new List<Rid>();
        try
        {
            LastCompletedPass=null;
            RequireDepthOfField2ComputeSource(index,input);
            input.FreshUndefinedEyeExposureRead?.RequireCurrent();
            input.RetainedControlledExposureRead?.RequireReadableMipRange(0,1);
            var links=Bank.Source.Passes[index];
            var dispatch=input.LensExecution?.ActualDispatch??links.DispatchExtent;
            if(links.Pass.ProgramReferences.Where(r=>r.StageIndex is not null).ToArray() is not [{StageCode:5}]||
                dispatch is not {X:>0,Y:>0,Z:>0})
                throw new NotSupportedException("3SPK automatic compute requires a sole actual CS and its positive source dispatch extent.");
            foreach(var n in input.AcceptedNullShaderResources)n.ValidateCurrent();
            var compiled=await Library.GetCompiledPassAsync(index,input.AcceptedNullShaderResources);
            if(compiled.Kind!=OriginalMapPostEffect3CompiledPassKind.Compute||compiled.ComputeLocalSize is not { } local)
                throw new InvalidDataException("Actual3SPK selected CS did not compile to its compute interface.");
            var stage=compiled.Stages.Single();var declarations=stage.Bindings.Bindings;
            if(stage.DofSpriteStorageAdaptation is{} acceptedStorage&&
                (acceptedStorage.SourceProgramSha256!=stage.Source.Sha256||
                 acceptedStorage.AdaptedSpirvSha256!=stage.DeviceTranslation.SpirvSha256||
                 Convert.ToHexString(SHA256.HashData(stage.DeviceTranslation.Bytes))!=acceptedStorage.AdaptedSpirvSha256))
                throw new InvalidDataException("Exact DOF2 storage adapter changed after selected shader compilation.");
            if(declarations.Any(d=>d.DescriptorKind is not ("UniformBuffer" or "Sampler" or "Texture" or "StorageImage" or "TextureBuffer" or "StorageBuffer")||
                    d.Device.DescriptorSet!=0))
                throw new NotSupportedException("Selected3SPK compute requires an unimplemented live buffer or descriptor interface.");
            var native=(input.LensExecution?.OrderedEffectiveBindings??links.ActiveBindings.ToArray()).Where(b=>(b.StageMask&1)!=0).ToArray();
            if(native.Any(b=>b.Role==OriginalMapPostEffect3BindingRole.RenderTargetWrite))
                throw new NotSupportedException("Selected3SPK compute has a native buffer/custom target binding outside this source scope.");
            var reads=native.Where(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead).ToArray();
            var nullRegisters=input.AcceptedNullShaderResources.Select(n=>n.SourceRegister).ToHashSet();
            var nullIndices=input.AcceptedNullShaderResources.Where(n=>n.ParameterIndex is not null).Select(n=>n.ParameterIndex!.Value).ToHashSet();
            foreach(var n in input.AcceptedNullShaderResources.Where(n=>n.Binding is not null))
            {
                var actual=reads.LastOrDefault(r=>r.RegisterIndex==n.SourceRegister);
                if(actual is null||actual.ParameterIndex!=n.ParameterIndex||!actual.RawBytes.Span.SequenceEqual(n.Binding!.RawBytes.Span))
                    throw new NotSupportedException("Null SRV source counted setter differs from an actual native Before override.");
            }
            var writes=native.Where(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&b.Role==OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite).ToArray();
            var bufferRows=native.Where(b=>b.Table==OriginalMapPostEffect3BindingTable.BufferParameters).ToArray();
            // Hold all bank owners, including shared texture mip views, through
            // queued descriptor execution and reverse-order retirement.
            bufferScope=await Bank.AcquireBufferExecutionScopeAsync();
            int[] requiredBufferReads=bufferRows.Where(b=>b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead)
                .Select(b=>b.ParameterIndex).Distinct().Order().ToArray();
            int[] requiredBufferWrites=bufferRows.Where(b=>b.Role==OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite)
                .Select(b=>b.ParameterIndex).Distinct().Order().ToArray();
            if(input.CurrentBuffers.Select(b=>b.ParameterIndex).Distinct().Count()!=input.CurrentBuffers.Count||
                requiredBufferReads.Any(i=>!input.CurrentBuffers.Any(b=>b.ParameterIndex==i))||
                input.CurrentBuffers.Any(b=>!requiredBufferReads.Contains(b.ParameterIndex)&&!requiredBufferWrites.Contains(b.ParameterIndex))||
                !input.OutputBufferInstances.Keys.Order().SequenceEqual(requiredBufferWrites))
                throw new InvalidDataException("Compute buffer inputs/outputs must match actual native selected buffer rows.");
            var bufferInputs=input.CurrentBuffers.ToDictionary(b=>b.ParameterIndex);
            var bufferReadReceipts=new List<OriginalMapPostEffect3ComputeBufferReadReceipt>();
            var bufferOutputs=new Dictionary<int,OriginalMapPostEffect3DeviceBuffer>();
            foreach(var b in input.CurrentBuffers)
            {
                b.RequireCurrent();var owned=Bank.RequireCurrentBuffer(b.ParameterIndex,b.InstanceIndex,input.FrameNumber);
                if(b.FrameNumber!=input.FrameNumber||b.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
                    b.StorageRid!=owned.StorageRid||b.DeviceTexelFormat!=owned.DeviceTexelFormat||b.ByteCount!=owned.Source.ByteCount||
                    b.UploadRevision!=owned.UploadRevision||!owned.NativeAllowsShaderRead||string.IsNullOrWhiteSpace(b.SourceEvidence))
                    throw new InvalidDataException("Compute buffer read needs the exact live same-frame original writer owner.");
                b.RequireReadRange(0,b.ByteCount);
                bufferReadReceipts.Add(new(b.ParameterIndex,b.InstanceIndex,b.StorageRid,b.ByteCount,b.UploadRevision,owned.CompleteBytesSha256));
            }
            foreach(int i in requiredBufferWrites)
            {
                var b=Bank.RequireBuffer(i,input.OutputBufferInstances[i]);
                if(!b.NativeAllowsUnorderedAccess)throw new InvalidDataException("Selected source buffer has no native UAV role.");
                if(bufferInputs.TryGetValue(i,out var read)&&read.StorageRid!=b.StorageRid)
                    throw new InvalidDataException("Storage buffer read/write owners differ.");
                bufferOutputs.Add(i,b);
            }
            int[] readIndices=reads.Select(b=>b.ParameterIndex).Where(i=>!nullIndices.Contains(i)).Distinct().Order().ToArray();
            if(!input.CurrentTextures.Select(t=>t.ParameterIndex).Order().SequenceEqual(readIndices)||
                input.CurrentTextures.Any(t=>t.FrameNumber!=input.FrameNumber||t.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
                    t.Extent.X<=0||t.Extent.Y<=0||t.Extent.Z!=1||!t.ShaderViewRid.IsValid||!t.StorageRid.IsValid||
                    t.NativeViewFormat==0||string.IsNullOrWhiteSpace(t.SourceEvidence)))
                throw new InvalidDataException("3SPK compute inputs need exact same-frame completed2D source texture leases.");
            var samplerRegisters=declarations.Where(d=>d.DescriptorKind=="Sampler").Select(d=>d.Source.BindPoint).Order().ToArray();
            if(!input.CurrentSamplers.Select(s=>s.SourceRegister).Order().SequenceEqual(samplerRegisters)||
                input.CurrentSamplers.Any(s=>s.FrameNumber!=input.FrameNumber||s.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
                    s.NativeStageOrdinal!=0||!s.SamplerRid.IsValid||string.IsNullOrWhiteSpace(s.SourceEvidence)))
                throw new InvalidDataException("3SPK compute needs its actual same-frame selected sampler leases.");
            foreach(var t in input.CurrentTextures)t.RequireCurrent();foreach(var s in input.CurrentSamplers)s.RequireCurrent();
            ValidateTextureTable();
            foreach(var t in input.CurrentTextures)
            {
                if(t.DeviceFormat!=SourceViewFormat(t.NativeViewFormat))
                    throw new InvalidDataException("3SPK input lease's native format does not match its actual device view.");
                var reference=input.AcceptedTextureParameters?[t.ParameterIndex].Reference;
                int? physical=reference is not null?reference.PhysicalAllocationParameterIndex:
                    Bank.Source.TextureParameters[t.ParameterIndex].Ownership==OriginalMapPostEffect3AllocationOwnership.OwnedDeclaration?t.ParameterIndex:null;
                if(reference is not null&&reference.InstanceIndex!=t.InstanceIndex)
                    throw new InvalidDataException("Current native texture alias has another actual side.");
                if(physical is{} owner)
                {
                    var owned=Bank.RequireTexture(owner,t.InstanceIndex);
                    if(owned.StorageRid!=t.StorageRid||!owned.NativeFormatViews.TryGetValue(t.NativeViewFormat,out var view)||
                        view!=t.ShaderViewRid&&t is not IOriginalMapPostEffect3ComputeSelectedMipTextureLease{IsSourceSelectedMipView:true})
                        throw new InvalidDataException("3SPK owned read lease is not this exact allocated side/native view.");
                }
                else if(reference is not null&&
                    (t is not IOriginalMapPostEffect3ComputeExternalTextureLease external||external.ProducerOwnerIdentity!=reference.ProducerOwnerIdentity))
                    throw new InvalidDataException("Native external alias is not the exact completed wrapper producer copied by source.");
            }
            int[] outputIndices=writes.Select(b=>b.ParameterIndex).Distinct().Order().ToArray();
            if(!input.OutputTextureInstances.Keys.Order().SequenceEqual(outputIndices))
                throw new InvalidDataException("3SPK compute output side must be supplied for exactly the native writes.");
            var outputs=new Dictionary<int,OriginalMapPostEffect3DeviceTexture>();
            foreach(int i in outputIndices)
            {
                var reference=input.AcceptedTextureParameters?[i].Reference;
                if(reference is not null&&(reference.PhysicalAllocationParameterIndex is null||
                    reference.InstanceIndex!=input.OutputTextureInstances[i]))
                    throw new NotSupportedException("Current native UAV alias needs its exact owned physical allocation/side.");
                var t=Bank.RequireTexture(reference?.PhysicalAllocationParameterIndex??i,input.OutputTextureInstances[i]);
                if(!t.Shape.AllowsUnorderedAccess||t.Shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||
                    t.Shape.Formats.UnorderedAccessFormat==0)
                    throw new NotSupportedException("3SPK compute UAV needs this exact owned2D native image.");
                outputs.Add(i,t);
            }
            var cbs=new Dictionary<int,OriginalMapPostEffect3DeviceConstantBuffer>();
            int[] cbIndices=native.Where(b=>b.Role==OriginalMapPostEffect3BindingRole.ConstantBufferRead)
                .Select(b=>b.ParameterIndex).Distinct().Order().ToArray();
            if(!input.ConstantBufferInstances.Keys.Order().SequenceEqual(cbIndices))
                throw new InvalidDataException("3SPK compute CB sides must match all native current bindings.");
            foreach(int i in cbIndices)cbs.Add(i,Bank.RequireConstantBufferForPass(i,input.ConstantBufferInstances[i],index,input.FrameNumber));
            var cbRevisions=cbs.ToDictionary(p=>p.Key,p=>p.Value.UploadRevision);
            var cbReadReceipts=cbs.Select(pair=>new OriginalMapPostEffect3ComputeConstantBufferReadReceipt(pair.Key,
                pair.Value.InstanceIndex,pair.Value.StorageRid,pair.Value.UploadRevision,pair.Value.HasCompleteWriterContents,
                pair.Value.CompleteBytesSha256)).ToArray();
            if(input.DepthOfField2Prepared is{} dofPrepared)
            {
                string expectedDofCb=Convert.ToHexString(SHA256.HashData(dofPrepared.ConstantBuffer.Bytes.Span));
                if(dofPrepared.ConstantBuffer.SourceSha256!=Library.SourceSha256||
                    (index==58||cbs.ContainsKey(18))&&
                    (!cbs.TryGetValue(18,out var dofCb)||!dofCb.HasCompleteWriterContents||dofCb.CompleteBytesSha256!=expectedDofCb||
                     dofCb.Source.Parameter.ByteCount!=144||dofCb.CompleteFrameNumber!=input.FrameNumber||
                     !cbReadReceipts.Any(c=>c.ParameterIndex==18&&c.HasCompleteWriterContents&&c.CompleteBytesSha256==expectedDofCb)))
                    throw new InvalidDataException("DOF2 compute metadata CB18 upload differs from the actual Prepared packet.");
                // CS45 declares no CB read. The source counted setter/upload
                // remains exact; this does not manufacture a shader RDEF read.
            }
            // Ordered setters remain intact. Only the final binding of each
            // register becomes shader-visible, including native Before overrides.
            var readViews=new Dictionary<uint,OriginalMapPostEffect3ComputeMipView>();
            var writeViews=new Dictionary<uint,(OriginalMapPostEffect3Binding Row,OriginalMapPostEffect3ComputeMipView View)>();
            foreach(var row in reads.GroupBy(r=>r.RegisterIndex).Select(g=>g.Last()))
            {
                if(nullRegisters.Contains(checked((uint)row.RegisterIndex)))continue;
                var current=input.CurrentTextures.Single(t=>t.ParameterIndex==row.ParameterIndex);
                if(current is IOriginalMapPostEffect3ComputeSelectedMipTextureLease{IsSourceSelectedMipView:true} selected)
                    selected.RequireSourceSelectedView(index,checked((uint)row.RegisterIndex));
                // The kind1 shader-resource binding writes FF
                // rather than consuming the counted row's mip column. The
                // resulting shader wrapper+10 is its full native SRV. Keep
                // LOD semantics intact even when that unused column is >=0.
                OriginalMapPostEffect3LensTextureReadFootprint? lensMipFootprint=null;
                if(current is FrozenTexture{Producer:IOriginalMapPostEffect3LensMipReadLease sourceMip})
                {
                    lensMipFootprint=sourceMip.RequireLensMipFootprint(index,checked((uint)row.RegisterIndex));
                    sourceMip.RequireReadableSourceFootprint(index,checked((uint)row.RegisterIndex),stage.Source.Sha256,cbReadReceipts);
                    var expected=lensMipFootprint.NativeBinding;
                    if(lensMipFootprint.SourceSha256!=Bank.Source.Source.Sha256||expected.ParameterIndex!=row.ParameterIndex||
                        expected.RegisterIndex!=row.RegisterIndex||expected.MipSelector!=row.MipSelector||
                        expected.SliceSelector!=row.SliceSelector||expected.NativeViewKind!=row.NativeViewKind||
                        expected.RawFlags0!=row.RawFlags0||expected.StageMask!=row.StageMask)
                        throw new InvalidDataException("Lens mip read proof differs from the actual last effective native setter.");
                    if(lensMipFootprint.RequiresActualSamplerProducer)
                    {
                        var sampler=input.CurrentSamplers.Single(s=>s.SourceRegister==lensMipFootprint.RequiredSamplerRegister);
                        if(sampler is not FrozenSampler{Producer:IOriginalMapPostEffect3SourceMipSamplerLease sourceSampler})
                            throw new NotSupportedException("Lens explicit SampleLevel needs its actual native sampler filter/LOD producer.");
                        sourceSampler.RequireSourceMipSampling(lensMipFootprint.RequiredSamplerFilter!.Value,
                            lensMipFootprint.RequiredSamplerMipLodBiasBits!.Value,lensMipFootprint.RequiredSamplerMinimumLodBits!.Value,
                            lensMipFootprint.RequiredSamplerMaximumLodBits!.Value);
                    }
                }
                var view=await Bank.CreateComputeReadMipViewAsync(current,
                    -1,row.SliceSelector,lensMipFootprint);
                if(view.RetireAfterSubmission)temporaryMipViews.Add(view.ViewRid);
                readViews.Add(checked((uint)row.RegisterIndex),view);
            }
            foreach(var row in writes.GroupBy(r=>r.RegisterIndex).Select(g=>g.Last()))
            {
                var view=await Bank.CreateComputeWriteMipViewAsync(outputs[row.ParameterIndex],row.MipSelector,row.SliceSelector);
                if(view.RetireAfterSubmission)temporaryMipViews.Add(view.ViewRid);
                if(readViews.Values.Any(r=>r.StorageRid==view.StorageRid&&
                    r.FirstMipLevel<view.FirstMipLevel+view.MipLevelCount&&view.FirstMipLevel<r.FirstMipLevel+r.MipLevelCount))
                    throw new InvalidDataException("Original compute sampled/storage views overlap the same physical mip subresource.");
                writeViews.Add(checked((uint)row.RegisterIndex),(row,view));
            }
            var mapped=new Dictionary<(string Kind,uint Register),Rid>();
            foreach(var row in native)
            {
                if(row.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
                    row.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&nullRegisters.Contains(checked((uint)row.RegisterIndex)))continue;
                // Preserve native ordered setters. If a register is rebound,
                // its final actual native row is the shader-visible resource.
                string kind=row.Table==OriginalMapPostEffect3BindingTable.BufferParameters?row.Role switch{
                    OriginalMapPostEffect3BindingRole.ShaderResourceRead=>"TextureBuffer",
                    OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite=>"StorageBuffer",
                    _=>throw new NotSupportedException("Native buffer role has no explicit texel interface.")}:row.Role switch{
                    OriginalMapPostEffect3BindingRole.ShaderResourceRead=>"Texture",
                    OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite=>"StorageImage",
                    OriginalMapPostEffect3BindingRole.ConstantBufferRead=>"UniformBuffer",
                    _=>throw new NotSupportedException("Unsupported native compute binding role.")};
                Rid rid=kind switch{
                    "Texture"=>readViews[checked((uint)row.RegisterIndex)].ViewRid,
                    "StorageImage"=>writeViews[checked((uint)row.RegisterIndex)].View.ViewRid,
                    "TextureBuffer"=>bufferInputs[row.ParameterIndex].ShaderReadRid,
                    "StorageBuffer"=>bufferOutputs[row.ParameterIndex].StorageRid,
                    _=>cbs[row.ParameterIndex].StorageRid};
                mapped[(kind,(uint)row.RegisterIndex)]=rid;
            }
            foreach(var s in input.CurrentSamplers)mapped.Add(("Sampler",s.SourceRegister),s.SamplerRid);
            foreach(var d in declarations)
                if(!mapped.ContainsKey((d.DescriptorKind,d.Source.BindPoint)))
                    throw new NotSupportedException("Actual selected CS reads an implicit backend CB/resource without a current producer.");
            foreach(var d in declarations.Where(d=>d.DescriptorKind is "TextureBuffer" or "StorageBuffer"))
            {
                bool write=d.DescriptorKind=="StorageBuffer";
                var row=bufferRows.Last(r=>r.RegisterIndex==d.Source.BindPoint&&r.Role==(write?
                    OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite:OriginalMapPostEffect3BindingRole.ShaderResourceRead));
                var b=write?bufferOutputs[row.ParameterIndex]:Bank.RequireBuffer(row.ParameterIndex,bufferInputs[row.ParameterIndex].InstanceIndex);
                bool structuredOrRaw=d.Source.InputType is 5 or 6 or 7 or 8;
                bool eyeTypedHistogram=write&&!structuredOrRaw&&row.ParameterIndex==8&&(index is 102 or 103)&&
                    stage.Source.StageCode==5&&stage.Source.StageIndex==index-52&&
                    stage.Source.Sha256==(index==102?
                        "04A6897BAD89D45E45F69CE3F8DA444975F5D693906D810059879EBEADD46D63":
                        "761B0FCD3090CEB80F40B94774C72098076BD1110E996D4E96D6D4CF71093A28")&&
                    b.Shape.Dimension==OriginalMapPostEffect3BufferDimension.Typed&&b.Shape.ViewFormat==42&&
                    b.Shape.ByteCount==256&&b.Shape.ViewElementCount==64&&d.Source.InputType==4&&d.Source.BindPoint==0;
                bool dofTypedSprites=write&&!structuredOrRaw&&index==58&&row.ParameterIndex is 6 or 7&&
                    stage.Source.StageCode==5&&stage.Source.StageIndex==46&&stage.Source.Sha256==OriginalMapPostEffect3DofSpriteStorageAdapter.BuildSourceSha256&&
                    stage.DofSpriteStorageAdaptation is{} sourceStorage&&sourceStorage.SourceProgramSha256==stage.Source.Sha256&&
                    stage.DeviceTranslation.SpirvSha256==sourceStorage.AdaptedSpirvSha256&&
                    sourceStorage.DescriptorProofs.Count(p=>p.SourceRegister==d.Source.BindPoint&&p.SourceInputType==4&&
                        p.SourceBufferParameter==row.ParameterIndex&&p.NativeElementBytes==8&&p.DeviceWordsPerElement==2)==1&&
                    b.Shape.Dimension==OriginalMapPostEffect3BufferDimension.Typed&&b.Shape.ViewFormat==10&&
                    b.Shape.ByteCount==2073600&&b.Shape.ViewElementCount==259200&&d.Source.InputType==4&&
                    d.Source.BindPoint==(row.ParameterIndex==6?1u:2u)&&d.Source.ReturnType==5;
                if(d.Source.Dimension!=1||(!write&&d.Device.Type.Dimension!=5)||
                    write&&(!b.UsesStorageBuffer||d.Device.Type is not{Kind:"struct",BufferBlock:true})||
                    write&&!structuredOrRaw&&!eyeTypedHistogram&&!dofTypedSprites||
                    !write&&!bufferInputs[row.ParameterIndex].ShaderReadRid.IsValid||
                    structuredOrRaw&&(b.DeviceTexelFormat!=RenderingDevice.DataFormat.R32Uint||d.Source.ReturnType!=6)||
                    !structuredOrRaw&&(b.Shape.Dimension!=OriginalMapPostEffect3BufferDimension.Typed||
                        d.Source.ReturnType!=(b.DeviceTexelFormat==RenderingDevice.DataFormat.R32Uint?4u:5u)))
                    throw new InvalidDataException("Selected original texel descriptor differs from the actual buffer view.");
            }
            Rid sourceAdmittedPriorStorage=default;
            if(input.ToneInfoSourceValueInfluence is{} tone)
            {
                if(index!=41||tone.SourcePassIndex!=index||tone.FrameNumber!=input.FrameNumber||
                    tone.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||tone.SourceSha256!=Library.SourceSha256||
                    tone.ProgramSha256!=stage.Source.Sha256||!tone.OldTexelLoadExecuted||tone.OutputDependsOnPriorTexel||
                    tone.ParameterIndex!=44||tone.TextureMipLevel!=0||tone.TextureWidth!=4||tone.TextureHeight!=1||
                    tone.RegionX!=0||tone.RegionY!=0||tone.RegionWidth!=2||tone.RegionHeight!=1||
                    tone.ActualDispatch!=dispatch||tone.LocalSize!=new OriginalMapPostEffect3DispatchExtent((uint)local.X,(uint)local.Y,(uint)local.Z)||
                    tone.RequiredConstantBufferBytesSha256.Count!=1||!tone.RequiredConstantBufferBytesSha256.TryGetValue(6,out var requiredCbHash)||
                    cbReadReceipts.Count(r=>r.ParameterIndex==6&&r.HasCompleteWriterContents&&r.CompleteBytesSha256==requiredCbHash)!=1||
                    !cbs.TryGetValue(6,out var actualCb)||actualCb.CompleteBytesSha256!=requiredCbHash||
                    !writeViews.TryGetValue(0,out var actualWrite)||actualWrite.Row.ParameterIndex!=44||
                    actualWrite.View.FirstMipLevel!=0||actualWrite.View.MipLevelCount!=1||
                    actualWrite.View.Extent!=new Vector3I(4,1,1)||!outputs.TryGetValue(44,out var actualToneTarget)||
                    actualToneTarget.Source.Parameter.Index!=44||actualWrite.View.StorageRid!=actualToneTarget.StorageRid||
                    !mapped.TryGetValue(("StorageImage",0),out sourceAdmittedPriorStorage)||sourceAdmittedPriorStorage!=actualWrite.View.ViewRid)
                    throw new InvalidDataException("ToneInfo discarded-prior influence is not this exact actual CS38/frame/CB6/UAV44 command.");
            }
            if(input.EyeExposureSourceValueInfluence is{} exposure)
            {
                exposure.RequireCurrent();
                if(index!=104||input.ToneInfoSourceValueInfluence is not null||exposure.SourcePassIndex!=index||
                    exposure.FrameNumber!=input.FrameNumber||exposure.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
                    exposure.SourceSha256!=Library.SourceSha256||exposure.ProgramSha256!=stage.Source.Sha256||
                    !exposure.AllowsFreshUninitializedExposureStorage||exposure.PriorExposureTexelLoadExecutes||
                    exposure.Table!=OriginalMapPostEffect3BindingTable.TextureParameters||exposure.ParameterIndex!=61||
                    exposure.TextureWidth!=2||exposure.TextureHeight!=1||exposure.TextureMipLevel!=0||
                    exposure.ActualDispatch!=dispatch||exposure.LocalSize!=new OriginalMapPostEffect3DispatchExtent((uint)local.X,(uint)local.Y,(uint)local.Z)||
                    exposure.RequiredConstantBufferBytesSha256.Count!=1||
                    !exposure.RequiredConstantBufferBytesSha256.TryGetValue(24,out var requiredExposureHash)||
                    cbReadReceipts.Count(r=>r.ParameterIndex==24&&r.HasCompleteWriterContents&&r.CompleteBytesSha256==requiredExposureHash)!=1||
                    !writeViews.TryGetValue(0,out var actualExposureWrite)||actualExposureWrite.Row.ParameterIndex!=61||
                    actualExposureWrite.View.FirstMipLevel!=0||actualExposureWrite.View.MipLevelCount!=1||
                    actualExposureWrite.View.Extent!=new Vector3I(2,1,1)||
                    !outputs.TryGetValue(61,out var actualExposureTarget)||actualExposureTarget.Source.Parameter.Index!=61||
                    actualExposureWrite.View.StorageRid!=actualExposureTarget.StorageRid||
                    !mapped.TryGetValue(("StorageImage",0),out sourceAdmittedPriorStorage)||sourceAdmittedPriorStorage!=actualExposureWrite.View.ViewRid)
                    throw new InvalidDataException("Eye unexecuted-prior load is not this exact actual CS52/frame/CB24/UAV61 command.");
            }
            if(input.FreshUndefinedEyeExposureRead is{} undefinedExposure)
            {
                undefinedExposure.RequireCurrent();var source=undefinedExposure.Source;var coverage=source.WriteCoverage;
                source.RequireCurrentTextureTable(input.TextureParameters??throw new InvalidDataException("Undefined exposure pointer table is absent."));
                if(index!=104||!ReferenceEquals(undefinedExposure.Bank,Bank)||source.SourceSha256!=Library.SourceSha256||
                    source.ProgramSha256!=stage.Source.Sha256||stage.Source.StageCode!=5||stage.Source.StageIndex!=52||
                    source.FrameNumber!=input.FrameNumber||source.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||
                    !source.PriorTexelLoadExecutes||source.PriorPixelsHaveKnownContents||
                    coverage.ActualDispatch!=dispatch||coverage.LocalSize!=new OriginalMapPostEffect3DispatchExtent((uint)local.X,(uint)local.Y,(uint)local.Z)||
                    input.ToneInfoSourceValueInfluence is not null||input.EyeExposureSourceValueInfluence is not null||
                    !cbReadReceipts.Any(c=>c.ParameterIndex==24&&c.HasCompleteWriterContents&&c.CompleteBytesSha256==source.ConstantBufferBytesSha256)||
                    !writeViews.TryGetValue(0,out var undefinedWrite)||undefinedWrite.Row.ParameterIndex!=61||
                    undefinedWrite.View.FirstMipLevel!=0||undefinedWrite.View.MipLevelCount!=1||
                    undefinedWrite.View.Extent!=undefinedExposure.Extent||undefinedWrite.View.NativeViewFormat!=41||
                    undefinedWrite.View.StorageRid!=undefinedExposure.StorageRid||
                    !outputs.TryGetValue(61,out var undefinedTarget)||undefinedTarget.StorageRid!=undefinedExposure.StorageRid||
                    undefinedTarget.InstanceIndex!=0||undefinedTarget.Source.Parameter.Index!=61||
                    !mapped.TryGetValue(("StorageImage",0),out sourceAdmittedPriorStorage)||sourceAdmittedPriorStorage!=undefinedWrite.View.ViewRid)
                    throw new InvalidDataException("Undefined initial exposure load is not this exact first actual CS52/CB24/owned61 command.");
            }
            if(input.RetainedControlledExposureRead is{} retainedExposure)
            {
                retainedExposure.RequireReadableSourceFootprint(index,0,stage.Source.Sha256,cbReadReceipts);
                if(index!=104||stage.Source.StageCode!=5||stage.Source.StageIndex!=52||
                    !ReferenceEquals(retainedExposure.Snapshot.Bank,Bank)||retainedExposure.FrameNumber!=input.FrameNumber||
                    input.FreshUndefinedEyeExposureRead is not null||input.EyeExposureSourceValueInfluence is not null||
                    retainedExposure.Consumer is not OriginalMapPostEffect3EyeComputeWriteCoverage priorProof||
                    !priorProof.RequiresPriorExposureTexelContents||priorProof.ActualDispatch!=dispatch||
                    priorProof.LocalSize!=new OriginalMapPostEffect3DispatchExtent((uint)local.X,(uint)local.Y,(uint)local.Z)||
                    !writeViews.TryGetValue(0,out var actualPriorWrite)||actualPriorWrite.Row.ParameterIndex!=61||
                    actualPriorWrite.View.StorageRid!=retainedExposure.StorageRid||actualPriorWrite.View.FirstMipLevel!=0||
                    actualPriorWrite.View.MipLevelCount!=1||actualPriorWrite.View.NativeViewFormat!=41||
                    !mapped.TryGetValue(("StorageImage",0),out sourceAdmittedPriorStorage)||sourceAdmittedPriorStorage!=actualPriorWrite.View.ViewRid)
                    throw new InvalidDataException("Retained exposure prior load differs from the exact currentCS52/CB24/owned61 source command.");
            }
            bool eyeHistogramWorkgroup=index==103&&Library.SourceSha256=="588FCC56248A90EE422971050B9D9EAB3305983F6B2AC0A33F5646C49C0623F0"&&
                stage.Source.StageCode==5&&stage.Source.StageIndex==51&&
                stage.Source.Sha256=="761B0FCD3090CEB80F40B94774C72098076BD1110E996D4E96D6D4CF71093A28";
            bool dofSpriteWorkgroup=index==58&&Library.SourceSha256==OriginalMapFinalOutputPostEffect3Source.PayloadSha256&&
                stage.Source.StageCode==5&&stage.Source.StageIndex==46&&
                stage.Source.Sha256==OriginalMapPostEffect3DofSpriteStorageAdapter.BuildSourceSha256&&
                stage.DofSpriteStorageAdaptation is{} dofStorage&&dofStorage.SourceProgramSha256==stage.Source.Sha256&&
                stage.DeviceTranslation.SpirvSha256==dofStorage.AdaptedSpirvSha256;
            IReadOnlySet<uint>? additionalLocalRoots=dofSpriteWorkgroup?
                RequireSourceWorkgroupRoots(stage.DeviceTranslation.Bytes,46):null;
            if(input.LensFlare2Execution is{} lf2)
            {
                lf2.RequireCurrentTextureTable(input.TextureParameters??throw new InvalidDataException("LF2 compute requires its actual same-frame texture table."));
                if(lf2.SourcePassIndex!=index||index is not(46 or 47 or 48 or 49)||lf2.FrameNumber!=input.FrameNumber||
                    lf2.ContextOwnerIdentity!=Bank.Source.ContextOwnerIdentity||lf2.SourceSha256!=Library.SourceSha256||
                    lf2.ProgramSha256!=stage.Source.Sha256||lf2.ActualDispatch!=dispatch||
                    lf2.LocalSize!=new OriginalMapPostEffect3DispatchExtent((uint)local.X,(uint)local.Y,(uint)local.Z)||
                    input.LensExecution is not null||input.EyeExposureSourceValueInfluence is not null||
                    !cbReadReceipts.Any(c=>c.ParameterIndex==17&&c.HasCompleteWriterContents&&c.CompleteBytesSha256==lf2.ConstantBufferBytesSha256))
                    throw new InvalidDataException("LF2 compute source/program/dispatch/CB17 differs from this actual accepted command.");
                if(index is 46 or 47)additionalLocalRoots=lf2.RequireFftWorkgroupRoots(stage.DeviceTranslation.Bytes);
            }
            RequireProvedStorageReads(stage.DeviceTranslation.Bytes,stage.Bindings,mapped,bufferInputs.Values,sourceAdmittedPriorStorage,eyeHistogramWorkgroup,additionalLocalRoots);
            RequireDepthOfField2ComputeSource(index,input);
            input.FreshUndefinedEyeExposureRead?.RequireCurrent();
            input.RetainedControlledExposureRead?.RequireReadableMipRange(0,1);
            var visibleBufferWrites=bufferOutputs.Keys.ToDictionary(i=>i,i=>declarations.Any(d=>d.DescriptorKind=="StorageBuffer"&&
                bufferRows.Last(r=>r.Role==OriginalMapPostEffect3BindingRole.UnorderedAccessReadWrite&&r.RegisterIndex==d.Source.BindPoint).ParameterIndex==i));
            var bufferWriteRevisions=bufferOutputs.ToDictionary(pair=>pair.Key,pair=>visibleBufferWrites[pair.Key]?
                Bank.InvalidateBufferAfterShaderWrite(pair.Key,pair.Value.InstanceIndex,input.FrameNumber,index):pair.Value.UploadRevision);
            var visibleTextureWrites=writeViews.Values.Where(pair=>declarations.Any(d=>d.DescriptorKind=="StorageImage"&&d.Source.BindPoint==pair.Row.RegisterIndex))
                .DistinctBy(pair=>(pair.View.StorageRid,pair.View.FirstMipLevel,pair.View.NativeViewFormat)).ToArray();
            var textureWriteRevisions=visibleTextureWrites.ToDictionary(pair=>(pair.View.StorageRid,pair.View.FirstMipLevel,pair.View.NativeViewFormat),pair=>
            {
                var output=outputs[pair.Row.ParameterIndex];
                return Bank.InvalidateTextureAfterShaderWrite(output.Source.Parameter.Index,output.InstanceIndex,
                    pair.View.FirstMipLevel,input.FrameNumber,index);
            });

            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {
                var uniforms=new Godot.Collections.Array<RDUniform>();
                try
                {
                    foreach(var d in declarations)
                    {
                        Rid resource=mapped[(d.DescriptorKind,d.Source.BindPoint)];
                        if(d.DescriptorKind is "Texture" or "StorageImage")
                        {
                            if(!rd.TextureIsValid(resource))throw new InvalidDataException("3SPK borrowed image retired before actual dispatch.");
                            using var f=rd.TextureGetFormat(resource);
                            if(d.Source.Dimension!=4||f.TextureType!=RenderingDevice.TextureType.Type2D||f.ArrayLayers!=1||
                                !f.UsageBits.HasFlag(d.DescriptorKind=="Texture"?RenderingDevice.TextureUsageBits.SamplingBit:RenderingDevice.TextureUsageBits.StorageBit))
                                throw new InvalidDataException("3SPK actual compute image dimension/usage differs from original source.");
                            string fmt=f.Format.ToString();
                            bool depth=fmt is "D32SfloatS8Uint" or "D32Sfloat";
                            bool isUint=!depth&&fmt.Contains("Uint",StringComparison.Ordinal),isSint=fmt.Contains("Sint",StringComparison.Ordinal);
                            if(d.Source.ReturnType!=(isSint?3u:isUint?4u:5u))
                                throw new InvalidDataException("3SPK actual compute image component type differs from its source RDEF.");
                            var proof=d.DescriptorKind=="Texture"?readViews[d.Source.BindPoint]:writeViews[d.Source.BindPoint].View;
                            bool effectiveFormatMatches=f.Format==proof.DeviceFormat;
                            if(!effectiveFormatMatches&&d.DescriptorKind=="Texture"&&
                                proof.ActualSampledProducer is IOriginalMapPostEffect3SharedSampledViewLease shared)
                            {
                                var producer=proof.ActualSampledProducer;
                                producer.RequireCurrent();
                                if(resource!=producer.ShaderViewRid||proof.StorageRid!=producer.StorageRid||
                                    proof.NativeViewFormat!=producer.NativeViewFormat||proof.DeviceFormat!=producer.DeviceFormat||
                                    proof.Extent!=producer.Extent||proof.FirstMipLevel!=0||proof.MipLevelCount!=1||
                                    proof.RetireAfterSubmission)
                                    throw new InvalidDataException("Compute shared sampled view does not retain the actual source's full native mip0/RIDs.");
                                shared.RequireSharedSampledViewProof().RequireCurrentNativeView(producer,rd,f);
                                effectiveFormatMatches=true;
                            }
                            if(!effectiveFormatMatches||f.Width!=proof.Extent.X||f.Height!=proof.Extent.Y||f.Depth!=1||f.Mipmaps!=proof.MipLevelCount)
                                throw new InvalidDataException("3SPK actual source image differs from its selected native mip view/extent.");
                        }
                        var type=d.DescriptorKind switch{
                            "UniformBuffer"=>RenderingDevice.UniformType.UniformBuffer,"Sampler"=>RenderingDevice.UniformType.Sampler,
                            "Texture"=>RenderingDevice.UniformType.Texture,"StorageImage"=>RenderingDevice.UniformType.Image,
                            "TextureBuffer"=>RenderingDevice.UniformType.TextureBuffer,"StorageBuffer"=>RenderingDevice.UniformType.StorageBuffer,
                            _=>throw new NotSupportedException()};
                        var u=new RDUniform{Binding=checked((int)d.Device.Binding!.Value),UniformType=type};u.AddId(resource);uniforms.Add(u);
                    }
                    set=rd.UniformSetCreate(uniforms,compiled.ShaderRid,0);
                    if(!set.IsValid||!rd.UniformSetIsValid(set))throw new InvalidDataException("Actual3SPK compute uniform set creation failed.");
                }
                finally{foreach(var u in uniforms)u.Dispose();}
                var pipelineKey=(index,compiled.ShaderRid);
                if(!pipelines.TryGetValue(pipelineKey,out var pipeline))
                {
                    pipeline=rd.ComputePipelineCreate(compiled.ShaderRid);
                    if(!pipeline.IsValid||!rd.ComputePipelineIsValid(pipeline))
                    {if(pipeline.IsValid)rd.FreeRid(pipeline);throw new InvalidDataException("Actual3SPK compute pipeline creation failed.");}
                    pipelines.Add(pipelineKey,pipeline);
                }
                long list=rd.ComputeListBegin();
                try{rd.ComputeListBindComputePipeline(list,pipeline);rd.ComputeListBindUniformSet(list,set,0);
                    rd.ComputeListDispatch(list,dispatch.X,dispatch.Y,dispatch.Z);}
                finally{rd.ComputeListEnd();}
                return true;
            });
            foreach(var t in input.CurrentTextures)t.RequireCurrent();foreach(var s in input.CurrentSamplers)s.RequireCurrent();
            RequireDepthOfField2ComputeSource(index,input);
            // The bank execution scope excludes other uploads. A native in-place
            // storage write deliberately invalidated its prior read lease before
            // submission; independent read inputs must remain current.
            foreach(var b in input.CurrentBuffers.Where(b=>!visibleBufferWrites.GetValueOrDefault(b.ParameterIndex)))b.RequireCurrent();
            ValidateTextureTable();
            foreach(int i in cbIndices)
                if(!ReferenceEquals(cbs[i],Bank.RequireConstantBufferForPass(i,input.ConstantBufferInstances[i],index,input.FrameNumber))||
                    cbs[i].UploadRevision!=cbRevisions[i])
                    throw new InvalidDataException("3SPK compute CB owner changed during dispatch.");
            var written=visibleTextureWrites.Select(pair=>{var t=outputs[pair.Row.ParameterIndex];var view=pair.View;return new OriginalMapPostEffect3ComputeWriteReceipt(
                    pair.Row.ParameterIndex,t.InstanceIndex,t.StorageRid,view.ViewRid,view.NativeViewFormat,view.Extent)
                    {PhysicalAllocationParameterIndex=t.Source.Parameter.Index,MipLevel=view.FirstMipLevel,
                        MipLevelCount=view.MipLevelCount,ViewRetiredAfterSubmission=view.RetireAfterSubmission,
                        WriterRevision=textureWriteRevisions[(view.StorageRid,view.FirstMipLevel,view.NativeViewFormat)]};}).ToArray();
            // A set must retire successfully before publishing completion.
            // Failure cleanup never leaves a successful last-pass receipt.
            if(set.IsValid)
            {await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(set);return true;});set=default;}
            await RetireMipViewsAsync();
            var bufferWritten=bufferOutputs.Select(pair=>
            {
                var b=pair.Value;return new OriginalMapPostEffect3ComputeBufferWriteReceipt(pair.Key,b.InstanceIndex,b.StorageRid,
                    b.Source.ByteCount,bufferWriteRevisions[pair.Key],visibleBufferWrites[pair.Key]);
            }).ToArray();
            return LastCompletedPass=new(input.FrameNumber,Bank.Source.ContextOwnerIdentity,index,stage.Source.Sha256,dispatch,local,
                Array.AsReadOnly(native.Select(b=>b.SourceRowIndex).ToArray()),Array.AsReadOnly(written),
                Bank.Source.SourceEvidence+";actual-CS-automatic-source-dispatch;source-input-leases;no-history-commit;no-full-coverage-claim;"+
                    (input.LensExecution?.SourceEvidence??"counted-native-setup")+
                    (input.FreshUndefinedEyeExposureRead is{} initialExposure?";"+initialExposure.SourceEvidence:""))
                {BufferWrites=Array.AsReadOnly(bufferWritten),BufferReads=bufferReadReceipts.AsReadOnly(),
                    ConstantBufferReads=Array.AsReadOnly(cbReadReceipts),
                    SourceNullShaderResources=Array.AsReadOnly(input.AcceptedNullShaderResources.ToArray()),
                    OrderedEffectiveBindings=Array.AsReadOnly(native.Select(b=>b with{RawBytes=b.RawBytes.ToArray()}).ToArray())};

            void ValidateTextureTable()
            {
                foreach(var n in input.AcceptedNullShaderResources)n.ValidateCurrent();
                if(input.TextureParameters is{} table)
                    foreach(var receipt in input.AcceptedTextureParameters!.Values)table.ValidateCurrent(receipt);
            }
        }
        finally
        {
            try{if(set.IsValid)await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>{rd.FreeRid(set);return true;});}
            finally
            {
                try{await RetireMipViewsAsync();}
                finally{bufferScope?.Dispose();serial.Release();lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();}
            }
        }

        async Task RetireMipViewsAsync()
        {
            if(temporaryMipViews.Count==0)return;
            await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {for(int i=temporaryMipViews.Count-1;i>=0;i--)rd.FreeRid(temporaryMipViews[i]);return true;});
            temporaryMipViews.Clear();
        }
    }

    public ValueTask DisposeAsync()
    {
        lock(lifetime){if(disposal is not null)return new(disposal);retiring=true;
            Task pending=accepted==0?Task.CompletedTask:(drained=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            return new(disposal=RetireAsync(pending));}
    }
    private async Task RetireAsync(Task pending)
    {
        await pending;await serial.WaitAsync();
        try{await OriginalTerrainDeviceTexture.OnRenderThreadAsync(rd=>
            {foreach(var p in pipelines.Values.Reverse())rd.FreeRid(p);pipelines.Clear();return true;});LastCompletedPass=null;}
        finally{serial.Release();}
    }

    // Exact source DXGI view rows, including original encoded G1T inputs.
    // These are validation mappings, not choices of an input's colour space.
    private static RenderingDevice.DataFormat SourceViewFormat(uint native)=>native switch
    {
        28=>RenderingDevice.DataFormat.R8G8B8A8Unorm,29=>RenderingDevice.DataFormat.R8G8B8A8Srgb,
        87=>RenderingDevice.DataFormat.B8G8R8A8Unorm,91=>RenderingDevice.DataFormat.B8G8R8A8Srgb,
        2=>RenderingDevice.DataFormat.R32G32B32A32Sfloat,10=>RenderingDevice.DataFormat.R16G16B16A16Sfloat,
        11=>RenderingDevice.DataFormat.R16G16B16A16Unorm,34=>RenderingDevice.DataFormat.R16G16Sfloat,
        49=>RenderingDevice.DataFormat.R8G8Unorm,61=>RenderingDevice.DataFormat.R8Unorm,
        30=>RenderingDevice.DataFormat.R8G8B8A8Uint,50=>RenderingDevice.DataFormat.R8G8Uint,
        54=>RenderingDevice.DataFormat.R16Sfloat,40=>RenderingDevice.DataFormat.D32Sfloat,
        41=>RenderingDevice.DataFormat.R32Sfloat,20 or 21=>RenderingDevice.DataFormat.D32SfloatS8Uint,
        26=>RenderingDevice.DataFormat.B10G11R11UfloatPack32,
        71=>RenderingDevice.DataFormat.Bc1RgbaUnormBlock,72=>RenderingDevice.DataFormat.Bc1RgbaSrgbBlock,
        77=>RenderingDevice.DataFormat.Bc3UnormBlock,78=>RenderingDevice.DataFormat.Bc3SrgbBlock,
        80=>RenderingDevice.DataFormat.Bc4UnormBlock,95=>RenderingDevice.DataFormat.Bc6HUfloatBlock,
        98=>RenderingDevice.DataFormat.Bc7UnormBlock,99=>RenderingDevice.DataFormat.Bc7SrgbBlock,
        _=>throw new NotSupportedException("3SPK current texture lease uses an unimplemented original typed view format.")
    };

    private void RequireDepthOfField2ComputeSource(int pass,OriginalMapPostEffect3ComputeFrameInputs input)
    {
        if(pass is not(57 or 58))
        {
            if(input.DepthOfField2Prepared is not null||input.DepthOfField2Before is not null)
                throw new InvalidDataException("DOF2 sprite source cannot admit another compute pass.");
            return;
        }
        var prepared=input.DepthOfField2Prepared??throw new InvalidDataException("DOF2 sprite compute needs its actual typed Prepared packet.");
        var before=input.DepthOfField2Before??throw new InvalidDataException("DOF2 sprite compute needs its actual typed Before receipt.");
        var table=input.TextureParameters??throw new InvalidDataException("DOF2 sprite compute needs its actual native pointer table.");
        prepared.RequireCurrentTextureTable(table);before.RequirePrepared(prepared);
        if(!ReferenceEquals(table.Allocation,Bank.Source)||prepared.FrameNumber!=input.FrameNumber||
            before.FrameNumber!=input.FrameNumber||before.SourcePassIndex!=pass||!before.RequiresGenericComputeDispatch||
            !prepared.BeforeHookAllows(pass)||prepared.ConstantBuffer.SourceSha256!=Bank.Source.Source.Sha256||
            prepared.ConstantBuffer.OwnerIdentity!=Bank.Source.ContextOwnerIdentity||
            prepared.ConstantBuffer.Declaration.Index!=18||prepared.ConstantBuffer.Bytes.Length!=144||
            input.LensExecution is not null||input.LensFlare2Execution is not null||
            input.ToneInfoSourceValueInfluence is not null||input.EyeExposureSourceValueInfluence is not null||
            input.FreshUndefinedEyeExposureRead is not null)
            throw new InvalidDataException("DOF2 sprite compute does not match the exact current selected source/Prepared/Before/CB18.");
    }

    private static void RequireProvedStorageReads(ReadOnlySpan<byte> bytes,OriginalTranslatedShaderBindings bindings,
        IReadOnlyDictionary<(string Kind,uint Register),Rid> mapped,IEnumerable<IOriginalMapPostEffect3ComputeBufferLease> inputs,
        Rid exactSourceAdmittedPriorStorage=default,bool exactEyeHistogramWorkgroup=false,IReadOnlySet<uint>? exactAdditionalWorkgroupRoots=null)
    {
        // Follow original translated opaque image loads to their descriptor.
        // Only completed same-frame buffer leases admit storage reads/atomics;
        // the pinned CS51's source-initialized workgroup counter array is local
        // shader storage, not an external resource or a prior-buffer lease.
        // Storage images and ambiguous pointer origins keep the previous guard.
        var localRoots=exactAdditionalWorkgroupRoots?.ToHashSet()??RequireSourceWorkgroupRoots(bytes,exactEyeHistogramWorkgroup?51:0);
        var declarations=bindings.Bindings.ToDictionary(d=>d.Device.VariableId);
        var origins=declarations.Keys.ToDictionary(i=>i,i=>i);
        foreach(uint root in localRoots)origins.Add(root,root);
        var reads=new HashSet<uint>();
        for(int at=20;at<bytes.Length;)
        {
            uint header=BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
            int words=checked((int)(header>>16));ushort op=(ushort)header;
            if(words==0||at+words*4>bytes.Length)throw new InvalidDataException("Malformed selected CS instruction extent.");
            if(op is 61 or 65 or 66 or 67 or 83 or 143)
            {
                if(words<4)throw new InvalidDataException("Malformed translated buffer pointer/load.");
                uint result=BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+8)..]),source=BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+12)..]);
                if(origins.TryGetValue(source,out uint origin))
                {origins[result]=origin;if(op==61&&!localRoots.Contains(origin)&&declarations[origin].DescriptorKind=="StorageBuffer")reads.Add(origin);}
            }
            if(op is 98 or 143 or >=227 and <=242)
            {
                int sourceWord=op==228?1:3;
                if(words<=sourceWord||!origins.TryGetValue(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+sourceWord*4)..]),out uint origin))
                    throw new NotSupportedException($"Selected storage read/atomic opcode{op} at byte{at} has no source-closed descriptor origin.");
                if(!localRoots.Contains(origin))reads.Add(origin);
            }
            at+=words*4;
        }
        foreach(uint id in reads)
        {
            var declaration=declarations[id];
            if(exactSourceAdmittedPriorStorage.IsValid&&declaration.DescriptorKind=="StorageImage"&&declaration.Source.BindPoint==0&&
                mapped.TryGetValue(("StorageImage",0),out var admittedPrior)&&admittedPrior==exactSourceAdmittedPriorStorage)
                continue; // Exact source gate admits this load; no prior texel is marked ready.
            if(declaration.DescriptorKind!="StorageBuffer"||!mapped.TryGetValue((declaration.DescriptorKind,declaration.Source.BindPoint),out var rid))
                throw new NotSupportedException("Selected storage image/read interface has no current completed storage input adapter.");
            var candidates=inputs.Where(b=>b.StorageRid==rid).ToArray();
            if(candidates.Length!=1)throw new NotSupportedException("Selected buffer storage read/atomic requires its exact completed same-frame input lease.");
            candidates[0].RequireReadRange(0,candidates[0].ByteCount);
        }
    }

    private static HashSet<uint> RequireSourceWorkgroupRoots(ReadOnlySpan<byte> bytes,int exactProgram)
    {
        // Official SPIR-V OpVariable/OpTypePointer storage class4 is Workgroup.
        // The source CS51 hash pins dcl_tgsm_structured g0,4,64, the unconditional
        // per-lane zero store followed by sync_g_t, and the second sync_g_t before
        // reading g0. CS46 pins512 structured16B rows: every lane initializes
        // two rows in iteration0, and each loop iteration syncs before reuse.
        // This permits no arbitrary shader's local atomics or external UAV reads.
        if(bytes.Length<20||bytes.Length%4!=0||BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=0x07230203)
            throw new InvalidDataException("Selected compute SPIR-V header is invalid.");
        var pointerTypes=new Dictionary<uint,(uint Storage,uint Element)>();
        var arrays=new Dictionary<uint,(uint Element,uint Length)>();
        var uintTypes=new HashSet<uint>();var constants=new Dictionary<uint,(uint Type,uint Value)>();
        var variables=new List<(uint Type,uint Id,int Words)>();
        var barriers=new List<(uint ExecutionScope,uint MemoryScope,uint Semantics)>();
        for(int at=20;at<bytes.Length;)
        {
            uint header=BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
            int count=checked((int)(header>>16));ushort op=(ushort)header;
            if(count==0||at+count*4>bytes.Length)throw new InvalidDataException("Malformed selected CS declaration extent.");
            if(op==21&&count==4&&BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+8)..])==32&&
                BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+12)..])==0)
                uintTypes.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+4)..]));
            else if(op==28&&count==4)
                arrays.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+4)..]),
                    (BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+8)..]),BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+12)..])));
            else if(op==32&&count==4)
                pointerTypes.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+4)..]),
                    (BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+8)..]),BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+12)..])));
            else if(op==43&&count==4)
                constants.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+8)..]),
                    (BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+4)..]),BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+12)..])));
            else if(op==59&&count>=4&&BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+12)..])==4)
                variables.Add((BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+4)..]),BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+8)..]),count));
            else if(op==224&&count==4)
                barriers.Add((BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+4)..]),BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+8)..]),
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at+12)..])));
            at+=count*4;
        }
        if(variables.Count==0)return [];
        if(exactProgram is not(51 or 46)||variables.Count!=1||variables[0].Words!=4||
            barriers.Count!=(exactProgram==51?2:1)||barriers.Any(b=>
            !constants.TryGetValue(b.ExecutionScope,out var execution)||!uintTypes.Contains(execution.Type)||execution.Value!=2||
            !constants.TryGetValue(b.MemoryScope,out var memory)||!uintTypes.Contains(memory.Type)||memory.Value!=2||
            !constants.TryGetValue(b.Semantics,out var semantics)||!uintTypes.Contains(semantics.Type)||semantics.Value!=264))
            throw new NotSupportedException("Selected workgroup storage has no exact original Eye/DOF initialization source.");
        var variable=variables[0];
        if(!pointerTypes.TryGetValue(variable.Type,out var pointer)||pointer.Storage!=4||
            !arrays.TryGetValue(pointer.Element,out var array)||!uintTypes.Contains(array.Element)||
            !constants.TryGetValue(array.Length,out var length)||!uintTypes.Contains(length.Type)||length.Value!=(exactProgram==51?64u:2048u))
            throw new NotSupportedException("Original Eye/DOF workgroup storage differs from its source uint32 counter/shared-row declaration.");
        return [variable.Id];
    }
}
