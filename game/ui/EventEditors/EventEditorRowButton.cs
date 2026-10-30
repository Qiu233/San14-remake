using Godot;
using San14.Data;

/// <summary>The event list's authored row and its mounted common command
/// behavior. The row supplies caption node 3 to common caption node 16.</summary>
internal partial class EventEditorRowButton : UiSourceButton
{
    private Rect2 hit;
    private UiSourceText caption = null!;
    private Action? released;
    internal string Caption { get => caption.Text; set => caption.SetKnownColourLine(value); }
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, string text, Action released)
    {
        this.released = released;
        var row = new UiSourceLayout(repository, 0x9E28F956);
        var source = new UiSourceLayout(repository, 0x16D9E461).ResizedSubtree(0, Size, true);
        hit = source.Rectangle(1);
        ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(node => source.Art(this, node, source.Rectangle(node), assets, shader)).ToArray();
        caption = row.Text(this, 3, default, "", font);
        var profile = MapFontProfile.FromNode(row.Nodes[3], row.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state =>
        { caption.Configure(font, profile.WithPreset(presets[28 + state])); caption.QueueRedraw(); });
        caption.SetKnownColourLine(text);
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", row.Source.Source.ResourceUri);
    }

    internal void Abandon() { released = null; Disabled = true; InputAdmission = null; }
}
