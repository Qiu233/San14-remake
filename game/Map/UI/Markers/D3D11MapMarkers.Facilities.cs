using Godot;
using San14.Core;
using San14.Data;
using NVector3=System.Numerics.Vector3;

/// <summary>Immutable same-frame Object fields. No mutable campaign owner is
/// retained across asynchronous native submission and acceptance.</summary>
internal sealed record FacilityMapInfoPublication(D3D11FacilityPublication Models,
    IReadOnlyDictionary<int,AreaAdministrationQuery<FacilityMapInfo>> Rows);

internal partial class D3D11MapMarkers
{
    private readonly Dictionary<int,FacilityMapInfoView> facilityInfoViews=[];
    private readonly Dictionary<int,NVector3> facilityInfoUnits=[];
    private Func<GameState,int,AreaAdministrationQuery<FacilityMapInfo>>? facilityInfoQuery;
    private GameResourceRepository? facilityInfoRepository;
    private Shader? facilityInfoShader;
    private FacilityMapInfoPublication? facilityInfoPublication;

    internal void SetFacilityMapInfoQuery(Func<GameState,int,AreaAdministrationQuery<FacilityMapInfo>> query)
        =>facilityInfoQuery=query;

    private void ConfigureFacilityInfo(GameResourceRepository repository)
    { facilityInfoRepository=repository;facilityInfoShader=UiSourceSprite.CreateShader(); }

    /// <summary>Only the accepted native frame publishes Object18. Connection
    /// models are distinct source units and cannot impersonate the primary.</summary>
    internal FacilityMapInfoPublication? CaptureFacilityInfo(GameState world,D3D11FacilityPublication? publication)
    {
        if(publication is not {IsReady:true})return null;
        var rows=new Dictionary<int,AreaAdministrationQuery<FacilityMapInfo>>();
        foreach(var row in publication.Rows)
            if(row.Identity=="facility:"+row.ReceiverId&&row.Instance.SourceWorldState is not null)
                rows[row.ReceiverId]=facilityInfoQuery?.Invoke(world,row.ReceiverId)
                    ??new(null,"facility.info.current_Object_query");
        return new(publication,new System.Collections.ObjectModel.ReadOnlyDictionary<int,AreaAdministrationQuery<FacilityMapInfo>>(rows));
    }

    internal void PresentFacilities(FacilityMapInfoPublication? publication)
    {
        if(publication is null||ReferenceEquals(publication,facilityInfoPublication))return;
        facilityInfoPublication=publication;
        facilityInfoUnits.Clear();
        foreach(var row in publication.Models.Rows)
            if(row.Identity=="facility:"+row.ReceiverId&&row.Instance.SourceWorldState is { } source)
                facilityInfoUnits[row.ReceiverId]=source.Position18;
        foreach(int receiver in facilityInfoViews.Keys.Where(id=>!facilityInfoUnits.ContainsKey(id)).ToArray())
            RetireFacilityInfo(receiver);
        foreach(int receiver in facilityInfoUnits.Keys)
        {
            if(publication.Rows.TryGetValue(receiver,out var query))
                ApplyFacilityInfo(receiver,query,out _);
        }
        PositionFacilityInfo();
    }

    internal bool TryNotifyFacilityCreated(GameState world,int receiverId,bool sourceUnitPresent,out string? missing)
    {
        if(!sourceUnitPresent)
        { RetireFacilityInfo(receiverId);missing=null;return true; }
        if(facilityInfoQuery is null)
        { missing="facility.info.current_Object_query";return false; }
        return ApplyFacilityInfo(receiverId,facilityInfoQuery(world,receiverId),out missing);
    }

    internal bool TryNotifyFacilityCreatedFromAccepted(D3D11FacilityPublication expectedModels,
        int receiverId,bool sourceUnitPresent,out string? missing)
    {
        if(!ReferenceEquals(expectedModels,facilityInfoPublication?.Models))
        { missing="facility.info.expected_native_publication_not_accepted";return false; }
        if(!sourceUnitPresent)
        { RetireFacilityInfo(receiverId);missing=null;return true; }
        if(!facilityInfoPublication!.Rows.TryGetValue(receiverId,out var query))
        { missing="facility.info.accepted_Object_fields";return false; }
        return ApplyFacilityInfo(receiverId,query,out missing);
    }

