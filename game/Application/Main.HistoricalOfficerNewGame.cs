using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

public partial class Main
{
    private CommandSelectionView? _historicalOfficerNewGame;
    private HistoricalOfficerProfile? _historicalOfficerNewGameProfile;
    private HistoricalOfficerEditorCatalog? _historicalOfficerNewGameCatalog;
    private (int Number, string Edition)? _historicalOfficerNewGameScenario;
    private IReadOnlyList<HistoricalOfficerEditorRow> _historicalOfficerNewGameCandidates = [];
    private int[] _historicalOfficerNewGameAdoptedIds = [];
    private Action<bool>? _historicalOfficerNewGameCompleted;

    private bool HasHistoricalOfficerNewGame => _historicalOfficerNewGame is not null;
    private bool HasHistoricalOfficerNewGameOverrides => _historicalOfficerNewGameProfile is { Overrides.IsEmpty: false };
    private bool HasHistoricalOfficerNewGameCandidates => _historicalOfficerNewGameCandidates.Count != 0;

    private void PrepareHistoricalOfficerNewGame(LoadedScenario source, EffectiveScenario selection)
    {
        if (_repository is null) throw new InvalidOperationException("Historical officer resources are unavailable.");
        if (source.Snapshot.Locale != selection.Locale)
            throw new InvalidDataException("Historical officer new-game source language differs from the selected scenario.");
        if (!PrepareHistoricalOfficerNewGameProfile(selection)) return;
        if (_historicalOfficerNewGameCatalog is null || _historicalOfficerNewGameCatalog.Locale != selection.Locale)
            _historicalOfficerNewGameCatalog = HistoricalOfficerEditorCatalog.Load(_repository, selection.Locale,
                GetRegisteredOfficerStore().Current, CollectionOfficerAdmission.FromProfile(GetApplicationProfile().Current));
        _historicalOfficerNewGameCandidates = HistoricalOfficerScenario.ReadNewGameCandidates(source,
            _historicalOfficerNewGameProfile!, _historicalOfficerNewGameCatalog);
        if (_historicalOfficerNewGameAdoptedIds.Any(id => !_historicalOfficerNewGameCandidates.Any(row => row.Id == id)))
            throw new InvalidDataException("An adopted historical officer is unavailable in the selected new-game source.");
    }

    // Empty editor inputs need no second parse of the selected original
    // scenario. Keep the same per-selection profile and reset semantics.
    private bool PrepareHistoricalOfficerNewGameProfile(EffectiveScenario selection)
    {
        var key = (selection.Number, selection.Edition);
        if (_historicalOfficerNewGameScenario != key)
        {
            ResetHistoricalOfficerNewGame();
            _historicalOfficerNewGameScenario = key;
        }
        _historicalOfficerNewGameProfile ??= GetHistoricalOfficerStore().Current;
        if (_historicalOfficerNewGameProfile.Overrides.IsEmpty)
        {
            _historicalOfficerNewGameCandidates = [];
            return false;
        }
        return true;
    }

    // The ordinary management page invokes this child from its source history
    // button. Returning does not accept or restart the enclosing new game.
    private void BeginHistoricalOfficerNewGame(LoadedScenario source, Action<bool> completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        if (!TryOpenHistoricalOfficerNewGame(source, completed)) completed(false);
    }

