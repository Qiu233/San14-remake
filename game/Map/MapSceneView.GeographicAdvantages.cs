using Godot;
using San14.Core;
using San14.Data;

public partial class MapSceneView
{
    private CampaignCameraSnapshot? geographicPriorCamera;
    private GeographicAdvantagePublication? geographicPublication;
    private D3D11TerritoryPublication? geographicTerritory;
    private GeographicAdvantagesView? geographicView;
    private ulong geographicRevision;

    internal int? UserDisplayMode => _mapPresentation?.Policy.Mode;
    internal int? SelectedSummaryProvinceId => geographicView?.SelectedProvinceId;
    internal Control? GeographicSummaryMount(int slot) => geographicView?.SummaryMount(slot);
    private void OnGeographicProvinceSelected(int provinceId) => InvalidateMapSummaries();

    internal bool TrySetGeographicAdvantages(bool visible, GeographicAdvantagePublication? publication,
        out string? missing)
    {
        missing=null;
        if(_mapPresentation is null || !IsPlayableMapReady || _periodMapActive || _detailsOpen ||
            storyPresentationOwner is not null || uniqueDirectionOwner is not null ||
            _sourceMainCamera is not {CurrentRuntime.Transition:null,CurrentRuntime.RequestMode188:0} camera)
            return false;
        if((UserDisplayMode==1)==visible)
        {SetSummaryGeographySelected(visible);return true;}
        if(visible)
        {
            if(publication is null){missing="geographic-advantages.publication";return false;}
            try
            {
                ApplyGeographicAdvantages(publication);
                if(geographicView is null)
                {
                    var candidate=new GeographicAdvantagesView{Name="GeographicAdvantages",ZIndex=4,Visible=false};
                    var parent=StrategicMapOverlayParent();
                    parent.AddChild(candidate);
                    candidate.SetSummaryOwnership(true);
                    try {candidate.Configure(_repository,_text,publication);}
                    catch {parent.RemoveChild(candidate);candidate.QueueFree();throw;}
                    geographicView=candidate;
                    candidate.ProvinceSelected+=OnGeographicProvinceSelected;
                }
            }
            catch(Exception error) when(error is InvalidDataException or NotSupportedException)
            {missing=error.Message;return false;}
            var p=camera.CurrentState;
            var prior=new CampaignCameraSnapshot((float)p.FocusX,(float)p.FocusY,(float)p.FocusZ,(float)p.FocusW,
                p.Distance90,p.YawA8,p.PitchC0);
            var request=new MainMapCameraControllerRequest(500,new(102400,0,102400,1),
                camera.Parameters.ZoomAnchor(6),BitConverter.Int64BitsToDouble(0x404F6A7A2966D6A4),-1,1);
            if(!camera.QueueTimedRequest(request,unchecked((int)Time.GetTicksMsec())))return false;
            geographicPriorCamera=prior;
        }
        else
        {
            if(geographicPriorCamera is not {} p){missing="geographic-advantages.saved_camera";return false;}
            if(!camera.QueueTimedRequest(new(500,new(p.FocusX,p.FocusY,p.FocusZ,p.FocusW),
                p.Distance,p.Yaw,p.Pitch,1),unchecked((int)Time.GetTicksMsec())))return false;
            geographicPriorCamera=null;
        }
        ResetSourceCameraInput();CloseCityCommandMenu();CloseAreaHexCommandMenu();
        _mapPresentation.SetGeographicAdvantagesVisible(visible);
        if(geographicView is not null)geographicView.Visible=visible;
        SetSummaryGeographySelected(visible);
        ApplyKeyboardMapInformation();ScheduleD3D11MapFrame();
        return true;
    }

    internal void ApplyGeographicAdvantages(GeographicAdvantagePublication publication)
    {
        if(ReferenceEquals(geographicPublication,publication))return;
        if(_d3d11Ownership is null || ForcePalette is not {} palette)
            throw new InvalidDataException("geographic-advantages.terrain_palette");
        if(publication.HexProvinceColorIndices.Count!=D3D11TerritoryPublisher.CellCount)
            throw new InvalidDataException("geographic-advantages.hex_colors");
        var colors=publication.HexProvinceColorIndices.ToArray();
        for(int i=0;i<colors.Length;i++)if(_d3d11Ownership.StaticMap.TerrainFlags[i]==0)colors[i]=60;
        var mapColors=new float[60*4];
        for(int i=0;i<60;i++)
        {
            var color=palette.MapColor(i);int at=i*4;
            mapColors[at]=color.RedFloat;mapColors[at+1]=color.GreenFloat;
            mapColors[at+2]=color.BlueFloat;mapColors[at+3]=color.AlphaFloat;
        }
        geographicTerritory=new(colors,0x8000000000000000UL|++geographicRevision,mapColors);
        geographicPublication=publication;
        geographicView?.Refresh(publication);
    }

    private D3D11TerritoryPublication? GeographicTerritory(GameState world) =>
        UserDisplayMode==1?geographicTerritory:null;

    internal void ClearGeographicAdvantages()
    {
        geographicPriorCamera=null;geographicPublication=null;geographicTerritory=null;
        _mapPresentation?.SetGeographicAdvantagesVisible(false);
        SetSummaryGeographySelected(false);
        if(geographicView is not null)
        {
            geographicView.ProvinceSelected-=OnGeographicProvinceSelected;
            geographicView.QueueFree();geographicView=null;
        }
    }
}