    private bool ApplyFacilityInfo(int receiverId,AreaAdministrationQuery<FacilityMapInfo> query,out string? missing)
    {
        if(facilityInfoRepository is null||facilityInfoShader is null||_assets is null||_font is null||
            _textPresets is null||_spritePalettes is null||_citySources is null||_barPanel is null||!IsInsideTree())
        { missing="facility.info.actual_map_UI_root";return false; }
        if(!facilityInfoUnits.ContainsKey(receiverId))
        { missing="facility.info.accepted_primary_SourceUnit";return false; }
        if(!query.Available)
        { RetireFacilityInfo(receiverId);missing=query.UnresolvedInput;return false; }
        if(query.Value is not { } info)
        { RetireFacilityInfo(receiverId);missing=null;return true; }
        if(facilityInfoViews.TryGetValue(receiverId,out var prior)&&
            (prior.DefinitionKind!=info.DefinitionKind||prior.DefinitionCategory!=info.DefinitionCategory||
             prior.FacilityName!=info.Name||prior.ForceColor49!=info.ForceColor49||prior.Outline!=info.Outline))
        { RetireFacilityInfo(receiverId);prior=null; }
        if(prior is not null)prior.Apply(info);
        else
        {
            FacilityMapInfoView? view=null;
            try
            {
                var icons=info.DefinitionCategory==3?null:FacilitiesInfoSource.IconNames(info.DefinitionKind,info.Outline!.UsesOutline);
                view=new();AddChild(view);
                view.Configure(facilityInfoRepository,_assets,facilityInfoShader,_font,_textPresets,_spritePalettes,
                    _citySources,_barPanel,info,icons?.Fill,icons?.Outline);
                // Register the actual owned, bound control only after both
                // source child layouts and their resource declarations exist.
                facilityInfoViews.Add(receiverId,view);
            }
            catch(Exception error)when(error is InvalidDataException or NotSupportedException)
            {
                if(view is not null) { RemoveChild(view);view.QueueFree(); }
                missing=error.Message;return false;
            }
        }
        PositionFacilityInfo();missing=null;return true;
    }

    private void PositionFacilityInfo()
    { if(_frame is { } frame)PositionFacilityInfo(frame); }

    private void PositionFacilityInfo(MainMapCameraFrame frame)
    {
        foreach(var (receiver,view) in facilityInfoViews)
        {
            if(!facilityInfoUnits.TryGetValue(receiver,out var unit)) { view.Hide();continue; }
            int level=MapLabelPresentation.SettledCameraLevel(frame.ZoomSelection);
            int mode=level<4?0:level==4?1:-1;
            var eye=frame.Camera.CurrentPose.Position80;
            float fade=MapLabelPresentation.DistanceFade(unit,new(eye.X,eye.Y,eye.Z),frame.ControllerState.Distance90);
            bool projected=TryProject(new(unit.X,unit.Y+900,unit.Z),frame.Camera.Matrices.WorldToProjection100,out var screen);
            view.Visible=projected&&mode!=-1&&!(level<4&&fade>1)&&_baseInformationVisible&&
                campaignMarkerHexes is null&&FacilityInformationModeAllows;
            if(!view.Visible)continue;
            view.Scale=Size/CanonicalSize;
            view.Position=(screen-new Vector2(view.ProjectionLeftOffset,0))*view.Scale;
            view.Present(mode,MapLabelPresentation.TextAlpha(level,fade),MapLabelPresentation.FlagAlpha(level,fade),
                _hoveredHex==view.HexId,_pressedHex==view.HexId);
        }
    }

    private bool TryPickFacility(Vector2 localPoint,MainMapCameraFrame frame,out int hexId)
    {
        hexId=-1;
        if(!_baseInformationVisible||campaignMarkerHexes is not null||!FacilityInformationModeAllows||
            !IsVisibleInTree()||Size.X<=0||Size.Y<=0)return false;
        int level=MapLabelPresentation.SettledCameraLevel(frame.ZoomSelection);
        int mode=level<4?0:level==4?1:-1;
        if(mode<0)return false;
        var eye=frame.Camera.CurrentPose.Position80;
        Vector2 cursor=localPoint/Size*CanonicalSize;
        foreach(var (receiver,view) in facilityInfoViews.Reverse())
        {
            if(!facilityInfoUnits.TryGetValue(receiver,out var unit))continue;
            float fade=MapLabelPresentation.DistanceFade(unit,new(eye.X,eye.Y,eye.Z),frame.ControllerState.Distance90);
            if(level<4&&fade>1||!TryProject(new(unit.X,unit.Y+900,unit.Z),frame.Camera.Matrices.WorldToProjection100,out var p))continue;
            if(view.HasPoint(cursor-(p-new Vector2(view.ProjectionLeftOffset,0)),mode))
            {hexId=view.HexId;return true;}
        }
        return false;
    }

    private void RetireFacilityInfo(int receiver)
    {
        if(!facilityInfoViews.Remove(receiver,out var old))return;
        old.Hide();RemoveChild(old);old.QueueFree();
    }

    internal bool TryConfirmObjectRetirementFromAccepted(D3D11FacilityPublication expected,int receiver,out string? missing)
    {
        if(!ReferenceEquals(facilityInfoPublication?.Models,expected))
        {missing="Object.retirement.expected_native_publication_not_presented";return false;}
        if(expected.Source.KnownReceivers.Contains(receiver)||expected.Rows.Any(row=>row.ReceiverId==receiver)||
            facilityInfoUnits.ContainsKey(receiver)||facilityInfoViews.ContainsKey(receiver))
        {missing="Object.retirement.model_or_paired_view_still_attached";return false;}
        missing=null;return true;
    }
    private void ReleaseFacilityInfo()
    {
        foreach(int receiver in facilityInfoViews.Keys.ToArray())RetireFacilityInfo(receiver);
        facilityInfoUnits.Clear();facilityInfoPublication=null;
        facilityInfoShader?.Dispose();facilityInfoShader=null;facilityInfoRepository=null;
    }
}
