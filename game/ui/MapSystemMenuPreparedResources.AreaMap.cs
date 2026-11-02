using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    private static void PrepareAreaMapSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, string locale, Dictionary<MapFont.PreparedTextKey, MapTextCache> caches,
        CancellationToken cancellation)
    {
        var face = assets.SourceLayout(0x7104E19F);
        var compact = assets.SourceLayout(0xC822D27E);
        _ = assets.SourceLayout(0xBED46BA4);
        var icon = assets.SourceLayout(0x1F40C35D);
        var clips = new HashSet<string>(StringComparer.Ordinal) { "0_normal", "1_focus ", "2_push", "3_disable" };
        _ = assets.SourceAnimationClips(face, clips); _ = assets.SourceAnimationClips(compact, clips);
        var portraitNodes = new HashSet<int> { 7, 16, 24, 30, 32 };
        assets.PrepareLayoutArt(face.Source, face.Nodes.Where(node => (node.Type is 1 or 3) &&
            !portraitNodes.Contains(node.Index) && node.Index != 36).Select(node => node.Index), commonImports: true);
        foreach (int index in portraitNodes)
        {
            var group = LcskSpriteNodeReader.Read(face.Source.Layout, face.Nodes[index]).Groups.Single();
            if (group.ComponentCount is not (2 or 3) || group.Parts.Count != group.ComponentCount)
                throw new InvalidDataException("area-map.portrait_mask_components");
            foreach (var part in group.Parts.Skip(1))
            {
                var mask = assets.SourceLayoutSprite(face.Source, part.SpriteName);
                assets.PrepareSprite(mask, MapPortraitQuad.UsesSrgbView(repository, assets, part.SpriteName, mask));
            }
        }
        assets.PrepareLayoutArt(compact.Source, [5, 6, 8, 9], commonImports: true);
        assets.PrepareLayoutArt(icon.Source, [2, 4, 5, 6, 8], commonImports: true);
        foreach (string name in AreaAdministrationMapButton.DevelopmentSprites) assets.PrepareCommonSprite(name);
        // These existing Domestic pages now borrow this same application
        //owner instead of allocating a new font/image collection per page.
        Art(0xA3B4A82E, [3]); Art(0xD15BF749, [2, 3, 6]);
        Art(0xC4464DC9, [2, 5, 7, 10, 12, 94, 95, 96]);
        Art(0xB178BC3C, [1, 2]); Art(0xB8DDF15B, [1, 2, 4]);
        Art(0xCB7DEBFC, [1, 11]); Art(0x27119CFA, [1, 4]); Art(0xC7A85B99, [1]);
        Art(0x3C47C086, [1]); _ = assets.SourceLayout(0xC043267A);
        assets.PrepareCommonSprite("cmn_mrk_max"); assets.PrepareCommonSprite("000_cmn_gauge_circle_bar");
        foreach (string prefix in new[] { "commerce", "agriculture", "barracks" })
            for (int level = 0; level <= 5; level++) assets.PrepareCommonSprite("texture_city_" + prefix + "_" + level, "texture_stl_city");
        var messages = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1];
        var presets = MapTextPresets.Create();
        foreach (int id in new[] { 0x04B7, 0x05AF, 0x032E })
        {
            cancellation.ThrowIfCancellationRequested();
            var header = assets.SourceLayout(0xDC8AC8AA);
            MapFont.PrepareText(font, messages.Records[id].Text ?? throw new InvalidDataException("area-map.header_caption"),
                MapFontProfile.FromNode(header.Nodes[2], header.Source.Source.ResourceUri).WithPreset(presets[56]),
                header.Rectangle(2).Size.X, caches);
        }
        void Art(uint file, int[] nodes)
        { cancellation.ThrowIfCancellationRequested(); var source = assets.SourceLayout(file); assets.PrepareLayoutArt(source.Source, nodes, commonImports: true); }
    }
}
