using Godot;
using San14.Data;

public partial class PlayableForceHud
{
    private UiSourceFunctionCheckButton? informationLayerButton;
    private MapInformationToolbarPopup? informationLayerPopup;
    private readonly UiSourceInformationButton[] informationLayerChoices=new UiSourceInformationButton[6];

    internal void ConfigureMerchant(Action open,Func<bool> admitted)
    {
        var (main,top)=InformationToolbarMounts();
        var button=new UiSourceFunctionButton{Name="MapMerchantButton",
            Position=main.Rectangle(84).Position+top.Rectangle(68).Position,InputAdmission=admitted};
        AddChild(button);
        button.Configure(_repository,_assets!,_spriteShader!,"000_cmn_icon_shounin",open,expansionCommonSprite:true);
        button.SetMeta("source_group_layout",top.Source.Source.ResourceUri);
        button.SetMeta("source_group_node",68);
    }

    internal void ConfigureInformationLayers(Action<int> toggle,Func<bool> admitted,uint selected)
    {
        var (main,top)=InformationToolbarMounts();
        informationLayerPopup=new(){Name="MapInformationLayerPopup",Visible=false,ZIndex=1,
            Position=main.Rectangle(84).Position+InformationLayerPopupOpenMount(top),InputAdmission=admitted};
        AddChild(informationLayerPopup);
        var popupSource=_assets!.SourceLayout("00_02_00_mein_offset_61");
        informationLayerPopup.Size=popupSource.Rectangle(0).Size;
        popupSource.Art(informationLayerPopup,1,popupSource.Rectangle(1),_assets,_spriteShader!);
        var buttonSource=_assets.SourceLayout("cmn_btn_fnc_check_sub");
        var ordinary=new UiSourceButtonStateProfile(buttonSource.Source,UiSourceInformationButton.Actions);
        var selectedPose=new UiSourceButtonStateProfile(buttonSource.Source,
            ["btn_5(push_forcus)","btn_1","btn_2","btn_3","btn4"]);
        int[] types=[5,0,1,2,3,4];
        int[] mounts=[2,6,10,14,18,22];
        for(int index=0;index<types.Length;index++)
        {
            int type=types[index],action=17+index;
            var button=new UiSourceInformationButton{Name="MapInformationLayer"+action,
                Position=popupSource.Rectangle(mounts[index]).Position,InputAdmission=admitted};
            informationLayerPopup.AddChild(button);informationLayerChoices[index]=button;
            button.Configure(buttonSource,_assets,_spriteShader!,type,ordinary,selectedPose,()=>toggle(action));
        }
        informationLayerButton=new(){Name="MapInformationLayerButton",InputAdmission=admitted,
            Position=main.Rectangle(84).Position+top.Rectangle(54).Position};
        AddChild(informationLayerButton);
        informationLayerButton.Configure(_repository,_assets,_spriteShader!,"000_cmn_icon_view",
            ()=>informationLayerPopup.Visible=!informationLayerPopup.Visible, expansionCommonSprite:false);
        informationLayerButton.SetMeta("source_group_layout",top.Source.Source.ResourceUri);
        informationLayerButton.SetMeta("source_group_node",54);
        SetInformationLayersSelected(selected);
    }

    internal void SetInformationLayersSelected(uint selected)
    {
        for(int type=0;type<informationLayerChoices.Length;type++)
            if(informationLayerChoices[type] is {} button)button.Selected=(selected&(1u<<type))!=0;
        if(informationLayerButton is not null)informationLayerButton.Selected=selected!=0;
    }

    private static Vector2 InformationLayerPopupOpenMount(UiSourceLayout top)
    {
        // The authored mount is the hidden view_out endpoint. The original
        // top group opens node2 through view_in before holding view_loop.
        var opening=LcskAnimationReader.Read(top.Source.Layout,new HashSet<string>{"view_in"})
            .Single(clip=>clip.Name=="view_in");
        var vertical=opening.Tracks.Single(track=>track.NodeId==2&&track.Channel==1);
        return top.Rectangle(2).Position+new Vector2(0,
            top.Nodes[2].HeaderValues[1]-(float)vertical.Curve.Sample(opening.EndFrame));
    }

    private (UiSourceLayout Main,UiSourceLayout Top) InformationToolbarMounts()=>
        (new(_assets!.SourceLayout(0x036BB544).Source,
            new Dictionary<int,UiSourceNodeGeometry>{[83]=new(Origin:new Vector2(960,0))}),
         _assets.SourceLayout("00_02_00_mein_offset_60_pk"));
}

/// <summary>The popup owns its full panel's input, including gaps between
/// buttons. A command page suspending map input also closes this transient UI.</summary>
internal partial class MapInformationToolbarPopup : Control
{
    internal Func<bool> InputAdmission { get; set; }=null!;
    public MapInformationToolbarPopup()
    {
        MouseFilter=MouseFilterEnum.Stop;
        VisibilityChanged+=()=>SetProcess(Visible);
    }
    public override void _Process(double delta)
    {if(!InputAdmission())Hide();}
    public override void _UnhandledInput(InputEvent input)
    {
        if(!Visible||!InputAdmission())return;
        if(input is InputEventKey{Pressed:true,Keycode:Key.Escape} or
            InputEventMouseButton{Pressed:true,ButtonIndex:MouseButton.Right})
        {Hide();GetViewport().SetInputAsHandled();}
    }
}
