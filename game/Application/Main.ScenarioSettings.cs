using Godot;

public partial class Main
{
    private CurrentScenarioSettingsView? _scenarioSettingsView;
    private bool HasScenarioSettings => _scenarioSettingsView is not null;

    private void OpenScenarioSettings()
    {
        if (_failed || _shuttingDown || HasScenarioSettings || _repository is null ||
            _mapSystemMenu is not { } parent || _mapSystemSession is not { } session ||
            _campaignComposition is not { } composition || !ReferenceEquals(_session, session) ||
            _mapSystemWorld is not { } world || !IsInspectionWorldCurrent(session, world) ||
            _residentMap != _mapSystemMap) return;
        bool periodInspection = _mapSystemPeriodInspection;
        var map = _mapSystemMap;
        var adapter = composition.PrepareArmyQueries(world);
        if (!adapter.CanViewCurrentScenarioSettings(world)) return;
        var publication = adapter.CaptureCurrentScenarioSettings(world, _ui.SourceLocale);
        var view = new CurrentScenarioSettingsView(); _scenarioSettingsView = view;
        parent.SetBusy(true); parent.SuspendContent(); AddChild(view);
        try { view.Configure(_repository, _ui, publication, Current, () => RunUiAction(() =>
            { if (Current()) CloseScenarioSettings(); }, allowDuringPeriod: periodInspection)); }
        catch { CloseScenarioSettings(); throw; }

        bool Current() => !_failed && !_shuttingDown && _scenarioSettingsView == view &&
            _mapSystemMenu == parent && _residentMap == map && ReferenceEquals(_session, session) &&
            _mapSystemWorld is { } queryWorld && IsInspectionWorldCurrent(session, queryWorld);
    }

    private void CloseScenarioSettings(bool resumeParent = true)
    {
        var view = _scenarioSettingsView; _scenarioSettingsView = null;
        if (view is not null) { view.Abandon(); view.QueueFree(); }
        if (resumeParent && _mapSystemMenu is { } parent && ResumeMapSystemMenu()) parent.SetBusy(false);
    }
}
