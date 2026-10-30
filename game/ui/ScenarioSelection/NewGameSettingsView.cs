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
    private readonly Dictionary<(int Group, int Row), UiSourceRadioButton[]> readOnlyButtons = [];
    private readonly Dictionary<(int Group, int Row), NewGameConfirmationSetting> summary = [];
    internal IReadOnlyList<NewGameConfirmationSetting> CaptureConfirmationSettings() => summary
        .OrderBy(pair => pair.Key.Group).ThenBy(pair => pair.Key.Row).Select(pair => pair.Value).ToArray();
    private readonly Dictionary<Control, (int Group, int Row, int Choice)> helpSources = [];
    private UiSourceFixedHelp help = null!;
    private AdministrationMessages messages = null!;
    private bool helpPending;
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
        Vector2 adviceMount = layout.Rectangle(1).Position;
        advice.Art(this, 1, advice.Rectangle(1, adviceMount), chrome.Assets, chrome.SpriteShader);
        messages = new AdministrationMessages(repository, text.SourceLocale);
        ConfigureHelp(advice, adviceMount, chrome);
        var tabLayout = new UiSourceLayout(repository, 0x5C9D1E9E);
        Vector2 tabMount = layout.Rectangle(12).Position;
        var data = repository.LoadMessages(text.SourceLocale, MessageKind.Data).Messages.Sections[1];
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
            tab.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption, ScenarioSettingsSourceLayout.TabLayout(group));
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
        Add(publication.DisasterFrequency, options => options.DisasterFrequency, (options, value) => options with { DisasterFrequency = value });
        Add(publication.TacticDamage, options => options.TacticDamage, (options, value) => options with { TacticDamage = value });
        Add(publication.DurabilityRecovery, options => options.DurabilityRecovery, (options, value) => options with { DurabilityRecovery = value });
        Add(publication.SupplyInterdictionEffects, options => options.SupplyInterdictionEffects, (options, value) => options with { SupplyInterdictionEffects = value });
        Add(publication.ForceMergingEnabled, options => options.ForceMergingEnabled, (options, value) => options with { ForceMergingEnabled = value });
        Add(publication.ForeignTradeTravelDuration, options => options.ForeignTradeTravelDuration, (options, value) => options with { ForeignTradeTravelDuration = value });
        Add(publication.SpecialForceAttacksEnabled, options => options.SpecialForceAttacksEnabled, (options, value) => options with { SpecialForceAttacksEnabled = value });
        Add(publication.LandmarkInvestmentCost, options => options.LandmarkInvestmentCost, (options, value) => options with { LandmarkInvestmentCost = value });
        Add(publication.LandmarkInvestmentDuration, options => options.LandmarkInvestmentDuration, (options, value) => options with { LandmarkInvestmentDuration = value });
        Add(publication.IntelligenceGain, options => options.IntelligenceGain, (options, value) => options with { IntelligenceGain = value });
        Add(publication.HiredOfficersEnabled, options => options.HiredOfficersEnabled, (options, value) => options with { HiredOfficersEnabled = value });
        Add(publication.FemaleOfficers, options => options.FemaleOfficers,
            (options, value) => options with { FemaleOfficers = value });
        Add(publication.WanderingOfficerCities, options => options.WanderingOfficerCities,
            (options, value) => options with { WanderingOfficerCities = value });
        Add(publication.OfficerBloodRelationships, options => options.OfficerBloodRelationships,
            (options, value) => options with { OfficerBloodRelationships = value });
        Add(publication.HistoricalEventsEnabled, options => options.HistoricalEventsEnabled,
            (options, value) => options with { HistoricalEventsEnabled = value });
        Add(publication.ProposalsEnabled, options => options.ProposalsEnabled,
            (options, value) => options with { ProposalsEnabled = value }, publication.ProposalsEnabledFor);
        Add(publication.EnemyArmyInformation, options => options.EnemyArmyInformation,
            (options, value) => options with { EnemyArmyInformation = value }, publication.EnemyArmyInformationFor);
        foreach (var setting in publication.ReadOnlyRows)
        {
            var (row, rowSource) = CreateRow(setting.Group, setting.Row, setting.Title, setting.Choices.Count);
            var rowButtons = new UiSourceRadioButton[setting.Choices.Count];
            for (int index = 0; index < rowButtons.Length; index++)
            {
                var choice = setting.Choices[index];
                var radio = CreateRadio(row, rowSource, setting.Group, setting.Row, index, setting.Choices.Count,
                    choice.Text, static () => false, static () => { });
                rowButtons[index] = radio;
            }
            readOnlyButtons.Add((setting.Group, setting.Row), rowButtons);
        }
        SelectGroup(0); Refresh();

        void Add<T>(NewGameSettingPreview<T> setting, Func<NewGameOptions, T?> read,
            Func<NewGameOptions, T, NewGameOptions> write,
            Func<NewGameOptions, NewGameSettingPreview<T>>? project = null) where T : struct
        {
            var (row, rowSource) = CreateRow(setting.Group, setting.Row, setting.Title ?? text.Get("detail_unknown"), setting.Choices.Count);
            var rowButtons = new UiSourceRadioButton[setting.Choices.Count];
            var current = setting;
            for (int index = 0; index < rowButtons.Length; index++)
            {
                int choiceIndex = index;
                rowButtons[index] = CreateRadio(row, rowSource, setting.Group, setting.Row, index, rowButtons.Length,
                    setting.Choices[index].Text ?? text.Get("detail_unknown"),
                    () => ReadAdmission() && current.Choices[choiceIndex].CanSelect == true,
                    () =>
                    {
                        if (!ReadAdmission() || current.Choices[choiceIndex].CanSelect != true) return;
                        Draft = write(Draft, current.Choices[choiceIndex].Value); Refresh(); this.changed?.Invoke(Draft);
                    });
            }
            refresh.Add(() =>
            {
                current = project?.Invoke(Draft) ?? setting;
                T? value = read(Draft) ?? current.EffectiveValue;
                if (value is T chosen && !current.Choices.Any(item => item.CanSelect == true &&
                    EqualityComparer<T>.Default.Equals(item.Value, chosen))) value = current.EffectiveValue;
                summary[(setting.Group, setting.Row)] = new(setting.TitleMessageId,
                    value.HasValue ? current.Choices.FirstOrDefault(choice => EqualityComparer<T>.Default.Equals(choice.Value, value.Value))?.Text : null);
                for (int index = 0; index < rowButtons.Length; index++)
                {
                    var choice = current.Choices[index];
                    bool selected = value.HasValue && EqualityComparer<T>.Default.Equals(value.Value, choice.Value);
                    rowButtons[index].Selected = selected;
                    // Like the original read-only selection, a forced value
                    // keeps its push artwork while the permission gate denies edits.
                    rowButtons[index].Disabled = busy || choice.CanSelect != true && !selected;
                }
            });
        }

        (Control Row, UiSourceLayout Source) CreateRow(int group, int index, string caption, int count)
        {
            var box = ScenarioSettingsSourceLayout.Row(layout, index);
            var row = new Control { Name = $"Setting{group}_{index}", Position = box.Position,
                Size = box.Size, MouseFilter = MouseFilterEnum.Pass };
            groups[group].AddChild(row);
            row.SetMeta("source_mount_index", ScenarioSettingsSourceLayout.RowMount(index));
            var source = ScenarioSettingsSourceLayout.RadioRow(repository, count);
            source.Art(row, 2, source.Rectangle(2), chrome.Assets, chrome.SpriteShader);
            source.Text(row, 3, default, caption, chrome.Font);
            BindHelp(row, group, index, 0);
            return (row, source);
        }

        UiSourceRadioButton CreateRadio(Control row, UiSourceLayout source, int group, int index,
            int ordinal, int count, string caption, Func<bool> admission, Action released)
        {
            Rect2 box = ScenarioSettingsSourceLayout.Radio(source, ordinal, count);
            var radio = new UiSourceRadioButton { Name = $"SettingChoice{group}_{index}_{ordinal}",
                Position = box.Position, Size = box.Size, InputAdmission = admission };
            row.AddChild(radio); buttons.Add(radio);
            var segment = ordinal == 0 ? UiSourceRadioSegment.Left : ordinal == count - 1 ?
                UiSourceRadioSegment.Right : UiSourceRadioSegment.Centre;
            radio.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, segment, caption, released);
            BindHelp(radio, group, index, ordinal);
            return radio;
        }
    }

    private void ConfigureHelp(UiSourceLayout source, Vector2 mount, AdministrationPageChrome chrome)
    {
        Rect2 box = source.Rectangle(2, mount);
        help = new UiSourceFixedHelp { Name = "NewGameSettingsHelp", Position = box.Position, Size = box.Size };
        AddChild(help);
        help.Configure(chrome.Font, MapFontProfile.FromNode(source.Nodes[2], source.Source.Source.ResourceUri));
        help.Apply(messages.Format(0x2790));
    }
    private void BindHelp(Control control, int group, int row, int choice)
    {
        helpSources.Add(control, (group, row, choice));
        control.MouseEntered += RequestHelpRefresh;
        control.MouseExited += RequestHelpRefresh;
    }
    private void RequestHelpRefresh()
    {
        if (abandoned || helpPending) return;
        helpPending = true;
        Callable.From(RefreshPointerHelp).CallDeferred();
    }
    private void RefreshPointerHelp()
    {
        helpPending = false;
        if (!ReadAdmission()) return;
        int message = 0x2790;
        for (var hovered = GetViewport().GuiGetHoveredControl(); hovered is not null; hovered = hovered.GetParent() as Control)
        {
            if (helpSources.TryGetValue(hovered, out var source))
            {
                message = ScenarioSettingsSourceLayout.Help(source.Group, source.Row, source.Choice,
                    Difficulty(Draft.DifficultyId) == NewGameDifficulty.Top);
                break;
            }
            if (hovered == this) break;
        }
        help.Apply(messages.Format(message));
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
        help.Apply(messages.Format(0x2790));
    }
    private void Refresh()
    {
        foreach (var update in refresh) update();
        foreach (var setting in Publication.ReadOnlyRowsFor(Draft))
        {
            var row = readOnlyButtons[(setting.Group, setting.Row)];
            summary[(setting.Group, setting.Row)] = new(setting.TitleMessageId,
                setting.Choices.FirstOrDefault(choice => choice.RadioIndex == setting.SelectedRadioIndex)?.Text);
            for (int index = 0; index < row.Length; index++)
            {
                bool selected = setting.SelectedRadioIndex == setting.Choices[index].RadioIndex;
                row[index].Selected = selected; row[index].Disabled = busy || !selected;
            }
        }
        RequestHelpRefresh();
    }
    internal void SetBusy(bool value)
    { busy = value; foreach (var tab in tabs) tab.Disabled = value; Refresh(); }
    internal void Abandon()
    {
        abandoned = true; changed = null; admitted = null; refresh.Clear(); helpSources.Clear(); readOnlyButtons.Clear(); summary.Clear();
        foreach (var button in buttons) { button.InputAdmission = null; button.Abandon(); }
        foreach (var tab in tabs) { tab.InputAdmission = null; tab.Disabled = true; }
        Hide();
    }
}
