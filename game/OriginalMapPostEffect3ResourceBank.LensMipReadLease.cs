using Godot;
using San14.Data;

/// <summary>A whole native SRV with initialized mips for one exact original
/// Lens shader footprint. It cannot grant unread mips or change native LODs.</summary>
public interface IOriginalMapPostEffect3LensMipReadLease:
    IOriginalMapPostEffect3ComputeMipTextureLease,IOriginalMapPostEffect3SourceTextureReadFootprintLease
{
    OriginalMapPostEffect3LensTextureReadFootprint RequireLensMipFootprint(int sourcePassIndex,uint sourceRegister);
}

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    public IOriginalMapPostEffect3LensMipReadLease CaptureLensSourceMipReadLease(
        OriginalMapPostEffect3LensTextureReadFootprint footprint,
        OriginalMapPostEffect3TextureParameterState actualTable)
        =>CaptureLensSourceMipReadLease(Array.AsReadOnly(new[]{footprint}),actualTable);

    public IOriginalMapPostEffect3LensMipReadLease CaptureLensSourceMipReadLease(
        IReadOnlyList<OriginalMapPostEffect3LensTextureReadFootprint> actualFootprints,
        OriginalMapPostEffect3TextureParameterState actualTable)
    {
        ArgumentNullException.ThrowIfNull(actualFootprints);ArgumentNullException.ThrowIfNull(actualTable);
        var footprints=Array.AsReadOnly(actualFootprints.ToArray());
        if(footprints.Count==0)throw new InvalidDataException("Lens mip read needs at least one positively closed shader register footprint.");
        var footprint=footprints[0];
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(footprint.FrameNumber!=actualTable.FrameNumber||footprint.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                footprint.SourceSha256!=Source.Source.Sha256||actualTable.SourceSha256!=footprint.SourceSha256||
                actualTable.ContextOwnerIdentity!=footprint.ContextOwnerIdentity||!footprint.RetainsOriginalFullShaderResourceView||
                footprints.Any(f=>f.FrameNumber!=footprint.FrameNumber||f.ContextOwnerIdentity!=footprint.ContextOwnerIdentity||
                    f.SourceSha256!=footprint.SourceSha256||f.SourcePassIndex!=footprint.SourcePassIndex||
                    f.ParameterIndex!=footprint.ParameterIndex||f.RequiredMipLevels.Count==0||!f.RetainsOriginalFullShaderResourceView)||
                footprints.Select(f=>f.SourceRegister).Distinct().Count()!=footprints.Count)
                throw new InvalidDataException("Lens mip footprint/table has another actual source/frame/module owner.");
            var accepted=actualTable.Require(footprint.ParameterIndex);
            if(accepted.Reference.PhysicalAllocationParameterIndex is not int physical)
                throw new NotSupportedException("Source-owned Lens mip coverage cannot stand in for an external producer.");
            var mips=footprints.SelectMany(f=>f.RequiredMipLevels).Distinct().Order()
                .ToDictionary(m=>checked((uint)m),m=>CaptureSourceWrittenTextureLease(physical,
                    accepted.Reference.InstanceIndex,footprint.FrameNumber,checked((uint)m),1));
            var first=mips.First().Value;
            if(mips.Values.Any(m=>m.StorageRid!=first.StorageRid||m.ShaderViewRid!=first.ShaderViewRid||
                m.NativeViewFormat!=first.NativeViewFormat||m.Extent!=first.Extent))
                throw new InvalidDataException("Lens mip receipts do not retain the same original whole shader view.");
            return new LensSourceMipReadLease(this,actualTable,accepted,footprints,
                new System.Collections.ObjectModel.ReadOnlyDictionary<uint,IOriginalMapPostEffect3ComputeMipTextureLease>(mips));
        }
    }

    private sealed record LensSourceMipReadLease(OriginalMapPostEffect3ResourceBank Bank,
        OriginalMapPostEffect3TextureParameterState Table,OriginalMapPostEffect3ResolvedTextureParameter Accepted,
        IReadOnlyList<OriginalMapPostEffect3LensTextureReadFootprint> Footprints,
        IReadOnlyDictionary<uint,IOriginalMapPostEffect3ComputeMipTextureLease> Mips):IOriginalMapPostEffect3LensMipReadLease
    {
        private OriginalMapPostEffect3LensTextureReadFootprint FirstFootprint=>Footprints[0];
        private IOriginalMapPostEffect3ComputeMipTextureLease FirstMip=>Mips.First().Value;
        public long FrameNumber=>FirstFootprint.FrameNumber;
        public string ContextOwnerIdentity=>FirstFootprint.ContextOwnerIdentity;
        public int ParameterIndex=>Accepted.ParameterIndex;
        public int InstanceIndex=>FirstMip.InstanceIndex;
        public Rid ShaderViewRid=>FirstMip.ShaderViewRid;
        public Rid StorageRid=>FirstMip.StorageRid;
        public uint NativeViewFormat=>FirstMip.NativeViewFormat;
        public RenderingDevice.DataFormat DeviceFormat=>FirstMip.DeviceFormat;
        public Vector3I Extent=>FirstMip.Extent;
        public string SourceEvidence=>string.Join(";",Footprints.Select(f=>f.SourceEvidence))+";actual-source-written-mips:"+
            string.Join(";",Mips.Values.Select(m=>m.SourceEvidence));
        public void RequireCurrent()
        {Table.ValidateCurrent(Accepted);foreach(var mip in Mips.Values)mip.RequireCurrent();}
        public OriginalMapPostEffect3LensTextureReadFootprint RequireLensMipFootprint(int pass,uint register)
        {
            RequireCurrent();return Footprints.SingleOrDefault(f=>f.SourcePassIndex==pass&&f.SourceRegister==register)
                ??throw new InvalidDataException("Lens completed mip set has no actual source footprint for this selected register.");
        }
        public void RequireReadableMipRange(uint first,uint count)
        {
            RequireCurrent();
            if(count==0||count>32||first>31||count>32-first)
                throw new InvalidDataException("Lens mip read range is outside its finite source footprint.");
            for(uint mip=first;mip-first<count;mip++)
            {
                if(!Mips.TryGetValue(mip,out var contents))throw new InvalidDataException("Lens shader footprint does not establish this additional mip's contents.");
                contents.RequireReadableMipRange(mip,1);
            }
        }
        public void RequireReadableSourceFootprint(int pass,uint register,string actualProgramSha256,
            IReadOnlyList<OriginalMapPostEffect3ComputeConstantBufferReadReceipt> actualConstantBufferReads)
        {
            RequireCurrent();
            var footprint=RequireLensMipFootprint(pass,register);
            if(actualProgramSha256!=footprint.ProgramSha256)
                throw new InvalidDataException("Lens partial mip contents cannot supply another selected shader/register footprint.");
            foreach(var required in footprint.RequiredConstantBufferBytesSha256)
            {
                var matches=actualConstantBufferReads.Where(r=>r.ParameterIndex==required.Key).ToArray();
                if(matches.Length!=1||!matches[0].HasCompleteWriterContents||matches[0].CompleteBytesSha256!=required.Value)
                    throw new InvalidDataException("Lens explicit source LOD uses a different actual uploaded constant buffer.");
                var read=matches[0];var current=Bank.RequireConstantBufferForPass(required.Key,read.InstanceIndex,pass,FrameNumber);
                if(current.StorageRid!=read.StorageRid||current.UploadRevision!=read.UploadRevision||current.CompleteBytesSha256!=required.Value)
                    throw new InvalidDataException("Lens source LOD constant buffer changed before actual dispatch.");
            }
            foreach(int mip in footprint.RequiredMipLevels)RequireReadableMipRange(checked((uint)mip),1);
        }
    }
}
