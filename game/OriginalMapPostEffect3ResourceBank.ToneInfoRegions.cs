using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    private sealed record ToneInfoRegionContents(long Frame,long Revision,uint X,uint Y,uint Width,uint Height,string Evidence);
    private readonly Dictionary<(int Parameter,int Side,uint Mip),ToneInfoRegionContents> toneInfoRegions=[];

    /// <summary>CS38 defines only texels0/1 of its original4x1 mip. The source
    /// zero adaptation selector discards the old texel1 value; its load still
    /// executes. Neither the remaining texels nor the full mip become ready.</summary>
    public void PublishToneInfoRegionCoverage(OriginalMapPostEffect3ToneInfoRegionWriteCoverage coverage,
        OriginalMapPostEffect3ComputeExecutor actualExecutor,OriginalMapPostEffect3ComputeCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(coverage);ArgumentNullException.ThrowIfNull(actualExecutor);ArgumentNullException.ThrowIfNull(completed);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(actualExecutor.Bank,this)||!ReferenceEquals(actualExecutor.LastCompletedPass,completed)||
                coverage.SourceSha256!=Source.Source.Sha256||coverage.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                coverage.SourcePassIndex!=41||coverage.ParameterIndex!=44||coverage.TextureMipLevel!=0||
                coverage.TextureWidth!=4||coverage.TextureHeight!=1||coverage.RegionX!=0||coverage.RegionY!=0||
                coverage.RegionWidth!=2||coverage.RegionHeight!=1||!coverage.OldTexelLoadExecuted||coverage.OutputDependsOnPriorTexel||
                completed.FrameNumber!=coverage.FrameNumber||completed.ContextOwnerIdentity!=coverage.ContextOwnerIdentity||
                completed.SourcePassIndex!=41||completed.ProgramSha256!=coverage.ProgramSha256||completed.Dispatch!=coverage.ActualDispatch||
                completed.ActualShaderLocalSize!=new Vector3I((int)coverage.LocalSize.X,(int)coverage.LocalSize.Y,(int)coverage.LocalSize.Z))
                throw new InvalidDataException("ToneInfo region is not the actual source-defined-output CS38 dispatch.");
            foreach(var required in coverage.RequiredConstantBufferBytesSha256)
            {
                var reads=completed.ConstantBufferReads.Where(r=>r.ParameterIndex==required.Key).ToArray();
                if(reads.Length!=1||!reads[0].HasCompleteWriterContents||reads[0].CompleteBytesSha256!=required.Value)
                    throw new InvalidDataException("ToneInfo discarded-prior-value proof differs from the actual CB selector bytes.");
            }
            foreach(int input in coverage.RequiredInputTextureParameters)
                if(!completed.OrderedEffectiveBindings.Any(b=>b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&
                    b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&b.ParameterIndex==input&&
                    (b.StageMask&1)!=0))
                    throw new InvalidDataException("ToneInfo reduction did not submit its required source image read.");
            foreach(var required in coverage.RequiredInputTextureBindings)
            {
                var actual=completed.OrderedEffectiveBindings.LastOrDefault(b=>b.Table==required.Table&&b.Role==required.Role&&
                    b.RegisterIndex==required.RegisterIndex&&(b.StageMask&1)!=0);
                if(actual is null||actual.ParameterIndex!=required.ParameterIndex||actual.NativeViewKind!=required.NativeViewKind||
                    actual.MipSelector!=required.MipSelector||actual.SliceSelector!=required.SliceSelector||
                    actual.StageMask!=required.StageMask||actual.RawFlags0!=required.RawFlags0||actual.SourceRowIndex!=required.SourceRowIndex)
                    throw new InvalidDataException("ToneInfo source reduction input is not its exact last accepted effective native setter.");
            }
            var writes=completed.Writes.Where(w=>w.ParameterIndex==44&&w.MipLevel==0).ToArray();
            if(writes.Length!=1)throw new InvalidDataException("ToneInfo region needs its single source UAV write.");
            var write=writes[0];var texture=Require(Textures,44,write.InstanceIndex,"ToneInfo region target");
            if(write.PhysicalAllocationParameterIndex!=44||write.MipLevelCount!=1||texture.Shape.MipLevels!=1||
                texture.Shape.Width!=4||texture.Shape.Height!=1||texture.Shape.Depth!=1||texture.StorageRid!=write.StorageRid||
                write.NativeViewFormat!=texture.Shape.Formats.UnorderedAccessFormat||write.Extent!=new Vector3I(4,1,1)||
                !textureContents.TryGetValue((44,write.InstanceIndex,0),out var submitted)||submitted.Revision!=write.WriterRevision||
                submitted.Frame!=coverage.FrameNumber||submitted.Pass!=41)
                throw new InvalidDataException("ToneInfo region physical storage, native mip/format or writer revision changed.");
            toneInfoRegions[(44,write.InstanceIndex,0)]=new(coverage.FrameNumber,write.WriterRevision,0,0,2,1,
                coverage.SourceEvidence+";"+completed.SourceEvidence+";only-texels0,1-defined;not-complete4x1-mip");
        }
    }

    /// <summary>The original PS3 reads at most literal texel0, depending on
    /// its actual Composite CB predicate. This is a source consumer capability,
    /// not a reduced texture allocation or a complete-mip lease.</summary>
    public IOriginalMapPostEffect3SourceTextureReadFootprintLease CaptureToneInfoSourceReadLease(
        OriginalMapPostEffect3ToneInfoReadFootprint footprint,OriginalMapPostEffect3TextureParameterState actualTable)
    {
        ArgumentNullException.ThrowIfNull(footprint);ArgumentNullException.ThrowIfNull(actualTable);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(footprint.SourceSha256!=Source.Source.Sha256||footprint.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                footprint.FrameNumber<=0||actualTable.FrameNumber!=footprint.FrameNumber||actualTable.SourceSha256!=footprint.SourceSha256||
                actualTable.ContextOwnerIdentity!=footprint.ContextOwnerIdentity||footprint.SourcePassIndex!=44||footprint.SourceRegister!=1||
                footprint.ParameterIndex!=5||footprint.TextureMipLevel!=0||footprint.TextureWidth!=4||footprint.TextureHeight!=1||
                footprint.RegionX!=0||footprint.RegionY!=0||footprint.RegionWidth!=1||footprint.RegionHeight!=1)
                throw new InvalidDataException("ToneInfo footprint is not this actual PS3/pass44 t1 source consumer.");
            var accepted=actualTable.Require(5);
            if(accepted.Reference.PhysicalAllocationParameterIndex!=44)
                throw new InvalidDataException("Lens ToneInfo alias5 does not refer to the actual original ToneMapInfo44 allocation.");
            var texture=Require(Textures,44,accepted.Reference.InstanceIndex,"ToneInfo sampled target");
            if(texture.Shape.Width!=4||texture.Shape.Height!=1||texture.Shape.MipLevels!=1||texture.Shape.Depth!=1)
                throw new InvalidDataException("ToneInfo source consumer retains its actual full4x1 allocation.");
            ToneInfoRegionContents? region=null;
            if(footprint.RequiresTextureRead)
            {
                if(!toneInfoRegions.TryGetValue((44,texture.InstanceIndex,0),out region)||region.Frame!=footprint.FrameNumber||
                    !textureContents.TryGetValue((44,texture.InstanceIndex,0),out var current)||current.Revision!=region.Revision||
                    !Contains(region,footprint.RegionX,footprint.RegionY,footprint.RegionWidth,footprint.RegionHeight))
                    throw new InvalidDataException("Actual ToneInfo texel0 read has no current source-defined region.");
            }
            uint format=texture.Shape.Formats.ShaderResourceFormat0;
            if(format==0||!texture.NativeFormatViews.TryGetValue(format,out var view))
                throw new InvalidDataException("ToneInfo source consumer has no actual native sampled view.");
            return new ToneInfoSourceReadLease(this,texture,view,format,footprint,actualTable,accepted,region);
        }
    }

    private static bool Contains(ToneInfoRegionContents region,uint x,uint y,uint width,uint height)
        =>width>0&&height>0&&x>=region.X&&y>=region.Y&&
            (ulong)x+width<=(ulong)region.X+region.Width&&(ulong)y+height<=(ulong)region.Y+region.Height;

    private sealed record ToneInfoSourceReadLease(OriginalMapPostEffect3ResourceBank Bank,
        OriginalMapPostEffect3DeviceTexture Texture,Rid ShaderViewRid,uint NativeViewFormat,
        OriginalMapPostEffect3ToneInfoReadFootprint Footprint,OriginalMapPostEffect3TextureParameterState Table,
        OriginalMapPostEffect3ResolvedTextureParameter Accepted,ToneInfoRegionContents? Region)
        :IOriginalMapPostEffect3SourceTextureReadFootprintLease
    {
        public long FrameNumber=>Footprint.FrameNumber;
        public string ContextOwnerIdentity=>Footprint.ContextOwnerIdentity;
        public int ParameterIndex=>Accepted.ParameterIndex;
        public int InstanceIndex=>Texture.InstanceIndex;
        public Rid StorageRid=>Texture.StorageRid;
        public RenderingDevice.DataFormat DeviceFormat=>ViewFormat(NativeViewFormat);
        public Vector3I Extent=>new(4,1,1);
        public string SourceEvidence=>Footprint.SourceEvidence+";"+(Region?.Evidence??"source-CB-predicate-proves-no-ToneInfo-texture-read;no-initialized-texel-claim");
        public void RequireCurrent()
        {
            lock(Bank.lifetime)
            {
                ObjectDisposedException.ThrowIf(Bank.retiring,Bank);Table.ValidateCurrent(Accepted);
                if(!ReferenceEquals(Require(Bank.Textures,44,InstanceIndex,"ToneInfo source footprint"),Texture))
                    throw new InvalidDataException("ToneInfo footprint physical owner changed.");
                if(Footprint.RequiresTextureRead&&
                    (Region is null||!Bank.toneInfoRegions.TryGetValue((44,InstanceIndex,0),out var currentRegion)||
                        currentRegion!=Region||!Bank.textureContents.TryGetValue((44,InstanceIndex,0),out var current)||
                        current.Revision!=Region.Revision||current.Frame!=FrameNumber))
                    throw new InvalidDataException("ToneInfo read region was invalidated by another source dispatch/frame.");
            }
        }
        public void RequireReadableSourceFootprint(int sourcePassIndex,uint sourceRegister,string programSha256,
            IReadOnlyList<OriginalMapPostEffect3ComputeConstantBufferReadReceipt> actualConstantBufferReads)
        {
            RequireCurrent();
            if(sourcePassIndex!=Footprint.SourcePassIndex||sourceRegister!=Footprint.SourceRegister||programSha256!=Footprint.ProgramSha256)
                throw new InvalidDataException("ToneInfo partial region cannot supply another shader/pass/register footprint.");
            foreach(var required in Footprint.RequiredConstantBufferBytesSha256)
            {
                var reads=actualConstantBufferReads.Where(r=>r.ParameterIndex==required.Key).ToArray();
                if(reads.Length!=1||!reads[0].HasCompleteWriterContents||reads[0].CompleteBytesSha256!=required.Value)
                    throw new InvalidDataException("ToneInfo read branch has another actually uploaded Composite CB predicate.");
                var actual=Bank.RequireConstantBufferForPass(required.Key,reads[0].InstanceIndex,sourcePassIndex,FrameNumber);
                if(actual.StorageRid!=reads[0].StorageRid||actual.UploadRevision!=reads[0].UploadRevision||actual.CompleteBytesSha256!=required.Value)
                    throw new InvalidDataException("ToneInfo consumer constant-buffer upload changed while its draw was queued.");
            }
        }
    }
}
