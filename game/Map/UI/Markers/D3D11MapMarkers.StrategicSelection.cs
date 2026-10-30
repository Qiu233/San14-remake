using Godot;
using San14.Core;
using San14.Data;

internal partial class D3D11MapMarkers
{
    private StrategicTargetMapQuery? strategicTargetSelection;
    private Func<int,bool>? strategicTargetAllowed;
    private MapFontProfile? strategicDisabledAreaFont;

    internal void SetStrategicTargetSelection(StrategicTargetMapQuery? query, Func<int,bool>? allowed)
    {
        strategicTargetSelection=query;strategicTargetAllowed=allowed;
        strategicDisabledAreaFont=query?.Kind==StrategicCommandTargetKind.Area?_areaFont?.WithPreset(_textPresets![23]):null;
        var issues = new HashSet<string>(StringComparer.Ordinal);
        if(query is not null)
            foreach(var marker in _markers.Values)
                if(StrategicMarkerState(marker).UnresolvedInput is { } issue)issues.Add(issue);
        if(issues.Count==0)RemoveMeta("strategic_target_marker_issue");
        else SetMeta("strategic_target_marker_issue",string.Join(";",issues));
        ClearPointer();PositionFacilityInfo();QueueMarkerRedraw();
    }

    private StrategicTargetMarkerState StrategicMarkerState(Marker marker)=>strategicTargetSelection!.QueryMarker(
        marker.Kind==Kind.Area?StrategicMapMarkerKind.Area:marker.Kind==Kind.Gate?StrategicMapMarkerKind.Gate:StrategicMapMarkerKind.City,marker.Id);
    private bool StrategicMarkerVisible(Marker marker)=>strategicTargetSelection?.Kind switch
    {
        StrategicCommandTargetKind.Force=>false,
        StrategicCommandTargetKind.City=>marker.Kind!=Kind.Area,
        _=>true
    };
    private bool StrategicMarkerEnabled(Marker marker)=>strategicTargetSelection is null||
        StrategicMarkerState(marker) is {Enabled:true,TargetId:int id}&&strategicTargetAllowed?.Invoke(id)==true;
    private bool TargetMarkerVisible(Marker marker)=>DiplomacyMarkerVisible(marker)&&StrategicMarkerVisible(marker);
    private bool TargetMarkerEnabled(Marker marker)=>DiplomacyMarkerEnabled(marker)&&StrategicMarkerEnabled(marker);
    private bool TargetMarkerDisabled(Marker marker)=>
        diplomacyTargetKind==DiplomacyTargetSelectionKind.City&&!DiplomacyMarkerEnabled(marker)||
        strategicTargetSelection is not null&&!StrategicMarkerEnabled(marker);
    private bool FacilityInformationModeAllows=>strategicTargetSelection is not null?
        strategicTargetSelection.ShowFacilities:foodWarningInformationMode==0;

    private AreaMapLabelPresentation ReadTargetAreaPresentation(Marker marker,MainMapCameraFrame frame)
    {
        var value=MapLabelPresentation.Area(frame,marker.AreaUnitPosition);
        if(strategicTargetSelection?.Kind!=StrategicCommandTargetKind.Area)return value;
        //19E940's actual MapInfo8 column. Disabled Areas retain this same
        //display in19E740; they are not forced to the flag-only display.
        return value with{Display=value.CameraLevel<5?0:value.CameraLevel==5?1:-1};
    }

    private CityGateMapLabelPresentation ReadTargetCityPresentation(Marker marker,MainMapCameraFrame frame)
    {
        var value=ReadDiplomacyCityPresentation(marker,frame);
        if(strategicTargetSelection is null)return value;
        return value with{Display=StrategicMarkerEnabled(marker)?StrategicCityDisplay(value.CameraLevel):2};
    }

    private static int StrategicCityDisplay(int level)=>level<4?0:level==4?1:2;
    private MapFontProfile TargetAreaFont(Marker marker)=>strategicDisabledAreaFont is { } disabled&&
        !StrategicMarkerEnabled(marker)?disabled:_areaFont!;

    private bool DrawStrategicDisabledArea(CanvasItem canvas,Marker marker,Vector2 point)
    {
        if(strategicTargetSelection?.Kind!=StrategicCommandTargetKind.Area||StrategicMarkerEnabled(marker))return false;
        var presentation=marker.AreaPresentation;
        Rect2 flag=AreaRectangle(_areaLayout!.FlagRectangle,point);
        // CCapitalFlagLayout00 3_disable exposes authored nodes16/17:
        // outline palette6, and1B0CE0's RGB*.4 preserving the original alpha.
        PaletteSprite(canvas,_areaFlag[0],flag,_spritePalettes![6],presentation.FlagAlpha);
        uint original=marker.Tint.ToAbgr32();
        uint dark=AreaMapMarkerSources.DarkenRgb(original);
        canvas.DrawTextureRect(_areaFlag[1],flag,false,Alpha(CanvasColor(new(dark,dark)),presentation.FlagAlpha));
        if(presentation.ShowName)
        {
            Rect2 text=AreaRectangle(_areaLayout.NameCellRectangle,point);
            var profile=TargetAreaFont(marker);
            _font!.Draw(canvas,marker.Name,text.Position+new Vector2(0,_font.CellBaselineY(profile,text.Size.Y)),profile,
                modulate:Alpha(Colors.White,presentation.TextAlpha));
        }
        return true;
    }
}
