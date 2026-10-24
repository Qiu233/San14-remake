using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal enum TransportSetupQuantity { Troops, Gold, Supplies }
internal sealed record TransportSetupAmountState(int Value, int Minimum, int Maximum,
    int Step, bool Enabled, int CalculatorDefault, string LimitText,
    bool ValueKnown = true, bool ProfileKnown = true);
internal sealed record TransportSetupPresentation(
    string SourceName, int? ReadyTroops, int? Gold, int? Supplies,
    int? BooksBefore, int? BooksAfter, int? BookCost,
    AreaAdministrationOfficerValues Officer,
    TransportSetupAmountState TroopsAmount, TransportSetupAmountState GoldAmount,
    TransportSetupAmountState SuppliesAmount,
    bool CanChooseOfficer, bool CanAccept, string Hint);

/// <summary>Transport Make presentation and local quantity input. Current caps,
/// person facts and draft publication belong to the caller. This view neither
/// publishes a campaign order nor installs transport preference defaults.</summary>
internal partial class TransportSetupView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas = null!, body = null!;
    private UiSourceHeader header = null!;
    private UiSourceRoundButton confirm = null!, back = null!;
    private AreaAdministrationOfficerView officer = null!;
    private UiSourceText hint = null!;
    private readonly Dictionary<int, UiSourceText> labels = [];
    private readonly Dictionary<TransportSetupQuantity, UiSourceSlider> sliders = [];
    private readonly Dictionary<TransportSetupQuantity, UiSourceNumberButton> numbers = [];
    private readonly Dictionary<TransportSetupQuantity, TransportSetupAmountState> amounts = [];
    private readonly Dictionary<TransportSetupQuantity, Func<TransportSetupAmountState, UiSourceSlider>> sliderFactories = [];
    private Func<bool>? inputAdmitted;
    private Action? chooseOfficer, accepted, returned;
    private Action<TransportSetupQuantity, int>? amountChanged;
    private DeploymentCalculatorDialog? calculator;
    private TransportSetupPresentation? presentation;
    private bool configured, abandoned, busy;
    private string unknown = "";

    internal bool HasCalculator => calculator is not null;
    internal void RequestBack() { if (Admitted()) returned?.Invoke(); }
    internal void ApplyHint(string value) { if (!abandoned) hint.Text = value; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        TransportSetupPresentation initial, Action chooseOfficer,
        Action<TransportSetupQuantity, int> amountChanged, Action accepted,
        Action returned, Func<bool> inputAdmitted)
    {
        if (configured || assets is not null) throw new InvalidOperationException("Transport Make is configured once.");
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(inputAdmitted);
        this.repository = repository; this.text = text;
        this.chooseOfficer = chooseOfficer; this.amountChanged = amountChanged;
        this.accepted = accepted; this.returned = returned; this.inputAdmitted = inputAdmitted;
        Name = "TransportSetup"; ZIndex = 3; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        unknown = text.Get("detail_unknown");
        assets = new(repository); font = new(repository, text.SourceLocale);
        shader = UiSourceSprite.CreateShader();
        canvas = new Control { Name = "SourceCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Stop };
        AddChild(canvas);
        // Same CCmnBlur resource and static endpoint already used by the
        // ordinary Make view. Transport owns a separate backdrop control.
        var backdrop = new UiSourceLayout(repository, 0xAEF1609E);
        backdrop.Blur(canvas, 1, backdrop.Rectangle(1), weightInput: 100);
        backdrop.Blur(canvas, 2, backdrop.Rectangle(2), weightInput: 100);
        // The exact installed geometry is consumed, never an ordinary-Make
        // substitute. The first actual load is still a separate verification.
        var page = new UiSourceLayout(TransportSetupLayoutSource.Load(repository));
        body = new Control { Name = "TransportPage", Position = new(460, 230),
            Size = page.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Pass };
        canvas.AddChild(body);
        int[] replaced = [27, 96, 102, 113, 119, 130, 136];
        int[] hidden = [153, 163, 176, 189];
        foreach (var node in page.Nodes)
        {
            if (Under(node.Index, replaced) || Under(node.Index, hidden)) continue;
            if (node.Type is 1 or 3) page.Art(body, node.Index, page.Rectangle(node.Index), assets, shader);
            else if (node.Type == 6) page.Blur(body, node.Index, page.Rectangle(node.Index));
        }
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException($"Missing transport message {id}.");
        var presets = MapTextPresets.Create();
        foreach (var (node, message) in new[] { (5, 0x205), (10, 0x20E), (15, 0x20C), (20, 0x20D), (147, 0x292) })
            page.Text(body, node, default, Message(message), font, presets[13]);
        foreach (var (node, message) in new[] { (25, 0x541), (95, 0x20E), (112, 0x20C), (129, 0x20D) })
            page.Text(body, node, default, Message(message), font);
        foreach (int node in new[] { 6, 11, 16, 21, 108, 125, 142, 149, 151, 152 })
            labels[node] = page.Text(body, node, default, unknown, font, node is 149 or 151 or 152 ? presets[0] : null);

        var box = page.Rectangle(27);
        officer = new AreaAdministrationOfficerView { Name = "TransportOfficer", Position = box.Position, Size = box.Size };
        body.AddChild(officer);
        officer.Configure(repository, assets, shader, font, text,
            () => { if (Admitted() && presentation?.CanChooseOfficer == true) this.chooseOfficer?.Invoke(); }, body);
        if (officer.SelectionControl is UiSourceButton officerButton) officerButton.InputAdmission = Admitted;

        AddQuantity(TransportSetupQuantity.Troops, 96, 102, initial.TroopsAmount);
        AddQuantity(TransportSetupQuantity.Gold, 113, 119, initial.GoldAmount);
        AddQuantity(TransportSetupQuantity.Supplies, 130, 136, initial.SuppliesAmount);

        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        Vector2 footerMount = new(0, 1080 - footer.Rectangle(0).Size.Y);
        back = Round("ReturnTransport", 4, "000_cmn_txt_decision_modoru", UiAudioCue.Cancel,
            () => this.returned?.Invoke());
        confirm = Round("ConfirmTransport", 5, "000_cmn_txt_decision_kettei", UiAudioCue.ButtonSelect,
            () => { if (presentation?.CanAccept == true) this.accepted?.Invoke(); });
        var narration = new UiSourceLayout(repository, 0xAB6ADAA7);
        Vector2 hintMount = footer.Rectangle(19, footerMount).Position;
        foreach (var node in narration.Nodes.Where(n => n.Type is 1 or 3))
            narration.Art(canvas, node.Index, narration.Rectangle(node.Index, hintMount), assets, shader);
        hint = narration.Text(canvas, narration.Nodes.Single(n => n.Type == 2).Index, hintMount, "", font);
        header = new UiSourceHeader { Name = "TransportHeader" }; canvas.AddChild(header);
        header.Configure(repository, assets, shader, font, Message(0x26B));
        SetMeta("source_layout", page.Source.Source.ResourceUri);
        configured = true; Resized += Fit; Fit(); Apply(initial);

        bool Under(int node, int[] roots)
        {
            for (int? index = node; index is int i; index = page.Nodes[i].ParentIndex)
                if (roots.Contains(i)) return true;
            return false;
        }
        void AddQuantity(TransportSetupQuantity kind, int sliderNode, int buttonNode, TransportSetupAmountState value)
        {
            Validate(value);
            var sliderBox = page.Rectangle(sliderNode);
            UiSourceSlider BuildSlider(TransportSetupAmountState current)
            {
                var slider = new UiSourceSlider { Name = kind + "Slider", Position = sliderBox.Position, Size = sliderBox.Size };
                body.AddChild(slider);
                slider.Configure(repository, assets, shader, current.ProfileKnown ? current.Minimum : 0,
                    current.ProfileKnown ? current.Maximum : 0, current.ProfileKnown ? current.Step : 1,
                    current.ValueKnown && current.ProfileKnown ? current.Value : 0, false, selected =>
                {
                    if (Admitted() && amounts.GetValueOrDefault(kind) is { Enabled: true, ValueKnown: true, ProfileKnown: true })
                        this.amountChanged?.Invoke(kind, selected);
                });
                return slider;
            }
            sliderFactories.Add(kind, BuildSlider);
            sliders.Add(kind, BuildSlider(value));
            var numberBox = page.Rectangle(buttonNode);
            var number = new UiSourceNumberButton { Name = kind + "Number", Position = numberBox.Position, Size = numberBox.Size };
            body.AddChild(number); numbers.Add(kind, number);
            number.Configure(repository, assets, shader, font, value.Value, () => OpenCalculator(kind));
            number.InputAdmission = Admitted;
        }
        UiSourceRoundButton Round(string name, int node, string caption, UiAudioCue cue, Action action)
        {
            var round = new UiSourceRoundButton { Name = name, Position = footer.Rectangle(node, footerMount).Position };
            canvas.AddChild(round);
            round.Configure(repository, assets, shader, text.SourceLocale, caption,
                () => { if (Admitted()) action(); }, cue);
            round.InputAdmission = Admitted; return round;
        }
    }

    internal void Apply(TransportSetupPresentation value)
    {
        if (abandoned) return;
        if (!configured) throw new InvalidOperationException("Transport Make is not configured.");
        foreach (var (kind, next) in new[] { (TransportSetupQuantity.Troops, value.TroopsAmount),
            (TransportSetupQuantity.Gold, value.GoldAmount), (TransportSetupQuantity.Supplies, value.SuppliesAmount) })
        {
            Validate(next);
            // Source minimum/quantization are construction inputs. Recreate
            // this local leaf when actual knowledge/profile changes; an unknown
            // profile's disabled geometry is never exposed as known 0/1 rules.
            if (amounts.TryGetValue(kind, out var old) && (old.ProfileKnown != next.ProfileKnown ||
                next.ProfileKnown && (old.Minimum != next.Minimum || old.Step != next.Step)))
            {
                var previous = sliders[kind];
                previous.SetValue(0, 0, false); previous.Hide();
                body.RemoveChild(previous); previous.QueueFree();
                sliders.Remove(kind);
                sliders[kind] = sliderFactories[kind](next);
            }
            amounts[kind] = next;
        }
        presentation = value;
        labels[6].Text = value.SourceName;
        labels[11].Text = Number(value.ReadyTroops); labels[16].Text = Number(value.Gold);
        labels[21].Text = Number(value.Supplies); labels[149].Text = Number(value.BooksBefore);
        labels[151].Text = Number(value.BooksAfter);
        if (value.BookCost is int cost)
            labels[152].SetRuns(new("("), new("-" + Number(cost), MapTextPresets.Create()[40].LT.CpuPacked), new(")"));
        else labels[152].Text = unknown;
        labels[108].Text = value.TroopsAmount.LimitText;
        labels[125].Text = value.GoldAmount.LimitText;
        labels[142].Text = value.SuppliesAmount.LimitText;
        hint.Text = value.Hint; RefreshInput();
    }

    internal void RegisterHeader(UiSourceHeaderStack stack) => header.Publish(stack);
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; RefreshInput(); } }
    private bool Admitted() => configured && !abandoned && !busy && calculator is null &&
        IsInsideTree() && IsVisibleInTree() && !IsQueuedForDeletion() && CanProcess() && inputAdmitted?.Invoke() == true;

    private void RefreshInput()
    {
        if (!configured || presentation is null || abandoned) return;
        bool active = !busy && calculator is null;
        body.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        foreach (var (kind, value) in amounts)
        {
            bool known = value.ValueKnown && value.ProfileKnown;
            sliders[kind].SetValue(known ? value.Value : 0, value.ProfileKnown ? value.Maximum : 0,
                active && value.Enabled && known);
            numbers[kind].Caption = value.ValueKnown ? Number(value.Value) : unknown;
            numbers[kind].Disabled = !active || !value.Enabled || !known;
        }
        officer.Apply(presentation.Officer, active && presentation.CanChooseOfficer);
        confirm.Disabled = !active || !presentation.CanAccept; back.Disabled = !active;
    }

    private void OpenCalculator(TransportSetupQuantity kind)
    {
        if (!Admitted() || !amounts.TryGetValue(kind, out var value) ||
            !value.Enabled || !value.ValueKnown || !value.ProfileKnown) return;
        var dialog = new DeploymentCalculatorDialog(); calculator = dialog; AddChild(dialog);
        RefreshInput();
        try
        {
            //70B160: canonical parent origin plus(412,106).
            dialog.Configure(repository, text, value.Value, value.Minimum, value.Maximum, result =>
            {
                if (calculator != dialog) return;
                calculator = null; dialog.Hide(); dialog.QueueFree(); RefreshInput();
                if (result is int selected && Admitted() &&
                    amounts.GetValueOrDefault(kind) is { Enabled: true, ValueKnown: true, ProfileKnown: true })
                    amountChanged?.Invoke(kind, selected);
            }, body.Position + new Vector2(412, 106));
        }
        catch { calculator = null; dialog.Abandon(); dialog.QueueFree(); RefreshInput(); throw; }
    }

    internal bool TryCancelModal()
    { if (calculator is not { } dialog) return false; dialog.RequestCancel(); return true; }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true; chooseOfficer = accepted = returned = null; amountChanged = null; inputAdmitted = null;
        if (calculator is { } dialog) { calculator = null; dialog.Abandon(); dialog.QueueFree(); }
        foreach (var button in numbers.Values) button.Abandon();
        foreach (var slider in sliders.Values) slider.SetValue(0, 0, false);
        sliderFactories.Clear();
        officer?.Abandon();
        if (confirm is not null) confirm.Disabled = true;
        if (back is not null) back.Disabled = true;
        Hide();
    }
    private static void Validate(TransportSetupAmountState value)
    {
        if (value.ValueKnown && value.Value < 0 ||
            value.ProfileKnown && (value.Minimum < 0 || value.Step <= 0 ||
                value.Enabled && value.ValueKnown && (value.Maximum < value.Minimum || value.CalculatorDefault < value.Minimum || value.CalculatorDefault > value.Maximum)) ||
            value.LimitText is null)
            throw new InvalidDataException("Transport amount requires an explicit current source profile.");
    }
    private string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? unknown;
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - canvas.Size * scale) / 2;
    }
    public override void _ExitTree()
    { Abandon(); font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
