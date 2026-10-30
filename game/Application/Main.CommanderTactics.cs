using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed record CommanderCardRequest(CampaignPeriodPlayback Playback,GameplaySession Session,
        MapSceneView Map,int OfficerId,int? UnitId,int? TacticSlot,int? TacticId,bool Automatic);
    private readonly List<CommanderCardRequest> commanderCardRequests=[];

    private void RequestCommanderTactic(int slot,int tacticSlot)
    {
        if(!CommanderCardInputAdmitted()||!_advancingPeriod||_periodPlayback is not{} playback||
            _commanderCardSource?.Cards.FirstOrDefault(card=>card.Slot==slot) is not{OfficerId:int officer,UnitId:int unit} card||
            card.Tactics.FirstOrDefault(row=>row.Slot==tacticSlot) is not{TacticId:int tactic,Enabled:true})return;
        QueueCommanderCardRequest(new(playback,_session!,_residentMap!,officer,unit,tacticSlot,tactic,false));
    }

    private void RequestCommanderAutomatic(int slot)
    {
        if(!CommanderCardInputAdmitted()||_commanderCardSource?.Cards.FirstOrDefault(card=>card.Slot==slot)
            is not{OfficerId:int officer,AutomaticEnabled:not null})return;
        if(_advancingPeriod&&_periodPlayback is{} playback)
            QueueCommanderCardRequest(new(playback,_session!,_residentMap!,officer,null,null,null,true));
        else if(_session?.PlayerForceId is int force)
            RunPlayAction(()=>ApplyPlayableOrder(new ToggleCommanderAutomaticTacticsOrder(force,officer)));
    }

    private void QueueCommanderCardRequest(CommanderCardRequest request)
    {
        // A click between checkpoints waits for the next genuine borrow. No
        // stale displayed world is handed to the worker or changed in place.
        commanderCardRequests.Add(request);request.Playback.ManualCommandSelectionOpen=true;
        request.Map.SetPeriodPlaybackPaused(request.Playback,true);
        if(request.Playback.Candidate is not null)FlushCommanderCardRequests(request.Playback);
    }

    private void FlushCommanderCardRequests(CampaignPeriodPlayback owner)
    {
        if(commanderCardRequests.Count==0)return;
        var requests=commanderCardRequests.ToArray();commanderCardRequests.Clear();
        try
        {
            foreach(var request in requests)
            {
                if(request.Playback!=owner||_periodPlayback!=owner||_session!=request.Session||_residentMap!=request.Map||
                    owner.Candidate is not{} world||_failed||_shuttingDown)continue;
                RefreshCommanderCards(world);
                var card=_commanderCardSource?.Cards.FirstOrDefault(row=>row.OfficerId==request.OfficerId);
                if(card is null)continue;
                if(request.Automatic)ApplyBorrowedCommanderAutomatic(owner,world,request.OfficerId);
                else if(card.UnitId==request.UnitId&&request.TacticSlot is int slot&&
                    card.Tactics.FirstOrDefault(row=>row.Slot==slot) is{Enabled:true} tactic&&tactic.TacticId==request.TacticId)
                {
                    if(_manualTacticCommand is{} selected&&selected.Playback==owner&&
                        selected.Publication.SelectedUnitId==card.UnitId&&selected.TacticSlot==slot)
                        CloseManualTacticCommands();
                    else
                    {
                        CloseManualTacticCommands();
                        OpenManualTacticCommands(card.UnitId,slot,showSelectionPage:false);
                    }
                }
            }
        }
        finally
        {
            if(_manualTacticCommand?.Playback!=owner)owner.ManualCommandSelectionOpen=false;
            if(GodotObject.IsInstanceValid(_residentMap))
                _residentMap!.SetPeriodPlaybackPaused(owner,owner.IsPaused||owner.ManualCommandSelectionOpen);
            RefreshCommanderCards();
        }
    }

    private void ApplyBorrowedCommanderAutomatic(CampaignPeriodPlayback owner,GameState world,int officer)
    {
        if(_session is not{PlayerForceId:int player} session||_campaignComposition is null)return;
        var rules=_campaignComposition.PrepareArmyQueries(world);
        var order=new ToggleCommanderAutomaticTacticsOrder(player,officer);
        if(!owner.TryApplyManualTacticCommand(world,
            candidate=>session.ApplyBorrowedCommanderAutomaticTactics(candidate,order,rules),out var result))return;
        if(result is not{Accepted:true})
        {_playMessage.Text=result?.Error??_ui.Get("detail_unknown");return;}
        ObserveTutorialOrderAccepted(order);
        var current=owner.Candidate!;_manualTacticCommandWorld=current;
        if(_manualTacticCommand is{} page&&page.Playback==owner&&ReferenceEquals(page.World,world))
        {
            page.World=current;page.Publication=page.Rules.QueryManualTacticCommands(current,player,page.Publication.SelectedUnitId);
            page.View?.Apply(page.Publication,page.TacticSlot);BindManualTacticMap(page);
        }
        RefreshCommanderCards(current);
    }

    private void RemoveCommanderCard(int slot)
    {
        if(!CommanderCardInputAdmitted()||_advancingPeriod||_session?.PlayerForceId is not int player||
            _commanderCardSource?.Cards.FirstOrDefault(card=>card.Slot==slot) is not{CanRemove:true,OfficerId:int officer})return;
        int[] retained=_commanderCardSource.Cards.Where(card=>card.OfficerId is not null&&card.OfficerId!=officer)
            .Select(card=>card.OfficerId!.Value).ToArray();
        RunPlayAction(()=>ApplyPlayableOrder(new SetCommanderSelectionOrder(player,retained)));
    }

    private bool CancelPendingCommanderTargets()
    {
        if(!commanderCardRequests.Any(request=>!request.Automatic))return false;
        var owners=commanderCardRequests.Where(request=>!request.Automatic).Select(request=>request.Playback).Distinct().ToArray();
        commanderCardRequests.RemoveAll(request=>!request.Automatic);
        foreach(var owner in owners)
        {
            if(_manualTacticCommand?.Playback!=owner&&!commanderCardRequests.Any(request=>request.Playback==owner))
                owner.ManualCommandSelectionOpen=false;
            RefreshCommanderPlaybackPause(owner);
        }
        RefreshCommanderCardSelection();return true;
    }

    private void RefreshCommanderPlaybackPause(CampaignPeriodPlayback owner)
    {
        if(_periodPlayback==owner&&_residentMap is{} map&&GodotObject.IsInstanceValid(map))
            map.SetPeriodPlaybackPaused(owner,owner.IsPaused||owner.ManualCommandSelectionOpen);
    }

    private void RetireCommanderCardRequests()
    {
        foreach(var owner in commanderCardRequests.Select(request=>request.Playback).Distinct())
        {
            if(_manualTacticCommand?.Playback!=owner)owner.ManualCommandSelectionOpen=false;
            RefreshCommanderPlaybackPause(owner);
        }
        commanderCardRequests.Clear();
    }
}
