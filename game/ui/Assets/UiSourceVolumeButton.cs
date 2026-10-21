using Godot;
using San14.Data;

internal enum UiSourceVolumePart { Mute, Left, Right }

/// <summary>A discrete volume control. Persistent selection also admits a
/// release over the control when the press began elsewhere (8761F0/856AC0).</summary>
internal partial class UiSourceVolumeButton : UiSourceButton
{
    // Its native control release changes the gain and lets the caller request
    // a category preview; it does not request a common button cue itself.
    internal override UiAudioCue PressedCue => UiAudioCue.None;

    private Rect2 hit;
    private Action? released;
    private bool transientPress;
    private bool PressedHere => transientPress && GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed;
    internal override int VisualState => Disabled ? (IsHovered() ? 4 : 3) : Selected || PressedHere ? 2 : IsHovered() ? 1 : 0;
    public override bool _HasPoint(Vector2 point) => point.X >= hit.Position.X && point.Y >= hit.Position.Y &&
        point.X <= hit.End.X && point.Y <= hit.End.Y;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        UiSourceVolumePart part, Action released)
    {
        this.released = released;
        uint file = part switch
        {
            UiSourceVolumePart.Mute => 0x2B7AA77D,
            UiSourceVolumePart.Left => 0xBF4690D6,
            UiSourceVolumePart.Right => 0x20992D75,
            _ => throw new ArgumentOutOfRangeException(nameof(part))
        };
        var source = new UiSourceLayout(repository,file);
        Size = source.Rectangle(0).Size;
        // Both constructors publish a Root-sized control and leave +48 empty.
        // 92DCF0/875B40 truncate its origin; keep half-pixel art positions.
        // The smaller authored HitArea belongs to the separate resize path.
        hit = new(new Vector2((int)Position.X-Position.X,(int)Position.Y-Position.Y),Size);
        int[] art = [3,5,8,11,13];
        var states = new Control[5];
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { Name = "State"+state, Size = Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group); states[state] = group;
            source.Art(group,art[state],source.Rectangle(art[state]),assets,shader);
            if (state is 1 or 4)
            {
                int node = state == 1 ? 6 : 14;
                byte alpha = part == UiSourceVolumePart.Mute ? (state == 1 ? (byte)190 : (byte)127) : (byte)255;
                uint packed = 0x00FFFFFFu | (uint)alpha << 24;
                var colorValue = new AreaMarkerColor(packed,packed);
                source.Art(group,node,source.Rectangle(node),assets,shader,
                    new(colorValue,colorValue,colorValue,colorValue),blend:UiSourceSprite.Blend.Additive);
            }
        }
        Action<int>? color = null;
        if (part == UiSourceVolumePart.Mute)
        {
            var glyph = source.Art(this,15,source.Rectangle(15),assets,shader,
                commonSpriteOverride:"000_icon_btn_mute");
            var colors = MapSpritePalettes.Create();
            color = state =>
            {
                foreach (var sprite in glyph.GetChildren().OfType<UiSourceSprite>()) sprite.SetColors(colors[25+state]);
            };
        }
        ConfigureStates(states,color);
        ButtonDown += () => { transientPress = !Disabled; QueueRedraw(); };
        // 8569C0/856DB0 restore the persistent selection on hover transfer;
        // leaving an unselected segment does not re-arm it on re-entry.
        MouseExited += () => { transientPress = false; QueueRedraw(); };
        ActionMode = ActionModeEnum.Release;
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    public override void _Input(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) return;
        bool eligible = Selected || PressedHere;
        transientPress = false; QueueRedraw();
        if (released is null || Disabled || !IsVisibleInTree() || !eligible ||
            GetViewport().GuiGetHoveredControl() != this) return;
        released();
        // Let the normal GUI pass clear the engine's transient press state.
        // No held/hover activation or duplicate Pressed subscription is added.
    }

    internal void Abandon() { released = null; transientPress = false; Disabled = true; }
}
