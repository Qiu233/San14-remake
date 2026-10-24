using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The ordinary in-game scenario settings publication. The source
/// keeps the selected radio's normal artwork while preventing all edits.</summary>
internal partial class CurrentScenarioSettingsView : Control
{
    private AdministrationPageChrome chrome = null!;
    private readonly List<Control> groups = [];
    private readonly List<(UiSourceTabButton Button, Action Handler)> tabs = [];
    private readonly List<UiSourceRadioButton> radios = [];
    private Func<bool>? admitted;
    private Action? returned;
    private bool abandoned;

    internal CurrentScenarioSettingsPreview Publication { get; private set; } = null!;
    internal int CurrentGroup { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        CurrentScenarioSettingsPreview publication, Func<bool> admitted, Action returned)
    {
        Publication = publication;
        this.admitted = admitted;
        this.returned = returned;
        Name = "CurrentScenarioSettings";
        ZIndex = 4;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        chrome = new AdministrationPageChrome { Name = "ScenarioSettingsChrome" };
        AddChild(chrome);
        chrome.Configure(repository, text, 0x00BB, RequestBack, static () => { },
            returnOnly: true, showFooterHint: false);

        var layout = new UiSourceLayout(repository, 0xA32D4D03);
        var advice = new UiSourceLayout(repository, 0xD6361A53);
        Vector2 adviceMount = layout.Rectangle(1).Position;
        advice.Art(chrome.Canvas, 1, advice.Rectangle(1, adviceMount), chrome.Assets, chrome.SpriteShader);
        ConfigureHelp(advice, adviceMount,
            new AdministrationMessages(repository, text.SourceLocale).Format(0x5F17));

        var tabLayout = new UiSourceLayout(repository, 0x5C9D1E9E);
        Vector2 tabMount = layout.Rectangle(12).Position;
        var messages = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        var rowPositions = layout.Nodes.Where(node => node.ParentIndex == 2 && node.Type == 0 &&
                node.Name.StartsWith("02_03_02_setup_offset_04", StringComparison.Ordinal))
            .Select(node => layout.Rectangle(node.Index).Position).OrderBy(point => point.Y).ToArray();

        for (int group = 0; group < 5; group++)
        {
            int index = group;
            var pane = new Control
            {
                Name = "ScenarioSettingsGroup" + group,
                Size = new(1920, 1080),
                MouseFilter = MouseFilterEnum.Ignore
            };
            chrome.Canvas.AddChild(pane);
            groups.Add(pane);
            Rect2 box = tabLayout.Rectangle(group + 2, tabMount);
            var tab = new UiSourceTabButton
            {
                Name = "ScenarioSettingsTab" + group,
                Position = box.Position,
                Size = box.Size,
                InputAdmission = ReadAdmission
            };
            chrome.Canvas.AddChild(tab);
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                messages.Records[0x881 + group].Text ??
                throw new InvalidDataException("Missing current scenario settings tab caption."));
            Action handler = () => { if (ReadAdmission()) SelectGroup(index); };
            tab.Pressed += handler;
            tabs.Add((tab, handler));
        }

