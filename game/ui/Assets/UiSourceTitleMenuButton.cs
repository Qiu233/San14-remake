using Godot;
using San14.Data;

/// <summary>The title menu's common FT button. Its surrounding 510-pixel pane
/// belongs to the menu; only this control's authored HitArea accepts input.</summary>
internal partial class UiSourceTitleMenuButton : UiSourceButton
{
    private Rect2 hitArea;
    private Action? released;

    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hitArea.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string text, Action released)
    {
        this.released = released;
        var source = new UiSourceLayout(repository, 0x918FA91E).ResizedSubtree(0, Size, true);
        var rectangle = source.Rectangle(1);
        hitArea = new(new Vector2((int)rectangle.Position.X, (int)rectangle.Position.Y),
            new Vector2((int)rectangle.Size.X, (int)rectangle.Size.Y));
        ActionMode = ActionModeEnum.Release;
        // Nodes8/15/24 have separate additive alpha tracks. Their animation
        // is deferred; the five states below retain their ordinary parts.
        int[][] layers = [[3,4,5], [7,9,10], [12,13,14], [17,18,19], [21,22,23]];
        var states = new Control[layers.Length];
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { Name = "TitleButtonState" + state, Size = Size,
                MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group);
            states[state] = group;
            foreach (int node in layers[state]) source.Art(group, node, source.Rectangle(node), assets, shader);
        }
        var caption = source.Text(this, 26, default, text, font);
        caption.Name = "TitleMenuCaption";
        var profile = MapFontProfile.FromNode(source.Nodes[26], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state =>
        {
            caption.Configure(font, profile.WithPreset(presets[3 + state]));
            caption.QueueRedraw();
        });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    public override void _ExitTree() { released = null; InputAdmission = null; }
}
