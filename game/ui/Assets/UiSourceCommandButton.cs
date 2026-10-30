using Godot;
using San14.Data;

/// <summary>The common hanyou-defo behavior used for text commands and number
/// entry. Both share its resized hit, five artwork states and presets28..32.</summary>
internal partial class UiSourceCommandButton : UiSourceButton
{
    private Rect2 hitArea;
    private UiSourceText caption = null!;
    private Action? released;
    internal string Caption { get => caption.Text; set => caption.Text = value; }
    public override bool _HasPoint(Vector2 point) => hitArea.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string text, Action? released)
    {
        this.released = released;
        var source = assets.CommandButtonLayout(Size);
        var rectangle = source.Rectangle(1);
        var centre = rectangle.GetCenter();
        hitArea = new(new Vector2((int)centre.X - (int)(rectangle.Size.X * .5f),
            (int)centre.Y - (int)(rectangle.Size.Y * .5f)),
            new Vector2((int)rectangle.Size.X, (int)rectangle.Size.Y));
        ActionMode = ActionModeEnum.Release;
        // Nodes6/9/14 have separate additive state. Drawing these as ordinary
        // source-alpha art would paint an incorrect second background.
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        caption = source.Text(this, 16, default, text, font);
        var profile = MapFontProfile.FromNode(source.Nodes[16], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state =>
        { caption.Configure(font, profile.WithPreset(presets[28 + state])); caption.QueueRedraw(); });
        Pressed += () => { if (!Disabled) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Abandon() { released = null; Disabled = true; }
}
