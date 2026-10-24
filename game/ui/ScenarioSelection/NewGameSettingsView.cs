using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>Ordinary settings with live rule consumers. Choices stay local
/// until Main confirms the draft and performs its normal fresh initialization.</summary>
internal partial class NewGameSettingsView : Control
{
    private readonly List<UiSourceTabButton> tabs = [];
    private readonly List<Control> groups = [];
    private readonly List<UiSourceRadioButton> buttons = [];
    private readonly List<Action> refresh = [];
    private Action<NewGameOptions>? changed;
    private Func<bool>? admitted;
    private bool busy, abandoned;
    internal NewGameOptions Draft { get; private set; } = null!;
    internal NewGameSettingsPreview Publication { get; private set; } = null!;
    internal int CurrentGroup { get; private set; }

    internal void Configure(GameResourceRepository repository, PreviewText text,
        AdministrationPageChrome chrome, NewGameSettingsPreview publication,
        Func<bool> admitted, Action<NewGameOptions> changed)
    {
        Publication = publication; Draft = publication.Draft;
        this.admitted = admitted; this.changed = changed;
        Name = "OrdinaryNewGameSettings"; Size = new(1920, 1080); MouseFilter = MouseFilterEnum.Ignore;
        var layout = new UiSourceLayout(repository, 0xA32D4D03);
        var advice = new UiSourceLayout(repository, 0xD6361A53);
        advice.Art(this, 1, advice.Rectangle(1, layout.Rectangle(1).Position), chrome.Assets, chrome.SpriteShader);
        var tabLayout = new UiSourceLayout(repository, 0x5C9D1E9E);
        Vector2 tabMount = layout.Rectangle(12).Position;
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
        var rowPositions = layout.Nodes.Where(node => node.ParentIndex == 2 && node.Type == 0 &&
            node.Name.StartsWith("02_03_02_setup_offset_04", StringComparison.Ordinal))
            .Select(node => layout.Rectangle(node.Index).Position).OrderBy(point => point.Y).ToArray();
        for (int group = 0; group < 5; group++)
        {
            int index = group;
            var pane = new Control { Name = "SettingsGroup" + group, Size = Size, MouseFilter = MouseFilterEnum.Ignore };
            AddChild(pane); groups.Add(pane);
            Rect2 rectangle = tabLayout.Rectangle(group + 2, tabMount);
            var tab = new UiSourceTabButton { Name = "SettingsTab" + group, Position = rectangle.Position,
                Size = rectangle.Size, InputAdmission = ReadAdmission };
            AddChild(tab); tabs.Add(tab);
            string caption = data.Records[0x881 + group].Text ?? throw new InvalidDataException("Missing settings tab caption.");
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption);
            tab.Pressed += () => { if (ReadAdmission()) SelectGroup(index); };
        }
        Add(publication.Difficulty, options => Difficulty(options.DifficultyId), (options, value) => options with
            { DifficultyId = value switch { NewGameDifficulty.Beginner => "beginner", NewGameDifficulty.Intermediate => "intermediate",
                NewGameDifficulty.Advanced => "advanced", NewGameDifficulty.Super => "super", NewGameDifficulty.Top => "top",
                _ => throw new ArgumentOutOfRangeException(nameof(value)) } });
        Add(publication.WoundedRecovery, options => options.WoundedRecovery, (options, value) => options with { WoundedRecovery = value });
        Add(publication.Lifespan, options => options.Lifespan, (options, value) => options with { Lifespan = value });
        Add(publication.BattleDeathFrequency, options => options.BattleDeathFrequency, (options, value) => options with { BattleDeathFrequency = value });
        Add(publication.DuelsEnabled, options => options.DuelsEnabled, (options, value) => options with { DuelsEnabled = value });
        Add(publication.EncirclementEnabled, options => options.EncirclementEnabled, (options, value) => options with { EncirclementEnabled = value });
        Add(publication.AdjacentRegionEffects, options => options.AdjacentRegionEffects, (options, value) => options with { AdjacentRegionEffects = value });
        Add(publication.WoundedSoldiersEnabled, options => options.WoundedSoldiersEnabled, (options, value) => options with { WoundedSoldiersEnabled = value });
        Add(publication.OfficerInjuryEnabled, options => options.OfficerInjuryEnabled, (options, value) => options with { OfficerInjuryEnabled = value });
        Add(publication.CooperationEffects, options => options.CooperationEffects, (options, value) => options with { CooperationEffects = value });
        Add(publication.AutomaticOccupationRange, options => options.AutomaticOccupationRange, (options, value) => options with { AutomaticOccupationRange = value });
        Add(publication.FireDamage, options => options.FireDamage, (options, value) => options with { FireDamage = value });
        Add(publication.MoraleEffects, options => options.MoraleEffects, (options, value) => options with { MoraleEffects = value });
        Add(publication.TransportMobility, options => options.TransportMobility, (options, value) => options with { TransportMobility = value });
        Add(publication.CaptureFrequency, options => options.CaptureFrequency, (options, value) => options with { CaptureFrequency = value });
        Add(publication.LandmarkEffects, options => options.LandmarkEffects, (options, value) => options with { LandmarkEffects = value });
        Add(publication.InitialCreditEnabled, options => options.InitialCreditEnabled, (options, value) => options with { InitialCreditEnabled = value });
        Add(publication.MeritGain, options => options.MeritGain, (options, value) => options with { MeritGain = value });
        Add(publication.CapturedOfficersEnabled, options => options.CapturedOfficersEnabled, (options, value) => options with { CapturedOfficersEnabled = value });
        SelectGroup(0); Refresh();

