using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class DamagePresentationWorkspace(GameplaySession session, GameState world,
        MapSceneView map, CampaignEffectRequest request)
    {
        internal readonly GameplaySession Session = session;
        internal readonly GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly CampaignEffectRequest Request = request;
        internal readonly CancellationTokenSource Lifetime = new();
        internal bool Running = true;
    }
    private DamagePresentationWorkspace? _damagePresentation;
    private bool HasDamagePresentation => _damagePresentation?.Running == true;
    private bool IsDamagePresentationCurrent => _damagePresentation is { } current &&
        ReferenceEquals(_session, current.Session) && _residentMap == current.Map &&
        ReferenceEquals(PersonnelPresentationOwner(current.Session), current.World);

    private bool TryPresentDamage(PersonnelPresentationEntry entry, GameplaySession session,
        GameState world, MapSceneView map)
    {
        if (_damagePresentation is { } prior && IsDamagePresentationCurrent &&
            ReferenceEquals(prior.Request, entry.Request)) return prior.Running;
        CloseDamagePresentation();
        var current = new DamagePresentationWorkspace(session, world, map, entry.Request);
        _damagePresentation = current; _ = Dispatch(); return true;

        async Task Dispatch()
        {
            try
            {
                var result = await map.DispatchDamagePresentationAsync(entry.Request,
                    _sceneAudioBinding, current.Lifetime.Token);
                if (!ReferenceEquals(_damagePresentation, current) || !IsDamagePresentationCurrent) return;
                if (result.Accepted)
                {
                    if (session.TryCompleteDamagePresentation(world, entry.Source,
                        entry.BatchSequence, entry.RequestIndex, entry.Request)) map.ForgetDamagePresentation(entry.Request);
                }
                else
                {
                    _personnelPresentationInspectedOwner = world;
                    GD.PushWarning("Damage presentation: " + result.UnresolvedInput);
                }
            }
            catch (OperationCanceledException) when (current.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (ReferenceEquals(_damagePresentation, current) && IsDamagePresentationCurrent)
                { _personnelPresentationInspectedOwner = world; GD.PushWarning("Damage presentation: " + error.Message); }
            }
            finally
            {
                current.Running = false;
                if (!ReferenceEquals(_damagePresentation, current)) current.Lifetime.Dispose();
                else if (!_failed && !_shuttingDown && IsInsideTree()) RunUiAction(RefreshPlayControls);
            }
        }
    }

    private void CloseDamagePresentation()
    {
        var current = _damagePresentation; _damagePresentation = null;
        if (current is null) return;
        current.Lifetime.Cancel(); if (!current.Running) current.Lifetime.Dispose();
    }
}
