using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The original environment settings pages. The application owns its
/// entry snapshot and commit; each enabled row edits a real current consumer.</summary>
internal partial class EnvironmentSettingsView : Control
{
    private AdministrationPageChrome chrome = null!;
    private UiSourceBottomButton defaults = null!;
    private Control help = null!;
    private MapFontProfile helpProfile = null!;
    private string? helpText;
    private readonly List<(UiSourceRadioButton Button, int Row, int Index)> choices = [];
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly Control[] pages = [new(), new()];
    private Action? returned, accepted, reset;
    private bool abandoned, busy;
    private string defaultHelp = "";
    internal MapCameraNavigationOptions Draft { get; private set; } = new();
    internal ListScrollSpeed ListScrollDraft { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        MapCameraNavigationOptions initial, AudioVolumeOptions audio, ListScrollSpeed listScroll, Action returned, Action accepted, Action reset,
        Action<AudioVolumeOptions> previewAudio)
    {
        this.returned = returned; this.accepted = accepted; this.reset = reset;
        initial.Validate(); Draft = initial;
        audio.Validate(); AudioDraft = audio; this.previewAudio = previewAudio;
        if (!Enum.IsDefined(listScroll)) throw new ArgumentOutOfRangeException(nameof(listScroll));
        ListScrollDraft = listScroll;
        Name = "EnvironmentSettings"; ZIndex = 4; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new AdministrationPageChrome { Name = "EnvironmentChrome" }; AddChild(chrome);
        chrome.Configure(repository, text, 0x0031, RequestBack,
            () => { if (!abandoned && !busy) this.accepted?.Invoke(); }, showFooterHint: false);
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].Name = "EnvironmentPage" + i; pages[i].Size = new(1920,1080);
            pages[i].MouseFilter = MouseFilterEnum.Ignore; chrome.Canvas.AddChild(pages[i]);
        }
        var source = new UiSourceLayout(repository, 0x6C019E60);
        var tabSource = new UiSourceLayout(repository, 0x99165634);
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages;
        var narration = new AdministrationMessages(repository, text.SourceLocale);
        string Data(int table, int id) => messages.Sections[table].Records[id].Text ??
            throw new InvalidDataException($"Missing environment caption {table}/{id:X4}.");
        Vector2 tabMount = source.Rectangle(1).Position;
        tabSource.Art(chrome.Canvas, 6, tabSource.Rectangle(6, tabMount), chrome.Assets, chrome.SpriteShader);
        int[] tabNodes = [2, 3, 5], tabCaptions = [0x04DA, 0x04D9, 0x04DB];
        uint[] tabFiles = [0x172EDC24, 0x9AFB27B7, 0xD45873AA];
        for (int i = 0; i < tabNodes.Length; i++)
        {
            int page = i;
            var box = tabSource.Rectangle(tabNodes[i], tabMount);
            var tab = new UiSourceTabButton { Name = "EnvironmentTab" + i, Position = box.Position,
                Size = box.Size, Selected = i == 0, Disabled = i == 2 };
            chrome.Canvas.AddChild(tab);
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Data(1, tabCaptions[i]), tabFiles[i]);
            tab.Pressed += () => { if (!abandoned && !busy && page < pages.Length) ShowPage(page); };
            tabs.Add(tab);
        }
        var hint = new UiSourceLayout(repository, 0xD6361A53);
        Vector2 hintMount = source.Rectangle(2).Position;
        hint.Art(chrome.Canvas, 1, hint.Rectangle(1, hintMount), chrome.Assets, chrome.SpriteShader);
        defaultHelp = narration.Format(0x2740);
        var helpBox = hint.Rectangle(2, hintMount);
        help = new Control { Name = "EnvironmentHelp", Position = helpBox.Position,
            Size = helpBox.Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(help);
        helpProfile = MapFontProfile.FromNode(hint.Nodes[2], hint.Source.Source.ResourceUri);
        if (helpProfile.RawFlags != 4 || helpProfile.RawAlign != 1 || helpProfile.RawLineArrange != 1 || helpProfile.WidthFitMode != 0)
            throw new InvalidDataException("Environment help requires its fixed-height centred source paragraph.");
        ApplyHelp(defaultHelp);
        var rowSource = new UiSourceLayout(repository, 0x8D9566B0);
        int[] radioNodes = [4, 7, 10, 13];
        int[] captions = [0x04C9, 0x04CA, 0x06EF, 0x04CC, 0x04CD, 0x04D2, 0x04D3, 0x04D4, 0x06C6];
        int[] helpMessages = [0x2741,0x2742,0x2759];
        int[][] values = [[0x58,0x59,0x5A,0x5B],[0x5C,0x5D,0x5E],[0x5C,0x5D,0x5E],
            [0x62,0x63,0x64,0x65],[0x66,0x67],[0x6A,0x6B],[0x6C,0x6D],[0x81,0x80],[0x82,0x83]];
        for (int row = 0; row < captions.Length; row++)
        {
            Vector2 mount = source.Rectangle(row + 4).Position;
            rowSource.Art(pages[0], 2, rowSource.Rectangle(2, mount), chrome.Assets, chrome.SpriteShader);
            rowSource.Text(pages[0], 3, mount, Data(1, captions[row]), chrome.Font);
            for (int index = 0; index < values[row].Length; index++)
            {
                int selectedRow = row, selectedIndex = index;
                var box = rowSource.Rectangle(radioNodes[index], mount);
                var button = new UiSourceRadioButton { Name = "EnvironmentOption" + row + "_" + index,
                    Position = box.Position, Size = box.Size, Disabled = row > 2 };
                pages[0].AddChild(button);
                var segment = index == 0 ? UiSourceRadioSegment.Left : index == values[row].Length - 1 ? UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre;
                button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, segment, Data(0, values[row][index]), () =>
                {
                    if (abandoned || busy || selectedRow > 2) return;
                    if (selectedRow == 2) ApplyListScroll((ListScrollSpeed)selectedIndex);
                    else ApplyOptions(selectedRow == 0 ? Draft with { Edge = (MapEdgeScrollSpeed)selectedIndex } :
                        Draft with { Keyboard = (MapKeyboardScrollSpeed)selectedIndex });
                    ApplyHelp(narration.Format(helpMessages[selectedRow]));
                });
                if (row <= 2)
                {
                    string rowHelp = narration.Format(helpMessages[row]);
                    button.MouseEntered += () => { if (!abandoned && !busy) ApplyHelp(rowHelp); };
                    button.MouseExited += () => { if (!abandoned && !busy) ApplyHelp(defaultHelp); };
                }
                choices.Add((button, row, index));
            }
        }
        var footer = new UiSourceLayout(repository, 0xD8913AB2);
        defaults = new UiSourceBottomButton { Name = "EnvironmentDefaults", Position = footer.Rectangle(14, new(0,904)).Position };
        chrome.Canvas.AddChild(defaults);
        defaults.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, Data(1,0x006B),
            () => { if (!abandoned && !busy) this.reset?.Invoke(); });
        defaults.MouseEntered += () => { if (!abandoned && !busy) ApplyHelp(narration.Format(0x275F)); };
        defaults.MouseExited += () => { if (!abandoned && !busy) ApplyHelp(defaultHelp); };
        ConfigureAudioPage(repository, text);
        ApplyOptions(initial); SetBusy(false);
        ApplyListScroll(listScroll);
        ApplyAudioOptions(audio, false); ShowPage(0);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
    }

    internal void RequestBack() { if (!abandoned && !busy) returned?.Invoke(); }
    internal void ApplyOptions(MapCameraNavigationOptions options)
    {
        if (abandoned) return;
        options.Validate(); Draft = options;
        foreach (var row in choices)
            row.Button.Selected = row.Row == 0 ? row.Index == (int)Draft.Edge : row.Row == 1 ? row.Index == (int)Draft.Keyboard : row.Row == 2 && row.Index == (int)ListScrollDraft;
    }
    internal void ApplyListScroll(ListScrollSpeed value)
    {
        if (abandoned) return;
        if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
        ListScrollDraft = value;
        foreach (var row in choices.Where(row => row.Row == 2)) row.Button.Selected = row.Index == (int)value;
    }
    private void ApplyHelp(string text)
    {
        if (helpText == text) return;
        string[] lines = text.Split('\n');
        var lineProfile = helpProfile with { RawAlign = 0, VerticalSpace = 0 };
        chrome.Font.Prepare(lines, lineProfile);
        if (lines.Any(line => chrome.Font.Measure(line, lineProfile).Width > help.Size.X))
            throw new InvalidDataException("Environment help requires its source wrapping branch.");
        // AC20D0/AD6140: truncate the scaled nominal height plus spacing
        // together. AD5F20 centres all explicit LF rows, removing only the
        // final spacing; B07260 subtracts spacing from the first baseline.
        int advance = (int)(chrome.Font.Source.Record('\u3042', helpProfile.Face).Height *
            helpProfile.ScaleY + helpProfile.VerticalSpace);
        int top = (int)((help.Size.Y - (lines.Length * advance - helpProfile.VerticalSpace)) * .5f);
        foreach (var child in help.GetChildren()) { help.RemoveChild(child); child.QueueFree(); }
        for (int index = 0; index < lines.Length; index++)
        {
            var line = new UiSourceText { Name = "HelpLine" + index, Position = new(0, top + index * advance),
                Size = new(help.Size.X, advance - helpProfile.VerticalSpace) };
            help.AddChild(line); line.Configure(chrome.Font, lineProfile); line.Text = lines[index];
        }
        helpText = text;
    }

    internal void ApplyNotice(string message) { if (!abandoned) ApplyHelp(message); }
    internal void SetBusy(bool value)
    {
        if (abandoned) return;
        busy = value; chrome.ApplyReturn(!value); chrome.ApplyDecision(!value, true); defaults.Disabled = value;
        foreach (var row in choices) row.Button.Disabled = value || row.Row > 2;
        for (int i = 0; i < tabs.Count; i++) tabs[i].Disabled = value || i == 2;
        foreach (var row in audioChoices) row.Button.Disabled = value;
    }
    internal void Abandon()
    {
        abandoned = true; returned = null; accepted = null; reset = null;
        previewAudio = null;
        foreach (var row in choices) row.Button.Abandon();
        foreach (var row in audioChoices) row.Button.Abandon();
        foreach (var tab in tabs) tab.Disabled = true;
        if (defaults is not null) defaults.Disabled = true;
        chrome?.Abandon(); Hide();
    }
}
