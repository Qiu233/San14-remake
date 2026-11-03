using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    // Ordinary information uses the current-world Scenery body, not the
    // title Collection layout. Only static art and captions are prepared here.
    private static void PrepareSceneryInformationSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, LoadedMessages messages, string locale,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken cancellationToken)
    {
        var source = assets.SourceLayout(SceneryInformationSources.LayoutName);
        var replacements = new HashSet<int> { 5, 11, 34, 38, 51 };
        bool Used(int index)
        {
            for (int? at = index; at is int node; at = source.Nodes[node].ParentIndex)
                if (replacements.Contains(node)) return false;
            return true;
        }
        Art(source, source.Nodes.Where(node => (node.Type is 1 or 3) && Used(node.Index)).Select(node => node.Index).ToArray());
        foreach (var node in source.Nodes.Where(node => node.Type == 6 && Used(node.Index))) _ = source.Type6(node.Index);
        // Current force and effect replace the authored sprite, but retain its
        // real mount. Prepare both actual state panels; the live query chooses one.
        Art(source, [11]);
        Art(assets.SourceLayout(0xAFC125D8), [1, 2]);
        Art(assets.SourceLayout(0x9F7499D5), [1]);
        Art(assets.SourceLayout(0xE35D91DD), [1]);
        foreach (string icon in new[] { "cmn_icon_map_city", "cmn_icon_map_gate", "cmn_icon_map_unit" })
            assets.PrepareCommonSprite(icon);
        foreach (int kind in new[] { 1, 2, 3 })
            assets.PrepareCommonSprite(SceneryInvestmentSources.EffectSprite(kind)!);

        // These are the same real kind27 image routes as ordinary scenery+48.
        // The existing catalog supplies only valid static definitions and their
        // actual image indices; no numeric gaps become resource dependencies.
        var collection = CollectionCatalog.Load(repository, locale);
        foreach (var image in collection.Sceneries.Select(row => collection.ReadScenery(row.SceneryId).Image).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            assets.PrepareCommonSprite(image.SpriteName, image.TexturePack);
        }
        var presets = MapTextPresets.Create();
        foreach (var (node, message) in SceneryInformationSources.Captions)
            Text(source, node, Caption(message), 13, source.Rectangle(node).Size.X);
        Text(source, 32, Caption(0x60C), 0, source.Rectangle(32).Size.X);
        foreach (int node in new[] { 31, 33 }) Text(source, node, Caption(0x353), 0, source.Rectangle(node).Size.X);
        var header = assets.SourceLayout(0xDC8AC8AA);
        Text(header, 2, Caption(SceneryInformationSources.TitleId), 56, header.Rectangle(2).Size.X);
        var tab = assets.SourceLayout(0x9AFB27B7);
        Art(tab, [3, 5, 8, 11, 13]);
        string basic = Caption(0x3CD);
        float tabWidth = tab.Rectangle(16).Size.X + basic.Length * 28 + 36 - tab.Nodes[0].Value44;
        for (int state = 0; state < 4; state++) Text(tab, 16, basic, 8 + state, tabWidth);
        var column = assets.SourceLayout("cmn_btn_clm");
        Art(column, [3, 5, 8, 11]);
        var columnProfile = MapFontProfile.FromNode(column.Nodes[13], column.Source.Source.ResourceUri)
            with { WidthFitMode = 2, WidthReserve = 0 };
        for (int state = 0; state < 4; state++)
            MapFont.PrepareText(font, Caption(0xAD), columnProfile.WithPreset(presets[3 + state]), 188 - 14, caches);

        string Caption(int id) => messages.Messages.Sections[1].Records[id].Text
            ?? throw new InvalidDataException("Missing ordinary Scenery detail caption.");
        void Art(UiSourceLayout layout, int[] nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            assets.PrepareLayoutArt(layout.Source, nodes, commonImports: true);
        }
        void Text(UiSourceLayout layout, int node, string value, int preset, float width)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MapFont.PrepareText(font, value, MapFontProfile.FromNode(layout.Nodes[node], layout.Source.Source.ResourceUri)
                .WithPreset(presets[preset]), width, caches);
        }
    }
}
