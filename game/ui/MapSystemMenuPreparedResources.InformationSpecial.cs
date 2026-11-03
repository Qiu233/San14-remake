using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    // History, chronology and belongings borrow the same application owner
    // as the ordinary information pages. Only their actual static artwork
    // and fixed captions belong here; saved history and current rows do not.
    private static void PrepareSpecialInformationSources(UiAssets assets, MapFontSource font,
        LoadedMessages messages, Dictionary<MapFont.PreparedTextKey, MapTextCache> caches,
        CancellationToken cancellationToken)
    {
        Art(assets.SourceLayout(0xBC4131C3), [5]);
        _ = assets.SourceLayout(0xBC4131C3).Type6(3);
        Art(assets.SourceLayout(0xE1B83276), [2]);

        Art(assets.SourceLayout("00_82_08_sum_force_offset_03"), [3]);
        var chronology = assets.SourceLayout("11_05_00_history");
        Art(chronology, [5]);
        _ = chronology.Type6(3); _ = chronology.Type6(4);
        Art(assets.SourceLayout("11_05_00_history_offset_00"), [4,6,7,9,10,12,14,15,21,24]);
        Art(assets.SourceLayout("11_05_00_history_offset_01"), [3,5,6,8,9,11,13,14]);
        var dateHeader = assets.SourceLayout("11_05_00_history_offset_02");
        Art(dateHeader, dateHeader.Nodes.Where(node => node.Type is 1 or 3).Select(node => node.Index).ToArray());
        foreach (string icon in new[] { "000_cmn_icon_vew_old", "000_cmn_icon_vew_onoff" })
            assets.PrepareCommonSprite(icon, "texture_pk_common");
        foreach (string icon in new[] { "000_cmn_icon_advance_play", "000_cmn_icon_advance_stop" })
            assets.PrepareCommonSprite(icon);

        var presets = MapTextPresets.Create();
        var header = assets.SourceLayout(0xDC8AC8AA);
        foreach (int caption in new[] { 0x5BB, 0x7F, 0x69B })
            Text(header, 2, Caption(caption), 56, header.Rectangle(2).Size.X);
        Text(dateHeader, 3, Caption(0x2E5), 13, dateHeader.Rectangle(3).Size.X);
        Text(dateHeader, 7, Caption(0x2E6), 13, dateHeader.Rectangle(7).Size.X);
        var column = assets.SourceLayout("cmn_btn_clm");
        Art(column, [3,5,8,11]);
        foreach (var (node, caption) in new[] { (25,0x2E5), (24,0x2E6) })
            Column(caption, chronology.Rectangle(node).Size.X, personList:false);
        // Actual Item/Gem column profiles used by the belongings caller.
        // Resource preparation retains their authored widths and cell fit.
        foreach (var (width,caption) in new[] { (188,0x246),(104,0x401),(132,0x452),(132,0x411),
            (76,0x423),(160,0x424),(132,0x425),(76,0x426),(188,0x41E),(62,0x41F),
            (160,0x234),(160,0x455),(132,0x456),(104,0x4B4) })
            Column(caption,width,personList:true);
        Tab(assets.SourceLayout("cmn_btn_tab_l"), 0x2E4, chronology.Rectangle(6).Size.X);
        Tab(assets.SourceLayout(0x172EDC24), 0x246, Caption(0x246).Length*28+36);
        Tab(assets.SourceLayout(0xD45873AA), 0x699, Caption(0x699).Length*28+36);

        void Art(UiSourceLayout layout, int[] nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            assets.PrepareLayoutArt(layout.Source, nodes, commonImports:true);
        }
        string Caption(int id) => messages.Messages.Sections[1].Records[id].Text
            ?? throw new InvalidDataException("Missing special information caption.");
        void Text(UiSourceLayout layout, int node, string value, int preset, float width)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MapFont.PrepareText(font, value, MapFontProfile.FromNode(layout.Nodes[node], layout.Source.Source.ResourceUri)
                .WithPreset(presets[preset]), width, caches);
        }
        void Tab(UiSourceLayout layout, int caption, float width)
        {
            Art(layout, [3,5,8,11,13]);
            for (int state=0; state<4; state++)
                Text(layout,16,Caption(caption),8+state,layout.Rectangle(16).Size.X+width-layout.Nodes[0].Value44);
        }
        void Column(int caption, float width, bool personList)
        {
            var profile=MapFontProfile.FromNode(column.Nodes[13],column.Source.Source.ResourceUri);
            if(personList)profile=profile with { WidthFitMode=2,WidthReserve=0 };
            float textWidth=personList ? width-14 : column.Rectangle(13).Size.X+width-column.Nodes[0].Value44;
            for(int state=0;state<4;state++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MapFont.PrepareText(font,Caption(caption),profile.WithPreset(presets[3+state]),textWidth,caches);
            }
        }
    }
}
