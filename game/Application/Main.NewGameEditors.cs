using Godot;
using San14.Data;

public partial class Main
{
    private bool _newGameItemEditorAccepted;

    private LoadedScenario TransformNewGameEditors(LoadedScenario original, EffectiveScenario selection)
    {
        var key = (selection.Number, selection.Edition);
        if (_registeredOfficerNewGameScenario != key)
        {
            ResetRegisteredOfficerNewGame();
            _registeredOfficerNewGameScenario = key;
        }
        _registeredOfficerInitializationSeed ??= CaptureApplicationCrtRandom().State;
        var personalities = PersonalityEditorRuntime.PrepareFresh(original, GetPersonalityEditorStore().Current);
        var historical = TransformHistoricalOfficerScenario(personalities, selection);
        var registered = TransformRegisteredOfficerScenario(historical, selection);
        return TransformItemEditorScenario(registered, _registeredOfficerInitializationSeed.Value);
    }

    private bool TryOpenNewGameItemEditor()
    {
        if (HasNewGameItemAdoption) return true;
        if (_newGameItemEditorAccepted || !HasNewGameItemCandidates || _campaignComposition is null ||
            _session is not { } session) return false;
        var selected = _scenarioList.GetSelectedItems();
        if (selected.Length != 1) return false;
        _registeredOfficerInitializationSeed ??= CaptureApplicationCrtRandom().State;
        BeginNewGameItemAdoption(_campaignComposition.Scenario,
            _registeredOfficerInitializationSeed.Value,
            adopted => RunUiAction(() =>
            {
                if (!ReferenceEquals(_session, session)) return;
                if (adopted is null)
                {
                    _registeredOfficerNewGameAccepted = false;
                    if (_registeredOfficerNewGameLibrary is { Officers.IsEmpty: false } || HasHistoricalOfficerNewGameOverrides)
                        OpenNewGameSetup();
                    else OpenScenarioSelection();
                    return;
                }
                _newGameItemEditorAccepted = true;
                BeginNewGameScenarioPreparation(selected[0]);
            }));
        _browserContent.Hide(); _browserContent.ProcessMode = ProcessModeEnum.Disabled;
        return true;
    }
}
