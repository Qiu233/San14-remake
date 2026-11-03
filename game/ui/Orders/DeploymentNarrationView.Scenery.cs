using Godot;
using San14.Data;

internal partial class DeploymentNarrationView
{
    internal void ConfigureSceneryDiscovery(GameResourceRepository repository,PreviewText text,
        TalentSceneryDiscoveryPanel panel,string expanded,IReadOnlyList<LoadedSprite> standingLayers,
        Action consumed,Func<AutomaticMessageAdvanceMode>? automaticAdvance=null,Func<bool>? admitted=null,
        AdministrationPageChrome.Resources? resources=null)
    {
        //The discovery state places its executor in slot3, beside the right
        //detail pane. It shares the original EventView message and input wait.
        ConfigureEvent(repository,text,panel.Executor.Name,expanded,[(6,standingLayers)],3,
            consumed,automaticAdvance,admitted,resources:resources);
        var source=assets!.SourceLayout(0x04C84E57);
        Vector2 mount=new(0,source.Nodes[0].Value46/3);
        source.Art(canvas,1,source.Rectangle(1,mount),assets!,shader!);
        source.Art(canvas,3,source.Rectangle(3,mount),assets!,shader!,
            commonSpriteOverride:Known(panel.ImageSpriteName),commonTexturePack:Known(panel.ImageResourceName));
        foreach(int node in new[]{4,5,6})
            source.Art(canvas,node,source.Rectangle(node,mount),assets!,shader!);
        source.Text(canvas,7,mount,panel.Name,font!);
        source.Text(canvas,8,mount,panel.LevelCaption,font!);
        source.Text(canvas,9,mount,Known(panel.LevelText),font!);
        source.Text(canvas,10,mount,panel.BonusCaption,font!);
        if(Known(panel.BonusIcon) is int icon)
            source.Art(canvas,11,source.Rectangle(11,mount),assets!,shader!,commonSpriteOverride:IconName(icon));
        source.Text(canvas,12,mount,Known(panel.BonusText),font!,
            preset:Known(panel.BonusEnabled)?null:MapTextPresets.Create()[14]);
        SetMeta("scenery_panel_layout",source.Source.Source.ResourceUri);
        SetMeta("scenery_id",panel.SceneryId);
    }

    private static T Known<T>(AreaAdministrationQuery<T> query)=>query.Available?query.Value!:
        throw new InvalidDataException(query.UnresolvedInput??"Scenery discovery input is unavailable.");

    private static string IconName(int sourceIcon)=>sourceIcon switch
    {
        0x1C1=>"000_cmn_icon_kane_fuchi",
        0x1A0=>"000_cmn_icon_hyourou_fuchi",
        0x19C=>"000_cmn_icon_heishi_fuchi",
        _=>throw new InvalidDataException("Unknown scenery effect icon.")
    };
}
