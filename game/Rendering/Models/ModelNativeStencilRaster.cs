using San14.Data;
using Godot;

/// <summary>A final source stencil receipt bound to one actual geometry
/// packet. Callers cannot replace the reference, operation or selected shader.</summary>
public sealed class ModelNativeStencilRaster
{
    private ModelNativeStencilRaster(ModelOrdinaryNativeStencilReceipt source,
        string primitiveKey) => (Source, PrimitiveKey) = (source, primitiveKey);
    public ModelOrdinaryNativeStencilReceipt Source { get; }
    public string PrimitiveKey { get; }
    public static ModelNativeStencilRaster Bind(NativeMapFrameContext context,
        ModelGbufferInstancePacket primitive, ModelOrdinaryNativeStencilReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(primitive);
        ArgumentNullException.ThrowIfNull(receipt);
        var publication = receipt.CurrentPublication; var state = receipt.DrawTimeState;
        if (publication.FrameNumber != context.FrameNumber || publication.PrimitiveIdentity != primitive.InstanceKey ||
            receipt.Source.ShaderUri != primitive.Shader.Source.ResourceUri ||
            receipt.Source.ShaderSha256 != primitive.Shader.Source.DecodedSha256 ||
            receipt.Source.GraphUri != context.TerrainFrame.Source.Graph.Source.ResourceUri ||
            receipt.Source.GraphSha256 != context.TerrainFrame.Source.Graph.Source.DecodedSha256 ||
            !receipt.ShouldSubmitPrimitive || !state.StencilEnabled || state.NativeCompareCode != 7 ||
            state.NativeFailOperation != 0 || state.NativeDepthFailOperation != 0 || state.NativePassOperation != 2 ||
            state.ReadMask != 255 || state.WriteMask != 255)
            throw new InvalidDataException("Native stencil does not identify this actual submitted primitive, source shader/graph and accepted frame.");
        return new(receipt, primitive.InstanceKey);
    }
    internal bool SamePipeline(ModelNativeStencilRaster? other) => other is not null &&
        Source.DrawTimeState.StencilEnabled == other.Source.DrawTimeState.StencilEnabled &&
        Source.DrawTimeState.Reference == other.Source.DrawTimeState.Reference &&
        Source.DrawTimeState.ReadMask == other.Source.DrawTimeState.ReadMask &&
        Source.DrawTimeState.WriteMask == other.Source.DrawTimeState.WriteMask;
}

/// <summary>Created only after the model layer submits its complete accepted
/// batch. It proves render-thread submission, not a GPU fence/readback.</summary>
public sealed class ModelGeometrySubmittedFrame
{
    private readonly ModelGbufferLayer layer;
    internal ModelGeometrySubmittedFrame(ModelGbufferLayer layer,RenderTargetFrame target, long ordinal,
        ModelGbufferInstancePacket[] packets)
    {
        this.layer=layer;Target = target; LayerSubmissionOrdinal = ordinal;
        Primitives = Array.AsReadOnly(packets.Select(p => new ModelGeometrySubmittedPrimitive(
            p.InstanceKey, p.DrawIndex, p.Model.Source.ResourceUri, p.Model.Source.DecodedSha256,
            p.Shader.Source.ResourceUri, p.Shader.Source.DecodedSha256, p.Raster.NativeStencil)
            { Raster = ModelGeometrySubmittedRaster.CaptureActualSubmitted(p.Raster) }).ToArray());
    }
    public RenderTargetFrame Target { get; }
    public long LayerSubmissionOrdinal { get; }
    public IReadOnlyList<ModelGeometrySubmittedPrimitive> Primitives { get; }
    public void RequireCurrent()
    {
        if(!ReferenceEquals(layer.LastSubmittedFrame,this))
            throw new InvalidDataException("Geometry submission has been superseded, failed or retired.");
    }
}
public sealed record ModelGeometrySubmittedPrimitive(string PacketKey, int SourceDrawIndex,
    string ModelUri, string ModelSha256, string ShaderUri, string ShaderSha256,
    ModelNativeStencilRaster? NativeStencil)
{
    /// <summary>Copied by the actual layer submission constructor. No mutable
    /// color attachment arrays or current caller state are retained.</summary>
    public ModelGeometrySubmittedRaster? Raster { get; internal init; }
}

public sealed class ModelGeometrySubmittedRaster
{
    private ModelGeometrySubmittedRaster(ModelRasterInputs actual)
    { Cull = actual.Cull; FrontFace = actual.FrontFace; DepthTest = actual.DepthTest;
        DepthWrite = actual.DepthWrite; DepthCompare = actual.DepthCompare; }
    public RenderingDevice.PolygonCullMode Cull { get; }
    public RenderingDevice.PolygonFrontFace FrontFace { get; }
    public bool DepthTest { get; }
    public bool DepthWrite { get; }
    public RenderingDevice.CompareOperator DepthCompare { get; }
    internal static ModelGeometrySubmittedRaster CaptureActualSubmitted(ModelRasterInputs actual)
        => new(actual);
}
