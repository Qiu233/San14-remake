using Godot;
using San14.Data;

/// <summary>The original common checkbox-list button with its check hidden.
/// Both city commands and the ordinary Move menu use this exact behavior.</summary>
internal partial class UiSourceListCheckboxButton : UiSourceButton
{
    private Rect2 hit;
    private Action? released;
    protected UiSourceText CaptionControl { get; private set; } = null!;
    public override bool _HasPoint(Vector2 point) => hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, Vector2 requestedSize, string caption, Action released, Rect2? captionRectangle = null)
    {
        this.released = released;
        var source = new UiSourceLayout(repository, 0x7BC42A66).ResizedSubtree(0, requestedSize, true);
        Size = source.Rectangle(0).Size;
        var rectangle = source.Rectangle(1);
        var centre = rectangle.GetCenter();
        // v51 copies integer centre-minus-half-size independently of art.
        // In particular requested124 becomes actual123, whose art starts at-.5.
        hit = new(new Vector2((int)centre.X - (int)(rectangle.Size.X * .5f),
            (int)centre.Y - (int)(rectangle.Size.Y * .5f)),
            new Vector2((int)rectangle.Size.X, (int)rectangle.Size.Y));
        ActionMode = ActionModeEnum.Release;
        int[] art = [3, 5, 8, 11, 13];
        var states = new Control[art.Length];
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { Name = "State" + state, Size = Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group);
            source.Art(group, art[state], source.Rectangle(art[state]), assets, shader);
            if (state is 1 or 4)
            {
                int node = state == 1 ? 6 : 14;
                byte alpha = state == 1 ? (byte)186 : (byte)127;
                uint packed = 0x00FFFFFFu | (uint)alpha << 24;
                var color = new AreaMarkerColor(packed, packed);
                source.Art(group, node, source.Rectangle(node), assets, shader,
                    new(color, color, color, color), blend: UiSourceSprite.Blend.Additive);
            }
            states[state] = group;
        }
        // The caller hides the check. The changing push glow is still deferred;
        // focus and disabled-focus above use their source range's constant alpha.
        CaptionControl = source.Text(this, 16, default, caption, font, rectangle: captionRectangle);
        CaptionControl.Name = "CommandCaption";
        var profile = MapFontProfile.FromNode(source.Nodes[16], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        int[] styles = [3, 4, 5, 6, 6];
        ConfigureStates(states, state =>
        {
            CaptionControl.Configure(font, profile.WithPreset(presets[styles[state]]));
            CaptionControl.QueueRedraw();
        });
        Pressed += () => { if (!Disabled) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Abandon() { released = null; Disabled = true; }
}
