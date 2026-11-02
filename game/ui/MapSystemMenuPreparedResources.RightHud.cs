using San14.Data;
internal sealed partial class MapSystemMenuPreparedResources
{
    private static void PrepareRightHudSources(UiAssets assets,CancellationToken cancellationToken)
    {
        foreach(string name in new[]{"cmn_btn_tab_s_l","cmn_btn_tab_s_r","00_82_02_jump_offset_15","00_82_02_jump_offset_20","00_82_02_jump_offset_21"})
        {
            cancellationToken.ThrowIfCancellationRequested();var source=assets.SourceLayout(name);
            assets.PrepareLayoutArt(source.Source,source.Nodes.Where(n=>n.Type is 1 or 3&&n.Name!="dummy_face").Select(n=>n.Index));
            if(name.StartsWith("cmn_btn_tab")||name.EndsWith("_15"))_=new UiSourceButtonStateProfile(source.Source,MapRightHudButton.ActionNames(name.StartsWith("cmn_btn_tab")));
        }
        // Both runtime sprite-selector branches belong to the same startup
        // resource owner as the fold layout; never cold-load on collapse.
        assets.PrepareCommonSprite(MapRightHudButton.FoldIcon(false));
        assets.PrepareCommonSprite(MapRightHudButton.FoldIcon(true));
        var jump=assets.SourceLayout(0xA2D0A6F1);
        assets.PrepareLayoutArt(jump.Source,[7,28]);_=jump.Type6(8);
    }
}
