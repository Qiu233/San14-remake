using Godot;
using San14.Data;

internal partial class D3D11MapMarkers
{
    private static Color DiplomacyDisabledColor(Color value)
    {uint packed=AreaMapMarkerSources.DarkenRgb(value.ToAbgr32());return CanvasColor(new AreaMarkerColor(packed,packed));}
    private DiplomacyTargetSelectionKind? diplomacyTargetKind;
    private MapTargetPresentation diplomacyTargetPresentation;
    private Func<int,bool>? diplomacyTargetAllowed;
    internal void SetDiplomacyTargetSelection(DiplomacyTargetSelectionKind? kind,Func<int,bool>? allowed,MapTargetPresentation presentation=MapTargetPresentation.Standard)
    {
        diplomacyTargetKind=kind;diplomacyTargetAllowed=allowed;diplomacyTargetPresentation=presentation;ClearPointer();QueueMarkerRedraw();
    }
    private bool DiplomacyMarkerVisible(Marker marker)=>diplomacyTargetKind switch
    {
        DiplomacyTargetSelectionKind.Force=>false,
        DiplomacyTargetSelectionKind.City=>diplomacyTargetPresentation==MapTargetPresentation.IntelligenceInspectCity?
            marker.Kind==Kind.City&&DiplomacyMarkerEnabled(marker):marker.Kind!=Kind.Area,
        _=>true
    };
    private bool DiplomacyMarkerEnabled(Marker marker)=>diplomacyTargetKind is null||
        marker.Kind!=Kind.Area&&(diplomacyTargetAllowed?.Invoke(marker.Id)??false);
    private CityGateMapLabelPresentation ReadDiplomacyCityPresentation(Marker marker,MainMapCameraFrame frame)
    {
        var ordinary=MapLabelPresentation.CityGate(frame,marker.CityUnitPosition);
        if(diplomacyTargetKind!=DiplomacyTargetSelectionKind.City)return ordinary;
        // 19EA60 mode9 camera column; disabled19E800 forces the flag-only layout.
        int level=ordinary.CameraLevel;
        return ordinary with{Display=DiplomacyMarkerEnabled(marker)?(level<4?0:level==4?1:2):2};
    }
}
