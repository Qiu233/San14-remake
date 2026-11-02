using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    // Actual existing-army target-page consumers. No campaign, selection or
    // draft is retained; these sources belong to the application UI owner.
    private static void PrepareArmyOrderSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, LoadedMessages messages, string locale,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken cancellationToken)
    {
        var presets = MapTextPresets.Create();
        var data = messages.Messages.Sections;
        var menu = assets.SourceLayout(0xA7513A25);
        Art(menu, [1]);
        var wrapper = assets.SourceLayout(0xA7DA0617);
        Art(wrapper, [3]); // node4's authored placeholder is replaced by the action icon.
        var button = assets.SourceLayout(0x7BC42A66).ResizedSubtree(0, new(124, 48), true);
        Art(button, [3,5,6,8,11,13,14]);
        foreach (var (section, id) in new[] { (1,0x282),(1,0x395),(1,0x261),(1,0x265),(1,0x26B),(1,0x27D),
            (0,0x10B),(0,0x10C),(0,0x10D),(0,0x111),(0,0x113),(0,0x114) })
            Text(button, 16, Caption(section,id), [3,4,5,6], wrapper.Rectangle(2).Size.X);
        foreach (string name in new[] { "000_cmn_icon_bt_kidou", "000_cmn_icon_bt_nyujo",
            "000_cmn_icon_bt_battle", "000_cmn_icon_bt_build", "000_cmn_icon_bt_senryo", "000_cmn_icon_bt_kakei" })
            assets.PrepareCommonSprite(name);
        assets.PrepareCommonSprite("000_cmn_icon_st_gihou", "texture_pk_common");
        foreach (int ordinal in Enumerable.Range(1,5))
            Text(wrapper,5,ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),[13]);

        var footerButton = assets.SourceLayout(0xCEE6F358);
        Art(footerButton,[3,5,8,11,13]);
        foreach (int id in new[] { 0x35D,0x25A,0x262 }) Text(footerButton,16,Caption(1,id),[3,4,5,6,7]);
        var header = assets.SourceLayout(0xDC8AC8AA);
        foreach (int id in new[] { 0x33A,0x223 }) Text(header,2,Caption(1,id),[56]);
        var route = assets.SourceLayout(0xC46DC9B2);
        Art(route,[1]);
        foreach (int ordinal in Enumerable.Range(1,5))
            Text(route,3,ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture),[13]);
        Art(assets.SourceLayout(UnitSelectionBoxSource.Load(repository).Source.FileId),[1]);
        var tooltip = assets.SourceLayout(0xBE0DC488);
        Art(tooltip,[2]); _ = tooltip.Type6(1);

        var policies = assets.SourceLayout(0x96B33F19);
        Art(policies,[2,3,8,17,26,35]); _ = policies.Type6(1);
        foreach (var (node,id) in new[] { (4,0x278),(9,0x279),(18,0x25A),(27,0x27B),(36,0x27C),
            (9,0x283),(18,0x284) }) Text(policies,node,Caption(1,id),[13]);
        // Both ordinary and transport panels replace these mounts using the
        // same integer size expression as their actual button producers.
        for (int group=0;group<4;group++)
        {
            PolicyButton(10+9*group,0xA97E37BE,group switch {0=>[0x27D,0x25A],1=>[0x27F,0x265],2=>[0x27F],_=>[0x25A]});
            PolicyButton(12+9*group,0x01649C44,group switch {0=>[0x27E,0x282],1=>[0x280,0x285],2=>[0x280],_=>[0x282]});
        }
        PolicyButton(5,0x60C991A4,[0x367]);
        assets.PrepareCommonSprite("000_cmn_icon_lock");

        var confirmation = assets.SourceLayout(0xDB48324F);
        Art(confirmation,[1,3]); _ = confirmation.Type6(2);
        var round = assets.SourceLayout(0xAD4A3EFA);
        Art(round,[3,5,8,12,14]);
        _ = assets.PrepareCommonCaption(locale,"000_cmn_txt_decision_yes");
        _ = assets.PrepareCommonCaption(locale,"000_cmn_txt_decision_no");
        var game = repository.LoadMessages(locale,MessageKind.Game).Messages;
        var bodyProfile = MapFontProfile.FromNode(confirmation.Nodes[4],confirmation.Source.Source.ResourceUri) with
            { WidthFitMode=3, RawCharacterMode=2 };
        var lineProfile = bodyProfile with { RawAlign=(byte)(bodyProfile.RawAlign&0xF0),VerticalSpace=0 };
        foreach (int id in new[] { 0x8A8,0x8A9,0x8AA })
            foreach (string line in game.Sections[2].Records[id].Literals.Single().Text.Split('\n'))
                MapFont.PrepareText(font,line,lineProfile,confirmation.Rectangle(4).Size.X,caches);
        cancellationToken.ThrowIfCancellationRequested();

        string Caption(int section,int id) => data[section].Records[id].Text
            ?? throw new InvalidDataException("Missing army target-page caption.");
        void Art(UiSourceLayout source,int[] nodes)
        { cancellationToken.ThrowIfCancellationRequested(); assets.PrepareLayoutArt(source.Source,nodes,commonImports:true); }
        void Text(UiSourceLayout source,int node,string caption,int[] styles,float? width=null)
        {
            var profile=MapFontProfile.FromNode(source.Nodes[node],source.Source.Source.ResourceUri);
            foreach (int style in styles)
                MapFont.PrepareText(font,caption,profile.WithPreset(presets[style]),width??source.Rectangle(node).Size.X,caches);
        }
        void PolicyButton(int mount,uint file,int[] captions)
        {
            var node=policies.Nodes[mount];
            Vector2 size=new((int)(node.Value44*node.HeaderValues[7]),(int)(node.Value46*node.HeaderValues[8]));
            var source=assets.SourceLayout(file).ResizedSubtree(0,size,true);
            Art(source,[3,5,6,8,9,11,13,14]); _=assets.ButtonStateProfile(source.Source);
            int start=file==0x60C991A4?28:3;
            foreach (int id in captions) Text(source,16,Caption(1,id),Enumerable.Range(start,5).ToArray());
        }
    }
}
