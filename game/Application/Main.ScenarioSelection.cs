using Godot;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private ScenarioSelectionView? _scenarioSelection;
    private Button _chooseScenario = null!;

    private void OpenScenarioSelection()
    {
        CancelNewGamePreparation(restoreParent: false);
        if (_failed || _shuttingDown || _scenarioSelection is not null || _repository is null ||
            _session?.PlayerForceId is not null) return;
        CloseNewGameSetup();
        var repository = _repository;
        string locale = _ui.SourceLocale;
        var selections = _shownScenarios.ToDictionary(row => row.Selection.SourcePath, StringComparer.OrdinalIgnoreCase);
        var documents = repository.ScenarioDocuments.Where(document => selections.ContainsKey(document.SourcePath))
            .ToDictionary(document => (int)(document.SelectionMetadata
                ?? throw new InvalidDataException("Scenario selection metadata is missing.")).NativeId);
        var profile = GetApplicationProfile().Current;
        var catalog = ScenarioSelectionCatalog.Create(repository, locale, documents,
            Enumerable.Range(0, 3).Select(profile.ReadEndingTrack).ToArray(), profile.ReadScenarioCompleted,
            () => null,
            document => selections[document.SourcePath].Selection.Edition switch
            {
                // Legacy badge fallback is provisional until cross-version UI
                // comparison. Serialized CE edition values take precedence.
                "CE" => 2, "PK" => 1, _ => 0
            });
        var names = ArmyMapUiSources.Create();
        var view = new ScenarioSelectionView { Name = "ScenarioSelection" };
        _scenarioSelection = view;
        AddChild(view);
        try
        {
            var resources = TryBorrowApplicationUiResources(view);
            var sprites = resources.Assets.DeclaredSprites;
            view.ConfigureOriginalCatalog(repository, resources.Assets, resources.SpriteShader, resources.Font, locale,
                catalog, (_, person) =>
                {
                    // First playable UI pass: show the installed header's stock
                    // artwork. The native current-person qualification owner is
                    // still pending; this does not manufacture an eligible result
                    // for the separately implemented qualified source resolver.
                    var route = names.PortraitNames(checked((ushort)person));
                    return sprites.LoadTexturePackSprite(route.ResourceName, route.SpriteName);
                }, false, () => !_failed && !_shuttingDown && _scenarioSelection == view && _newGamePreparation is null,
                id => RunUiAction(() =>
                {
                    string source = catalog.Scenario(id).SourcePath;
                    int index = _shownScenarios.ToList().FindIndex(row =>
                        string.Equals(row.Selection.SourcePath, source, StringComparison.OrdinalIgnoreCase));
                    if (index < 0) throw new InvalidDataException("Selected scenario is no longer installed.");
                    BeginNewGameScenarioPreparation(index);
                }), () => RunUiAction(()=>{CloseScenarioSelection();OpenTitleMenu();}), () => { },
                error => { CloseScenarioSelection(); ShowFailure(error, startup: false); }, _sourceHeaders);
            _browserContent.Hide();
            _browserContent.ProcessMode = ProcessModeEnum.Disabled;
        }
        catch { CloseScenarioSelection(); throw; }
    }

    private void CloseScenarioSelection()
    {
        var view = _scenarioSelection;
        _scenarioSelection = null;
        if (view is not null && GodotObject.IsInstanceValid(view)) view.Abandon();
        if (!_failed && !_shuttingDown)
        {
            _browserContent.ProcessMode = ProcessModeEnum.Inherit;
            _browserContent.Show();
        }
    }
}
