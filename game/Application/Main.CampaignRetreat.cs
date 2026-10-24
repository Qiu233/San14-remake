using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;
using System.Globalization;

public partial class Main
{
    private CampaignRetreatView? _campaignRetreatView;
    private CampaignAllRetreatPage? _campaignRetreatPage;
    private DeploymentTargetConfirmation? _campaignRetreatConfirmation;
    private GameplaySession? _campaignRetreatSession;
    private GameState? _campaignRetreatWorld;
    private MapSceneView? _campaignRetreatMap;
    private bool HasCampaignRetreatPage=>_campaignRetreatView is not null;
    private bool HasCampaignCommandPage=>HasCampaignDeploymentPage||HasCampaignRetreatPage;
    private bool IsCampaignRetreatCurrent=>!_failed&&!_shuttingDown&&HasCampaignRetreatPage&&
        ReferenceEquals(_session,_campaignRetreatSession)&&ReferenceEquals(_world,_campaignRetreatWorld)&&
        ReferenceEquals(_session?.World,_campaignRetreatWorld)&&_residentMap==_campaignRetreatMap;

    private void OpenCampaignRetreat()
    {
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||HasMilitaryOrderPage||HasAdministrationPage||
            HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||HasActionReport||HasPersonnelConfirmation||
            _detailsView is not null||_repository is null||_campaignComposition is not {} composition||
            _session is not {PlayerForceId:int player} session||session.HasPendingOperation||
            session.World is not {Phase:TurnPhase.Strategy} world||_residentMap is not {IsPlayableMapReady:true} map)return;
        var page=composition.PrepareArmyQueries(world).QueryCampaignAllRetreatPage(world,player);
        if(!page.Available){CampaignDeploymentUnavailable(page.UnresolvedOwner!);return;}
        var choices=page.Rows.Select(row=>new CampaignRetreatChoice(row.CampaignId,
            (row.ProvinceName??_ui.Get("detail_unknown"))+"  "+row.SequenceNumber.ToString(CultureInfo.InvariantCulture),
            _ui.Format("play_campaign_retreat_details",CampaignNumber(row.ArmyCount),CampaignNumber(row.Troops),row.StartYear,row.StartMonth),
            row.Enabled&&!row.IsWithdrawing&&row.UnresolvedOwner is null,false,
            row.DisabledMessageId is int message?CampaignLiteral(message):row.UnresolvedOwner is null?null:_ui.Get("detail_unknown"))).ToArray();
        var view=new CampaignRetreatView();_campaignRetreatView=view;_campaignRetreatPage=page;
        _campaignRetreatSession=session;_campaignRetreatWorld=world;_campaignRetreatMap=map;AddChild(view);
        try
        {
            view.Configure(_repository,_ui,choices,CampaignLiteral(0x5804),
                ()=>IsCampaignRetreatCurrent&&_campaignRetreatConfirmation is null,
                ()=>RunUiAction(RequestCampaignRetreatBack),selected=>RunUiAction(()=>ConfirmCampaignRetreat(selected)),CurrentListScrollSpeed);
            map.CloseCityCommandMenu();map.SetDetailsOpen(true,keepMapVisible:true);SetMapFocus(true);HideDeploymentHud();
        }
        catch{CloseCampaignRetreat();throw;}
    }

    private string CampaignNumber(int? value)=>value?.ToString(CultureInfo.InvariantCulture)??_ui.Get("detail_unknown");

    private void ConfirmCampaignRetreat(IReadOnlyList<int> ids)
    {
        if(!IsCampaignRetreatCurrent||_campaignRetreatConfirmation is not null||ids.Count==0)return;
        if(_campaignRetreatPage?.ConfirmationText is not string prompt)
        {_campaignRetreatView!.ApplyNotice(_ui.Get("detail_unknown"));return;}
        var selected=ids.ToArray();
        var question=new DeploymentTargetConfirmation();_campaignRetreatConfirmation=question;AddChild(question);
        _campaignRetreatView!.SetBusy(true);
        try
        {
            question.ConfigureMessage(_repository!,_ui,prompt,accepted=>RunPlayAction(()=>
            {
                if(!IsCampaignRetreatCurrent||_campaignRetreatConfirmation!=question)return;
                _campaignRetreatConfirmation=null;question.Abandon();question.QueueFree();
                if(!accepted){_campaignRetreatView!.SetBusy(false);return;}
                if(!ApplyPlayableOrder(new RetreatCampaignsOrder(_campaignRetreatSession!.PlayerForceId!.Value,selected),CloseCampaignRetreat))
                {_campaignRetreatView!.SetBusy(false);_campaignRetreatView.ApplyNotice(_playMessage.Text);}
            }));
        }
        catch{CloseCampaignRetreat();throw;}
    }

    private void RequestCampaignRetreatBack()
    {
        if(!IsCampaignRetreatCurrent)return;
        if(_campaignRetreatConfirmation is {} question)question.RequestCancel();else CloseCampaignRetreat();
    }
    private void RequestCampaignCommandBack()
    {if(HasCampaignDeploymentPage)RequestCampaignDeploymentBack();else if(HasCampaignRetreatPage)RequestCampaignRetreatBack();}
    private void CloseCampaignRetreat()
    {
        var view=_campaignRetreatView;var map=_campaignRetreatMap;var question=_campaignRetreatConfirmation;
        _campaignRetreatView=null;_campaignRetreatPage=null;_campaignRetreatConfirmation=null;
        _campaignRetreatSession=null;_campaignRetreatWorld=null;_campaignRetreatMap=null;
        if(question is not null){question.Abandon();question.QueueFree();}
        if(view is not null){view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        if(!_failed&&!_shuttingDown&&map is not null&&GodotObject.IsInstanceValid(map)&&map==_residentMap)
        {map.SetDetailsOpen(false);SetMapFocus(true);}
    }
}
