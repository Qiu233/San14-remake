using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Source-semantic acknowledgement of the owned fresh mode1 fallback.
/// Construct only after the Game owner has completed the actual source output
/// call. This is not a GPU fence, a native-process observation, or an A1/SPK
/// execution receipt. The five-input overload remains partial; Host history
/// additionally requires the exact fresh owned Q writer-scope receipt.</summary>
public sealed class MapPostEffect3ApplyMode1Completed
{
    private readonly byte[] cameraBytes, exposureBytes, drawBytes;
    private readonly byte[][] parameterBytes;
    private readonly MapPostEffect3ApplyFallbackTextureBinding[] before,after;
    private MapPostEffect3ApplyMode1Completed(MapCameraConstructionPacket camera,
        MapExposurePacket exposure, MapPostEffect3ApplyMode1Route route,
        MapPostEffect3ApplyFallbackViewportPlan viewport,
        MapPostEffect3ApplyFallbackBindingPlan bindings,MapPostEffect3ApplyMode1WriterScopeCurrent? writerScope=null)
    {
        Camera=camera;Exposure=exposure;Route=route;Viewport=viewport;Bindings=bindings;WriterScope=writerScope;
        cameraBytes=camera.Prefix.ToArray();exposureBytes=exposure.Bytes.ToArray();drawBytes=bindings.Native49Packet.ToArray();
        parameterBytes=bindings.GenericShaderParameters.Select(p=>p.Bytes.ToArray()).ToArray();
        before=bindings.BeforeDraw.ToArray();after=bindings.AfterDraw.ToArray();
    }
    public long FrameNumber=>Camera.FrameNumber;
    public MapCameraConstructionPacket Camera { get; }
    public MapExposurePacket Exposure { get; }
    public MapPostEffect3ApplyMode1Route Route { get; }
    public MapPostEffect3ApplyFallbackViewportPlan Viewport { get; }
    public MapPostEffect3ApplyFallbackBindingPlan Bindings { get; }
    public MapPostEffect3ApplyMode1WriterScopeCurrent? WriterScope { get; }
    // Exposure writes15. 's stock draw/restore retains it;
    // the admitted optional caller composite also explicitly writes15.
    public uint FallbackRt0ColorWriteMask=>15;
    // .. clears frontend+B0 through native opcode47.
    public bool FallbackCustomVertexShaderIsAbsent=>true;
    public bool IsCompleteForSourceHostHistory=>WriterScope is not null;
    public void RequireCompletedSourceHostWriterScope()
    {
        Validate();
        if(WriterScope is null)
            throw new NotSupportedException("Mode1 Host history requires its actual fresh owned Q.v170/shared-tail writer scope.");
        WriterScope.Validate(Camera,Route);
    }
    public string SourceEvidence=>Route.SourceEvidence+";"+Viewport.SourceEvidence+";"+Bindings.SourceEvidence+
        "Exposure-RT0=15;34D7-B0=null;-stock-scalars-and-clip-mask-preserving"+
        (WriterScope is null?"caller-writer-scope-unclosed":WriterScope.SourceEvidence)+";not-GPU-receipt";

