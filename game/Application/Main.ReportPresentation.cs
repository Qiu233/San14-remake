using Godot;
using San14.Client.Presentation;
using San14.Core;

public partial class Main
{
    private PlayablePeriodReportView? _periodReportView;
    private GameState? _reportPresentationWorld;
    private PreviewText? _reportPresentationText;
    private int _reportPresentationForce;
    private PeriodReportPresentation? _reportPresentation;

    private string RenderPlayReport(GameState world, int playerForce)
    {
        if (!ReferenceEquals(_reportPresentationWorld, world) || !ReferenceEquals(_reportPresentationText, _ui) ||
            _reportPresentationForce != playerForce)
        {
            _reportPresentation = PeriodReportPresenter.Create(world, playerForce,
                new PeriodReportText(_ui.Get, _ui.Format, _ui.TacticReason, _ui.MapSeparator));
            _reportPresentationWorld = world; _reportPresentationText = _ui; _reportPresentationForce = playerForce;
        }
        return _reportPresentation!.Body;
    }

    private void RefreshPlayablePeriodReport()
    {
        if(HasForcePresentation||TryOpenPersonnelPresentation()){RetirePeriodReportView();return;}
        if (HasIncomeReport) { RetirePeriodReportView(); return; }
        if (_failed || _repository is null || _sceneView is not null ||
            _session is not { } session || _campaignApplication is not { } application ||
            _world is not { Phase: TurnPhase.Reporting, RulesetId: PlayableSkeleton.RulesetId } world ||
            _playerForceId is not int player || !world.Forces.ContainsKey(player))
        {
            ClosePlayablePeriodReport();
            return;
        }
        if (RefreshActionReport()) return;
        world = _world!;
        string status = _ui.Format("play_reporting", world.Forces[player].Name,
            _ui.DateLine(world.Date.Year, world.Date.Month, world.Date.Day), world.PeriodNumber);
        _ = RenderPlayReport(world, player);
        if (_periodReportView is { } view)
        {
            EnsureReportBackground(session, application, _residentMap, view);
            view.Refresh(_ui, status, _reportPresentation!, world.PeriodNumber);
            return;
        }
        view = new PlayablePeriodReportView();
        AddChild(view);
        try
        {
            EnsureReportBackground(session, application, _residentMap, view);
            view.Configure(_repository, _ui, status, _reportPresentation!, world.PeriodNumber,
                () => RunPlayAction(FinishPlayableReport), _sourceHeaders, CurrentListScrollSpeed(), CurrentListScrollSpeed,
                TryBorrowApplicationUiResources(view));
            _periodReportView = view;
            if (_detailsView is { } details && details.GetParent() == this)
                MoveChild(details, GetChildCount() - 1);
            SetMeta("play_report_overlay_visible", true);
        }
        catch
        {
            RemoveChild(view);
            view.Free();
            if (!HasActionReport && !HasIncomeReport) CloseReportBackground();
            throw;
        }
    }

    private void ClosePlayablePeriodReport()
    {
        CloseActionReport();
        CloseIncomeReport();
        _reportPresentationWorld = null; _reportPresentationText = null; _reportPresentation = null;
        RetirePeriodReportView();
        SetMeta("play_report_overlay_visible", false);
    }

    private void RetirePeriodReportView()
    {
        if (_periodReportView is { } view)
        {
            _periodReportView = null;
            view.Retire();
            if (view.GetParent() == this) RemoveChild(view);
            view.QueueFree();
            if (!HasActionReport && !HasIncomeReport) CloseReportBackground();
        }
    }
}
