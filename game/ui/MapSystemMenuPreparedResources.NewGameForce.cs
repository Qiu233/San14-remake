using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    // Fixed dependencies of the normal force page and the loading surface.
    // They belong to the existing startup generation, just like Title and
    // Scenario. Current-person images and world previews keep their actual
    // routing owners; this list does not manufacture a campaign to obtain them.
    private static void PrepareNewGameForceSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, LoadedMessages messages, string locale,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken cancellationToken)
    {
        PrepareNewGameSettingsText(assets, font, messages, caches, cancellationToken);
        var presets = MapTextPresets.Create();
        var records = messages.Messages.Sections[1].Records;
        var force = Art(0xB468AE65, [4, 6, 8, 49, 50, 51, 52, 53, 55, 56, 57,
            82, 84, 85, 87, 88, 90, 92, 93]);
        PortraitMasks(force, 3, 2);
        Art(0x999728A3, [38]);
        Art(0x0CF6FDA2, [5, 9, 15, 20, 24]);
        PortraitMasks(assets.SourceLayout(0xA76F2721), 1, 3);
        Art(0x6C522E54, [2]);
        var loading = Art(0xF6332D26, [1, 3]);
        _ = loading.Type6(2);
        // NewGameSetup currently mounts its ordinary settings alongside the
        // force page. Predeclare only those fixed skins, without creating
        // hidden Controls or building their world-dependent publications.
        _ = assets.SourceLayout(0xA32D4D03);
        _ = assets.SourceLayout(0x5C9D1E9E);
        Art(0xD6361A53, [1]);
        foreach (uint file in new uint[] { 0x28012CA9, 0x7F1F1D88 }) Art(file, [2]);
        foreach (uint file in new uint[] { 0xF2F266CA, 0x4D3FB1C1, 0x0C1434D0 })
            Art(file, [3, 5, 8, 11, 13, 17]);
        var footer = Art(0xCEE6F358, [3, 5, 8, 11, 13]);
        assets.PrepareCommonSprite((locale == "TC" ? "tw" : "ch") + "_000_cmn_txt_decision_chushi",
            "texture_lang_" + (locale == "TC" ? "tw" : "ch"));
        int[] nodes = [61, 63, 65, 67, 69, 71, 75, 77];
        int[] captions = [0x20C, 0x20D, 0x20E, 0x21B, 0x207, 0x218, 0x83E, 0x2E1];
        for (int index = 0; index < nodes.Length; index++) Fixed(force, nodes[index], captions[index], 13);
        foreach (int caption in new[] { 0x242, 0x371, 0x36E })
            for (int state = 0; state < 5; state++) Fixed(footer, 16, caption, 3 + state);
        for (int state = 0; state < 5; state++) Fixed(force, 95, 0x35F, 3 + state);
        var header = assets.SourceLayout(0xDC8AC8AA);
        foreach (int caption in new[] { 0x36D, 0xBB, 0x709 }) Fixed(header, 2, caption, 56);
        // The force page's geographic child is the actual two-group
        // information list. Its common list skins are already prepared by
        // PrepareDeploymentSources; these are its additional fixed captions.
        var geographicColumn=assets.SourceLayout("cmn_btn_clm");
        foreach(var binding in new[]{(Caption:0x2E1,Width:104),(Caption:0x1FD,Width:132),
            (Caption:0x400,Width:1000),(Caption:0x412,Width:440)})
        {
            var profile=MapFontProfile.FromNode(geographicColumn.Nodes[13],geographicColumn.Source.Source.ResourceUri)
                with {WidthFitMode=2,WidthReserve=0};
            for(int state=0;state<4;state++)
                MapFont.PrepareText(font,records[binding.Caption].Text??throw new InvalidDataException("Missing geographic column caption."),
                    profile.WithPreset(presets[3+state]),binding.Width-14,caches);
        }
        foreach(var binding in new[]{(File:0x172EDC24u,Caption:0x2DC),(File:0xD45873AAu,Caption:0x412)})
        {
            var tab=Art(binding.File,[3,5,8,11,13]);
            string caption=records[binding.Caption].Text??throw new InvalidDataException("Missing geographic group caption.");
            float width=caption.Length*28+36;
            for(int state=0;state<4;state++)Fixed(tab,16,binding.Caption,8+state,
                tab.Rectangle(16).Size.X+width-tab.Nodes[0].Value44);
        }
        //534CF0 opens the common pane14 list. Its fixed eleven groups
        //share this application font/artwork; Person cells remain current.
        Fixed(header, 2, 0x5D1, 56);
        var personGroups = OfficerInformationSources.Create((section,id) =>
            messages.Messages.Sections[section].Records[id].Text
                ?? throw new InvalidDataException("Missing Person information caption."));
        var personColumn = assets.SourceLayout("cmn_btn_clm");
        foreach (var group in personGroups)
        {
            var tab = Art(group.Index == 0 ? 0x172EDC24u
                : group.Index == personGroups.Length - 1 ? 0xD45873AAu : 0x9AFB27B7u, [3,5,8,11,13]);
            float width = tab.Rectangle(16).Size.X + group.Caption.Length * 28 + 36 - tab.Nodes[0].Value44;
            for (int style=8;style<=11;style++) Fixed(tab,16,group.CaptionMessageId,style,width);
            foreach (var column in group.Columns.Where(column=>column.Visible))
            {
                var profile = MapFontProfile.FromNode(personColumn.Nodes[13],personColumn.Source.Source.ResourceUri)
                    with { WidthFitMode=2,WidthReserve=0 };
                for (int style=3;style<=6;style++)
                    MapFont.PrepareText(font,column.Caption,profile.WithPreset(presets[style]),column.Width-14,caches);
            }
        }
        for (int group = 0; group < 5; group++)
        {
            var tabs = assets.SourceLayout(0x5C9D1E9E);
            var shape = assets.SourceLayout(ScenarioSettingsSourceLayout.TabLayout(group));
            float width = shape.Rectangle(16).Size.X + tabs.Rectangle(group + 2).Size.X - shape.Nodes[0].Value44;
            for (int state = 0; state < 5; state++)
                Fixed(shape, 16, 0x881 + group, 8 + Math.Min(state, 3), width);
        }

        void Fixed(UiSourceLayout source, int node, int caption, int preset, float? width = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MapFont.PrepareText(font, records[caption].Text
                ?? throw new InvalidDataException("Missing pre-game fixed caption."),
                MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri).WithPreset(presets[preset]),
                width ?? source.Rectangle(node).Size.X, caches);
        }
        UiSourceLayout Art(uint file, IEnumerable<int> indices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = assets.SourceLayout(file);
            int[] nodes = indices.ToArray();
            assets.PrepareLayoutArt(source.Source, nodes.Where(index => source.Nodes[index].Type is 1 or 3), commonImports: true);
            foreach (int index in nodes.Where(index => source.Nodes[index].Type == 6)) _ = source.Type6(index);
            return source;
        }
        void PortraitMasks(UiSourceLayout source, int node, int count)
        {
            var group = LcskSpriteNodeReader.Read(source.Source.Layout, source.Nodes[node]).Groups.Single();
            if (group.ComponentCount != count || group.Parts.Count != count)
                throw new InvalidDataException("Pre-game portrait mask differs from its current source consumer.");
            foreach (var part in group.Parts.Skip(1))
            {
                var sprite = assets.SourceLayoutSprite(source.Source, part.SpriteName);
                assets.PrepareSprite(sprite, MapPortraitQuad.UsesSrgbView(repository, assets, part.SpriteName, sprite));
            }
        }
    }

    private static void PrepareNewGameForceScenarioHeader(UiAssets assets, MapFontSource font,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, string year, string month, string title)
    {
        var source = assets.SourceLayout(0x6C522E54);
        var preset = MapTextPresets.Create()[27];
        string[] text = [year, month, title];
        for (int index = 0; index < text.Length; index++)
            MapFont.PrepareText(font, text[index],
                MapFontProfile.FromNode(source.Nodes[index + 6], source.Source.Source.ResourceUri).WithPreset(preset),
                source.Rectangle(index + 6).Size.X, caches);
    }
}
