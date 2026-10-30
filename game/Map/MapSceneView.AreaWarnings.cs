using San14.Data;

public partial class MapSceneView
{
    internal bool TryApplyAreaOccupationWarnings(AreaOccupationWarningPublication publication)
    {
        if(!IsPlayableMapReady||_d3d11Markers is not{} markers)return false;
        markers.ApplyAreaOccupationWarnings(publication);return true;
    }
}
