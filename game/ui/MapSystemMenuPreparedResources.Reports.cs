using Godot;
using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    // The report's fixed artwork and captions belong to the same startup
    // generation as the rest of the application UI. Current history, totals,
    // advice, portrait selection and graph geometry remain report-owned.
    private static void PrepareReportSources(UiAssets assets, MapFontSource font,
        LoadedMessages messages, string locale,
        IDictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken cancellation)
    {
        var records = messages.Messages.Sections[1].Records;
        var presets = MapTextPresets.Create();
        string Caption(int id) => records[id].Text
            ?? throw new InvalidDataException("Missing report caption: " + id);
        void Art(UiSourceLayout layout, IEnumerable<int> indices)
        {
            cancellation.ThrowIfCancellationRequested();
            assets.PrepareLayoutArt(layout.Source, indices, commonImports: true);
        }
        void FixedText(UiSourceLayout layout, int node, int message, int preset)
        {
            cancellation.ThrowIfCancellationRequested();
            MapFont.PrepareText(font, Caption(message),
                MapFontProfile.FromNode(layout.Nodes[node], layout.Source.Source.ResourceUri).WithPreset(presets[preset]),
                layout.Rectangle(node).Size.X, caches);
        }

        var income = assets.SourceLayout(0x3E9CC8AD);
        Art(income, [27]);
        var side = assets.SourceLayout(0xA3ABC4B3);
        Art(side, [13]);
        var numbers = assets.SourceLayout(0x8A0BAA70);
        Art(numbers, numbers.Nodes.Where(node => node.Type is 1 or 3).Select(node => node.Index));
        foreach (var (node, message) in new[] { (2, 0x20F), (4, 0x210), (9, 0x20C),
            (18, 0x20D), (27, 0x20E), (36, 0x835) }) FixedText(numbers, node, message, 13);
        Art(assets.SourceLayout(0x47835420), [3]);

        _ = assets.SourceLayout(0xF6352F00);
        assets.PrepareCommonSprite("stl_event_bg_028", "texture_stl_event_bg_028");
        Art(assets.SourceLayout(0x2B7AA77D), [3, 5, 6, 8, 9, 11, 13, 14]);
        var action = assets.SourceLayout(0xDBB537B1);
        Art(action, [4]);
        _ = action.Type6(3);
        Art(assets.SourceLayout(0xE0AA945F), [1]);
        Art(assets.SourceLayout(0x79D1D49E), [1, 8]);
        foreach (string name in new[] { "000_cmn_icon_opt", "000_cmn_icon_map_mode", "000_cmn_icon_info",
            "000_cmn_icon_kane", "000_cmn_icon_st_hyourou", "000_cmn_icon_heishi" })
            assets.PrepareCommonSprite(name);
        assets.PrepareCommonSprite("000_cmn_icon_gunjou", "texture_ce_common");
        foreach (string name in new[] { "base_pad", "pad_l1", "pad_r1", "pad_triangle" })
            assets.PrepareCommonSprite(name, "texture_pad");
        _ = assets.SourceLayout(0x95761708);
        var confirmation = assets.SourceLayout(0x2F276C90);
        Art(confirmation, [3, 5, 8, 12, 14]);
        string language = locale switch { "TC" => "tw", "SC" => "ch",
            _ => throw new NotSupportedException("Unsupported report locale: " + locale) };
        assets.PrepareCommonSprite(language + "_000_cmn_txt_decision_kakunin", "texture_lang_" + language);

        // Graph layouts are explicit dependencies even before a campaign has
        // accumulated history. Creating curves or fake history is unnecessary.
        foreach (string name in new[] { "05_00_01_shushi_offset_01_graph", "05_00_01_shushi_offset_01_point" })
        {
            var layout = assets.SourceLayout(name);
            Art(layout, layout.Nodes.Where(node => node.Type is 1 or 3).Select(node => node.Index));
        }
        _ = assets.SourceLayout("05_00_01_shushi_offset_01_bar");
        var balloon = assets.SourceLayout("05_00_01_shushi_offset_02");
        Art(balloon, balloon.Nodes.Where(node => node.Index != 4 && node.Type is 1 or 3).Select(node => node.Index));
        var header = assets.SourceLayout(0xDC8AC8AA);
        foreach (int message in new[] { 0x5A3, 0x5E9, 0x329 }) FixedText(header, 2, message, 56);
        foreach (var (node, message) in new[] { (1, 0x5F4), (3, 0x443), (1, 0x20C),
            (3, 0x20D), (5, 0x20E), (7, 0x835) })
        {
            var button = assets.CommandButtonLayout(side.Rectangle(node).Size);
            Art(button, [3, 5, 6, 8, 9, 11, 13, 14]);
            _ = assets.ButtonStateProfile(button.Source);
            for (int state = 0; state < 5; state++) FixedText(button, 16, message, 28 + state);
        }
        int[] actionCaptions = [0x5F7, 0x586, 0x287, 0x2A5, 0x3FB, 0x835, 0x5B5];
        for (int index = 0; index < actionCaptions.Length; index++)
            PrepareTab(index == 0 ? 0x172EDC24u : index == actionCaptions.Length - 1 ? 0xD45873AAu : 0x9AFB27B7u,
                action.Rectangle(5 + index * 2).Size, actionCaptions[index]);
        for (int index = 0; index < 8; index++)
            foreach (int message in Enumerable.Range(0x38D, 8))
                foreach (uint file in new[] { 0x172EDC24u, 0x9AFB27B7u, 0xD45873AAu })
                    PrepareTab(file, income.Rectangle(2 + index * 2).Size, message);
        cancellation.ThrowIfCancellationRequested();

        void PrepareTab(uint file, Vector2 size, int message)
        {
            var layout = assets.SourceLayout(file);
            Art(layout, [3, 5, 8, 11, 13]);
            var caption = layout.Rectangle(16);
            caption.Size += size - layout.Rectangle(0).Size;
            var profile = MapFontProfile.FromNode(layout.Nodes[16], layout.Source.Source.ResourceUri);
            // Construction prepares the authored profile before its first
            // state selects the retained normal/hover/pressed/disabled preset.
            MapFont.PrepareText(font, Caption(message), profile, caption.Size.X, caches);
            for (int state = 0; state < 4; state++)
                MapFont.PrepareText(font, Caption(message), profile.WithPreset(presets[8 + state]), caption.Size.X, caches);
        }
    }
}
