using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class DuelWorkspace(GameplaySession session,GameState world,MapSceneView map,
        CampaignComposition composition,CampaignRegisteredDuel request,CampaignPeriodPlayback? playback)
    {
        internal readonly GameplaySession Session=session;
        internal readonly GameState World=world;
        internal readonly MapSceneView Map=map;
        internal readonly CampaignComposition Composition=composition;
        internal readonly CampaignRegisteredDuel Request=request;
        internal readonly CampaignPeriodPlayback? Playback=playback;
        internal GameState PreparedState=world;
        internal DuelPresentationView? View;
    }
    private DuelWorkspace? _duelPresentation;
    private GameState? _duelPresentationUnavailableOwner;
    private bool HasDuelPresentation=>_duelPresentation is not null;
    private bool IsDuelPresentationCurrent=>_duelPresentation is{} owner&&CurrentDuel(owner);
    private bool CurrentDuel(DuelWorkspace owner)=>ReferenceEquals(_session,owner.Session)&&_residentMap==owner.Map&&
        (owner.Playback is{} playback?ReferenceEquals(_periodPlayback,playback)&&ReferenceEquals(playback.Candidate,owner.World):
            ReferenceEquals(PersonnelPresentationOwner(owner.Session),owner.World));

    private bool TryOpenDuelPresentation(GameplaySession session,GameState world,MapSceneView map,CampaignComposition composition)
    {
        if(ReferenceEquals(_duelPresentationUnavailableOwner,world))return false;
        if(world.ManualTacticIntervention is{} tactics&&(tactics.Effects.Length!=0||tactics.Directions.Length!=0))return false;
        if(CampaignDuelPresentationPlaybackRules.Pending(world) is not{} request)return false;
        var page=composition.PrepareArmyQueries(world).QueryDuelPresentation(world,request);
        if(!page.Available){GD.PushWarning(page.UnresolvedInput);_duelPresentationUnavailableOwner=world;return false;}
        OpenDuel(new(session,world,map,composition,request,null),page.Value!);
        return true;
    }

    private void OpenDuel(DuelWorkspace owner,DuelPresentationPage page)
    {
        if(_duelPresentation is not null)throw new InvalidOperationException("duel.presentation.concurrent_host");
        var view=new DuelPresentationView();owner.View=view;owner.PreparedState=page.PreparedState;_duelPresentation=owner;
        owner.Map.CloseCityCommandMenu();owner.Map.SetDetailsOpen(true,keepMapVisible:true);SetMapFocus(false);HideDeploymentHud();
        AddChild(view);
        try
        {
            view.Configure(_repository!,_ui,page,()=>RunUiAction(()=>CompleteDuel(owner)),
                ()=>_duelPresentation==owner&&CurrentDuel(owner)&&!_failed&&!_shuttingDown&&!IsReturningToTitle&&
                    !HasEnvironmentSettings&&!HasCampaignSavePage&&!HasMapSystemMenu);
        }
        catch{CloseDuelPresentation();throw;}
    }

    private void CompleteDuel(DuelWorkspace owner)
    {
        if(_duelPresentation!=owner)return;
        if(!CurrentDuel(owner)||_failed||_shuttingDown||IsReturningToTitle){CloseDuelPresentation();return;}
        try
        {
            var prepared=owner.Composition.PrepareArmyQueries(owner.PreparedState).PrepareDuelPresentationCompletion(owner.PreparedState,owner.Request);
            if(!prepared.Available)throw new InvalidDataException(prepared.UnresolvedInput);
            bool accepted=owner.Playback is{} playback
                ?playback.TryCompleteDuelPresentation(owner.World,owner.Request.Sequence,prepared.Value!)
                :owner.Session.TryCompleteDuelPresentation(owner.World,owner.Request.Sequence,prepared.Value!);
            if(!accepted)throw new InvalidOperationException("duel.presentation.completion_owner");
            RetireDuel(owner);
            if(owner.Playback is{} active)
            {
                var next=active.Candidate!;owner.Map.PublishPeriodPlayback(active,next);
                OpenNextPeriodDuel(active,owner.Map,next,owner.Composition);
            }
            else
            {
                var next=PersonnelPresentationOwner(owner.Session);
                SynchronizeApplicationCrtRandom(next);
                if(next.CampaignAppRandom32 is{} random)_presentationRandom.InstallCommittedState(random.State);
                RefreshArmyMapSpeechBinding(owner.Session,next,owner.Map);
                if(!TryOpenPersonnelPresentation()){RefreshPlayControls();RefreshDiplomaticNegotiation();}
            }
        }
        catch(Exception error)
        {
            if(owner.Playback is{} playback)playback.FailPresentation(error);
            else{GD.PushWarning(error.Message);_duelPresentationUnavailableOwner=owner.World;}
            RetireDuel(owner);
        }
    }

    private void BeginPeriodDuelPresentation(CampaignPeriodPlayback playback,MapSceneView? map,GameState candidate)
    {
        if(playback.DuelPresentationOpen||map is null||_campaignComposition is not{} composition||
            CampaignDuelPresentationPlaybackRules.Pending(candidate) is null)return;
        playback.DuelPresentationOpen=true;
        var token=_periodAdvanceCancellation?.Token??CancellationToken.None;
        _=WaitForOtherPresentation();
        async Task WaitForOtherPresentation()
        {
            try
            {
                //Both consumers borrow the same private candidate. Start only
                //after the earlier occupation/selection consumer publishes it.
                while(playback.OccupationPresentationOpen||playback.ManualCommandSelectionOpen||
                    playback.Candidate?.ManualTacticIntervention is{} tactics&&(tactics.Effects.Length!=0||tactics.Directions.Length!=0))
                {
                    if(ordinaryTacticFailures.Count!=0)throw new InvalidDataException("duel.presentation.preceding_tactic_failed");
                    await Task.Delay(16,token);
                }
                if(ReferenceEquals(_periodPlayback,playback)&&playback.Candidate is{} world&&_residentMap==map)
                    OpenNextPeriodDuel(playback,map,world,composition);
                else playback.DuelPresentationOpen=false;
            }
            catch(OperationCanceledException)when(token.IsCancellationRequested){playback.DuelPresentationOpen=false;}
            catch(Exception error){playback.FailPresentation(error);playback.DuelPresentationOpen=false;}
        }
    }
    private void OpenNextPeriodDuel(CampaignPeriodPlayback playback,MapSceneView map,GameState world,CampaignComposition composition)
    {
        if(CampaignDuelPresentationPlaybackRules.Pending(world) is not{} request){playback.DuelPresentationOpen=false;return;}
        var page=composition.PrepareArmyQueries(world).QueryDuelPresentation(world,request);
        if(!page.Available)throw new InvalidDataException(page.UnresolvedInput);
        OpenDuel(new(_session!,world,map,composition,request,playback),page.Value!);
    }

    private void RetireDuel(DuelWorkspace owner)
    {
        if(_duelPresentation!=owner)return;_duelPresentation=null;
        if(owner.View is{} view){view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        if(GodotObject.IsInstanceValid(owner.Map)&&_residentMap==owner.Map)
            owner.Map.SetDetailsOpen(false);
    }
    private void CloseDuelPresentation()
    {
        if(_duelPresentation is not{} owner)return;
        //An abandoned view does not dequeue its pending result. Cancellation
        //or campaign replacement owns release of the worker's borrow.
        RetireDuel(owner);
        if(owner.Playback is{} playback)
        {playback.DuelPresentationOpen=false;if(ReferenceEquals(_periodPlayback,playback))playback.FailPresentation(new OperationCanceledException("duel.presentation.abandoned"));}
    }
    private void RetirePeriodDuelPresentation(CampaignPeriodPlayback playback)
    {
        if(_duelPresentation is{} owner&&ReferenceEquals(owner.Playback,playback))RetireDuel(owner);
        playback.DuelPresentationOpen=false;
    }
}
