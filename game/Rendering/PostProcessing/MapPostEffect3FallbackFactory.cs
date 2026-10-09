using System.Buffers.Binary;
using Godot;
using San14.Data;

/// <summary>Explicit current tone fallback inputs. Capture these while current stock
/// source receipts are still live, before completed Host history consumes them.
/// Samplers/raster and context Camera selection are independent source owners.</summary>
public sealed record MapPostEffect3FallbackFrameInputs(
    MapPostEffect3ApplyMode1Route Route,
    MapPostEffect3ApplyFallbackViewportPlan Viewport,
    MapPostEffect3ApplyFallbackBindingPlan Bindings,
    MapPostEffect3ApplyFallbackGammaPlan Gamma,
    MapCameraConstructionPacket ActualSelectedContextCamera,
    MapCopy97CurrentStockPacket CurrentStock,
    MapPostEffect3ApplyExposureReference ExposureReference,
    IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> SamplerDescriptors,
    ModelRasterInputs Raster);

public sealed record MapPostEffect3FallbackAcceptedFrame(NativeMapFrameContext Context,
    MapPostEffect3FallbackFrameInputs Inputs,MapExposurePacket ActualExposure,
    ReadOnlyMemory<byte> PixelConstants,ReadOnlyMemory<byte> VertexConstants,ReadOnlyMemory<byte> Quad,
    IReadOnlyList<int> UnreadPixelPaddingBytes,string SourceEvidence);

/// <summary>Actual mode1 Tone fallback, independent of A1 execution.
/// Borrows the accepted original node1 Exposure owner and node73 working image.
/// It neither commits Host history nor claims to execute the3SPK functor.
/// Unknown current resources/state reject instead of being substituted.</summary>
public sealed class MapPostEffect3FallbackFactory:IAsyncDisposable
{
    public MapFinalOutputOwner Output{get;}
    public LoadedMapFinalOutputToneShaderSource ToneSource{get;}
    public MapExposureOwner Reference50Owner{get;}
    public MapPostEffect3FallbackAcceptedFrame? LastAcceptedFrame{get;private set;}
    public RenderOutputPresentation? Presentation=>Output.Presentation;
    public string OutputTargetIdentity=>Output.Source.OutputTexture.Source.Source.ResourceUri+
        "#object/"+Output.Source.OutputTexture.Object.ObjectId.ToString("X8");
    private readonly SemaphoreSlim serial=new(1,1);private readonly object lifetime=new();
    private bool retiring;private int accepted;private TaskCompletionSource? drained;private Task? disposal;
    private MapPostEffect3FallbackFactory(MapFinalOutputOwner output,
        LoadedMapFinalOutputToneShaderSource tone,MapExposureOwner reference)
        =>(Output,ToneSource,Reference50Owner)=(output,tone,reference);

    public static async Task<MapPostEffect3FallbackFactory> BuildAsync(
        LoadedMapFinalOutputResources source,LoadedMapFinalOutputToneShaderSource actualTone,
        MapFinalOutputDeadToneLut actualCurrentDeadLut,MapExposureOwner borrowedReference50,
        Vkd3dShaderCompiler compiler)
    {
        ArgumentNullException.ThrowIfNull(source);ArgumentNullException.ThrowIfNull(actualTone);
        ArgumentNullException.ThrowIfNull(actualCurrentDeadLut);ArgumentNullException.ThrowIfNull(borrowedReference50);
        if(borrowedReference50.Source.Texture.Object.ObjectId!=MapExposureSource.TextureObjectId||
            borrowedReference50.Source.Texture.Source.Source.ResourceUri!=source.ExposureTexture.Source.Source.ResourceUri)
            throw new InvalidDataException("Tone reference50 must borrow this source's actual node1 Exposure declaration.");
        var output=await MapFinalOutputOwner.BuildAsync(source,actualTone.ToProgramSource(),0,1,compiler,
            MapFinalOutputTargetRoute.SourceHostParentOutput,sourceToneShader:actualTone,sourceDeadToneLut:actualCurrentDeadLut);
        return new(output,actualTone,borrowedReference50);
    }

