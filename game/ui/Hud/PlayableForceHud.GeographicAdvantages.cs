using Godot;
using San14.Data;

public partial class PlayableForceHud
{
    private UiSourceFunctionCheckButton? geographicButton;
    internal void ConfigureGeographicAdvantages(Action toggle,Func<bool> admitted)
    {
        if(geographicButton is not null){geographicButton.QueueFree();geographicButton=null;}
        var main=new UiSourceLayout(_repository,0x036BB544,
            new Dictionary<int,UiSourceNodeGeometry>{[83]=new(Origin:new Vector2(960,0))});
        // CMainTopBtnGroupLayout loads the PK group. In the base group
        // node61 is Null_cs, not the geographical-advantages mount.
        var top=new UiSourceLayout(MapMarkerLayout.LoadNamed(_repository,"00_02_00_mein_offset_60_pk"));
        geographicButton=new(){Name="MapGeographicAdvantagesButton",Position=main.Rectangle(84).Position+top.Rectangle(61).Position,
            InputAdmission=admitted};
        AddChild(geographicButton);
        geographicButton.Configure(_repository,_assets!,_spriteShader!,"000_cmn_icon_gadvantage",toggle);
        geographicButton.SetMeta("source_group_node",61);
        geographicButton.SetMeta("source_tutorial_registry_key",75);
    }
    internal void SetGeographicAdvantagesSelected(bool selected)
    {if(geographicButton is not null)geographicButton.Selected=selected;}
}
