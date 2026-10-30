using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original five-action new-personality menu. The owner supplies
/// current archive count and actual command availability.</summary>
internal partial class PersonalityEditorMenuView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<UiSourceCommandButton> buttons = [];
    private UiSourceText countText = null!;
    private Func<int>? count;
    private Func<int, bool>? enabled;
    private Func<bool>? admitted;
    private Action? returned;
    private Action<int>? selected;
    private bool abandoned, busy;
    internal void Configure(GameResourceRepository repository, PreviewText text, Func<int> count,
        Func<int, bool> enabled, Func<bool> admitted, Action returned, Action<int> selected)
    {
        this.count = count; this.enabled = enabled; this.admitted = admitted; this.returned = returned; this.selected = selected;
        Name = "PersonalityEditorMenu"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x7F5, RequestBack, () => { }, returnOnly: true);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing new-personality menu caption.");
        var root = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_05_00_edit_register"));
        root.Art(chrome.Canvas, 5, root.Rectangle(5), chrome.Assets, chrome.SpriteShader);
        root.Text(chrome.Canvas, 6, default, Caption(0x7F6), chrome.Font);
        root.Text(chrome.Canvas, 7, default, "100", chrome.Font);
        root.Text(chrome.Canvas, 8, default, "/", chrome.Font);
        countText = root.Text(chrome.Canvas, 9, default, "", chrome.Font);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "02_04_00_btn_set_offset_00_new"));
        Vector2 mount = root.Rectangle(3).Position;
        foreach (var node in source.Nodes.Where(node => (node.Type is 1 or 3) && node.ParentIndex == 0))
            source.Art(chrome.Canvas, node.Index, source.Rectangle(node.Index, mount), chrome.Assets, chrome.SpriteShader);
        int[] captions = [0x7F7, 0x7F5, 0x7F8, 0x7F9, 0x7FA];
        for (int index = 0; index < captions.Length; index++)
        {
            int action = index; var box = source.Rectangle(3 + index * 3, mount);
            var button = new UiSourceCommandButton { Name = "PersonalityEditorMenu" + index,
                Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            chrome.Canvas.AddChild(button); buttons.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Caption(captions[index]),
                () => { if (CanEdit() && !button.Disabled) this.selected?.Invoke(action); });
        }
        SetMeta("source_layout", root.Source.Source.ResourceUri); SetMeta("source_buttons", source.Source.Source.ResourceUri); Refresh();
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && admitted?.Invoke() == true;
    internal void Refresh()
    {
        if (abandoned) return;
        countText.Text = (count?.Invoke() ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (int index = 0; index < buttons.Count; index++) buttons[index].Disabled = busy || enabled?.Invoke(index) != true;
    }
    internal void SetBusy(bool value) { if (abandoned) return; busy = value; chrome.ApplyReturn(!value); Refresh(); }
    internal void ApplyNotice(string message) { if (!abandoned) chrome.ApplyDefaultHint(message); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; count = null; enabled = null; admitted = null; returned = null; selected = null;
        foreach (var button in buttons) button.Abandon(); chrome?.Abandon(); Hide();
    }
}
