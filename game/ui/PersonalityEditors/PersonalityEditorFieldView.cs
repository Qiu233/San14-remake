using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The actual condition/effect popup owns a detached code and amount.
/// The application applies its result through the catalog's real draft setter.</summary>
internal partial class PersonalityEditorFieldView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private AdministrationPageChrome? chrome;
    private UiSourceLayout source = null!;
    private Control panel = null!, parameterHost = null!;
    private UiSourceCommandButton type = null!, reference = null!, number = null!;
    private UiSourceText minimumText = null!, maximumText = null!;
    private readonly List<UiSourceHitButton> steps = [];
    private UiSourceSlider? slider;
    private PersonalityEditorChoiceView? selector;
    private DeploymentCalculatorDialog? calculator;
    private IReadOnlyList<PersonalityEditorOptionRow> choices = [];
    private PersonalityEditorOptionRow? selected;
    private Action<PersonalityEditorOptionSelection?>? closed;
    private Action<int>? referenceRequested;
    private Func<bool>? admitted;
    private Func<int> scrollSpeed = () => (int)ListScrollSpeed.Normal;
    private int field, value, currentCode;
    private string currentCaption = "";
    private bool abandoned, busy;
    internal PersonalityEditorOptionSelection Working => new(currentCode, value);

    internal void Configure(GameResourceRepository repository, PreviewText text, int field,
        IReadOnlyList<PersonalityEditorOptionRow> choices, int originalCode, int originalValue,
        Func<bool> admitted, Action<PersonalityEditorOptionSelection?> closed, Func<int> scrollSpeed,
        Action<int>? referenceRequested = null, PersonalityEditorOptionRow? initialRow = null,
        string? initialCaption = null)
    {
        if (field is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(field));
        this.repository = repository; this.text = text; this.field = field; this.choices = choices;
        this.admitted = admitted; this.closed = closed; this.scrollSpeed = scrollSpeed; this.referenceRequested = referenceRequested;
        selected = choices.FirstOrDefault(row => row.Code == originalCode);
        selected ??= initialRow?.Code == originalCode ? initialRow : null;
        currentCode = originalCode;
        currentCaption = selected?.Caption ?? initialCaption ?? originalCode.ToString(CultureInfo.InvariantCulture);
        // Legacy imports may retain a code outside the current selector. Opening
        // its field must neither repair that code nor invent a parameter range.
        value = originalValue;
        Name = "PersonalityEditorField" + field; ZIndex = 8; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // ConditionOption is the source's direct list state, not another
        // effect editor with a made-up numeric field.
        if (field == 2) { OpenChoices(true); return; }
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, Title(field), RequestBack, Accept);
        source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_41_01_02_edit_kosei"));
        panel = new Control { Name = "OriginalPersonalityFieldPanel", Position = (new Vector2(1920, 904) - source.Rectangle(0).Size) / 2,
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(panel);
        foreach (int node in new[] { 2, 5, 6 }) source.Art(panel, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        source.Text(panel, 7, default, Caption(Title(field)), chrome.Font);
        type = Button(panel, 8, "PersonalityChooseCode", () => OpenChoices(false));
        reference = Button(panel, 41, "PersonalityReadReference", () =>
        { if (CanEdit() && currentCode != 0) this.referenceRequested?.Invoke(currentCode); });
        reference.SetMeta("reference_caption_unresolved", true); // Source static literal, never a fake copy command.
        parameterHost = new Control { Name = "PersonalityNumericParameter", MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(parameterHost);
        foreach (int node in new[] { 12, 15, 16, 18, 19, 24, 25, 27, 28 })
            source.Art(parameterHost, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        source.Text(parameterHost, 13, default, Caption(0x7FF), chrome.Font);
        minimumText = source.Text(parameterHost, 35, default, "", chrome.Font);
        maximumText = source.Text(parameterHost, 39, default, "", chrome.Font);
        number = Button(parameterHost, 29, "PersonalityParameterNumber", OpenCalculator);
        int[] nodes = [14, 17, 23, 26];
        for (int index = 0; index < nodes.Length; index++)
        {
            int action = index; var box = source.Rectangle(nodes[index]);
            var hit = new UiSourceHitButton { Name = "PersonalityParameterStep" + index, Position = box.Position,
                Size = box.Size, FocusMode = FocusModeEnum.None, IgnoreTextureSize = true };
            parameterHost.AddChild(hit); steps.Add(hit);
            hit.Pressed += () =>
            {
                if (!CanEdit() || selected is not { } row || !ParameterVisible) return;
                int next = action switch { 0 => row.Minimum, 1 => value - row.Step, 2 => value + row.Step, _ => row.Maximum };
                SetValue(next);
            };
        }
        RebuildSlider(); Refresh(); SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    private static int Title(int field) => field switch { 1 => 0x7FB, 2 => 0x7FC, 3 => 0x404, 4 => 0x405, _ => throw new ArgumentOutOfRangeException(nameof(field)) };
    private string Caption(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[id].Text
        ?? throw new InvalidDataException("Missing personality field caption.");
    private bool PageAdmitted() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    private bool CanEdit() => PageAdmitted() && selector is null && calculator is null;
    private static bool KnownRange(PersonalityEditorOptionRow row) => row.Minimum <= row.Maximum && row.Step > 0;
    private bool ParameterVisible => field != 1 && selected is { } row && KnownRange(row) && row.Minimum != row.Maximum;
    private bool CanConfirm => selected is { } row && KnownRange(row)
        && choices.Any(choice => choice.Code == currentCode) && value >= row.Minimum && value <= row.Maximum;
    private static int InitialValue(PersonalityEditorOptionRow row, int original)
    {
        if (row.Minimum > row.Maximum || row.Step <= 0) throw new InvalidDataException("Invalid source personality parameter range.");
        return original >= row.Minimum && original <= row.Maximum ? original : row.Maximum < 0 ? row.Maximum : row.Minimum;
    }
    private UiSourceCommandButton Button(Control host, int node, string name, Action action)
    {
        var box = source.Rectangle(node);
        var button = new UiSourceCommandButton { Name = name, Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
        host.AddChild(button); button.Configure(repository, chrome!.Assets, chrome.SpriteShader, chrome.Font, "", () => { if (CanEdit()) action(); });
        return button;
    }
    private void OpenChoices(bool direct)
    {
        if (!CanEdit()) return;
        var view = new PersonalityEditorChoiceView(); selector = view; AddChild(view);
        view.Configure(repository, text, Title(field), choices, currentCode,
            () => PageAdmitted() && selector == view, result =>
            {
                if (abandoned || selector != view) return;
                selector = null; view.Abandon(); view.QueueFree();
                if (result is null) { if (direct) Finish(null); else Refresh(); return; }
                selected = result; currentCode = result.Code; currentCaption = result.Caption;
                value = InitialValue(result, value);
                if (direct) { Finish(new(result.Code, value)); return; }
                RebuildSlider(); Refresh();
            }, scrollSpeed);
        Refresh();
    }
    private void RebuildSlider()
    {
        if (slider is not null) { slider.SetProcessInput(false); parameterHost.RemoveChild(slider); slider.QueueFree(); slider = null; }
        if (!ParameterVisible || selected is not { } row) return;
        var box = source.Rectangle(20);
        var view = new UiSourceSlider { Name = "PersonalityParameterSlider", Position = box.Position, Size = box.Size };
        slider = view; parameterHost.AddChild(view);
        view.Configure(repository, chrome!.Assets, chrome.SpriteShader, row.Minimum, row.Maximum, row.Step, value, true,
            next => { if (CanEdit() && slider == view) { value = next; Refresh(); } });
    }
    private void SetValue(int next)
    {
        if (selected is not { } row || !KnownRange(row)) return;
        next = Math.Clamp(next, row.Minimum, row.Maximum);
        value = next == row.Maximum ? next : Math.Clamp(unchecked(row.Step * (int)MathF.Round((float)next / row.Step,
            MidpointRounding.AwayFromZero)), row.Minimum, row.Maximum);
        Refresh();
    }
    private void OpenCalculator()
    {
        if (!CanEdit() || !ParameterVisible || selected is not { } row) return;
        bool negative = row.Maximum < 0;
        // Actual source choices separate positive and negative branches. The
        // shared integer keyboard edits magnitude for the negative branch.
        if (row.Minimum < 0 && !negative) return;
        int minimum = negative ? -row.Maximum : row.Minimum, maximum = negative ? -row.Minimum : row.Maximum;
        var view = new DeploymentCalculatorDialog(); calculator = view; AddChild(view); Refresh();
        view.Configure(repository, text, negative ? -value : value, minimum, maximum, result =>
        {
            if (calculator != view || abandoned) return;
            calculator = null; view.Abandon(); view.QueueFree();
            if (result is int amount) SetValue(negative ? -amount : amount); else Refresh();
        });
        view.Name = "PersonalityParameterCalculator";
    }
    private void Refresh()
    {
        if (abandoned || chrome is null) return;
        type.Caption = currentCaption; type.Disabled = !CanEdit();
        reference.Disabled = !CanEdit() || currentCode == 0 || referenceRequested is null;
        SetMeta("parameter_range_unresolved", selected is null || !KnownRange(selected));
        parameterHost.Visible = ParameterVisible;
        if (ParameterVisible && selected is { } row)
        {
            number.Caption = value.ToString(CultureInfo.InvariantCulture); number.Disabled = !CanEdit() || row.Minimum < 0 && row.Maximum >= 0;
            minimumText.Text = row.Minimum.ToString(CultureInfo.InvariantCulture); maximumText.Text = row.Maximum.ToString(CultureInfo.InvariantCulture);
            slider?.SetValue(value, row.Maximum, CanEdit()); foreach (var step in steps) step.Disabled = !CanEdit();
        }
        chrome.ApplyDecision(CanEdit() && CanConfirm, true); chrome.ApplyReturn(CanEdit());
    }
    private void Accept() { if (CanEdit() && CanConfirm) Finish(Working); }
    private void Finish(PersonalityEditorOptionSelection? result) { var callback = closed; Abandon(); callback?.Invoke(result); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void RequestBack()
    {
        if (calculator is not null) { calculator.RequestCancel(); return; }
        if (selector is not null) { selector.RequestBack(); return; }
        if (CanEdit()) Finish(null);
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; closed = null; referenceRequested = null;
        selector?.Abandon(); selector = null; calculator?.Abandon(); calculator = null;
        slider?.SetProcessInput(false); type?.Abandon(); reference?.Abandon(); number?.Abandon(); chrome?.Abandon(); Hide();
    }
}
