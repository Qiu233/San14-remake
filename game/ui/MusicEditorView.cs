using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record MusicEditorRow(int Slot, string ContextName, string MusicName, string Hint, int Row);
internal sealed record MusicSelectionRow(int Index, string Name, string Number);

/// <summary>Original BGM categories over an application-owned edit draft.</summary>
internal partial class MusicEditorView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly List<MusicAssignmentRowView> rows = [];
    private UiSourceBottomButton defaults = null!;
    private Func<bool>? admitted;
    private Func<int, IReadOnlyList<MusicEditorRow>>? readRows;
    private Action? returned, accepted, reset;
    private Action<int>? choose;
    private GameResourceRepository repository = null!;
    private Control list = null!;
    private UiSourceScrollbar? scroll;
    private Vector2 firstRow, secondRow, rowSize;
    private Rect2 scrollRectangle;
    private Func<int>? currentScrollSpeed;
    private Func<bool>? canInitializeDefaults;
    private bool abandoned, busy;
    private int selectedTab;

    internal void Configure(GameResourceRepository repository, PreviewText text, IReadOnlyList<string> groups,
        Func<int, IReadOnlyList<MusicEditorRow>> readRows, Func<bool> admitted,
        Action returned, Action accepted, Action reset, Action<int> choose, Func<int>? currentScrollSpeed = null,
        Func<bool>? canInitializeDefaults = null, AdministrationPageChrome.Resources? resources = null)
    {
        this.repository = repository; this.readRows = readRows; this.admitted = admitted;
        this.returned = returned; this.accepted = accepted; this.reset = reset; this.choose = choose;
        this.currentScrollSpeed = currentScrollSpeed;
        this.canInitializeDefaults = canInitializeDefaults;
        Name = "MusicEditor"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x02DF, RequestBack,
            () => { if (CanEdit()) this.accepted?.Invoke(); }, resources: resources);
        var source = chrome.Assets.SourceLayout("02_12_00_BGM_edit");
        source.Blur(chrome.Canvas, 2, source.Rectangle(2));
        source.Art(chrome.Canvas, 3, source.Rectangle(3), chrome.Assets, chrome.SpriteShader);
        for (int index = 0; index < groups.Count; index++)
        {
            int tabIndex = index;
            var box = source.Rectangle(index == 0 ? 4 : 6);
            var tab = new UiSourceTabButton { Name = "MusicGroup" + index, Position = box.Position, Size = box.Size };
            chrome.Canvas.AddChild(tab);
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, groups[index], index == 0 ? 0x172EDC24u : 0x9AFB27B7u);
            tab.Pressed += () => { if (CanEdit()) { selectedTab = tabIndex; Refresh(); } };
            tabs.Add(tab);
        }
        var rectangle = source.Rectangle(23);
        list = new Control { Name = "MusicAssignments", Position = rectangle.Position,
            Size = rectangle.Size, ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(list);
        firstRow = source.Rectangle(24).Position - rectangle.Position;
        secondRow = source.Rectangle(25).Position - rectangle.Position;
        rowSize = source.Rectangle(24).Size;
        scrollRectangle = source.Rectangle(14);
        list.GuiInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
        var footer = chrome.Assets.SourceLayout(0xD8913AB2);
        var captions = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        defaults = new UiSourceBottomButton { Name = "InitializeMusic", Position = footer.Rectangle(14, new(0, 904)).Position };
        chrome.Canvas.AddChild(defaults);
        defaults.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
            captions.Records[0x006B].Text ?? throw new InvalidDataException("Missing initialize caption."),
            () => { if (CanEdit()) this.reset?.Invoke(); });
        chrome.ApplyDefaultHint(new AdministrationMessages(repository, text.SourceLocale).Format(0x287C));
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        Refresh();
    }

    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    internal void Refresh()
    {
        if (abandoned || readRows is null) return;
        foreach (var button in rows) { button.Abandon(); list.RemoveChild(button); button.QueueFree(); }
        rows.Clear();
        var publication = readRows(selectedTab);
        for (int index = 0; index < publication.Count; index++)
        {
            var row = publication[index];
            if (row.Slot == 28) continue;
            int slot = row.Slot;
            var button = new MusicAssignmentRowView { Name = "MusicAssignment" + slot, Size = rowSize };
            list.AddChild(button); rows.Add(button);
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                row.ContextName, row.MusicName, () => { if (CanEdit()) choose?.Invoke(slot); });
            button.SetMeta("music_row", row.Row);
            button.InputAdmission = CanEdit;
            button.GuiInput += input => { if (CanEdit()) scroll?.HandleWheel(input); };
            button.MouseEntered += () => { if (CanEdit()) chrome.ApplyHoverHint(row.Hint); };
            button.MouseExited += () => { if (!abandoned) chrome.ApplyHoverHint(null); };
        }
        if (scroll is not null) { chrome.Canvas.RemoveChild(scroll); scroll.QueueFree(); }
        scroll = new UiSourceScrollbar { Name = "MusicAssignmentsScrollbar", Position = scrollRectangle.Position, Size = scrollRectangle.Size };
        chrome.Canvas.AddChild(scroll);
        int height = (int)(firstRow.Y + Math.Max(0, publication.Select(row => row.Row).DefaultIfEmpty(0).Max() / 2) * 90 + rowSize.Y);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, height - (int)list.Size.Y),
            0, _ => PositionRows(), currentScrollSpeed: currentScrollSpeed);
        PositionRows();
        for (int index = 0; index < tabs.Count; index++) tabs[index].Selected = index == selectedTab;
        SetBusy(busy);
    }
    private void PositionRows()
    {
        foreach (var row in rows)
        {
            int index = row.GetMeta("music_row").AsInt32();
            row.Position = (index % 2 == 0 ? firstRow : secondRow) + new Vector2(0, index / 2 * 90 - (scroll?.Value ?? 0));
            row.Visible = row.Position.Y > -row.Size.Y && row.Position.Y < list.Size.Y;
        }
    }
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; chrome.ApplyReturn(!value); chrome.ApplyDecision(!value, true);
        defaults.Disabled = value || canInitializeDefaults?.Invoke() == false;
        foreach (var button in rows) button.Disabled = value;
        foreach (var tab in tabs) tab.Disabled = value;
    }
    internal void ApplyNotice(string text) { if (!abandoned) chrome.ApplyDefaultHint(text); }
    internal void RequestBack() { if (CanEdit()) returned?.Invoke(); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; readRows = null; returned = null; accepted = null; reset = null; choose = null;
        foreach (var row in rows) row.Abandon();
        foreach (var tab in tabs) tab.Disabled = true;
        if (defaults is not null) defaults.Disabled = true;
        chrome?.Abandon(); Hide();
    }
}
