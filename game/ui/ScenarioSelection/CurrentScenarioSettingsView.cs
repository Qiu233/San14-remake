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
                throw new InvalidDataException("Missing current scenario settings tab caption."), ScenarioSettingsSourceLayout.TabLayout(group));
            Action handler = () => { if (ReadAdmission()) SelectGroup(index); };
            tab.Pressed += handler;
            tabs.Add((tab, handler));
        }

        foreach (var setting in publication.Rows)
        {
            var row = new Control
            {
                Name = $"ScenarioSetting{setting.Group}_{setting.Row}",
                Position = ScenarioSettingsSourceLayout.Row(layout, setting.Row).Position,
                Size = new(1128, 64),
                MouseFilter = MouseFilterEnum.Ignore
            };
            groups[setting.Group].AddChild(row);
            var rowSource = ScenarioSettingsSourceLayout.RadioRow(repository, setting.Choices.Count);
            rowSource.Art(row, 2, rowSource.Rectangle(2), chrome.Assets, chrome.SpriteShader);
            rowSource.Text(row, 3, default, setting.Title ?? text.Get("detail_unknown"), chrome.Font);
            for (int index = 0; index < setting.Choices.Count; index++)
            {
                var choice = setting.Choices[index];
                Rect2 radioBox = ScenarioSettingsSourceLayout.Radio(rowSource, index, setting.Choices.Count);
                bool selected = setting.SelectedRadioIndex == choice.RadioIndex;
                var radio = new UiSourceRadioButton
                {
                    Name = $"ScenarioSetting{setting.Group}_{setting.Row}_{choice.RadioIndex}",
                    Position = radioBox.Position,
                    Size = radioBox.Size,
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
        var help = new UiSourceFixedHelp { Name = "ScenarioSettingsHelp", Position = box.Position, Size = box.Size };
        chrome.Canvas.AddChild(help);
        help.Configure(chrome.Font, MapFontProfile.FromNode(source.Nodes[2], source.Source.Source.ResourceUri));
        help.Apply(message);
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
