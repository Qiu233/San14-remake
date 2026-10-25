using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record StrategicOrdersChoice(int Value, string Caption, bool Enabled);
internal sealed record StrategicOrdersPageValues(string TargetCaption, string SecondForceCaption,
    AreaAdministrationOfficerValues Executor, IReadOnlyDictionary<int, string> Text,
    int? MilitaryIntelligence, int? MilitaryMinimum, int? MilitaryMaximum, int? MilitaryStep, int? RequiredMilitaryIntelligence,
    bool CanChooseTarget, bool CanChooseSecondForce, bool CanChooseExecutor, bool CanEditMilitaryIntelligence,
    int? IndependenceChoice, IReadOnlyList<StrategicOrdersChoice> IndependenceChoices,
    bool CanConfirm, bool CanContinue, string Notice, LoadedSprite? AdvisorPortrait);

/// <summary>The three authored strategic order panes. Candidate selection,
/// budgets and previews are supplied by one retained command draft.</summary>
internal partial class StrategicOrdersView : Control
{
    private AdministrationPageChrome chrome = null!;
    private AreaAdministrationOfficerView executor = null!;
    private UiSourceCommandButton target = null!;
    private UiSourceCommandButton? secondForce;
    private UiSourceNumberButton amount = null!;
    private UiSourceSlider slider = null!;
    private UiSourceAdviceView advice = null!;
    private UiSourceRoundButton continueOrders = null!;
    private UiSourceHitButton requiredMilitary = null!;
    private UiSourceText requiredText = null!;
    private Vector2 markerOrigin;
    private readonly Dictionary<int, UiSourceText> fields = [];
    private readonly List<UiSourceRadioButton> independence = [];
    private Func<bool>? admitted;
    private Action? returned;
    private Action<int>? militaryChanged;
    private StrategicOrdersPageValues? publication;
    private bool abandoned, busy;
    private string unknown = "";
    private int sliderMinimum, sliderStep;
    private Func<int, int, UiSourceSlider> sliderFactory = null!;

    internal void Configure(GameResourceRepository repository, PreviewText text, int kind,
        Func<bool> admitted, Action returned, Action accepted, Action continued,
        Action chooseTarget, Action chooseSecondForce, Action chooseExecutor, Action calculateMilitary,
        Action<int> militaryChanged, Action<int> independenceChanged)
    {
        this.admitted = admitted; this.returned = returned; this.militaryChanged = militaryChanged;
        unknown = text.Get("detail_unknown");
        Name = "StrategicOrders"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, kind + 0x127, RequestBack,
            () => { if (ReadAdmission() && publication?.CanConfirm == true) accepted(); }, threeButtonFooter: true, titleSection: 0);
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        continueOrders = new() { Name = "ContinueStrategicOrders", Position = footer.Rectangle(3, new(0, 904)).Position,
            ActionMode = BaseButton.ActionModeEnum.Release, InputAdmission = ReadAdmission, Disabled = true };
        chrome.Canvas.AddChild(continueOrders);
        continueOrders.ConfigureContinue(repository, chrome.Assets, chrome.SpriteShader, text.SourceLocale,
            () => { if (ReadAdmission() && publication?.CanContinue == true) continued(); });

