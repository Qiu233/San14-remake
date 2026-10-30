using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class FirePresentationWorkspace(GameplaySession session, GameState world,
        MapSceneView map, CampaignEffectRequest request)
    {
        internal readonly GameplaySession Session = session;
        internal readonly GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly CampaignEffectRequest Request = request;
        internal readonly CancellationTokenSource Lifetime = new();
        internal bool Running = true;
    }
    private FirePresentationWorkspace? _firePresentation;
    private bool HasFirePresentation => _firePresentation?.Running == true;
    private bool IsFirePresentationCurrent => _firePresentation is { } current &&
        ReferenceEquals(_session, current.Session) && _residentMap == current.Map &&
        ReferenceEquals(PersonnelPresentationOwner(current.Session), current.World);

    private bool TryPresentFire(PersonnelPresentationEntry entry, GameplaySession session,
        GameState world, MapSceneView map)
    {
        if (_firePresentation is { } prior && IsFirePresentationCurrent &&
            ReferenceEquals(prior.Request, entry.Request)) return prior.Running;
        CloseFirePresentation();
        var current = new FirePresentationWorkspace(session, world, map, entry.Request);
        _firePresentation = current; _ = Dispatch(); return true;

        async Task Dispatch()
        {
            try
            {
                var result = await map.DispatchFirePresentationAsync(entry.Request,
                    _sceneAudioBinding, current.Lifetime.Token);
                if (!ReferenceEquals(_firePresentation, current) || !IsFirePresentationCurrent) return;
                if (result.Accepted)
                {
                    if (session.TryCompleteFirePresentation(world, entry.Source,
                        entry.BatchSequence, entry.RequestIndex, entry.Request)) map.ForgetFirePresentation(entry.Request);
                }
                else
                {
                    _personnelPresentationInspectedOwner = world;
                    GD.PushWarning("Fire presentation: " + result.UnresolvedInput);
                }
            }
            catch (OperationCanceledException) when (current.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (ReferenceEquals(_firePresentation, current) && IsFirePresentationCurrent)
                { _personnelPresentationInspectedOwner = world; GD.PushWarning("Fire presentation: " + error.Message); }
            }
            finally
            {
                current.Running = false;
                if (!ReferenceEquals(_firePresentation, current)) current.Lifetime.Dispose();
                else if (!_failed && !_shuttingDown && IsInsideTree()) RunUiAction(RefreshPlayControls);
            }
        }
    }

    private void CloseFirePresentation()
    {
        var current = _firePresentation; _firePresentation = null;
        if (current is null) return;
        current.Lifetime.Cancel(); if (!current.Running) current.Lifetime.Dispose();
    }
}
