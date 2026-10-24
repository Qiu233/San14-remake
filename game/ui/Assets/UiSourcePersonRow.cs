using Godot;
using San14.Data;

/// <summary>A checked person-list row. It shares the source button artwork
/// while keeping check content independent from transient input-state changes.</summary>
internal partial class UiSourcePersonRow : UiSourceButton
{
    private UiSourceText? _check;
    private string _checkedGlyph = "";
    private readonly List<(UiSourceText Label, MapFontProfile Profile, bool Emphasized)> _labels = [];
    private Action<int>? _applyTextState;
    private bool? _alteredHealth = false;

    internal void ApplyHealthStyle(bool? alteredHealth)
    {
        _alteredHealth = alteredHealth;
        SetMeta("health_style_known", alteredHealth.HasValue);
        _applyTextState?.Invoke(VisualState);
    }

    internal void ApplyCells(IReadOnlyList<DeploymentPersonCell> cells)
    {
        if(cells.Count!=_labels.Count)throw new ArgumentException("Person cells must match their source columns.");
        bool styleChanged=false;
        for(int i=0;i<cells.Count;i++)
        {
            var (label,profile,emphasized)=_labels[i];
            label.Text=cells[i].Text;
            label.FirstGlyphColour=cells[i].FirstGlyphColour;
            if(emphasized!=cells[i].Emphasized)
            {
                _labels[i]=(label,profile,cells[i].Emphasized);
                styleChanged=true;
            }
        }
        if(styleChanged)_applyTextState?.Invoke(VisualState);
    }

    internal override bool Selected
    {
        get => base.Selected;
        set { base.Selected = value; RefreshCheck(); }
    }
    // The person list paints an existing check before its enable/focus gates.
    internal override int VisualState => Selected ? 2 : base.VisualState;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string checkedGlyph, IReadOnlyList<DeploymentPersonColumn> columns, IReadOnlyList<DeploymentPersonCell> cells,
        float width = 1464)
    {
        if (columns.Count != cells.Count) throw new ArgumentException("Person cells must match their source columns.");
        Size = new(width, 48);
        _checkedGlyph = checkedGlyph;
        var source = new UiSourceLayout(repository, 0xF2C06161);
        var presets = MapTextPresets.Create();
        int[] frontNodes = [4, 6, 9, 12, 14], bodyNodes = [23, 25, 28, 31, 33];
        var states = new Control[5];
        for (int state = 0; state < states.Length; state++)
        {
            var group = new Control { Size = Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(group);
            source.Art(group, frontNodes[state], new(Vector2.Zero, new(48, 48)), assets, shader);
            source.Art(group, bodyNodes[state], new(new(48, 0), new(width - 48, 48)), assets, shader);
            states[state] = group;
        }
        _check = source.Text(this, 17, default, "", font);
        _check.Name = "PersonCheckedGlyph";
        var checkProfile = MapFontProfile.FromNode(source.Nodes[17], source.Source.Source.ResourceUri);
        int x = 48;
        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var profile = new MapFontProfile(source.Source.Source.ResourceUri + "/dynamic" + (40 + i), 0, 0,
                .875f, .875f, 0, 0, uint.MaxValue, uint.MaxValue, uint.MaxValue, uint.MaxValue,
                0, column.Align, column.LineArrange, 0, 0, 0xFF000000, 0xFF000000, 0xFF000000, 0xFF000000)
            { WidthFitMode = 2, WidthReserve = 0, StrokeColour = 0xFF000000, StrokeWidth = 3 };
            var label = new UiSourceText { Name = "PersonColumn" + column.Id,
                Position = new(x + 10, 10), Size = new(column.Width - 20, 28) };
            AddChild(label);
            label.Configure(font, profile.WithPreset(presets[8]));
            label.Text = cells[i].Text;
            label.FirstGlyphColour = cells[i].FirstGlyphColour;
            _labels.Add((label, profile, cells[i].Emphasized));
            x += column.Width;
        }
        _applyTextState=ApplyTextState;
        ConfigureStates(states,ApplyTextState);
        RefreshCheck();
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        void ApplyTextState(int state)
        {
            int ordinary = _alteredHealth == true && state < 3 ? state == 2 ? 68 : 67 : 8 + Math.Min(state, 3);
            _check.Configure(font, checkProfile.WithPreset(presets[ordinary]));
            _check.QueueRedraw();
            foreach (var (label, profile, emphasized) in _labels)
            {
                int preset = emphasized ? state is 0 or 1 ? 65 : state == 2 ? 66 : 11 : ordinary;
                label.Configure(font, profile.WithPreset(presets[preset]));
                label.QueueRedraw();
            }
        }
    }

    private void RefreshCheck()
    {
        if (_check is not null) _check.Text = Selected ? _checkedGlyph : "";
    }
}
