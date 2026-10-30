using Godot;
using San14.Data;

/// <summary>The dedicated key-configuration button. Its authored 267 by40
/// root and 279 by48 artwork are independent; the key row only mounts it.</summary>
internal partial class UiSourceKeyboardBindingButton : UiSourceButton
{
    private Rect2 hitArea;
    private UiSourceText caption = null!;
    private Action? released;
    internal string Caption { get => caption.Text; set => caption.Text = value; }
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hitArea.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string text, Action? released)
    {
        this.released = released;
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_03_02_setup_btn_offset_00"));
        Size = source.Rectangle(0).Size;
        Rect2 rectangle = source.Rectangle(1);
        Vector2 centre = rectangle.GetCenter();
        hitArea = new(new Vector2((int)centre.X - (int)(rectangle.Size.X * .5f),
            (int)centre.Y - (int)(rectangle.Size.Y * .5f)),
            new Vector2((int)rectangle.Size.X, (int)rectangle.Size.Y));
        ActionMode = ActionModeEnum.Release;
        // The behavior's five static states use this dedicated layout; its
        // separate additive focus animation remains with the other deferred UI animations.
        Control[] states = new[] { 3, 5, 8, 12, 14 }.Select(node =>
        {
            if (node != 8) return source.Art(this, node, source.Rectangle(node), assets, shader);
            var pressed = new Control { Name = "KeyConfigurationPressed", Size = Size,
                MouseFilter = MouseFilterEnum.Ignore };
            AddChild(pressed);
            source.Art(pressed, 8, source.Rectangle(8), assets, shader);
            source.Art(pressed, 10, source.Rectangle(10), assets, shader);
            return pressed;
        }).ToArray();
        caption = source.Text(this, 17, default, text, font);
        MapFontProfile profile = MapFontProfile.FromNode(source.Nodes[17], source.Source.Source.ResourceUri);
        MapTextPresets presets = MapTextPresets.Create();
        int[] statePresets = [3, 4, 5, 6, 6];
        ConfigureStates(states, state =>
        { caption.Configure(font, profile.WithPreset(presets[statePresets[state]])); caption.QueueRedraw(); });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Abandon() { released = null; Disabled = true; }
}
