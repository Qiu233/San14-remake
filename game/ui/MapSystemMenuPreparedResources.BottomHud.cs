using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    private static void PrepareBottomHudSources(GameResourceRepository repository,UiAssets assets,CancellationToken cancellationToken)
    {
        foreach(string name in new[]{"00_02_00_mein_offset_44","00_02_00_mein_offset_45","cmn_btn_fnc","cmn_btn_fnc_check","cmn_btn_hanyou_s_check"})
        {
            var source=assets.SourceLayout(name);
            assets.PrepareLayoutArt(source.Source,source.Nodes.Where(n=>n.Type is 1 or 3).Select(n=>n.Index),commonImports:true);
            string[]? actions=name switch
            {
                "cmn_btn_fnc"=>["btn_0","btn_1","btn_2","btn_3","btn_4"],
                "cmn_btn_fnc_check"=>["0_normal","1_focus","2_push ","3_disable","4_disable_focus"],
                "cmn_btn_hanyou_s_check"=>["0_normal","1_focus ","2_push","3_disable","4_disable_focus"],
                _=>null
            };
            _=actions is null?assets.ButtonStateProfile(source.Source):new UiSourceButtonStateProfile(source.Source,actions);
        }
        var advice=assets.SourceLayout("00_02_00_mein_offset_50");assets.PrepareLayoutArt(advice.Source,[2],commonImports:true);
        _=assets.SourceLayout("00_02_00_mein_offset_40");
        var log=assets.SourceLayout("00_02_00_mein_offset_41");assets.PrepareLayoutArt(log.Source,[5,8,12],commonImports:true);
        var filter=assets.SourceLayout("00_02_00_mein_offset_43");assets.PrepareLayoutArt(filter.Source,[2],commonImports:true);_=filter.Type6(1);
        foreach(uint face in new uint[]{0xB20B608C,0xA9487A40})
        {
            var source=assets.SourceLayout(face);int pane=face==0xB20B608C?3:1;
            var parts=LcskSpriteNodeReader.Read(source.Source.Layout,source.Nodes[pane]).Groups.Single().Parts;
            foreach(var part in parts.Skip(1))
            {
                var sprite=assets.SourceLayoutSprite(source.Source,part.SpriteName);
                assets.PrepareSprite(sprite,MapPortraitQuad.UsesSrgbView(repository,assets,part.SpriteName,sprite));
            }
        }
        foreach(string icon in new[]{"000_cmn_icon_jogen","000_cmn_icon_log_add","000_cmn_icon_log_filter"})assets.PrepareCommonSprite(icon);
        assets.PrepareCommonSprite("000_cmn_icon_jogen_run","texture_pk_common");
        cancellationToken.ThrowIfCancellationRequested();
    }
}
