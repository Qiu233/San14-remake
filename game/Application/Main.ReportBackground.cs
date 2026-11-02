using Godot;
using San14.Client.Gameplay;

public partial class Main
{
    private ReportSequenceBackgroundView? _reportBackground;
    private GameplaySession? _reportBackgroundSession;
    private CampaignApplicationState? _reportBackgroundApplication;
    private MapSceneView? _reportBackgroundMap;
    private Action? _reportBackgroundMapExited;

    // The outer report sequence owns its background independently of either
    // dialog. Hiding a dialog for details does not retire this shared picture.
    private void EnsureReportBackground(GameplaySession session, CampaignApplicationState application,
        MapSceneView? map, Control content)
    {
        if (_reportBackground is { } retained &&
            (!GodotObject.IsInstanceValid(retained) || retained.IsQueuedForDeletion() ||
             !ReferenceEquals(_reportBackgroundSession, session) ||
             !ReferenceEquals(_reportBackgroundApplication, application) || _reportBackgroundMap != map))
            CloseReportBackground();
        if (_reportBackground is null)
        {
            var view = new ReportSequenceBackgroundView();
            _reportBackground = view; _reportBackgroundSession = session;
            _reportBackgroundApplication = application; _reportBackgroundMap = map;
            AddChild(view);
            view.TreeExiting += () =>
            {
                if (_reportBackground == view) ForgetReportBackground();
            };
            if (map is not null)
            {
                // The map's parent is inside RemoveChild while this signal
                // runs. Retire now, then let QueueFree remove the sibling.
                _reportBackgroundMapExited = () => CloseReportBackground(deferRemoval: true);
                map.TreeExiting += _reportBackgroundMapExited;
            }
            try { view.Configure(_repository!, TryBorrowApplicationUiResources(view)); }
            catch { CloseReportBackground(); throw; }
        }
        // A retained background may already precede this dialog. Account for
        // removal before calculating its final sibling position.
        int index = content.GetIndex();
        if (_reportBackground.GetIndex() < index) index--;
        if (_reportBackground.GetIndex() != index) MoveChild(_reportBackground, index);
        SetReportBackgroundMapSuspended(_reportMapReturn is not null);
    }

    private void SetReportBackgroundMapSuspended(bool suspended)
    {
        if (_reportBackground is { } view && GodotObject.IsInstanceValid(view) && !view.IsQueuedForDeletion())
        {
            _reportBackgroundMap?.SetReportMapOverlaysSuppressed(view, !suspended);
            view.Visible = !suspended;
        }
    }

    private void ForgetReportBackground()
    {
        var map = _reportBackgroundMap; var exited = _reportBackgroundMapExited;
        if (map is not null && _reportBackground is { } owner && GodotObject.IsInstanceValid(map))
            map.SetReportMapOverlaysSuppressed(owner, false);
        _reportBackground = null; _reportBackgroundSession = null;
        _reportBackgroundApplication = null; _reportBackgroundMap = null; _reportBackgroundMapExited = null;
        if (map is not null && exited is not null && GodotObject.IsInstanceValid(map))
            map.TreeExiting -= exited;
    }

    private void CloseReportBackground(bool deferRemoval = false)
    {
        var view = _reportBackground;
        ForgetReportBackground();
        if (view is null || !GodotObject.IsInstanceValid(view)) return;
        view.Retire();
        if (!deferRemoval && view.GetParent() is Node parent && !parent.IsQueuedForDeletion()) parent.RemoveChild(view);
        if (!view.IsQueuedForDeletion()) view.QueueFree();
    }
}
