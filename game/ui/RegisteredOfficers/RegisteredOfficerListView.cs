using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

internal partial class RegisteredOfficerListView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scroll = null!;
    private readonly List<(int Id, UiSourcePersonRow Row)> rows = [];
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private bool abandoned, busy;
    private int? selected;
    internal void Configure(GameResourceRepository repository, PreviewText text, IReadOnlyList<RegisteredOfficer> officers,
        Func<bool> admitted, Action<int?> closed, Func<int> scrollSpeed)
    {
        this.admitted = admitted; this.closed = closed;
        Name = "RegisteredOfficerList"; ZIndex = 6; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x032E, RequestBack, () => { if (CanEdit() && selected.HasValue) Finish(selected); }, showFooterHint: false);
        var source = new UiSourceLayout(repository, 0xA3B4A82E);
        source.Blur(chrome.Canvas, 2, source.Rectangle(2, new(191, 147)));
        source.Art(chrome.Canvas, 3, source.Rectangle(3, new(191, 147)), chrome.Assets, chrome.SpriteShader);
        DeploymentPersonColumn[] columns = [new(0, 400, 0x0211, 0x01, 1, true),
            new(1, 180, 0x238, 0x21, 3), new(2, 180, 0x239, 0x21, 3), new(3, 180, 0x23A, 0x21, 3),
            new(4, 180, 0x23B, 0x21, 3), new(5, 180, 0x23C, 0x21, 3)];
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        int x = 261;
        foreach (var column in columns)
        {
            var header = new UiSourceColumnButton { Position = new(x, 214), Size = new(column.Width, 48), Disabled = true };
            chrome.Canvas.AddChild(header); header.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                messages.Records[column.CaptionMessage].Text ?? throw new InvalidDataException("Missing registered officer list heading."), personList: true);
            x += column.Width;
        }
        var clip = new Control { Position = new(213, 264), Size = new(1464, 596), ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip);
        scroll = new() { Position = new(1687, 265), Size = new(16, 597) }; chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, officers.Count * 48 - 596),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        clip.GuiInput += input => { if (CanEdit()) scroll.HandleWheel(input); };
        foreach (var person in officers)
        {
            int id = person.Id; var row = new UiSourcePersonRow { Name = "RegisteredOfficer" + id, InputAdmission = CanEdit };
            clip.AddChild(row); rows.Add((id, row));
            row.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, "", columns,
                [new(person.Name), new(person.Leadership.ToString()), new(person.Strength.ToString()), new(person.Intelligence.ToString()),
                    new(person.Politics.ToString()), new(person.Charm.ToString())]);
            row.GuiInput += input => { if (CanEdit()) scroll.HandleWheel(input); };
            row.Pressed += () => { if (CanEdit()) { selected = id; Refresh(); } };
        }
        PositionRows(); Refresh(); SetMeta("source_layout", source.Source.Source.ResourceUri);
    }
    private bool CanEdit() => !abandoned && !busy && IsVisibleInTree() && (admitted?.Invoke() ?? false);
    private void PositionRows()
    {
        for (int i = 0; i < rows.Count; i++)
        { var row = rows[i].Row; row.Position = new(0, i * 48 - scroll.Value); row.Visible = row.Position.Y > -48 && row.Position.Y < 596; }
    }
    private void Refresh()
    { foreach (var item in rows) { item.Row.Selected = item.Id == selected; item.Row.Disabled = busy; } chrome.ApplyDecision(!busy && selected.HasValue, true); }
    internal void SetBusy(bool value) { if (!abandoned) { busy = value; chrome.ApplyReturn(!value); Refresh(); } }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    private void Finish(int? result) { var callback = closed; Abandon(); callback?.Invoke(result); }
    internal void RequestBack() { if (CanEdit()) Finish(null); }
    internal void Abandon()
    { abandoned = true; admitted = null; closed = null; foreach (var item in rows) item.Row.Disabled = true; chrome?.Abandon(); Hide(); }
}
