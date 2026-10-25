using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record DistrictMapSelectionPageValues(string Title, string Guide, bool ThreeButtonFooter,
    bool ShowDecision, bool CanConfirm, bool CanContinue, bool ShowList, bool ShowSelectionButtons, bool CanSelectAll, string SelectionCaption);

/// <summary>The original district/city/target steps over the live map. Lists
/// are alternate views of the same draft; this layer never issues an order.</summary>
internal partial class DistrictMapSelectionView : Control
{
    private UiAssets assets = null!;
    private MapFont font = null!;
    private Shader shader = null!;
    private Control canvas = null!;
    private UiSourceHeader header = null!;
    private UiSourceText hint = null!;
    private UiSourceRoundButton firstBack = null!, back = null!, threeBack = null!, confirm = null!, threeConfirm = null!, again = null!;
    private UiSourceBottomButton list = null!, clear = null!;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<bool>? accepted;
    private DistrictMapSelectionPageValues? publication;
    private bool busy, abandoned;
    private Control? districtActions;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    internal bool HasDistrictActions => districtActions is not null;

    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> admitted,
        Action returned, Action<bool> accepted, Action showList, Action clearSelection)
    {
        this.repository = repository; this.text = text;
        this.admitted = admitted; this.returned = returned; this.accepted = accepted;
        Name = "DistrictMapSelection"; ZIndex = 3; MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets = new(repository); font = new(repository, text.SourceLocale); shader = UiSourceSprite.CreateShader();
        canvas = new() { Size = new(1920, 1080), MouseFilter = MouseFilterEnum.Ignore }; AddChild(canvas);
        header = new(); canvas.AddChild(header); header.Configure(repository, assets, shader, font, "");
        var footer = new UiSourceLayout(repository, 0xD8913AB2); Vector2 mount = new(0, 904);
        firstBack = Round(1, "000_cmn_txt_decision_modoru", RequestBack, true);
        back = Round(4, "000_cmn_txt_decision_modoru", RequestBack, true);
        confirm = Round(5, "000_cmn_txt_decision_kettei", () => RequestApply(false));
        threeBack = Round(1, "000_cmn_txt_decision_modoru", RequestBack, true);
        threeConfirm = Round(2, "000_cmn_txt_decision_kettei", () => RequestApply(false));
        again = new() { Position = footer.Rectangle(3, mount).Position, InputAdmission = ReadAdmission };
        canvas.AddChild(again); again.ConfigureContinue(repository, assets, shader, text.SourceLocale, () => RequestApply(true));
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing district map caption.");
        list = Bottom(14, Message(0x5C), showList); clear = Bottom(17, Message(0x3B), clearSelection);
        var narration = new UiSourceLayout(repository, 0xAB6ADAA7);
        Vector2 hintMount = footer.Rectangle(19, mount).Position;
        foreach (var node in narration.Nodes.Where(node => node.Type is 1 or 3))
            narration.Art(canvas, node.Index, narration.Rectangle(node.Index, hintMount), assets, shader);
        hint = narration.Text(canvas, narration.Nodes.Single(node => node.Type == 2).Index, hintMount, "", font);
        Resized += Fit; Fit(); SetBusy(false);

        UiSourceRoundButton Round(int node, string caption, Action action, bool cancel = false)
        {
            var button = new UiSourceRoundButton { Position = footer.Rectangle(node, mount).Position, InputAdmission = ReadAdmission };
            canvas.AddChild(button);
            button.Configure(repository, assets, shader, text.SourceLocale, caption,
                () => { if (ReadAdmission()) action(); }, cancel ? UiAudioCue.Cancel : UiAudioCue.ButtonSelect);
            return button;
        }
        UiSourceBottomButton Bottom(int node, string caption, Action action)
        {
            var button = new UiSourceBottomButton { Position = footer.Rectangle(node, mount).Position, InputAdmission = ReadAdmission };
            canvas.AddChild(button); button.Configure(repository, assets, shader, font, caption, () => { if (ReadAdmission()) action(); });
            return button;
        }
    }
    internal void RegisterHeader(UiSourceHeaderStack stack) => header.Publish(stack);
    internal void Apply(DistrictMapSelectionPageValues value)
    {
        if (abandoned) return;
        publication = value; header.ApplyCaption(value.Title); hint.Text = value.Guide;
        firstBack.Visible = !value.ShowDecision;
        back.Visible = confirm.Visible = value.ShowDecision && !value.ThreeButtonFooter;
        threeBack.Visible = threeConfirm.Visible = again.Visible = value.ShowDecision && value.ThreeButtonFooter;
        firstBack.Disabled = back.Disabled = threeBack.Disabled = busy;
        confirm.Disabled = threeConfirm.Disabled = busy || !value.CanConfirm;
        again.Disabled = busy || !value.CanContinue;
        list.Visible = value.ShowList; list.Disabled = busy;
        clear.Visible = value.ShowSelectionButtons; clear.Disabled = busy || !value.CanSelectAll; clear.ApplyCaption(value.SelectionCaption);
    }
    internal void ShowDistrictActions(Action edit, Action dissolve)
    {
        CloseDistrictActions();
        if (!ReadAdmission()) return;
        var menu = new Control { Position = new(830, 496), Size = new(260, 92), MouseFilter = MouseFilterEnum.Stop };
        districtActions = menu; canvas.AddChild(menu);
        int[] captions = [0x3A8, 0x3AA]; Action[] actions = [edit, dissolve];
        for (int index = 0; index < captions.Length; index++)
        {
            var action = actions[index];
            var button = new UiSourceCommandButton { Position = new(0, index * 44), Size = new(260, 48), InputAdmission = ReadAdmission };
            menu.AddChild(button);
            string caption = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1].Records[captions[index]].Text
                ?? throw new InvalidDataException("Missing district action caption.");
            button.Configure(repository, assets, shader, font, caption, () =>
            { if (ReadAdmission()) { CloseDistrictActions(); action(); } });
        }
    }
    internal void CloseDistrictActions()
    { var menu = districtActions; districtActions = null; if (menu is not null) { menu.GetParent()?.RemoveChild(menu); menu.QueueFree(); } }
    internal void SetBusy(bool value)
    { if (abandoned) return; busy = value; if (publication is { } current) Apply(current); }
    internal void ApplyHoverHint(string? value)
    { if (!abandoned) hint.Text = value ?? publication?.Guide ?? ""; }
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    internal void RequestBack() { if (ReadAdmission()) returned?.Invoke(); }
    private void RequestApply(bool keepOpen)
    { if (ReadAdmission() && (keepOpen ? publication?.CanContinue : publication?.CanConfirm) == true) accepted?.Invoke(keepOpen); }
    internal void Abandon()
    { abandoned = true; admitted = null; returned = null; accepted = null; publication = null; CloseDistrictActions(); Hide(); }
    private void Fit()
    {
        if (canvas is null) return;
        float scale = Math.Min(Size.X / 1920, Size.Y / 1080);
        canvas.Scale = Vector2.One * scale; canvas.Position = (Size - new Vector2(1920, 1080) * scale) / 2;
    }
    public override void _ExitTree() { font?.Dispose(); assets?.Dispose(); shader?.Dispose(); }
}
