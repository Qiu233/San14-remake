using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    private static (CollectionCatalog Catalog, CollectionItemCatalog Items, PoemEventCatalog PoemEvents)
        PrepareTitleCatalogSources(GameResourceRepository repository, UiAssets assets, MapFontSource font,
            LoadedMessages messages, string locale, Dictionary<MapFont.PreparedTextKey, MapTextCache> caches,
            CancellationToken cancellationToken)
    {
        var catalog = CollectionCatalog.Load(repository, locale);
        var items = repository.LoadCollectionItemCatalog(locale);
        var events = PoemEventCatalog.Load(repository, locale);
        var presets = MapTextPresets.Create();
        var poem = Template("02_07_06_info_collection_sibun_cn", [2]);
        var scenery = Template("02_07_07_info_collection_meishou_cn", [5, 11]);
        var item = Template("11_01_08_00_collection_item_cn", [2, 7]);
        foreach (int id in new[] { 0xB8, 0xB7, 0x6D })
            Text(assets.SourceLayout(0xDC8AC8AA), 2, Caption(id), 56);
        foreach (var column in catalog.PoemColumns.Concat(catalog.SceneryColumns)
            .Concat(items.Tabs.SelectMany(tab => tab.Columns)))
            Column(column.Caption, column.Width);
        foreach (int id in new[] { 0x3CD, 0x2DC }) Tab(Caption(id));

        foreach (var (node, id) in new[] { (19, 0x201), (24, 0x202), (30, 0x400) })
            Text(scenery, node, Caption(id), 13);
        Text(poem, 16, Caption(0x400), 13);
        string language = locale == "TC" ? "tw" : "ch";
        assets.PrepareCommonSprite(language + "_000_cmn_txt_kansho", "texture_ce_lang_" + language);
        var appreciate = assets.CommandButtonLayout(new(96, 48));
        Art(appreciate, [3, 5, 6, 8, 9, 11, 13, 14]);
        for (int state = 0; state < 5; state++) Text(appreciate, 16, Caption(0xAE), 28 + state);

        foreach (var row in catalog.Poems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = catalog.ReadPoem(row.ItemId);
            Image(value.Image);
            Text(poem, 9, value.Name); Text(poem, 11, value.Author); Text(poem, 12, value.Era);
        }
        foreach (var row in catalog.Sceneries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = catalog.ReadScenery(row.SceneryId);
            Image(value.Image);
            Text(scenery, 13, value.Name); Text(scenery, 20, value.CityName); Text(scenery, 25, value.AreaName);
        }
        foreach (int id in items.Tabs.SelectMany(tab => items.ReadRows(tab.Id)).Select(row => row.ItemId).Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = items.ReadItem(id);
            if (value.Image is { } image) Image(image);
            foreach (var field in value.DetailFields)
                if (field.Key != 42)
                    Text(item, field.Key, field.Value, field.Key is 16 or 21 or 26 or 31 or 36 or 41 ? 13 : 0);
        }

        var help = assets.SourceLayout(0xAB6ADAA7);
        int hintNode = help.Nodes.Single(node => node.Type == 2).Index;
        var game = repository.LoadMessages(locale, MessageKind.Game).Messages;
        foreach (int id in new[] { 0x2984, 0x2985, 0x2986, 0x2987 })
        {
            string value = game.Sections[id / 10000].Records[id % 10000].Literals.Single().Text;
            MapFont.PrepareText(font, value, MapFontProfile.FromNode(help.Nodes[hintNode], help.Source.Source.ResourceUri),
                help.Rectangle(hintNode).Size.X, caches);
        }
        Art(help, help.Nodes.Where(node => node.Type is 1 or 3).Select(node => node.Index));
        var info = Template("02_07_06_info_sibun_cn", [7], includeType6: true);
        foreach (int id in events.ItemIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var publication = events.Read(id);
            var source = assets.SourceLayout(publication.LayoutName);
            var replacements = publication.Images.ToDictionary(image => image.NodeId);
            var specialized = new HashSet<int>();
            if (publication.MaxTextScrollOffsetX > 0)
            {
                foreach (var image in publication.Images)
                {
                    int glyph = image.NodeId, colour = glyph + 1;
                    specialized.Add(glyph); specialized.Add(colour);
                    Part(source, SinglePart(source, glyph), image.Image);
                    Part(source, SinglePart(source, colour), null);
                    Part(source, SinglePart(source, 4), null);
                }
                specialized.Add(4);
            }
            foreach (var node in source.Nodes.Where(node => (node.Type is 1 or 3) && !specialized.Contains(node.Index)))
            {
                var groups = LcskSpriteNodeReader.Read(source.Source.Layout, node).Groups;
                CollectionImageRoute? replacement = replacements.TryGetValue(node.Index, out var image) ? image.Image : null;
                if (node.Type == 1 && groups.Single().ComponentCount == 2)
                {
                    var pair = groups.Single().Parts;
                    Part(source, pair[0], replacement); Part(source, pair[1], null);
                }
                else if (replacement is not null) Image(replacement);
                else Art(source, [node.Index]);
            }
            Image(publication.Info.Poem.Image);
            Text(info, 14, publication.Info.Poem.Name);
            Text(info, 16, publication.Info.Poem.Author);
            Text(info, 17, publication.Info.Poem.Era);
            Text(info, 21, publication.Info.DescriptionCaption, 13);
        }
        // Descriptions retain the real paragraph wrapping consumer. Static
        // catalog text and font/image sources are retained by this generation;
        // view-local line geometry is not a second font or resource owner.
        return (catalog, items, events);

        UiSourceLayout Template(string name, int[] replaced, bool includeType6 = false)
        {
            var source = assets.SourceLayout(name);
            Art(source, source.Nodes.Where(node => (node.Type is 1 or 3) && !replaced.Contains(node.Index))
                .Select(node => node.Index));
            if (includeType6)
                foreach (var node in source.Nodes.Where(node => node.Type == 6)) _ = source.Type6(node.Index);
            return source;
        }
        void Art(UiSourceLayout source, IEnumerable<int> nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            assets.PrepareLayoutArt(source.Source, nodes, commonImports: true);
        }
        string Caption(int id) => messages.Messages.Sections[1].Records[id].Text
            ?? throw new InvalidDataException("Missing title catalog caption.");
        void Text(UiSourceLayout source, int node, string value, int preset = 0)
            => MapFont.PrepareText(font, value, MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri)
                .WithPreset(presets[preset]), source.Rectangle(node).Size.X, caches);
        void Image(CollectionImageRoute image)
        {
            if (image.Index > 0) assets.PrepareCommonSprite(image.SpriteName, image.TexturePack);
        }
        void Column(string caption, float width)
        {
            var column = assets.SourceLayout("cmn_btn_clm");
            var profile = MapFontProfile.FromNode(column.Nodes[13], column.Source.Source.ResourceUri)
                with { WidthFitMode = 2, WidthReserve = 0 };
            for (int state = 0; state < 4; state++)
                MapFont.PrepareText(font, caption, profile.WithPreset(presets[3 + state]), width - 14, caches);
        }
        void Tab(string caption)
        {
            var tab = assets.SourceLayout(0x9AFB27B7);
            float width = tab.Rectangle(16).Size.X + caption.Length * 28 + 36 - tab.Nodes[0].Value44;
            for (int state = 0; state < 4; state++)
                MapFont.PrepareText(font, caption, MapFontProfile.FromNode(tab.Nodes[16], tab.Source.Source.ResourceUri)
                    .WithPreset(presets[8 + state]), width, caches);
        }
        LcskSpriteNodePart SinglePart(UiSourceLayout source, int node)
            => LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[node]).Groups.Single().Parts.Single();
        void Part(UiSourceLayout source, LcskSpriteNodePart part, CollectionImageRoute? image)
        {
            var uv = part.Uv;
            if (uv.BottomRight.U < uv.TopLeft.U) uv = new(uv.TopRight, uv.TopLeft, uv.BottomRight, uv.BottomLeft);
            if (uv.BottomRight.V < uv.TopLeft.V) uv = new(uv.BottomLeft, uv.BottomRight, uv.TopLeft, uv.TopRight);
            if (image is null) assets.PrepareSrgbLayoutSpritePart(source.Source, part.SpriteName, uv);
            else assets.PrepareSrgbTexturePackSpritePart(image.TexturePack, image.SpriteName, uv);
        }
    }
}
