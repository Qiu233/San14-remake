using San14.Core;
using San14.Data;

public partial class Main
{
    private void InitializeOrdinaryBottomHud(MapSceneView map)
    {
        map.SetMapBottomHudQueries(
            PrepareOrdinaryMapAdvice,
            world=>_campaignComposition!.PrepareArmyQueries(world).QueryMapBottomLog(world),
            (world,people)=>_campaignComposition!.PrepareArmyQueries(world).QueryOfficerPortraitRoutes(world,people),
            entry=>RunUiAction(()=>ExecuteOrdinaryMapAdvice(map,entry)),
            hex=>RunUiAction(()=>{map.FocusCommanderHex(hex);}));
    }
    private AreaAdministrationQuery<MapAdvicePublication> PrepareOrdinaryMapAdvice(GameState world)
    {
        // The map has admitted this committed strategy World. Presentation
        // draws belong to the existing application cursor, not a replacement
        // gameplay World: retaining its reference keeps the map cache stable.
        if(_session is not{HasPendingOperation:false} session||
            !ReferenceEquals(_world,world)||!ReferenceEquals(session.World,world)||_advancingPeriod)
            return new(null,"map.advice.committed_strategy_owner");
        var prepared=_campaignComposition!.PrepareArmyQueries(world)
            .PrepareMapAdvice(world,new(_presentationRandom.State));
        var cursor=prepared.State.CampaignAppRandom32
            ??throw new InvalidDataException("map.advice.presentation_App32_owner");
        _presentationRandom.InstallCommittedState(cursor.State);
        return prepared.Publication;
    }
    private void ExecuteOrdinaryMapAdvice(MapSceneView map,MapAdviceEntry entry)
    {
        if(_residentMap!=map||_world is not{} world||_session is not{} session||
            !ReferenceEquals(session.World,world)||session.HasPendingOperation||_advancingPeriod||
            world.Phase!=TurnPhase.Strategy||HasAdministrationPage||HasMilitaryOrderPage||HasMapSystemMenu)return;
        if(entry.Action is 1 or 2&&entry.CityId is int city&&world.Cities.ContainsKey(city))
        {
            map.CloseCityCommandMenu();SelectMapCommandCity(city);OpenCityDomestic(city);
            _cityDomesticPresenter?.OpenSuggestedDuty(entry.Action==1?CityDomesticDuty.Recruiter:CityDomesticDuty.Trainer);
        }
        else if(entry.Action==3&&entry.CityId is int home&&entry.AreaId is int area)
        {map.CloseCityCommandMenu();SelectMapCommandCity(home);OpenAreaAdministration(home,area);}
        else if(entry.Action==4&&entry.SceneryId is int scenery)
        {map.CloseCityCommandMenu();OpenSceneryInvestmentOrders(entry.CityId,scenery);}
    }
}

