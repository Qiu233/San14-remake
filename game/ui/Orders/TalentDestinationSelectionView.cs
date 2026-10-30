using Godot;
using San14.Data;

internal partial class TalentDestinationSelectionView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceScrollbar scroll = null!;
    private readonly List<(int City, UiSourceCommandButton Button)> rows = [];
    private Func<bool>? admitted;
    private Action<int?>? closed;
    private int? selected;
    private bool abandoned;
    private Func<int, bool>? selectionAdmission;
    private bool returnAllowed = true;
    internal int? SelectedCityId => selected;
    internal void ApplySelectionAdmission(Func<int, bool>? allowed, bool canReturn)
    { selectionAdmission = allowed; returnAllowed = canReturn; Refresh(); }
    private bool CityAllowed(int city) => selectionAdmission?.Invoke(city) ?? true;

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<TalentSearchDestination> destinations, int? current, Func<bool> admitted,
        Action<int?> closed, Func<int> scrollSpeed)
    {
        this.admitted = admitted; this.closed = closed;
        selected = destinations.Any(value => value.CityId == current) ? current : null;
        Name = "TalentDestinationSelection"; ZIndex = 5; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop = new UiSourceCommonBlur(); AddChild(backdrop); backdrop.Configure(repository);
        chrome = new(); AddChild(chrome);
        chrome.Configure(repository, text, 0x0591, RequestCancel,
            () => { if (ReadAdmission() && selected is int city && CityAllowed(city)) Finish(city); }, showFooterHint: false);
        var shell = new UiSourceLayout(repository, 0xA3B4A82E);
        shell.Blur(chrome.Canvas, 2, shell.Rectangle(2, new(191, 147)));
        shell.Art(chrome.Canvas, 3, shell.Rectangle(3, new(191, 147)), chrome.Assets, chrome.SpriteShader);
        var clip = new Control { Position = new(213, 214), Size = new(1464, 648), ClipContents = true,
            MouseFilter = MouseFilterEnum.Pass };
        chrome.Canvas.AddChild(clip);
        scroll = new() { Position = new(1687, 214), Size = new(16, 648) }; chrome.Canvas.AddChild(scroll);
        scroll.Configure(repository, chrome.Assets, chrome.SpriteShader, Math.Max(0, destinations.Count * 48 - 648),
            scrollSpeed(), _ => PositionRows(), currentScrollSpeed: scrollSpeed);
        clip.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
        foreach (var destination in destinations)
        {
            int city = destination.CityId;
            var button = new UiSourceCommandButton { Name = "TalentDestination" + city, Size = new(1440, 48), InputAdmission = ReadAdmission };
            clip.AddChild(button); rows.Add((city, button));
            button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                text.Format("talent_destination_count", destination.Name, destination.AreaHexes.Count), () =>
                { if (ReadAdmission() && CityAllowed(city)) { selected = city; Refresh(); } });
            button.GuiInput += input => { if (ReadAdmission()) scroll.HandleWheel(input); };
        }
        SetMeta("original_talent_city_columns_recovered", false);
        PositionRows(); Refresh();
    }
    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void Refresh()
    {
        foreach (var (city, button) in rows) { button.Selected = city == selected; button.Disabled = !CityAllowed(city); }
        chrome.ApplyDecision(selected is int confirmedCity && CityAllowed(confirmedCity), true);
        chrome.ApplyReturn(returnAllowed);
    }
    private void PositionRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var button = rows[i].Button; button.Position = new(0, i * 48 - scroll.Value);
            button.Visible = button.Position.Y > -48 && button.Position.Y < 648;
        }
    }
    internal void RequestCancel() { if (ReadAdmission() && returnAllowed) Finish(null); }
    private void Finish(int? city) { if (!abandoned) closed?.Invoke(city); }
    internal void Abandon()
    {
        abandoned = true; admitted = null; closed = null;
        foreach (var (_, button) in rows) button.Abandon();
        scroll?.SetProcessInput(false); chrome?.Abandon(); Hide();
    }
}
