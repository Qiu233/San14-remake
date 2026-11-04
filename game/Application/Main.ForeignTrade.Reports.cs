using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private ForeignTradeReportView? _foreignTradeReport;
    private GameplaySession? _foreignTradeReportSession;
    private GameState? _foreignTradeReportWorld;
    private MapSceneView? _foreignTradeReportMap;
    private ForeignTradeReturnReportPublication? _foreignTradeReportPublication;
    private bool HasForeignTradeReport=>_foreignTradeReport is not null;
    private bool IsForeignTradeReportCurrent=>!_failed&&!_shuttingDown&&_foreignTradeReport is not null&&
        ReferenceEquals(_session,_foreignTradeReportSession)&&ReferenceEquals(_residentMap,_foreignTradeReportMap)&&
        ReferenceEquals(_foreignTradeReportSession?.PendingNormalPeriod?.Candidate??_foreignTradeReportSession?.World,_foreignTradeReportWorld);
    private long ForeignTradeReportOpenedSequence {get;set;}
    private long ForeignTradeReportAcceptedSequence {get;set;}
    private int? ForeignTradeLastReportedTaskId {get;set;}

    private bool TryOpenForeignTradeReport()
    {
        if(HasForeignTradeReport)
        {
            if(IsForeignTradeReportCurrent)return true;
            CloseForeignTradeReport();
        }
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||HasAdministrationPage||HasMilitaryOrderPage||
            HasCampaignSavePage||HasMapSystemMenu||HasPersonnelConfirmation||_repository is null||
            _campaignComposition is not { } composition||_residentMap is not { IsPlayableMapReady:true } map||
            _session is not { PlayerForceId:int player } session)return false;
        var owner=session.PendingNormalPeriod?.Candidate??session.World;
        int? pending=session.PendingNormalPeriod?.PendingCaravanReportTaskId;
        if(pending is null&&session.PendingNormalPeriod is null)
            pending=owner.PersonnelCommands?.Caravans?.Values.Where(task=>task.PendingReport is not null&&
                task.SourceForceId==player).OrderBy(task=>task.DispatchSlot).Select(task=>(int?)task.Id).FirstOrDefault();
        if(pending is not int taskId)return false;
        var rules=composition.PrepareArmyQueries(owner);
        //Prepare the actual random phrases once and retain them through every
        //message wait. Querying these steps never re-applies return rewards.
        var prepared=rules.QueryForeignTradeReturnReport(owner,taskId,_presentationRandom.Next);
        if(!prepared.Available||prepared.Value is not { } publication)
            throw new InvalidDataException(prepared.UnresolvedInput??"foreign-trade.report.publication");
        var view=new ForeignTradeReportView();_foreignTradeReport=view;_foreignTradeReportSession=session;
        _foreignTradeReportWorld=owner;_foreignTradeReportMap=map;_foreignTradeReportPublication=publication;
        AddChild(view);
        try
        {
            map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);SetMapFocus(false);HideDeploymentHud();
            view.KeyboardInput=input=>HandleNarrationKeyboard(input,view.Message);
            view.StepEntered=step=>
            {
                if(step.Kind==ForeignTradeReportStepKind.Reward)
                    _sceneAudioBinding?.PlayPersonnelPresentationAudio(new(TalentCommandPresentationOperation.SoundCue,5,null,null,null,null),
                        null,_preferences.Voice.Language);
            };
            var resources=TryBorrowApplicationUiResources(view);
            view.Configure(_repository,_ui,publication,()=>IsForeignTradeReportCurrent&&_foreignTradeReport==view&&!TutorialOverlayOwnsInput,
                ()=>_preferences.AutomaticMessageAdvance,step=>
                {
                    var current=_foreignTradeReportWorld!;
                    var query=composition.PrepareArmyQueries(current).QueryForeignTradeRewardPanel(current,step);
                    if(!query.Available||query.Value is not { } panel)
                        throw new InvalidDataException(query.UnresolvedInput??"foreign-trade.report.reward_panel");
                    // The report owns the application lease. Its reward is
                    // configured before adoption as a child, so it borrows that
                    // same bundle instead of registering an off-tree lease.
                    var reward=new ForeignTradeRewardView();
                    try {reward.Configure(_repository,_ui,panel,resources);return reward;}
                    catch {reward.Free();throw;}
                },()=>RunPlayAction(()=>CompleteForeignTradeReport(view)),
                resources:resources);
            ForeignTradeReportOpenedSequence++;return true;
        }
        catch{CloseForeignTradeReport();throw;}
    }

    private void CompleteForeignTradeReport(ForeignTradeReportView view)
    {
        if(!IsForeignTradeReportCurrent||_foreignTradeReport!=view||_foreignTradeReportPublication is not { } publication||
            _foreignTradeReportSession is not { PlayerForceId:int player } session||_foreignTradeReportWorld is not { } owner)return;
        var result=session.CompleteForeignTradeReport(owner,new CompleteForeignTradeReportOrder(player,publication.TaskId),
            new(_presentationRandom.State));
        if(!result.Accepted)
            throw new InvalidDataException(result.MissingRuleKey??result.Error??"foreign-trade.report.confirmation");
        ForeignTradeLastReportedTaskId=publication.TaskId;ForeignTradeReportAcceptedSequence++;
        CloseForeignTradeReport();RefreshCurrentPlayWorld();RefreshPlayControls();RefreshDiplomaticNegotiation();
    }

    private void CloseForeignTradeReport()
    {
        var view=_foreignTradeReport;var map=_foreignTradeReportMap;
        _foreignTradeReport=null;_foreignTradeReportSession=null;_foreignTradeReportWorld=null;
        _foreignTradeReportMap=null;_foreignTradeReportPublication=null;
        if(view is not null){view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        if(map is not null&&GodotObject.IsInstanceValid(map)&&_residentMap==map)
        {map.SetDetailsOpen(false);if(!_failed&&!_shuttingDown)SetMapFocus(_mapFocused);}
    }
}
