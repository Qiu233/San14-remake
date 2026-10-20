using Godot;
using San14.Data;

internal sealed record UiSourcePulldownItem(string Caption,bool Enabled,string? Hint=null);
internal enum UiSourcePulldownOutsideClick { PressPassthrough, ConfirmConsumed }

/// <summary>The two callers of CCmnPulldownMenu share rows and ownership,
/// but pass their actual outer rectangle and outside-click policy.</summary>
internal partial class UiSourcePulldownMenu : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private readonly List<UiSourcePulldownButton> buttons=[];
    private Action<int?>? selected;
    private Action<Control,string?>? hint;
    private Control? hintOwner;
    private bool abandoned;
    private Vector2I? rightStart,leftStart;
    private UiSourcePulldownOutsideClick outsideClick;
    protected bool IsAbandoned=>abandoned;

    internal void Configure(GameResourceRepository repository,string locale,Rect2 rectangle,
        IReadOnlyList<UiSourcePulldownItem> items,UiSourcePulldownOutsideClick outsideClick,
        Action<int?> selected,Action<Control,string?>? hintChanged=null)
    {
        Position=rectangle.Position;Size=rectangle.Size;MouseFilter=MouseFilterEnum.Stop;
        this.outsideClick=outsideClick;this.selected=selected;hint=hintChanged;
        assets=new(repository);font=new(repository,locale);shader=UiSourceSprite.CreateShader();
        for(int index=0;index<items.Count;index++)
        {
            int action=index;var item=items[index];
            uint layout=index==0?0xF0E45A19u:index==items.Count-1?0xD28668D7u:0xE1B56178u;
            var button=new UiSourcePulldownButton{Name="MenuItem"+index,Position=new(0,38*index),Size=new(rectangle.Size.X,40),Disabled=!item.Enabled};
            AddChild(button);buttons.Add(button);
            button.Configure(repository,assets,shader,font,layout,item.Caption,()=>Publish(action));
            button.MouseEntered+=()=>{if(!abandoned){hintOwner=button;hint?.Invoke(button,item.Hint);}};
            button.MouseExited+=()=>{if(!abandoned){hint?.Invoke(button,null);if(hintOwner==button)hintOwner=null;}};
        }
        //8514B0 hides the generic background. Row extent does not resize the
        //source outer rectangle (Domestic's last row extends two units).
    }

    internal void RequestCancel()=>Publish(null);
    private void Publish(int? action){if(!abandoned)selected?.Invoke(action);}
    internal virtual void Abandon()
    {
        if(abandoned)return;abandoned=true;selected=null;ClearHint();hint=null;
        foreach(var button in buttons)button.Disabled=true;Hide();
    }

    private Vector2I SourcePixel(Vector2 viewportPoint)
    {
        var point=GetParent<CanvasItem>().GetGlobalTransformWithCanvas().AffineInverse()*viewportPoint;
        return new((int)point.X,(int)point.Y);
    }
    internal bool ContainsSourcePoint(Vector2 viewportPoint)
    {
        var point=SourcePixel(viewportPoint);
        //86FFE0 uses the own rectangle, with all four boundaries included.
        return point.X>=Position.X&&point.Y>=Position.Y&&point.X<=Position.X+Size.X&&point.Y<=Position.Y+Size.Y;
    }
    public override void _Input(InputEvent input)
    {
        if(abandoned||!IsVisibleInTree())return;
        if(TryHandleCancelInput(input)){GetViewport().SetInputAsHandled();return;}
        if(input is not InputEventMouseButton{ButtonIndex:MouseButton.Left} mouse)return;
        var point=SourcePixel(mouse.Position);
        if(mouse.Pressed)
        {
            leftStart=point;
            if(outsideClick==UiSourcePulldownOutsideClick.PressPassthrough&&!ContainsSourcePoint(mouse.Position))RequestCancel();
        }
        else
        {
            bool confirm=leftStart is Vector2I start&&(point-start).LengthSquared()<=100;
            leftStart=null;
            if(outsideClick==UiSourcePulldownOutsideClick.ConfirmConsumed&&confirm&&!ContainsSourcePoint(mouse.Position))
            {RequestCancel();GetViewport().SetInputAsHandled();}
        }
    }
    internal bool TryHandleCancelInput(InputEvent input)
    {
        if(abandoned||!IsVisibleInTree())return false;
        if(input is InputEventMouseButton{ButtonIndex:MouseButton.Right} mouse)
        {
            var point=SourcePixel(mouse.Position);
            if(mouse.Pressed)rightStart=point;
            else
            {
                if(rightStart is Vector2I start&&(point-start).LengthSquared()<=100)RequestCancel();
                rightStart=null;
            }
            return true;
        }
        if(input is InputEventKey{Keycode:Key.Escape} key)
        {if(!key.Pressed&&!key.Echo)RequestCancel();return true;}
        return false;
    }
    public override void _ExitTree()
    {selected=null;ClearHint();hint=null;assets?.Dispose();font?.Dispose();shader?.Dispose();}
    private void ClearHint(){if(hintOwner is {} owner){hintOwner=null;hint?.Invoke(owner,null);}}
}
