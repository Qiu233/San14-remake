using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class ManualTacticCommandPage(ManualTacticCommandView? view,GameplaySession session,
        MapSceneView map,GameState world,ArmyCampaignRuleAdapter rules,CampaignPeriodPlayback? playback,
        ManualTacticCommandPublication publication)
    {
        internal readonly ManualTacticCommandView? View=view;
        internal readonly GameplaySession Session=session;
        internal readonly MapSceneView Map=map;
        internal GameState World=world;
        internal readonly ArmyCampaignRuleAdapter Rules=rules;
        internal readonly CampaignPeriodPlayback? Playback=playback;
        internal ManualTacticCommandPublication Publication=publication;
        internal int? TacticSlot;
    }
    private ManualTacticCommandPage? _manualTacticCommand;
    private GameState? _manualTacticCommandWorld;
    private Button _nativeManualTacticEntry=null!;
    private bool HasManualTacticCommands=>_manualTacticCommand is not null;
    private bool CanOpenManualTacticCommands=>_manualTacticCommand is null&&!_failed&&!_shuttingDown&&
        !IsReturningToTitle&&!TutorialOverlayOwnsInput&&_repository is not null&&_campaignComposition is not null&&
        _residentMap is{IsPlayableMapReady:true}&&_session is{PlayerForceId:int player}&&
        _manualTacticCommandWorld is{Phase:TurnPhase.Strategy,NativeArmySimulation:not null} owner&&
        owner.Forces.GetValueOrDefault(player) is{IsActive:true}&&
        (_advancingPeriod?ReferenceEquals(_periodPlayback?.Candidate,owner):
            !_session.HasPendingOperation&&ReferenceEquals(_session.World,owner));

    private void RefreshManualTacticCommands(GameState? owner)
    {
        if(_manualTacticCommand is{ } page&&!IsManualTacticCommandCurrent(page))CloseManualTacticCommands();
        _manualTacticCommandWorld=owner;
        if(_nativeManualTacticEntry is not null)
        {
            _nativeManualTacticEntry.Visible=DeveloperWorkspaceEnabled&&owner?.NativeArmySimulation is not null&&!_advancingPeriod;
            _nativeManualTacticEntry.Disabled=!CanOpenManualTacticCommands;
        }
        RefreshPeriodPlaybackControls();
    }

    private void OpenManualTacticCommands(int? selectedUnitId=null,int? selectedTacticSlot=null,bool showSelectionPage=true)
    {
        if(!CanOpenManualTacticCommands||_manualTacticCommandWorld is not{ } world||
            _session is not{PlayerForceId:int player} session||_residentMap is not{ } map)return;
        if(HasMilitaryOrderPage||HasAdministrationPage||HasCampaignSavePage||HasMapSystemMenu||HasActionReport||HasIncomeReport||
            HasProposalsPage||HasPersonnelConfirmation||_detailsView is not null)return;
        CloseCommandWorkspace();CloseUnitRetarget();
        var playback=_advancingPeriod?_periodPlayback:null;
        if(playback is not null)
        {playback.ManualCommandSelectionOpen=true;map.SetPeriodPlaybackPaused(playback,true);}
        try
        {
            var rules=_campaignComposition!.PrepareArmyQueries(world);
            var publication=rules.QueryManualTacticCommands(world,player,selectedUnitId);
            var view=showSelectionPage?new ManualTacticCommandView():null;
            var page=new ManualTacticCommandPage(view,session,map,world,rules,playback,publication);
            _manualTacticCommand=page;
            if(view is not null)
            {
                AddChild(view);view.Configure(_repository!,_ui,_sourceHeaders,
                    unit=>RunUiAction(()=>SelectManualTacticCaster(page,unit), allowDuringPeriod: true),
                    slot=>RunUiAction(()=>SelectManualTactic(page,slot), allowDuringPeriod: true),()=>RunUiAction(RequestManualTacticBack, allowDuringPeriod: true));
                view.Apply(publication,null);
            }
            map.SetUnitSelectionPicker();map.SetDetailsOpen(false);SetMapFocus(true);
            BindManualTacticMap(page);
            if(selectedTacticSlot is int slot)SelectManualTactic(page,slot);
            RefreshManualTacticCommands(world);RefreshCommanderCardSelection();
        }
        catch
        {
            if(playback is not null)
            {playback.ManualCommandSelectionOpen=false;map.SetPeriodPlaybackPaused(playback,playback.IsPaused);}
            CloseManualTacticCommands();throw;
        }
    }

    private bool IsManualTacticCommandCurrent(ManualTacticCommandPage page)=>
        _manualTacticCommand==page&&_session==page.Session&&_residentMap==page.Map&&
        !_failed&&!_shuttingDown&&!IsReturningToTitle&&
        (page.Playback is{ } playback?ReferenceEquals(_periodPlayback,playback)&&
            ReferenceEquals(playback.Candidate,page.World):!_advancingPeriod&&ReferenceEquals(page.Session.World,page.World));

    private void SelectManualTacticCaster(ManualTacticCommandPage page,int unitId)
    {
        if(!IsManualTacticCommandCurrent(page)||
            !page.Publication.Casters.Any(caster=>caster.UnitId==unitId&&caster.Enabled==true))return;
        page.Publication=page.Rules.QueryManualTacticCommands(page.World,page.Publication.ForceId,unitId);
        page.TacticSlot=null;page.View?.Apply(page.Publication,null);BindManualTacticMap(page);
    }
    private void SelectManualTactic(ManualTacticCommandPage page,int slot)
    {
        if(!IsManualTacticCommandCurrent(page)||page.Publication.Tactics.FirstOrDefault(row=>row.Slot==slot)
            is not{Enabled:true,TargetHexIds:{Count:>0}})return;
        page.TacticSlot=slot;page.View?.Apply(page.Publication,slot);BindManualTacticMap(page);RefreshCommanderCardSelection();
    }
    private void BindManualTacticMap(ManualTacticCommandPage page)
    {
        var targets=page.Publication.Tactics.FirstOrDefault(row=>row.Slot==page.TacticSlot)?.TargetHexIds;
        var allowed=targets?.ToHashSet()??[];
        page.Map.SetCommandTargetPicker(hex=>RunUiAction(()=>SubmitManualTactic(page,hex), allowDuringPeriod: true),
            hex=>IsManualTacticCommandCurrent(page)&&!TutorialOverlayOwnsInput&&allowed.Contains(hex));
    }
    private void SubmitManualTactic(ManualTacticCommandPage page,int hex)
    {
        if(!IsManualTacticCommandCurrent(page)||TutorialOverlayOwnsInput||page.Publication.SelectedUnitId is not int unit||
            page.Publication.Tactics.FirstOrDefault(row=>row.Slot==page.TacticSlot) is not{Enabled:true} tactic||
            tactic.TargetHexIds?.Contains(hex)!=true)return;
        var order=new IssueManualTacticInterventionOrder(page.Publication.ForceId,unit,tactic.TacticId,hex);
        CommandResult? result;
        // 4464B0 includes the user's Progress pause flag. This ordinary
        // calendar checkpoint has no map child state, log pause or running
        // event coordinator: those use their separate owned continuations.
        // The command-selection lease is only a worker hold, not that flag.
        bool replacement=page.Playback?.IsPaused==true;
        if(page.Playback is{ } playback)
        {
            if(!playback.TryApplyManualTacticCommand(page.World,
                owner=>page.Session.ApplyBorrowedManualTacticCommand(owner,order,page.Rules,replacement),out result))return;
        }
        else result=page.Session.IssueManualTacticCommand(page.World,order,page.Rules,replacement);
        if(result is not{Accepted:true})
        {page.View?.ApplyNotice(result?.Error??_ui.Get("detail_unknown"));return;}
        ObserveTutorialOrderAccepted(order);
        CloseManualTacticCommands();
        _playMessage.Text=_ui.Get("play_order_accepted");
        if(page.Playback is not null){RefreshManualTacticCommands(page.Playback.Candidate);RefreshCommanderCards(page.Playback.Candidate);}
        else RefreshCurrentPlayWorld();
    }
    private void RequestManualTacticBack()
    {
        if(_manualTacticCommand is not{ } page){CancelPendingCommanderTargets();return;}
        if(page.View is not null&&page.TacticSlot.HasValue)
        {page.TacticSlot=null;page.View?.Apply(page.Publication,null);BindManualTacticMap(page);return;}
        CloseManualTacticCommands();
    }
    private void CloseManualTacticCommands()
    {
        if(_manualTacticCommand is not{ } page)return;
        _manualTacticCommand=null;
        if(page.Playback is not null)
        {
            page.Playback.ManualCommandSelectionOpen=false;
            if(GodotObject.IsInstanceValid(page.Map))
                page.Map.SetPeriodPlaybackPaused(page.Playback,page.Playback.IsPaused);
        }
        if(GodotObject.IsInstanceValid(page.Map))
        {page.Map.SetCommandTargetPicker(null);page.Map.SetUnitSelectionPicker();}
        if(page.View is{} view){view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        RefreshCommanderCardSelection();
        if(_nativeManualTacticEntry is not null)_nativeManualTacticEntry.Disabled=!CanOpenManualTacticCommands;
        RefreshPeriodPlaybackControls();
    }
}
