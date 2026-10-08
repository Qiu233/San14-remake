using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    private sealed record TextureContents(long Revision,long Frame,int Pass,bool Complete,string Evidence);
    private readonly Dictionary<(int Parameter,int Side,uint Mip),TextureContents> textureContents=[];

    internal long InvalidateTextureAfterShaderWrite(int physicalIndex,int side,uint mip,long frame,int pass)
    {
        lock(lifetime)
        {
            var texture=Require(Textures,physicalIndex,side,"written texture");
            if(mip>=texture.Shape.MipLevels||frame<=0)throw new InvalidDataException("Texture write has another mip/frame.");
            var key=(physicalIndex,side,mip);
            long revision=checked((textureContents.TryGetValue(key,out var previous)?previous.Revision:0)+1);
            textureContents[key]=new(revision,frame,pass,false,"actual submitted source shader write; coverage not yet established");
            return revision;
        }
    }

    /// <summary>Only CS30's positively proved complete ClearGhost stores.
    /// Allocation, a dispatch receipt, or another shader cannot grant this
    /// capability. Later writes invalidate it before GPU submission.</summary>
    public IOriginalMapPostEffect3ComputeMipTextureLease PublishLensTextureCoverage(
        OriginalMapPostEffect3LensWriteCoverage coverage,OriginalMapPostEffect3ComputeExecutor actualExecutor,
        OriginalMapPostEffect3ComputeCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(coverage);ArgumentNullException.ThrowIfNull(actualExecutor);
        ArgumentNullException.ThrowIfNull(completed);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(actualExecutor.Bank,this)||!ReferenceEquals(actualExecutor.LastCompletedPass,completed)||
                coverage.Table!=OriginalMapPostEffect3BindingTable.TextureParameters||coverage.SourcePassIndex!=32||coverage.ParameterIndex!=41||
                coverage.SourceSha256!=Source.Source.Sha256||coverage.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                completed.FrameNumber!=coverage.FrameNumber||completed.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                completed.SourcePassIndex!=32||completed.ProgramSha256!=coverage.ProgramSha256||completed.Dispatch!=coverage.ActualDispatch||
                completed.ActualShaderLocalSize!=new Vector3I(16,16,1))
                throw new InvalidDataException("ClearGhost coverage is not this actual source executor/frame/dispatch.");
            var write=completed.Writes.Single(w=>w.ParameterIndex==41);
            var texture=Require(Textures,write.PhysicalAllocationParameterIndex,write.InstanceIndex,"covered texture");
            var key=(write.PhysicalAllocationParameterIndex,write.InstanceIndex,0u);
            if(write.PhysicalAllocationParameterIndex!=41||write.MipLevel!=0||write.MipLevelCount!=1||
                texture.Shape.MipLevels!=1||texture.StorageRid!=write.StorageRid||
                texture.Shape.Width!=coverage.TextureWidth||texture.Shape.Height!=coverage.TextureHeight||
                write.Extent!=new Vector3I((int)coverage.TextureWidth,(int)coverage.TextureHeight,1)||
                !textureContents.TryGetValue(key,out var submitted)||submitted.Revision!=write.WriterRevision||
                submitted.Frame!=coverage.FrameNumber||submitted.Pass!=32)
                throw new InvalidDataException("ClearGhost physical storage, mip, side or writer revision changed.");
            textureContents[key]=submitted with{Complete=true,Evidence=coverage.SourceEvidence+";"+completed.SourceEvidence};
            return CaptureCurrentTextureLease(41,write.InstanceIndex,coverage.FrameNumber);
        }
    }

    public IOriginalMapPostEffect3ComputeMipTextureLease CaptureCurrentTextureLease(int parameter,int side,long frame)
    {
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var texture=Require(Textures,parameter,side,"readable texture");
            if(texture.Shape.MipLevels!=1||!textureContents.TryGetValue((parameter,side,0),out var contents)||
                !contents.Complete||contents.Frame!=frame)
                throw new InvalidDataException("Texture has no complete same-frame source coverage.");
            uint format=texture.Shape.Formats.ShaderResourceFormat0;
            if(format==0||!texture.NativeFormatViews.TryGetValue(format,out var view))
                throw new InvalidDataException("Covered texture has no source shader view.");
            return new CoveredTextureLease(this,texture,frame,contents.Revision,format,view,contents.Evidence);
        }
    }

    private sealed record CoveredTextureLease(OriginalMapPostEffect3ResourceBank Bank,
        OriginalMapPostEffect3DeviceTexture Texture,long FrameNumber,long Revision,uint NativeViewFormat,
        Rid ShaderViewRid,string SourceEvidence):IOriginalMapPostEffect3ComputeMipTextureLease
    {
        public string ContextOwnerIdentity=>Bank.Source.ContextOwnerIdentity;
        public int ParameterIndex=>Texture.Source.Parameter.Index;
        public int InstanceIndex=>Texture.InstanceIndex;
        public Rid StorageRid=>Texture.StorageRid;
        public RenderingDevice.DataFormat DeviceFormat=>ViewFormat(NativeViewFormat);
        public Vector3I Extent=>new((int)Texture.Shape.Width,(int)Texture.Shape.Height,(int)Texture.Shape.Depth);
        public void RequireCurrent()
        {
            lock(Bank.lifetime)
            {
                ObjectDisposedException.ThrowIf(Bank.retiring,Bank);
                if(!ReferenceEquals(Require(Bank.Textures,ParameterIndex,InstanceIndex,"covered texture"),Texture))
                    throw new InvalidDataException("Covered texture owner changed.");
                if(!Bank.textureContents.TryGetValue((ParameterIndex,InstanceIndex,0),out var current)||
                    !current.Complete||current.Frame!=FrameNumber||current.Revision!=Revision)
                    throw new InvalidDataException("Covered texture pixels were replaced or invalidated.");
            }
        }
        public void RequireReadableMipRange(uint first,uint count)
        {
            RequireCurrent();
            if(first!=0||count!=1)throw new InvalidDataException("ClearGhost covers only its actual sole mip0.");
        }
    }
}
