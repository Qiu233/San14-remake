using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    public IOriginalMapPostEffect3ComputeMipTextureLease PublishShaftGraphicsTextureCoverage(
        OriginalMapPostEffect3ShaftGraphicsWriteCoverage coverage,OriginalMapPostEffect3ShaftExecutionOwner actualShaftOwner,
        OriginalMapPostEffect3StockGraphicsExecutor actualExecutor,OriginalMapPostEffect3StockGraphicsCompletedPass completed,
        long writerRevision)
    {
        ArgumentNullException.ThrowIfNull(coverage);ArgumentNullException.ThrowIfNull(actualShaftOwner);
        ArgumentNullException.ThrowIfNull(actualExecutor);ArgumentNullException.ThrowIfNull(completed);
        // The actual Shaft owner privately captured the draw producer and all
        // full-viewport/no-blend/no-cull/no-depth/no-stencil source conditions.
        // A generic stock receipt does not assert those raster facts.
        actualShaftOwner.RequireCurrentActualStockCoverage(actualExecutor,completed,writerRevision);
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(actualExecutor.Bank,this)||!ReferenceEquals(actualExecutor.LastCompletedPass,completed)||
                coverage.SourceSha256!=Source.Source.Sha256||coverage.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                coverage.FrameNumber<=0||coverage.SourcePassIndex!=7||coverage.ParameterIndex!=11||coverage.TextureMipLevel!=0||
                completed.FrameNumber!=coverage.FrameNumber||completed.ContextOwnerIdentity!=coverage.ContextOwnerIdentity||
                completed.SourcePassIndex!=7||completed.PixelProgramSha256!=coverage.ProgramSha256||
                completed.TargetParameterIndex!=11||
                completed.StockVertexConstants.Length!=32||completed.StockQuad.Length!=96)
                throw new InvalidDataException("Shaft graphics coverage is not this actual source pass7/stock VS/PS1 draw.");
            var texture=Require(Textures,11,completed.TargetInstanceIndex,"Shaft full color output");
            var key=(11,completed.TargetInstanceIndex,0u);
            if(texture.Shape.Dimension!=OriginalMapPostEffect3TextureDimension.Texture2D||texture.Shape.Depth!=1||
                texture.Shape.MipLevels!=1||texture.Shape.Width!=coverage.TextureWidth||texture.Shape.Height!=coverage.TextureHeight||
                texture.StorageRid!=completed.TargetStorageRid||
                !texture.NativeFormatViews.TryGetValue(texture.Shape.Formats.RenderTargetFormat0,out var actualTarget)||
                actualTarget!=completed.TargetViewRid||!textureContents.TryGetValue(key,out var current)||
                current.Revision!=writerRevision||current.Frame!=coverage.FrameNumber||current.Pass!=7)
                throw new InvalidDataException("Shaft full raster write has another actual target storage/side/view/revision.");
            var cb=RequireConstantBufferForPass(coverage.RequiredConstantBufferParameterIndex,
                actualShaftOwner.LastComputeChain!.Prefix.ActualShaftConstantBufferInstance,7,coverage.FrameNumber);
            if(!cb.HasCompleteWriterContents)
                throw new InvalidDataException("Shaft7 actual constant-buffer values differ from its exact source write proof.");
            textureContents[key]=current with{Complete=true,Evidence=coverage.SourceEvidence+";"+completed.SourceEvidence+
                ";private-owned-full-stock-raster-proof;all-source-target11-mip0-RGBA-defined"};
            return CaptureSourceWrittenTextureLease(11,completed.TargetInstanceIndex,coverage.FrameNumber,0,1);
        }
    }
}
