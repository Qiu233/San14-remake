using Godot;
using San14.Data;

internal sealed record CurrentWorldEditNumberField(int MountNode, string Caption,
    int? Value, int Minimum, int? Maximum, bool Enabled = true, bool Extended = false,
    int? ValueAnnotationNode = null, Func<int?, int?, string>? FormatAnnotation = null);
internal sealed record CurrentWorldEditRadioOption(int MountNode, int Id, string Caption);
internal sealed record CurrentWorldEditNumberResult(IReadOnlyList<int?> Values, int? Choice);

/// <summary>The original numeric subpage keeps its own values until its
/// decision. A calculator edits this subpage and never submits a game order.</summary>
internal partial class CurrentWorldEditNumbersView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<UiSourceEditSlider> sliders = [];
    private readonly List<(int Id, UiSourceRadioButton Button)> radios = [];
    private readonly List<(int Index, UiSourceText Label)> annotations = [];
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private IReadOnlyList<CurrentWorldEditNumberField> fields = [];
    private int?[] values = [];
    private int? choice;
    private Func<bool>? admitted;
    private Action<CurrentWorldEditNumberResult?>? closed;
    private Func<CurrentWorldEditNumberResult, IReadOnlyList<CurrentWorldEditNumberField>>? preview;
    private DeploymentCalculatorDialog? calculator;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text, string layoutName,
        int titleMessage, string guide, IReadOnlyList<CurrentWorldEditNumberField> fields,
        IReadOnlyList<string> functionIcons, Func<bool> admitted,
        Action<CurrentWorldEditNumberResult?> closed,
        IReadOnlyList<CurrentWorldEditRadioOption>? options = null, int? initialChoice = null,
        IReadOnlyDictionary<int, string>? literalText = null,
        Func<CurrentWorldEditNumberResult, IReadOnlyList<CurrentWorldEditNumberField>>? preview = null)
    {
        this.repository = repository; this.text = text; this.fields = fields;
        this.admitted = admitted; this.closed = closed; choice = initialChoice; this.preview = preview;
        values = fields.Select(field => field.Value).ToArray();
        Name = "CurrentWorldEditNumbers"; ZIndex = 7; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, titleMessage, RequestBack, Accept);
        chrome.ApplyDefaultHint(guide);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, layoutName));
        Vector2 mount = new((1920 - source.Rectangle(0).Size.X) / 2,
            (1080 - source.Rectangle(0).Size.Y) / 2);
        var replaced = fields.Select(field => field.MountNode).ToHashSet();
        if (options is not null) replaced.UnionWith(options.Select(option => option.MountNode));
        foreach (var node in source.Nodes)
        {
            bool hidden = false;
            for (int? cursor = node.Index; cursor is int at; cursor = source.Nodes[at].ParentIndex)
                if (replaced.Contains(at)) { hidden = true; break; }
            if (hidden) continue;
            if (node.Type is 1 or 3)
                source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
            else if (node.Type == 6) source.Blur(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount));
        }
        if (literalText is not null)
            foreach (var (node, value) in literalText) source.Text(chrome.Canvas, node, mount, value, chrome.Font);
        for (int index = 0; index < fields.Count; index++)
        {
            int entry = index; var field = fields[index];
            var row = new UiSourceEditSlider { Name = "CurrentWorldEditNumber" + index,
                Position = source.Rectangle(field.MountNode, mount).Position };
            chrome.Canvas.AddChild(row); sliders.Add(row);
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, field.Caption,
                field.Minimum, field.Maximum ?? field.Minimum, 1, field.Value ?? field.Minimum,
                CanChange(field), CanEdit,
                value => { if (CanEdit()) { values[entry] = value; RefreshAnnotations(); } },
                () => OpenCalculator(entry), functionIcons, field.Extended);
            if (field.Value is null) row.ApplyUnavailable(text.Get("detail_unknown"));
            row.SetMeta("source_mount_node", field.MountNode);
            if (field.ValueAnnotationNode is int annotation)
                annotations.Add((entry, source.Text(chrome.Canvas, annotation, mount, "", chrome.Font)));
        }
        if (options is not null)
            for (int index = 0; index < options.Count; index++)
            {
                var option = options[index]; var box = source.Rectangle(option.MountNode, mount);
                var button = new UiSourceRadioButton { Name = "CurrentWorldEditChoice" + option.Id,
                    Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
                chrome.Canvas.AddChild(button); radios.Add((option.Id, button));
                button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                    index == 0 ? UiSourceRadioSegment.Left : index == options.Count - 1 ? UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre,
                    option.Caption, () =>
                    {
                        if (!CanEdit()) return;
                        choice = option.Id;
                        if (this.preview is { } project)
                        {
                            var replacement = project(new(values.ToArray(), choice));
                            if (replacement.Count != fields.Count)
                                throw new InvalidDataException("Editing preview changed the source row count.");
                            this.fields = replacement;
                            values = replacement.Select(field => field.Value).ToArray();
                            for (int row = 0; row < replacement.Count; row++)
                            {
                                var field = replacement[row];
                                sliders[row].SetValue(field.Value ?? field.Minimum, field.Maximum ?? field.Minimum, CanChange(field));
                                if (field.Value is null) sliders[row].ApplyUnavailable(text.Get("detail_unknown"));
                            }
                        }
                        Refresh();
                    });
            }
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();
    }

    private bool CanEdit() => !abandoned && calculator is null && IsVisibleInTree() && admitted?.Invoke() == true;
    private static bool CanChange(CurrentWorldEditNumberField field) => field.Enabled && field.Value is not null && field.Maximum is not null;
    private void Refresh()
    {
        if (abandoned) return;
        for (int index = 0; index < sliders.Count; index++) sliders[index].SetEnabled(CanEdit() && CanChange(fields[index]));
        foreach (var (id, button) in radios) { button.Selected = id == choice; button.Disabled = !CanEdit(); }
        RefreshAnnotations();
        chrome.ApplyReturn(calculator is null); chrome.ApplyDecision(CanEdit(), true);
    }
    private void RefreshAnnotations()
    {
        foreach (var (index, label) in annotations)
            label.Text = fields[index].FormatAnnotation?.Invoke(values[index], fields[index].Maximum) ?? "";
    }
    private void OpenCalculator(int index)
    {
        if (!CanEdit() || !CanChange(fields[index])) return;
        var field = fields[index];
        var view = new DeploymentCalculatorDialog(); calculator = view; AddChild(view);
        view.Configure(repository, text, values[index]!.Value, field.Minimum, field.Maximum!.Value, result =>
        {
            if (abandoned || calculator != view) return;
            calculator = null; view.Abandon(); view.QueueFree(); Refresh();
            if (result is int amount) sliders[index].ChangeValue(amount);
        });
        Refresh();
    }
    private void Accept()
    {
        if (!CanEdit()) return;
        Finish(new(values.ToArray(), choice));
    }
    private void Finish(CurrentWorldEditNumberResult? result)
    { var callback = closed; Abandon(); callback?.Invoke(result); }
    internal void RequestBack()
    {
        if (calculator is { } child) { child.RequestCancel(); return; }
        if (CanEdit()) Finish(null);
    }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; admitted = null; closed = null; preview = null;
        calculator?.Abandon(); calculator = null;
        foreach (var slider in sliders) slider.Abandon();
        foreach (var (_, button) in radios) { button.Disabled = true; button.InputAdmission = null; }
        chrome?.Abandon(); Hide();
    }
}
