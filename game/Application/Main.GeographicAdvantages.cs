using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private GameState? geographicQueryWorld;
    private GeographicAdvantagePublication? geographicQuery;
    private MapSceneView? geographicMap;

    /// <summary>Bind after the real resident map/HUD pair has been installed.</summary>
    private void ConfigureGeographicAdvantages(MapSceneView map,PlayableForceHud hud)
    {
        geographicMap=map;geographicQueryWorld=null;geographicQuery=null;
        hud.ConfigureGeographicAdvantages(()=>RunUiAction(ToggleGeographicAdvantages),
            ()=>GeographicAdvantagesInputAdmitted(map));
        hud.SetGeographicAdvantagesSelected(map.UserDisplayMode==1);
        hud.ConfigureMerchant(()=>RunUiAction(OpenGlobalMerchant),()=>GeographicAdvantagesInputAdmitted(map));
        hud.ConfigureInformationLayers(action=>RunUiAction(()=>map.ToggleInformationLayer(action)),
            ()=>GeographicAdvantagesInputAdmitted(map),map.InformationLayerSelectionMask);
        map.InformationLayersChanged+=hud.SetInformationLayersSelected;
        hud.TreeExiting+=()=>map.InformationLayersChanged-=hud.SetInformationLayersSelected;
    }
    private bool GeographicAdvantagesInputAdmitted(MapSceneView map)=>
        !_failed&&!_shuttingDown&&!_advancingPeriod&&!IsReturningToTitle&&
        TutorialEntryAllowed&&!TutorialOverlayOwnsInput&&
        !HasAdministrationPage&&!HasMilitaryOrderPage&&!HasCampaignSavePage&&!HasMapSystemMenu&&!HasCommandWorkspace&&
        _detailsView is null&&_commandDetailsView is null&&
        _session is {HasPendingOperation:false}&&ReferenceEquals(_residentMap,map)&&map.IsPlayableMapReady;

    private void ToggleGeographicAdvantages()
    {
        if(_residentMap is not {} map || !GeographicAdvantagesInputAdmitted(map) ||
            _session is not {} session || _campaignComposition is null)return;
        bool enter=map.UserDisplayMode!=1;
        if(enter&&!CaptureGeographicAdvantages(session.World,out var missing))
        {GD.PushWarning(missing);_playMessage.Text=_ui.Get("detail_unknown");return;}
        if(!map.TrySetGeographicAdvantages(enter,geographicQuery,out var problem))
        {if(problem is not null)GD.PushWarning(problem);_forceHud?.SetGeographicAdvantagesSelected(map.UserDisplayMode==1);return;}
        _forceHud?.SetGeographicAdvantagesSelected(enter);
        if(enter)ObserveTutorialAdvantageEntered();
        else ObserveTutorialAdvantageExited();
    }
    private bool CaptureGeographicAdvantages(GameState world,out string? missing)
    {
        missing=null;
        if(ReferenceEquals(geographicQueryWorld,world)&&geographicQuery is not null)return true;
        var result=_campaignComposition!.PrepareArmyQueries(world).QueryGeographicAdvantages(world);
        if(result.Value is not {} value){missing=result.UnresolvedInput;return false;}
        geographicQueryWorld=world;geographicQuery=value;return true;
    }
    /// <summary>Accept new gameplay owners once, never from the render clock.</summary>
    private void RefreshGeographicAdvantages(GameState world,MapSceneView map)
    {
        if(!ReferenceEquals(geographicMap,map)||map.UserDisplayMode!=1)return;
        if(!CaptureGeographicAdvantages(world,out var missing)){GD.PushWarning(missing);return;}
        map.ApplyGeographicAdvantages(geographicQuery!);
    }
    private bool TryCloseGeographicAdvantages()
    {
        if(_residentMap?.UserDisplayMode!=1)return false;
        ToggleGeographicAdvantages();return true;
    }
    private void ClearGeographicAdvantages()
    {
        geographicMap?.ClearGeographicAdvantages();geographicMap=null;
        geographicQueryWorld=null;geographicQuery=null;
        _forceHud?.SetGeographicAdvantagesSelected(false);
    }
}
