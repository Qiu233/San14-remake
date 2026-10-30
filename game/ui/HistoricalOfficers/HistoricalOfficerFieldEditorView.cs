using System.Collections.Immutable;
using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>A detached historical field edit. The parent alone adopts the
/// returned record; cancelling this page never writes the shared draft.</summary>
internal partial class HistoricalOfficerFieldEditorView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private HistoricalOfficerEditorCatalog catalog = null!;
    private HistoricalOfficerEditorDraft draft = null!;
    private HistoricalOfficerEdit working = null!;
    private AdministrationPageChrome? chrome;
    private CommandSelectionView? selector;
    private DeploymentCalculatorDialog? calculator;
    private OfficerPortraitSelectionView? portraitSelector;
    private Func<bool>? admitted;
    private Func<int> scrollSpeed = () => (int)ListScrollSpeed.Normal;
    private Action<HistoricalOfficerEdit>? accepted;
    private Action? cancelled;
    private readonly List<Action> refreshers = [];
    private readonly List<UiSourceCommandButton> buttons = [];
    private readonly List<UiSourceSlider> sliders = [];
    private bool abandoned, busy;
    private int section;
    internal HistoricalOfficerEdit Working => working;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        HistoricalOfficerEditorCatalog catalog, HistoricalOfficerEditorDraft draft, int section,
        Action<HistoricalOfficerEdit> accepted, Action cancelled, Func<bool>? admitted = null, Func<int>? scrollSpeed = null)
    {
        if (!HistoricalOfficerEditorCatalog.CanEdit(section)) throw new ArgumentOutOfRangeException(nameof(section));
        this.repository = repository; this.text = text; this.catalog = catalog; this.draft = draft;
        this.section = section; working = draft.Value; this.accepted = accepted; this.cancelled = cancelled; this.admitted = admitted;
        if (scrollSpeed is not null) this.scrollSpeed = scrollSpeed;
        Name = "HistoricalOfficerFieldEditor" + section; ZIndex = 7; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); SetMeta("historical_officer_id", working.Id);
        if (section is 2 or 4 or 7)
        {
            chrome = new(); AddChild(chrome); chrome.Configure(repository, text, Title(section), RequestBack, Accept);
            if (section == 7) BuildLifetime(); else BuildForm();
            Refresh();
        }
        else BuildSelection();
    }

    private static int Title(int section) => section switch
    { 2 => 0x452, 3 => 0x23D, 4 => 0x53, 5 => 0x234, 6 => 0x22B, 7 => 0x54, 9 => 0x2BC, 10 => 0x2BD, 12 => 0x42, _ => throw new ArgumentOutOfRangeException(nameof(section)) };
    private string Message(int id) => repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[id].Text
        ?? throw new InvalidDataException("Missing historical officer editor caption.");
    private bool CanEdit() => !abandoned && !busy && calculator is null && IsVisibleInTree() && (admitted?.Invoke() ?? true);
    private bool CanEditForm() => CanEdit() && selector is null;
    private void Refresh()
    {
        if (abandoned) return;
        foreach (var refresh in refreshers) refresh();
        bool valid;
        try { catalog.Validate(working); valid = true; }
        catch (InvalidDataException error) { valid = false; chrome?.ApplyDefaultHint(error.Message); }
        chrome?.ApplyDecision(!busy && calculator is null && selector is null && valid, true);
        chrome?.ApplyReturn(!busy && calculator is null && selector is null);
    }

    private void BuildForm()
    {
        var source = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadWindow(repository, section));
        var panel = Panel(source); source.Art(panel, 2, source.Rectangle(2), chrome!.Assets, chrome.SpriteShader);
        if (section == 2)
        {
            // The resource's five real numeric groups retain their authored mounts.
            for (int index = 0; index < 5; index++)
            {
                int ability = index, root = 16 + index * 18;
                NumericRow(panel, source, index, root + 3, root + 4, root + 9, root + 16,
                    [root + 1, root + 2, root + 6, root + 8, root + 13, root + 15],
                    [root + 8, root + 6, root + 15, root + 13], 0x238 + index,
                    () => Ability(ability), 0, () => 100, value => SetAbility(ability, value));
            }
        }
        else
        {
            foreach (int node in new[] { 4, 8, 11, 15 }) source.Art(panel, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
            source.Text(panel, 5, default, Message(0x53), chrome.Font);
            source.Text(panel, 12, default, Message(0x241), chrome.Font);
            ChoiceButton(6, "HistoricalOfficerDoctrine", 0x53, catalog.Doctrines, () => working.DoctrineId,
                value => working = working with { DoctrineId = (byte)value });
            ChoiceButton(13, "HistoricalOfficerPolicy", 0x241, catalog.Policies, () => working.PolicyId,
                value =>
                {
                    working = working with { PolicyId = (byte)value };
                    working = working with { PolicyBaseCost = (byte)Math.Clamp((int)working.PolicyBaseCost, 1, catalog.PolicyCostMaximum(working)) };
                });
            NumericRow(panel, source, 0, 19, 41, 26, 35,
                [18, 21, 22, 24, 25, 30, 31, 33, 34], [24, 21, 33, 30], 0x250,
                () => working.PolicyBaseCost, 1, () => catalog.PolicyCostMaximum(working),
                value => working = working with { PolicyBaseCost = (byte)value });
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        void ChoiceButton(int node, string name, int title, IReadOnlyList<RegisteredOfficerEditorChoice> options,
            Func<int> read, Action<int> write)
        {
            var box = source.Rectangle(node);
            var button = new UiSourceCommandButton { Name = name, Position = box.Position, Size = box.Size, InputAdmission = CanEditForm };
            panel.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", () =>
            {
                if (!CanEditForm()) return;
                OpenChoice(options, [read()], 1, true, title, selected =>
                { write(selected.Single()); Refresh(); });
            });
            refreshers.Add(() => { button.Caption = options.FirstOrDefault(option => option.Id == read())?.Name ?? ""; button.Disabled = !CanEditForm(); });
        }
    }

    private Control Panel(UiSourceLayout source)
    {
        var size = source.Rectangle(0).Size;
        var panel = new Control { Name = "HistoricalOfficerSourceWindow", Size = size,
            Position = (new Vector2(1920, 904) - size) / 2, MouseFilter = MouseFilterEnum.Ignore };
        chrome!.Canvas.AddChild(panel); return panel;
    }

    private void NumericRow(Control panel, UiSourceLayout source, int id, int captionNode, int valueNode,
        int trackNode, int numberNode, int[] art, int[] steps, int caption, Func<int> read,
        int minimum, Func<int> maximum, Action<int> write, bool enabled = true)
    {
        foreach (int node in art) source.Art(panel, node, source.Rectangle(node), chrome!.Assets, chrome.SpriteShader);
        source.Text(panel, captionNode, default, Message(caption), chrome!.Font);
        var valueText = source.Text(panel, valueNode, default, "", chrome.Font);
        var box = source.Rectangle(trackNode);
        var slider = new UiSourceSlider { Name = "HistoricalOfficerSlider" + id, Position = box.Position, Size = box.Size };
        panel.AddChild(slider); sliders.Add(slider); slider.Configure(repository, chrome.Assets, chrome.SpriteShader, minimum, maximum(), 1, read(),
            enabled && minimum < maximum(), value => { if (CanEditForm()) { write(value); Refresh(); } });
        var numberBox = source.Rectangle(numberNode);
        var number = new UiSourceCommandButton { Name = "HistoricalOfficerNumber" + id, Position = numberBox.Position,
            Size = numberBox.Size, InputAdmission = CanEditForm };
        panel.AddChild(number); buttons.Add(number);
        number.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", () =>
        {
            if (!CanEditForm() || !enabled || maximum() < minimum || minimum < 0) return;
            var view = new DeploymentCalculatorDialog(); calculator = view; AddChild(view); Refresh();
            view.Configure(repository, text, read(), minimum, maximum(), result =>
            {
                if (calculator != view) return;
                calculator = null; view.Abandon(); view.QueueFree();
                if (abandoned) return;
                if (result is int value) write(value); Refresh();
            });
            view.Name = "HistoricalOfficerCalculator";
        });
        var hits = new List<UiSourceHitButton>();
        for (int mode = 0; mode < steps.Length; mode++)
        {
            int action = mode; var rectangle = source.Rectangle(steps[mode]);
            var hit = new UiSourceHitButton { Name = "HistoricalOfficerStep" + id + "_" + action,
                Position = rectangle.Position, Size = rectangle.Size, IgnoreTextureSize = true, FocusMode = FocusModeEnum.None };
            panel.AddChild(hit); hits.Add(hit);
            hit.Pressed += () =>
            {
                if (!CanEditForm() || !enabled || minimum > maximum()) return;
                int value = action switch { 0 => minimum, 1 => read() - 1, 2 => read() + 1, _ => maximum() };
                write(Math.Clamp(value, minimum, maximum())); Refresh();
            };
        }
        refreshers.Add(() =>
        {
            bool active = enabled && CanEditForm() && minimum <= maximum();
            valueText.Text = read().ToString(System.Globalization.CultureInfo.InvariantCulture);
            slider.SetValue(read(), maximum(), active && minimum < maximum()); number.Disabled = !active || minimum < 0;
            foreach (var hit in hits) hit.Disabled = !active;
        });
    }

    private void BuildSelection()
    {
        if(section==12)
        {
            var picker=new OfficerPortraitSelectionView();portraitSelector=picker;AddChild(picker);
            picker.Configure(repository,text,catalog.PortraitCatalog,working.PortraitId,CanEdit,chosen=>
            {
                if(abandoned||portraitSelector!=picker)return;
                portraitSelector=null;RemoveChild(picker);picker.QueueFree();
                if(chosen is not int appearance){Cancel();return;}
                working=working with{PortraitId=appearance};Accept();
            },scrollSpeed);
            return;
        }
        IReadOnlyList<RegisteredOfficerEditorChoice> choices = section switch
        { 3 => catalog.Personalities.Where(choice => draft.Row.Sex != 0 || !catalog.IsFemaleOnlyTrait(choice.Id) || working.TraitIds.Contains(choice.Id)).ToArray(),
            5 => catalog.Tactics, 6 => catalog.Formations, 9 or 10 => catalog.Officers, 12 => catalog.Portraits, _ => throw new InvalidOperationException() };
        var initial = (section switch
        { 3 => working.TraitIds, 5 => working.TacticIds, 6 => working.FormationIds, 9 => working.LikePersonIds,
            10 => working.DislikePersonIds, _ => ImmutableArray.Create(working.PortraitId) }).Where(id => section == 6 || id != 0).ToArray();
        int maximum = section switch { 3 or 6 => 5, 5 => 10, 9 or 10 => 8, _ => 1 };
        OpenChoice(choices, initial, maximum, section is 6 or 12, Title(section), selected =>
        {
            ImmutableArray<int> Slots(int count) => [.. selected, .. Enumerable.Repeat(0, count - selected.Count)];
            working = section switch
            { 3 => working with { TraitIds = Slots(9) }, 5 => working with { TacticIds = Slots(10) },
                6 => working with { FormationIds = [.. selected] }, 9 => working with { LikePersonIds = Slots(8) },
                10 => working with { DislikePersonIds = Slots(8) }, _ => working with { PortraitId = selected.Single() } };
            Accept();
        }, closePageOnCancel: true);
    }

    private void OpenChoice(IReadOnlyList<RegisteredOfficerEditorChoice> choices, IReadOnlyList<int> initial,
        int maximum, bool requireSelection, int title, Action<IReadOnlyList<int>> apply, bool closePageOnCancel = false)
    {
        // Occurrences retain duplicate portrait rows; they are not historical identities.
        var rows = choices.Select((choice, index) => new CommandSelectionRow(index,
            [new CommandSelectionCell(choice.Name, SortUnavailable: true)], true)).ToArray();
        var initialRows = initial.Select(id => choices.ToList().FindIndex(choice => choice.Id == id)).Where(index => index >= 0).Distinct().Take(maximum).ToArray();
        var view = new CommandSelectionView(); selector = view; AddChild(view);
        view.Configure(repository, text, title, [new("choice", Message(title), 1416)], rows, initialRows, maximum,
            () => CanEdit() && selector == view, result =>
            {
                if (selector != view || abandoned) return;
                selector = null; view.Abandon(); view.QueueFree();
                if (result is null) { if (closePageOnCancel) Cancel(); else Refresh(); return; }
                apply(result.Select(index => choices[index].Id).ToArray());
            }, scrollSpeed, requireSelection: requireSelection, singleSelection: maximum == 1);
        view.Name = "HistoricalOfficerChoices" + section;
    }

    private int Ability(int index) => index switch { 0 => working.Leadership, 1 => working.Strength, 2 => working.Intelligence, 3 => working.Politics, _ => working.Charm };
    private void SetAbility(int index, int value) => working = index switch
    { 0 => working with { Leadership = (byte)value }, 1 => working with { Strength = (byte)value },
        2 => working with { Intelligence = (byte)value }, 3 => working with { Politics = (byte)value }, _ => working with { Charm = (byte)value } };
    private void Accept()
    {
        if (!CanEditForm()) return;
        try { catalog.Validate(working); }
        catch (InvalidDataException error) { chrome?.ApplyDefaultHint(error.Message); return; }
        var callback = accepted; var value = working; Abandon(); callback?.Invoke(value);
    }
    private void Cancel() { if (!CanEdit()) return; var callback = cancelled; Abandon(); callback?.Invoke(); }
    internal void RequestBack()
    {
        if (portraitSelector is not null) { portraitSelector.RequestCancel(); return; }
        if (calculator is not null) { calculator.RequestCancel(); return; }
        if (selector is not null) { selector.RequestCancel(); return; }
        Cancel();
    }
    internal void RequestCancel() => RequestBack();
    internal void SetBusy(bool value) { busy = value; Refresh(); }
    internal void Abandon()
    {
        abandoned = true; accepted = null; cancelled = null; admitted = null;
        calculator?.Abandon(); calculator = null; selector?.Abandon(); selector = null;
        portraitSelector?.Abandon(); portraitSelector = null;
        foreach (var slider in sliders) slider.SetProcessInput(false);
        foreach (var button in buttons) button.Abandon(); chrome?.Abandon(); Hide();
    }
}
