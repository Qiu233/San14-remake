using Godot;
using San14.Data;

/// <summary>The source horizontal slider, including its distinct programmatic
/// value setter and user-input quantization.</summary>
internal partial class UiSourceSlider : Control
{
    private UiSourceHitButton track=null!;
    private SliderHandle handle=null!;
    private Control fill=null!;
    private int minimum,maximum,step,current;
    private float fullFillWidth;
    private int pressedMouseX,pressedHandleX;
    private bool dragging,enabled;
    private Action<int>? changed;
    internal int Value=>current==maximum?maximum:unchecked(step*(int)MathF.Round((float)current/step,MidpointRounding.AwayFromZero));
    internal bool Enabled=>enabled;
    internal float PositionForValue(int value)
    {
        if(minimum>=maximum)return value==0?-2:Size.X-16;
        int clamped=Math.Clamp(value,minimum,maximum);
        return (int)((float)(clamped-minimum)/(maximum-minimum)*(Size.X-14))-2;
    }
    internal int ValueForPosition(int position)
    {
        int left=-2,right=(int)Size.X-16;
        if(position<=left)return minimum;
        if(position>=right)return maximum;
        int value=unchecked((int)((float)(position-left)/(right-left)*(maximum-minimum))+minimum);
        return unchecked((int)MathF.Round((float)value/step,MidpointRounding.AwayFromZero)*step);
    }

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,
        int minimum,int maximum,int step,int value,bool enabled,Action<int>? changed)
    {
        if(step<=0)throw new ArgumentOutOfRangeException(nameof(step));
        this.minimum=minimum;this.maximum=maximum;this.step=step;this.changed=changed;
        MouseFilter=MouseFilterEnum.Pass;SetProcessInput(false);
        var authored=new UiSourceLayout(repository,0xE78964A4);
        var source=authored.ResizedSubtree(1,new(Size.X,authored.Nodes[1].Value46),true,Size);
        source.Art(this,2,source.Rectangle(2),assets,shader);
        fill=source.Art(this,3,source.Rectangle(3),assets,shader);fullFillWidth=fill.Size.X;
        var box=source.Rectangle(1);
        track=new UiSourceHitButton {Name="SliderTrack",Position=box.Position,Size=box.Size,
            IgnoreTextureSize=true,FocusMode=FocusModeEnum.None};AddChild(track);
        track.GuiInput+=input=>
        {
            if(!this.enabled||input is not InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:true} mouse)return;
            int mouseX=(int)(track.Position.X+mouse.Position.X);
            ChangeFromPointer(mouseX);BeginDrag(mouseX);AcceptEvent();
        };
        var handleBox=source.Rectangle(4);
        handle=new SliderHandle {Name="SliderHandle",Size=handleBox.Size};AddChild(handle);
        var states=new Control[5];int[] art=[6,8,10,12,12];
        for(int i=0;i<states.Length;i++)
        {
            states[i]=new Control {MouseFilter=MouseFilterEnum.Ignore};handle.AddChild(states[i]);
            var rectangle=source.Rectangle(art[i]);rectangle.Position-=handleBox.Position;
            source.Art(states[i],art[i],rectangle,assets,shader);
        }
        handle.ConfigureStates(states);
        handle.GuiInput+=input=>
        {
            if(this.enabled&&input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:true} mouse)
            {BeginDrag((int)(handle.Position.X+mouse.Position.X));AcceptEvent();}
        };
        SetValue(value,maximum,enabled);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void SetValue(int value,int maximum,bool enabled)
    {
        current=value;this.maximum=maximum;this.enabled=enabled;
        track.Disabled=handle.Disabled=!enabled;
        if(!enabled)EndDrag();
        Refresh();
    }
    private void Refresh()
    {
        bool inRange=minimum<maximum&&current>=minimum&&current<=maximum;
        handle.Position=new(inRange?PositionForValue(current):current==0?-2:Size.X-16,0);
        float width=inRange?(int)((float)(current-minimum)*fullFillWidth/(maximum-minimum)):current==0?0:fullFillWidth;
        fill.Size=new(width,fill.Size.Y);
        foreach(var image in fill.GetChildren().OfType<UiSourceSprite>())image.Size=new(width,image.Size.Y);
        fill.Visible=width>0;
    }
    private void ChangeFromPointer(int position)
    {
        int value=Math.Clamp(ValueForPosition(position),minimum,maximum);
        if(value>0&&value%step!=0)value=unchecked((value/step+1)*step);
        current=Math.Min(value,maximum);Refresh();changed?.Invoke(Value);
    }
    private void BeginDrag(int mouseX)
    {
        dragging=true;pressedMouseX=mouseX;pressedHandleX=(int)handle.Position.X;
        handle.Selected=true;SetProcessInput(true);
    }
    private void EndDrag()
    {
        dragging=false;handle.Selected=false;SetProcessInput(false);
    }
    public override void _Input(InputEvent input)
    {
        if(!dragging)return;
        if(input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:false} release)
        {
            DragTo(release.Position);EndDrag();return;
        }
        if(input is InputEventMouseMotion motion)DragTo(motion.Position);
    }
    private void DragTo(Vector2 viewportPosition)
    {
        int mouseX=(int)(GetGlobalTransformWithCanvas().AffineInverse()*viewportPosition).X;
        ChangeFromPointer(unchecked(pressedHandleX+mouseX-pressedMouseX));
    }
    private partial class SliderHandle : UiSourceButton
    {
        internal override int VisualState=>Disabled?3:base.VisualState;
    }
}
