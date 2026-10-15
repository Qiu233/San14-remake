using San14.Data;
using System.Buffers.Binary;

public sealed partial class MapFinalOutputSceneChain
{
    public bool HasCompleteSourceEffects=>CompleteEffects is not null;
    /// <summary>Source Hostfalse, source DOF0 and the saved template's first
    /// successful initialized noise route. Every final effect image is produced
    /// by an actual native draw; source LUT0/intensity0 is proof specialized.</summary>
    public static async Task<MapFinalOutputSceneChain> BuildCompleteSourceHostAsync(
        LoadedMapFinalOutputResources source,Vkd3dShaderCompiler compiler,
        LoadedMapCameraConstructionSource sourceCreatedCamera,
        MapExposureOwner? sharedExposureProducer=null,bool allowReadback=false)
    {
        var profile=MapFinalOutputDeadColorCorrection.FromSourceCreatedCamera(sourceCreatedCamera);
        var chain=await BuildSourceHostAsync(source,compiler,false,sharedExposureProducer,allowReadback,profile);
        try{chain.CompleteEffects=await MapFinalOutputFullEffectsOwner.BuildAsync(chain.Graph,compiler,allowReadback);
            chain.RequiredExtraFinalTextureIndices=Array.Empty<int>();return chain;}
        catch(Exception failure)
        {
            var pending=NativeMapBorrowerRetirementPackage.CreatePending("Final-complete-chain-partial",[chain],
                (failure as NativeMapBorrowerConstructionException)?.RetainedPackage);
            try{await NativeMapBorrowerRetirementPackage.DrainPendingAsync(pending);}
            catch(Exception retirementFailure)
            {throw new NativeMapBorrowerConstructionException(failure,retirementFailure,pending!);}
            throw;
        }
    }
    public Task<RenderOutputPresentation> RenderCompleteSourceAsync(
        NativeMapFrameContext frame,MapFinalOutputCompleteFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(input);
        if(CompleteEffects is null||Final.Program.DeadColorCorrection is null)throw new InvalidOperationException("This chain was not built for its complete source activation route.");
        if(input.HdrRoute is { } hdr)
        {
            if(!frame.OutputFrame.Textures.TryGetValue(0x064B746B,out var working))
                throw new InvalidDataException("Resolved HDR route requires the actual accepted Scene target.");
            hdr.ValidateSameWorkingTarget(working.EffectiveSource,working.ExplicitExtent.X,working.ExplicitExtent.Y,
                frame.FrameNumber,hdr.CallerOwnerIdentity);
        }
        var snapshot=SnapshotComplete(input);Accept();return RenderCompleteAcceptedAsync(frame,snapshot);
    }
    private async Task<RenderOutputPresentation> RenderCompleteAcceptedAsync(
        NativeMapFrameContext frame,MapFinalOutputCompleteFrameInputs input)
    {
        await _gate.WaitAsync();
        try
        {
            var camera=DeferredSceneConstants.ReadPreparedCamera(input.Camera.Prefix.Span.Slice(0x10,0x130));
            if(frame.FrameNumber<=0||frame.FrameNumber!=input.Camera.FrameNumber||frame.FrameNumber!=input.Exposure.FrameNumber||
                frame.FrameNumber!=input.DofWeight.FrameNumber||frame.PreparedCamera!=camera||input.Exposure.Camera!=camera||input.DofWeight.Camera!=camera||
                frame.OutputFrame.Source.Graph.Source.ResourceUri!=Source.Graph.Source.ResourceUri||
                frame.OutputFrame.Source.Graph.Source.DecodedSha256!=Source.Graph.Source.DecodedSha256||
                !frame.OutputFrame.Textures.TryGetValue(0x064B746B,out var scene)||!frame.OutputFrame.Textures.TryGetValue(0xE13DDB4F,out var depth))
                throw new InvalidDataException("Complete source chain requires the actual accepted node73 Scene/depth and the same Camera+10/frame for every producer.");
            var exposure=await Exposure.PrepareAndBorrowAsync(frame,input.Exposure);
            await using var weight=await DofWeight.PrepareAndBorrowAsync(frame,input.DofWeight);
            var external=new Dictionary<int,ModelDeviceBinding>
            {
                [0]=new("Texture",0,scene.ShaderViewRid,scene.StorageRid,Source.SceneTexture.Source.Source.ResourceUri+"#064B746B/accepted-node73"),
                [14]=exposure,[29]=new("Texture",0,depth.ShaderViewRid,depth.StorageRid,Source.TextureObjects.Source.ResourceUri+"#E13DDB4F/accepted-node73"),[30]=weight.Binding
            };
            if(Final.TargetTexture is not null&&Final.TargetTexture.ExplicitExtent!=scene.ExplicitExtent)
                await Final.RetireTargetAsync();
            var textures=await CompleteEffects!.RenderAsync(input.Camera,input.DofWeight,scene.ExplicitExtent.X,scene.ExplicitExtent.Y,
                input.Effects,input.FinalDraw.PixelConstants[0],external,weight.CommitDrawSuccess);
            var live=Final.Program.PixelBindings.Bindings.Where(b=>b.DescriptorKind=="Texture").Select(b=>b.Source.BindPoint).ToHashSet();
            var required=Graph.Passes[28].Inputs.Where(i=>live.Contains(i.TextureRegister)).ToArray();
            if(required.Any(i=>!textures.ContainsKey(i.TextureIndex)))throw new InvalidDataException("Actual source effect batch did not publish every live final input.");
            var images=required.Select(i=>textures[i.TextureIndex] with{SourceBindPoint=i.TextureRegister}).ToArray();
            var samplers=MapFinalOutputSamplers.PrepareFinal(Source,required.ToDictionary(i=>i.SamplerRegister,i=>CompleteEffects.ProducerKey(i.TextureIndex)),
                Final.Program.DeadColorCorrection).ToDictionary(s=>s.Register,s=>(ReadOnlyMemory<byte>)s.Recipe.EncodeD3D11Descriptor());
            var draw=input.FinalDraw;var packet=new MapFinalOutputFramePacket(frame.FrameNumber,draw.VertexConstants,draw.PixelConstants,
                MapFinalOutputShaft.CopyNativeQuad96(),images.Single(i=>i.SourceBindPoint==3),images.Single(i=>i.SourceBindPoint==4),
                images.Where(i=>i.SourceBindPoint is not(3 or 4)).ToArray(),samplers,draw.Raster,draw.NativeContextSource);
            var output=await Final.RenderAsync(frame,packet);CompletedFrameCount++;return output;
        }
        finally{_gate.Release();Complete();}
    }
    private MapFinalOutputCompleteFrameInputs SnapshotComplete(MapFinalOutputCompleteFrameInputs input)
    {
        if(string.IsNullOrWhiteSpace(input.NativeContextSource)||string.IsNullOrWhiteSpace(input.Effects.NativeContextSource)||
            string.IsNullOrWhiteSpace(input.FinalDraw.NativeContextSource)||input.FinalDraw.OutputCopy is not null||
            input.FinalDraw.SamplerDescriptors.Count!=0||input.FinalDraw.Quad.Length!=0&&
                !input.FinalDraw.Quad.Span.SequenceEqual(MapFinalOutputShaft.CopyNativeQuad96().Span))
            throw new InvalidDataException("Complete source Host route requires explicit provenance, internally owned original samplers/quad and its original current Output target.");
        static IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> Bytes(IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> values)
            =>values.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
        var context=input.Effects with
        {
            CurrentParameters=input.Effects.CurrentParameters.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray()),
            RetainedTemplatesByPass=input.Effects.RetainedTemplatesByPass.ToDictionary(p=>p.Key,p=>(IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>>)p.Value.ToDictionary(c=>c.Key,c=>(ReadOnlyMemory<byte>)c.Value.ToArray())),
            Wrapper=input.Effects.Wrapper with{RetainedUvVectors=input.Effects.Wrapper.RetainedUvVectors.ToArray()},
            RadialWrapper=input.Effects.RadialWrapper with{RetainedUvVectors=input.Effects.RadialWrapper.RetainedUvVectors.ToArray()},
            OrdinaryRaster=MapFinalOutputRasterSnapshot.Capture(input.Effects.OrdinaryRaster),
            RetainedStockPixelConstants=input.Effects.RetainedStockPixelConstants.ToArray()
        };
        var final=input.FinalDraw with{VertexConstants=Bytes(input.FinalDraw.VertexConstants),PixelConstants=Bytes(input.FinalDraw.PixelConstants),
            Quad=ReadOnlyMemory<byte>.Empty,Raster=MapFinalOutputRasterSnapshot.Capture(input.FinalDraw.Raster)};
        if(!final.PixelConstants.TryGetValue(0,out var cb))throw new InvalidDataException("Complete source final draw misses original PS28 CB0.");
        if(final.VertexConstants.Count!=1||!final.VertexConstants.TryGetValue(0,out var vertex)||vertex.Length!=288||
            final.PixelConstants.Count!=1||cb.Length!=240||final.Raster.DepthTest||final.Raster.DepthWrite||final.Raster.Attachments.Count!=1||
            input.Camera.FrameNumber!=input.Exposure.FrameNumber||input.Camera.FrameNumber!=input.DofWeight.FrameNumber||
            input.Exposure.Bytes.Length!=32||input.DofWeight.Bytes.Length!=512||input.Exposure.HalfRectangle||input.Exposure.ToneType3A4!=2||
            (input.Exposure.ViewFlags8&((1u<<22)|(1u<<23)))!=0||
            context.CurrentParameters.Count!=Graph.Parameters.Count||Graph.Parameters.Any(p=>!context.CurrentParameters.TryGetValue(p.Index,out var b)||b.Length!=p.ByteSize)||
            context.Wrapper.RetainedUvVectors.Length!=0||context.RadialWrapper.RetainedUvVectors.Length!=48||
            context.RetainedStockPixelConstants.Length!=16||context.OrdinaryRaster.DepthTest||context.OrdinaryRaster.DepthWrite||context.OrdinaryRaster.Attachments.Count!=1)
            throw new InvalidDataException("Complete source chain requires complete coherent retained context before accepting work.");
        foreach(int index in new[]{0,1,2,3,10,11,12,13,14,15,16,17,19,20,21,22,23,24,27})
        {
            var buffers=Graph.Passes[index].ConstantBuffers;
            if(!context.RetainedTemplatesByPass.TryGetValue(index,out var retained)||retained.Count!=buffers.Count||
                buffers.Any(b=>!retained.TryGetValue((b.StageCode,b.Register),out var value)||value.Length!=b.ByteSize))
                throw new InvalidDataException($"Complete source chain misses complete retained native CBs for pass{index}.");
        }
        var fields=Graph.Passes[28].ConstantBuffers.Single(b=>b.StageCode==0&&b.Register==0).SourceParameterSlices;
        uint Bits(int index)=>BinaryPrimitives.ReadUInt32LittleEndian(cb.Span.Slice(fields.Single(f=>f.ParameterIndex==index).Offset,4));
        uint expectedHdr=input.HdrRoute?.ResolvedTrueHdr==true?0x3F800000u:0u;
        if(Bits(1)!=0x3E4CCCCD||Bits(2)!=expectedHdr||Bits(52)!=BitConverter.SingleToUInt32Bits(input.Camera.Optics.Fxaa.EdgeThresholdMin))
            throw new InvalidDataException("Complete source route requires Tone2, its actual resolved HDR receipt and FXAA1 camera minimum.");
        Final.Program.DeadColorCorrection!.ValidateFrame(cb.Span);
        return input with{Camera=input.Camera.CopyAcceptedSnapshot(),Exposure=input.Exposure.CopyAcceptedSnapshot(),
            DofWeight=input.DofWeight.CopyAcceptedSnapshot(),Effects=context,FinalDraw=final};
    }
}
