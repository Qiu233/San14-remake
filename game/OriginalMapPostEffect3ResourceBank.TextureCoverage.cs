using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    public IOriginalMapPostEffect3ComputeMipTextureLease PublishShaftTextureCoverage(
        OriginalMapPostEffect3ShaftWriteCoverage coverage,OriginalMapPostEffect3ComputeExecutor actualExecutor,
        OriginalMapPostEffect3ComputeCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        return PublishSourceTextureCoverage(actualExecutor,completed,coverage.FrameNumber,
            coverage.ContextOwnerIdentity,coverage.SourceSha256,coverage.SourcePassIndex,coverage.ProgramSha256,
            coverage.ParameterIndex,checked((int)coverage.TextureMipLevel),coverage.TextureWidth,coverage.TextureHeight,
            coverage.ActualDispatch,coverage.LocalSize,coverage.SourceEvidence,
            new Dictionary<int,string>{{coverage.RequiredConstantBufferParameterIndex,coverage.RequiredConstantBufferBytesSha256}},
            Array.Empty<int>());
    }

    public IOriginalMapPostEffect3ComputeMipTextureLease PublishLensTextureCoverage(
        OriginalMapPostEffect3LensTextureWriteCoverage coverage,OriginalMapPostEffect3ComputeExecutor actualExecutor,
        OriginalMapPostEffect3ComputeCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        foreach(var requiredNull in coverage.RequiredNullSources)
        {
            requiredNull.ValidateCurrent();
            if(!completed.SourceNullShaderResources.Any(actual=>ReferenceEquals(actual,requiredNull))||
                requiredNull.SourcePassIndex!=coverage.SourcePassIndex||requiredNull.SourceStageCode!=5||
                requiredNull.FrameNumber!=coverage.FrameNumber||requiredNull.ContextOwnerIdentity!=coverage.ContextOwnerIdentity||
                requiredNull.SourceSha256!=coverage.SourceSha256)
                throw new InvalidDataException("Lens coverage requires another actual native null-SRV submission.");
        }
        return PublishSourceTextureCoverage(actualExecutor,completed,coverage.FrameNumber,
            coverage.ContextOwnerIdentity,coverage.SourceSha256,coverage.SourcePassIndex,coverage.ProgramSha256,
            coverage.ParameterIndex,coverage.TextureMipLevel,coverage.TextureWidth,coverage.TextureHeight,
            coverage.ActualDispatch,coverage.LocalSize,coverage.SourceEvidence,
            coverage.RequiredConstantBufferBytesSha256,coverage.RequiredInputTextureParameters,
            coverage.RequiredInputBufferParameters,coverage.RequiredPriorUavContentsParameters,
            coverage.RequiredInputTextureBindings,coverage.RequiredInputBufferBindings);
    }

    /// <summary>A native by-value table alias retains both its logical slot
    /// revision and the underlying source-written physical mip contents.</summary>
    public IOriginalMapPostEffect3ComputeMipTextureLease CaptureSourceWrittenTextureAliasLease(
        OriginalMapPostEffect3TextureParameterState actualTable,int logicalParameterIndex,long frameNumber,
        uint firstMipLevel,uint mipLevelCount)
    {
        ArgumentNullException.ThrowIfNull(actualTable);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(actualTable.FrameNumber!=frameNumber||actualTable.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                actualTable.SourceSha256!=Source.Source.Sha256)
                throw new InvalidDataException("Source-written alias table has another frame/module/allocation owner.");
            var accepted=actualTable.Require(logicalParameterIndex);
            if(accepted.Reference.HasSourceSelectedMipView)
                throw new InvalidDataException("A copied single-mip wrapper requires its source-selected view lease; a full native SRV cannot replace it.");
            if(accepted.Reference.PhysicalAllocationParameterIndex is not int physical)
                throw new InvalidDataException("A source-written module lease cannot stand in for an external texture producer.");
            var contents=CaptureSourceWrittenTextureLease(physical,accepted.Reference.InstanceIndex,
                frameNumber,firstMipLevel,mipLevelCount);
            return new SourceWrittenTextureAliasLease(actualTable,accepted,contents);
        }
    }

    /// <summary>Captures only the positively source-written physical mip range.
    /// The full native SRV remains the owner; a consumer requesting additional
    /// mips must obtain their actual coverage, rather than infer a whole chain.</summary>
    public IOriginalMapPostEffect3ComputeMipTextureLease CaptureSourceWrittenTextureLease(
        int physicalParameterIndex,int instanceIndex,long frameNumber,uint firstMipLevel,uint mipLevelCount)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var texture=Require(Textures,physicalParameterIndex,instanceIndex,"source-written texture");
            if(frameNumber<=0||texture.Shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||
                texture.Shape.Depth!=1||firstMipLevel>=texture.Shape.MipLevels||
                mipLevelCount==0||mipLevelCount>texture.Shape.MipLevels-firstMipLevel)
                throw new InvalidDataException("Source texture read range differs from its ordinary2D allocation.");
            var snapshot=new Dictionary<uint,TextureContents>();
            for(uint mip=firstMipLevel;mip-firstMipLevel<mipLevelCount;mip++)
            {
                if(!textureContents.TryGetValue((physicalParameterIndex,instanceIndex,mip),out var contents)||
                    !contents.Complete||contents.Frame!=frameNumber)
                    throw new InvalidDataException("Requested source texture mip has no complete same-frame writer coverage.");
                snapshot.Add(mip,contents);
            }
            uint format=texture.Shape.Formats.ShaderResourceFormat0;
            if(format==0||!texture.NativeFormatViews.TryGetValue(format,out var view))
                throw new InvalidDataException("Covered source texture has no actual native shader resource view.");
            return new SourceWrittenMipTextureLease(this,texture,frameNumber,format,view,
                new System.Collections.ObjectModel.ReadOnlyDictionary<uint,TextureContents>(snapshot));
        }
    }

    // This private gate accepts only fields supplied by the closed typed source
    // proof overloads below. A dispatch or user-provided evidence string alone
    // cannot invoke it or turn allocated pixels into readable contents.
    private IOriginalMapPostEffect3ComputeMipTextureLease PublishSourceTextureCoverage(
        OriginalMapPostEffect3ComputeExecutor executor,OriginalMapPostEffect3ComputeCompletedPass completed,
        long frame,string owner,string sourceSha,int pass,string programSha,int parameter,int mip,
        uint width,uint height,OriginalMapPostEffect3DispatchExtent dispatch,
        OriginalMapPostEffect3DispatchExtent local,string sourceEvidence,
        IReadOnlyDictionary<int,string> requiredConstantBufferBytesSha256,
        IReadOnlyList<int> requiredInputTextureParameters,
        IReadOnlyList<int>? requiredInputBufferParameters=null,
        IReadOnlyList<int>? requiredPriorUavContentsParameters=null,
        IReadOnlyList<OriginalMapPostEffect3Binding>? requiredInputTextureBindings=null,
        IReadOnlyList<OriginalMapPostEffect3Binding>? requiredInputBufferBindings=null)
    {
        ArgumentNullException.ThrowIfNull(executor);ArgumentNullException.ThrowIfNull(completed);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(executor.Bank,this)||!ReferenceEquals(executor.LastCompletedPass,completed)||
                sourceSha!=Source.Source.Sha256||owner!=Source.ContextOwnerIdentity||frame<=0||
                completed.FrameNumber!=frame||completed.ContextOwnerIdentity!=owner||
                completed.SourcePassIndex!=pass||completed.ProgramSha256!=programSha||
                completed.Dispatch!=dispatch||completed.ActualShaderLocalSize!=
                    new Vector3I(checked((int)local.X),checked((int)local.Y),checked((int)local.Z)))
                throw new InvalidDataException("Texture coverage is not this exact source executor/module/frame/program/dispatch.");
            foreach(var required in requiredConstantBufferBytesSha256)
            {
                var reads=completed.ConstantBufferReads.Where(r=>r.ParameterIndex==required.Key).ToArray();
                if(reads.Length!=1||!reads[0].HasCompleteWriterContents||reads[0].CompleteBytesSha256!=required.Value)
                    throw new InvalidDataException("Complete shader write coverage depends on another actual constant-buffer writer.");
            }
            // Before can replace a shader register with a derived row(-1).
            // The actual executor freezes the accepted effective setter table;
            // counted metadata row IDs alone cannot describe that replacement.
            var passBindings=completed.OrderedEffectiveBindings.Where(b=>(b.StageMask&1)!=0).ToArray();
            foreach(var required in (requiredInputTextureBindings??Array.Empty<OriginalMapPostEffect3Binding>())
                .Concat(requiredInputBufferBindings??Array.Empty<OriginalMapPostEffect3Binding>()))
            {
                var actual=passBindings.LastOrDefault(b=>b.Table==required.Table&&b.Role==required.Role&&
                    b.RegisterIndex==required.RegisterIndex);
                if(actual is null||actual.ParameterIndex!=required.ParameterIndex||actual.NativeViewKind!=required.NativeViewKind||
                    actual.MipSelector!=required.MipSelector||actual.SliceSelector!=required.SliceSelector||
                    actual.StageMask!=required.StageMask||actual.RawFlags0!=required.RawFlags0||actual.SourceRowIndex!=required.SourceRowIndex)
                    throw new InvalidDataException("Source coverage input differs from the exact submitted last effective native setter.");
            }
            foreach(int input in requiredInputTextureParameters)
                if(!passBindings.Any(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
                    b.ParameterIndex==input&&b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead))
                    throw new InvalidDataException("Coverage proof requires an absent actual submitted source texture input.");
            foreach(int input in requiredInputBufferParameters??Array.Empty<int>())
                if(completed.BufferReads.Count(r=>r.ParameterIndex==input)!=1||
                    !passBindings.Any(b=>b.Table==OriginalMapPostEffect3BindingTable.BufferParameters&&
                        b.ParameterIndex==input&&b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead))
                    throw new InvalidDataException("Coverage proof requires an absent actual submitted source buffer input.");
            if(requiredPriorUavContentsParameters is{Count:>0})
                throw new NotSupportedException("Complete image coverage cannot infer an earlier UAV read from a later write receipt.");
            var writes=completed.Writes.Where(w=>w.ParameterIndex==parameter&&mip>=0&&w.MipLevel==(uint)mip).ToArray();
            if(writes.Length!=1)throw new InvalidDataException("Coverage proof needs one exact submitted texture output.");
            var write=writes[0];
            var texture=Require(Textures,write.PhysicalAllocationParameterIndex,write.InstanceIndex,"source-covered texture");
            // Current closed producers describe source-owned physical targets.
            // An alias to another allocation needs its own source proof.
            if(mip<0||(uint)mip>=texture.Shape.MipLevels||mip>=32||
                write.PhysicalAllocationParameterIndex!=parameter||write.MipLevel!=(uint)mip||write.MipLevelCount!=1||
                texture.Shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||texture.Shape.Depth!=1||
                texture.StorageRid!=write.StorageRid||write.NativeViewFormat!=texture.Shape.Formats.UnorderedAccessFormat||
                Math.Max(1u,texture.Shape.Width>>mip)!=width||Math.Max(1u,texture.Shape.Height>>mip)!=height||
                write.Extent!=new Vector3I(checked((int)width),checked((int)height),1))
                throw new InvalidDataException("Complete source shader store covers another physical allocation/side/mip/format/extent.");
            var key=(parameter,write.InstanceIndex,(uint)mip);
            if(!textureContents.TryGetValue(key,out var submitted)||submitted.Revision!=write.WriterRevision||
                submitted.Frame!=frame||submitted.Pass!=pass)
                throw new InvalidDataException("Source texture was replaced or written again after the actual complete dispatch.");
            textureContents[key]=submitted with{Complete=true,Evidence=sourceEvidence+";"+completed.SourceEvidence};
            return CaptureSourceWrittenTextureLease(parameter,write.InstanceIndex,frame,(uint)mip,1);
        }
    }

    private sealed record SourceWrittenMipTextureLease(OriginalMapPostEffect3ResourceBank Bank,
        OriginalMapPostEffect3DeviceTexture Texture,long FrameNumber,uint NativeViewFormat,Rid ShaderViewRid,
        IReadOnlyDictionary<uint,TextureContents> Mips):IOriginalMapPostEffect3ComputeMipTextureLease
    {
        public string ContextOwnerIdentity=>Bank.Source.ContextOwnerIdentity;
        public int ParameterIndex=>Texture.Source.Parameter.Index;
        public int InstanceIndex=>Texture.InstanceIndex;
        public Rid StorageRid=>Texture.StorageRid;
        public RenderingDevice.DataFormat DeviceFormat=>ViewFormat(NativeViewFormat);
        public Vector3I Extent=>new((int)Texture.Shape.Width,(int)Texture.Shape.Height,(int)Texture.Shape.Depth);
        public string SourceEvidence=>string.Join(";",Mips.OrderBy(p=>p.Key).Select(p=>"physical-mip"+p.Key+":"+p.Value.Evidence));
        public void RequireCurrent()
        {
            lock(Bank.lifetime)
            {
                ObjectDisposedException.ThrowIf(Bank.retiring,Bank);
                if(!ReferenceEquals(Require(Bank.Textures,ParameterIndex,InstanceIndex,"source-covered texture"),Texture))
                    throw new InvalidDataException("Source-covered texture allocation owner changed.");
                foreach(var pair in Mips)
                    if(!Bank.textureContents.TryGetValue((ParameterIndex,InstanceIndex,pair.Key),out var current)||
                        !current.Complete||current.Frame!=FrameNumber||current.Revision!=pair.Value.Revision)
                        throw new InvalidDataException("Source-written mip pixels were invalidated by another dispatch/frame.");
            }
        }
        public void RequireReadableMipRange(uint first,uint count)
        {
            RequireCurrent();
            if(count==0||first>=Texture.Shape.MipLevels||count>Texture.Shape.MipLevels-first)
                throw new InvalidDataException("Requested native sampled mip range is outside the source allocation.");
            for(uint mip=first;mip-first<count;mip++)
                if(!Mips.ContainsKey(mip))throw new InvalidDataException("This lease does not prove complete contents of every requested native mip.");
        }
    }

    private sealed record SourceWrittenTextureAliasLease(OriginalMapPostEffect3TextureParameterState Table,
        OriginalMapPostEffect3ResolvedTextureParameter Accepted,IOriginalMapPostEffect3ComputeMipTextureLease Contents)
        :IOriginalMapPostEffect3ComputeMipTextureLease
    {
        public long FrameNumber=>Contents.FrameNumber;
        public string ContextOwnerIdentity=>Contents.ContextOwnerIdentity;
        public int ParameterIndex=>Accepted.ParameterIndex;
        public int InstanceIndex=>Contents.InstanceIndex;
        public Rid ShaderViewRid=>Contents.ShaderViewRid;
        public Rid StorageRid=>Contents.StorageRid;
        public uint NativeViewFormat=>Contents.NativeViewFormat;
        public RenderingDevice.DataFormat DeviceFormat=>Contents.DeviceFormat;
        public Vector3I Extent=>Contents.Extent;
        public string SourceEvidence=>Accepted.Reference.SourceEvidence+";"+Contents.SourceEvidence;
        public void RequireCurrent(){Table.ValidateCurrent(Accepted);Contents.RequireCurrent();}
        public void RequireReadableMipRange(uint first,uint count)
        {RequireCurrent();Contents.RequireReadableMipRange(first,count);}
    }
}
