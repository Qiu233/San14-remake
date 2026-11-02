using San14.Data;

public partial class MapSceneView
{
    // The actually accepted campaign image owns this season. A requested or
    // not-yet-presented World date is not evidence that the scene has changed.
    internal MapSeasonRuntimeFrame? DisplayedCampaignSeason =>
        _d3d11Map?.LastAcceptedInput?.CurrentSeason?.Calendar as MapSeasonRuntimeFrame;
}
