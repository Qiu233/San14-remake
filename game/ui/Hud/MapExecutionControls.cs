using Godot;
using San14.Data;

/// <summary>The original hover speed strip, sharing the resident advance button.</summary>
internal partial class MapExecutionControls : Control
{
    private readonly UiSourceIconRadioButton[] speeds=new UiSourceIconRadioButton[3];
    private MapAdvanceButton advance=null!;
    private bool retired;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,
        MapAdvanceButton advance,Action<int> changeSpeed,int selected)
    {
        this.advance=advance;Name="MapExecutionSpeeds";Visible=false;MouseFilter=MouseFilterEnum.Stop;
        var source=new UiSourceLayout(repository,0x40F993CE);
        // The source window is wider than its designer root. Preserve the third
        // radio and the real hover region instead of clipping to root160.
        Size=source.Rectangle(1).Size;
        source.Art(this,1,source.Rectangle(1),assets,shader);
        for(int i=0;i<speeds.Length;i++)
        {
            int index=i;
            var button=new UiSourceIconRadioButton {Name="MapExecutionSpeed"+i,
                Position=source.Rectangle(2+i*2).Position};
            AddChild(button);button.Configure(repository,assets,shader,index,()=>changeSpeed(index));
            button.MouseEntered+=Reveal;button.MouseExited+=QueueHide;speeds[i]=button;
        }
        advance.MouseEntered+=Reveal;advance.MouseExited+=QueueHide;
        MouseEntered+=Reveal;MouseExited+=QueueHide;
        ApplySpeed(selected);SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void ApplySpeed(int selected)
    {
        if((uint)selected>2)throw new ArgumentOutOfRangeException(nameof(selected));
        for(int i=0;i<speeds.Length;i++)speeds[i].Selected=i==selected;
        SetMeta("selected_speed",selected);
    }
    private void Reveal(){if(!retired)Show();}
    private void QueueHide()=>Callable.From(HideOutsideControls).CallDeferred();
    private void HideOutsideControls()
    {
        if(retired||!IsInsideTree())return;
        // Follow this viewport's actual GUI input owner, including a radio
        // child. The OS pointer can belong to another viewport or window.
        var hovered=GetViewport().GuiGetHoveredControl();
        if(hovered is not null&&(hovered==this||IsAncestorOf(hovered)||
            hovered==advance||advance.IsAncestorOf(hovered)))return;
        Hide();
    }
    public override void _ExitTree()
    {
        retired=true;
        if(GodotObject.IsInstanceValid(advance))
        {advance.MouseEntered-=Reveal;advance.MouseExited-=QueueHide;}
    }
}
