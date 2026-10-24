using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private Func<GameState,MapInformationLayerSnapshot>? informationQuery;
    private GameState? informationWorld;
    private MapInformationLayerSnapshot? informationSnapshot;
    private MapInformationCanvas? informationCanvas;
    private MenuButton? informationMenu;
    private KeyboardBindingCatalog? informationKeyboard;
    private bool informationNeighbors,informationDiplomacy;
    private int informationStyle;
    internal int InformationTerritoryStyle => informationStyle;

    internal void SetInformationLayerQuery(Func<GameState,MapInformationLayerSnapshot> query)
    { informationQuery=query; informationWorld=null; }

    internal void InitializeInformationLayers()
    {
        if(informationCanvas is not null)return;
        var parent=_sourceMainCameraAspect as Control??_surface.GetParent() as Control??this;
        informationCanvas=new(){Name="MapInformationLayers",ZIndex=3};
        parent.AddChild(informationCanvas);
        informationCanvas.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        informationCanvas.Configure(_repository,_text,_playerForceId,
            hex=>
            {
                var point=_projection!.Project(hex);
                float x=(float)point.X,z=(float)point.Z;
                return new(x,MapGrounding.SampleHeight(TerrainMeshes!,x,z),z);
            },ForcePalette,()=>
            {
                bool visible=IsInsideTree()&&IsVisibleInTree()&&!_detailsOpen&&!_periodMapActive;
                if(informationMenu is not null)informationMenu.Visible=visible;
                return visible;
            });
        informationKeyboard=KeyboardBindingCatalog.Load(_repository,_text.SourceLocale);
        informationMenu=new(){Name="MapInformationMenu",Text=_text.Get("play_map_information"),
            Position=new(12,12),ZIndex=5};
        parent.AddChild(informationMenu);
        var popup=informationMenu.GetPopup();
        foreach(int action in new[]{17,18,19,20,21,22})
            popup.AddCheckItem(informationKeyboard.Actions.Single(row=>row.ActionId==action).Name,action);
        popup.HideOnCheckableItemSelection=false;
        popup.IdPressed+=id=>Run(()=>ToggleInformationLayer((int)id));
        RefreshInformationMenu();
    }

    internal bool ToggleInformationLayer(int action)
    {
        if(!IsInsideTree()||!IsVisibleInTree()||!D3D11MapInteractive||_detailsOpen||_periodMapActive||_mapPresentation is null)return false;
        switch(action)
        {
            case 17:informationNeighbors=!informationNeighbors;break;
            case 18:informationStyle=informationStyle==0?2:0;break;
            case 19:informationStyle=informationStyle==1?2:1;break;
            case 20:
                informationDiplomacy=(_mapPresentation.Policy.Flags15&4)==0;
                _mapPresentation.SetDiplomacyInformationVisible(informationDiplomacy);
                break;
            case 21:
                _mapPresentation.SetBaseInformationVisible((_mapPresentation.Policy.Category16&4)==0);
                informationDiplomacy=(_mapPresentation.Policy.Flags15&4)!=0;
                break;
            case 22:ToggleKeyboardMapInformation(22);break;
            default:return false;
        }
        ApplyKeyboardMapInformation();
        RefreshInformationMenu();
        ScheduleD3D11MapFrame();
        return true;
    }

    private void RefreshInformationMenu()
    {
        informationDiplomacy=_mapPresentation is not null&&(_mapPresentation.Policy.Flags15&4)!=0;
        if(informationMenu is null)return;
        var popup=informationMenu.GetPopup();
        foreach(int action in new[]{17,18,19,20,21,22})
        {
            int index=popup.GetItemIndex(action);
            popup.SetItemChecked(index,action switch
            {
                17=>informationNeighbors,18=>informationStyle==0,19=>informationStyle==1,
                20=>informationDiplomacy,21=>_mapPresentation is not null&&(_mapPresentation.Policy.Category16&4)!=0,
                _=>_keyboardArmyInformationVisible
            });
        }
    }

    internal MapInformationLayerSnapshot? CaptureInformationLayers(GameState world)
    {
        RefreshInformationMenu();
        if(!informationNeighbors&&!informationDiplomacy)return null;
        if(!_periodMapActive&&!ReferenceEquals(informationWorld,world))
        {
            informationSnapshot=informationQuery?.Invoke(world);
            informationWorld=world;
            SetMeta("map_information_unresolved",informationSnapshot is null?"map-information.query-owner":
                string.Join(";",informationSnapshot.UnresolvedInputs));
        }
        return informationSnapshot;
    }

    internal void PresentInformationLayers(D3D11MapInput input)
    {
        if(informationCanvas is null||input.SourceFrame is not {} frame)return;
        informationCanvas.Present(input.InformationLayers,frame,informationNeighbors,informationDiplomacy);
        if(informationMenu is not null)informationMenu.Visible=!_detailsOpen&&!_periodMapActive;
    }

    internal void ReleaseInformationLayers()
    {
        informationWorld=null;informationSnapshot=null;
        if(informationMenu is not null){informationMenu.QueueFree();informationMenu=null;}
        if(informationCanvas is not null){informationCanvas.Abandon();informationCanvas.QueueFree();informationCanvas=null;}
    }
}
