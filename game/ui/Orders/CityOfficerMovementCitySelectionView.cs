using Godot;
using San14.Data;

/// <summary>The movement destination selector. Selection is local until its decision callback.</summary>
internal partial class CityOfficerMovementCitySelectionView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scroll = null!;
    private readonly List<(int Id, UiSourcePersonRow Row)> rows = [];
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private int? selected;
    private bool busy, abandoned;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<CityOfficerMovementCity> cities, int? initial, Func<bool> admitted, Action<int?> closed)
    {
        this.admitted = admitted; this.closed = closed;
        selected = cities.Any(c => c.CityId == initial) ? initial : null;
        Name = "CityOfficerMovementCities"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x0545, RequestCancel, () => { if (selected is int id) Finish(id); });
        var hint = new AdministrationMessages(repository, text.SourceLocale);
        chrome.ApplyDefaultHint(hint.Format(0x578C)); chrome.ApplyReturnHint(hint.Format(0x2733));
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191,147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191,147)), chrome.Assets, chrome.SpriteShader);
        var messages = repository.LoadMessages(text.SourceLocale, San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        string Message(int id) => messages.Records[id].Text ?? throw new InvalidDataException("Missing movement city caption.");
        var heading = new UiSourceColumnButton { Name = "MovementCityNameColumn", Position = new(213,214), Size = new(1464,48), Disabled = true };
        chrome.Canvas.AddChild(heading);
        heading.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Message(0x0478), personList:true);
        var clip = new Control { Name = "MovementCitiesViewport", Position = new(213,264), Size = new(1464,596),
            ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip);
        scroll = new() { Name = "MovementCityScrollbar", Position = new(1687,265), Size = new(16,597) };
        chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0,cities.Count*48-596),0,_=>PositionRows(),stepPixels:48);
        clip.GuiInput += input => { if (Active) scroll.HandleWheel(input); };
        foreach (var city in cities)
        {
            int id = city.CityId;
            var row = new UiSourcePersonRow { Name = "MovementDestination"+id, InputAdmission = () => Active };
            clip.AddChild(row);
            row.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,Message(0x0628),
                [new(0,1416,0x0478,0x01,1,true)],[new(city.Name)]);
            row.ApplyHealthStyle(false); row.GuiInput += input => { if (Active) scroll.HandleWheel(input); };
            row.Pressed += () => { if (Active) { selected = id; Refresh(); } };
            rows.Add((id,row));
        }
        SetMeta("source_selector", "749460 / foothold single selection");
        PositionRows(); Refresh();
    }
    private bool Active => !abandoned && !busy && closed is not null && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void PositionRows()
    {
        for (int index=0;index<rows.Count;index++)
        {
            var row=rows[index].Row; row.Position=new(0,index*48-scroll.Value);
            row.Visible=row.Position.Y>-48 && row.Position.Y<596;
        }
    }
    private void Refresh()
    {
        foreach (var (id,row) in rows) { row.Selected=selected==id; row.Disabled=busy; }
        chrome.ApplyDecision(!busy && selected is not null,true); chrome.ApplyReturn(!busy);
    }
    internal void SetBusy(bool value) { if (!abandoned) { busy=value; Refresh(); } }
    internal void RequestCancel() => Finish(null);
    private void Finish(int? id)
    {
        if (!Active) return;
        var callback=closed; closed=null; admitted=null; Hide(); callback?.Invoke(id);
    }
    public override void _Process(double delta) { if (!abandoned && chrome is not null) chrome.ApplyHoverHint(null); }
    internal void Abandon()
    {
        if (abandoned) return;
        abandoned=true; closed=null; admitted=null;
        foreach (var (_,row) in rows) { row.InputAdmission=null; row.Disabled=true; }
        scroll?.SetProcessInput(false); chrome?.Abandon(); SetProcess(false); Hide();
    }
}
