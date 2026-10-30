using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class RegisteredOfficerLibraryMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<UiSourceCommandButton> buttons = [];
    private Func<bool>? admitted, canCreate, hasOfficers;
    private Action? returned;
    private Action? birthYears, exportOfficers, importOfficers;
    private bool abandoned, busy;
    internal void Configure(GameResourceRepository repository, PreviewText text, Func<bool> admitted,
        Action returned, Action create, Action edit, Action delete, Func<bool> canCreate, Func<bool> hasOfficers,
        Action? birthYears = null, Action? exportOfficers = null, Action? importOfficers = null)
    {
        this.admitted = admitted; this.returned = returned; this.canCreate = canCreate; this.hasOfficers = hasOfficers;
        this.birthYears = birthYears;
        this.exportOfficers = exportOfficers; this.importOfficers = importOfficers;
        Name = "RegisteredOfficerLibraryMenu"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x0030, RequestBack, () => { }, returnOnly: true);
        var source = new UiSourceLayout(RegisteredOfficerEditorLayoutSource.LoadMenuButtons(repository));
        foreach (var node in source.Nodes.Where(node => (node.Type is 1 or 3) && node.ParentIndex == 0))
            source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        int[] captions = [0x5CB, 0x5CF, 0x5CC, 0x5DE, 0x5C9, 0x5CA, 0x60D];
        Action[] actions = [create, edit, delete, () => this.birthYears?.Invoke(),
            () => this.exportOfficers?.Invoke(), () => this.importOfficers?.Invoke()];
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        for (int index = 0; index < captions.Length; index++)
        {
            int action = index; Rect2 box = source.Rectangle(3 + index * 3);
            var button = new UiSourceCommandButton { Name = "RegisteredOfficerMenu" + index,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                messages.Records[captions[index]].Text ?? throw new InvalidDataException("Missing registered officer menu caption."),
                () => { if (CanEdit() && action < actions.Length && !button.Disabled) actions[action](); });
        }
        SetMeta("source_layout", source.Source.Source.ResourceUri); Refresh();
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void Refresh()
    {
        if (abandoned) return;
        for (int index = 0; index < buttons.Count; index++)
            buttons[index].Disabled = busy || index >= 6 || index == 3 && birthYears is null ||
                index == 4 && exportOfficers is null || index == 5 && importOfficers is null ||
                (index is 0 or 5 ? canCreate?.Invoke() != true : hasOfficers?.Invoke() != true);
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; chrome.ApplyReturn(!value); Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; returned = null; canCreate = null; hasOfficers = null;
        birthYears = null; exportOfficers = null; importOfficers = null;
        foreach (var button in buttons) button.Abandon(); chrome?.Abandon(); Hide();
    }
}