    public static MapPostEffect3ApplyMode1Completed CaptureAfterSuccessfulSourceCall(
        MapCameraConstructionPacket camera, MapExposurePacket exposure,
        MapPostEffect3ApplyMode1Route route, MapPostEffect3ApplyFallbackViewportPlan viewport,
        MapPostEffect3ApplyFallbackBindingPlan bindings)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(exposure);
        ArgumentNullException.ThrowIfNull(route);ArgumentNullException.ThrowIfNull(viewport);ArgumentNullException.ThrowIfNull(bindings);
        var value=new MapPostEffect3ApplyMode1Completed(camera,exposure,route,viewport,bindings);
        value.Validate();return value;
    }
    /// <summary>Positive complete writer family for the limited fresh owned
    /// Main Q route. Submission success remains the Game caller's duty.</summary>
    public static MapPostEffect3ApplyMode1Completed CaptureAfterSuccessfulSourceCall(
        MapCameraConstructionPacket camera, MapExposurePacket exposure,
        MapPostEffect3ApplyMode1Route route, MapPostEffect3ApplyFallbackViewportPlan viewport,
        MapPostEffect3ApplyFallbackBindingPlan bindings,MapPostEffect3ApplyMode1WriterScopeCurrent writerScope)
    {
        ArgumentNullException.ThrowIfNull(camera);ArgumentNullException.ThrowIfNull(exposure);
        ArgumentNullException.ThrowIfNull(route);ArgumentNullException.ThrowIfNull(viewport);ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(writerScope);
        var value=new MapPostEffect3ApplyMode1Completed(camera,exposure,route,viewport,bindings,writerScope);
        value.RequireCompletedSourceHostWriterScope();return value;
    }

    /// <summary>Revalidate retained inputs before any ledger commits. Neither
    /// successful preparation nor this pure validation acknowledges submission.</summary>
    public void Validate()
    {
        Route.ValidateSameCamera(Camera);
        WriterScope?.Validate(Camera,Route);
        _=Camera.CopyAcceptedSnapshot(); // reparse and validate the retained source document
        if(FrameNumber<=0||Exposure.FrameNumber!=FrameNumber||Route.FrameNumber!=FrameNumber||
            Viewport.FrameNumber!=FrameNumber||Bindings.FrameNumber!=FrameNumber||
            !Camera.Prefix.Span.SequenceEqual(cameraBytes)||!Exposure.Bytes.Span.SequenceEqual(exposureBytes)||
            !Bindings.Native49Packet.Span.SequenceEqual(drawBytes)||
            !Bindings.BeforeDraw.SequenceEqual(before)||!Bindings.AfterDraw.SequenceEqual(after)||
            Bindings.GenericShaderParameters.Count!=parameterBytes.Length||
            Bindings.GenericShaderParameters.Where((p,i)=>!p.Bytes.Span.SequenceEqual(parameterBytes[i])).Any()||
            Exposure.Camera!=DeferredSceneConstants.ReadPreparedCamera(Camera.Prefix.Span.Slice(0x10,0x130))||
            Exposure.Bytes.Length!=32||BinaryPrimitives.ReadUInt32LittleEndian(Exposure.Bytes.Span)!=BinaryPrimitives.ReadUInt32LittleEndian(Camera.Prefix.Span.Slice(0x170,4))||
            Exposure.ToneType3A4!=2||Exposure.HalfRectangle||(Exposure.ViewFlags8&((1u<<22)|(1u<<23)))!=0||
            Camera.Source.CpuWord(0xBF0)!=0||Camera.ExecutableLocale is not("SC" or "TC")||
            Viewport.Inputs.ActualCamera1CC!=BinaryPrimitives.ReadUInt32LittleEndian(Camera.Prefix.Span.Slice(0x1CC,4)))
            throw new InvalidDataException("Mode1 completion requires the same retained Camera/Exposure and admitted full output frame.");
        if(!Route.ExecutesToneFallback||Route.ExecutesModuleFunctor||!Route.BypassesLegacyA1Branch||
            Viewport.Inputs.ActualCameraViewport1540!=new Vector4(0,0,1,1)||Viewport.SpecialRectangleApplied||
            Viewport.Inputs.OwnerIdentity!=Bindings.ActualTarget.OwnerIdentity||Bindings.ActualTarget.FrameNumber!=FrameNumber||
            Viewport.PixelRectangle!=new MapPostEffect3ApplyFallbackPixelRectangle(0,0,
                Viewport.Inputs.ActualTargetWidth50,Viewport.Inputs.ActualTargetHeight54)||
            Bindings.UsesDirectTextureDrawPath!=Viewport.UsesDirectTextureDrawPath||
            !Bindings.Native49Packet.Span.SequenceEqual(Viewport.Native49Packet.Span))
            throw new InvalidDataException("Mode1 completion requires the actual target, full viewport and same native49 draw plan.");
        int beforeCount=Bindings.UsesDirectTextureDrawPath?1:2;
        if(Bindings.BeforeDraw.Count!=beforeCount||Bindings.AfterDraw.Count!=2||
            Bindings.BeforeDraw[0].NativeSlot!=0||
            Bindings.AfterDraw[0].NativeSlot!=0||
            Bindings.AfterDraw[1].NativeSlot!=1||
            Bindings.AfterDraw.Any(b=>b.Owner is not null)||
            Bindings.BeforeDraw.Any(b=>b.Owner is null||b.Owner.FrameNumber!=FrameNumber||string.IsNullOrWhiteSpace(b.Owner.OwnerIdentity)))
            throw new InvalidDataException("Mode1 completion lost the source bind/draw/unbind order or owned texture frame.");
        if(Bindings.UsesDirectTextureDrawPath)
        {
            if(Bindings.ToneShaderAuxF8 is not null||Bindings.GenericShaderParameters.Count!=0)
                throw new InvalidDataException("Direct mode1 completion cannot include foreign generic tone writers.");
        }
        else
        {
            if(Bindings.BeforeDraw[1].NativeSlot!=1||
                Bindings.ToneShaderAuxF8 is not { } shader||shader.FrameNumber!=FrameNumber||string.IsNullOrWhiteSpace(shader.OwnerIdentity))
                throw new InvalidDataException("Tone mode1 completion requires its actual reference50/AuxF8 owners.");
            var expected=new[]{("vToneMapParams",16),("fGamma",4),("fToneCurveType",4)};
            if(Bindings.GenericShaderParameters.Count!=expected.Length)
                throw new InvalidDataException("Tone mode1 completion has incomplete typed source parameter writes.");
            for(int i=0;i<expected.Length;i++)
            {
                var write=Bindings.GenericShaderParameters[i];var e=expected[i];
                if(write.Name!=e.Item1||write.Bytes.Length!=e.Item2||
                    string.IsNullOrWhiteSpace(write.SourceEvidence))
                    throw new InvalidDataException("Tone mode1 completion has a foreign or reordered scalar writer.");
                for(int p=0;p<write.Bytes.Length;p+=4)
                    if(!float.IsFinite(BinaryPrimitives.ReadSingleLittleEndian(write.Bytes.Span.Slice(p,4))))
                        throw new InvalidDataException("Tone completion contains nonfinite uploaded scalar data.");
            }
            if(BinaryPrimitives.ReadSingleLittleEndian(Bindings.GenericShaderParameters[2].Bytes.Span)!=Viewport.FifthScalarXmm8)
                throw new InvalidDataException("Tone completion has another caller fifth argument.");
        }
        if(string.IsNullOrWhiteSpace(Exposure.SourceProvenance)||string.IsNullOrWhiteSpace(Bindings.SourceEvidence))
            throw new InvalidDataException("Mode1 completion lacks retained source identities.");
    }
}
