using Godot;
using San14.Client.Gameplay;
using San14.Data;
using San14.Data.Formats;

/// <summary>The income dialog's numeric mode. The application supplies current
/// totals/advice and owns the real detail window, graph mode and turn transition.</summary>
internal partial class IncomeReportNumericView : Control
{
    private Control canvas = null!;
    private IncomeReportDistrictNumbersView districts = null!;
    private ActionReportStrategistView strategist = null!;
    private UiSourceReportFooter footer = null!;
    private readonly List<UiSourceCommandButton> commands = [];
    private readonly List<Control> districtGuides = [];
    private Func<DistrictReference, string>? readAdvice;
    private Action<DistrictReference>? showDetails, showGraph;
    private Func<bool>? inputAdmitted;
    private Action? closed;
    private Action<Exception>? failed;
    private bool configured, closing, abandoned;

    internal DistrictReference? SelectedDistrict => districts.SelectedDistrict;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        string locale, SourceWorldContext current, ForceReference force,
        Func<DistrictReference, IncomeReportNumbers> readNumbers, Func<DistrictReference, string> readAdvice,
        IReadOnlyList<LoadedSprite>? standingLayers, bool inputGuidesVisible,
        Action<DistrictReference> showDetails, Action<DistrictReference> showGraph,
        Func<bool> inputAdmitted, Action closed, Action<Exception> failed, UiSourceHeaderStack headers)
    {
        ArgumentNullException.ThrowIfNull(readAdvice);
        ArgumentNullException.ThrowIfNull(showDetails);
        ArgumentNullException.ThrowIfNull(showGraph);
        ArgumentNullException.ThrowIfNull(inputAdmitted);
        ArgumentNullException.ThrowIfNull(closed);
        ArgumentNullException.ThrowIfNull(failed);
        ArgumentNullException.ThrowIfNull(headers);
        this.readAdvice = readAdvice; this.showDetails = showDetails; this.showGraph = showGraph;
        this.inputAdmitted = inputAdmitted; this.closed = closed; this.failed = failed;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        canvas = new Control { Name = "IncomeNumericCanvas", Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Pass };
        AddChild(canvas);
        var source = new UiSourceLayout(repository, 0x3E9CC8AD);
        var side = new UiSourceLayout(repository, 0xA3ABC4B3);
        source.Art(canvas, 27, source.Rectangle(27), assets, shader);
        strategist = new ActionReportStrategistView { Name = "IncomeStrategist", Position = source.Rectangle(59).Position };
        canvas.AddChild(strategist);
        strategist.Configure(repository, assets, shader, font, standingLayers);
        districts = new IncomeReportDistrictNumbersView { Name = "IncomeDistricts" };
        canvas.AddChild(districts);
        districts.Configure(repository, assets, shader, font, locale, current, force, readNumbers,
            district => strategist.ApplyPage(this.readAdvice!(district)
                ?? throw new InvalidDataException("Income report advice is unknown.")), ReadAdmission, Fail);
        var messages = repository.LoadMessages(locale, MessageKind.Data).Messages.Sections[1].Records;
        foreach (var (node, message, id) in new[] { (1, 0x5F4, 9), (3, 0x443, 10) })
        {
            int command = id;
            var box = side.Rectangle(node);
            var button = new UiSourceCommandButton { Name = "IncomeCommand" + id, Position = box.Position, Size = box.Size };
            canvas.AddChild(button); commands.Add(button);
            button.Configure(repository, assets, shader, font, messages[message].Text
                ?? throw new InvalidDataException("Missing income command caption."), () => Run(() =>
            {
                if (!ReadAdmission() || SelectedDistrict is not DistrictReference district) return;
                if (command == 9) this.showDetails!(district);
                else this.showGraph!(district);
            }));
            button.InputAdmission = ReadAdmission;
            button.SetMeta("source_control_id", id);
        }
        var pad = new UiSourceLayout(repository, 0x95761708);
        int pitch = (int)source.Nodes[2].HeaderValues[0] - (int)source.Nodes[4].HeaderValues[0];
        foreach (var (node, icon) in new[] { (21, "pad_l1"), (18, "pad_r1") })
        {
            var box = source.Rectangle(node);
            var guide = new Control { Name = "IncomeDistrictGuide" + node, Position = box.Position,
                Size = pad.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
            if (node == 18) guide.Position += new Vector2(pitch * (8 - districts.DistrictCount), 0);
            canvas.AddChild(guide); districtGuides.Add(guide);
            pad.Art(guide, 1, pad.Rectangle(1), assets, shader,
                commonSpriteOverride: "base_pad", commonTexturePack: "texture_pad");
            pad.Art(guide, 2, pad.Rectangle(2), assets, shader,
                commonSpriteOverride: icon, commonTexturePack: "texture_pad");
            guide.SetMeta("source_layout", pad.Source.Source.ResourceUri);
        }
        footer = new UiSourceReportFooter { Name = "IncomeFooter" };
        canvas.AddChild(footer);
        footer.Configure(repository, assets, shader, locale, inputGuidesVisible, RequestClose, ReadAdmission);
        SetInputGuidesVisible(inputGuidesVisible);
        var title = new UiSourceHeader { Name = "IncomeTitle" };
        canvas.AddChild(title);
        title.Configure(repository, assets, shader, font, messages[0x5A3].Text
            ?? throw new InvalidDataException("Missing income report title."));
        title.Publish(headers);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Resized += Fit;
        Fit();
        configured = true;
        ReadAdmission();
    }

    internal void SetInputGuidesVisible(bool visible)
    {
        foreach (var guide in districtGuides) guide.Visible = visible;
        footer.SetInputGuidesVisible(visible);
    }
    internal void ApplyStrategistPortrait(IReadOnlyList<LoadedSprite>? standingLayers) => strategist.ApplyPortrait(standingLayers);

    // Cancel has already passed the application's real shortcut/modal gates.
    // Income has no Action-style click-anywhere advice-page advance path.
    internal void ProcessSourceInput(bool cancelRequested) => Run(() =>
    {
        if (cancelRequested && ReadAdmission()) footer.RequestConfirmation();
    });

    private bool ReadAdmission()
    {
        if (!configured || closing || abandoned) return false;
        try
        {
            bool allowed = IsInsideTree() && IsVisibleInTree() && CanProcess() && inputAdmitted!();
            foreach (var button in commands) button.Disabled = !allowed || SelectedDistrict is null;
            if (footer is not null) footer.ConfirmEnabled = allowed;
            return allowed;
        }
        catch (Exception error) { Fail(error); return false; }
    }

    private void RequestClose() => Run(() =>
    {
        if (!ReadAdmission()) return;
        closing = true;
        RetireFooter();
        Hide(); QueueFree();
    });

    private void Run(Action action)
    {
        if (closing || abandoned) return;
        try { action(); }
        catch (Exception error) { Fail(error); }
    }

    private void Fail(Exception error)
    {
        var notify = failed;
        Abandon();
        notify?.Invoke(error);
    }

    internal void Abandon()
    {
        if (abandoned) return;
        abandoned = true;
        ClearCallbacks();
        foreach (var button in commands) button.Abandon();
        RetireFooter();
        Hide(); SetProcess(false); QueueFree();
    }

    private void RetireFooter()
    {
        if (footer is null || !GodotObject.IsInstanceValid(footer)) return;
        footer.Hide();
        if (footer.GetParent() is Node parent) parent.RemoveChild(footer);
        if (!footer.IsQueuedForDeletion()) footer.QueueFree();
    }

    private void ClearCallbacks()
    { readAdvice = null; showDetails = null; showGraph = null; inputAdmitted = null; closed = null; failed = null; }

    public override void _Process(double delta) => ReadAdmission();
    public override void _ExitTree()
    {
        var notify = closing && !abandoned ? closed : null;
        configured = false;
        ClearCallbacks();
        if (notify is not null) Callable.From(() => { if (!abandoned) notify(); }).CallDeferred();
    }

    private void Fit()
    {
        if (canvas is null) return;
        float factor = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * factor;
        canvas.Position = (Size - new Vector2(1920, 1080) * factor) / 2;
    }
}
