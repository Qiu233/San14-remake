using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class CoalitionCameraCommand(GameplaySession session, GameState world, MapSceneView map)
    {
        internal readonly GameplaySession Session = session;
        internal GameState World = world;
        internal readonly MapSceneView Map = map;
    }
    private CoalitionCameraCommand? _coalitionCameraCommand;
    private MapSceneView? _restoredCoalitionMap;
    private CoalitionCameraPresentationState? _restoredCoalitionCamera;
    private bool IsCoalitionCameraCurrent => _coalitionCameraCommand is { } command &&
        ReferenceEquals(_session, command.Session) && ReferenceEquals(_residentMap, command.Map) &&
        ReferenceEquals(PersonnelPresentationOwner(command.Session), command.World);

    private ArmyCampaignRuleAdapter CoalitionPresentationQueries(GameState owner, MapSceneView map)
    {
        var rules = _campaignComposition!.PrepareArmyQueries(owner);
        if (map.SourceMainCameraTerrain is { } terrain) rules.ConfigureStoryCameraTerrain(terrain);
        return rules;
    }

    private void RestoreCoalitionCamera(GameState world, MapSceneView map)
    {
        if (_coalitionCameraCommand is not null) return;
        var saved = world.CoalitionCameraPresentation;
        if (ReferenceEquals(_restoredCoalitionMap, map) && ReferenceEquals(_restoredCoalitionCamera, saved)) return;
        if (saved is not null)
        {
            var query = CoalitionPresentationQueries(world, map).QueryForceCoalitionCameraTarget(world, saved.TargetHexId);
            var target = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
            var owner = new object();
            map.BeginStoryPresentation(owner);
            try
            {
                if (!map.MoveStoryCamera(owner, target, 0))
                    throw new InvalidOperationException("coalition.camera.restore_focus_owner");
            }
            finally { map.EndStoryPresentation(owner); }
        }
        _restoredCoalitionMap = map; _restoredCoalitionCamera = saved;
    }

    private bool TryPresentCoalitionVisual(PersonnelPresentationEntry entry, ForceCoalitionVisualRequest request,
        GameplaySession session, GameState world, MapSceneView map)
    {
        if (request.Operation == ForceCoalitionVisualOperation.CityProvinceAdvantageIndicatorsRefresh)
        {
            // This is the city's province-advantage attention, independent
            // of the diplomacy information cards.
            GD.PushWarning("coalition.city_province_advantage_indicator_owner");
            _personnelPresentationInspectedOwner = world;
            return false;
        }
        var query = CoalitionPresentationQueries(world, map).QueryForceCoalitionIllustration(world, request);
        if (!query.Available) throw new InvalidDataException(query.UnresolvedInput);
        if (query.Value is { } illustration) ShowMonthlyScene(session, map, illustration);
        else CloseMonthlyScene();
        return session.TryCompleteForceCoalitionVisual(world, entry.Source, entry.BatchSequence,
            entry.RequestIndex, request.Operation);
    }

    private bool TryPresentEmperorScene(PersonnelPresentationEntry entry, GameplaySession session,
        GameState world, MapSceneView map)
    {
        var request = entry.Request.MonthlyMaintenance!;
        var query = CoalitionPresentationQueries(world, map).QueryEmperorScene(world, request);
        var publication = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
        if (publication.Illustration is { } illustration) ShowMonthlyScene(session, map, illustration);
        else CloseMonthlyScene();
        return session.TryCompleteEmperorScene(world, entry.Source, entry.BatchSequence,
            entry.RequestIndex, request.Operation);
    }

    private bool TryPresentCoalitionCamera(PersonnelPresentationEntry entry, ForceCoalitionVisualRequest request,
        GameplaySession session, GameState world, MapSceneView map)
    {
        var command = new CoalitionCameraCommand(session, world, map);
        _coalitionCameraCommand = command;
        RetirePeriodReportView(); CloseActionReport(); map.CloseCityCommandMenu();
        map.BeginStoryPresentation(command); SetMapFocus(false); HideDeploymentHud();
        _ = PresentAsync();
        return true;

        async Task PresentAsync()
        {
            try
            {
                // Wait only for the real controller; rendering frames do not
                // manufacture a camera-completion receipt.
                while (IsCoalitionCameraCurrent && !map.IsStoryCameraIdle(command))
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!IsCoalitionCameraCurrent) return;
                CampaignCameraSnapshot? captured = null;
                bool accepted;
                if (request.Operation == ForceCoalitionVisualOperation.CameraFocus)
                {
                    captured = map.CaptureStoryCamera() ?? throw new InvalidOperationException("coalition.camera.current_backup");
                    var query = CoalitionPresentationQueries(world, map).QueryForceCoalitionCameraTarget(world,
                        request.TargetHexId ?? throw new InvalidDataException("coalition.camera.target_hex"));
                    var target = query.Value ?? throw new InvalidDataException(query.UnresolvedInput);
                    accepted = map.MoveStoryCamera(command, target, 1000);
                }
                else
                {
                    var saved = world.CoalitionCameraPresentation ?? throw new InvalidOperationException("coalition.camera.saved_backup");
                    accepted = map.RestoreStoryCamera(command, saved.SavedCamera);
                }
                if (!accepted) throw new InvalidOperationException("coalition.camera.request_owner");
                // The real controller accepted its source command. Persist
                // its backup now, then retain the modal until motion ends.
                if (!session.TryCompleteForceCoalitionVisual(world, entry.Source, entry.BatchSequence,
                        entry.RequestIndex, request.Operation, captured))
                    throw new InvalidOperationException("coalition.camera.source_head");
                command.World = PersonnelPresentationOwner(session);
                _restoredCoalitionMap = map; _restoredCoalitionCamera = command.World.CoalitionCameraPresentation;
                while (IsCoalitionCameraCurrent && !map.IsStoryCameraIdle(command))
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                if (!IsCoalitionCameraCurrent) return;
                CloseCoalitionCameraCommand();
                if (!TryOpenPersonnelPresentation()) { RefreshPlayControls(); RefreshDiplomaticNegotiation(); }
            }
            catch (Exception error)
            {
                if (_coalitionCameraCommand == command && IsCoalitionCameraCurrent)
                { CloseCoalitionCameraCommand(); ReportPlayFailure(error); }
            }
        }
    }

    private void CloseCoalitionCameraCommand()
    {
        var command = _coalitionCameraCommand; _coalitionCameraCommand = null;
        if (command is not null && GodotObject.IsInstanceValid(command.Map)) command.Map.EndStoryPresentation(command);
    }
}
