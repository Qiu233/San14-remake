using System.Globalization;
using Godot;
using San14.Core;
using San14.Data;

/// <summary>The ordinary bulk birth dialog. The reference year is editable;
/// its original lifespan remains fixed throughout this bulk operation.</summary>
internal partial class RegisteredOfficerBirthYearView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceSlider birthSlider = null!, lifespanSlider = null!;
    private UiSourceCommandButton birthNumber = null!, lifespanNumber = null!;
    private readonly List<UiSourceHitButton> arrows = [];
    private readonly Dictionary<int, UiSourceText> fields = [];
    private readonly List<(RegisteredOfficerBirthYearScenario Scenario, UiSourceText Age)> history = [];
    private RegisteredOfficerBirthYearPlan plan = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private DeploymentCalculatorDialog? calculator;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<int>? accepted;
    private int birthYear;
    private bool busy, abandoned;
    internal int BirthYear => birthYear;
    private bool HasRange => plan.MinimumBirthYear <= plan.MaximumBirthYear;

    internal void Configure(GameResourceRepository repository, PreviewText text, RegisteredOfficer reference,
        RegisteredOfficerBirthYearPlan plan, IReadOnlyList<RegisteredOfficerBirthYearScenario> scenarios,
        Func<bool> admitted, Action returned, Action<int> accepted)
    {
        this.repository = repository; this.text = text; this.plan = plan;
        this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        birthYear = plan.OriginalBirthYear;
        Name = "RegisteredOfficerBirthYearEditor"; ZIndex = 7; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x5DE, RequestBack, Accept);
        var source = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadWindow(repository, 7));
        var panel = new Control { Name = "RegisteredOfficerBirthYearSourceWindow", Size = source.Rectangle(0).Size,
            Position = (new Vector2(1920, 904) - source.Rectangle(0).Size) / 2, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(panel);
        foreach (int index in new[] { 2, 4, 5, 9, 11, 16, 18, 22, 23, 27, 29, 34, 36, 39, 40, 41, 44, 45, 48 })
            source.Art(panel, index, source.Rectangle(index), chrome.Assets, chrome.SpriteShader);
        foreach (int index in new[] { 6, 7, 24, 25, 42, 43, 46, 47 })
            fields.Add(index, source.Text(panel, index, default, "", chrome.Font));
        fields[6].Text = Message(0x4E7); fields[24].Text = Message(0x4C6);
        fields[42].Text = Message(0x4E9); fields[46].Text = Message(0x4E8);
        fields[25].Text = plan.Lifespan.ToString(CultureInfo.InvariantCulture);
        birthSlider = Slider(12, plan.MinimumBirthYear, plan.MaximumBirthYear, birthYear, true,
            value => { if (CanEdit()) { birthYear = value; Refresh(); } });
        lifespanSlider = Slider(30, 30, 99, plan.Lifespan, false, null);
        birthNumber = Number(19, "BirthYearNumber", OpenCalculator);
        lifespanNumber = Number(37, "BirthYearLifespan", () => { }); lifespanNumber.Disabled = true;
        Arrow(8, "Previous", () => birthYear - 1);
        Arrow(10, "Minimum", () => plan.MinimumBirthYear);
        Arrow(17, "Next", () => birthYear + 1);
        Arrow(15, "Maximum", () => plan.MaximumBirthYear);
        BuildHistory(source.Rectangle(49).Position);
        chrome.ApplyDefaultHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x281B));
        SetMeta("source_layout", source.Source.Source.ResourceUri); SetMeta("reference_officer", reference.Id);
        Refresh();

        UiSourceSlider Slider(int index, int minimum, int maximum, int value, bool enabled, Action<int>? changed)
        {
            var box = source.Rectangle(index);
            var slider = new UiSourceSlider { Name = index == 12 ? "BirthYearSlider" : "BirthYearLifespanSlider",
                Position = box.Position, Size = box.Size };
            panel.AddChild(slider); slider.Configure(repository, chrome.Assets, chrome.SpriteShader,
                minimum, maximum, 1, value, enabled && HasRange, changed);
            return slider;
        }
        UiSourceCommandButton Number(int index, string name, Action activated)
        {
            var box = source.Rectangle(index);
            var button = new UiSourceCommandButton { Name = name, Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            panel.AddChild(button); button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                "", activated);
            button.SetMeta("caption_source_pending", true);
            return button;
        }
        void Arrow(int index, string name, Func<int> value)
        {
            var box = source.Rectangle(index);
            var hit = new UiSourceHitButton { Name = "BirthYear" + name, Position = box.Position, Size = box.Size,
                IgnoreTextureSize = true, FocusMode = FocusModeEnum.None };
            panel.AddChild(hit); arrows.Add(hit);
            hit.Pressed += () => { if (CanEdit() && HasRange) { birthYear = Math.Clamp(value(),
                plan.MinimumBirthYear, plan.MaximumBirthYear); Refresh(); } };
        }
        void BuildHistory(Vector2 mount)
        {
            var layout = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_05_01_00_history_offset_00"));
            var host = new Control { Name = "BirthYearScenarioHistory", Position = mount,
                Size = layout.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            panel.AddChild(host);
            for (int row = 0; row < Math.Min(12, scenarios.Count); row++)
            {
                var scenario = scenarios[row];
                // The original fills the six left rows, then the six right rows.
                int first = 1 + row % 6 * 6 + row / 6 * 3;
                layout.Text(host, first, default,
                    Format(Message(0x569), scenario.StartYear), chrome.Font);
                layout.Text(host, first + 1, default, scenario.Name, chrome.Font);
                var age = layout.Text(host, first + 2, default, "", chrome.Font);
                history.Add((scenario, age));
            }
            SetMeta("history_source_layout", layout.Source.Source.ResourceUri);
        }
    }

    private string Message(int id) => repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data)
        .Messages.Sections[1].Records[id].Text ?? throw new InvalidDataException("Missing birth-year editor caption.");
    private static string Format(string format, int value) =>
        format.Replace("%d", value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    private bool CanEdit() => !abandoned && !busy && calculator is null && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private void Refresh()
    {
        if (abandoned) return;
        fields[7].Text = birthYear.ToString(CultureInfo.InvariantCulture);
        fields[43].Text = (birthYear + 14).ToString(CultureInfo.InvariantCulture);
        int death = birthYear + plan.Lifespan - 1;
        fields[47].Text = death.ToString(CultureInfo.InvariantCulture);
        birthSlider.SetValue(birthYear, plan.MaximumBirthYear, !busy && HasRange && calculator is null);
        lifespanSlider.SetValue(plan.Lifespan, 99, false);
        birthNumber.Disabled = busy || !HasRange || calculator is not null;
        foreach (var arrow in arrows) arrow.Disabled = busy || !HasRange || calculator is not null;
        foreach (var (scenario, age) in history)
            age.Text = scenario.StartYear < birthYear ? Message(0x55C) : death < scenario.StartYear
                ? Message(0x55D) : Format(Message(0x55E), scenario.StartYear - birthYear + 1);
        chrome.ApplyReturn(!busy && calculator is null);
        chrome.ApplyDecision(!busy && calculator is null && HasRange && birthYear >= plan.MinimumBirthYear && birthYear <= plan.MaximumBirthYear, true);
    }
    private void OpenCalculator()
    {
        if (!CanEdit() || !HasRange) return;
        var view = new DeploymentCalculatorDialog(); calculator = view; AddChild(view); Refresh();
        view.Configure(repository, text, birthYear, plan.MinimumBirthYear, plan.MaximumBirthYear, result =>
        {
            if (calculator != view) return;
            calculator = null; view.Abandon(); view.QueueFree();
            if (abandoned) return;
            if (result is int year) birthYear = year;
            Refresh();
        });
        view.Name = "RegisteredOfficerBirthYearCalculator";
    }
    private void Accept() { if (CanEdit() && HasRange && birthYear >= plan.MinimumBirthYear && birthYear <= plan.MaximumBirthYear) accepted?.Invoke(birthYear); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack()
    {
        if (calculator is not null) { calculator.RequestCancel(); return; }
        if (CanEdit()) returned?.Invoke();
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; accepted = null;
        calculator?.Abandon(); calculator = null;
        foreach (var arrow in arrows) arrow.Disabled = true;
        birthNumber?.Abandon(); lifespanNumber?.Abandon(); chrome?.Abandon(); Hide();
    }
}
