using Godot;
using San14.Core;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class FacilityRepairWorkspace(GameplaySession session,GameState world,MapSceneView map,
        FacilityRepairPresentationRequest request)
    {
        internal readonly GameplaySession Session=session;
        internal readonly GameState World=world;
        internal readonly MapSceneView Map=map;
        internal readonly FacilityRepairPresentationRequest Request=request;
        internal readonly CancellationTokenSource Lifetime=new();
        internal bool Running=true;
    }
    private FacilityRepairWorkspace? _facilityRepairPresentation;
    private bool HasFacilityRepairPresentation=>_facilityRepairPresentation?.Running==true;
    private bool IsFacilityRepairPresentationCurrent=>_facilityRepairPresentation is { } current&&
        ReferenceEquals(_session,current.Session)&&_residentMap==current.Map&&
        ReferenceEquals(PersonnelPresentationOwner(current.Session),current.World);

    private void RefreshFacilityRepairScene(GameplaySession session,GameState world,MapSceneView map)
    {
        if(!ReferenceEquals(_session,session)||_residentMap!=map||_campaignComposition is not { } composition)return;
        var frame=composition.PrepareArmyQueries(world).QueryFacilityRepairScene(world);
        map.BindFacilityRepairScene(world,frame,_sceneAudioBinding);
    }

    private bool TryPresentFacilityRepair(PersonnelPresentationEntry entry,GameplaySession session,GameState world,MapSceneView map)
    {
        var request=entry.Request.FacilityRepairPresentation!;
        if(_facilityRepairPresentation is { } prior&&IsFacilityRepairPresentationCurrent&&ReferenceEquals(prior.Request,request))
            return prior.Running;
        CloseFacilityRepairPresentation();
        var current=new FacilityRepairWorkspace(session,world,map,request);_facilityRepairPresentation=current;
        _=Dispatch();return true;
        async Task Dispatch()
        {
            try
            {
                var result=await map.DispatchFacilityRepairPresentationAsync(request,world,_sceneAudioBinding,current.Lifetime.Token);
                if(!ReferenceEquals(_facilityRepairPresentation,current)||!IsFacilityRepairPresentationCurrent)return;
                if(result.Accepted)
                {
                    if(session.TryCompleteFacilityRepairPresentation(world,entry.Source,entry.BatchSequence,entry.RequestIndex,request.Operation))
                        map.ForgetFacilityRepairPresentation(request);
                }
                else
                {
                    _personnelPresentationInspectedOwner=world;
                    GD.PushWarning("Facility repair presentation: "+result.UnresolvedInput);
                }
            }
            catch(OperationCanceledException) when(current.Lifetime.IsCancellationRequested) { }
            catch(Exception error)
            {
                if(ReferenceEquals(_facilityRepairPresentation,current)&&IsFacilityRepairPresentationCurrent)
                {_personnelPresentationInspectedOwner=world;GD.PushWarning("Facility repair presentation: "+error.Message);}
            }
            finally
            {
                current.Running=false;
                if(!ReferenceEquals(_facilityRepairPresentation,current))current.Lifetime.Dispose();
                else if(!_failed&&!_shuttingDown&&IsInsideTree())RunUiAction(RefreshPlayControls);
            }
        }
    }

    private void CloseFacilityRepairPresentation()
    {
        var current=_facilityRepairPresentation;_facilityRepairPresentation=null;
        if(current is null)return;
        current.Lifetime.Cancel();if(!current.Running)current.Lifetime.Dispose();
    }
}