        void Add<T>(NewGameSettingPreview<T> setting, Func<NewGameOptions, T?> read,
            Func<NewGameOptions, T, NewGameOptions> write) where T : struct
        {
            var row = new Control { Name = $"Setting{setting.Group}_{setting.Row}",
                Position = rowPositions[setting.Row], Size = new(1128, 64), MouseFilter = MouseFilterEnum.Ignore };
            groups[setting.Group].AddChild(row);
            var rowSource = new UiSourceLayout(repository, setting.Choices.Count > 4 ? 0x7F1F1D88u : 0x28012CA9u);
            rowSource.Art(row, 2, rowSource.Rectangle(2), chrome.Assets, chrome.SpriteShader);
            rowSource.Text(row, 3, default, setting.Title ?? text.Get("detail_unknown"), chrome.Font);
            var slots = rowSource.Nodes.Where(node => node.ParentIndex == 0 && node.Type == 0 &&
                node.Name.StartsWith("cmn_btn_radio_", StringComparison.Ordinal))
                .Select(node => rowSource.Rectangle(node.Index)).OrderBy(rectangle => rectangle.Position.X).ToArray();
            float start = slots[0].Position.X, end = slots[^1].End.X;
            float width = (end - start) / setting.Choices.Count;
            for (int index = 0; index < setting.Choices.Count; index++)
            {
                var choice = setting.Choices[index];
                var button = new UiSourceRadioButton { Name = $"SettingChoice{setting.Group}_{setting.Row}_{choice.RadioIndex}",
                    Position = new(start + width * index, slots[0].Position.Y), Size = new(width, slots[0].Size.Y),
                    InputAdmission = ReadAdmission };
                row.AddChild(button); buttons.Add(button);
                var segment = index == 0 ? UiSourceRadioSegment.Left : index == setting.Choices.Count - 1 ?
                    UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre;
                button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, segment,
                    choice.Text ?? text.Get("detail_unknown"), () =>
                    {
                        if (!ReadAdmission() || choice.CanSelect != true) return;
                        Draft = write(Draft, choice.Value); Refresh(); this.changed?.Invoke(Draft);
                    });
                refresh.Add(() =>
                {
                    T? value = read(Draft) ?? setting.EffectiveValue;
                    if (value is T selected && !setting.Choices.Any(item => item.CanSelect == true &&
                        EqualityComparer<T>.Default.Equals(item.Value, selected))) value = setting.EffectiveValue;
                    button.Selected = value.HasValue && EqualityComparer<T>.Default.Equals(value.Value, choice.Value);
                    button.Disabled = busy || choice.CanSelect != true;
                });
            }
        }
    }

    private static NewGameDifficulty? Difficulty(string id) => id switch
    {
        "beginner" => NewGameDifficulty.Beginner, "intermediate" => NewGameDifficulty.Intermediate,
        "advanced" => NewGameDifficulty.Advanced, "super" => NewGameDifficulty.Super,
        "top" => NewGameDifficulty.Top, _ => null
    };
    private bool ReadAdmission() => !abandoned && !busy && IsVisibleInTree() && CanProcess() && (admitted?.Invoke() ?? false);
    private void SelectGroup(int group)
    {
        CurrentGroup = group;
        for (int index = 0; index < groups.Count; index++) { groups[index].Visible = index == group; tabs[index].Selected = index == group; }
    }
    private void Refresh() { foreach (var update in refresh) update(); }
    internal void SetBusy(bool value)
    { busy = value; foreach (var tab in tabs) tab.Disabled = value; Refresh(); }
    internal void Abandon()
    {
        abandoned = true; changed = null; admitted = null; refresh.Clear();
        foreach (var button in buttons) { button.InputAdmission = null; button.Abandon(); }
        foreach (var tab in tabs) { tab.InputAdmission = null; tab.Disabled = true; }
        Hide();
    }
}
