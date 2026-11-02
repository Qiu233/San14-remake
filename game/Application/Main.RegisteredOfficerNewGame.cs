using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private RegisteredOfficerNewGameView? _registeredOfficerNewGame;
    private RegisteredOfficerNewGameSelection _registeredOfficerNewGameSelection = RegisteredOfficerNewGameSelection.None;
    private RegisteredOfficerLibrary? _registeredOfficerNewGameLibrary;
    private (int Number, string Edition)? _registeredOfficerNewGameScenario;
    private uint? _registeredOfficerInitializationSeed;
    private bool _registeredOfficerNewGameAccepted;
    private bool HasRegisteredOfficerNewGame => _registeredOfficerNewGame is not null;

    private bool TryOpenRegisteredOfficerNewGame()
    {
        if (HasRegisteredOfficerNewGame) return true;
        if (_repository is null || _campaignComposition is null || _session is not { } session ||
            _world is not { Phase: TurnPhase.Initializing } || _playerForceId.HasValue) return false;
        int[] selected = _scenarioList.GetSelectedItems();
        if (selected.Length != 1) return false;
        var selection = _shownScenarios[selected[0]].Selection;
        var key = (selection.Number, selection.Edition);
        if (_registeredOfficerNewGameScenario != key)
        {
            _registeredOfficerNewGameScenario = key;
            _registeredOfficerNewGameSelection = RegisteredOfficerNewGameSelection.None;
            _registeredOfficerNewGameLibrary = null;
            _registeredOfficerInitializationSeed = null;
            _registeredOfficerNewGameAccepted = false;
        }
        if (_registeredOfficerNewGameAccepted) return false;
        var library = _registeredOfficerNewGameLibrary ?? GetRegisteredOfficerStore().Current;
        if (library.Officers.IsEmpty && !PrepareHistoricalOfficerNewGameProfile(selection)) return false;
        var original = _repository.LoadScenario(selection);
        PrepareHistoricalOfficerNewGame(original, selection);
        if (library.Officers.IsEmpty && !HasHistoricalOfficerNewGameOverrides) return false;
        var draft = new RegisteredOfficerNewGameDraft(library, original, _registeredOfficerNewGameSelection);
        var view = new RegisteredOfficerNewGameView();
        _registeredOfficerNewGame = view;
        AddChild(view);
        try
        {
            view.Configure(_repository, _ui, draft,
                () => !_failed && !_shuttingDown && _registeredOfficerNewGame == view &&
                    ReferenceEquals(_session, session) && _world is { Phase: TurnPhase.Initializing } && !_playerForceId.HasValue,
                adopted => RunUiAction(() =>
                {
                    if (_registeredOfficerNewGame != view || !ReferenceEquals(_session, session)) return;
                    _registeredOfficerNewGameSelection = adopted;
                    _registeredOfficerNewGameLibrary = library;
                    _registeredOfficerInitializationSeed ??= CaptureApplicationCrtRandom().State;
                    _registeredOfficerNewGameAccepted = true;
                    CloseRegisteredOfficerNewGame();
                    BeginNewGameScenarioPreparation(selected[0]);
                }),
                () => RunUiAction(() =>
                {
                    if (_registeredOfficerNewGame != view) return;
                    CloseRegisteredOfficerNewGame();
                    OpenScenarioSelection();
                }), CurrentListScrollSpeed, OpenRegisteredOfficerCityPicker,
                HasHistoricalOfficerNewGameCandidates ? () => RunUiAction(() =>
                {
                    if (_registeredOfficerNewGame != view || !ReferenceEquals(_session, session)) return;
                    view.SetBusy(true); view.Hide();
                    try
                    {
                        BeginHistoricalOfficerNewGame(original, _ => RunUiAction(() =>
                        {
                            if (_registeredOfficerNewGame != view || !ReferenceEquals(_session, session)) return;
                            view.Show(); view.SetBusy(false);
                        }));
                    }
                    catch { view.Show(); view.SetBusy(false); throw; }
                }) : null,GetNewGamePortraitCatalog(_ui.SourceLocale));
            _browserContent.Hide();
            _browserContent.ProcessMode = ProcessModeEnum.Disabled;
            return true;
        }
        catch { CloseRegisteredOfficerNewGame(); throw; }
    }

    private LoadedScenario TransformRegisteredOfficerScenario(LoadedScenario original, EffectiveScenario selection)
    {
        if (!_registeredOfficerNewGameAccepted || _registeredOfficerNewGameScenario != (selection.Number, selection.Edition) ||
            _registeredOfficerNewGameLibrary is not { } library || _registeredOfficerNewGameSelection.AdoptedOfficerIds.IsEmpty)
            return original;
        return RegisteredOfficerScenario.Apply(_repository!, original, library, _registeredOfficerNewGameSelection,
            _registeredOfficerInitializationSeed ?? throw new InvalidOperationException("Registered-officer initialization has no captured random owner."),
            GetNewGamePortraitCatalog(selection.Locale));
    }

    private void RequestRegisteredOfficerNewGameBack()
    {
        if (HasHistoricalOfficerNewGame) RequestHistoricalOfficerNewGameBack();
        else _registeredOfficerNewGame?.RequestBack();
    }

    private void ReturnFromNewGameForceSelection()
    {
        CloseNewGameSetup();
        _newGameItemEditorAccepted = false;
        if (HasNewGameItemCandidates) OpenNewGameSetup();
        else if (_registeredOfficerNewGameLibrary is { Officers.IsEmpty: false } || HasHistoricalOfficerNewGameOverrides)
        {
            _registeredOfficerNewGameAccepted = false;
            OpenNewGameSetup();
        }
        else OpenScenarioSelection();
    }

    private void CloseRegisteredOfficerNewGame()
    {
        CloseHistoricalOfficerNewGame();
        CloseRegisteredOfficerCityPicker(notify: false);
        var view = _registeredOfficerNewGame;
        _registeredOfficerNewGame = null;
        if (view is null) return;
        view.Abandon();
        view.GetParent()?.RemoveChild(view);
        view.QueueFree();
    }

    private void ResetRegisteredOfficerNewGame()
    {
        CloseRegisteredOfficerNewGame();
        ResetHistoricalOfficerNewGame();
        ResetNewGameItemAdoption();
        _newGameItemEditorAccepted = false;
        _registeredOfficerNewGameSelection = RegisteredOfficerNewGameSelection.None;
        _registeredOfficerNewGameLibrary = null;
        _registeredOfficerNewGameScenario = null;
        _registeredOfficerInitializationSeed = null;
        _registeredOfficerNewGameAccepted = false;
    }
}
