using Godot;
using San14.Data;

public sealed partial class PostProcessResourceBank
{
    /// <summary>The original PS3 explicit-LOD read domain, resolved against
    /// the actual complete native SRV's mip count. The view is never narrowed.</summary>
    public IMapPostEffect3SourceTextureReadFootprintLease CaptureCompositeSourceMipReadLease(
        MapPostEffect3CompositeTextureReadFootprint footprint,
        MapPostEffect3TextureParameterState table)
    {
        ArgumentNullException.ThrowIfNull(footprint);ArgumentNullException.ThrowIfNull(table);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(footprint.SourceSha256!=Source.Source.Sha256||footprint.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                footprint.FrameNumber!=table.FrameNumber||table.SourceSha256!=footprint.SourceSha256||
                table.ContextOwnerIdentity!=footprint.ContextOwnerIdentity||footprint.SourcePassIndex!=44)
                throw new InvalidDataException("Composite mip read requires this exact source/module/table/frame.");
            var accepted=table.Require(footprint.ParameterIndex);
            if(accepted.Reference.PhysicalAllocationParameterIndex is not int physical)
                throw new NotSupportedException("Owned Composite mip publication cannot stand in for an external native view producer.");
            var texture=Require(Textures,physical,accepted.Reference.InstanceIndex,"Composite source mip view");
            if(texture.Shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||texture.Shape.Depth!=1)
                throw new NotSupportedException("Original Composite footprint requires its actual ordinary2D complete native view.");
            var levels=footprint.ResolveActualMipLevels(checked((uint)texture.Shape.MipLevels));
            if(levels.Count==0&&footprint.RequiresTextureRead)
                throw new InvalidDataException("Composite positive source read has no resolved native mip contents.");
            uint format=texture.Shape.Formats.ShaderResourceFormat0;
            if(format==0||!texture.NativeFormatViews.TryGetValue(format,out var view))
                throw new InvalidDataException("Actual Composite source allocation has no native full shader-resource view.");
            var mips=levels.Select(level=>CaptureSourceWrittenTextureLease(physical,accepted.Reference.InstanceIndex,
                footprint.FrameNumber,checked((uint)level),1)).ToArray();
            return new CompositeSourceMipReadLease(this,table,accepted,texture,footprint,format,view,Array.AsReadOnly(mips));
        }
    }

    private sealed record CompositeSourceMipReadLease(PostProcessResourceBank Bank,
        MapPostEffect3TextureParameterState Table,MapPostEffect3ResolvedTextureParameter Accepted,
        MapPostEffect3DeviceTexture Texture,MapPostEffect3CompositeTextureReadFootprint Footprint,
        uint NativeViewFormat,Rid ShaderViewRid,
        IReadOnlyList<IMapPostEffect3ComputeMipTextureLease> Mips):IMapPostEffect3SourceTextureReadFootprintLease
    {
        public long FrameNumber=>Footprint.FrameNumber;
        public string ContextOwnerIdentity=>Footprint.ContextOwnerIdentity;
        public int ParameterIndex=>Accepted.ParameterIndex;
        public int InstanceIndex=>Texture.InstanceIndex;
        public Rid StorageRid=>Texture.StorageRid;
        public RenderingDevice.DataFormat DeviceFormat=>ViewFormat(NativeViewFormat);
        public Vector3I Extent=>new(checked((int)Texture.Shape.Width),checked((int)Texture.Shape.Height),1);
        public string SourceEvidence=>Footprint.SourceEvidence+";actual-full-view-mipcount="+Texture.Shape.MipLevels+
            ";"+string.Join(";",Mips.Select(m=>m.SourceEvidence));
        public void RequireCurrent()
        {
            Table.ValidateCurrent(Accepted);
            if(!ReferenceEquals(Bank.RequireTexture(Texture.Source.Parameter.Index,InstanceIndex),Texture))
                throw new InvalidDataException("Actual Composite native view allocation changed.");
            foreach(var mip in Mips)mip.RequireCurrent();
        }
        public void RequireReadableSourceFootprint(int pass,uint register,string programSha,
            IReadOnlyList<MapPostEffect3ComputeConstantBufferReadReceipt> actualConstantBufferReads)
        {
            RequireCurrent();
            if(pass!=44||register!=Footprint.SourceRegister||programSha!=Footprint.ProgramSha256)
                throw new InvalidDataException("Composite mip footprint cannot authorize another actual shader/register.");
            foreach(var required in Footprint.RequiredConstantBufferBytesSha256)
            {
                var matches=actualConstantBufferReads.Where(r=>r.ParameterIndex==required.Key).ToArray();
                if(matches.Length!=1||!matches[0].HasCompleteWriterContents||matches[0].CompleteBytesSha256!=required.Value)
                    throw new InvalidDataException("Composite sampled mip predicate differs from the actual uploaded source CB.");
                var read=matches[0];var cb=Bank.RequireConstantBufferForPass(required.Key,read.InstanceIndex,pass,FrameNumber);
                if(cb.StorageRid!=read.StorageRid||cb.UploadRevision!=read.UploadRevision||cb.CompleteBytesSha256!=required.Value)
                    throw new InvalidDataException("Composite actual uploaded CB changed before its source mip read.");
            }
        }
    }
}
