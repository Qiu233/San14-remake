using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;
using San14.Client.Gameplay;

public partial class Main
{
    private sealed class RegisteredOfficerCityPicker(MapSceneView map,RegisteredOfficerCityPickerView controls,
        GameState world,MapDocument logicalMap,LoadedMap loadedMap,GameplaySession session,
        RegisteredOfficerNewGameView parent,IReadOnlyList<int> eligible,Action<int?> completed)
    {
        internal readonly MapSceneView Map=map;
        internal readonly RegisteredOfficerCityPickerView Controls=controls;
        internal readonly GameState World=world;
        internal readonly MapDocument LogicalMap=logicalMap;
        internal readonly LoadedMap LoadedMap=loadedMap;
        internal readonly GameplaySession Session=session;
        internal readonly RegisteredOfficerNewGameView Parent=parent;
        internal readonly IReadOnlyList<int> Eligible=eligible;
        internal Action<int?>? Completed=completed;
        internal bool Ready,Retiring;
    }
    private RegisteredOfficerCityPicker? _registeredOfficerCityPicker;
    private bool HasRegisteredOfficerCityPicker=>_registeredOfficerCityPicker is not null;

    private void OpenRegisteredOfficerCityPicker(IReadOnlyList<int> eligible,Action<int?> completed)
    {
        ArgumentNullException.ThrowIfNull(eligible);ArgumentNullException.ThrowIfNull(completed);
        if(_registeredOfficerCityPicker is not null)
            throw new InvalidOperationException("A registered-officer map selection is already active.");
        if(_failed||_shuttingDown||_repository is null||_campaignComposition is null||
            _world is not{Phase:TurnPhase.Initializing} world||_playerForceId.HasValue||
            _session is not{ } session||_map is not{ } logicalMap||_loadedMap is not{ } loaded||
            _registeredOfficerNewGame is not{ } parent||_residentMap is not null||_sceneView is not null)
            throw new InvalidOperationException("Registered-officer city selection has no current pre-start map.");
        int[] candidates=eligible.Distinct().ToArray();
        if(candidates.Any(id=>id is <1 or >51||!world.Cities.ContainsKey(id)))
            throw new InvalidDataException("Registered-officer city candidates do not belong to the preview world.");
        var map=new MapSceneView{ZIndex=5};
        map.SetApplicationNativeMapDevice(RequireApplicationNativeMapDevice());
        var controls=new RegisteredOfficerCityPickerView();
        var page=new RegisteredOfficerCityPicker(map,controls,world,logicalMap,loaded,session,parent,candidates,completed);
        _registeredOfficerCityPicker=page;AddChild(map);AddChild(controls);
        try
        {
            controls.Configure(_repository,_ui,_sourceHeaders,()=>RunUiAction(RequestRegisteredOfficerCityPickerBack));
            var safeText=new PreviewText(_locale.Selected==1,null);
            map.Configure(_repository,_ui,safeText,()=>RunUiAction(RequestRegisteredOfficerCityPickerBack),
                logicalMap,world,playerForceId:null,embedded:true,sourceSnapshot:_selectedSnapshot,
                worldScene:true,cameraEntry:MapCameraEntry.FreshController);
            _=CompleteRegisteredOfficerCityPickerAsync(page);
        }
        catch{CloseRegisteredOfficerCityPicker(false);throw;}
    }

    private bool IsRegisteredOfficerCityPickerCurrent(RegisteredOfficerCityPicker page)=>
        _registeredOfficerCityPicker==page&&!page.Retiring&&!_failed&&!_shuttingDown&&
        _registeredOfficerNewGame==page.Parent&&ReferenceEquals(_session,page.Session)&&
        ReferenceEquals(_world,page.World)&&ReferenceEquals(_map,page.LogicalMap)&&
        ReferenceEquals(_loadedMap,page.LoadedMap)&&_world is{Phase:TurnPhase.Initializing}&&!_playerForceId.HasValue;

    private async Task CompleteRegisteredOfficerCityPickerAsync(RegisteredOfficerCityPicker page)
    {
        try
        {
            await page.Map.InitialSceneLoad;
            if(!IsRegisteredOfficerCityPickerCurrent(page))return;
            if(!page.Map.GetMeta("scene_loaded",false).AsBool()||!page.Map.IsPlayableMapReady)
                throw new InvalidDataException(page.Map.GetMeta("scene_error",
                    page.Map.GetMeta("native_map_source_error","registered_officers.city_picker.map_not_ready")).AsString());
            var query=_campaignComposition!.PrepareArmyQueries(page.World).CreateRegisteredOfficerCityMapQuery(page.World);
            page.Ready=true;
            page.Map.BeginRegisteredOfficerCityPick(page.World,query,page.Eligible,
                ()=>page.Ready&&IsRegisteredOfficerCityPickerCurrent(page),city=>RunUiAction(()=>
                {
                    if(!IsRegisteredOfficerCityPickerCurrent(page)||!page.Eligible.Contains(city))return;
                    _=FinishRegisteredOfficerCityPickerAsync(page,city,true);
                }));
        }
        catch(Exception error)
        {
            if(!IsRegisteredOfficerCityPickerCurrent(page))return;
            page.Ready=false;page.Controls.ApplyNotice(error.Message);
            GD.PushWarning("Registered-officer city map: "+error.Message);
        }
    }

    private void RequestRegisteredOfficerCityPickerBack()=>CloseRegisteredOfficerCityPicker(true);
    private void CloseRegisteredOfficerCityPicker(bool notify=false)
    {
        if(_registeredOfficerCityPicker is not{ } page)return;
        if(!notify)page.Completed=null;
        if(!page.Retiring)_=FinishRegisteredOfficerCityPickerAsync(page,null,notify);
    }
    private async Task FinishRegisteredOfficerCityPickerAsync(RegisteredOfficerCityPicker page,int? city,bool notify)
    {
        if(_registeredOfficerCityPicker!=page||page.Retiring)return;
        page.Retiring=true;page.Ready=false;
        page.Map.EndRegisteredOfficerCityPick();page.Controls.Abandon();
        page.Controls.GetParent()?.RemoveChild(page.Controls);page.Controls.QueueFree();
        RetireMapView(page.Map);
        //The next additional-city pick may start from its callback. Release
        //the actual native map before permitting that next owner to allocate.
        Task retirement=_mapRetirements.Last(entry=>ReferenceEquals(entry.View,page.Map)).Completion;
        try{await retirement;}
        catch(Exception error)
        {
            if(!_shuttingDown)ShowFailure(error,startup:false);
            return;
        }
        finally
        {
            if(_registeredOfficerCityPicker==page)_registeredOfficerCityPicker=null;
        }
        var callback=page.Completed;page.Completed=null;
        if(notify&&callback is not null&&!_failed&&!_shuttingDown&&_registeredOfficerNewGame==page.Parent&&
            ReferenceEquals(_session,page.Session)&&ReferenceEquals(_world,page.World)&&
            ReferenceEquals(_loadedMap,page.LoadedMap))callback(city);
    }
}
