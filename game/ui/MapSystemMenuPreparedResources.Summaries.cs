using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    // These are the actual summary publishers' replaceable mounts, not an
    // instruction to preload their authored dummy characters or item pictures.
    // Only the remaining artwork and the actual override branches below are
    // static dependencies. Current people, flags and quantities stay local.
    private static void PrepareSummarySources(GameResourceRepository repository, UiAssets assets, CancellationToken cancellationToken)
    {
        Template("00_82_12_sum_chikei_offset_00", [4]);
        Template("00_82_03_01_sum_butai_btl", [10,19,31,36,39,45,53], [40]);
        Template("00_82_03_00_sum_butai_trs", [8,17,29,34,35,40,45]);
        Template("00_82_03_02_sum_butai_kosei", [3]);
        Template("00_82_04_sum_area", [152,7,148,11,21,31,51,56,61,66,140,43], [44]);
        Template("00_82_05_sum_city", [25,9,17,21,13,36,41,46,51,58,59,66,77,153,84,
            98,108,118,94,138,143,148,130], [131]);
        Template("00_82_06_sum_seki", [65,8,25,29,34,12,17,18,19,40,51,135,58,
            80,90,100,76,120,125,130,112], [113]);
        Template("00_82_07_sum_architect", [7,14]);
        Template("00_82_07_sum_buil_offset_00", [4,5,6]);
        Template("00_82_08_sum_force", [6,7,10,11], [1,2,3]);
        Template("00_82_08_sum_force_offset_00_cn", [7,61,62], [0x41,0x49,0x54]);
        // The named radial gauge really consumes these same two panes.
        var force = assets.SourceLayout("00_82_08_sum_force_offset_00_cn");
        foreach (int pane in new[] { 61,62 })
        {
            var parts = LcskSpriteNodeReader.Read(force.Source.Layout,force.Nodes[pane]).Groups.Single().Parts;
            // UiSourceRadialGauge consumes two masks from the same actual
            // sprite. Do not send this composition to ordinary one-part Art.
            assets.PrepareSrgbLayoutSprite(force.Source,parts[0].SpriteName);
        }
        Template("00_82_09_sum_gundan", [12,7,13,17,22,28,32,36]);
        Template("00_82_10_sum_shu", [58,65,71,10,19,28,37,46,55,61,9]);
        Template("00_82_11_sum_gunzei", [103,12,29,24,36,8,44,58,72,86,100]);
        Template("00_82_11_sum_gunzei_offset_00", [4,5,6,11]);
        Template("00_82_03_sum_butai_offset_01", [1,4]);
        Template("00_82_05_sum_city_offset_00", [7]);
        Template("00_82_05_sum_city_offset_01", [2]);
        Template("00_82_05_sum_city_offset_02", []);
        Template("00_82_05_sum_city_offset_03", [2]);
        Template("00_82_05_sum_city_offset_03_pk", [2]);
        Template("00_82_05_sum_city_offset_05", [2,7,8], [3,5,10]);
        // The Army status branch uses the authored five icons, while the
        // disaster branch replaces three with the explicit common sprites.
        Template("00_82_05_sum_city_offset_06_pk", []);
        Template("00_82_05_sum_city_offset_07", [3,8,16,24,32,40,48,56,64], [4]);
        Template("00_82_05_sum_city_offset_08", [6], [7]);
        Template("00_82_05_sum_city_offset_09", [2,4], [3]);
        Template("00_82_10_sum_shu_offset_00", []);
        Template("00_82_10_sum_shu_offset_01", [4]);
        Template("00_82_10_sum_shu_offset_02", [4,7]);
        Template("00_82_10_sum_shu_offset_03", []);
        Template("00_82_04_sum_area_mes_p", []);
        Template("00_82_04_sum_area_mes_e", []);
        Template("03_81_08_tinori_discovery_en", [2,9]);
        Template("03_81_08_tinori_offset_01", [9,17,25,33,41]);
        Template(SceneryMapSummarySources.LayoutName, [14,34]);
        Template("cmn_chart_s", []);
        var bar = assets.SourceLayout("cmn_bar");
        assets.PrepareLayoutArt(bar.Source, [1,2], commonImports: true);
        _ = assets.SourceLayout(0xD493EA98); // Actual resized trait marker; its art is rebound below.
        var flag = assets.SourceLayout(0xA564F68D);
        assets.PrepareLayoutArt(flag.Source, [1], commonImports: true);

        PortraitMasks("00_82_03_sum_butai_offset_01", [1]);
        PortraitMasks("00_82_11_sum_gunzei_offset_00", [4]);
        PortraitMasks("00_82_08_sum_force", [6,10]);
        PortraitMasks("00_82_09_sum_gundan", [12]);
        PortraitMasks("00_82_10_sum_shu", [9]);
        PortraitMasks("00_82_10_sum_shu_offset_02", [4]);

        foreach (string name in new[]
        {
            "000_cmn_icon_kane_fuchi", "000_cmn_icon_hyourou_fuchi", "000_cmn_icon_heishi_fuchi",
            "000_cmn_icon_genek_fuchi", "000_cmn_icon_siki_fuchi", "000_cmn_icon_taikyu_fuchi",
            "cmn_mrk_max", "000_cmn_icon_disaster_ekibyou", "000_cmn_icon_disaster_inago", "000_cmn_icon_disaster_housaku",
            "000_cmn_base_indivi_brank", "000_cmn_base_indivi_buff", "000_cmn_base_indivi_buff_off",
            "000_cmn_base_indivi_debuff", "000_cmn_base_indivi_debuff_off", "000_cmn_base_indivi_unique",
            "000_cmn_base_indivi_unique_off", "000_cmn_base_indivi_buffdebuff"
        }) assets.PrepareCommonSprite(name);
        foreach (string name in Enumerable.Range(-1,7).Select(index => MapSummaryIndicatorRules.Describe(index).SpriteName)
            .Distinct(StringComparer.Ordinal)) assets.PrepareCommonSprite(name);
        var icons = ArmyMapUiSources.Create();
        foreach (int id in new[] { 0x169,0x16A,0x16B,0x16C,0x16D,0x16E,0x16F,0x170,0x171,0x172,0x3E9,0x231,0x232,0x233,0x234,0x235,0x236,0x237,0x238,0x221,0x222,0x21F,0x21C })
            assets.PrepareCommonSprite(icons.IconName(new(id,0)),id==0x3E9?"texture_pk_common":null);
        // The actual selected formation domain is the nonzero slots of the
        // 21-row fixed catalog. Class 7E only changes the palette, not the
        // selected sprite. Include naval/siege and reserved fallback slots.
        foreach (var icon in Enumerable.Range(1,20).Select(index =>
            ArmyMapUiSources.FormationIcon((byte)index,0)).Distinct())
            assets.PrepareCommonSprite(icons.IconName(icon),
                icon.SpriteId == 0x3F0 ? "texture_pk_common" : null);
        foreach (int kind in new[] { 3,4,5,6,7,8,11,12,13,14,15,17 })
        {
            var plain = FacilitiesInfoSource.IconNames(kind,false);
            var outlined = FacilitiesInfoSource.IconNames(kind,true);
            foreach (string name in new[] { plain.Fill,plain.Outline,outlined.Outline }.Distinct(StringComparer.Ordinal))
                assets.PrepareCommonSprite(name,kind is 15 or 17 ? "texture_pk_common" : null);
        }
        assets.PrepareCommonSprite("000_cmn_icon_gadvantage_fuchi","texture_pk_common");
        cancellationToken.ThrowIfCancellationRequested();

        void Template(string name, int[] replacements, int[]? alwaysHidden = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = assets.SourceLayout(name);
            var excluded = replacements.Concat(alwaysHidden ?? []).ToHashSet();
            bool Used(int index)
            {
                for (int? current=index; current is int node; current=source.Nodes[node].ParentIndex)
                    if (excluded.Contains(node)) return false;
                return true;
            }
            assets.PrepareLayoutArt(source.Source, source.Nodes.Where(node => (node.Type is 1 or 3) && Used(node.Index))
                .Select(node => node.Index), commonImports: true);
            foreach (var node in source.Nodes.Where(node => node.Type==6 && Used(node.Index)))
                _ = source.Type6(node.Index);
        }

        void PortraitMasks(string name, int[] panes)
        {
            var source = assets.SourceLayout(name);
            foreach (int pane in panes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var group = LcskSpriteNodeReader.Read(source.Source.Layout,source.Nodes[pane]).Groups.Single();
                if (group.ComponentCount is not (2 or 3) || group.Parts.Count!=group.ComponentCount)
                    throw new InvalidDataException("Summary portrait requires its actual two/three components.");
                // Component zero is a current person, not the dummy authored in
                // this template. The real face masks have no such identity.
                foreach (var part in group.Parts.Skip(1))
                {
                    var mask = assets.SourceLayoutSprite(source.Source,part.SpriteName);
                    assets.PrepareSprite(mask,MapPortraitQuad.UsesSrgbView(repository,assets,part.SpriteName,mask));
                }
            }
        }
    }
}
