using Godot;
using San14.Data;
using San14.Data.Formats;

internal partial class ItemEditorMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private Func<bool>? admitted;
    private Func<int, bool>? enabled;
    private Action? returned;
    private Action<int>? selected;
    private readonly List<UiSourceCommandButton> buttons = [];
    private bool abandoned, busy;
    internal void Configure(GameResourceRepository repository, PreviewText text, bool created,
        Func<bool> admitted, Func<int,bool> enabled, Action returned, Action<int> selected)
    {
        this.admitted = admitted; this.enabled = enabled; this.returned = returned; this.selected = selected;
        Name = created ? "NewItemEditorMenu" : "HistoricalItemEditorMenu";
        ZIndex = 5; MouseFilter = MouseFilterEnum.Stop; SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, created ? 0x7CD : 0x72C, RequestBack, () => { }, returnOnly: true);
        var page = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_05_00_edit_register"));
        foreach (var node in page.Nodes.Where(node => (node.Type is 1 or 3) && node.ParentIndex == 0))
            page.Art(chrome.Canvas, node.Index, page.Rectangle(node.Index), chrome.Assets, chrome.SpriteShader);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_00_btn_set_offset_00"));
        Vector2 mount = page.Rectangle(3).Position;
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        int[] captions = created ? [0x7D1, 0x7CD, 0x7D2, 0x7D3, 0x7D4] : [0x72C, 0x97];
        for (int index = 0; index < captions.Length; index++)
        {
            int action = index; var box = source.Rectangle(3 + index * 3, mount);
            var button = new UiSourceCommandButton { Name = "ItemEditorMenu" + index,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                messages.Records[captions[index]].Text ?? throw new InvalidDataException("Missing item editor menu caption."),
                () => { if (CanEdit() && !button.Disabled) this.selected?.Invoke(action); });
        }
        SetMeta("source_layout", page.Source.Source.ResourceUri); Refresh();
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    { if (!abandoned) for (int index = 0; index < buttons.Count; index++) buttons[index].Disabled = busy || enabled?.Invoke(index) != true; }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; chrome.ApplyReturn(!value); Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    { abandoned = true; admitted = null; enabled = null; returned = null; selected = null;
      foreach (var button in buttons) button.Abandon(); chrome?.Abandon(); Hide(); }
}
