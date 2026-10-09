using Godot;
using San14.Client.Presentation;
using San14.Core;

public partial class Main
{
    private PlayablePeriodReportView? _periodReportView;

    private string RenderPlayReport(GameState world, int playerForce)
    {
        PeriodReportPresentation report = PeriodReportPresenter.Create(world, playerForce,
            new PeriodReportText(_ui.Get, _ui.Format, _ui.TacticReason, _ui.MapSeparator),
            BindPeriodReportMetadata);
        return report.Body;
    }

    private void BindPeriodReportMetadata(PeriodReportMetadata metadata)
    {
        SetMeta("play_report_monthly_receipt_count", metadata.MonthlyReceiptCount);
        SetMeta("play_report_monthly_receipts_json", metadata.MonthlyReceiptsJson);
        SetMeta("play_report_source_event_count", metadata.SourceEventCount);
        SetMeta("play_report_source_events_json", metadata.SourceEventsJson);
        SetMeta("play_report_rule_receipts_json", metadata.RuleReceiptsJson);
        SetMeta("play_report_period_entries_json", metadata.PeriodEntriesJson);
    }

    private void RefreshPlayablePeriodReport()
    {
        if (_failed || _repository is null || _sceneView is not null ||
            _world is not { Phase: TurnPhase.Reporting, RulesetId: PlayableSkeleton.RulesetId } world ||
            _playerForceId is not int player || !world.Forces.ContainsKey(player))
        {
            ClosePlayablePeriodReport();
            return;
        }
        string status = _ui.Format("play_reporting", world.Forces[player].Name,
            _ui.DateLine(world.Date.Year, world.Date.Month, world.Date.Day), world.PeriodNumber);
        if (_periodReportView is { } view)
        {
            view.Refresh(_ui, status, _playReport.Text, world.PeriodNumber);
            return;
        }
        view = new PlayablePeriodReportView();
        AddChild(view);
        try
        {
            view.Configure(_repository, _ui, status, _playReport.Text, world.PeriodNumber,
                () => RunPlayAction(FinishPlayableReport));
            _periodReportView = view;
            if (_detailsView is { } details && details.GetParent() == this)
                MoveChild(details, GetChildCount() - 1);
            SetMeta("play_report_overlay_visible", true);
        }
        catch
        {
            RemoveChild(view);
            view.Free();
            throw;
        }
    }

    private void ClosePlayablePeriodReport()
    {
        if (_periodReportView is { } view)
        {
            _periodReportView = null;
            view.Hide();
            if (view.GetParent() == this) RemoveChild(view);
            view.QueueFree();
        }
        SetMeta("play_report_overlay_visible", false);
    }
}
