using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private CampaignDeploymentView? _campaignDeploymentView;
    private CampaignDeploymentPage? _campaignDeploymentPage;
    private CampaignDeploymentDraft? _campaignDeploymentDraft;
    private GameplaySession? _campaignDeploymentSession;
    private GameState? _campaignDeploymentWorld;
    private MapSceneView? _campaignDeploymentMap;
    private CampaignDeploymentUiStage _campaignDeploymentStage;
    private int? _campaignTargetProvince, _campaignSourceDistrict, _campaignHoveredProvince;
    private DeploymentTargetConfirmation? _campaignDeploymentConfirmation;
    private DeploymentNarrationView? _campaignDeploymentNarration;
    private readonly Dictionary<(string Pack,string Sprite),LoadedSprite> _campaignPortraits = [];
    private bool HasCampaignDeploymentPage => _campaignDeploymentView is not null;
    private bool IsCampaignDeploymentCurrent => !_failed&&!_shuttingDown&&HasCampaignDeploymentPage&&
        ReferenceEquals(_session,_campaignDeploymentSession)&&ReferenceEquals(_world,_campaignDeploymentWorld)&&
        ReferenceEquals(_session?.World,_campaignDeploymentWorld)&&_residentMap==_campaignDeploymentMap;
    private bool CanPickCampaignDeployment => IsCampaignDeploymentCurrent&&
        _campaignDeploymentConfirmation is null&&_campaignDeploymentNarration is null;

    private void OpenCampaignDeployment()
    {
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||HasMilitaryOrderPage||HasAdministrationPage||
            HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||HasActionReport||HasPersonnelConfirmation||
            _detailsView is not null||_repository is null||_campaignComposition is not {} composition||
            _session is not {PlayerForceId:int player} session||session.HasPendingOperation||
            session.World is not {Phase:TurnPhase.Strategy} world||
            _residentMap is not {IsPlayableMapReady:true} map)return;
        var page=composition.PrepareArmyQueries(world).PrepareCampaignDeploymentPage(world,player);
        if(!page.Available){CampaignDeploymentUnavailable(page.UnresolvedOwner!);return;}
        var view=new CampaignDeploymentView();_campaignDeploymentView=view;_campaignDeploymentPage=page;
        _campaignDeploymentWorld=world;_campaignDeploymentSession=session;_campaignDeploymentMap=map;
        _campaignDeploymentStage=CampaignDeploymentUiStage.Target;
        _campaignTargetProvince=_campaignSourceDistrict=_campaignHoveredProvince=null;_campaignDeploymentDraft=null;
        AddChild(view);
        try
        {
            view.Configure(_repository,_ui,()=>CanPickCampaignDeployment,
                ()=>RunUiAction(RequestCampaignDeploymentBack),()=>RunUiAction(ConfirmCampaignDeployment));
            view.RegisterHeader(_sourceHeaders);map.CloseCityCommandMenu();
            SetMapFocus(true);BindCampaignDeploymentMap();HideDeploymentHud();
        }
        catch{CloseCampaignDeployment();throw;}
    }

    private void BindCampaignDeploymentMap()
    {
        if(!CanPickCampaignDeployment)return;
        var stage=_campaignDeploymentStage switch
        {
            CampaignDeploymentUiStage.Target=>CampaignTargetMapStage.TargetProvince,
            CampaignDeploymentUiStage.District=>CampaignTargetMapStage.District,
            _=>CampaignTargetMapStage.Foothold
        };
        _campaignDeploymentMap!.SetDetailsOpen(false,keepMapVisible:true);
        _campaignDeploymentMap.BeginCampaignTargetMapPick(_campaignDeploymentPage!,stage,
            _campaignTargetProvince,_campaignSourceDistrict,_campaignDeploymentDraft?.Sources??[],
            ()=>CanPickCampaignDeployment,id=>RunUiAction(()=>PickCampaignDeployment(id)),HoverCampaignDeployment);
        RefreshCampaignDeployment();HideDeploymentHud();
    }

    private void PickCampaignDeployment(int id)
    {
        if(!CanPickCampaignDeployment||_campaignDeploymentPage is not {} page)return;
        switch(_campaignDeploymentStage)
        {
            case CampaignDeploymentUiStage.Target:
                if(page.QueryTargets().FirstOrDefault(row=>row.Id==id)?.Available!=true)return;
                _campaignTargetProvince=id;_campaignSourceDistrict=null;_campaignDeploymentDraft=null;
                _campaignDeploymentStage=CampaignDeploymentUiStage.District;BindCampaignDeploymentMap();break;
            case CampaignDeploymentUiStage.District:
                if(_campaignTargetProvince is not int province||
                    page.QueryDistricts(province).FirstOrDefault(row=>row.Id==id)?.Available!=true)return;
                _campaignSourceDistrict=id;_campaignDeploymentDraft=page.CreateDraft(province,id);
                _campaignDeploymentStage=CampaignDeploymentUiStage.Footholds;BindCampaignDeploymentMap();break;
            default:
                if(_campaignDeploymentDraft is not {} draft)return;
                if(!draft.ToggleSource(id))
                {if(draft.UnresolvedOwner is {} missing)CampaignDeploymentUnavailable(missing);return;}
                _campaignDeploymentMap!.ApplyCampaignTargetMapPick(draft.Sources);RefreshCampaignDeployment();break;
        }
    }

    private void HoverCampaignDeployment(int? id)
    {
        if(!CanPickCampaignDeployment||_campaignDeploymentPage is not {} page)return;
        int? hintId=_campaignDeploymentStage switch
        {
            CampaignDeploymentUiStage.Target=>page.QueryTargets().FirstOrDefault(row=>row.Id==id)?.HintMessageId,
            CampaignDeploymentUiStage.District when _campaignTargetProvince is int province=>
                page.QueryDistricts(province).FirstOrDefault(row=>row.Id==id)?.HintMessageId,
            CampaignDeploymentUiStage.Footholds when _campaignTargetProvince is int province&&_campaignSourceDistrict is int district=>
                page.QueryFootholds(province,district).FirstOrDefault(row=>row.Id==id)?.HintMessageId,
            _=>null
        };
        if(_campaignDeploymentStage==CampaignDeploymentUiStage.Target&&_campaignHoveredProvince!=id)
        {_campaignHoveredProvince=id;RefreshCampaignDeployment();}
        _campaignDeploymentView!.ApplyHoverHint(hintId.HasValue?CampaignLiteral(hintId.Value):null);
    }

    private void RefreshCampaignDeployment()
    {
        if(!IsCampaignDeploymentCurrent)return;
        int? province=_campaignDeploymentStage==CampaignDeploymentUiStage.Target?_campaignHoveredProvince:_campaignTargetProvince;
        CampaignDeploymentBarValues values=new(0,null,[],[],"");
        if(province is int selected)
        {
            var bar=_campaignDeploymentPage!.QueryBar(selected,_campaignDeploymentDraft);
            var segments=new List<CampaignDeploymentBarSegment>();
            if(_campaignDeploymentStage==CampaignDeploymentUiStage.Footholds&&bar.OurTroops is int own&&bar.OtherTroops is int other)
                segments.Add(new(own+other==0?0:(double)own/(own+other),CampaignBarColors(bar.PlayerForceColor49)));
            segments.AddRange(bar.Segments.Select(segment=>new CampaignDeploymentBarSegment(segment.Fraction,CampaignBarColors(segment.ForceColor49))));
            var portraits=bar.Commanders.Select(person=>LoadCampaignPortrait(person.FacePortrait)).ToArray();
            values=new(bar.OtherTroops,bar.OurTroops,segments,portraits,bar.AdditionalArmiesText??"");
            if(bar.UnresolvedOwner is {} missing)GD.PushWarning(missing);
        }
        int guide=_campaignDeploymentStage switch
        {CampaignDeploymentUiStage.Target=>0x5806,CampaignDeploymentUiStage.District=>0x5809,_=>0x580C};
        _campaignDeploymentView!.Apply(_campaignDeploymentStage,CampaignLiteral(guide),
            CanPickCampaignDeployment&&_campaignDeploymentDraft?.BuildOrder() is not null,values);
    }

    private static MapCornerColors? CampaignBarColors(byte? color)
    {
        if(color is not byte index)return null;
        var fill=MapSpritePalettes.ForceFill(index);return new(fill,fill,fill,fill);
    }
    private LoadedSprite? LoadCampaignPortrait(OfficerPortraitRoute? route)
    {
        if(route is not {Kind:OfficerPortraitRouteKind.StockNamed})return null;
        var key=(route.ResourceName!,route.SpriteName!);
        if(!_campaignPortraits.TryGetValue(key,out var sprite))
        {sprite=new LocalizedUiSpriteSource(_repository!).LoadTexturePackSprite(key.Item1,key.Item2);_campaignPortraits.Add(key,sprite);}
        return sprite;
    }
    private string CampaignLiteral(int id)=>_repository!.LoadMessages(_ui.SourceLocale,MessageKind.Game)
        .Messages.Sections[id/10000].Records[id%10000].Literals.Single().Text;

    private void ConfirmCampaignDeployment()
    {
        if(!CanPickCampaignDeployment||_campaignDeploymentDraft is not {} draft||draft.BuildOrder() is null)return;
        var quote=_campaignDeploymentPage!.QueryConfirmation(draft);
        if(!quote.CanConfirm||quote.Text is null){CampaignDeploymentUnavailable(quote.UnresolvedOwner??"campaign.confirmation");return;}
        var question=new DeploymentTargetConfirmation();_campaignDeploymentConfirmation=question;AddChild(question);
        _campaignDeploymentView!.SetBusy(true);_campaignDeploymentMap!.SetDetailsOpen(true,keepMapVisible:true);
        try
        {
            question.ConfigureMessage(_repository!,_ui,quote.Text,accepted=>RunUiAction(()=>
            {
                if(!IsCampaignDeploymentCurrent||_campaignDeploymentConfirmation!=question)return;
                _campaignDeploymentConfirmation=null;question.Abandon();question.QueueFree();
                if(accepted)PrepareCampaignDeploymentNarration();
                else{_campaignDeploymentView!.SetBusy(false);BindCampaignDeploymentMap();}
            }));
        }
        catch{CloseCampaignDeployment();throw;}
    }

    private void PrepareCampaignDeploymentNarration()
    {
        if(!IsCampaignDeploymentCurrent||_campaignDeploymentDraft is not {} draft)return;
        var preparation=draft.PrepareHandover(_ui.SourceLocale,_presentationRandom.Next);
        if(!preparation.Available||preparation.PortraitRoute is not {} route)
        {
            _campaignDeploymentView!.SetBusy(false);BindCampaignDeploymentMap();
            CampaignDeploymentUnavailable(preparation.UnresolvedInput??"campaign.narration.portrait");return;
        }
        var sprites=new LocalizedUiSpriteSource(_repository!);
        var layers=route.LoadSprites(sprites.LoadTexturePackSprite);
        var narration=new DeploymentNarrationView {KeyboardInput=HandleGameplayNarrationKeyboard};
        _campaignDeploymentNarration=narration;AddChild(narration);
        try
        {
            narration.Configure(_repository!,_ui,preparation.SpeakerName!,preparation.Text!,layers,()=>RunPlayAction(()=>
            {
                if(_campaignDeploymentNarration!=narration)return;
                if(!IsCampaignDeploymentCurrent){CloseCampaignDeployment();return;}
                if(draft.BuildOrder() is not {} order)return;
                if(!ApplyPlayableOrder(order,CloseCampaignDeployment))
                {
                    _campaignDeploymentNarration=null;narration.Abandon();narration.QueueFree();
                    _campaignDeploymentView!.Show();_campaignDeploymentView.SetBusy(false);BindCampaignDeploymentMap();
                    _campaignDeploymentView.ApplyNotice(_playMessage.Text);
                }
            }),()=>_preferences.AutomaticMessageAdvance,
                ()=>CanAdvanceNarration&&_campaignDeploymentNarration==narration&&IsCampaignDeploymentCurrent);
            _campaignDeploymentView!.Hide();_campaignDeploymentMap!.EndCampaignTargetMapPick();
            _campaignDeploymentMap.SetDetailsOpen(true,keepMapVisible:true);
        }
        catch{CloseCampaignDeployment();throw;}
    }

    private void RequestCampaignDeploymentBack()
    {
        if(!IsCampaignDeploymentCurrent||_campaignDeploymentNarration is not null)return;
        if(_campaignDeploymentConfirmation is {} question){question.RequestCancel();return;}
        if(_campaignDeploymentStage==CampaignDeploymentUiStage.Target){CloseCampaignDeployment();return;}
        _campaignDeploymentDraft=null;_campaignSourceDistrict=null;
        if(_campaignDeploymentStage==CampaignDeploymentUiStage.District)
        {_campaignDeploymentStage=CampaignDeploymentUiStage.Target;_campaignTargetProvince=_campaignHoveredProvince=null;}
        else _campaignDeploymentStage=CampaignDeploymentUiStage.District;
        BindCampaignDeploymentMap();
    }
    private void CampaignDeploymentUnavailable(string reason)
    {string message=_ui.Get("detail_unknown");_playMessage.Text=message;_campaignDeploymentView?.ApplyNotice(message);GD.PushWarning(reason);}
    private void CloseCampaignDeployment()
    {
        var map=_campaignDeploymentMap;_campaignDeploymentMap=null;
        var view=_campaignDeploymentView;_campaignDeploymentView=null;
        var question=_campaignDeploymentConfirmation;_campaignDeploymentConfirmation=null;
        var narration=_campaignDeploymentNarration;_campaignDeploymentNarration=null;
        _campaignDeploymentPage=null;_campaignDeploymentDraft=null;_campaignDeploymentSession=null;_campaignDeploymentWorld=null;
        _campaignTargetProvince=_campaignSourceDistrict=_campaignHoveredProvince=null;_campaignPortraits.Clear();
        if(question is not null){question.Abandon();question.QueueFree();}
        if(narration is not null){narration.Abandon();narration.QueueFree();}
        if(view is not null){view.Abandon();view.GetParent()?.RemoveChild(view);view.QueueFree();}
        if(map is not null&&GodotObject.IsInstanceValid(map))
        {map.EndCampaignTargetMapPick();if(!_failed&&!_shuttingDown&&map==_residentMap){map.SetDetailsOpen(false);SetMapFocus(true);}}
    }
}