    private bool TryOpenHistoricalOfficerNewGame(LoadedScenario source, Action<bool> completed)
    {
        if (HasHistoricalOfficerNewGame) return true;
        if (_failed || _shuttingDown || _repository is null || _session is not { } session ||
            _world is not { Phase: TurnPhase.Initializing } || _playerForceId.HasValue ||
            _historicalOfficerNewGameCatalog is not { } catalog ||
            _historicalOfficerNewGameProfile is null || !HasHistoricalOfficerNewGameCandidates)
            return false;
        if (source.Snapshot.Locale != catalog.Locale)
            throw new InvalidDataException("Historical officer adoption has no matching title catalogue.");
        var candidates = _historicalOfficerNewGameCandidates;
        var scenario = _historicalOfficerNewGameScenario;
        var people = source.Snapshot.Characters.Where(person => person.CharacterId != 0)
            .ToDictionary(person => (int)person.CharacterId);
        var messages = _repository.LoadMessages(catalog.Locale, MessageKind.Data).Messages.Sections[1];
        string Caption(int id) => messages.Records[id].Text
            ?? throw new InvalidDataException("Historical officer adoption column caption is missing.");
        CommandSelectionColumn[] columns = [new("name", Caption(0x211), 400),
            new("leadership", Caption(0x238), 180, true), new("strength", Caption(0x239), 180, true),
            new("intelligence", Caption(0x23A), 180, true), new("politics", Caption(0x23B), 180, true),
            new("charm", Caption(0x23C), 180, true)];
        var rows = candidates.Select(candidate =>
        {
            var person = people[candidate.Id];
            return new CommandSelectionRow(candidate.Id,
                [CommandSelectionCell.PersonName(person.Name, person.NameSortKey),
                    new(person.Leadership.ToString(), person.Leadership), new(person.Strength.ToString(), person.Strength),
                    new(person.Intelligence.ToString(), person.Intelligence), new(person.Politics.ToString(), person.Politics),
                    new(person.Charm.ToString(), person.Charm)], true);
        }).ToArray();
        var view = new CommandSelectionView();
        _historicalOfficerNewGame = view;
        _historicalOfficerNewGameCompleted = completed;
        AddChild(view);
        try
        {
            view.Configure(_repository, _ui, 0x6BC, columns, rows, _historicalOfficerNewGameAdoptedIds,
                rows.Length,
                () => !_failed && !_shuttingDown && _historicalOfficerNewGame == view &&
                    ReferenceEquals(_session, session) && _world is { Phase: TurnPhase.Initializing } &&
                    !_playerForceId.HasValue && _historicalOfficerNewGameScenario == scenario,
                result => RunUiAction(() =>
                {
                    if (_historicalOfficerNewGame != view || !ReferenceEquals(_session, session)) return;
                    if (result is not null) _historicalOfficerNewGameAdoptedIds = result.ToArray();
                    var callback = _historicalOfficerNewGameCompleted;
                    CloseHistoricalOfficerNewGame();
                    callback?.Invoke(result is not null);
                }), CurrentListScrollSpeed,
                guide: new AdministrationMessages(_repository, catalog.Locale).Format(0x2864));
            view.Name = "HistoricalOfficerNewGameAdoption";
            view.ZIndex = 6;
            return true;
        }
        catch { CloseHistoricalOfficerNewGame(); throw; }
    }

    private LoadedScenario TransformHistoricalOfficerScenario(LoadedScenario original, EffectiveScenario selection)
    {
        if (!_registeredOfficerNewGameAccepted || _historicalOfficerNewGameScenario != (selection.Number, selection.Edition) ||
            _historicalOfficerNewGameProfile is not { } profile || _historicalOfficerNewGameAdoptedIds.Length == 0)
            return original;
        return HistoricalOfficerScenario.Apply(_repository!, original, profile, _historicalOfficerNewGameAdoptedIds,
            GetNewGamePortraitCatalog(selection.Locale));
    }

    private void RequestHistoricalOfficerNewGameBack() => _historicalOfficerNewGame?.RequestCancel();

    private void CloseHistoricalOfficerNewGame()
    {
        var view = _historicalOfficerNewGame;
        _historicalOfficerNewGame = null;
        _historicalOfficerNewGameCompleted = null;
        if (view is null) return;
        view.Abandon(); view.GetParent()?.RemoveChild(view); view.QueueFree();
    }

    private void ResetHistoricalOfficerNewGame()
    {
        CloseHistoricalOfficerNewGame();
        _historicalOfficerNewGameProfile = null; _historicalOfficerNewGameCatalog = null;
        _historicalOfficerNewGameScenario = null;
        _historicalOfficerNewGameCandidates = []; _historicalOfficerNewGameAdoptedIds = [];
    }
}
