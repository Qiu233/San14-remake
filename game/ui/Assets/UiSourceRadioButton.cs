using Godot;
using San14.Data;

internal enum UiSourceRadioSegment { Left, Centre, Right }

/// <summary>A common joined radio's settled artwork states. The caller owns
/// the selected value and group; releasing an already selected item is still
/// an event, as it is in the original common radio control.</summary>
internal partial class UiSourceRadioButton : UiSourceButton
{
    private Rect2 hit;
    private Action? released;
    private UiSourceText caption = null!;
    internal string Caption { get => caption.Text; set => caption.Text = value; }
    public override bool _HasPoint(Vector2 point) => SourceInputAdmitted && hit.HasPoint(point);

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader,
        MapFont font, UiSourceRadioSegment segment, string text, Action released)
    {
        this.released = released;
        uint file = segment switch
        {
            UiSourceRadioSegment.Left => 0xF2F266CA,
            UiSourceRadioSegment.Centre => 0x4D3FB1C1,
            UiSourceRadioSegment.Right => 0x0C1434D0,
            _ => throw new ArgumentOutOfRangeException(nameof(segment))
        };
        var source = new UiSourceLayout(repository, file).ResizedSubtree(0, Size, true);
        Size = source.Rectangle(0).Size;
        var rectangle = source.Rectangle(1);
        var center = rectangle.Position + rectangle.Size * .5f;
        // 92DCF0/9313A0 truncate the centre and half-size independently.
        // The artwork keeps its half-pixel origin when the caller is odd-width.
        hit = new(new Vector2((int)center.X - (int)(rectangle.Size.X * .5f),
            (int)center.Y - (int)(rectangle.Size.Y * .5f)),
            new Vector2((int)rectangle.Size.X, (int)rectangle.Size.Y));
        ActionMode = ActionModeEnum.Release;
        var states = new[] { 3, 5, 8, 11, 13 }
            .Select(index => source.Art(this, index, source.Rectangle(index), assets, shader)).ToArray();
        caption = source.Text(this, 16, default, text, font);
        caption.Name = "Caption";
        var profile = MapFontProfile.FromNode(source.Nodes[16], source.Source.Source.ResourceUri);
        var presets = MapTextPresets.Create();
        var mark = source.Art(this, 17, source.Rectangle(17), assets, shader);
        mark.Name = "SelectionMark";
        ConfigureStates(states, state =>
        {
            caption.Configure(font, profile.WithPreset(presets[3 + state]));
            caption.QueueRedraw();
            // Node17 property 0xA is alpha: only push/push_focus settled
            // ranges are opaque. Value52 merely declares animation channels.
            mark.Visible = state == 2;
        });
        Pressed += () => { if (!Disabled && SourceInputAdmitted) this.released?.Invoke(); };
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void Abandon()
    {
        released = null;
        Disabled = true;
    }
}
