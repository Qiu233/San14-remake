using Godot;
using San14.Data;

public partial class Main
{
    private ItemEditorNewGameView? _newGameItemEditor;
    private ItemEditorNewGameSelection _newGameItemEditorSelection = ItemEditorNewGameSelection.Empty;
    private bool HasNewGameItemAdoption => _newGameItemEditor is not null;
    private bool HasNewGameItemCandidates => GetItemEditorStore().Current is { } library &&
        (!library.Historical.IsEmpty || !library.Created.IsEmpty);

    private LoadedScenario TransformItemEditorScenario(LoadedScenario source, uint initializationSeed)
    {
        // Older source formats remain browsable. CE is the actual embedded
        // item definition owner supported by this fresh-game projection.
        if (source.Metadata.FormatMarker != 0x66) return source;
        return ItemEditorScenario.Apply(_repository ?? throw new InvalidOperationException("The title resource owner has retired."),
            source, GetItemEditorStore().Current, _newGameItemEditorSelection, initializationSeed);
    }

    private void BeginNewGameItemAdoption(LoadedScenario source, uint seed,
        Action<ItemEditorNewGameSelection?> completed)
    {
        if (_repository is null || _newGameItemEditor is not null || _failed || _shuttingDown) return;
        var library = GetItemEditorStore().Current;
        var draft = new ItemEditorNewGameDraft(library, source, seed, _newGameItemEditorSelection);
        var view = new ItemEditorNewGameView(); _newGameItemEditor = view; AddChild(view);
        bool Current() => !_failed && !_shuttingDown && _playerForceId is null && _newGameItemEditor == view;
        void Finish(ItemEditorNewGameSelection? selection)
        {
            if (!Current()) return;
            if (selection is not null) { selection.Validate(library); _newGameItemEditorSelection = selection; }
            CloseNewGameItemAdoption(); completed(selection);
        }
        try
        {
            view.Configure(_repository, _ui, GetItemEditorCatalog(), library, draft, Current,
                selected => RunUiAction(() => Finish(selected)), () => RunUiAction(() => Finish(null)), CurrentListScrollSpeed);
        }
        catch { CloseNewGameItemAdoption(); throw; }
    }
    private void RequestNewGameItemAdoptionBack() => _newGameItemEditor?.RequestBack();
    private void CloseNewGameItemAdoption()
    {
        var view = _newGameItemEditor; _newGameItemEditor = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
    }
    private void ResetNewGameItemAdoption()
    { CloseNewGameItemAdoption(); _newGameItemEditorSelection = ItemEditorNewGameSelection.Empty; }
}
