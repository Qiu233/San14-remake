using San14.Core;
using San14.Data;

public partial class Main
{
    private void BindAreaHexCommands(MapSceneView map)
    {
        map.SetAreaHexCommandMenuQuery((world,area,commands)=>
        {
            if(_failed||_shuttingDown||_residentMap!=map||_session is not {} session||
                !ReferenceEquals(_world,world)||!ReferenceEquals(session.World,world)||
                _campaignComposition is not {} composition)
                return new(null,"area-hex-menu.ui_world_binding");
            return composition.PrepareArmyQueries(world).QueryAreaHexCommandMenu(world,area,commands);
        });
        map.AreaHexCommand+=(page,command)=>RunUiAction(()=>
        {
            int areaId=page.AreaId;
            if(_failed||_shuttingDown||_advancingPeriod||IsReturningToTitle||!TutorialEntryAllowed||
                _residentMap!=map||_session is not {HasPendingOperation:false} session||
                _world is not {} world||!ReferenceEquals(session.World,world)||
                !world.Areas.TryGetValue(areaId,out var area))return;
            void Returned()
            {
                if(_failed||_shuttingDown||IsReturningToTitle||_residentMap!=map||_session!=session||
                    session.HasPendingOperation||!ReferenceEquals(_world,session.World)||
                    session.World.Phase!=TurnPhase.Strategy)return;
                SetMapFocus(true);map.ReopenAreaHexCommandMenu(areaId);
            }
            switch(command)
            {
                case AreaHexCommandMenuCommand.AreaAdministration:
                    if(page.CityId is not int city||!TutorialCommandAllowed(3))return;
                    SelectMapCommandCity(city);OpenAreaAdministration(city,areaId,Returned);
                    if(_administrationParent is not null)ObserveTutorialCommand(3);
                    break;
                case AreaHexCommandMenuCommand.UnitTarget:
                    OpenGeneralUnitTarget(new(DeploymentTargetKind.Area,areaId),Returned);break;
                case AreaHexCommandMenuCommand.Strategic:
                    OpenStrategicOrders(13,areaId,Returned);break;
                case AreaHexCommandMenuCommand.AreaInformation:
                    OpenMapAreaDetails(map,areaId,Returned);break;
            }
        });
    }
}
