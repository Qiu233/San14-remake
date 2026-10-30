using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class FacilityConstructionWorkspace(GameplaySession session,GameState world,MapSceneView map,
        FacilityConstructionPresentationRequest request)
    {
        internal readonly GameplaySession Session=session;
        internal readonly GameState World=world;
        internal readonly MapSceneView Map=map;
        internal readonly FacilityConstructionPresentationRequest Request=request;
        internal readonly CancellationTokenSource Lifetime=new();
        internal bool Running=true;
    }
    private FacilityConstructionWorkspace? _facilityConstructionPresentation;
    private bool HasFacilityConstructionPresentation=>_facilityConstructionPresentation?.Running==true;
    private bool IsFacilityConstructionPresentationCurrent=>_facilityConstructionPresentation is { } current&&
        ReferenceEquals(_session,current.Session)&&_residentMap==current.Map&&
        ReferenceEquals(PersonnelPresentationOwner(current.Session),current.World);

    private bool TryPresentFacilityConstruction(PersonnelPresentationEntry entry,GameplaySession session,GameState world,MapSceneView map)
    {
        var request=entry.Request.FacilityConstructionPresentation!;
        if (_facilityConstructionPresentation is { } prior&&IsFacilityConstructionPresentationCurrent&&ReferenceEquals(prior.Request,request))
            return prior.Running;
        CloseFacilityConstructionPresentation();
        var current=new FacilityConstructionWorkspace(session,world,map,request);
        _facilityConstructionPresentation=current;
        _=Dispatch();
        return true;

        async Task Dispatch()
        {
            try
            {
                var result=await map.DispatchFacilityConstructionPresentationAsync(request,world,_sceneAudioBinding,current.Lifetime.Token);
                if (!ReferenceEquals(_facilityConstructionPresentation,current)||!IsFacilityConstructionPresentationCurrent) return;
                if (result.Accepted)
                {
                    if (session.TryCompleteFacilityConstructionPresentation(world,entry.Source,entry.BatchSequence,entry.RequestIndex,request.Operation))
                        map.ForgetFacilityConstructionPresentation(request);
                }
                else
                {
                    _personnelPresentationInspectedOwner=world;
                    GD.PushWarning("Facility construction presentation: "+result.UnresolvedInput);
                }
            }
            catch (OperationCanceledException) when(current.Lifetime.IsCancellationRequested) { }
            catch (Exception error)
            {
                if (ReferenceEquals(_facilityConstructionPresentation,current)&&IsFacilityConstructionPresentationCurrent)
                { _personnelPresentationInspectedOwner=world; GD.PushWarning("Facility construction presentation: "+error.Message); }
            }
            finally
            {
                current.Running=false;
                if (!ReferenceEquals(_facilityConstructionPresentation,current)) current.Lifetime.Dispose();
                else if (!_failed&&!_shuttingDown&&IsInsideTree()) RunUiAction(RefreshPlayControls);
            }
        }
    }

    private void CloseFacilityConstructionPresentation()
    {
        var current=_facilityConstructionPresentation;_facilityConstructionPresentation=null;
        if (current is null) return;
        current.Lifetime.Cancel();
        if (!current.Running) current.Lifetime.Dispose();
    }
}