        foreach (var setting in publication.Rows)
        {
            var row = new Control
            {
                Name = $"ScenarioSetting{setting.Group}_{setting.Row}",
                Position = rowPositions[setting.Row],
                Size = new(1128, 64),
                MouseFilter = MouseFilterEnum.Ignore
            };
            groups[setting.Group].AddChild(row);
            var rowSource = new UiSourceLayout(repository,
                setting.Choices.Count > 4 ? 0x7F1F1D88u : 0x28012CA9u);
            rowSource.Art(row, 2, rowSource.Rectangle(2), chrome.Assets, chrome.SpriteShader);
            rowSource.Text(row, 3, default, setting.Title ?? text.Get("detail_unknown"), chrome.Font);
            var slots = rowSource.Nodes.Where(node => node.ParentIndex == 0 && node.Type == 0 &&
                    node.Name.StartsWith("cmn_btn_radio_", StringComparison.Ordinal))
                .Select(node => rowSource.Rectangle(node.Index)).OrderBy(box => box.Position.X).ToArray();
            float start = slots[0].Position.X, end = slots[^1].End.X;
            float width = (end - start) / setting.Choices.Count;
            for (int index = 0; index < setting.Choices.Count; index++)
            {
                var choice = setting.Choices[index];
                bool selected = setting.SelectedRadioIndex == choice.RadioIndex;
                var radio = new UiSourceRadioButton
                {
                    Name = $"ScenarioSetting{setting.Group}_{setting.Row}_{choice.RadioIndex}",
                    Position = new(start + width * index, slots[0].Position.Y),
                    Size = new(width, slots[0].Size.Y),
                    MouseFilter = MouseFilterEnum.Ignore,
                    InputAdmission = static () => false,
                    Selected = selected,
                    Disabled = !selected
                };
                row.AddChild(radio);
                radios.Add(radio);
                var segment = index == 0 ? UiSourceRadioSegment.Left :
                    index == setting.Choices.Count - 1 ? UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre;
                radio.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, segment,
                    choice.Text ?? text.Get("detail_unknown"), static () => { });
            }
        }

        SelectGroup(0);
        SetMeta("source_layout", layout.Source.Source.ResourceUri);
    }

    private bool ReadAdmission() => !abandoned && IsVisibleInTree() && CanProcess() &&
        (admitted?.Invoke() ?? false);

    private void ConfigureHelp(UiSourceLayout source, Vector2 mount, string message)
    {
        Rect2 box = source.Rectangle(2, mount);
        var help = new Control
        {
            Name = "ScenarioSettingsHelp", Position = box.Position,
            Size = box.Size, MouseFilter = MouseFilterEnum.Ignore
        };
        chrome.Canvas.AddChild(help);
        var profile = MapFontProfile.FromNode(source.Nodes[2], source.Source.Source.ResourceUri);
        if (profile.RawFlags != 4 || profile.RawAlign != 1 || profile.RawLineArrange != 1 || profile.WidthFitMode != 0)
            throw new InvalidDataException("Scenario settings help requires its fixed-height centred source paragraph.");
        string[] lines = message.Split('\n');
        var lineProfile = profile with { RawAlign = 0, VerticalSpace = 0 };
        chrome.Font.Prepare(lines, lineProfile);
        if (lines.Any(line => chrome.Font.Measure(line, lineProfile).Width > help.Size.X))
            throw new InvalidDataException("Scenario settings help requires its source wrapping branch.");
        // Share EnvironmentSettingsView's actual source paragraph baseline:
        // truncate nominal line height plus spacing, then centre the LF rows.
        int advance = (int)(chrome.Font.Source.Record('\u3042', profile.Face).Height *
            profile.ScaleY + profile.VerticalSpace);
        int top = (int)((help.Size.Y - (lines.Length * advance - profile.VerticalSpace)) * .5f);
        for (int index = 0; index < lines.Length; index++)
        {
            var line = new UiSourceText
            {
                Name = "HelpLine" + index, Position = new(0, top + index * advance),
                Size = new(help.Size.X, advance - profile.VerticalSpace)
            };
            help.AddChild(line);
            line.Configure(chrome.Font, lineProfile);
            line.Text = lines[index];
        }
    }

    private void SelectGroup(int group)
    {
        CurrentGroup = group;
        for (int index = 0; index < groups.Count; index++)
        {
            groups[index].Visible = index == group;
            tabs[index].Button.Selected = index == group;
        }
    }

    internal void RequestBack()
    {
        if (ReadAdmission()) returned?.Invoke();
    }

    internal void Abandon()
    {
        abandoned = true;
        admitted = null;
        returned = null;
        foreach (var radio in radios)
        {
            radio.InputAdmission = null;
            radio.Abandon();
        }
        foreach (var (button, handler) in tabs)
        {
            button.Pressed -= handler;
            button.InputAdmission = null;
            button.Disabled = true;
        }
        radios.Clear();
        tabs.Clear();
        groups.Clear();
        chrome?.Abandon();
        Hide();
    }
}
