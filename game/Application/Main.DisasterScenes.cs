using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private EventIllustrationView? _disasterScene;
    private GameplaySession? _disasterSceneSession;
    private MapSceneView? _disasterSceneMap;

    private void RefreshDisasterScene(GameplaySession session, GameState owner, MapSceneView map)
    {
        if (owner.ActiveDisasterScene is { } active) ShowDisasterScene(session, map, active.EventKind);
        else CloseDisasterScene();
    }

    private void ShowDisasterScene(GameplaySession session, MapSceneView map, int eventKind)
    {
        if (_disasterScene?.EventKind == eventKind && ReferenceEquals(_disasterSceneSession, session) &&
            ReferenceEquals(_disasterSceneMap, map)) return;
        CloseDisasterScene();
        var view = new EventIllustrationView();
        _disasterScene = view; _disasterSceneSession = session; _disasterSceneMap = map;
        AddChild(view);
        try { view.Configure(_repository!, eventKind); }
        catch { CloseDisasterScene(); throw; }
    }

    private bool TryPresentDisasterScene(PersonnelPresentationEntry entry, GameplaySession session,
        GameState owner, MapSceneView map)
    {
        var request = entry.Request.MonthlyMaintenance!;
        if (request.Operation == MonthlyMaintenanceOperation.DisasterSceneEnter)
            ShowDisasterScene(session, map, request.EventKind ??
                throw new InvalidDataException("Disaster scene has no source event kind."));
        else if (request.Operation == MonthlyMaintenanceOperation.DisasterSceneExit) CloseDisasterScene();
        else throw new InvalidOperationException("Not a disaster scene boundary.");
        // The real layer has now accepted the image or retired it. Only this
        // receipt changes ActiveDisasterScene, allowing a save during the next
        // narration to reconstruct the same already displayed background.
        return session.TryCompletePersonnelPresentation(owner, entry.BatchSequence, entry.RequestIndex);
    }

    private void CloseDisasterScene()
    {
        var view = _disasterScene;
        _disasterScene = null; _disasterSceneSession = null; _disasterSceneMap = null;
        if (view is null) return;
        view.Hide(); view.GetParent()?.RemoveChild(view); view.QueueFree();
    }
}
