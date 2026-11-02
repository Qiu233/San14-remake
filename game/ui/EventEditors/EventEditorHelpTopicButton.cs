using Godot;
using San14.Data;

/// <summary>The normal help list row. Its leading 108 pixels belong to the
/// icon column, outside the source's 958-pixel topic button hit rectangle.</summary>
internal partial class EventEditorHelpTopicButton : UiSourceButton
{
    private Rect2 hit;
    private Action? selected;
    private UiSourceText caption = null!;
    private Control? marker17, marker18;
    private bool abandoned;

    public override bool _HasPoint(Vector2 point) => !abandoned && SourceInputAdmitted && hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string title, Action selected)
    {
        this.selected = selected;
        var source = new UiSourceLayout(repository, 0xDB3C7E26);
        Size = source.Rectangle(0).Size;
        hit = source.Rectangle(1);
        ActionMode = ActionModeEnum.Release;
        int[][] artwork = [[3], [5, 6], [8, 9], [11], [13, 14]];
        var states = new Control[artwork.Length];
        for (int index = 0; index < artwork.Length; index++)
        {
            var state = new Control { Name = "HelpTopicState" + index, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(state); states[index] = state;
            foreach (int node in artwork[index])
                source.Art(state, node, source.Rectangle(node), assets, shader);
        }
        caption = source.Text(this, 16, default, "", font);
        var profile = MapFontProfile.FromNode(source.Nodes[16], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        //848E90 sets preset 8;84B140/84B1B0 change focus preset 9 back to 8.
        //The help list has no alternate selected text or numeric value style.
        ConfigureStates(states, state =>
        {
            caption.Configure(font, profile.WithPreset(presets[state is 1 or 2 ? 9 : 8]));
            caption.QueueRedraw();
        });
        caption.SetKnownColourLine(title);
        marker17 = source.Art(this, 17, source.Rectangle(17), assets, shader);
        marker18 = source.Art(this, 18, source.Rectangle(18), assets, shader);
        marker17.Hide(); marker18.Hide();
        Pressed += () => { if (!abandoned && !Disabled && SourceInputAdmitted) this.selected?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void BindTitle(string value, bool showMarker17 = false, bool showMarker18 = false)
    {
        caption.SetKnownColourLine(value);
        if (marker17 is not null) marker17.Visible = showMarker17 && !showMarker18;
        if (marker18 is not null) marker18.Visible = showMarker18;
    }

    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; selected = null; Disabled = true; InputAdmission = null;
    }
}
