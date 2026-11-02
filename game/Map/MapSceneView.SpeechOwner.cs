using San14.Core;

public partial class MapSceneView
{
    // Only Main's synchronous, committed speech transaction may call this.
    // Campaign state did not change: retain the map, selections, controllers,
    // presentation clocks and accepted render publications exactly as they are.
    internal bool AdoptSpeechWorld(GameState previous, GameState replacement)
    {
        if (ReferenceEquals(previous, replacement) || !ReferenceEquals(_logicalWorld, previous)) return false;

        _logicalWorld = replacement;
        if (ReferenceEquals(_cityDomesticMenuWorld, previous)) _cityDomesticMenuWorld = replacement;
        if (ReferenceEquals(_areaHexCommandWorld, previous)) _areaHexCommandWorld = replacement;
        _sceneryMap?.AdoptSpeechWorld(previous, replacement);

        // These are cache ownership keys, not accepted-frame provenance. The
        // retained current and its compact pages remain the same. Leave the
        // bound-current key behind so the next normal summary tick rebinds
        // the query adapter's state readers to the actual replacement owner.
        if (ReferenceEquals(_summaryInputWorld, previous)) _summaryInputWorld = replacement;
        if (ReferenceEquals(_summaryCachedWorld, previous)) _summaryCachedWorld = replacement;
        if (ReferenceEquals(summaryFocusWorld, previous)) summaryFocusWorld = replacement;
        if (ReferenceEquals(informationWorld, previous)) informationWorld = replacement;
        if (ReferenceEquals(_facilityInfoCaptureWorld, previous)) _facilityInfoCaptureWorld = replacement;

        // Reuse the detached campaign snapshot for this presentation-only
        // commit. Never rewrite a queued, prepared or accepted frame capture.
        if (ReferenceEquals(_nativeMapWorldSource, previous)) _nativeMapWorldSource = replacement;
        if (ReferenceEquals(_nativeMapSuspendedWorldSource, previous)) _nativeMapSuspendedWorldSource = replacement;
        return true;
    }
}

internal partial class SceneryMapLayer
{
    // Current is a retained query cache. Prepared/Accepted retain the exact
    // frame and World which really produced them until another frame succeeds.
    internal void AdoptSpeechWorld(GameState previous, GameState replacement)
    {
        if (ReferenceEquals(currentWorld, previous)) currentWorld = replacement;
    }
}
