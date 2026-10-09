using System.Security.Cryptography;
using Godot;
using San14.Data;

public sealed partial class MapPostEffect3Composite2GraphicsOwner
{
    private MapPostEffect3Composite2GraphicsCoverageReceipt CaptureCoverage(
        MapPostEffect3Composite2GraphicsRecipe recipe,IMapPostEffect3StockGraphicsTargetLease target,
        MapPostEffect3FullscreenCompletedPass completed)
    {
        RequireCurrent();recipe.RequireCurrent();int pass=recipe.Before.SourcePassIndex;
        var directory=Bank.Source.Source.Passes[pass];
        string Shader(uint stage)=>Bank.Source.Source.GetProgram(stage,directory.ProgramReferences.Single(r=>r.StageCode==stage).StageIndex!.Value).Sha256;
        if(!ReferenceEquals(Executor.LastCompletedPass,completed)||completed.SourcePassIndex!=pass||
            completed.FrameNumber!=Frame.FrameNumber||completed.ContextOwnerIdentity!=Frame.OwnerIdentity||
            completed.VertexProgramSha256!=Shader(1)||completed.PixelProgramSha256!=Shader(0)||
            completed.ConstantBufferReads is not [var cb]||cb.ParameterIndex!=22||!cb.HasCompleteWriterContents||
            cb.CompleteBytesSha256!=Convert.ToHexString(SHA256.HashData(Frame.Prepared.ConstantBuffer.Bytes.Span)))
            throw new InvalidDataException("Composite2 coverage does not join its actual selected source VS/PS and complete CB22 writer.");
        var nativeReads=Bank.Source.Passes[pass].ActiveBindings.Where(b=>
            b.Role==MapPostEffect3BindingRole.ShaderResourceRead&&b.Table==MapPostEffect3BindingTable.TextureParameters&&
            (b.StageMask&4)!=0).GroupBy(b=>b.RegisterIndex).Select(g=>g.Last()).ToArray();
        foreach(var row in nativeReads)
        {
            var nulls=completed.SourceNullShaderResources.Where(n=>n.SourceStageCode==0&&n.SourceRegister==row.RegisterIndex).ToArray();
            if(nulls.Length!=0)
            {
                if(nulls is not [var actualNull]||actualNull.Binding?.SourceRowIndex!=row.SourceRowIndex||actualNull.ParameterIndex!=row.ParameterIndex)
                    throw new InvalidDataException("Composite2 completed null shader read differs from its actual original setter.");
                actualNull.ValidateCurrent();continue;
            }
            var actual=completed.TextureReads.Where(r=>r.SourceStageCode==0&&r.SourceRegister==row.RegisterIndex).ToArray();
            if(completed.WasPixelDrawSubmitted&&(actual is not [var read]||read.ParameterIndex!=row.ParameterIndex||
                !read.HasSourceReadFootprintProof||string.IsNullOrWhiteSpace(read.SourceEvidence)))
                throw new InvalidDataException("Composite2 completed shader read lost its actual initialized or source-independent footprint capability.");
        }
        if(!completed.WasPixelDrawSubmitted)
            return new(this,recipe,completed,target,false);
        if(completed.Writes is not [var write]||write.SourceTargetRegister!=0||write.StorageRid!=target.StorageRid||
            write.TargetViewRid!=target.TargetViewRid||write.NativeViewFormat!=target.NativeViewFormat||write.Extent!=target.Extent)
            throw new InvalidDataException("Composite2 source coverage received another actual target writer.");
        var raster=completed.ActualRaster;
        if(recipe.Before.RequiresPriorTargetContents&&!write.PriorDestinationPixelsWereValidated)
            throw new InvalidDataException("Composite2 AddBloom coverage requires actual readable prior Scene destination pixels.");
        bool complete=completed.ActualViewport==new Rect2I(Vector2I.Zero,target.Extent)&&
            (completed.ActualScissor is null||completed.ActualScissor.Value.Intersection(completed.ActualViewport)==completed.ActualViewport)&&
            raster.Cull==RenderingDevice.PolygonCullMode.Disabled&&!raster.DepthTest&&!raster.DepthWrite&&raster.NativeStencil is null&&
            raster.Attachments is [var color]&&color.WriteR&&color.WriteG&&color.WriteB&&color.WriteA&&
            (!color.EnableBlend||write.PriorDestinationPixelsWereValidated);
        return new(this,recipe,completed,target,complete);
    }
}
