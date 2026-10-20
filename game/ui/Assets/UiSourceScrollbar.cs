using Godot;
using San14.Data;

/// <summary>The source pixel scrollbar. Animation is deferred; discrete input
/// retains the original direction alignment, clamping and thumb geometry.</summary>
internal partial class UiSourceScrollbar : Control
{
    private UiSourceButton _thumb = null!;
    private Action<int>? _changed;
    private int _range, _value, _step, _direction, _travel, _interior;
    private bool _dragging;
    private int _pressedMouseY;
    private int _pressedThumbY;
    internal int Value => _value;
    internal int Range => _range;
    private int ThumbY => _range == 0 ? 17 : (_travel-_interior)*_value/_range+17;

    internal void ConfigureNoRange(GameResourceRepository repository, UiAssets assets, Shader shader) =>
        Configure(repository, assets, shader, 0, 0, null);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        int range, int scrollSpeedSetting, Action<int>? changed, int? stepPixels = null)
    {
        if (range < 0 || Size.Y < 132) throw new ArgumentOutOfRangeException(nameof(range));
        _range = range; _changed = changed;
        _step = stepPixels ?? (scrollSpeedSetting switch { 1 => 96, 2 => 144, _ => 48 });
        if (_step <= 0) throw new ArgumentOutOfRangeException(nameof(stepPixels));
        int height = (int)Size.Y;
        _travel = height-24-54;
        _interior = height*_travel/(range+height);
        if (_interior < 38) _interior = 38;
        else if (_travel < _interior) _interior = _travel;
        var authored = new UiSourceLayout(repository,0x9F416DB7);
        var geometry = new Dictionary<int,UiSourceNodeGeometry>
        {
            [0] = new(Size: Size),
            [1] = new(Scale: new(1,(float)(height-24-8)/authored.Nodes[1].Value46)),
            [17] = new(Origin: new(authored.Rectangle(17).Position.X,height-24)),
            [31] = new(Size: new(24,_interior+47),Origin: new(-4,17))
        };
        foreach (int node in new[]{33,35,38,39,41}) geometry[node] = new(Size: new(24,_interior+47));
        var source = new UiSourceLayout(repository,0x9F416DB7,geometry);
        MouseFilter = MouseFilterEnum.Pass;
        SetProcessInput(false);
        source.Art(this,2,source.Rectangle(2),assets,shader);
        var track = new UiSourceHitButton { Name = "ScrollTrack", Position = source.Rectangle(1).Position,
            Size = source.Rectangle(1).Size, Disabled = range == 0, IgnoreTextureSize = true,
            FocusMode = FocusModeEnum.None };
        AddChild(track);
        track.GuiInput += input =>
        {
            if (input is InputEventMouseButton {ButtonIndex: MouseButton.Left,Pressed:true} mouse && _range != 0)
            {
                int mouseY=(int)(track.Position.Y+mouse.Position.Y);
                if (SeekThumb(mouseY-_interior/2)) BeginDrag(mouseY);
                AcceptEvent();
            }
            HandleWheel(input);
        };
        var top = Pane("ScrollUp",3,[[5],[7,8],[10,11],[13],[13]]);
        var bottom = Pane("ScrollDown",17,[[19],[21,22],[24,25],[27],[27]]);
        _thumb = Pane("ScrollThumb",31,[[33],[35,36],[38,39],[41],[41]]);
        top.ButtonDown += () => Step(-1);
        bottom.ButtonDown += () => Step(1);
        _thumb.GuiInput += input =>
        {
            if (input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:true} mouse && _range != 0)
            { BeginDrag((int)(_thumb.Position.Y+mouse.Position.Y)); AcceptEvent(); }
        };
        GuiInput += HandleWheel;
        SetMeta("source_layout",source.Source.Source.ResourceUri);

        UiSourceButton Pane(string name,int node,int[][] artwork)
        {
            Rect2 box = source.Rectangle(node);
            var button = new UiSourceButton {Name=name,Position=box.Position,
                Size=node==31 ? new(24,_interior+47) : box.Size,Disabled=range==0};
            AddChild(button);
            var states = new Control[5];
            for (int i=0;i<states.Length;i++)
            {
                states[i] = new Control {Name="State"+i,MouseFilter=MouseFilterEnum.Ignore};
                button.AddChild(states[i]);
                foreach (int art in artwork[i])
                {
                    Rect2 rectangle = source.Rectangle(art); rectangle.Position -= box.Position;
                    source.Art(states[i],art,rectangle,assets,shader);
                }
            }
            button.ConfigureStates(states);
            button.GuiInput += HandleWheel;
            return button;
        }
    }

    internal void HandleWheel(InputEvent input)
    {
        if (_range == 0 || input is not InputEventMouseButton {Pressed:true} mouse) return;
        int direction = mouse.ButtonIndex switch {MouseButton.WheelUp => -1,MouseButton.WheelDown => 1,_ => 0};
        if (direction == 0) return;
        Step(direction); AcceptEvent();
    }

    internal void Step(int direction)
    {
        if (_range == 0) return;
        int distance = _step;
        if (_direction != 0 && _direction != direction)
        {
            int remainder = _value%_step;
            distance = direction>0 ? _step-remainder : remainder==0 ? _step : remainder;
        }
        _direction = direction;
        SetValue(_value+direction*distance);
    }

    internal void SetValue(int value)
    {
        int next = Math.Clamp(value,0,_range);
        if (next == _value) return;
        _value = next; _thumb.Position = new(_thumb.Position.X,ThumbY);
        _changed?.Invoke(_value);
    }

    private bool SeekThumb(int targetY)
    {
        int previous = _value;
        if (_travel != _interior) SetValue((targetY-17)*_range/(_travel-_interior));
        return previous != _value;
    }
    private void BeginDrag(int mouseY)
    {
        _dragging = true; _pressedMouseY = mouseY; _pressedThumbY = ThumbY;
        _thumb.Selected = true; SetProcessInput(true);
    }
    public override void _Input(InputEvent input)
    {
        if (!_dragging) return;
        if (input is InputEventMouseMotion motion) DragTo(motion.Position);
        else if (input is InputEventMouseButton {ButtonIndex:MouseButton.Left,Pressed:false} release)
        { DragTo(release.Position); _dragging = false; _thumb.Selected = false; SetProcessInput(false); }
    }
    private void DragTo(Vector2 viewportPosition)
    {
        int mouseY=(int)(GetGlobalTransformWithCanvas().AffineInverse()*viewportPosition).Y;
        SeekThumb(unchecked(mouseY-_pressedMouseY+_pressedThumbY));
    }
}
