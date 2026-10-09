using Godot;
using San14.Data;

/// <summary>Issued only by the actual bank before its source Ghost draw. It
/// preserves proof of already complete destination contents while invalidating
/// the prior readable revision. It does not itself prove a submitted draw.</summary>
public sealed class MapPostEffect3LensGhostTextureWrite
{
    internal MapPostEffect3LensGhostTextureWrite(long frame,string owner,int physical,int side,
        Rid storage,long revision,string evidence)
        =>(FrameNumber,ContextOwnerIdentity,PhysicalAllocationParameterIndex,InstanceIndex,StorageRid,WriterRevision,SourceEvidence)=
            (frame,owner,physical,side,storage,revision,evidence);
    public long FrameNumber{get;}
    public string ContextOwnerIdentity{get;}
    public int PhysicalAllocationParameterIndex{get;}
    public int InstanceIndex{get;}
    public uint MipLevel=>0;
    public Rid StorageRid{get;}
    public long WriterRevision{get;}
    public string SourceEvidence{get;}
}

public sealed partial class PostProcessResourceBank
{
    private sealed record PendingLensGhostWrite(MapPostEffect3LensGhostTextureWrite Token,
        TextureContents PreviousComplete,MapPostEffect3DeviceTexture Texture);
    private PendingLensGhostWrite? pendingLensGhostWrite;

    /// <summary>Exact pass34 may update part of an already initialized texture.
    /// A partial draw preserves full initialization, but replaces pixel revision.
    /// Failure leaves that revision unreadable; no failed draw restores it.</summary>
    public MapPostEffect3LensGhostTextureWrite BeginLensGhostPreservingTextureWrite(
        MapPostEffect3LensExecutionRecipe recipe,int physicalParameterIndex,int instanceIndex)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(pendingLensGhostWrite is not null||!recipe.ExecutesPass||recipe.SourcePassIndex!=34||recipe.GhostDraw is null||
                recipe.SourceSha256!=Source.Source.Sha256||recipe.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                recipe.FrameNumber<=0||physicalParameterIndex!=41)
                throw new InvalidDataException("Preserving Ghost write requires its actual source pass34 and current owned target41.");
            var texture=Require(Textures,physicalParameterIndex,instanceIndex,"Ghost preserving target");
            var key=(physicalParameterIndex,instanceIndex,0u);
            if(texture.Shape.Dimension!=MapPostEffect3TextureDimension.Texture2D||texture.Shape.Depth!=1||texture.Shape.MipLevels!=1||
                !textureContents.TryGetValue(key,out var old)||!old.Complete||old.Frame!=recipe.FrameNumber)
                throw new InvalidDataException("Partial Ghost rendering cannot initialize an unknown destination texture.");
            long revision=InvalidateTextureAfterShaderWrite(physicalParameterIndex,instanceIndex,0,recipe.FrameNumber,34);
            var token=new MapPostEffect3LensGhostTextureWrite(recipe.FrameNumber,Source.ContextOwnerIdentity,
                physicalParameterIndex,instanceIndex,texture.StorageRid,revision,
                old.Evidence+";"+recipe.SourceEvidence+";exact-Ghost34-partial-draw-retains-prior-complete-destination;old-revision="+old.Revision);
            pendingLensGhostWrite=new(token,old,texture);
            return token;
        }
    }

    public IMapPostEffect3ComputeMipTextureLease RetainLensGhostTextureCoverage(
        MapPostEffect3LensExecutionRecipe recipe,MapPostEffect3LensGhostExecutor actualExecutor,
        MapPostEffect3LensGhostCompletedDraw completed)
    {
        ArgumentNullException.ThrowIfNull(recipe);ArgumentNullException.ThrowIfNull(actualExecutor);ArgumentNullException.ThrowIfNull(completed);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            var pending=pendingLensGhostWrite??throw new InvalidDataException("Ghost draw has no private prior-complete writer scope.");
            var token=pending.Token;
            if(!ReferenceEquals(actualExecutor.Bank,this)||!ReferenceEquals(actualExecutor.LastCompletedDraw,completed)||
                !ReferenceEquals(completed.TextureWrite,token)||recipe.SourcePassIndex!=34||!recipe.ExecutesPass||recipe.GhostDraw is not{} draw||
                recipe.SourceSha256!=Source.Source.Sha256||recipe.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                recipe.FrameNumber!=token.FrameNumber||completed.FrameNumber!=token.FrameNumber||completed.ContextOwnerIdentity!=token.ContextOwnerIdentity||
                completed.VertexCount!=draw.VertexCount||completed.InstanceCount!=draw.InstanceCount||
                completed.TargetStorageRid!=token.StorageRid||token.PhysicalAllocationParameterIndex!=41||token.MipLevel!=0||
                !ReferenceEquals(Require(Textures,41,token.InstanceIndex,"completed Ghost target"),pending.Texture))
                throw new InvalidDataException("Ghost coverage retention is not the exact successful executor draw and pending physical writer.");
            var key=(41,token.InstanceIndex,0u);
            if(!pending.PreviousComplete.Complete||pending.PreviousComplete.Frame!=token.FrameNumber||
                !textureContents.TryGetValue(key,out var current)||current.Revision!=token.WriterRevision||
                current.Frame!=token.FrameNumber||current.Pass!=34||current.Complete)
                throw new InvalidDataException("Ghost destination had another write or its prior complete content was not preserved.");
            textureContents[key]=current with{Complete=true,Evidence=token.SourceEvidence+";"+completed.SourceEvidence};
            pendingLensGhostWrite=null;
            return CaptureSourceWrittenTextureLease(41,token.InstanceIndex,token.FrameNumber,0,1);
        }
    }
}
