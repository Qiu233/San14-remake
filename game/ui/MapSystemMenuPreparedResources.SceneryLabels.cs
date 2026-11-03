using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    // These are the actual scenery label's replacement county layouts.
    // Fixed art and source names are prepared once; current force colours,
    // names edited during play, geometry and interactions remain view data.
    private static void PrepareSceneryLabelSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, CollectionCatalog catalog,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var marker = SceneryMapMarkerSources.Load(repository);
        _ = assets.SourceLayout(marker.Root.Source.FileId);
        var text = assets.SourceLayout(marker.Text.Source.FileId);
        _ = assets.SourceLayout(marker.Flag.Source.FileId);
        assets.PrepareLayoutArt(text.Source, [6,7,13,14], commonImports: true);
        assets.PrepareCommonSprite("000_cmn_icon_scenic_outline", "texture_ce_common");
        assets.PrepareCommonSprite("000_cmn_icon_scenic", "texture_ce_common");
        var profile = MapFontProfile.FromNode(marker.Text.Nodes[SceneryMapMarkerSources.NameNode],
            marker.Text.Source.ResourceUri).WithPreset(MapTextPresets.Create()[SceneryMapMarkerSources.NameTextPreset]);
        foreach (string name in catalog.Sceneries.Select(row => row.Name).Distinct())
        {
            token.ThrowIfCancellationRequested();
            MapFont.PrepareText(font, name, profile, marker.NameWidthRequests(name).Name, caches);
        }
    }
}
