using Godot;
using San14.Data;

/// <summary>The force page's embedded small button, using its own authored
/// subtree and hit area rather than the larger common function-button skin.</summary>
internal partial class NewGameGeographicAdvantagesButton : UiSourceButton
{
    private Rect2 hit;
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);

    internal void Configure(UiSourceLayout source, UiAssets assets, Shader shader,
        MapFont font, string caption, Action released)
    {
        Vector2 origin = source.Rectangle(79).Position;
        Position = origin; Size = source.Rectangle(79).Size;
        Rect2 area = source.Rectangle(80);
        hit = new(area.Position - origin, area.Size);
        ActionMode = ActionModeEnum.Release;
        int[][] groups = [[82], [84, 85], [87, 88], [90], [92, 93]];
        var states = groups.Select((nodes, index) =>
        {
            var state = new Control { Name = "State" + index, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(state);
            foreach (int node in nodes)
            {
                Rect2 rect = source.Rectangle(node);
                source.Art(state, node, new(rect.Position - origin, rect.Size), assets, shader);
            }
            return state;
        }).ToArray();
        Rect2 labelRect = source.Rectangle(95);
        var label = source.Text(this, 95, default, caption, font,
            rectangle: new(labelRect.Position - origin, labelRect.Size));
        var profile = MapFontProfile.FromNode(source.Nodes[95], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        ConfigureStates(states, state => label.Configure(font, profile.WithPreset(presets[3 + state])));
        Pressed += () => { if (!Disabled && SourceInputAdmitted) released(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri); SetMeta("source_node", 79);
    }
}
