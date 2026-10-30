using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class FacilityEffectsWorkspace(GameplaySession session, GameState world,
        MapSceneView map, CampaignEffectRequest request)
    {
        internal readonly GameplaySession Session = session;
        internal readonly GameState World = world;
        internal readonly MapSceneView Map = map;
        internal readonly CampaignEffectRequest Request = request;
        internal readonly CancellationTokenSource Lifetime = new();
        internal bool Running = true;
    }
    private FacilityEffectsWorkspace? _facilityEffectsPresentation;
    private bool HasFacilityEffectsPresentation => _facilityEffectsPresentation?.Running == true;
    private bool IsFacilityEffectsPresentationCurrent => _facilityEffectsPresentation is { } current &&
        ReferenceEquals(_session, current.Session) && _residentMap == current.Map &&
        ReferenceEquals(PersonnelPresentationOwner(current.Session), current.World);

    private bool TryPresentFacilityEffects(PersonnelPresentationEntry entry, GameplaySession session,
        GameState world, MapSceneView map)
    {
        if (_facilityEffectsPresentation is { } prior && IsFacilityEffectsPresentationCurrent &&
            ReferenceEquals(prior.Request, entry.Request)) return prior.Running;
        CloseFacilityEffectsPresentation();
        var current = new FacilityEffectsWorkspace(session, world, map, entry.Request);
        _facilityEffectsPresentation = current;
        _ = Dispatch();
        return true;

        async Task Dispatch()
        {
            try
            {
                var result = await map.DispatchFacilityEffectsPresentationAsync(entry.Request,
                    _sceneAudioBinding, current.Lifetime.Token);
                if (!ReferenceEquals(_facilityEffectsPresentation, current) || !IsFacilityEffectsPresentationCurrent) return;
                if (result.Accepted)
                {
                    if (session.TryCompleteFacilityEffectsPresentation(world, entry.Source,
                        entry.BatchSequence, entry.RequestIndex, entry.Request))
                        map.ForgetFacilityEffectsPresentation(entry.Request);
                }
                else
                {
                    _personnelPresentationInspectedOwner = world;
                    GD.PushWarning("Facility effects presentation: " + result.UnresolvedInput);
                }
            }
            catch (OperationCanceledException) when (current.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (ReferenceEquals(_facilityEffectsPresentation, current) && IsFacilityEffectsPresentationCurrent)
                { _personnelPresentationInspectedOwner = world; GD.PushWarning("Facility effects presentation: " + error.Message); }
            }
            finally
            {
                current.Running = false;
                if (!ReferenceEquals(_facilityEffectsPresentation, current)) current.Lifetime.Dispose();
                else if (!_failed && !_shuttingDown && IsInsideTree()) RunUiAction(RefreshPlayControls);
            }
        }
    }

    private void CloseFacilityEffectsPresentation()
    {
        var current = _facilityEffectsPresentation; _facilityEffectsPresentation = null;
        if (current is null) return;
        current.Lifetime.Cancel();
        if (!current.Running) current.Lifetime.Dispose();
    }
}
