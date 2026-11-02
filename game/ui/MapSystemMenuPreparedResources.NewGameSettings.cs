using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    // The same static resource table supplies every row's title and choices.
    // Preparing their exact source profiles keeps first display and later
    // group rebinding on the existing resident font generation.
    private static void PrepareNewGameSettingsText(UiAssets assets, MapFontSource font,
        LoadedMessages messages, Dictionary<MapFont.PreparedTextKey, MapTextCache> caches,
        CancellationToken cancellationToken)
    {
        var presets = MapTextPresets.Create();
        foreach (var row in ArmyCampaignRuleAdapter.ReadNewGameSettingResourceText(messages))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = row.Choices.Count;
            var source = assets.SourceLayout(count == 5 ? 0x7F1F1D88u : 0x28012CA9u);
            MapFont.PrepareText(font, row.Title,
                MapFontProfile.FromNode(source.Nodes[3], source.Source.Source.ResourceUri), source.Rectangle(3).Size.X, caches);
            for (int index = 0; index < count; index++)
            {
                var box = ScenarioSettingsSourceLayout.Radio(source, index, count);
                uint file = index == 0 ? 0xF2F266CAu : index == count - 1 ? 0x0C1434D0u : 0x4D3FB1C1u;
                var radio = assets.SourceLayout(file).ResizedSubtree(0, box.Size, true);
                var profile = MapFontProfile.FromNode(radio.Nodes[16], radio.Source.Source.ResourceUri);
                for (int state = 0; state < 5; state++)
                    MapFont.PrepareText(font, row.Choices[index], profile.WithPreset(presets[3 + state]),
                        radio.Rectangle(16).Size.X, caches);
            }
        }
    }
}
