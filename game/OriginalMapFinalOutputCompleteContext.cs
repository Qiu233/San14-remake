using Godot;
using San14.Data;

/// <summary>Complete retained native state at the accepted Apply boundary.
/// Source Camera writers and source functor uploads are applied by the owner.
/// The owned working surface explicitly selects its full viewport.</summary>
public sealed record OriginalMapFinalOutputEffectsContext(
    IReadOnlyDictionary<int,ReadOnlyMemory<byte>> CurrentParameters,
    IReadOnlyDictionary<int,IReadOnlyDictionary<(uint Stage,uint Register),ReadOnlyMemory<byte>>> RetainedTemplatesByPass,
    OriginalMapFinalOutputVertexWrapperState Wrapper,
    OriginalMapFinalOutputVertexWrapperState RadialWrapper,
    OriginalModelRasterInputs OrdinaryRaster,Vector2 StockScreenScale,Color StockColorScale,
    ReadOnlyMemory<byte> RetainedStockPixelConstants,string NativeContextSource);

/// <summary>Same accepted Camera and actual native texture producers. The
/// complete route owns Hex/Bloom/Star/Flare/Shaft and has no external effect
/// image slots. Source LUT type0/intensity0 uses the proved dead branch.</summary>
public sealed record OriginalMapFinalOutputCompleteFrameInputs(
    OriginalMapCameraConstructionPacket Camera,OriginalMapExposurePacket Exposure,
    OriginalMapFinalOutputDofWeightPacket DofWeight,OriginalMapFinalOutputEffectsContext Effects,
    OriginalMapFinalOutputSceneDrawPacket FinalDraw,string NativeContextSource)
{
    public OriginalMapFinalOutputHdrRoute? HdrRoute { get; init; }
}
