using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class EnvironmentSettingsView
{
    private KeyboardBindingCatalog keyboardCatalog = null!;
    private readonly List<(int Action, int Slot, UiSourceCommandButton Button)> keyboardButtons = [];
    private readonly List<Control> keyboardRows = [];
    private UiSourceScrollbar keyboardScroll = null!;
    private Control keyboardCaptureMask = null!;
    private DeploymentTargetConfirmation? keyboardConflict;
    private GameResourceRepository keyboardRepository = null!;
    private PreviewText keyboardText = null!;
    private string keyboardHelp = "", keyboardCaptureHelp = "";
    private string CurrentPageHelp => pages[2].Visible ? keyboardHelp : defaultHelp;
    private int captureAction, captureSlot;
    private bool KeyboardModal => captureAction != 0 || keyboardConflict is not null;
    internal KeyboardBindingDraft KeyboardDraft { get; private set; } = null!;

    private void ConfigureKeyboardPage(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<int>? initial)
    {
        keyboardRepository = repository; keyboardText = text;
        keyboardCatalog = KeyboardBindingCatalog.Load(repository, text.SourceLocale);
        KeyboardDraft = new(keyboardCatalog, initial);
        var pageSource = new UiSourceLayout(KeyboardBindingLayoutSource.LoadPage(repository));
        var list = new UiSourceLayout(KeyboardBindingLayoutSource.LoadList(repository));
        var rowSource = new UiSourceLayout(KeyboardBindingLayoutSource.LoadRow(repository));
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages;
        string Data(int id) => messages.Sections[1].Records[id].Text ?? text.Get("detail_unknown");
        keyboardHelp = new AdministrationMessages(repository, text.SourceLocale).Format(0x275E);
        keyboardCaptureHelp = Data(0x04AF);
        var table = new Control { Name = "KeyboardBindingList", Position = pageSource.Rectangle(3).Position,
            Size = list.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        pages[2].AddChild(table);
        var box = list.Rectangle(1);
        var clip = new Control { Name = "KeyboardRowsViewport", Position = box.Position, Size = box.Size,
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        table.AddChild(clip);
        var scrollBox = list.Rectangle(17);
        keyboardScroll = new() { Name = "KeyboardBindingScrollbar", Position = scrollBox.Position, Size = scrollBox.Size };
        table.AddChild(keyboardScroll);
        keyboardScroll.Configure(repository, chrome.Assets, chrome.SpriteShader,
            Math.Max(0, keyboardCatalog.Actions.Count * 62 - (int)box.Size.Y), (int)ListScrollDraft,
            _ => PositionKeyboardRows(), currentScrollSpeed: () => (int)ListScrollDraft);
        clip.GuiInput += HandleKeyboardWheel;
        foreach (var action in keyboardCatalog.Actions)
        {
            var row = new Control { Name = "KeyboardAction" + action.ActionId,
                Size = rowSource.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Pass };
            clip.AddChild(row); keyboardRows.Add(row); row.GuiInput += HandleKeyboardWheel;
            rowSource.Text(row, 3, default, action.Name, chrome.Font);
            for (int slot = 0; slot < 2; slot++)
            {
                int actionId = action.ActionId, selectedSlot = slot;
                var rectangle = rowSource.Rectangle(4 + slot);
                var button = new UiSourceCommandButton { Name = $"KeyboardBinding{actionId}_{slot}",
                    Position = rectangle.Position, Size = rectangle.Size, InputAdmission = KeyboardEditable };
                row.AddChild(button);
                button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                    keyboardCatalog.FormatBinding(KeyboardDraft.Read(actionId, slot)), () => BeginKeyboardCapture(actionId, selectedSlot));
                button.GuiInput += HandleKeyboardWheel;
                keyboardButtons.Add((actionId, slot, button));
            }
        }
        // The source capture state covers the environment controls. It does not
        // request OS focus or open an external window.
        keyboardCaptureMask = new Control { Name = "KeyboardCaptureMask", Size = new(1920,1080),
            MouseFilter = MouseFilterEnum.Stop, Visible = false };
        chrome.Canvas.AddChild(keyboardCaptureMask);
        keyboardCaptureMask.GuiInput += input =>
        {
            if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            { CancelKeyboardModal(); GetViewport().SetInputAsHandled(); }
        };
        var promptBox = list.Rectangle(11, table.Position);
        var prompt = new UiSourceText { Name = "KeyboardCapturePrompt", Position = promptBox.Position, Size = promptBox.Size };
        keyboardCaptureMask.AddChild(prompt);
        prompt.Configure(chrome.Font, MapFontProfile.FromNode(list.Nodes[16], list.Source.Source.ResourceUri));
        prompt.Text = Data(0x04B0);
        pages[2].SetMeta("source_layout", pageSource.Source.Source.ResourceUri);
        table.SetMeta("source_layout", list.Source.Source.ResourceUri);
        PositionKeyboardRows(); RefreshKeyboardBindings();
    }

    private bool KeyboardEditable() => !abandoned && !busy && !KeyboardModal && pages[2].IsVisibleInTree() && CanProcess();

    private void HandleKeyboardWheel(InputEvent input)
    { if (KeyboardEditable()) keyboardScroll.HandleWheel(input); }

    private void PositionKeyboardRows()
    {
        for (int index = 0; index < keyboardRows.Count; index++)
        {
            var row = keyboardRows[index]; row.Position = new(0,index * 62 - keyboardScroll.Value);
            row.Visible = row.Position.Y > -64 && row.Position.Y < keyboardScroll.Size.Y + 8;
        }
    }

    private void RefreshKeyboardBindings()
    {
        foreach (var row in keyboardButtons)
            row.Button.Caption = keyboardCatalog.FormatBinding(KeyboardDraft.Read(row.Action,row.Slot));
        RefreshKeyboardAdmission();
    }

    private void RefreshKeyboardAdmission()
    {
        foreach (var row in keyboardButtons)
        {
            row.Button.Disabled = busy || KeyboardModal || KeyboardDraft.Read(row.Action,row.Slot) == KeyboardBindingCatalog.ReservedBinding;
            row.Button.Selected = captureAction == row.Action && captureSlot == row.Slot;
        }
    }

    internal void ApplyKeyboardBindings(IReadOnlyList<int> bindings)
    { if (!abandoned) { CancelKeyboardModal(); KeyboardDraft.Apply(bindings); RefreshKeyboardBindings(); } }

    internal void ResetKeyboardBindings()
    { if (!abandoned) { CancelKeyboardModal(); KeyboardDraft.Reset(); RefreshKeyboardBindings(); } }

    private void BeginKeyboardCapture(int action, int slot)
    {
        if (!KeyboardEditable() || KeyboardDraft.Read(action,slot) == KeyboardBindingCatalog.ReservedBinding) return;
        captureAction = action; captureSlot = slot;
        keyboardCaptureMask.Show(); ApplyHelp(keyboardCaptureHelp); SetBusy(busy);
    }

    public override void _Input(InputEvent input)
    {
        if (abandoned || busy || !IsVisibleInTree() || !KeyboardModal) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        { CancelKeyboardModal(); GetViewport().SetInputAsHandled(); return; }
        if (input is not InputEventKey key) return;
        GetViewport().SetInputAsHandled();
        if (!key.Pressed || key.Echo) return;
        if (key.Keycode == Key.Escape) { CancelKeyboardModal(); return; }
        if (keyboardConflict is not null) return;
        if (!KeyboardPhysicalInput.TryEncode(key,out int encoded)) return;
        // The actual current cancel command remains usable during capture.
        if (encoded != 0 && (encoded == KeyboardDraft.Read(13,0) || encoded == KeyboardDraft.Read(13,1)))
        { CancelKeyboardModal(); return; }
        int? binding = keyboardCatalog.Normalize(captureAction,encoded);
        if (binding is not int candidate) return;
        var conflict = keyboardCatalog.FindConflict(KeyboardDraft.Bindings,candidate);
        if (conflict is not { } prior || prior.ActionId == captureAction)
        { CommitKeyboardCapture(candidate); return; }
        var question = new DeploymentTargetConfirmation(); keyboardConflict = question; AddChild(question);
        try
        {
            question.ConfigureWrappedMessage(keyboardRepository,keyboardText,
                keyboardCatalog.FormatConflict(prior.ActionId), accepted =>
                {
                    if (abandoned || keyboardConflict != question) return;
                    keyboardConflict = null; question.Abandon(); question.QueueFree();
                    if (accepted) CommitKeyboardCapture(candidate);
                    else EndKeyboardCapture();
                });
            question.Name = "KeyboardBindingConflict";
        }
        catch { keyboardConflict = null; question.Abandon(); question.QueueFree(); EndKeyboardCapture(); throw; }
    }

    private void CommitKeyboardCapture(int binding)
    {
        KeyboardDraft.Rebind(captureAction,captureSlot,binding,true);
        EndKeyboardCapture(); RefreshKeyboardBindings();
    }

    private void EndKeyboardCapture()
    {
        captureAction = 0; captureSlot = 0; keyboardCaptureMask.Hide();
        ApplyHelp(keyboardHelp); SetBusy(busy);
    }

    private bool CancelKeyboardModal()
    {
        if (keyboardConflict is { } question) { question.RequestCancel(); return true; }
        if (captureAction == 0) return false;
        EndKeyboardCapture(); return true;
    }

    private void AbandonKeyboard()
    {
        var question = keyboardConflict; keyboardConflict = null;
        if (question is not null) { question.Abandon(); question.QueueFree(); }
        captureAction = 0;
        if (keyboardCaptureMask is not null) keyboardCaptureMask.Hide();
        foreach (var row in keyboardButtons) { row.Button.InputAdmission = null; row.Button.Abandon(); }
        keyboardScroll?.SetProcessInput(false);
    }
}
