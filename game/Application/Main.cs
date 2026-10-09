using Godot;
using San14.Data;
using San14.Data.Formats;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main : Control
{
    private const int NoTacticId = int.MaxValue;
    private const int NoGovernanceOfficerId = int.MaxValue;
    private const int PlayableSaveSlotCount = 10;
    private GameResourceRepository? _repository;
    private MapDocument? _map;
    private LoadedMap? _loadedMap;
    private MapSurface? _mapSurface;
    private bool _mapSurfaceAttempted;
    private ScenarioTerritory? _territory;
    private GameplaySession? _session;
    private GameState? _world => _session?.World;
    private MapCell? _selectedCell;
    private string? _loadError;
    private PreviewText _ui = null!;
    private PreviewText? _safeUi;
    private bool _failed;
    private ResourceOverrides _overrides = null!;
    private readonly List<S14Entry> _shownScenarios = [];
    private readonly List<EffectiveScenario> _shownSelections = [];
    private readonly List<int> _shownForces = [];
    private ScenarioCityPreview? _selectedSnapshot;
    private S14Entry? _selectedScenario;
    private int? _selectedForceSlot;
    private Label _title = null!;
    private Label _hint = null!;
    private Label _languageLabel = null!;
    private Label _scenarioLabel = null!;
    private Label _difficultyLabel = null!;
    private Label _newGameOptionNotice = null!;
    private Label _forceLabel = null!;
    private Label _mapTitle = null!;
    private HBoxContainer _previewRow = null!;
    private VBoxContainer _sidebar = null!;
    private ScrollContainer _commandScroll = null!;
    private Button _mapFocusToggle = null!;
    private bool _mapFocused;
    private Label _mapHint = null!;
    private Label _assetHint = null!;
    private ItemList _scenarioList = null!;
    private OptionButton _difficulty = null!;
    private CheckBox _initialCredit = null!;
    private CheckBox _capturedOfficers = null!;
    private NewGameOptions _newGameOptions = new("intermediate", true, true);
    private static readonly string[] DifficultyIds = ["beginner", "intermediate", "advanced", "super", "top"];
    private RichTextLabel _description = null!;
    private Label _status = null!;
    private Label _tileInfo = null!;
    private TextureRect _mapTexture = null!;
    private Control _mapHost = null!;
    private MapSceneView? _residentMap;
    private PlayableForceHud? _forceHud;
    private TextureRect _assetPreview = null!;
    private OptionButton _locale = null!;
    private OptionButton _force = null!;
    private OptionButton _mapMode = null!;
    private Button _saveInitial = null!;
    private Button _loadInitial = null!;
    private Label _worldStatus = null!;
    private Button _openForceDetails = null!;
    private Button _openCityDetails = null!;
    private OptionButton _city = null!;
    private OptionButton _officer = null!;
    private Button _openOfficerDetails = null!;
    private DetailsView? _detailsView;
    private MapSceneView? _sceneView;
    private Button _openScenePreview = null!;
    private Control _browserContent = null!;
    private bool _consumeBackRelease;
    private int? _playerForceId => _session?.PlayerForceId;
    private PlayableRuleSet? _playRules => _session?.Rules;
    private Label _playTitle = null!;
    private Label _playStatus = null!;
    private Label _playMessage = null!;
    private RichTextLabel _playReport = null!;
    private Label _playTarget = null!;
    private Label _playCityLabel = null!;
    private Label _playAreaLabel = null!;
    private Label _playAdminOfficerLabel = null!;
    private Label _playOfficerLabel = null!;
    private Label _playDutyLabel = null!;
    private Label _playDutyStatus = null!;
    private Label _playUnitLabel = null!;
    private Label _playPreparedLabel = null!;
    private Label _playFocusLabel = null!;
    private Label _playTroopsLabel = null!;
    private Label _playFormationLabel = null!;
    private Label _playTransportCityLabel = null!;
    private Label _playTransportSuppliesLabel = null!;
    private Label _playTransportGoldLabel = null!;
    private Label _playTransportArrivalLabel = null!;
    private Label _playMoveOfficerLabel = null!;
    private Label _playMoveCityLabel = null!;
    private Label _playSummonOfficerLabel = null!;
    private Label _playMoveStatus = null!;
    private OptionButton _playCity = null!;
    private OptionButton _playArea = null!;
    private OptionButton _playAdminOfficer = null!;
    private OptionButton _playOfficer = null!;
    private OptionButton _playFormation = null!;
    private readonly OptionButton[] _playTactics = new OptionButton[3];
    private OptionButton _playDutyOfficer = null!;
    private OptionButton _playUnit = null!;
    private OptionButton _playPrepared = null!;
    private OptionButton _playFocus = null!;
    private OptionButton _playTransportCity = null!;
    private OptionButton _playTransportArrival = null!;
    private OptionButton _playMoveOfficer = null!;
    private OptionButton _playMoveCity = null!;
    private OptionButton _playSummonOfficer = null!;
    private Label _playTalentExecutorLabel = null!;
    private Label _playTalentTargetLabel = null!;
    private Label _playTalentStatus = null!;
    private OptionButton _playTalentExecutor = null!;
    private OptionButton _playTalentTarget = null!;
    private Button _playTalentSearch = null!;
    private Button _playTalentRecruit = null!;
    private SpinBox _playTroops = null!;
    private SpinBox _playTransportSupplies = null!;
    private SpinBox _playTransportGold = null!;
    private Button _playStart = null!;
    private Button _playAdmin = null!;
    private Button _playDeploy = null!;
    private Button _playDeployOccupy = null!;
    private Button _playDeployBuild = null!;
    private Button _playTransport = null!;
    private Button _playMove = null!;
    private Button _playSummon = null!;
    private Button _playRecruiter = null!;
    private Button _playTrainer = null!;
    private Button _playRetarget = null!;
    private Label _playStructureKindLabel = null!;
    private OptionButton _playStructureKind = null!;
    private string? _playStructureCatalogRevision;
    private Button _playBuild = null!;
    private Button _playRepair = null!;
    private Button _playDemolish = null!;
    private Button _playOccupy = null!;
    private Button _playWithdraw = null!;
    private Button _playDisband = null!;
    private Button _playCancelDeployment = null!;
    private Button _playAdvance = null!;
    private Button _playFinishReport = null!;
    private Button _playSave = null!;
    private Button _playContinue = null!;
    private OptionButton _playSaveSlot = null!;
    private Label _playSaveSlotLabel = null!;
    private Label _playGovernanceLabel = null!;
    private OptionButton _playGovernanceSlot = null!;
    private OptionButton _playGovernanceOfficer = null!;
    private Button _playGovernanceApply = null!;
    private Label _playGovernanceStatus = null!;
    private PlayableCommandDeck _playCommandDeck = null!;

    public override void _Input(InputEvent input)
    {
        if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Right } button) return;
        if (!button.Pressed)
        {
            if (_consumeBackRelease) GetViewport().SetInputAsHandled();
            _consumeBackRelease = false;
            return;
        }
        if (_failed) return;
        Action? back = null;
        if (_detailsView is { } details && details.IsVisibleInTree() && !details.IsQueuedForDeletion())
            back = details.RequestClose;
        else if (_periodReportView is { } report && report.IsVisibleInTree() && !report.IsQueuedForDeletion())
            back = report.RequestAcknowledge;
        else if (_sceneView is null && _residentMap is not null &&
                 _world is { Phase: TurnPhase.Reporting })
            back = () => RunPlayAction(FinishPlayableReport);
        else if (_sceneView is null && _residentMap is not null && !_mapFocused)
            back = () => SetMapFocus(true);
        if (back is null) return;
        // Back is routed before GUI dispatch. Neither half of this click should
        // reach a map or command panel exposed by closing the current layer.
        GetViewport().SetInputAsHandled();
        _consumeBackRelease = true;
        PlayUiAudio(cancel: true);
        RunUiAction(back);
    }

    public override void _Ready()
    {
        try
        {
            _safeUi = new PreviewText(false, null);
            string? configured = System.Environment.GetEnvironmentVariable("SAN14_OVERRIDE_DIR");
            _overrides = new ResourceOverrides(string.IsNullOrWhiteSpace(configured)
                ? ProjectSettings.GlobalizePath("user://overrides") : configured);
            BuildInterface();
            _repository = new GameResourceRepository(overridesDirectory: _overrides.DirectoryPath);
            _map = _repository.LoadCeMap().Map;
            DrawMap();
            RefreshScenarios();
            RefreshStatus();
            GD.Print($"SAN14 source {_repository.SourceDirectory}: " +
                $"{_repository.EffectiveScenarios.Count} scenarios, map {_map.Width}x{_map.Height}.");
            BeginUiAudio();
        }
        catch (Exception error)
        {
            ShowFailure(error, startup: true);
        }
    }

    private void RunUiAction(Action action)
    {
        if (_failed) return;
        try { action(); }
        catch (Exception error) { ShowFailure(error, startup: false); }
    }

    private void ShowFailure(Exception error, bool startup)
    {
        _failed = true;
        _loadError = error.Message;
        _session = null;
        GD.PushError(error.ToString());
        foreach (Node child in GetChildren()) child.QueueFree();
        var background = new ColorRect { Color = new Color("#231714") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 32);
        AddChild(margin);
        var panel = new VBoxContainer();
        margin.AddChild(panel);
        var title = new Label
        {
            Text = startup ? _safeUi?.StartupFailed ?? "Startup failed" :
                _safeUi?.ClientFailed ?? "Client operation failed"
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        panel.AddChild(title);
        panel.AddChild(new Label
        {
            Text = startup ? _safeUi?.StartupFailedHelp ?? error.GetType().Name :
                _safeUi?.ClientFailedHelp ?? error.GetType().Name,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        panel.AddChild(new RichTextLabel
        {
            Text = error.Message,
            BbcodeEnabled = false,
            ScrollActive = true,
            SizeFlagsVertical = SizeFlags.ExpandFill
        });
    }

    private void BuildInterface()
    {
        _ui = new PreviewText(false, _overrides);
        var background = new ColorRect { Color = new Color("#111b1d") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        _browserContent = margin;
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        AddChild(margin);

        var vertical = new VBoxContainer();
        margin.AddChild(vertical);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 28);
        vertical.AddChild(_title);
        _hint = new Label();
        vertical.AddChild(_hint);

        var body = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        vertical.AddChild(body);

        var sidebar = new VBoxContainer { CustomMinimumSize = new Vector2(390, 0) };
        _sidebar = sidebar;
        body.AddChild(sidebar);
        _languageLabel = new Label();
        sidebar.AddChild(_languageLabel);
        _locale = new OptionButton { Name = "LanguageSelection" };
        _locale.AddItem(_ui.LocaleSimplified);
        _locale.AddItem(_ui.LocaleTraditional);
        sidebar.AddChild(_locale);
        _scenarioLabel = new Label();
        sidebar.AddChild(_scenarioLabel);
        _scenarioList = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _scenarioList.ItemSelected += index => RunUiAction(() => ShowScenario(index));
        sidebar.AddChild(_scenarioList);
        var difficultyRow = new HBoxContainer();
        _difficultyLabel = new Label();
        difficultyRow.AddChild(_difficultyLabel);
        _difficulty = new OptionButton { Name = "DifficultySelection", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        for (int index = 0; index < DifficultyIds.Length; index++) _difficulty.AddItem("", index);
        _difficulty.Select(1);
        _difficulty.ItemSelected += _ => RunUiAction(ChangeNewGameOptions);
        difficultyRow.AddChild(_difficulty);
        sidebar.AddChild(difficultyRow);
        var initialChoices = new HBoxContainer();
        _initialCredit = new CheckBox { Name = "InitialCreditSelection", ButtonPressed = true };
        _capturedOfficers = new CheckBox { Name = "CapturedOfficersSelection", ButtonPressed = true };
        _initialCredit.Toggled += _ => RunUiAction(ChangeNewGameOptions);
        _capturedOfficers.Toggled += _ => RunUiAction(ChangeNewGameOptions);
        initialChoices.AddChild(_initialCredit);
        initialChoices.AddChild(_capturedOfficers);
        sidebar.AddChild(initialChoices);
        _newGameOptionNotice = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        sidebar.AddChild(_newGameOptionNotice);
        _forceLabel = new Label();
        sidebar.AddChild(_forceLabel);
        _force = new OptionButton();
        _force.ItemSelected += index => RunUiAction(() => ShowForce(index));
        sidebar.AddChild(_force);
        _openForceDetails = new Button { Name = "OpenForceDetails", Disabled = true };
        _openForceDetails.Pressed += () => RunUiAction(OpenForceDetails);
        sidebar.AddChild(_openForceDetails);
        var cityRow = new HBoxContainer();
        _city = new OptionButton { Name = "CitySelection", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _openCityDetails = new Button { Name = "OpenCityDetails", Disabled = true };
        _openCityDetails.Pressed += () => RunUiAction(OpenCityDetails);
        cityRow.AddChild(_city);
        cityRow.AddChild(_openCityDetails);
        sidebar.AddChild(cityRow);
        var officerRow = new HBoxContainer();
        _officer = new OptionButton { Name = "OfficerSelection", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _openOfficerDetails = new Button { Name = "OpenOfficerDetails", Disabled = true };
        _openOfficerDetails.Pressed += () => RunUiAction(OpenOfficerDetails);
        officerRow.AddChild(_officer);
        officerRow.AddChild(_openOfficerDetails);
        sidebar.AddChild(officerRow);
        _openScenePreview = new Button { Name = "OpenScenePreview" };
        _openScenePreview.Pressed += () => RunUiAction(OpenScenePreview);
        sidebar.AddChild(_openScenePreview);
        _description = new RichTextLabel
        {
            CustomMinimumSize = new Vector2(0, 280),
            FitContent = false,
            ScrollActive = true
        };
        sidebar.AddChild(_description);
        var saveButtons = new HBoxContainer();
        _saveInitial = new Button { Name = "SaveInitialState", Disabled = true };
        _loadInitial = new Button { Name = "LoadInitialState" };
        _saveInitial.Pressed += () => RunUiAction(SaveInitialState);
        _loadInitial.Pressed += () => RunUiAction(LoadInitialState);
        saveButtons.AddChild(_saveInitial);
        saveButtons.AddChild(_loadInitial);
        sidebar.AddChild(saveButtons);
        _worldStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        sidebar.AddChild(_worldStatus);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(right);
        var previewRow = new HBoxContainer();
        _previewRow = previewRow;
        right.AddChild(previewRow);
        var previewLabels = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        previewRow.AddChild(previewLabels);
        _mapTitle = new Label();
        _mapHint = new Label();
        _assetHint = new Label();
        previewLabels.AddChild(_mapTitle);
        previewLabels.AddChild(_mapHint);
        previewLabels.AddChild(_assetHint);
        _mapMode = new OptionButton();
        _mapMode.AddItem(_ui.TerritoryMap);
        _mapMode.AddItem(_ui.EncodingMap);
        _mapMode.ItemSelected += _ => RunUiAction(DrawMap);
        previewLabels.AddChild(_mapMode);
        _assetPreview = new TextureRect
        {
            CustomMinimumSize = new Vector2(220, 220),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
        };
        previewRow.AddChild(_assetPreview);
        var playScroll = new ScrollContainer
        {
            Name = "PlayableCommandScroll", CustomMinimumSize = new Vector2(0, 300),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        _commandScroll = playScroll;
        right.AddChild(playScroll);
        var playHost = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        playScroll.AddChild(playHost);
        BuildPlayControls(playHost);
        _mapHost = new Control
        {
            Name = "MapHost", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _mapHost.Resized += () => _forceHud?.FitToMap(_mapHost.Size);
        right.AddChild(_mapHost);
        _mapTexture = new TextureRect
        {
            Name = "PlayableMapTexture",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale
        };
        _mapTexture.GuiInput += input => RunUiAction(() => OnMapInput(input));
        _mapHost.AddChild(_mapTexture);
        _mapTexture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _mapFocusToggle = new Button { Name = "ToggleMapCommands", Visible = false };
        _mapFocusToggle.Pressed += () => RunUiAction(() => SetMapFocus(!_mapFocused));
        _mapHost.AddChild(_mapFocusToggle);
        _mapFocusToggle.AnchorLeft = 1;
        _mapFocusToggle.AnchorRight = 1;
        _mapFocusToggle.OffsetLeft = -150;
        _mapFocusToggle.OffsetRight = -12;
        _mapFocusToggle.OffsetTop = 12;
        _mapFocusToggle.OffsetBottom = 54;
        _tileInfo = new Label();
        right.AddChild(_tileInfo);
        _status = new Label();
        vertical.AddChild(_status);
        _uiAudioStatus = new Label { Name = "OriginalUiAudioStatus" };
        vertical.AddChild(_uiAudioStatus);
        _sceneAudioStatus = new Label { Name = "OriginalSceneAudioStatus", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        vertical.AddChild(_sceneAudioStatus);
        _locale.ItemSelected += _ => RunUiAction(() =>
        {
            UpdateLocale();
            RefreshScenarios();
        });
        UpdateLocale();
    }

    private void UpdateLocale()
    {
        _safeUi = new PreviewText(_locale.Selected == 1, null);
        _ui = new PreviewText(_locale.Selected == 1, _overrides);
        _locale.SetItemText(0, _ui.LocaleSimplified);
        _locale.SetItemText(1, _ui.LocaleTraditional);
        _title.Text = _ui.Title;
        _hint.Text = _ui.Hint;
        _languageLabel.Text = _ui.Language;
        _scenarioLabel.Text = _ui.Scenarios;
        _difficultyLabel.Text = _ui.Get("new_game_difficulty");
        for (int index = 0; index < DifficultyIds.Length; index++)
            _difficulty.SetItemText(index, _ui.Get("new_game_difficulty_" + DifficultyIds[index]));
        UpdateNewGameCheckTexts();
        _newGameOptionNotice.Text = _ui.Get(_world?.NewGameOptions is null && _world is not null
            ? "new_game_options_unknown" : "new_game_options_pending");
        _forceLabel.Text = _ui.ForceSelection;
        _openForceDetails.Text = _ui.Get("detail_open");
        _openCityDetails.Text = _ui.Get("detail_city_open");
        _city.TooltipText = _ui.Get("detail_city_selection");
        _openOfficerDetails.Text = _ui.Get("detail_officer_open");
        _officer.TooltipText = _ui.Get("detail_officer_selection");
        _openScenePreview.Text = _ui.Get("scene_preview_open");
        _mapFocusToggle.Text = _ui.Get(_mapFocused ? "play_map_show_commands" : "play_map_hide_commands");
        _mapTitle.Text = _ui.MapTitle;
        _mapHint.Text = _mapSurfaceAttempted && _mapSurface is null
            ? _ui.Get("map_surface_fallback") : _ui.MapHint;
        _assetHint.Text = _ui.AssetHint;
        _mapMode.SetItemText(0, _ui.TerritoryMap);
        _mapMode.SetItemText(1, _ui.EncodingMap);
        _saveInitial.Text = _ui.SaveInitialState;
        _loadInitial.Text = _ui.LoadInitialState;
        _worldStatus.Text = _ui.InitialStateHint;
        UpdatePlayLocale();
        RefreshTileInfo();
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (_loadError is not null)
        {
            _status.Text = _ui.LoadError(_loadError);
            return;
        }
        if (_repository is null)
        {
            _status.Text = _ui.Loading;
            return;
        }
        _status.Text = _ui.LoadedStatus(_repository.EffectiveScenarios.Count);
    }

    private void RefreshScenarios()
    {
        _scenarioList.Clear();
        _shownScenarios.Clear();
        _shownSelections.Clear();
        if (_repository is null) return;
        string locale = _locale.Selected == 1 ? "TC" : "SC";
        var documents = _repository.ScenarioDocuments.ToDictionary(item => item.SourcePath,
            StringComparer.OrdinalIgnoreCase);
        foreach (EffectiveScenario scenario in _repository.EffectiveScenarios
                     .Where(item => item.Locale == locale)
                     .OrderBy(item => item.Number))
        {
            S14Entry entry = documents[scenario.SourcePath];
            _shownScenarios.Add(entry);
            _shownSelections.Add(scenario);
            string date = entry.StartYear is int year && entry.StartMonth is int month &&
                entry.StartDay is int day ? _ui.DateInline(year, month, day) : "";
            _scenarioList.AddItem(_ui.ScenarioItem(scenario.Number, date, entry.Title));
        }
        if (_shownScenarios.Count > 0)
        {
            _scenarioList.Select(0);
            ShowScenario(0);
        }
    }

    private void ShowScenario(long index) => ShowScenario(index, null);

    private void ChangeNewGameOptions()
    {
        if (_world is not { Phase: TurnPhase.Initializing } || _difficulty.Selected < 0) return;
        string difficultyId = _difficulty.Selected < DifficultyIds.Length
            ? DifficultyIds[_difficulty.Selected] : _newGameOptions.DifficultyId;
        _newGameOptions = new NewGameOptions(difficultyId,
            _initialCredit.ButtonPressed, _capturedOfficers.ButtonPressed);
        UpdateNewGameCheckTexts();
        int[] selected = _scenarioList.GetSelectedItems();
        if (selected.Length > 0) ShowScenario(selected[0]);
    }

    private void UpdateNewGameCheckTexts()
    {
        _initialCredit.Text = _ui.Get("new_game_initial_credit") + "：" +
            _ui.Get(_initialCredit.ButtonPressed ? "new_game_yes" : "new_game_no");
        _capturedOfficers.Text = _ui.Get("new_game_captured_officers") + "：" +
            _ui.Get(_capturedOfficers.ButtonPressed ? "new_game_yes" : "new_game_no");
    }

    private void SyncNewGameOptions(GameState world)
    {
        if (world.NewGameOptions is { } options)
        {
            _newGameOptions = options;
            int known = Array.IndexOf(DifficultyIds, options.DifficultyId);
            if (known < 0)
            {
                if (_difficulty.ItemCount > DifficultyIds.Length) _difficulty.RemoveItem(DifficultyIds.Length);
                _difficulty.AddItem(options.DifficultyId);
                known = DifficultyIds.Length;
            }
            else if (_difficulty.ItemCount > DifficultyIds.Length)
                _difficulty.RemoveItem(DifficultyIds.Length);
            _difficulty.Select(known);
            _initialCredit.SetPressedNoSignal(options.InitialCreditEnabled);
            _capturedOfficers.SetPressedNoSignal(options.CapturedOfficersEnabled);
        }
        else
        {
            _newGameOptions = new NewGameOptions("intermediate", true, true);
            if (_difficulty.ItemCount > DifficultyIds.Length) _difficulty.RemoveItem(DifficultyIds.Length);
            _difficulty.Select(1);
            _initialCredit.SetPressedNoSignal(true);
            _capturedOfficers.SetPressedNoSignal(true);
        }
        UpdateNewGameCheckTexts();
        _newGameOptionNotice.Text = _ui.Get(world.NewGameOptions is null
            ? "new_game_options_unknown" : "new_game_options_pending");
        bool editable = world.Phase == TurnPhase.Initializing;
        _difficulty.Disabled = !editable;
        _initialCredit.Disabled = !editable;
        _capturedOfficers.Disabled = !editable;
    }

    private void ShowScenario(long index, GameState? restored, int? restoredPlayerForceId = null)
    {
        if (index < 0 || index >= _shownScenarios.Count || _repository is null) return;
        EffectiveScenario selection = _shownSelections[(int)index];
        CampaignLoadResult campaign = CampaignComposition.Load(_repository, selection,
            _newGameOptions, restored, restoredPlayerForceId);
        LoadedScenario loaded = campaign.Composition.Scenario;
        LoadedMap loadedMap = campaign.Composition.Map;
        S14Entry selected = loaded.Metadata;
        ScenarioCityPreview snapshot = loaded.Snapshot;
        GameState world = campaign.Session.World;
        // Prepare source validation and campaign rules before replacing the live
        // session. A failed load must leave its player, orders and save path usable.
        ResetMilitaryDeploymentComposition();
        LeaveSceneAudioContext();
        CloseResidentMap();
        _loadError = null;
        _map = loadedMap.Map;
        _campaignComposition = campaign.Composition;
        _loadedMap = loadedMap;
        _territory = campaign.Territory;
        _session = campaign.Session;
        SyncNewGameOptions(world);
        PublishCampaignPresentation(campaign.Presentation);
        _detailsView?.QueueFree();
        _detailsView = null;
        _selectedForceSlot = null;
        _openForceDetails.Disabled = true;
        _openCityDetails.Disabled = true;
        _city.Clear();
        _officer.Clear();
        _openOfficerDetails.Disabled = true;
        _saveInitial.Disabled = false;
        _worldStatus.Text = _ui.InitialStateHint;
        _selectedCell = null;
        _selectedSnapshot = snapshot;
        _selectedScenario = selected;
        _shownForces.Clear();
        _force.Clear();
        foreach (ForceState force in world.Forces.Values.Where(force => force.IsActive ||
                     force.Id == restoredPlayerForceId).OrderBy(force => force.Id))
        {
            int cityCount = world.Cities.Values.Count(city => city.ForceId == force.Id);
            _shownForces.Add(force.Id);
            _force.AddItem(_ui.ForceItem(force.Id, force.Name, cityCount));
        }
        if (_shownForces.Count > 0)
        {
            int defaultIndex = selection.Number == 1 && world.Cities.TryGetValue(7, out CityState? defaultCity) &&
                defaultCity.ForceId is int defaultForceId ? _shownForces.IndexOf(defaultForceId) : -1;
            if (defaultIndex < 0) defaultIndex = 0;
            _force.Select(defaultIndex);
            ShowForce(defaultIndex);
        }
        else
            _description.Text = $"{selected.Title}\n{selected.Description}";
        RefreshStatus();
        RefreshTileInfo();
        RefreshPlayControls();
        RefreshSceneAudioContext();
    }

    private void ShowForce(long index)
    {
        if (index < 0 || index >= _shownForces.Count ||
            _selectedSnapshot is null || _selectedScenario is null) return;
        ScenarioCityPreview snapshot = _selectedSnapshot;
        S14Entry selected = _selectedScenario;
        GameState world = _world ?? throw new InvalidDataException("Current scenario state is missing.");
        int forceSlot = _shownForces[(int)index];
        if (!world.Forces.TryGetValue(forceSlot, out ForceState? force))
            throw new InvalidDataException($"Current force {forceSlot} is missing.");
        _selectedForceSlot = forceSlot;
        _openForceDetails.Disabled = false;
        CityState[] cities = world.Cities.Values.Where(city => city.ForceId == forceSlot)
            .OrderBy(city => city.Id).ToArray();
        OfficerState[] people = world.Officers.Values.Where(person => person.ForceId == forceSlot &&
            person.Presence is null or OfficerPresence.Present)
            .OrderBy(person => person.Id).ToArray();
        DistrictState[] districts = world.Districts.Values.Where(district => district.ForceId == forceSlot)
            .OrderBy(district => district.Number).ThenBy(district => district.Id).ToArray();
        GateState[] gates = world.Gates.Values.Where(gate => gate.ForceId == forceSlot)
            .OrderBy(gate => gate.Id).ToArray();
        OfficerState? ruler = force.RulerId is int rulerId ? world.Officers[rulerId] : null;
        _city.Clear();
        foreach (CityState city in cities)
            _city.AddItem(city.Name, city.Id);
        int defaultCityId = ruler?.LocationId ?? -1;
        int defaultCityIndex = _city.GetItemIndex(defaultCityId);
        if (_city.ItemCount > 0) _city.Select(defaultCityIndex < 0 ? 0 : defaultCityIndex);
        _openCityDetails.Disabled = _city.ItemCount == 0;
        _officer.Clear();
        foreach (OfficerState officer in people)
            _officer.AddItem(officer.Name, officer.Id);
        int defaultOfficerIndex = _officer.GetItemIndex(ruler?.Id ?? -1);
        if (_officer.ItemCount > 0) _officer.Select(defaultOfficerIndex < 0 ? 0 : defaultOfficerIndex);
        _openOfficerDetails.Disabled = _officer.ItemCount == 0;
        CharacterPreviewRecord? portrait = ruler is null ? null : snapshot.Characters.FirstOrDefault(person =>
            person.CharacterId != 0 && person.CharacterId == ruler.Id);
        ShowPortrait(portrait is null ? null : portrait with { Name = ruler!.Name });
        string date = _ui.DateLine(world.Date.Year, world.Date.Month, world.Date.Day);
        string districtRows = string.Join("\n", districts.Select(district =>
            $"{_ui.DistrictNumber(district.Number)}  {_ui.DistrictLeader}{_ui.Colon}" +
            CurrentOfficerName(world, district.LeaderId)));
        OfficerAbilities? baseAbilities = ruler?.BaseAbilities;
        string abilities = ruler is null ? "" :
            $"\n{_ui.BaseAbilities}{_ui.Colon}" +
            $"{_ui.Leadership} {CurrentNumber(baseAbilities?.Leadership)} / {_ui.Strength} {CurrentNumber(baseAbilities?.Strength)} / " +
            $"{_ui.Intelligence} {CurrentNumber(baseAbilities?.Intelligence)} / {_ui.Politics} {CurrentNumber(baseAbilities?.Politics)} / " +
            $"{_ui.Charm} {CurrentNumber(baseAbilities?.Charm)}\n{_ui.BaseAbilitiesHint}";
        string rows = string.Join("\n\n", cities.Select(city =>
        {
            CityPreviewRecord? original = snapshot.Cities.FirstOrDefault(item => item.Slot == city.Id);
            string names = string.Join(_ui.ListSeparator, people.Where(person =>
                person.LocationId == city.Id)
                .Select(person => person.Name));
            string links = original is null ? _ui.Get("detail_unknown") : string.Join(_ui.ListSeparator,
                original.LinkedLocationSlots.Select(slot => CurrentLocationName(world, slot)));
            string rawLimits = original is null ? _ui.Get("detail_unknown") :
                $"{original.RawLimit0:N0} / {original.RawLimit1:N0} / {original.RawLimit2:N0}";
            return $"{city.Id:00} {city.Name}  {_ui.Governor} " +
                $"{CurrentOfficerName(world, city.GovernorId)}\n" +
                $"{CurrentDistrictName(world, city.DistrictId)}  {_ui.Size}{_ui.Colon}{CurrentCitySize(city.Size)}\n" +
                $"{_ui.CityHex}{_ui.Colon}{CurrentNumber(city.HexId)}  " +
                $"{_ui.GroupCode}{_ui.Colon}{CurrentNumber(original?.GroupCode)}\n" +
                $"{_ui.LinkedLocations}{_ui.Colon}{links}\n" +
                $"{_ui.Gold}{_ui.Colon}{city.Gold:N0}  {_ui.Supplies}{_ui.Colon}{city.Supplies:N0}  " +
                $"{_ui.Troops}{_ui.Colon}{city.Troops:N0}\n" +
                $"{_ui.Morale}{_ui.Colon}{CurrentNumber(city.Morale)}  {_ui.Durability}{_ui.Colon}{CurrentNumber(city.Durability, "N0")}\n" +
                $"{_ui.RawLimits}{_ui.Colon}{rawLimits}\n" +
                $"{_ui.OfficerList}{_ui.Colon}{names}";
        }));
        string gateRows = string.Join("\n", gates.Select(gate =>
            $"{gate.Name}  {CurrentDistrictName(world, gate.DistrictId)}  " +
            $"{_ui.Troops}{_ui.Colon}{gate.Troops:N0}"));
        int controlled = world.Territory.Values.Count(tile => tile.ForceId == forceSlot);
        int available = world.Territory.Values.Count(tile => tile.AreaId is not null);
        string land = world.Territory.Count == 0 ?
            $"{_ui.LandControlled}{_ui.Colon}{_ui.Get("detail_unknown")}  {_ui.LandRate}{_ui.Colon}{_ui.Get("detail_unknown")}\n" :
            $"{_ui.LandControlled}{_ui.Colon}{controlled:N0} / {available:N0}  " +
            $"{_ui.LandRate}{_ui.Colon}{(available == 0 ? _ui.Get("detail_unknown") : $"{100.0 * controlled / available:F1}%")}\n";
        string unresolved = snapshot.UnresolvedCharacters.Count == 0 && snapshot.UnresolvedPlacements.Count == 0
            ? "" : _ui.UnresolvedReferences(snapshot.UnresolvedCharacters.Count,
                snapshot.UnresolvedPlacements.Count) + "\n";
        _description.Text = $"{selected.Title}\n{date}\n{selected.Description}\n\n" +
            $"{_ui.SourceFile}{_ui.Colon}{selected.SourcePath}\n" +
            $"{_ui.SourceHash}{_ui.Colon}{selected.DecodedSha256}\n" +
            $"{_ui.DecodedBytes}{_ui.Colon}{selected.DecodedBytes:N0} {_ui.Bytes}\n\n" +
            unresolved +
            $"{_ui.Force} {forceSlot}{_ui.Colon}{force.Name}\n" +
            $"{_ui.CityRecords}{_ui.Colon}{cities.Length}  {_ui.Districts}{_ui.Colon}{districts.Length}  " +
            $"{_ui.Officer}{_ui.Colon}{people.Length}\n" +
            land +
            $"{_ui.Gold}{_ui.Colon}{cities.Sum(city => (long)city.Gold):N0}  " +
            $"{_ui.Supplies}{_ui.Colon}{cities.Sum(city => (long)city.Supplies):N0}  " +
            $"{_ui.Troops}{_ui.Colon}{cities.Sum(city => (long)city.Troops) + gates.Sum(gate => (long)gate.Troops):N0}\n" +
            $"{_ui.TroopsIncludesGates}\n{districtRows}{abilities}\n\n" + rows +
            (gates.Length == 0 ? "" : $"\n\n{_ui.Gates}\n{gateRows}");
        DrawMap();
        RefreshPlayControls();
    }

    private string CurrentNumber(int? value, string format = "") =>
        value is int number ? number.ToString(format) : _ui.Get("detail_unknown");

    private string CurrentOfficerName(GameState world, int? officerId)
    {
        if (officerId is not int id) return _ui.Get("detail_unknown");
        return world.Officers.TryGetValue(id, out OfficerState? officer) ? officer.Name :
            throw new InvalidDataException($"Current officer reference {id} is missing.");
    }

    private string CurrentDistrictName(GameState world, int? districtId)
    {
        if (districtId is not int id) return _ui.Get("detail_unknown");
        return world.Districts.TryGetValue(id, out DistrictState? district) ? _ui.DistrictNumber(district.Number) :
            throw new InvalidDataException($"Current district reference {id} is missing.");
    }

    private static string CurrentLocationName(GameState world, int id)
    {
        if (world.Cities.TryGetValue(id, out CityState? city)) return city.Name;
        if (world.Gates.TryGetValue(id, out GateState? gate)) return gate.Name;
        throw new InvalidDataException($"Current linked location reference {id} is missing.");
    }

    private string CurrentCitySize(CitySize? size) => size switch
    {
        CitySize.Small => _ui.CitySize(1),
        CitySize.Medium => _ui.CitySize(2),
        CitySize.Large => _ui.CitySize(3),
        CitySize.Giant => _ui.CitySize(4),
        null => _ui.Get("detail_unknown"),
        _ => throw new InvalidDataException($"Current city size {(int)size.Value} is invalid.")
    };

    private void OpenOfficerDetails()
    {
        if (_detailsView is not null || _sceneView is not null || _repository is null || _world is null ||
            _selectedSnapshot is null || _officer.Selected < 0) return;
        var view = new OfficerDetailsView();
        _detailsView = view;
        AddChild(view);
        view.Configure(_repository, _world, _selectedSnapshot, _officer.GetSelectedId(), _ui,
            () => RunUiAction(() =>
            {
                view.QueueFree();
                _detailsView = null;
                _openOfficerDetails.GrabFocus();
            }));
    }

    private void OpenScenePreview()
    {
        if (_residentMap is not null) return;
        if (_sceneView is not null || _detailsView is not null || _repository is null) return;
        var view = new MapSceneView();
        _sceneView = view;
        AddChild(view);
        _browserContent.Hide();
        var safeText = new PreviewText(_locale.Selected == 1, null);
        view.Configure(_repository, _ui, safeText,
            () => RunUiAction(() => CloseScenePreview(view, false)),
            _map, _world, cityId => OpenMapCityDetails(view, cityId),
            _world is { RulesetId: PlayableSkeleton.RulesetId } && _map is not null
                ? hexId => RunUiAction(() => UseSceneTarget(view, hexId)) : null,
            _playerForceId);
        RefreshSceneAudioContext();
    }

    private void UseSceneTarget(MapSceneView view, int hexId)
    {
        if (_sceneView != view || _map is null || _world is not
            { RulesetId: PlayableSkeleton.RulesetId } ||
            (uint)hexId >= (uint)_map.Cells.Length)
            return;
        SelectMapTarget(hexId);
        CloseScenePreview(view, true);
    }

    private void SelectMapTarget(int hexId)
    {
        if (_map is null || (uint)hexId >= (uint)_map.Cells.Length) return;
        _selectedCell = _map.Cells[hexId];
        SetMeta("play_target_hex_id", hexId);
        RefreshTileInfo();
        RefreshPlayTarget();
        RefreshPlayControls();
        SelectPlayableUnitAtHex(hexId);
    }

    private void SelectMapCommandCity(int cityId)
    {
        if (_world is not GameState world || _playerForceId is not int player ||
            (world.Cities.GetValueOrDefault(cityId)?.ForceId ?? world.Gates.GetValueOrDefault(cityId)?.ForceId) != player)
            return;
        int index = _playCity.GetItemIndex(cityId);
        if (index < 0) return;
        _playCity.Select(index);
        RefreshPlayControls();
        _playCommandDeck.Select(world.Gates.ContainsKey(cityId) ? PlayableCommandDeck.Page.Military : PlayableCommandDeck.Page.Administration);
        SetMapFocus(false);
    }

    private void SelectMapCommandArea(int areaId)
    {
        if (_world is not GameState world || _playerForceId is not int player ||
            !world.Areas.TryGetValue(areaId, out AreaState? area) || area.ForceId != player ||
            !world.Cities.TryGetValue(area.CityId, out CityState? city) || city.ForceId != player ||
            !city.AreaIds.Contains(areaId))
            return;
        int cityIndex = _playCity.GetItemIndex(city.Id);
        if (cityIndex < 0) return;
        _playCity.Select(cityIndex);
        RefreshPlayCityChoices();
        int areaIndex = _playArea.GetItemIndex(areaId);
        if (areaIndex < 0) return;
        _playArea.Select(areaIndex);
        SetMeta("play_selected_area_id", areaId);
        RefreshPlayControls();
        _playCommandDeck.Select(PlayableCommandDeck.Page.Administration);
        SetMapFocus(false);
    }

    private void SelectMapCommandUnit(int unitId)
    {
        if (_world is not GameState world || _playerForceId is not int player ||
            !world.Units.TryGetValue(unitId, out UnitState? unit) || unit.ForceId != player)
            return;
        int index = _playUnit.GetItemIndex(unitId);
        if (index < 0) return;
        _playUnit.Select(index);
        _playCommandDeck.Select(PlayableCommandDeck.Page.Military);
        SetMeta("play_selected_unit_id", unitId);
        RefreshPlayDisbandAvailability();
        RefreshPlayOccupationAvailability();
        RefreshPlayWithdrawalAvailability();
        RefreshPlayBuildAvailability();
        RefreshExtendedCommands();
        SetMapFocus(false);
    }

    private void CloseResidentMap()
    {
        if (_forceHud is not null)
        {
            if (_forceHud.GetParent() is Node parent) parent.RemoveChild(_forceHud);
            _forceHud.QueueFree();
            _forceHud = null;
        }
        if (_residentMap is not null)
        {
            if (_residentMap.GetParent() is Node parent) parent.RemoveChild(_residentMap);
            _residentMap.QueueFree();
            _residentMap = null;
        }
        if (_mapTexture is not null) _mapTexture.Show();
        if (_openScenePreview is not null) _openScenePreview.Show();
        if (_previewRow is not null) _previewRow.Show();
        if (_mapFocusToggle is not null) SetMapFocus(false);
    }

    private void SetMapFocus(bool focused)
    {
        _mapFocused = focused && _residentMap is not null;
        _sidebar.Visible = !_mapFocused;
        _commandScroll.Visible = !_mapFocused;
        _title.Visible = !_mapFocused;
        _hint.Visible = !_mapFocused;
        _mapFocusToggle.Visible = _residentMap is not null;
        _mapFocusToggle.Text = _ui.Get(_mapFocused ? "play_map_show_commands" : "play_map_hide_commands");
        if (_forceHud is not null) _forceHud.Visible = _mapFocused;
    }

    // Explicit developer opt-in until the complete World output has passed
    // visual comparison. Default resident maps retain their playable preview.
    private static bool WorldMapDiagnosticEnabled =>
        System.Environment.GetEnvironmentVariable("SAN14_NATIVE_WORLD_MAP") == "1";

    private void UpdateResidentMap()
    {
        if (_repository is null || _map is null || _world is not GameState world ||
            world.RulesetId != PlayableSkeleton.RulesetId || _playerForceId is not int player)
        {
            CloseResidentMap();
            return;
        }
        if (_residentMap is not null)
        {
            _residentMap.RefreshPlayableWorld(world);
            if (WorldMapDiagnosticEnabled && !_residentMap.HasNativeWorldMapFactory && world.Units.Count == 0 &&
                world.Structures?.Count == 0 && world.NativeMapSeason is not null)
                _residentMap.SetNativeWorldMapDependencyFactory(
                    SceneViewNativeMapWorldSourceFactory.Create());
            _forceHud?.Refresh(world);
            return;
        }
        var view = new MapSceneView();
        _mapHost.AddChild(view);
        view.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // The accepted World graph currently owns the original static map
        // and an empty Army publication. Supply it before Configure captures
        // its first frame. Later unsupported publications retain the last
        // accepted image through SceneView's existing transaction boundary.
        if (WorldMapDiagnosticEnabled && world.Units.Count == 0 &&
            world.Structures?.Count == 0 && world.NativeMapSeason is not null)
            view.SetNativeWorldMapDependencyFactory(
                SceneViewNativeMapWorldSourceFactory.Create());
        var safeText = new PreviewText(_locale.Selected == 1, null);
        var campaignQueries = _campaignComposition!.PrepareArmyQueries(world);
        view.SetD3D11CurrentWorldQueries(campaignQueries.CaptureCurrentDefinitions,
            campaignQueries.CaptureGraphicsMotionInputs, campaignQueries.CaptureMapArmyGroups);
        view.Configure(_repository, _ui, safeText, () => { }, _map, world,
            openCityDetails: cityId => RunUiAction(() => OpenMapCityDetails(view, cityId)),
            acceptHex: hexId => RunUiAction(() => SelectMapTarget(hexId)),
            playerForceId: player, embedded: true, sourceSnapshot: _selectedSnapshot,
            selectCommandCity: cityId => RunUiAction(() => SelectMapCommandCity(cityId)),
            selectCommandUnit: unitId => RunUiAction(() => SelectMapCommandUnit(unitId)),
            selectCommandArea: areaId => RunUiAction(() => SelectMapCommandArea(areaId)));
        if (!view.GetMeta("scene_loaded", false).AsBool() ||
            !view.GetMeta("map_projection_available", false).AsBool())
        {
            string reason = view.GetMeta("scene_error", "Source map projection unavailable.").AsString();
            view.QueueFree();
            _mapHint.Text = reason;
            return;
        }
        _residentMap = view;
        var hud = new PlayableForceHud();
        _mapHost.AddChild(hud);
        try
        {
            hud.Configure(_repository, _ui, world, player, _selectedSnapshot,
                () => RunPlayAction(AdvancePlayable),
                () => RunUiAction(() => OpenForceDetailsFor(player, null)));
            hud.FitToMap(_mapHost.Size);
            _forceHud = hud;
        }
        catch (Exception error)
        {
            _mapHost.RemoveChild(hud);
            hud.Free();
            _mapHint.Text = $"Force HUD source: {error.Message}";
            SetMeta("playable_force_hud_error", error.Message);
            GD.PushError(error.ToString());
        }
        _mapTexture.Hide();
        _openScenePreview.Hide();
        _previewRow.Hide();
        _mapHost.MoveChild(_mapFocusToggle, _mapHost.GetChildCount() - 1);
        SetMapFocus(true);
    }

    private void SelectPlayableUnitAtHex(int hexId)
    {
        if (_world is not GameState world || _playerForceId is not int player) return;
        UnitState? unit = world.Units.Values.Where(value => value.ForceId == player &&
                value.HexId == hexId).OrderBy(value => value.Id).FirstOrDefault();
        if (unit is null) return;
        int index = _playUnit.GetItemIndex(unit.Id);
        if (index < 0) return;
        _playUnit.Select(index);
        SetMeta("play_selected_unit_id", unit.Id);
        RefreshPlayDisbandAvailability();
        RefreshPlayOccupationAvailability();
        RefreshPlayWithdrawalAvailability();
        RefreshPlayBuildAvailability();
        RefreshExtendedCommands();
    }

    private void CloseScenePreview(MapSceneView view, bool focusTarget)
    {
        if (_sceneView != view) return;
        view.QueueFree();
        _sceneView = null;
        RefreshSceneAudioContext();
        _browserContent.Show();
        if (focusTarget && !_playDeploy.Disabled) _playDeploy.GrabFocus();
        else _openScenePreview.GrabFocus();
    }

    private void OpenMapCityDetails(MapSceneView owner, int cityId)
    {
        if (_failed || _detailsView is not null ||
            (_sceneView != owner && _residentMap != owner) || _repository is null ||
            _world is null || _selectedSnapshot is null) return;
        var view = new CityDetailsView();
        _detailsView = view;
        AddChild(view);
        try
        {
            if (!_world.Cities.ContainsKey(cityId)) throw new InvalidDataException($"Selected city {cityId} is absent from the current world.");
            owner.SetDetailsOpen(true);
            view.Configure(_repository, _world, _selectedSnapshot, cityId, _ui, () =>
            {
                if (_detailsView != view) return;
                view.Hide(); view.QueueFree(); _detailsView = null;
                if ((_sceneView == owner || _residentMap == owner) && GodotObject.IsInstanceValid(owner))
                    owner.SetDetailsOpen(false);
            }, _ownedArmyCampaign?.CaptureCurrentDefinitions(_world));
        }
        catch (Exception error)
        {
            _detailsView = null;
            RemoveChild(view); view.Free();
            owner.SetDetailsOpen(false);
            owner.ReportCityDetailsError(error);
        }
    }

    private void OpenForceDetails()
    {
        if (_selectedForceSlot is int forceId)
            OpenForceDetailsFor(forceId, _openForceDetails);
    }

    private void OpenForceDetailsFor(int forceId, Button? returnFocus)
    {
        if (_detailsView is not null || _sceneView is not null || _repository is null || _world is null ||
            _selectedSnapshot is null || !_world.Forces.ContainsKey(forceId)) return;
        var view = new ForceDetailsView();
        _detailsView = view;
        AddChild(view);
        try
        {
            view.Configure(_repository, _world, _selectedSnapshot, forceId, _ui, () => RunUiAction(() =>
            {
                view.Hide();
                view.QueueFree();
                _detailsView = null;
                if (returnFocus?.IsVisibleInTree() == true) returnFocus.GrabFocus();
            }));
        }
        catch
        {
            _detailsView = null;
            RemoveChild(view);
            view.Free();
            throw;
        }
    }

    private void OpenCityDetails()
    {
        if (_detailsView is not null || _sceneView is not null || _repository is null || _world is null ||
            _selectedSnapshot is null || _city.Selected < 0) return;
        int cityId = _city.GetSelectedId();
        var view = new CityDetailsView();
        _detailsView = view;
        AddChild(view);
        view.Configure(_repository, _world, _selectedSnapshot, cityId, _ui, () => RunUiAction(() =>
        {
            view.QueueFree();
            _detailsView = null;
            _openCityDetails.GrabFocus();
        }), _ownedArmyCampaign?.CaptureCurrentDefinitions(_world));
    }

    private void BuildPlayControls(VBoxContainer parent)
    {
        var panel = new PanelContainer { Name = "PlayableSession" };
        parent.AddChild(panel);
        var content = new VBoxContainer();
        panel.AddChild(content);
        var actions = new HFlowContainer();
        content.AddChild(actions);
        _playTitle = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        actions.AddChild(_playTitle);
        _playStart = new Button { Name = "StartPlayable" };
        _playStart.Pressed += () => RunPlayAction(StartPlayable);
        actions.AddChild(_playStart);
        _playSaveSlotLabel = new Label();
        actions.AddChild(_playSaveSlotLabel);
        _playSaveSlot = new OptionButton { Name = "PlayableSaveSlot", CustomMinimumSize = new Vector2(135, 0) };
        for (int slot = 1; slot <= PlayableSaveSlotCount; slot++)
            _playSaveSlot.AddItem(slot.ToString(), slot);
        _playSaveSlot.Select(0);
        _playSaveSlot.ItemSelected += _ => RunUiAction(RefreshSaveSlots);
        actions.AddChild(_playSaveSlot);
        _playSave = new Button { Name = "SavePlayable" };
        _playSave.Pressed += () => RunPlayAction(SavePlayable);
        actions.AddChild(_playSave);
        _playContinue = new Button { Name = "ContinuePlayable" };
        _playContinue.Pressed += () => RunPlayAction(LoadPlayable);
        actions.AddChild(_playContinue);

        _playCommandDeck = new PlayableCommandDeck();
        content.AddChild(_playCommandDeck);
        VBoxContainer administration = _playCommandDeck.Content(PlayableCommandDeck.Page.Administration);
        VBoxContainer military = _playCommandDeck.Content(PlayableCommandDeck.Page.Military);
        VBoxContainer personnel = _playCommandDeck.Content(PlayableCommandDeck.Page.Personnel);
        VBoxContainer governance = _playCommandDeck.Content(PlayableCommandDeck.Page.Governance);
        var governanceRow = new HFlowContainer();
        governance.AddChild(governanceRow);
        _playGovernanceLabel = new Label();
        governanceRow.AddChild(_playGovernanceLabel);
        _playGovernanceSlot = new OptionButton { Name = "PlayablePolicySlot",
            CustomMinimumSize = new Vector2(125, 0) };
        _playGovernanceSlot.ItemSelected += _ => RunUiAction(RefreshPlayGovernanceChoices);
        governanceRow.AddChild(_playGovernanceSlot);
        _playGovernanceOfficer = new OptionButton { Name = "PlayablePolicyOfficer",
            CustomMinimumSize = new Vector2(235, 0) };
        governanceRow.AddChild(_playGovernanceOfficer);
        _playGovernanceApply = new Button { Name = "AssignPlayablePolicy" };
        _playGovernanceApply.Pressed += () => RunPlayAction(AssignPlayablePolicy);
        governanceRow.AddChild(_playGovernanceApply);
        _playGovernanceStatus = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        governanceRow.AddChild(_playGovernanceStatus);

        var selections = new HFlowContainer();
        content.AddChild(selections);
        content.MoveChild(selections, 1);
        _playCityLabel = new Label();
        selections.AddChild(_playCityLabel);
        _playCity = new OptionButton { Name = "PlayableCity", CustomMinimumSize = new Vector2(110, 0) };
        _playCity.ItemSelected += _ => RunPlayAction(RefreshPlayControls);
        selections.AddChild(_playCity);
        var areaSelections = new HFlowContainer();
        administration.AddChild(areaSelections);
        _playAreaLabel = new Label();
        areaSelections.AddChild(_playAreaLabel);
        _playArea = new OptionButton { Name = "PlayableArea", CustomMinimumSize = new Vector2(110, 0) };
        areaSelections.AddChild(_playArea);
        _playAdminOfficerLabel = new Label();
        areaSelections.AddChild(_playAdminOfficerLabel);
        _playAdminOfficer = new OptionButton { Name = "PlayableAdminOfficer", CustomMinimumSize = new Vector2(110, 0) };
        areaSelections.AddChild(_playAdminOfficer);
        var deploymentOfficer = new HFlowContainer();
        military.AddChild(deploymentOfficer);
        _playOfficerLabel = new Label();
        deploymentOfficer.AddChild(_playOfficerLabel);
        _playOfficer = new OptionButton { Name = "PlayableOfficer", CustomMinimumSize = new Vector2(110, 0) };
        _playOfficer.ItemSelected += _ => RunPlayAction(RefreshPlayFormationChoices);
        deploymentOfficer.AddChild(_playOfficer);
        var deployed = new HFlowContainer();
        military.AddChild(deployed);
        _playUnitLabel = new Label();
        deployed.AddChild(_playUnitLabel);
        _playUnit = new OptionButton { Name = "PlayableUnit", CustomMinimumSize = new Vector2(170, 0) };
        _playUnit.ItemSelected += _ =>
        {
            RefreshPlayDisbandAvailability();
            RefreshPlayOccupationAvailability();
            RefreshPlayWithdrawalAvailability();
            RefreshPlayBuildAvailability();
            RefreshExtendedCommands();
        };
        deployed.AddChild(_playUnit);
        _playRetarget = new Button { Name = "RetargetUnit" };
        _playRetarget.Pressed += () => RunPlayAction(RetargetUnit);
        deployed.AddChild(_playRetarget);
        var structureActions = new HFlowContainer();
        military.AddChild(structureActions);
        _playStructureKindLabel = new Label();
        structureActions.AddChild(_playStructureKindLabel);
        _playStructureKind = new OptionButton
        {
            Name = "PlayableStructureKind", CustomMinimumSize = new Vector2(90, 0)
        };
        _playStructureKind.ItemSelected += _ => RunPlayAction(() =>
        {
            RefreshPlayBuildAvailability();
            RefreshPlayDeploymentAvailability();
        });
        structureActions.AddChild(_playStructureKind);
        _playBuild = new Button { Name = "BuildStructure" };
        _playBuild.Pressed += () => RunPlayAction(BuildStructure);
        structureActions.AddChild(_playBuild);
        _playRepair = new Button { Name = "RepairStructure" };
        _playRepair.Pressed += () => RunPlayAction(RepairStructure);
        structureActions.AddChild(_playRepair);
        _playDemolish = new Button { Name = "DemolishStructure" };
        _playDemolish.Pressed += () => RunPlayAction(DemolishStructure);
        structureActions.AddChild(_playDemolish);
        _playOccupy = new Button { Name = "OccupyArea" };
        _playOccupy.Pressed += () => RunPlayAction(OccupySelectedArea);
        deployed.AddChild(_playOccupy);
        _playWithdraw = new Button { Name = "WithdrawUnit" };
        _playWithdraw.Pressed += () => RunPlayAction(WithdrawUnit);
        deployed.AddChild(_playWithdraw);
        _playDisband = new Button { Name = "DisbandUnit" };
        _playDisband.Pressed += () => RunPlayAction(DisbandUnit);
        deployed.AddChild(_playDisband);
        var prepared = new HFlowContainer();
        military.AddChild(prepared);
        _playPreparedLabel = new Label();
        prepared.AddChild(_playPreparedLabel);
        _playPrepared = new OptionButton { Name = "PreparedDeployment",
            CustomMinimumSize = new Vector2(180, 0) };
        prepared.AddChild(_playPrepared);
        _playCancelDeployment = new Button { Name = "CancelPreparedDeployment" };
        _playCancelDeployment.Pressed += () => RunPlayAction(CancelPreparedDeployment);
        prepared.AddChild(_playCancelDeployment);
        var settings = new HFlowContainer();
        administration.AddChild(settings);
        _playFocusLabel = new Label();
        settings.AddChild(_playFocusLabel);
        _playFocus = new OptionButton { Name = "PlayableFocus" };
        _playFocus.AddItem("", (int)AreaDevelopmentFocus.Commerce);
        _playFocus.AddItem("", (int)AreaDevelopmentFocus.Agriculture);
        _playFocus.AddItem("", (int)AreaDevelopmentFocus.Barracks);
        _playFocus.Select(0);
        settings.AddChild(_playFocus);
        var deploymentSettings = new HFlowContainer();
        military.AddChild(deploymentSettings);
        _playTroopsLabel = new Label();
        deploymentSettings.AddChild(_playTroopsLabel);
        _playTroops = new SpinBox { Name = "PlayableTroops", MinValue = 1, MaxValue = 1000000,
            Step = 1, Value = 1000, CustomMinimumSize = new Vector2(100, 0) };
        deploymentSettings.AddChild(_playTroops);
        _playFormationLabel = new Label();
        deploymentSettings.AddChild(_playFormationLabel);
        _playFormation = new OptionButton { Name = "PlayableFormation",
            CustomMinimumSize = new Vector2(110, 0) };
        _playFormation.ItemSelected += _ => RunPlayAction(RefreshPlayTacticChoices);
        deploymentSettings.AddChild(_playFormation);
        var tactics = new HFlowContainer();
        military.AddChild(tactics);
        for (int slot = 0; slot < _playTactics.Length; slot++)
        {
            var label = new Label { Name = $"PlayableTacticLabel_{slot + 1}" };
            label.Text = _ui.Format("play_tactic_slot", slot + 1);
            tactics.AddChild(label);
            var choice = new OptionButton
            {
                Name = $"PlayableTactic_{slot + 1}", CustomMinimumSize = new Vector2(140, 0)
            };
            _playTactics[slot] = choice;
            tactics.AddChild(choice);
        }

        var transport = new HFlowContainer();
        military.AddChild(transport);
        _playTransportCityLabel = new Label();
        transport.AddChild(_playTransportCityLabel);
        _playTransportCity = new OptionButton { Name = "TransportCity",
            CustomMinimumSize = new Vector2(110, 0) };
        transport.AddChild(_playTransportCity);
        _playTransportSuppliesLabel = new Label();
        transport.AddChild(_playTransportSuppliesLabel);
        _playTransportSupplies = new SpinBox { Name = "TransportSupplies", MinValue = 0,
            MaxValue = 1000000, Step = 100, Value = 5000, CustomMinimumSize = new Vector2(110, 0) };
        transport.AddChild(_playTransportSupplies);
        _playTransportGoldLabel = new Label();
        transport.AddChild(_playTransportGoldLabel);
        _playTransportGold = new SpinBox { Name = "TransportGold", MinValue = 0,
            MaxValue = 1000000, Step = 100, Value = 1000, CustomMinimumSize = new Vector2(100, 0) };
        transport.AddChild(_playTransportGold);
        _playTransportArrivalLabel = new Label();
        transport.AddChild(_playTransportArrivalLabel);
        _playTransportArrival = new OptionButton { Name = "TransportArrival" };
        _playTransportArrival.AddItem("", (int)TransportArrivalDisposition.EnterCity);
        _playTransportArrival.AddItem("", (int)TransportArrivalDisposition.ReturnToSource);
        //Actual fresh World1690 selects the commander's Return option.
        _playTransportArrival.Select(1);
        transport.AddChild(_playTransportArrival);
        _playTransport = new Button { Name = "DispatchTransport" };
        _playTransport.Pressed += () => RunPlayAction(DispatchTransport);
        transport.AddChild(_playTransport);

        var moves = new HFlowContainer();
        personnel.AddChild(moves);
        _playMoveOfficerLabel = new Label();
        moves.AddChild(_playMoveOfficerLabel);
        _playMoveOfficer = new OptionButton { Name = "MoveOfficer",
            CustomMinimumSize = new Vector2(105, 0) };
        moves.AddChild(_playMoveOfficer);
        _playMoveCityLabel = new Label();
        moves.AddChild(_playMoveCityLabel);
        _playMoveCity = new OptionButton { Name = "MoveDestination",
            CustomMinimumSize = new Vector2(105, 0) };
        moves.AddChild(_playMoveCity);
        _playMove = new Button { Name = "DispatchOfficerMove" };
        _playMove.Pressed += () => RunPlayAction(DispatchOfficerMove);
        moves.AddChild(_playMove);
        _playSummonOfficerLabel = new Label();
        moves.AddChild(_playSummonOfficerLabel);
        _playSummonOfficer = new OptionButton { Name = "SummonOfficer",
            CustomMinimumSize = new Vector2(130, 0) };
        moves.AddChild(_playSummonOfficer);
        _playSummon = new Button { Name = "SummonOfficerMove" };
        _playSummon.Pressed += () => RunPlayAction(SummonOfficer);
        moves.AddChild(_playSummon);
        _playMoveStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        moves.AddChild(_playMoveStatus);

        var talent = new HFlowContainer();
        personnel.AddChild(talent);
        _playTalentExecutorLabel = new Label();
        talent.AddChild(_playTalentExecutorLabel);
        _playTalentExecutor = new OptionButton { Name = "TalentExecutor",
            CustomMinimumSize = new Vector2(125, 0) };
        talent.AddChild(_playTalentExecutor);
        _playTalentSearch = new Button { Name = "SearchTalent" };
        _playTalentSearch.Pressed += () => RunPlayAction(PrepareTalentSearch);
        talent.AddChild(_playTalentSearch);
        _playTalentTargetLabel = new Label();
        talent.AddChild(_playTalentTargetLabel);
        _playTalentTarget = new OptionButton { Name = "TalentTarget",
            CustomMinimumSize = new Vector2(125, 0) };
        talent.AddChild(_playTalentTarget);
        _playTalentRecruit = new Button { Name = "RecruitTalent" };
        _playTalentRecruit.Pressed += () => RunPlayAction(PrepareTalentRecruit);
        talent.AddChild(_playTalentRecruit);
        _playTalentStatus = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        talent.AddChild(_playTalentStatus);

        var cityDuties = new HFlowContainer();
        administration.AddChild(cityDuties);
        _playDutyLabel = new Label();
        cityDuties.AddChild(_playDutyLabel);
        _playDutyOfficer = new OptionButton { Name = "PlayableDutyOfficer",
            CustomMinimumSize = new Vector2(120, 0) };
        cityDuties.AddChild(_playDutyOfficer);
        _playRecruiter = new Button { Name = "AssignRecruiter" };
        _playRecruiter.Pressed += () => RunPlayAction(() => AssignCityDuty(CityDutyKind.Recruitment));
        cityDuties.AddChild(_playRecruiter);
        _playTrainer = new Button { Name = "AssignTrainer" };
        _playTrainer.Pressed += () => RunPlayAction(() => AssignCityDuty(CityDutyKind.Training));
        cityDuties.AddChild(_playTrainer);
        _playDutyStatus = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        cityDuties.AddChild(_playDutyStatus);

        var administrationActions = new HFlowContainer();
        administration.AddChild(administrationActions);
        var militaryActions = new HFlowContainer();
        military.AddChild(militaryActions);
        var commands = new HFlowContainer();
        content.AddChild(commands);
        _playAdmin = new Button { Name = "OrderAdministration" };
        _playAdmin.Pressed += () => RunPlayAction(OrderAdministration);
        administrationActions.AddChild(_playAdmin);
        _playDeploy = new Button { Name = "OrderDeployment" };
        _playDeploy.Pressed += () => RunPlayAction(OrderDeployment);
        militaryActions.AddChild(_playDeploy);
        _playDeployOccupy = new Button { Name = "OrderOccupationDeployment" };
        _playDeployOccupy.Pressed += () => RunPlayAction(OrderOccupationDeployment);
        militaryActions.AddChild(_playDeployOccupy);
        _playDeployBuild = new Button { Name = "OrderConstructionDeployment" };
        _playDeployBuild.Pressed += () => RunPlayAction(OrderConstructionDeployment);
        militaryActions.AddChild(_playDeployBuild);
        _playTarget = new Label { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        commands.AddChild(_playTarget);
        _playAdvance = new Button { Name = "AdvanceTenDays" };
        _playAdvance.Pressed += () => RunPlayAction(AdvancePlayable);
        commands.AddChild(_playAdvance);
        _playFinishReport = new Button { Name = "FinishPlayableReport" };
        _playFinishReport.Pressed += () => RunPlayAction(FinishPlayableReport);
        commands.AddChild(_playFinishReport);
        _playStatus = new Label();
        content.AddChild(_playStatus);
        _playMessage = new Label { Name = "PlayableCommandMessage", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(_playMessage);
        _playReport = new RichTextLabel { Name = "PlayablePeriodReport", CustomMinimumSize = new Vector2(0, 58),
            FitContent = false, ScrollActive = true, BbcodeEnabled = false };
        content.AddChild(_playReport);
        BuildExtendedCommandPanels();
        BuildMilitaryDeploymentComposition(military);
        BuildTransportOrders(military);
    }

    private void RunPlayAction(Action action)
    {
        if (_failed) return;
        try { action(); }
        catch (Exception error)
        {
            _playMessage.Text = error is PlayableEventRuleUnavailableException unavailable
                ? _ui.Format("play_event_rule_unavailable", unavailable.MissingRuleKey)
                : _ui.Format("play_error", error.Message);
            GD.PushWarning(error.ToString());
        }
    }

    private void UpdatePlayLocale()
    {
        _playCommandDeck.UpdateLocale(_ui);
        ConfigureExtendedCommandLocale();
        ConfigureMilitaryDeploymentComposition();
        ConfigureTransportOrders();
        _playTitle.Text = _ui.Get("play_title");
        _playStart.Text = _ui.Get("play_start");
        _playSave.Text = _ui.Get("play_save");
        _playContinue.Text = _ui.Get("play_continue");
        _playSaveSlotLabel.Text = _ui.Get("play_save_slot");
        RefreshSaveSlots();
        _playGovernanceLabel.Text = _ui.Get("play_governance");
        _playGovernanceApply.Text = _ui.Get("play_governance_apply");
        _playCityLabel.Text = _ui.Get("play_source_base");
        _playAreaLabel.Text = _ui.Get("play_area");
        _playAdminOfficerLabel.Text = _ui.Get("play_admin_officer");
        _playOfficerLabel.Text = _ui.Get("play_officer");
        _playFormationLabel.Text = _ui.Get("play_formation");
        _playDutyLabel.Text = _ui.Get("play_duty_officer");
        _playUnitLabel.Text = _ui.Get("play_unit");
        _playPreparedLabel.Text = _ui.Get("play_prepared_deployment");
        _playCancelDeployment.Text = _ui.Get("play_cancel_deployment");
        _playFocusLabel.Text = _ui.Get("play_focus");
        _playTroopsLabel.Text = _ui.Get("play_troops");
        _playTransportCityLabel.Text = _ui.Get("play_transport_city");
        _playTransportSuppliesLabel.Text = _ui.Get("play_transport_supplies");
        _playTransportGoldLabel.Text = _ui.Get("play_transport_gold");
        _playTransportArrivalLabel.Text = _ui.Get("play_transport_arrival");
        _playTransportArrival.SetItemText(0, _ui.Get("play_transport_enter_city"));
        _playTransportArrival.SetItemText(1, _ui.Get("play_transport_return_city"));
        _playTransport.Text = _ui.Get("play_transport_dispatch");
        _playTransport.TooltipText = _ui.Get("play_transport_hint");
        _playMoveOfficerLabel.Text = _ui.Get("play_move_officer");
        _playMoveCityLabel.Text = _ui.Get("play_move_destination");
        _playSummonOfficerLabel.Text = _ui.Get("play_summon_officer");
        _playMove.Text = _ui.Get("play_move_dispatch");
        _playSummon.Text = _ui.Get("play_move_summon");
        _playTalentExecutorLabel.Text = _ui.Get("play_talent_executor");
        _playTalentTargetLabel.Text = _ui.Get("play_talent_target");
        _playTalentSearch.Text = _ui.Get("play_talent_search");
        _playTalentRecruit.Text = _ui.Get("play_talent_recruit");
        _playFocus.SetItemText(0, _ui.Get("play_focus_commerce"));
        _playFocus.SetItemText(1, _ui.Get("play_focus_agriculture"));
        _playFocus.SetItemText(2, _ui.Get("play_focus_barracks"));
        _playAdmin.Text = _ui.Get("play_admin");
        _playDeploy.Text = _ui.Get("play_deploy");
        _playDeployOccupy.Text = _ui.Get("play_deploy_occupy");
        _playDeployBuild.Text = _ui.Get("play_deploy_build");
        _playRetarget.Text = _ui.Get("play_retarget");
        _playStructureKindLabel.Text = _ui.Get("play_structure_kind");
        _playBuild.Text = _ui.Get("play_build_formation");
        _playRepair.Text = _ui.Get("play_repair_structure");
        _playDemolish.Text = _ui.Get("play_demolish_structure");
        _playOccupy.Text = _ui.Get("play_occupy_area");
        _playWithdraw.Text = _ui.Get("play_withdraw");
        _playDisband.Text = _ui.Get("play_disband");
        _playRecruiter.Text = _ui.Get("play_assign_recruiter");
        _playTrainer.Text = _ui.Get("play_assign_trainer");
        _playAdvance.Text = _ui.Get("play_advance");
        _playFinishReport.Text = _ui.Get("play_finish_report");
        RefreshPlayTarget();
        RefreshPlayControls();
    }

    private void RefreshPlayTarget() => _playTarget.Text = _selectedCell is MapCell cell
        ? _ui.Format("play_target", cell.Id) : _ui.Get("play_target_none");

    private void RefreshPlayStructureKinds()
    {
        if (_repository is null || _selectedSnapshot is null || _world is null)
        {
            _playStructureKind.Clear();
            _playStructureCatalogRevision = null;
            return;
        }
        string fixedUri = GameResourceId.File(_selectedSnapshot.FixedDataPath).ToString();
        string revision = fixedUri + "|" +
            _world.ContentHashes.GetValueOrDefault(fixedUri, "") + "|" + _ui.SourceLocale;
        if (revision == _playStructureCatalogRevision) return;
        LoadedObjectDefinitions loaded = _repository.LoadObjectDefinitions(
            _selectedSnapshot.FixedDataPath);
        if (!_world.ContentHashes.TryGetValue(loaded.Source.ResourceUri,
                out string? expectedHash) ||
            !expectedHash.Equals(loaded.Source.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Construction catalog differs from the current scenario source.");
        int prior = _playStructureKind.Selected >= 0
            ? _playStructureKind.GetSelectedId() : PlayableConstructionRules.SupportedKinds[0].ObjectSlot;
        _playStructureKind.Clear();
        foreach (PlayableConstructionRules.KindDefinition kind in PlayableConstructionRules.SupportedKinds)
        {
            S14ObjectDefinition definition = loaded.Catalog.Definitions.Single(row =>
                row.Slot == kind.ObjectSlot && !string.IsNullOrWhiteSpace(row.Name));
            _playStructureKind.AddItem(definition.Name, kind.ObjectSlot);
        }
        int index = _playStructureKind.GetItemIndex(prior);
        _playStructureKind.Select(index < 0 ? 0 : index);
        _playStructureCatalogRevision = revision;
        SetMeta("structure_catalog_source_uri", loaded.Source.ResourceUri);
        SetMeta("structure_catalog_source_sha256", loaded.Source.Sha256);
    }

    private void RefreshPlayControls()
    {
        bool editableOptions = _world is { Phase: TurnPhase.Initializing };
        _difficulty.Disabled = !editableOptions;
        _initialCredit.Disabled = !editableOptions;
        _capturedOfficers.Disabled = !editableOptions;
        bool playing = _world is { RulesetId: PlayableSkeleton.RulesetId } &&
            _playerForceId is int player && _world.Forces.ContainsKey(player);
        PlayableResult outcome = playing ? PlayableOutcome.Evaluate(_world!, _playerForceId!.Value) :
            PlayableResult.Ongoing;
        bool strategy = playing && _world!.Phase == TurnPhase.Strategy && outcome == PlayableResult.Ongoing;
        bool reporting = playing && _world!.Phase == TurnPhase.Reporting;
        _saveInitial.Disabled = _world is null || _world.Phase != TurnPhase.Initializing;
        _playStart.Disabled = _world is null || _world.Phase != TurnPhase.Initializing || _selectedForceSlot is null;
        _playSave.Disabled = !playing;
        RefreshSaveSlots();
        RefreshPlayStructureKinds();
        _playStructureKind.Disabled = !strategy;
        RefreshPlayGovernanceChoices();
        _playAdmin.Disabled = !strategy;
        RefreshPlayDeploymentAvailability();
        _playRecruiter.Disabled = !strategy || _playCity.ItemCount == 0;
        _playTrainer.Disabled = !strategy || _playCity.ItemCount == 0;
        _playRetarget.Disabled = !strategy || _selectedCell is null || _playUnit.ItemCount == 0;
        RefreshPlayBuildAvailability();
        RefreshPlayOccupationAvailability();
        RefreshPlayWithdrawalAvailability();
        _playDisband.Disabled = !strategy;
        _playAdvance.Disabled = !strategy;
        _playFinishReport.Disabled = !reporting;
        _playCity.Disabled = !strategy;
        _playArea.Disabled = !strategy;
        _playAdminOfficer.Disabled = !strategy;
        _playOfficer.Disabled = !strategy;
        _playDutyOfficer.Disabled = !strategy;
        _playUnit.Disabled = !strategy;
        _playPrepared.Disabled = !strategy;
        _playCancelDeployment.Disabled = !strategy || _playPrepared.ItemCount == 0;
        _playFocus.Disabled = !strategy;
        _playTroops.Editable = strategy;
        _playTransportCity.Disabled = !strategy;
        _playTransportSupplies.Editable = strategy;
        _playTransportGold.Editable = strategy;
        _playTransportArrival.Disabled = !strategy;
        _playTransport.Disabled = !strategy || _playTransportCity.ItemCount == 0;
        _playMoveOfficer.Disabled = !strategy;
        _playMoveCity.Disabled = !strategy;
        _playSummonOfficer.Disabled = !strategy;
        _playMove.Disabled = !strategy || _playMoveOfficer.ItemCount == 0 ||
            _playMoveCity.ItemCount == 0;
        _playSummon.Disabled = !strategy || _playSummonOfficer.ItemCount == 0;
        _playTalentSearch.Disabled = !strategy || _playTalentExecutor.ItemCount == 0;
        _playTalentRecruit.Disabled = !strategy || _playTalentExecutor.ItemCount == 0 ||
            _playTalentTarget.ItemCount == 0;
        if (!playing)
        {
            _playCity.Clear();
            _playArea.Clear();
            _playAdminOfficer.Clear();
            _playOfficer.Clear();
            _playFormation.Clear();
            foreach (OptionButton tactic in _playTactics) tactic.Clear();
            _playDutyOfficer.Clear();
            _playDutyStatus.Text = "";
            _playUnit.Clear();
            _playPrepared.Clear();
            _playCancelDeployment.Disabled = true;
            _playTransportCity.Clear();
            _playTransport.Disabled = true;
            _playMoveOfficer.Clear();
            _playMoveCity.Clear();
            _playSummonOfficer.Clear();
            _playMoveStatus.Text = "";
            _playTalentExecutor.Clear();
            _playTalentTarget.Clear();
            _playTalentStatus.Text = "";
            _playTalentSearch.Disabled = true;
            _playTalentRecruit.Disabled = true;
            _playMove.Disabled = true;
            _playSummon.Disabled = true;
            _playStatus.Text = _ui.Get("play_not_started");
            _playReport.Text = "";
            ClosePlayablePeriodReport();
            RefreshExtendedCommands();
            return;
        }
        GameState world = _world!;
        int playerForce = _playerForceId!.Value;
        int oldCity = _playCity.Selected >= 0 ? _playCity.GetSelectedId() : -1;
        _playCity.Clear();
        CityState[] ownedCities = world.Cities.Values.Where(city => city.ForceId == playerForce)
            .OrderBy(city => city.Id).ToArray();
        foreach (CityState city in ownedCities) _playCity.AddItem(city.Name, city.Id);
        foreach (GateState gate in world.Gates.Values.Where(gate => gate.ForceId == playerForce).OrderBy(gate => gate.Id))
            _playCity.AddItem(gate.Name, gate.Id);
        int defaultCityId = world.Forces[playerForce].RulerId is int rulerId &&
            world.Officers.TryGetValue(rulerId, out OfficerState? ruler)
            ? ruler.LocationId ?? -1 : -1;
        int cityIndex = _playCity.GetItemIndex(oldCity);
        if (cityIndex < 0) cityIndex = _playCity.GetItemIndex(defaultCityId);
        if (_playCity.ItemCount > 0) _playCity.Select(cityIndex < 0 ? 0 : cityIndex);
        RefreshPlayCityChoices();
        _playCancelDeployment.Disabled = !strategy || _playPrepared.ItemCount == 0;
        bool selectedCity = _playCity.Selected >= 0 && world.Cities.ContainsKey(_playCity.GetSelectedId());
        _playAdmin.Disabled = !strategy || !selectedCity;
        _playTransport.Disabled = !strategy || !selectedCity || _playTransportCity.ItemCount == 0;
        _playMove.Disabled = !strategy || _playMoveOfficer.ItemCount == 0 ||
            _playMoveCity.ItemCount == 0;
        _playSummon.Disabled = !strategy || _playSummonOfficer.ItemCount == 0;
        _playMoveStatus.Text = _ui.Format("play_move_pending", world.PreparedOfficerMoves?.Count ?? 0);
        _playTalentStatus.Text = _ui.Format("play_talent_pending", world.PreparedTalentOrders?.Count ?? 0);
        _playTalentSearch.Disabled = !strategy || _playTalentExecutor.ItemCount == 0;
        _playTalentRecruit.Disabled = !strategy || _playTalentExecutor.ItemCount == 0 ||
            _playTalentTarget.ItemCount == 0;
        _playRecruiter.Disabled = !strategy || !selectedCity;
        _playTrainer.Disabled = !strategy || !selectedCity;
        string date = _ui.DateLine(world.Date.Year, world.Date.Month, world.Date.Day);
        string orders = world.Districts.Values.Where(district => district.ForceId == playerForce &&
            district.Number == 1).Sum(district => district.OrdersRemaining ?? 0).ToString();
        _playStatus.Text = _ui.Format(reporting ? "play_reporting" : "play_active",
            world.Forces[playerForce].Name, date, world.PeriodNumber) + "  " +
            _ui.Format("play_orders", orders);
        if (outcome == PlayableResult.Unified) _playStatus.Text += "  " + _ui.Get("play_unified");
        if (outcome == PlayableResult.Defeated) _playStatus.Text += "  " + _ui.Get("play_defeated");
        _playReport.Text = reporting ? RenderPlayReport(world, playerForce) : "";
        int oldUnit = _playUnit.Selected >= 0 ? _playUnit.GetSelectedId() : -1;
        _playUnit.Clear();
        foreach (UnitState unit in world.Units.Values.Where(unit => unit.ForceId == playerForce)
                     .OrderBy(unit => unit.Id))
            _playUnit.AddItem(_ui.Format("play_unit_item", world.Officers[unit.OfficerId].Name,
                unit.Troops), unit.Id);
        if (_playUnit.ItemCount > 0)
        {
            int unitIndex = _playUnit.GetItemIndex(oldUnit);
            _playUnit.Select(unitIndex < 0 ? 0 : unitIndex);
        }
        _playRetarget.Disabled = !strategy || _selectedCell is null || _playUnit.ItemCount == 0;
        RefreshPlayBuildAvailability();
        RefreshPlayOccupationAvailability();
        RefreshPlayWithdrawalAvailability();
        RefreshPlayDisbandAvailability();
        RefreshExtendedCommands();
        RefreshPlayablePeriodReport();
    }

    private void RefreshPlayGovernanceChoices()
    {
        int? priorSlot = SelectionId(_playGovernanceSlot);
        _playGovernanceSlot.Clear();
        _playGovernanceOfficer.Clear();
        GovernanceCommandChoices? choices = _world is GameState world && _playerForceId is int player
            ? GovernanceCommandChoices.Create(world, player, priorSlot) : null;
        if (choices is null)
        {
            _playGovernanceStatus.Text = _ui.Get("play_governance_unavailable");
            _playGovernanceSlot.Disabled = true;
            _playGovernanceOfficer.Disabled = true;
            _playGovernanceApply.Disabled = true;
            return;
        }
        foreach (GovernanceSlotChoice slot in choices.Slots)
        {
            string label = slot.Kind == GovernanceSlotKind.Advisor
                ? _ui.Get("play_governance_advisor") : _ui.Format("play_governance_group", slot.Id);
            _playGovernanceSlot.AddItem(label, slot.Id);
        }
        if (choices.SelectedSlotId is int selectedSlot)
        {
            _playGovernanceSlot.Select(_playGovernanceSlot.GetItemIndex(selectedSlot));
            _playGovernanceOfficer.AddItem(_ui.Get("play_governance_vacant"), NoGovernanceOfficerId);
            foreach (GovernanceOfficerChoice officer in choices.Officers)
                _playGovernanceOfficer.AddItem($"{officer.Name} · {officer.PolicyName ?? _ui.Get("detail_unknown")}", officer.Id);
            int officerIndex = _playGovernanceOfficer.GetItemIndex(choices.SelectedOfficerId ?? NoGovernanceOfficerId);
            _playGovernanceOfficer.Select(officerIndex < 0 ? 0 : officerIndex);
        }
        _playGovernanceStatus.Text = _ui.Format("play_governance_status",
            choices.ConfiguredCount, choices.ActiveCount, choices.HasPendingChange
                ? _ui.Get("play_governance_pending") : _ui.Get("play_governance_no_pending"));
        _playGovernanceSlot.Disabled = !choices.CanConfigure;
        _playGovernanceOfficer.Disabled = !choices.CanConfigure || _playGovernanceOfficer.ItemCount == 0;
        _playGovernanceApply.Disabled = !choices.CanConfigure || _playGovernanceOfficer.ItemCount == 0;
    }

    private void AssignPlayablePolicy()
    {
        if (_world is not GameState world || _playerForceId is not int player ||
            _playGovernanceSlot.Selected < 0 || _playGovernanceOfficer.Selected < 0) return;
        int officerId = _playGovernanceOfficer.GetSelectedId();
        SetGovernanceOrder? order = GovernanceCommandComposer.ReplaceOfficer(world, player,
            _playGovernanceSlot.GetSelectedId(), officerId == NoGovernanceOfficerId ? null : officerId);
        if (order is not null) ApplyPlayableOrder(order);
    }

    private void RefreshPlayCityChoices()
    {
        int oldArea = _playArea.Selected >= 0 ? _playArea.GetSelectedId() : -1;
        int oldAdminOfficer = _playAdminOfficer.Selected >= 0 ? _playAdminOfficer.GetSelectedId() : -1;
        int oldOfficer = _playOfficer.Selected >= 0 ? _playOfficer.GetSelectedId() : -1;
        int oldDutyOfficer = _playDutyOfficer.Selected >= 0 ? _playDutyOfficer.GetSelectedId() : -1;
        int oldTransportCity = _playTransportCity.Selected >= 0 ? _playTransportCity.GetSelectedId() : -1;
        int oldMoveOfficer = _playMoveOfficer.Selected >= 0 ? _playMoveOfficer.GetSelectedId() : -1;
        int oldMoveCity = _playMoveCity.Selected >= 0 ? _playMoveCity.GetSelectedId() : -1;
        int oldSummonOfficer = _playSummonOfficer.Selected >= 0 ? _playSummonOfficer.GetSelectedId() : -1;
        int oldTalentExecutor = _playTalentExecutor.Selected >= 0 ? _playTalentExecutor.GetSelectedId() : -1;
        int oldTalentTarget = _playTalentTarget.Selected >= 0 ? _playTalentTarget.GetSelectedId() : -1;
        int oldPrepared = _playPrepared.Selected >= 0 ? _playPrepared.GetSelectedId() : -1;
        _playArea.Clear();
        _playAdminOfficer.Clear();
        _playOfficer.Clear();
        _playDutyOfficer.Clear();
        _playTransportCity.Clear();
        _playMoveOfficer.Clear();
        _playMoveCity.Clear();
        _playSummonOfficer.Clear();
        _playTalentExecutor.Clear();
        _playTalentTarget.Clear();
        _playPrepared.Clear();
        _playDutyStatus.Text = "";
        if (_world is not GameState world || _playerForceId is not int player || _playCity.Selected < 0)
        {
            RefreshPlayFormationChoices();
            return;
        }
        int sourceId = _playCity.GetSelectedId();
        CityCommandChoices? choices = CityCommandChoices.Create(world, player, sourceId);
        if (choices is null) { RefreshPlayFormationChoices(); return; }
        foreach (NamedCommandChoice target in choices.DestinationCities)
        {
            _playTransportCity.AddItem(target.Name, target.Id);
            _playMoveCity.AddItem(target.Name, target.Id);
        }
        int transportIndex = _playTransportCity.GetItemIndex(oldTransportCity);
        if (_playTransportCity.ItemCount > 0)
            _playTransportCity.Select(transportIndex < 0 ? 0 : transportIndex);
        int moveCityIndex = _playMoveCity.GetItemIndex(oldMoveCity);
        if (_playMoveCity.ItemCount > 0)
            _playMoveCity.Select(moveCityIndex < 0 ? 0 : moveCityIndex);
        foreach (PreparedArmyChoice plan in choices.PreparedArmies)
        {
            _playPrepared.AddItem(plan.BuildTargetHexId is int buildHex
                ? _ui.Format("play_prepared_build_item",
                    plan.CommanderName, plan.Troops, buildHex)
                : _ui.Format("play_prepared_item",
                    plan.CommanderName, plan.Troops), plan.Id);
        }
        int preparedIndex = _playPrepared.GetItemIndex(oldPrepared);
        if (_playPrepared.ItemCount > 0)
            _playPrepared.Select(preparedIndex < 0 ? 0 : preparedIndex);
        _playTransportSupplies.MaxValue = choices.MaximumSupplies;
        _playTransportGold.MaxValue = choices.MaximumGold;
        if (_playTransportSupplies.Value > _playTransportSupplies.MaxValue)
            _playTransportSupplies.Value = _playTransportSupplies.MaxValue;
        if (_playTransportGold.Value > _playTransportGold.MaxValue)
            _playTransportGold.Value = _playTransportGold.MaxValue;
        foreach (NamedCommandChoice area in choices.Areas)
            _playArea.AddItem(string.IsNullOrEmpty(area.Name) ? area.Id.ToString() : area.Name, area.Id);
        int areaIndex = _playArea.GetItemIndex(oldArea);
        if (_playArea.ItemCount > 0) _playArea.Select(areaIndex < 0 ? 0 : areaIndex);
        foreach (NamedCommandChoice officer in choices.Officers)
        {
            _playOfficer.AddItem(officer.Name, officer.Id);
            if (choices.IsCity)
            {
                _playAdminOfficer.AddItem(officer.Name, officer.Id);
                _playDutyOfficer.AddItem(officer.Name, officer.Id);
            }
        }
        foreach (NamedCommandChoice officer in choices.TalentExecutors)
            _playTalentExecutor.AddItem(officer.Name, officer.Id);
        foreach (NamedCommandChoice target in choices.TalentTargets)
            _playTalentTarget.AddItem(target.Name, target.Id);
        int talentExecutorIndex = _playTalentExecutor.GetItemIndex(oldTalentExecutor);
        if (_playTalentExecutor.ItemCount > 0)
            _playTalentExecutor.Select(talentExecutorIndex < 0 ? 0 : talentExecutorIndex);
        int talentTargetIndex = _playTalentTarget.GetItemIndex(oldTalentTarget);
        if (_playTalentTarget.ItemCount > 0)
            _playTalentTarget.Select(talentTargetIndex < 0 ? 0 : talentTargetIndex);
        foreach (NamedCommandChoice officer in choices.MoveOfficers)
            _playMoveOfficer.AddItem(officer.Name, officer.Id);
        foreach (SummonOfficerChoice officer in choices.SummonOfficers)
            _playSummonOfficer.AddItem(_ui.Format("play_summon_item", officer.Name,
                officer.SourceCityName), officer.Id);
        int moveOfficerIndex = _playMoveOfficer.GetItemIndex(oldMoveOfficer);
        if (_playMoveOfficer.ItemCount > 0)
            _playMoveOfficer.Select(moveOfficerIndex < 0 ? 0 : moveOfficerIndex);
        int summonOfficerIndex = _playSummonOfficer.GetItemIndex(oldSummonOfficer);
        if (_playSummonOfficer.ItemCount > 0)
            _playSummonOfficer.Select(summonOfficerIndex < 0 ? 0 : summonOfficerIndex);
        if (choices.IsCity) _playDutyOfficer.AddItem(_ui.Get("play_duty_none"), -1);
        int defaultOfficer = choices.DefaultCommanderId;
        int defaultAdminOfficer = choices.DefaultAdministratorId;
        int adminIndex = _playAdminOfficer.GetItemIndex(oldAdminOfficer);
        if (adminIndex < 0) adminIndex = _playAdminOfficer.GetItemIndex(defaultAdminOfficer);
        if (_playAdminOfficer.ItemCount > 0) _playAdminOfficer.Select(adminIndex < 0 ? 0 : adminIndex);
        int officerIndex = _playOfficer.GetItemIndex(oldOfficer);
        if (officerIndex < 0) officerIndex = _playOfficer.GetItemIndex(defaultOfficer);
        if (_playOfficer.ItemCount > 0) _playOfficer.Select(officerIndex < 0 ? 0 : officerIndex);
        RefreshPlayFormationChoices();
        int dutyIndex = _playDutyOfficer.GetItemIndex(oldDutyOfficer);
        if (dutyIndex < 0) dutyIndex = _playDutyOfficer.GetItemIndex(defaultAdminOfficer);
        if (_playDutyOfficer.ItemCount > 0) _playDutyOfficer.Select(dutyIndex < 0 ? 0 : dutyIndex);
        if (choices.Duties is CityDutyNames duties)
        {
            _playDutyStatus.Text = _ui.Format("play_duty_status",
                duties.Recruiter ?? _ui.Get("play_duty_none"), duties.Trainer ?? _ui.Get("play_duty_none"), duties.PreparationPeriods);
        }
        else _playDutyStatus.Text = choices.IsCity ? _ui.Get("play_duty_unknown") : "";
        _playTroops.MaxValue = choices.MaximumTroops;
        if (_playTroops.Value > _playTroops.MaxValue) _playTroops.Value = _playTroops.MaxValue;
        RefreshPlayDisbandAvailability();
        RefreshPlayOccupationAvailability();
        RefreshPlayWithdrawalAvailability();
        RefreshPlayBuildAvailability();
        RefreshExtendedCommands();
    }

    private void RefreshPlayFormationChoices()
    {
        int previous = _playFormation.Selected >= 0 ? _playFormation.GetSelectedId() : -1;
        _playFormation.Clear();
        if (_world is GameState world)
        {
            foreach (FormationCommandChoice formation in MilitaryLoadoutChoices.Formations(world, SelectionId(_playOfficer)))
            {
                _playFormation.AddItem(formation.Name, formation.Id);
                _playFormation.SetItemTooltip(_playFormation.ItemCount - 1,
                    formation.BaseCoefficients is { } values
                        ? _ui.Format("play_formation_source_base", values.Mobility,
                            values.ArmyAttack, values.GarrisonAttack, values.DurabilityAttack, values.Defense)
                        : _ui.Get("play_formation_source_unknown"));
            }
            if (_playFormation.ItemCount > 0)
            {
                int index = _playFormation.GetItemIndex(previous);
                _playFormation.Select(index < 0 ? 0 : index);
            }
        }
        RefreshPlayDeploymentAvailability();
        RefreshPlayTacticChoices();
    }

    private void RefreshPlayTacticChoices()
    {
        int[] previous = _playTactics.Select(choice => choice.Selected >= 0
            ? choice.GetSelectedId() : -1).ToArray();
        foreach (OptionButton choice in _playTactics) choice.Clear();
        IReadOnlyList<NamedCommandChoice>? available = _world is GameState world
            ? MilitaryLoadoutChoices.Tactics(world, SelectionId(_playOfficer), SelectionId(_playFormation)) : null;
        if (available is null) return;
        for (int slot = 0; slot < _playTactics.Length; slot++)
        {
            OptionButton choice = _playTactics[slot];
            choice.AddItem(_ui.Get("play_tactic_none"), NoTacticId);
            foreach (NamedCommandChoice tactic in available) choice.AddItem(tactic.Name, tactic.Id);
            int index = choice.GetItemIndex(previous[slot]);
            choice.Select(index < 0 ? 0 : index);
        }
    }

    private MilitarySelection SelectedMilitaryCommand() => new(
        SelectionId(_playCity), SelectionId(_playOfficer), SelectionId(_playFormation),
        SelectionId(_playUnit), SelectionId(_playStructureKind), _selectedCell?.Id);

    private static int? SelectionId(OptionButton choice) =>
        choice.Selected >= 0 ? choice.GetSelectedId() : null;

    private void RefreshPlayDeploymentAvailability()
    {
        DeploymentPreview preview = MilitaryCommandPreview.Deployment(_world, _playerForceId,
            SelectedMilitaryCommand(), _playRules?.Routes);
        _playDeploy.Disabled = !preview.Deploy;
        _playDeployOccupy.Disabled = !preview.Occupy;
        _playDeployBuild.Disabled = !preview.Build;
    }

    private void RefreshPlayWithdrawalAvailability() =>
        _playWithdraw.Disabled = !MilitaryCommandPreview.Withdraw(_world, _playerForceId, SelectedMilitaryCommand());

    private void RefreshPlayOccupationAvailability() =>
        _playOccupy.Disabled = !MilitaryCommandPreview.Occupy(_world, _playerForceId, SelectedMilitaryCommand());

    private void RefreshPlayDisbandAvailability() =>
        _playDisband.Disabled = !MilitaryCommandPreview.Disband(_world, _playerForceId, SelectedMilitaryCommand());

    private void RefreshPlayBuildAvailability()
    {
        StructureCommandPreview preview = MilitaryCommandPreview.Structures(_world, _playerForceId,
            SelectedMilitaryCommand());
        _playBuild.Disabled = !preview.Build;
        _playRepair.Disabled = !preview.Repair;
        _playDemolish.Disabled = !preview.Demolish;
    }

    private void StartPlayable()
    {
        if (_world is not GameState world || _selectedForceSlot is not int forceId ||
            world.Phase != TurnPhase.Initializing) return;
        CampaignStartResult campaign = _campaignComposition!.Start(_session!, forceId);
        _session = campaign.Session;
        PublishCampaignPresentation(campaign.Presentation);
        RefreshSceneAudioContext();
        _playMessage.Text = _ui.Get("play_started");
        RefreshCurrentPlayWorld();
    }

    private void PrepareTalentSearch()
    {
        if (_playerForceId is not int player || _playCity.Selected < 0 ||
            _playTalentExecutor.Selected < 0) return;
        ApplyPlayableOrder(new PrepareTalentSearchOrder(player, _playCity.GetSelectedId(),
            _playTalentExecutor.GetSelectedId()));
    }

    private void PrepareTalentRecruit()
    {
        if (_playerForceId is not int player || _playCity.Selected < 0 ||
            _playTalentExecutor.Selected < 0 || _playTalentTarget.Selected < 0) return;
        ApplyPlayableOrder(new PrepareTalentRecruitOrder(player, _playCity.GetSelectedId(),
            _playTalentExecutor.GetSelectedId(), _playTalentTarget.GetSelectedId()));
    }

    private void OrderAdministration()
    {
        if (_world is not GameState world || _playerForceId is not int player ||
            _playCity.Selected < 0 || _playArea.Selected < 0 || _playAdminOfficer.Selected < 0) return;
        int cityId = _playCity.GetSelectedId();
        int areaId = _playArea.GetSelectedId();
        int officerId = _playAdminOfficer.GetSelectedId();
        var focus = (AreaDevelopmentFocus)_playFocus.GetSelectedId();
        ApplyPlayableOrder(new SetAreaAdministrationOrder(player, cityId,
            [new AreaAdministrationChange(areaId, officerId, focus)]));
    }

    private void AssignCityDuty(CityDutyKind kind)
    {
        if (_world is null || _playerForceId is not int player ||
            _playCity.Selected < 0 || _playDutyOfficer.Selected < 0) return;
        int selected = _playDutyOfficer.GetSelectedId();
        ApplyPlayableOrder(new SetCityDutyOrder(player, _playCity.GetSelectedId(), kind,
            selected < 0 ? null : selected));
    }

    private void OrderDeployment() => PreparePlayableDeployment(false);

    private void OrderOccupationDeployment() => PreparePlayableDeployment(true);

    private void OrderConstructionDeployment() => PreparePlayableDeployment(false, true);

    private void PreparePlayableDeployment(bool occupyArea, bool buildStructure = false)
    {
        if (_world is not GameState world || _playerForceId is not int player ||
            _playCity.Selected < 0 || _playOfficer.Selected < 0 ||
            _playFormation.Selected < 0 || _selectedCell is not MapCell target) return;
        if (buildStructure && _playStructureKind.Selected < 0) return;
        if (occupyArea && (!world.Territory.TryGetValue(target.Id, out TerritoryState? land) ||
                           land.AreaId is null)) return;
        if (buildStructure && (_playRules is null ||
            !PlayableConstructionRules.CanBuildAt(world, _playRules.Routes, player,
                target.Id, world.Structures ?? []))) return;
        int officerId = _playOfficer.GetSelectedId();
        int formationId = _playFormation.GetSelectedId();
        DeploymentAction action = occupyArea
            ? new DeploymentAction("occupy", new DeploymentTarget(DeploymentTargetKind.Area,
                world.Territory[target.Id].AreaId!.Value))
            : buildStructure
            ? new DeploymentAction("build", new DeploymentTarget(DeploymentTargetKind.Hex,
                target.Id), PlayableConstructionRules.DescribeSlot(_playStructureKind.GetSelectedId()).KindId)
            : new DeploymentAction("move", new DeploymentTarget(DeploymentTargetKind.Hex, target.Id));
        int[] tacticIds = _playTactics.Where(choice => choice.Selected >= 0 &&
            choice.GetSelectedId() != NoTacticId).Select(choice => choice.GetSelectedId()).ToArray();
        var request = new DeploymentUnitRequest(officerId, formationId, (int)_playTroops.Value,
            tacticIds,
            [action])
        {
            Behavior = _militaryOrders.BehaviorSelection,
            WaypointHexIds = _militaryOrders.WaypointSnapshot()
        };
        ApplyPlayableOrder(new PrepareDeploymentOrder(player, _playCity.GetSelectedId(), [request]));
    }

    private void CancelPreparedDeployment()
    {
        if (_world is null || _playerForceId is not int player || _playPrepared.Selected < 0) return;
        ApplyPlayableOrder(new CancelPreparedDeploymentOrder(player, _playPrepared.GetSelectedId()));
    }

    private void DispatchTransport()
    {
        if (_world is null || _playerForceId is not int player ||
            _playCity.Selected < 0 || _playTransportCity.Selected < 0 ||
            _playOfficer.Selected < 0) return;
        ApplyPlayableOrder(new DispatchTransportOrder(player, _playCity.GetSelectedId(),
            _playTransportCity.GetSelectedId(), _playOfficer.GetSelectedId(),
            (int)_playTroops.Value, (int)_playTransportSupplies.Value,
            (int)_playTransportGold.Value,
            (TransportArrivalDisposition)_playTransportArrival.GetSelectedId()));
    }

    private void DispatchOfficerMove()
    {
        if (_world is null || _playerForceId is not int player ||
            _playCity.Selected < 0 || _playMoveCity.Selected < 0 ||
            _playMoveOfficer.Selected < 0) return;
        ApplyPlayableOrder(new PrepareOfficerMoveOrder(player, _playCity.GetSelectedId(),
            _playMoveCity.GetSelectedId(), _playMoveOfficer.GetSelectedId()));
    }

    private void SummonOfficer()
    {
        if (_world is not GameState world || _playerForceId is not int player ||
            _playCity.Selected < 0 || _playSummonOfficer.Selected < 0) return;
        int officerId = _playSummonOfficer.GetSelectedId();
        if (world.Officers[officerId].LocationId is not int sourceId) return;
        ApplyPlayableOrder(new PrepareOfficerMoveOrder(player, sourceId,
            _playCity.GetSelectedId(), officerId, OfficerMoveMode.Summon));
    }

    private void RetargetUnit()
    {
        if (_world is null || _playerForceId is not int player ||
            _playUnit.Selected < 0 || _selectedCell is not MapCell target) return;
        ApplyPlayableOrder(new RetargetUnitOrder(player, _playUnit.GetSelectedId(), target.Id));
    }

    private void BuildStructure()
    {
        if (_world is null || _playerForceId is not int player ||
            _playUnit.Selected < 0 || _playStructureKind.Selected < 0 ||
            _selectedCell is not MapCell target) return;
        ApplyPlayableOrder(new BuildStructureOrder(player, _playUnit.GetSelectedId(),
            target.Id, PlayableConstructionRules.DescribeSlot(_playStructureKind.GetSelectedId()).KindId));
    }

    private void RepairStructure()
    {
        if (_world?.Structures is not { } structures || _playerForceId is not int player ||
            _playUnit.Selected < 0 || _selectedCell is not MapCell target) return;
        StructureState? structure = structures.Values.FirstOrDefault(item => item.HexId == target.Id);
        if (structure is not null)
            ApplyPlayableOrder(new RepairStructureOrder(player, _playUnit.GetSelectedId(), structure.Id));
    }

    private void DemolishStructure()
    {
        if (_world?.Structures is not { } structures || _playerForceId is not int player ||
            _playUnit.Selected < 0 || _selectedCell is not MapCell target) return;
        StructureState? structure = structures.Values.FirstOrDefault(item => item.HexId == target.Id);
        if (structure is not null)
            ApplyPlayableOrder(new DemolishStructureOrder(player, _playUnit.GetSelectedId(), structure.Id));
    }

    private void OccupySelectedArea()
    {
        if (_world is not GameState world || _playerForceId is not int player ||
            _playUnit.Selected < 0 || _selectedCell is not MapCell target ||
            !world.Territory.TryGetValue(target.Id, out TerritoryState? land) ||
            land.AreaId is not int areaId) return;
        ApplyPlayableOrder(new OccupyAreaOrder(player, _playUnit.GetSelectedId(), areaId));
    }

    private void WithdrawUnit()
    {
        if (_world is null || _playerForceId is not int player || _playUnit.Selected < 0) return;
        ApplyPlayableOrder(new WithdrawUnitOrder(player, _playUnit.GetSelectedId()));
    }

    private void DisbandUnit()
    {
        if (_world is null || _playerForceId is not int player ||
            _playUnit.Selected < 0 || _playCity.Selected < 0) return;
        ApplyPlayableOrder(new DisbandUnitOrder(player, _playUnit.GetSelectedId(),
            _playCity.GetSelectedId()));
    }

    private void ApplyPlayableOrder(GameOrder order)
    {
        if (_session?.PlayerForceId is null) return;
        CommandResult result = _session!.Issue(order);
        if (!result.Accepted)
        {
            _playMessage.Text = _ui.Format("play_order_rejected",
                result.MissingRuleKey is { } missing ? _ui.CommandError(missing) :
                result.ErrorCode is { } code ? _ui.CommandError(code) : result.Error ?? "?");
            return;
        }
        _playMessage.Text = _ui.Get("play_order_accepted");
        RefreshCurrentPlayWorld();
    }

    private void AdvancePlayable()
    {
        if (_session?.PlayerForceId is null) return;
        if (!_session!.AdvancePeriod()) return;
        _playMessage.Text = "";
        RefreshCurrentPlayWorld();
        if (_session.World.Phase == TurnPhase.Reporting) SetMapFocus(false);
    }

    private void FinishPlayableReport()
    {
        if (_world is not { Phase: TurnPhase.Reporting } || _playerForceId is null) return;
        _session!.FinishReport();
        _playMessage.Text = "";
        RefreshCurrentPlayWorld();
    }

    private void RefreshCurrentPlayWorld()
    {
        if (_force.Selected >= 0) ShowForce(_force.Selected);
        else DrawMap();
        RefreshTileInfo();
        RefreshPlayControls();
        UpdateResidentMap();
    }

    private string PlayableSavePath(int slot)
    {
        if (slot < 1 || slot > PlayableSaveSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        string fileName = slot == 1 ? "playable-session.sav" : $"playable-session-{slot:00}.sav";
        return ProjectSettings.GlobalizePath("user://saves/" + fileName);
    }

    private int SelectedPlayableSaveSlot => _playSaveSlot.GetSelectedId();

    private void RefreshSaveSlots()
    {
        for (int index = 0; index < _playSaveSlot.ItemCount; index++)
        {
            int slot = _playSaveSlot.GetItemId(index);
            string label = _ui.Format("play_save_slot_item", slot);
            if (File.Exists(PlayableSavePath(slot)))
                label += " " + _ui.Get("play_save_slot_used");
            _playSaveSlot.SetItemText(index, label);
        }
        _playContinue.Disabled = !File.Exists(PlayableSavePath(SelectedPlayableSaveSlot));
    }

    private void SavePlayable()
    {
        if (_session?.PlayerForceId is null) return;
        _session!.Save(PlayableSavePath(SelectedPlayableSaveSlot));
        RefreshSaveSlots();
        _playMessage.Text = _ui.Get("play_saved");
    }

    private void LoadPlayable()
    {
        GameSessionSave restored = GameplaySession.ReadSave(PlayableSavePath(SelectedPlayableSaveSlot));
        int index = _shownScenarios.FindIndex(scenario =>
            GameResourceId.File(scenario.SourcePath).ToString() == restored.State.ScenarioKey);
        if (index < 0) throw new InvalidDataException("The saved scenario is unavailable in the selected language.");
        ShowScenario(index, restored.State, restored.PlayerForceId);
        _scenarioList.Select(index);
        RefreshSceneAudioContext();
        int forceIndex = _shownForces.IndexOf(restored.PlayerForceId);
        if (forceIndex >= 0)
        {
            _force.Select(forceIndex);
            ShowForce(forceIndex);
        }
        _playMessage.Text = _ui.Get("play_loaded");
        RefreshCurrentPlayWorld();
    }

    private string InitialSavePath => ProjectSettings.GlobalizePath("user://saves/initial-state.sav");

    private void SaveInitialState()
    {
        if (_world is null) return;
        try
        {
            _session!.SavePreview(InitialSavePath);
            _worldStatus.Text = _ui.InitialStateSaved;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _worldStatus.Text = _safeUi?.StateFileError(error.Message) ?? error.Message;
            GD.PushWarning(error.ToString());
        }
    }

    private void LoadInitialState()
    {
        GameState restored;
        int index;
        try
        {
            restored = GameplaySession.ReadPreview(InitialSavePath);
            index = _shownScenarios.FindIndex(scenario =>
                GameResourceId.File(scenario.SourcePath).ToString() == restored.ScenarioKey);
            if (index < 0) throw new InvalidDataException("The saved scenario is unavailable in the selected language.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            InvalidDataException or System.Text.Json.JsonException)
        {
            _worldStatus.Text = _safeUi?.StateFileError(error.Message) ?? error.Message;
            GD.PushWarning(error.ToString());
            return;
        }
        ShowScenario(index, restored);
        _scenarioList.Select(index);
        _worldStatus.Text = _ui.InitialStateLoaded;
    }

    private void ShowPortrait(CharacterPreviewRecord? person)
    {
        _assetPreview.Texture = null;
        if (person is null || _repository is null)
        {
            _assetHint.Text = _ui.PortraitUnavailable;
            return;
        }
        try
        {
            LoadedSprite sprite = _repository.LoadSprite("ScreenLayout",
                $"face_{person.AppearanceCode:0000}_unique");
            RgbaTexture texture = sprite.Texture.Texture;
            Image image = Image.CreateFromData(texture.Width, texture.Height,
                false, Image.Format.Rgba8, texture.Pixels);
            SpriteRectangle rectangle = sprite.Definition.GetRectangle(texture.Width, texture.Height);
            _assetPreview.Texture = new AtlasTexture
            {
                Atlas = ImageTexture.CreateFromImage(image),
                Region = new Rect2(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height)
            };
            _assetHint.Text = _ui.PortraitSource(person.Name);
        }
        catch (Exception error)
        {
            _assetHint.Text = _safeUi?.PortraitError(person.Name, error.Message) ?? error.Message;
            GD.PushWarning(error.ToString());
        }
    }

    private void DrawMap()
    {
        if (_map is null) return;
        GameState? world = DisplayedMapWorld();
        if (world is { RulesetId: PlayableSkeleton.RulesetId } && !_mapSurfaceAttempted &&
            _repository is not null)
        {
            _mapSurfaceAttempted = true;
            try { _mapSurface = MapSurface.Build(_repository, _map); }
            catch (Exception error)
            {
                _mapHint.Text = _ui.Get("map_surface_fallback");
                GD.PushWarning(error.ToString());
            }
        }
        using Image image = Image.CreateEmpty(_map.Width, _map.Height, false, Image.Format.Rgb8);
        foreach (MapCell cell in _map.Cells)
        {
            byte code = cell.SurfaceCode;
            Color color = code == 23 ? new Color("#0d2630") :
                code == 19 ? new Color("#28373b") :
                Color.FromHsv((code * 0.137f) % 1.0f, 0.50f, 0.78f);
            if (_mapMode.Selected == 0 && (world is not null || _territory is not null))
            {
                (int? area, int? owner) = DisplayedMapOwnership(cell.Id, world);
                Color ownerColor = area is null ? new Color("#0d2630") :
                    _selectedForceSlot is int selected && owner == selected ? new Color("#eaa52f") :
                    owner is null ? new Color("#a1aba2") : new Color("#64748a");
                color = _mapSurface is null ? ownerColor :
                    _mapSurface.ColorAt(cell.Id).Lerp(ownerColor,
                        area is null ? 0.20f : owner is null ? 0.13f : 0.32f);
            }
            image.SetPixel(cell.Id % _map.Width, cell.Id / _map.Width, color);
        }
        if (world is not null || _selectedSnapshot is not null)
        {
            IEnumerable<int?> gatePositions = world is not null
                ? world.Gates.Values.Select(gate => gate.HexId)
                : _selectedSnapshot!.Gates.Where(gate => gate.Slot != 0).Select(gate => gate.HexId);
            foreach (int? position in gatePositions)
            {
                if (position is not int hex) continue;
                ValidateDisplayedHex(hex);
                int x = hex % _map.Width;
                int y = hex / _map.Width;
                Color marker = new("#55e5d2");
                for (int delta = -1; delta <= 1; delta++)
                {
                    if (x + delta >= 0 && x + delta < _map.Width)
                        image.SetPixel(x + delta, y, marker);
                    if (y + delta >= 0 && y + delta < _map.Height)
                        image.SetPixel(x, y + delta, marker);
                }
            }
            IEnumerable<(int? HexId, int? ForceId)> cityPositions = world is not null
                ? world.Cities.Values.Select(city => (city.HexId, city.ForceId))
                : _selectedSnapshot!.Cities.Where(city => city.Slot != 0)
                    .Select(city => (city.HexId, city.ForceSlot == 0 ? (int?)null : city.ForceSlot));
            foreach ((int? position, int? forceId) in cityPositions)
            {
                if (position is not int hex) continue;
                ValidateDisplayedHex(hex);
                int x = hex % _map.Width;
                int y = hex / _map.Width;
                Color marker = _selectedForceSlot is int selected && forceId == selected
                    ? new Color("#ffd66b") : new Color("#f0f5ed");
                for (int delta = -1; delta <= 1; delta++)
                {
                    if (x + delta >= 0 && x + delta < _map.Width)
                        image.SetPixel(x + delta, y, marker);
                    if (y + delta >= 0 && y + delta < _map.Height)
                        image.SetPixel(x, y + delta, marker);
                }
            }
            if (world is not null)
                foreach (UnitState unit in world.Units.Values.OrderBy(unit => unit.Id))
                {
                    ValidateDisplayedHex(unit.HexId);
                    int x = unit.HexId % _map.Width;
                    int y = unit.HexId / _map.Width;
                    Color marker = unit.ForceId == _playerForceId ? new Color("#ff4837") :
                        new Color("#e953da");
                    for (int delta = -2; delta <= 2; delta++)
                    {
                        if (x + delta >= 0 && x + delta < _map.Width)
                            image.SetPixel(x + delta, y, marker);
                        if (y + delta >= 0 && y + delta < _map.Height)
                            image.SetPixel(x, y + delta, marker);
                    }
                }
        }
        _mapTexture.Texture = ImageTexture.CreateFromImage(image);
        _mapTexture.SetMeta("original_backdrop_available", _mapSurface is not null);
        if (_mapSurface is not null)
        {
            _mapTexture.SetMeta("original_backdrop_source_uri", _mapSurface.SourceUri);
            _mapTexture.SetMeta("original_backdrop_source_sha256", _mapSurface.SourceSha256);
        }
    }

    private void OnMapInput(InputEvent input)
    {
        if (_map is null || input is not InputEventMouseButton button || !button.Pressed ||
            button.ButtonIndex != MouseButton.Left) return;
        int x = Math.Clamp((int)(button.Position.X / _mapTexture.Size.X * _map.Width), 0, _map.Width - 1);
        int y = Math.Clamp((int)(button.Position.Y / _mapTexture.Size.Y * _map.Height), 0, _map.Height - 1);
        SelectMapTarget(y * _map.Width + x);
    }

    private void RefreshTileInfo()
    {
        if (_selectedCell is not MapCell cell || _map is null)
        {
            _tileInfo.Text = _ui.TilePrompt;
            return;
        }
        GameState? world = DisplayedMapWorld();
        ValidateDisplayedHex(cell.Id);
        int x = cell.Id % _map.Width;
        int y = cell.Id / _map.Width;
        string? locationName = world is not null
            ? world.Cities.Values.FirstOrDefault(city => city.HexId == cell.Id)?.Name ??
                world.Gates.Values.FirstOrDefault(gate => gate.HexId == cell.Id)?.Name
            : _selectedSnapshot?.Cities.FirstOrDefault(city => city.Slot != 0 && city.HexId == cell.Id)?.Name ??
                _selectedSnapshot?.Gates.FirstOrDefault(gate => gate.Slot != 0 && gate.HexId == cell.Id)?.Name;
        string location = locationName is null ? "" :
            $"{_ui.MapSeparator}{_ui.Location} {locationName}";
        string region = "";
        if (world is not null || (_territory is not null && _selectedSnapshot is not null))
        {
            (int? areaId, int? forceId) = DisplayedMapOwnership(cell.Id, world);
            string? areaName = world is not null
                ? areaId is int id ? world.Areas.GetValueOrDefault(id)?.Name : null
                : _selectedSnapshot!.Areas.FirstOrDefault(item => item.Slot == (areaId ?? 0))?.Name;
            string? forceName = world is not null
                ? forceId is int forceKey ? world.Forces.GetValueOrDefault(forceKey)?.Name : null
                : _selectedSnapshot!.Forces.FirstOrDefault(item => item.Slot == (forceId ?? 0))?.RulerName;
            string owner = forceId is null ? _ui.Neutral : forceName ?? forceId.Value.ToString();
            region = $"{_ui.MapSeparator}{_ui.Region} {areaName ?? (areaId ?? 0).ToString()}" +
                $"{_ui.MapSeparator}{_ui.Force} {owner}";
        }
        _tileInfo.Text = $"{_ui.Cell} {cell.Id} ({x}, {y}){_ui.MapSeparator}{_ui.Surface} {cell.SurfaceCode}{_ui.MapSeparator}" +
            $"{_ui.RawFields} {cell.Field1:X8} / {cell.Field2:X8} / {cell.Field3:X8}{region}{location}";
    }

    // Rendering keeps the immutable raw map separate from current saveable ownership.
    private GameState? DisplayedMapWorld()
    {
        MapDocument map = _map ?? throw new InvalidDataException("No displayed map is loaded.");
        int count = checked(map.Width * map.Height);
        if (map.Width < 1 || map.Height < 1 || map.Cells.Length != count)
            throw new InvalidDataException("Displayed map dimensions and cell count differ.");
        if (_world is not GameState world)
        {
            if (_territory is not null && (_territory.Width != map.Width || _territory.Height != map.Height ||
                _territory.Cells.Count != count))
                throw new InvalidDataException("Initial territory does not match the displayed map dimensions.");
            return null;
        }
        if (world.MapWidth != map.Width || world.MapHeight != map.Height || world.Territory.Count != count)
            throw new InvalidDataException("Current world does not match the displayed map dimensions or territory count.");
        // Reconstruct the exact MapReader representation to validate the actual displayed cells,
        // including mutable array copies; a matching URI or cell count alone is insufficient.
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        Span<byte> row = stackalloc byte[16];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(row, count);
        hash.AppendData(row[..4]);
        for (int index = 0; index < count; index++)
        {
            MapCell cell = map.Cells[index];
            if (cell.Id != index) throw new InvalidDataException($"Displayed map has an invalid cell ID at {index}.");
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(row, cell.Id);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(row[4..], cell.Field1);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(row[8..], cell.Field2);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(row[12..], cell.Field3);
            hash.AppendData(row);
        }
        string actualHash = Convert.ToHexString(hash.GetHashAndReset());
        if (!actualHash.Equals(world.MapSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Current world map source {world.MapSha256} does not match displayed map {actualHash}.");
        return world;
    }

    private (int? AreaId, int? ForceId) DisplayedMapOwnership(int hexId, GameState? world)
    {
        if (world is not null)
        {
            if (!world.Territory.TryGetValue(hexId, out TerritoryState? tile) || tile.Id != hexId ||
                (tile.AreaId is int area && !world.Areas.ContainsKey(area)) ||
                (tile.ForceId is int force && !world.Forces.ContainsKey(force)))
                throw new InvalidDataException($"Current world has invalid territory at displayed cell {hexId}.");
            return (tile.AreaId, tile.ForceId);
        }
        TerritoryCell initial = _territory!.Cells[hexId];
        if (initial.HexId != hexId) throw new InvalidDataException($"Initial territory cell {hexId} is misaligned.");
        return (initial.AreaId == 0 ? null : initial.AreaId, initial.ForceId == 0 ? null : initial.ForceId);
    }

    private void ValidateDisplayedHex(int hexId)
    {
        if (_map is null || (uint)hexId >= _map.Cells.Length)
            throw new InvalidDataException($"Location {hexId} is outside the displayed map.");
    }
}
