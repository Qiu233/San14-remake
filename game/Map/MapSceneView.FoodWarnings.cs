using San14.Data;

public partial class MapSceneView
{
    internal void ApplyFoodWarnings(NormalTurnFoodWarningPublication publication)
    {
        if (!IsPlayableMapReady)
            throw new InvalidOperationException("map.food-warning.map_owner_not_ready");
        if (_d3d11Markers is not { } markers)
        {
            //This preview backend has no Base tooltip views. Clearing an
            //empty population is real; a nonempty request must stay pending.
            if (publication.Markers.Count == 0) return;
            throw new InvalidOperationException("map.food-warning.preview_tooltip_consumer");
        }
        markers.ApplyFoodWarnings(_repository, publication);
    }
}
