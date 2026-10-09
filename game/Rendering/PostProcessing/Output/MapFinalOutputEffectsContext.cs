using Godot;
using San14.Data;

/// <summary>Complete retained native state at the accepted Apply boundary.
/// Source Camera writers and source functor uploads are applied by the owner.
/// The owned working surface explicitly selects its full viewport.</summary>
public sealed record MapFinalOutputEffectsContext(
    IReadOnlyDictionary<int,ReadOnlyMemory<byte>> CurrentParameters,
    IReadOnlyDictionary<int,IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>>> RetainedTemplatesByPass,
    MapFinalOutputVertexWrapperState Wrapper,
    MapFinalOutputVertexWrapperState RadialWrapper,
    ModelRasterInputs OrdinaryRaster,Vector2 StockScreenScale,Color StockColorScale,
    ReadOnlyMemory<byte> RetainedStockPixelConstants,string NativeContextSource);

/// <summary>Same accepted Camera and actual native texture producers. The
/// complete route owns Hex/Bloom/Star/Flare/Shaft and has no external effect
/// image slots. Source LUT type0/intensity0 uses the proved dead branch.</summary>
public sealed record MapFinalOutputCompleteFrameInputs(
    MapCameraConstructionPacket Camera,MapExposurePacket Exposure,
    MapFinalOutputDofWeightPacket DofWeight,MapFinalOutputEffectsContext Effects,
    MapFinalOutputSceneDrawPacket FinalDraw,string NativeContextSource)
{
    public MapFinalOutputHdrRoute? HdrRoute { get; init; }
}
