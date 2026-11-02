using Godot;
using San14.Core;
using San14.Data;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class CityDeploymentCancellationOwner(GameplaySession session,GameState world,MapSceneView map,int city,CityDeploymentCancellationView view)
    {
        internal GameplaySession Session=session;
        internal GameState World=world;
        internal readonly MapSceneView Map=map;
        internal readonly int CityId=city;
        internal readonly CityDeploymentCancellationView View=view;
    }
    private CityDeploymentCancellationOwner? _cityDeploymentCancellation;
    private bool IsCityDeploymentCancellationCurrent(CityDeploymentCancellationOwner owner)=>
        !_failed&&!_shuttingDown&&_cityDeploymentCancellation==owner&&ReferenceEquals(_session,owner.Session)&&
        ReferenceEquals(_world,owner.World)&&ReferenceEquals(owner.Session.World,owner.World)&&_residentMap==owner.Map;

    private void OpenCityDeploymentCancellation(MapSceneView map,int city)
    {
        if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||HasMilitaryOrderPage||HasAdministrationPage||
            HasCampaignSavePage||HasMapSystemMenu||HasCommandWorkspace||HasActionReport||HasPersonnelConfirmation||
            _detailsView is not null||_repository is null||_campaignComposition is null||_residentMap!=map||
            map.CurrentCityCommandMenuId!=city||_session is not{PlayerForceId:int} session||session.HasPendingOperation||
            session.World is not{Phase:TurnPhase.Strategy} world)return;
        var page=_campaignComposition.PrepareArmyQueries(world).QueryDeploymentCancellation(world,city);
        if(page.UnresolvedOwner is{} missing){GD.PushWarning(missing);_playMessage.Text=_ui.Get("detail_unknown");return;}
        var view=new CityDeploymentCancellationView();
        var owner=new CityDeploymentCancellationOwner(session,world,map,city,view);
        _cityDeploymentCancellation=owner;AddChild(view);
        try
        {
            map.SetCityDeploymentCancellationOpen(true);HideDeploymentHud();
            view.Configure(_repository,_ui,page,map.CurrentCityCommandMenuRectangle,
                ()=>IsCityDeploymentCancellationCurrent(owner),army=>RunPlayAction(()=>Cancel(army)),
                ()=>RunUiAction(CloseCityDeploymentCancellation),CurrentListScrollSpeed, resources:TryBorrowApplicationUiResources(view));
        }
        catch{CloseCityDeploymentCancellation();throw;}
        void Cancel(ArmyReceiverId army)
        {
            if(!IsCityDeploymentCancellationCurrent(owner))return;
            bool accepted=ApplyPlayableOrder(new CancelCityArmyDeploymentOrder(session.PlayerForceId!.Value,city,army),
                ()=>owner.World=session.World);
            if(!accepted||!IsCityDeploymentCancellationCurrent(owner))return;
            if(!map.RefreshOpenCityCommandMenu(city)){CloseCityDeploymentCancellation();return;}
            RestoreFoodWarnings(session,owner.World,map);
            view.Apply(_campaignComposition.PrepareArmyQueries(owner.World).QueryDeploymentCancellation(owner.World,city),CurrentListScrollSpeed);
        }
    }
    private void CloseCityDeploymentCancellation()
    {
        var owner=_cityDeploymentCancellation;_cityDeploymentCancellation=null;
        if(owner is null)return;
        owner.View.Abandon();owner.View.QueueFree();
        if(GodotObject.IsInstanceValid(owner.Map))owner.Map.SetCityDeploymentCancellationOpen(false);
        if(!_failed&&!_shuttingDown&&_residentMap is not null)
        {SetMapFocus(_mapFocused);if(_residentMap.IsCityCommandMenuOpen)HideDeploymentHud();}
    }
}
