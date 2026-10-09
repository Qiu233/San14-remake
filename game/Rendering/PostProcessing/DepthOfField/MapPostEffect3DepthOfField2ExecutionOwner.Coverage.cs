using Godot;
using San14.Data;

public sealed partial class MapPostEffect3DepthOfField2ExecutionOwner
{
    private MapPostEffect3DepthOfField2GraphicsCoverageReceipt CaptureCoverage(
        MapPostEffect3DepthOfField2GraphicsSource proof,IReadOnlyList<MapPostEffect3FullscreenTargetBinding> targets,
        MapPostEffect3FullscreenCompletedPass completed,bool allocatedOnlyUnusedHistory)
    {
        RequireCurrent();proof.RequireCurrent();
        if(!ReferenceEquals(Graphics.LastCompletedPass,completed)||completed.SourcePassIndex!=proof.SourcePassIndex||
            completed.FrameNumber!=proof.FrameNumber||completed.ContextOwnerIdentity!=proof.ContextOwnerIdentity||
            completed.VertexProgramSha256!=proof.VertexProgramSha256||completed.PixelProgramSha256!=proof.PixelProgramSha256||
            completed.ConstantBufferReads.Count!=proof.RequiredConstantBufferBytesSha256.Count||
            proof.RequiredConstantBufferBytesSha256.Any(cb=>!completed.ConstantBufferReads.Any(actual=>actual.ParameterIndex==cb.Key&&
                actual.HasCompleteWriterContents&&actual.CompleteBytesSha256==cb.Value)))
            throw new InvalidDataException("DOF2 coverage does not join its actual original selected VS/PS and complete CB18 writer.");
        foreach(var row in proof.EffectivePixelTextureReads)
        {
            var nulls=completed.SourceNullShaderResources.Where(n=>n.SourceStageCode==0&&n.SourceRegister==row.RegisterIndex).ToArray();
            if(nulls.Length!=0)
            {
                if(nulls is not [var actual]||actual.ParameterIndex!=row.ParameterIndex)
                    throw new InvalidDataException("DOF2 original null texture read differs from its actual effective source setter.");
                actual.ValidateCurrent();continue;
            }
            if(completed.WasPixelDrawSubmitted&&!completed.TextureReads.Any(r=>r.SourceStageCode==0&&r.SourceRegister==row.RegisterIndex&&
                r.ParameterIndex==row.ParameterIndex&&r.HasSourceReadFootprintProof))
                throw new InvalidDataException("DOF2 submitted read lacks its actual native mip/read-domain capability.");
        }
        var completeness=new Dictionary<int,bool>();var raster=completed.ActualRaster;
        bool rasterCovers=completed.WasPixelDrawSubmitted&&float.IsFinite(proof.VertexClipDepth)&&proof.VertexClipDepth>=0&&proof.VertexClipDepth<=1&&
            raster.Cull==RenderingDevice.PolygonCullMode.Disabled&&!raster.DepthTest&&!raster.DepthWrite&&raster.NativeStencil is null;
        foreach(var binding in targets)
        {
            int register=binding.SourceTargetRegister;var target=binding.Target;
            var write=completed.Writes.SingleOrDefault(w=>w.SourceTargetRegister==register);
            if(completed.WasPixelDrawSubmitted&&(write is null||write.StorageRid!=target.StorageRid||write.TargetViewRid!=target.TargetViewRid||
                write.ParameterIndex!=target.ParameterIndex||write.NativeViewFormat!=target.NativeViewFormat||write.Extent!=target.Extent))
                throw new InvalidDataException("DOF2 coverage received another actual MRT target writer.");
            byte meaningful=target.NativeViewFormat switch{26=>7,54 or 41 or 61=>1,10 or 2 or 28 or 29 or 87 or 91=>15,
                _=>throw new NotSupportedException("DOF2 coverage has no exact native-format channel proof.")};
            var color=raster.Attachments[register];byte enabled=(byte)((color.WriteR?1:0)|(color.WriteG?2:0)|(color.WriteB?4:0)|(color.WriteA?8:0));
            bool channels=proof.OutputChannelMasks.TryGetValue(register,out byte written)&&((written&enabled)&meaningful)==meaningful;
            bool complete=rasterCovers&&!allocatedOnlyUnusedHistory&&channels&&
                completed.ActualViewport==new Rect2I(Vector2I.Zero,target.Extent)&&
                (completed.ActualScissor is null||completed.ActualScissor.Value.Intersection(completed.ActualViewport)==completed.ActualViewport)&&
                (!color.EnableBlend||write is{PriorDestinationPixelsWereValidated:true});
            if(proof.RequiresPriorTargetPixels&&write is not{PriorDestinationPixelsWereValidated:true})
                throw new InvalidDataException("DOF2 source composite requires its actual readable prior destination pixels.");
            completeness.Add(register,complete);
        }
        return new(this,proof,completed,targets,new System.Collections.ObjectModel.ReadOnlyDictionary<int,bool>(completeness),allocatedOnlyUnusedHistory);
    }
}