    public Task<RenderOutputPresentation> RenderFromPreparedAsync(NativeMapFrameContext context,
        MapFinalOutputProductionPrepared sourceExposurePreparation,
        MapPostEffect3FallbackFrameInputs input)
    {
        ArgumentNullException.ThrowIfNull(context);ArgumentNullException.ThrowIfNull(sourceExposurePreparation);ArgumentNullException.ThrowIfNull(input);
        // Freeze caller-owned byte arrays before queued work, preserving real
        // source owners and current stock capability rather than cloning them.
        input=input with{Bindings=input.Bindings with{
            Native49Packet=input.Bindings.Native49Packet.ToArray(),
            BeforeDraw=Array.AsReadOnly(input.Bindings.BeforeDraw.ToArray()),
            AfterDraw=Array.AsReadOnly(input.Bindings.AfterDraw.ToArray()),
            GenericShaderParameters=Array.AsReadOnly(input.Bindings.GenericShaderParameters.Select(v=>v.CopyAcceptedSnapshot()).ToArray())},
            SamplerDescriptors=input.SamplerDescriptors.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray()),
            Raster=MapFinalOutputRasterSnapshot.Capture(input.Raster),
            ActualSelectedContextCamera=input.ActualSelectedContextCamera.CopyAcceptedSnapshot()};
        sourceExposurePreparation=sourceExposurePreparation with{Camera=sourceExposurePreparation.Camera.CopyAcceptedSnapshot(),
            Exposure=sourceExposurePreparation.Exposure.CopyAcceptedSnapshot()};
        lock(lifetime){ObjectDisposedException.ThrowIf(retiring,this);accepted++;}
        return RenderAcceptedAsync(context,sourceExposurePreparation,input);
    }
    private async Task<RenderOutputPresentation> RenderAcceptedAsync(NativeMapFrameContext context,
        MapFinalOutputProductionPrepared prepared,MapPostEffect3FallbackFrameInputs input)
    {
        await serial.WaitAsync();
        try
        {
            long frame=context.FrameNumber;var viewport=input.Viewport;var binding=input.Bindings;
            if(frame<=0||prepared.FrameNumber!=frame||input.Route.FrameNumber!=frame||viewport.FrameNumber!=frame||
                binding.FrameNumber!=frame||input.Gamma.FrameNumber!=frame||input.CurrentStock.FrameNumber!=frame||
                !input.Route.ExecutesToneFallback||input.Route.ExecutesModuleFunctor||!input.Route.BypassesLegacyA1Branch||
                viewport.UsesDirectTextureDrawPath||binding.NativeShaderType!=5||binding.UsesDirectTextureDrawPath||
                input.Gamma.OwnerIdentity!=input.Route.OwnerIdentity||viewport.Inputs.OwnerIdentity!=input.Route.OwnerIdentity||
                !binding.Native49Packet.Span.SequenceEqual(viewport.Native49Packet.Span))
                throw new InvalidDataException("Tone fallback requires its exact same-frame generic native mode1 route/viewport/bindings.");
            input.CurrentStock.ValidateCurrent();
            input.Route.ValidateSameCamera(input.ActualSelectedContextCamera);
            if(input.ActualSelectedContextCamera.FrameNumber!=frame||
                PreparedCameraSceneSource.ReadCompletePrefix(input.ActualSelectedContextCamera.Prefix.Span)!=context.PreparedCamera||
                viewport.Inputs.ActualCamera1CC!=MapPostEffect3ApplyFallbackToneCurve.ReadSelectedCamera1CC(input.ActualSelectedContextCamera))
                throw new InvalidDataException("Tone fallback context Camera is foreign to its exact actual Scene attempt.");
            // The tone fallback explicitly clears B0 before this source draw.
            // The selected stock program implements that operation regardless
            // of the incoming stock custom-VS state.
            var reference=input.ExposureReference;
            if(!context.OutputFrame.Textures.TryGetValue(0x064B746B,out var scene)||
                viewport.Inputs.ActualTargetWidth50!=scene.ExplicitExtent.X||viewport.Inputs.ActualTargetHeight54!=scene.ExplicitExtent.Y||
                viewport.Inputs.ActualTargetColorSpace60!=Output.Source.OutputTexture.ColorSpace||
                Reference50Owner.AcceptedFrameNumber!=frame||Reference50Owner.LastPacket is not{ } exposure||
                !exposure.Bytes.Span.SequenceEqual(prepared.Exposure.Bytes.Span)||exposure.SourceProvenance!=prepared.Exposure.SourceProvenance||
                exposure.Camera!=prepared.Exposure.Camera||exposure.Camera!=context.PreparedCamera)
                throw new InvalidDataException("Tone fallback must consume this current Scene and already generated same-frame original Exposure packet.");
            if(binding.BeforeDraw is not [{NativeSlot:0,Owner:not null},{NativeSlot:1,Owner:not null}]||
                binding.BeforeDraw[0].Owner!.OwnerIdentity!=scene.EffectiveSource.OwnerIdentity||
                binding.BeforeDraw[1].Owner!=reference.ExposureTexture50||reference.FrameNumber!=frame||
                binding.ActualTarget.OwnerIdentity!=OutputTargetIdentity||
                binding.AfterDraw is not [{NativeSlot:0,Owner:null},{NativeSlot:1,Owner:null}]||
                string.IsNullOrWhiteSpace(reference.SourceEvidence))
                throw new InvalidDataException("Tone native slots must retain actual working, reference50, target and cleanup owners.");

            if(viewport.RequestsSourceViewBit15Clear)
                throw new NotSupportedException("Tone source requests a changed sampled view; its current GPU view producer is not implemented.");

            byte[] pixel=BuildPixelConstants(binding,reference.ActualReferenceLuminance18,input.Gamma.ShaderReciprocalGamma);
            Output.Program.DeadToneLut!.ValidateFrame(pixel);
            byte[] vertex=DeferredAuxiliaryDepthCopySource.BuildStockVertexConstants(
                viewport.Inputs.ActualTargetWidth50,viewport.Inputs.ActualTargetHeight54,
                input.CurrentStock.CurrentScreenScale29E0.X,input.CurrentStock.CurrentScreenScale29E0.Y,
                input.CurrentStock.CurrentColorScale29F0);
            byte[] quad=BuildQuad(viewport);
            var raster=MapFinalOutputRasterSnapshot.Capture(input.Raster);
            var samplers=input.SamplerDescriptors.ToDictionary(p=>p.Key,p=>(ReadOnlyMemory<byte>)p.Value.ToArray());
            string evidence=binding.SourceEvidence+";"+input.Gamma.SourceEvidence+";"+input.CurrentStock.SourceProvenance+
                ";"+reference.SourceEvidence+";"+ToneSource.SourceEvidence+
                ";tone-fallback-explicit-stock-custom-VS-clear;"+
                ";actual-same-frame-Exposure-owner;no-A1-effect-draw;no-Host-history-commit";
            var packet=new MapFinalOutputFramePacket(frame,new Dictionary<uint,ReadOnlyMemory<byte>>{{0,vertex}},
                new Dictionary<uint,ReadOnlyMemory<byte>>{{0,pixel}},quad,
                new("Texture",0,scene.ShaderViewRid,scene.StorageRid,scene.Source.Source.Source.ResourceUri+"#working/"+scene.EffectiveSource.OwnerIdentity),
                new("Texture",1,Reference50Owner.TextureRid,Reference50Owner.TextureRid,Reference50Owner.Source.SourceResourceUri),
                [],samplers,raster,evidence);
            var result=await Output.RenderAsync(context,packet);
            LastAcceptedFrame=new(context,input,prepared.Exposure.CopyAcceptedSnapshot(),pixel,vertex,quad,
                Array.AsReadOnly(Enumerable.Range(24,8).ToArray()),evidence);
            return result;
        }
        finally{serial.Release();lock(lifetime)if(--accepted==0&&retiring)drained?.TrySetResult();}
    }

    private byte[] BuildPixelConstants(MapPostEffect3ApplyFallbackBindingPlan bindings,float luminance,float gamma)
    {
        var source=ToneSource.Pixel.Definitions;var native=source.Buffers.Single();byte[] bytes=new byte[native.ByteSize];
        var known=new bool[bytes.Length];
        if(bindings.GenericShaderParameters.Count!=3)throw new InvalidDataException("Tone fallback must retain all three original generic setters.");
        foreach(var value in bindings.GenericShaderParameters)
        {
            var field=native.Variables.SingleOrDefault(v=>v.Name.Text==value.Name)
                ??throw new InvalidDataException("Tone generic setter does not name an actual source global.");
            if(value.Bytes.Length!=field.ByteSize||known.AsSpan(field.StartOffset,field.ByteSize).Contains(true))
                throw new InvalidDataException("Tone source generic field is incomplete or repeated.");
            value.Bytes.Span.CopyTo(bytes.AsSpan(field.StartOffset));Array.Fill(known,true,field.StartOffset,field.ByteSize);
        }
        foreach(var field in native.Variables.Where(v=>(v.Flags&2)!=0))
            if(known.AsSpan(field.StartOffset,field.ByteSize).Contains(false))
                throw new InvalidDataException("Tone program reads an unresolved source generic value.");
        var actualTone=bindings.GenericShaderParameters.Single(p=>p.Name=="vToneMapParams").Bytes.Span;
        float sourceScale=1f/(luminance*luminance);
        if(!float.IsFinite(luminance)||luminance<=0||BitConverter.SingleToUInt32Bits(sourceScale)!=BinaryPrimitives.ReadUInt32LittleEndian(actualTone)||
            BitConverter.SingleToUInt32Bits(gamma)!=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(16)))
            throw new InvalidDataException("Tone constants differ from the actual selected luminance18 or source gamma result.");
        //24..31 are unread backend padding, not asserted native zero values.
        return bytes;
    }
    private static byte[] BuildQuad(MapPostEffect3ApplyFallbackViewportPlan viewport)
    {
        var r=viewport.PixelRectangle;var u=viewport.UvRectangle;
        float[][] rows=[[r.Left,r.Top,u.X,u.Y],[r.Right,r.Top,u.Z,u.Y],
            [r.Left,r.Bottom,u.X,u.W],[r.Right,r.Bottom,u.Z,u.W]];byte[] bytes=new byte[96];
        for(int i=0;i<rows.Length;i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24),rows[i][0]);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+4),rows[i][1]);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+8),viewport.FifthDrawArgument);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*24+12),viewport.PackedColorR9);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+16),rows[i][2]);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+20),rows[i][3]);
        }
        return bytes;
    }
    public ValueTask DisposeAsync()
    {
        lock(lifetime)
        {
            if(disposal is not null)return new(disposal);retiring=true;
            Task pending=accepted==0?Task.CompletedTask:(drained=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
            return new(disposal=RetireAsync(pending));
        }
    }
    private async Task RetireAsync(Task pending)
    {await pending;await serial.WaitAsync();try{await Output.DisposeAsync();LastAcceptedFrame=null;}finally{serial.Release();}}
}
