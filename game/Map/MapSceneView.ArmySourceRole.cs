using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    //Loaded immutable resources can exist while the Startup map has no live
    //campaign Army publication owner. Retain these inputs, not a false empty
    //publication or a world with fabricated playable rules/receivers.
    private sealed record CampaignArmyVisualSources(GameResourceRepository Repository,
        LoadedScenario Scenario, LoadedMap Map, MapWorldProjection Projection,
        LoadedHeightField Height, string Locale, ArmyGraphicsActions Actions);
    private CampaignArmyVisualSources? _d3d11ArmySources;

    private void InstallCampaignArmyPublisher()
    {
        if (IsNewGameScenarioPreview)
            throw new InvalidOperationException("Startup cannot install the live campaign Army publication owner.");
        if (_d3d11Army is not null) return;
        var source = _d3d11ArmySources
            ?? throw new InvalidOperationException("Campaign Army visuals require the retained loaded map sources.");
        //The normal caller either loaded an actual campaign, or has drained
        //the Startup map, adopted the real Start world and bound its queries.
        //The original publisher retains all playable/source/receiver gates.
        _d3d11Army = new(source.Repository, source.Scenario, source.Map,
            source.Projection, source.Height, source.Locale, CaptureCurrentArmyMotion, source.Actions, CaptureCurrentArmyPresence);
    }
}
