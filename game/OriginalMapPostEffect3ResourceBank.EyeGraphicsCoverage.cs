using Godot;
using San14.Data;

public sealed partial class OriginalMapPostEffect3ResourceBank
{
    /// <summary>Joins the original Eye shader's exact pixel domain with the
    /// submitted VS/PS, current CB bytes, actual source reads and target mip.
    /// The completed draw receipt alone never establishes complete pixels.</summary>
    public IOriginalMapPostEffect3ComputeMipTextureLease PublishEyeGraphicsCoverage(
        OriginalMapPostEffect3EyeGraphicsWriteCoverage coverage,
        OriginalMapPostEffect3FullscreenGraphicsExecutor actualExecutor,
        OriginalMapPostEffect3FullscreenCompletedPass completed)
    {
        ArgumentNullException.ThrowIfNull(coverage);ArgumentNullException.ThrowIfNull(actualExecutor);
        ArgumentNullException.ThrowIfNull(completed);coverage.RequireCurrent();
        lock(lifetime)
        {
            ObjectDisposedException.ThrowIf(retiring,this);
            if(!ReferenceEquals(actualExecutor.Bank,this)||!ReferenceEquals(actualExecutor.LastCompletedPass,completed)||
                coverage.FrameNumber!=completed.FrameNumber||coverage.ContextOwnerIdentity!=Source.ContextOwnerIdentity||
                completed.ContextOwnerIdentity!=Source.ContextOwnerIdentity||coverage.SourceSha256!=Source.Source.Sha256||
                coverage.SourcePassIndex!=completed.SourcePassIndex||coverage.VertexProgramSha256!=completed.VertexProgramSha256||
                coverage.PixelProgramSha256!=completed.PixelProgramSha256)
                throw new InvalidDataException("Eye pixel coverage belongs to another actual source GPU submission.");
            static OriginalMapPostEffect3PixelRectangle Rect(Rect2I r)=>new(r.Position.X,r.Position.Y,r.Size.X,r.Size.Y);
            if(!coverage.HasCompleteMipPixelDomain(Rect(completed.ActualViewport),
                completed.ActualScissor is{} scissor?Rect(scissor):null))
                throw new NotSupportedException("Cropped Eye source scissor does not write the entire physical mip.");
            var raster=completed.ActualRaster;
            if(raster.DepthTest||raster.DepthWrite||raster.NativeStencil is not null||
                raster.Cull!=RenderingDevice.PolygonCullMode.Disabled||raster.Attachments is not [var color]||
                !color.WriteR||!color.WriteG||!color.WriteB||!color.WriteA||
                color.EnableBlend!=coverage.RequiresPriorTargetContents)
                throw new NotSupportedException("Eye complete pixels require actual full RGBA writes and source raster without sample rejection.");
            // Original temporary F4=00111100 / RT0 blend-enable1 means
            // additive One/One for both RGB and alpha, independently of the
            // earlier inherited blend state. EnableBlend alone is insufficient.
            if(color.EnableBlend&&(coverage.RequiredTemporaryNativeColorStateF4!=0x111100u||
                coverage.RequiredTemporaryNativeRt0BlendEnable1C0!=1||
                color.SourceColor!=RenderingDevice.BlendFactor.One||color.DestinationColor!=RenderingDevice.BlendFactor.One||
                color.ColorOperation!=RenderingDevice.BlendOperation.Add||
                color.SourceAlpha!=RenderingDevice.BlendFactor.One||color.DestinationAlpha!=RenderingDevice.BlendFactor.One||
                color.AlphaOperation!=RenderingDevice.BlendOperation.Add))
                throw new InvalidDataException("Eye upsample coverage requires its actual native additive RGB/alpha blend formula.");
            foreach(var cb in coverage.RequiredConstantBufferBytesSha256)
            {
                var reads=completed.ConstantBufferReads.Where(r=>r.ParameterIndex==cb.Key).ToArray();
                if(reads.Length!=1||!reads[0].HasCompleteWriterContents||reads[0].CompleteBytesSha256!=cb.Value)
                    throw new InvalidDataException("Eye full-mip shader write used different original constant-buffer bytes.");
            }
            foreach(var read in coverage.RequiredShaderReads)
            {
                var receipts=completed.TextureReads.Where(r=>r.SourceStageCode==read.SourceStageCode&&r.SourceRegister==read.SourceRegister).ToArray();
                var binding=completed.OrderedEffectiveBindings.LastOrDefault(b=>b.Role==OriginalMapPostEffect3BindingRole.ShaderResourceRead&&
                    b.Table==OriginalMapPostEffect3BindingTable.TextureParameters&&b.RegisterIndex==read.SourceRegister&&
                    (b.StageMask&(read.SourceStageCode==1?2:4))!=0);
                var expected=read.NativeBinding;
                if(receipts.Length!=1||binding is null||binding.ParameterIndex!=expected.ParameterIndex||
                    binding.NativeViewKind!=expected.NativeViewKind||binding.MipSelector!=expected.MipSelector||
                    binding.SliceSelector!=expected.SliceSelector||binding.SourceRowIndex!=expected.SourceRowIndex||
                    receipts[0].ParameterIndex!=expected.ParameterIndex||string.IsNullOrWhiteSpace(receipts[0].SourceEvidence))
                    throw new InvalidDataException("Eye complete shader write used another current original texture setter.");
                var receipt=receipts[0];
                if(read.ActualSelectedPhysicalMipLevel is{} mip&&
                    (receipt.NativeFirstMipLevel!=mip||receipt.NativeMipLevelCount!=1))
                    throw new InvalidDataException("Eye sampled another physical mip than its source Before producer.");
                if(!read.RequiresInitializedContents&&!receipt.HasSourceReadFootprintProof)
                    throw new InvalidDataException("An executed Eye load without output dependency still requires its exact source value-influence proof.");
            }
            if(completed.Writes is not [var write]||write.SourceTargetRegister!=coverage.SourceTargetRegister||
                write.ParameterIndex!=coverage.PhysicalAllocationParameterIndex||write.PhysicalAllocationParameterIndex!=coverage.PhysicalAllocationParameterIndex||
                write.NativeMipLevel!=coverage.TextureMipLevel||write.WriterRevision is not{} revision||
                write.PriorDestinationPixelsWereValidated!=coverage.RequiresPriorTargetContents)
                throw new InvalidDataException("Eye source full-mip coverage does not match its actual submitted target writer.");
            int physical=coverage.PhysicalAllocationParameterIndex;uint targetMip=checked((uint)coverage.TextureMipLevel);
            var texture=Require(Textures,physical,write.InstanceIndex,"Eye source-covered mip");
            var key=(physical,write.InstanceIndex,targetMip);
            if(texture.StorageRid!=write.StorageRid||write.NativeViewFormat!=texture.Shape.Formats.RenderTargetFormat0||
                write.Extent!=new Vector2I((int)coverage.TextureWidth,(int)coverage.TextureHeight)||
                Math.Max(1u,texture.Shape.Width>>(int)targetMip)!=coverage.TextureWidth||
                Math.Max(1u,texture.Shape.Height>>(int)targetMip)!=coverage.TextureHeight||
                !textureContents.TryGetValue(key,out var current)||current.Revision!=revision||
                current.Frame!=coverage.FrameNumber||current.Pass!=coverage.SourcePassIndex)
                throw new InvalidDataException("Eye GPU storage, selected mip or writer revision changed after submission.");
            textureContents[key]=current with{Complete=true,Evidence=coverage.SourceEvidence+";"+completed.SourceEvidence};
            return CaptureSourceWrittenTextureLease(physical,write.InstanceIndex,coverage.FrameNumber,targetMip,1);
        }
    }
}
