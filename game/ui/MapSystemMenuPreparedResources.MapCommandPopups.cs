using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    // Static inputs of the actual location menu, deployment-cancellation
    // popup and manual-tactic page. Current rosters and values stay in views.
    private static void PrepareMapCommandPopupSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, LoadedMessages messages, string locale,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken token)
    {
        var presets = MapTextPresets.Create();
        var location = assets.SourceLayout(0x26D980DF);
        Art(location, [2]);
        var button = assets.SourceLayout(0x7BC42A66).ResizedSubtree(0, location.Rectangle(3).Size, true);
        Art(button, [3,5,6,8,11,13,14]);
        // Existing location-popup captions plus every root of the actual
        // primary/secondary CityCommandMenuProjection.
        foreach (int id in new[] { 0x4B7,0x29A,0xA,0x5E,0x27,0x5FA,0x29,
            0x223,0x1D,0x1E,0x22,0x24,0x25,0xB1 }.Distinct())
        {
            string caption = messages.Messages.Sections[1].Records[id].Text
                ?? throw new InvalidDataException("Location command caption is absent.");
            var profile = MapFontProfile.FromNode(button.Nodes[16], button.Source.Source.ResourceUri);
            MapFont.PrepareText(font, caption, profile, button.Rectangle(16).Size.X, caches);
            foreach (int style in new[] { 3,4,5,6 })
                MapFont.PrepareText(font, caption, profile.WithPreset(presets[style]), button.Rectangle(16).Size.X, caches);
        }

        var childPane = assets.SourceLayout(0x6E4B5E76);
        Art(childPane, [1]);
        var childButton = assets.SourceLayout(0xBD19DBED);
        Art(childButton, [3,6,7,10,15,19,20]);
        int[] childTextNodes = [4,8,12,16,21];
        foreach (int id in new[] { 0x4B7,0x476,0x270,0x271,0x25 })
        {
            string caption = messages.Messages.Sections[1].Records[id].Text
                ?? throw new InvalidDataException("City child command caption is absent.");
            for (int state = 0; state < childTextNodes.Length; state++)
            {
                int node = childTextNodes[state];
                var profile = MapFontProfile.FromNode(childButton.Nodes[node], childButton.Source.Source.ResourceUri)
                    .WithPreset(presets[28 + state]);
                MapFont.PrepareText(font, caption, profile, childButton.Rectangle(node).Size.X, caches);
            }
        }

        var cancellation = assets.SourceLayout(0xFE9C1A61);
        Art(cancellation, [2]); _ = cancellation.Type6(1);
        var row = assets.SourceLayout(0x4BC1E64C);
        Art(row, [2,3,5,7,14,15]);
        Art(assets.SourceLayout(0xAFC125D8), [1,2]);
        // Face5 masks are already prepared by PrepareDeploymentSources from
        // their actual face template, not this row's replaced portrait mount.
        Art(assets.SourceLayout(0x9F416DB7), [2,5,7,8,10,11,13,19,21,22,24,25,27,33,35,36,38,39,41]);

        _ = assets.SourceLayout(0xD8913AB2);
        _ = assets.SourceLayout(0x1CDA8363);
        var body = assets.SourceLayout("00_10_01_saihai_offset_20");
        Art(body, [2,48,54,60]);
        var hint = assets.SourceLayout(0xAB6ADAA7);
        Art(hint, hint.Nodes.Where(node => node.Type is 1 or 3).Select(node => node.Index));
        var hintNode = hint.Nodes.Single(node => node.Type == 2);
        string target = new AdministrationMessages(repository, locale).Format(0xC965);
        MapFont.PrepareText(font, target, MapFontProfile.FromNode(hintNode, hint.Source.Source.ResourceUri),
            hint.Rectangle(hintNode.Index).Size.X, caches);
        _ = assets.PrepareCommonCaption(locale, "000_cmn_txt_decision_modoru");
        token.ThrowIfCancellationRequested();

        void Art(UiSourceLayout source, IEnumerable<int> nodes)
        { token.ThrowIfCancellationRequested(); assets.PrepareLayoutArt(source.Source, nodes, commonImports:true); }
    }

    private void PrepareMapCommandPopupCaptions(PreviewText text)
    {
        var header = Assets.SourceLayout(0xDC8AC8AA);
        var profile = MapFontProfile.FromNode(header.Nodes[2], header.Source.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[56]);
        MapFont.PrepareText(FontSource, text.Get("play_tactic_active_title"), profile,
            header.Rectangle(2).Size.X, textCaches);
        UiSourceSimpleTooltip.PreparePlainText(Assets.SourceLayout(0xBE0DC488), FontSource,
            text.Get("detail_unknown"), textCaches);
        var hint = Assets.SourceLayout(0xAB6ADAA7);
        var node = hint.Nodes.Single(value => value.Type == 2);
        var hintProfile = MapFontProfile.FromNode(node, hint.Source.Source.ResourceUri);
        foreach (string key in new[] { "play_tactic_choice", "detail_unknown" })
            MapFont.PrepareText(FontSource, text.Get(key), hintProfile, hint.Rectangle(node.Index).Size.X, textCaches);
    }
}