        var (file, mount) = kind switch
        {
            10 => (0x06ED6A40u, new Vector2(434, 161)),
            11 => (0x913B1D70u, new Vector2(434, 163)),
            12 or 13 or 14 or 20 or 24 => (0x425B8821u, new Vector2(434, 191)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var source = new UiSourceLayout(repository, file);
        bool doubleTiger = kind == 10;
        var captions = CaptionMessages(kind);
        var hiddenRoots = new HashSet<int>();
        if (!doubleTiger && kind is not (11 or 24)) hiddenRoots.Add(19);
        int cardNode = doubleTiger ? 41 : 32, sliderNode = doubleTiger ? 74 : 65;
        int faceNode = doubleTiger ? 145 : kind == 11 ? 149 : 132;
        var replaced = new HashSet<int>(hiddenRoots) { doubleTiger ? 7 : 6, cardNode, sliderNode, faceNode };
        if (doubleTiger) replaced.Add(17);
        if (kind == 11) replaced.UnionWith([108, 112, 116]);
        foreach (var node in source.Nodes)
        {
            if (IsHidden(node.Index)) continue;
            if (node.Type is 1 or 3)
                source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
            else if (node.Type == 6) source.Blur(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount));
        }
        var messages = new AdministrationMessages(repository, text.SourceLocale);
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        foreach (var caption in captions)
            Field(caption.Key).Text = data.Records[caption.Value].Text ?? throw new InvalidDataException("Missing strategic page caption.");
        // Dynamic fields are actual authored text panes; no text is estimated
        // from a model or recaptured while this page is displaying a draft.
        int[] values = doubleTiger ? [31, 89, 113, 121, 123, 124, 130, 131, 137, 139, 140]
            : kind == 11 ? [18, 25, 78, 100, 125, 127, 128, 134, 135, 141, 143, 144]
            : [18, 25, 78, 100, 108, 110, 111, 117, 118, 124, 126, 127];
        foreach (int node in values)
            if (!hiddenRoots.Any(root => IsDescendant(node, root))) Field(node);
        target = Command(doubleTiger ? 7 : 6, chooseTarget);
        if (doubleTiger) secondForce = Command(17, chooseSecondForce);
        executor = new() { Position = source.Rectangle(cardNode, mount).Position };
        chrome.Canvas.AddChild(executor);
        executor.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, text,
            () => { if (ReadAdmission() && publication?.CanChooseExecutor == true) chooseExecutor(); }, chrome.Canvas);
        advice = new(); chrome.Canvas.AddChild(advice);
        advice.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, source, faceNode, mount);

        int numberNode = doubleTiger ? 85 : 74, trackNode = doubleTiger ? 83 : 72;
        // The source slider has its own authored track and numeric-button mount.
        var numberBox = source.Rectangle(numberNode, mount);
        amount = new() { Position = numberBox.Position, Size = numberBox.Size, InputAdmission = ReadAdmission, Disabled = true };
        chrome.Canvas.AddChild(amount);
        amount.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, 0,
            () => { if (ReadAdmission() && publication?.CanEditMilitaryIntelligence == true) calculateMilitary(); });
        var track = source.Rectangle(trackNode, mount);
        sliderFactory = (minimum, step) =>
        {
            var control = new UiSourceSlider { Position = track.Position, Size = track.Size };
            chrome.Canvas.AddChild(control);
            control.Configure(repository, chrome.Assets, chrome.SpriteShader, minimum, minimum, step, minimum, false,
                value => { if (ReadAdmission() && publication?.CanEditMilitaryIntelligence == true) this.militaryChanged?.Invoke(value); });
            return control;
        };
        slider = sliderFactory(0, 1); sliderMinimum = 0; sliderStep = 1;
        int markerNode = doubleTiger ? 79 : 69;
        var marker = source.Rectangle(markerNode, mount); markerOrigin = marker.Position;
        requiredMilitary = new() { Position = marker.Position, Size = marker.Size, IgnoreTextureSize = true, FocusMode = FocusModeEnum.None };
        chrome.Canvas.AddChild(requiredMilitary);
        var artBox = source.Rectangle(markerNode + 1, mount); artBox.Position -= marker.Position;
        source.Art(requiredMilitary, markerNode + 1, artBox, chrome.Assets, chrome.SpriteShader);
        var labelBox = source.Rectangle(markerNode + 2, mount); labelBox.Position -= marker.Position;
        requiredText = source.Text(requiredMilitary, markerNode + 2, default, "", chrome.Font, rectangle: labelBox);
        requiredMilitary.Pressed += () =>
        {
            if (ReadAdmission() && publication is { CanEditMilitaryIntelligence: true, RequiredMilitaryIntelligence: int required })
                this.militaryChanged?.Invoke(required);
        };
        if (kind == 11)
        {
            int[] nodes = [108, 112, 116];
            for (int index = 0; index < nodes.Length; index++)
            {
                int choice = index; var box = source.Rectangle(nodes[index], mount);
                var button = new UiSourceRadioButton { Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
                chrome.Canvas.AddChild(button);
                button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                    index == 0 ? UiSourceRadioSegment.Left : index == 2 ? UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre,
                    "", () =>
                    {
                        if (ReadAdmission() && publication is { } current && choice < current.IndependenceChoices.Count)
                            independenceChanged(current.IndependenceChoices[choice].Value);
                    });
                independence.Add(button);
            }
        }
        chrome.ApplyReturnHint(messages.Format(0x2733));
        SetMeta("source_layout", source.Source.Source.ResourceUri);

        bool IsHidden(int index)
        {
            for (int? parent = index; parent is int current; parent = source.Nodes[current].ParentIndex)
                if (replaced.Contains(current)) return true;
            return false;
        }
        bool IsDescendant(int index, int root)
        {
            for (int? parent = index; parent is int current; parent = source.Nodes[current].ParentIndex)
                if (current == root) return true;
            return false;
        }
        UiSourceText Field(int index)
        {
            if (!fields.TryGetValue(index, out var field))
            {
                field = source.Text(chrome.Canvas, index, mount, "", chrome.Font);
                fields.Add(index, field);
            }
            return field;
        }
        UiSourceCommandButton Command(int index, Action clicked)
        {
            var box = source.Rectangle(index, mount);
            var button = new UiSourceCommandButton { Position = box.Position, Size = box.Size, InputAdmission = ReadAdmission };
            chrome.Canvas.AddChild(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "",
                () => { if (ReadAdmission()) clicked(); });
            return button;
        }
    }

    internal void Apply(StrategicOrdersPageValues value)
    {
        if (abandoned) return;
        publication = value;
        foreach (var pair in value.Text)
            if (fields.TryGetValue(pair.Key, out var field)) field.SetKnownColourLine(pair.Value);
        target.Caption = value.TargetCaption; target.Disabled = busy || !value.CanChooseTarget;
        if (secondForce is not null)
        { secondForce.Caption = value.SecondForceCaption; secondForce.Disabled = busy || !value.CanChooseSecondForce; }
        executor.Apply(value.Executor, !busy && value.CanChooseExecutor);
        bool profileKnown = value.MilitaryMinimum is int minimum && value.MilitaryMaximum is int maximum &&
            minimum <= maximum && value.MilitaryStep is > 0;
        amount.Caption = value.MilitaryIntelligence?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? unknown;
        amount.Disabled = busy || !value.CanEditMilitaryIntelligence || !profileKnown;
        if (profileKnown && (sliderMinimum != value.MilitaryMinimum || sliderStep != value.MilitaryStep))
        {
            slider.GetParent()?.RemoveChild(slider); slider.QueueFree();
            sliderMinimum = value.MilitaryMinimum.GetValueOrDefault(); sliderStep = value.MilitaryStep.GetValueOrDefault();
            slider = sliderFactory(sliderMinimum, sliderStep);
        }
        slider.SetValue(value.MilitaryIntelligence ?? 0, value.MilitaryMaximum ?? 0,
            !busy && value.CanEditMilitaryIntelligence && profileKnown && value.MilitaryIntelligence is not null);
        requiredMilitary.Visible = value.RequiredMilitaryIntelligence is > 0 &&
            value.RequiredMilitaryIntelligence <= value.MilitaryMaximum;
        requiredMilitary.Disabled = busy || !value.CanEditMilitaryIntelligence || !profileKnown ||
            value.RequiredMilitaryIntelligence < value.MilitaryMinimum || value.RequiredMilitaryIntelligence > value.MilitaryMaximum;
        if (value.RequiredMilitaryIntelligence is int required)
        {
            requiredText.Text = required.ToString(System.Globalization.CultureInfo.InvariantCulture);
            requiredMilitary.Position = markerOrigin + new Vector2(slider.PositionForValue(required), 0);
        }
        for (int index = 0; index < independence.Count; index++)
        {
            bool available = index < value.IndependenceChoices.Count;
            independence[index].Visible = available;
            independence[index].Disabled = busy || !available || !value.IndependenceChoices[index].Enabled;
            if (available)
            {
                independence[index].Caption = value.IndependenceChoices[index].Caption;
                independence[index].Selected = value.IndependenceChoice == value.IndependenceChoices[index].Value;
            }
        }
        advice.Apply(value.AdvisorPortrait, value.Notice);
        chrome.ApplyDecision(!busy && value.CanConfirm, true); chrome.ApplyReturn(!busy);
        continueOrders.Disabled = busy || !value.CanContinue;
    }

    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void SetBusy(bool value) { busy = value; if (publication is { } current) Apply(current); }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; militaryChanged = null; publication = null;
        if (slider is not null) slider.SetValue(0, 0, false);
        target?.Abandon(); secondForce?.Abandon(); executor?.Abandon();
        foreach (var button in independence) button.Abandon();
        if (continueOrders is not null) continueOrders.Disabled = true;
        if (amount is not null) amount.Disabled = true;
        chrome?.Abandon(); Hide();
    }

    private static IReadOnlyDictionary<int, int> CaptionMessages(int kind)
    {
        if (kind == 10) return new Dictionary<int, int>
        {
            [6] = 0x632, [16] = 0x636, [27] = 0x413, [39] = 0x541,
            [73] = 0x835, [112] = 0x846, [119] = 0x20C, [128] = 0x26C,
            [131] = 0x469, [135] = 0x292
        };
        var values = new Dictionary<int, int>
        {
            [5] = kind == 11 ? 0x478 : kind is 12 or 24 ? 0x542 : 0x477,
            [15] = kind == 11 ? 0x215 : kind == 12 ? 0x21D : 0x1FD,
            [30] = 0x541, [64] = 0x835, [99] = 0x846,
            [kind == 11 ? 123 : 106] = 0x20C,
            [kind == 11 ? 132 : 115] = 0x26C,
            [kind == 11 ? 135 : 118] = 0x469,
            [kind == 11 ? 139 : 122] = 0x292
        };
        if (kind == 11) { values[22] = 0x1FD; values[105] = 0x85C; }
        if (kind == 24) values[22] = 0x219;
        return values;
    }
}
