using Godot;
using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    // Only static UI definitions are prepared here. The normal title entry
    // still projects the latest application-profile admission into its catalog.
    private static void PrepareTitleOfficerSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, LoadedMessages messages, string locale,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken cancellationToken)
    {
        var catalog = repository.LoadCollectionOfficerCatalog(locale);
        var presets = MapTextPresets.Create();
        var header = assets.SourceLayout(0xDC8AC8AA);
        Text(header, 2, catalog.Title, 56);
        foreach (var column in catalog.Tabs.SelectMany(tab => tab.Columns)
            .Where(column => column.Width > 0).DistinctBy(column => (column.Caption, column.Width)))
        {
            var source = assets.SourceLayout("cmn_btn_clm");
            var profile = MapFontProfile.FromNode(source.Nodes[13], source.Source.Source.ResourceUri)
                with { WidthFitMode = 2, WidthReserve = 0 };
            for (int state = 0; state < 4; state++)
                MapFont.PrepareText(font, column.Caption, profile.WithPreset(presets[3 + state]), column.Width - 14, caches);
        }
        foreach (string caption in catalog.Tabs.Select(tab => tab.Caption).Append(Caption(0x297)).Distinct())
            foreach (uint file in new uint[] { 0x172EDC24, 0x9AFB27B7, 0xD45873AA })
            {
                var tab = assets.SourceLayout(file);
                float width = tab.Rectangle(16).Size.X + caption.Length * 28 + 36 - tab.Nodes[0].Value44;
                for (int state = 0; state < 4; state++)
                    Text(tab, 16, caption, 8 + state, width);
            }

        // These roots are supplied by the typed body publication. Their
        // placeholder characters, charts and markers are never loaded as art.
        PrepareBodies();
        foreach (int id in new[] { 0x70, 0x6F, 0x73, 0xC4 })
            Command(Caption(id), new(Math.Max(96, Caption(id).Length * 28 + 20), 48));
        var filter = assets.SourceLayout(0x2714E51B);
        Art(filter, filter.Nodes.Where(node => node.Index < 7 && node.Type is 1 or 3).Select(node => node.Index));
        foreach (var node in filter.Nodes.Where(node => node.Index < 7 && node.Type == 6)) _ = filter.Type6(node.Index);
        Text(filter, 19, Caption(0x70));
        int[] groupCaptions = [0x71, 0x72, 0x65];
        for (int group = 0; group < groupCaptions.Length; group++)
            Command(Caption(groupCaptions[group]), filter.Rectangle(7 + group * 4).Size);
        _ = assets.PrepareCommonCaption(locale, "000_cmn_txt_decision_kettei");

        _ = assets.SourceLayout(0xB7B5EAA0);
        var frame = assets.SourceLayout("cmn_face_frame"); Art(frame, [1]);
        var pager = assets.SourceLayout(0x6148B858);
        Art(pager, pager.Nodes.Where(node => node.Type is 1 or 3).Select(node => node.Index));
        Text(pager, 3, "/");
        foreach (int node in new[] { 2, 4 }) foreach (string number in new[] { "1", "2" }) Text(pager, node, number);
        var chart = assets.SourceLayout(0x27119CFA); Art(chart, [1, 4]);
        for (int axis = 0; axis < 5; axis++) Text(chart, 5 + axis * 2, Caption(0x238 + axis), 13);
        _ = assets.SourceLayout(0xD493EA98);
        _ = assets.SourceLayout(0x270E3D1B);
        foreach (string name in new[] { "000_cmn_mrk_base_yellow", "000_cmn_mrk_base_disable" }) assets.PrepareCommonSprite(name);
        var iconNames = ArmyMapUiSources.Create();
        foreach (int id in new[] { 0x1A2, 0x1A8, 0x1A9, 0x1AA, 0x1AC, 0x1AD, 0x1AB, 0x1AE, 0x1AF, 0x1A3 })
            assets.PrepareCommonSprite(iconNames.IconName(new(id, 0)));

        var balloon = assets.SourceLayout("11_00_01_offset_05");
        _ = balloon.Type6(1); Art(balloon, [2, 5, 6, 10, 11, 14]);
        Text(balloon, 7, Caption(0x4B4), 13); Text(balloon, 12, Caption(0x6C5), 13);
        foreach (int id in new[] { 0x4B1, 0x4B2 }) Text(balloon, 3, Caption(id), 0);
        foreach (int id in new[] { 0x338, 0x339 }) Text(balloon, 8, Caption(id), 0);
        for (int opcode = 1; opcode <= 23; opcode++)
        {
            string caption = messages.Messages.Sections[0].Records[0x151 + opcode].Text
                ?? throw new InvalidDataException("Missing collection tactic effect caption.");
            bool beneficial = opcode is >= 5 and <= 9 or 15 or >= 19 and <= 21;
            // The two physical slots retain their original node identities.
            foreach (int node in new[] { 16, 18 }) Text(balloon, node, caption, beneficial ? 15 : 16);
        }
        foreach (string name in new[] { "000_cmn_icon_st_damage", "000_cmn_icon_st_konran", "000_cmn_icon_st_asidome",
            "000_cmn_icon_st_chohatu", "000_cmn_icon_bt_kidou", "000_cmn_icon_bt_kougun", "000_cmn_icon_bt_koujyou",
            "000_cmn_icon_bt_bougyo", "000_cmn_icon_bt_hajyou", "000_cmn_icon_siki", "000_cmn_icon_st_firing",
            "000_cmn_icon_taikyu_damage", "000_cmn_icon_recovery", "000_cmn_icon_heishi_recovery",
            "000_cmn_icon_all_abilities", "000_cmn_icon_st_grant" }) assets.PrepareCommonSprite(name);

        void PrepareBodies()
        {
            // Union of typed replacement mounts and unconditional hidden
            // roots in CollectionOfficerDetails.Basic/Profile. Conditional
            // year labels remain prepared for every eligible person's page.
            Template("02_07_01_busho1",
                [2,4,8,17,22,23,25,26,27,29,30,32,33,35,36,37,50,51,52,53,54,65,
                 72,73,74,75,76,77,78,79,80,81,85,86,87,88,89,90,91,92,93,94,99,105],
                [(14,0x55F),(59,0x244),(64,0x241),(66,0x386),(71,0x234),(84,0x22B),(98,0x411)]);
            Template("02_07_01_busho2", [2,4,17,24,31,39,48,54,61],
                [(14,0x55F),(28,0x42C),(35,0x42D),(43,0x42E),(58,0x2BF),(69,0x2BC),(83,0x2BD),(97,0x297)]);
        }
        void Template(string name, int[] omitted, (int Node, int Message)[] captions)
        {
            var source = assets.SourceLayout(name);
            bool Used(int index)
            {
                for (int? at = index; at is int node; at = source.Nodes[node].ParentIndex)
                    if (omitted.Contains(node)) return false;
                return true;
            }
            Art(source, source.Nodes.Where(node => (node.Type is 1 or 3) && Used(node.Index)).Select(node => node.Index));
            foreach (var node in source.Nodes.Where(node => node.Type == 6 && Used(node.Index))) _ = source.Type6(node.Index);
            foreach (var (node, id) in captions) Text(source, node, Caption(id), 13);
        }
        string Caption(int id) => messages.Messages.Sections[1].Records[id].Text
            ?? throw new InvalidDataException("Missing collection officer caption.");
        void Art(UiSourceLayout source, IEnumerable<int> nodes)
        { cancellationToken.ThrowIfCancellationRequested(); assets.PrepareLayoutArt(source.Source, nodes, commonImports: true); }
        void Text(UiSourceLayout source, int node, string value, int? preset = null, float? width = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri);
            MapFont.PrepareText(font, value, preset is int index ? profile.WithPreset(presets[index]) : profile,
                width ?? source.Rectangle(node).Size.X, caches);
        }
        void Command(string caption, Vector2 size)
        {
            var button = assets.CommandButtonLayout(size);
            Art(button, [3, 5, 6, 8, 9, 11, 13, 14]); _ = assets.ButtonStateProfile(button.Source);
            for (int state = 0; state < 5; state++) Text(button, 16, caption, 28 + state);
        }
    }
}
