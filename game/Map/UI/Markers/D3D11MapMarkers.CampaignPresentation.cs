using System.Globalization;
using Godot;
using San14.Data;

internal partial class D3D11MapMarkers
{
    private CampaignMapPresentation? campaignPresentation;
    private readonly Dictionary<(Kind Kind,int Location),CampaignMapMarkerPresentation> campaignRows=[];
    private Func<int,MainMapCameraFrame,Vector2?>? campaignDistrictProjector;
    private UiSourceLayout? campaignDistrictFlag;
    private UiSourceLayout? campaignDistrictFull, campaignDistrictFlagGeometry;
    private readonly Dictionary<int,Texture2D> campaignDistrictSprites=[];
    private MapFontProfile[] campaignDistrictLetters=[],campaignDistrictNames=[];
    private readonly Dictionary<int,MapMarkerSlices> campaignDistrictPanels=[];
    private readonly Dictionary<Marker,Control> campaignDistrictFaces=[];
    private Shader? campaignDistrictPortraitShader;

    internal void SetCampaignPresentation(CampaignMapPresentation? publication,
        IReadOnlySet<int>? selectedHexes=null,Func<int,MainMapCameraFrame,Vector2?>? projectDistrict=null)
    {
        if(publication is null){SetCampaignMarkers(null);return;}
        if(publication.Stage==CampaignDeploymentMapStage.District)
        {
            if(projectDistrict is null)throw new InvalidDataException("campaign.District_actual_projection_owner");
            PrepareCampaignDistrictResources();
        }
        var next=new Dictionary<(Kind,int),CampaignMapMarkerPresentation>();
        foreach(var row in publication.Markers)
        {
            Kind kind=row.Kind==BaseKind.City?Kind.City:Kind.Gate;
            int location=row.PhysicalSlot+(row.Kind==BaseKind.Gate?51:0);
            if(location!=row.LocationId)throw new InvalidDataException("campaign.marker_receiver_identity");
            if(publication.Stage==CampaignDeploymentMapStage.District&&
                (row.DistrictNumber is null||row.ForceColor49 is null))
                throw new InvalidDataException("campaign.District_style_owner");
            next.Add((kind,location),row);
        }
        var preparedFaces=publication.Stage==CampaignDeploymentMapStage.District
            ? PrepareCampaignDistrictFaces(publication) : new Dictionary<Marker,Control>();
        ClearCampaignPresentationControls();
        campaignPresentation=publication;campaignDistrictProjector=projectDistrict;
        campaignRows.Clear();foreach(var row in next)campaignRows.Add(row.Key,row.Value);
        ApplyCampaignMarkerSets(publication.Markers.Select(row=>row.HexId).ToHashSet(),selectedHexes);
        foreach(var face in preparedFaces)
        {baseInformationInk[face.Key].Ink.AddChild(face.Value);campaignDistrictFaces.Add(face.Key,face.Value);}
        if(_frame is {} frame)ProjectMarkers(frame);
        QueueMarkerRedraw();
    }

    private void ClearCampaignPresentationControls()
    {
        foreach(var face in campaignDistrictFaces.Values)
        {face.GetParent()?.RemoveChild(face);face.Free();}
        campaignDistrictFaces.Clear();campaignPresentation=null;campaignRows.Clear();campaignDistrictProjector=null;
    }

    private void ReleaseCampaignPresentation()
    {ClearCampaignPresentationControls();campaignDistrictPortraitShader?.Dispose();campaignDistrictPortraitShader=null;}

    private CampaignMapMarkerPresentation? CampaignRow(Marker marker)=>
        campaignRows.GetValueOrDefault((marker.Kind,marker.Id));
    private bool CampaignMarkerDisabled(Marker marker)=>campaignPresentation is not null&&
        CampaignRow(marker) is {Enabled:false};

    private CityGateMapLabelPresentation ReadCampaignCityPresentation(Marker marker,CityGateMapLabelPresentation value)
    {
        if(campaignPresentation is null)return value;
        if(campaignPresentation.Stage==CampaignDeploymentMapStage.District)
            return value with{Display=value.CameraLevel<5?0:2,DistanceFade=0,PanelAlpha=255,FlagAlpha=255};
        //19E800 mode5: enabled keeps the actual global camera display;
        //disabled is flag-only2. Neither mode9 nor a synthetic camera is used.
        return CampaignMarkerDisabled(marker)?value with{Display=2}:value;
    }

    private bool ProjectCampaignDistrictMarker(Marker marker,MainMapCameraFrame frame)
    {
        if(campaignPresentation?.Stage!=CampaignDeploymentMapStage.District||marker.Kind==Kind.Area)return false;
        Vector2? point=CampaignRow(marker) is {} row?campaignDistrictProjector!(row.HexId,frame):null;
        marker.CityPresentation=ReadCampaignCityPresentation(marker,MapLabelPresentation.CityGate(frame,marker.CityUnitPosition));
        marker.Screen=point??default;marker.OnScreen=point is not null&&marker.PositionReady;
        if(campaignDistrictFaces.TryGetValue(marker,out var face))
        {
            face.Visible=marker.OnScreen;
            face.Scale=Size/CanonicalSize;
            if(point is {} p)face.Position=(p+new Vector2(-20,-40)+campaignDistrictFull!.Rectangle(79).Position)*face.Scale;
        }
        return true;
    }

    private void PrepareCampaignDistrictResources()
    {
        if(campaignDistrictFlag is not null)return;
        if(_assets is null||_font is null||_textPresets is null||_citySources is null)
            throw new InvalidDataException("campaign.marker_resources");
        var flag=new UiSourceLayout(MapMarkerLayout.LoadNamed(tacticRepository,"00_81_07_0110_btn_city_offset_00"));
        var full=_assets.SourceLayout(0xEDC35713);
        var sprites=new Dictionary<int,Texture2D>();
        foreach(int id in new[]{3,4,6,7,11,12,16,17})
        {
            var node=San14.Data.Formats.LcskSpriteNodeReader.Read(flag.Source.Layout,flag.Nodes[id]);
            var part=node.Groups.Single().Parts.Single();
            var declared=_assets.SourceLayoutSprite(flag.Source,part.SpriteName);
            sprites.Add(id,_assets.SpritePart(part.SpriteName,part.Uv,declared));
        }
        var letter=MapFontProfile.FromNode(flag.Nodes[18],flag.Source.Source.ResourceUri);
        var name=MapFontProfile.FromNode(_citySources.Simple.Nodes[14],_citySources.Simple.Source.ResourceUri);
        MapFontProfile[] letters=[letter,letter.WithPreset(_textPresets[4]),letter.WithPreset(_textPresets[5]),letter];
        MapFontProfile[] names=[name.WithPreset(_textPresets[0]),name.WithPreset(_textPresets[4]),
            name.WithPreset(_textPresets[5]),name.WithPreset(_textPresets[6])];
        var panels=new Dictionary<int,MapMarkerSlices>();
        foreach(int id in new[]{3,5,6,8,9,11})panels.Add(id,MapMarkerSlices.Load(_citySources.Simple,id,_assets));
        foreach(var profile in letters)_font.Prepare(["0123456789"],profile);
        campaignDistrictFlag=campaignDistrictFlagGeometry=flag;campaignDistrictFull=full;
        campaignDistrictLetters=letters;campaignDistrictNames=names;
        foreach(var row in sprites)campaignDistrictSprites.Add(row.Key,row.Value);
        foreach(var row in panels)campaignDistrictPanels.Add(row.Key,row.Value);
    }

    private Dictionary<Marker,Control> PrepareCampaignDistrictFaces(CampaignMapPresentation publication)
    {
        campaignDistrictPortraitShader??=UiSourceSprite.CreateShader();
        var result=new Dictionary<Marker,Control>();
        var full=campaignDistrictFull!;Vector2 mount=full.Rectangle(79).Position;
        try
        {
            foreach(var row in publication.Markers)
            {
                if(row.DistrictLeaderFace is not {} face)continue;
                Kind kind=row.Kind==BaseKind.City?Kind.City:Kind.Gate;
                if(!_markers.TryGetValue((kind,row.LocationId),out var marker))continue;
                if(!baseInformationInk.ContainsKey(marker))throw new InvalidDataException("campaign.District_ink_owner");
                var pane=new Control{Name="CampaignDistrictLeaderFace",MouseFilter=MouseFilterEnum.Ignore,Size=full.Rectangle(79).Size,Visible=false};
                result.Add(marker,pane);
                full.Art(pane,80,new(full.Rectangle(80).Position-mount,full.Rectangle(80).Size),_assets!,campaignDistrictPortraitShader);
                if(face.Portrait.Kind==OfficerPortraitRouteKind.StockNamed)
                {
                    var sprite=new LocalizedUiSpriteSource(tacticRepository).LoadTexturePackSprite(face.Portrait.ResourceName!,face.Portrait.SpriteName!);
                    var portrait=new UiSourcePortrait{Position=full.Rectangle(82).Position-mount,Size=full.Rectangle(82).Size};
                    pane.AddChild(portrait);portrait.ConfigureLayoutPane(tacticRepository,_assets!,campaignDistrictPortraitShader,sprite,full,82);
                }
                else if(face.Portrait.Kind!=OfficerPortraitRouteKind.Hidden)
                    throw new InvalidDataException(face.Portrait.UnresolvedInput??"campaign.District_face_route");
                full.Art(pane,83,new(full.Rectangle(83).Position-mount,full.Rectangle(83).Size),_assets!,campaignDistrictPortraitShader);
            }
            return result;
        }
        catch
        {
            foreach(var pane in result.Values)pane.Free();
            throw;
        }
    }

    private bool DrawCampaignDistrictMarker(CanvasItem canvas,Marker marker)
    {
        if(campaignPresentation?.Stage!=CampaignDeploymentMapStage.District)return false;
        if(CampaignRow(marker) is not {} row)return true;
        Vector2 point=marker.Screen,root=point+new Vector2(-20,-40);
        int state=!row.Enabled?3:_hoveredHex==marker.Hex?(_pressedHex==marker.Hex?2:1):0;
        var fill=MapSpritePalettes.ForceFill(row.ForceColor49!.Value);
        var outline=_areaSources!.DistrictColor(row.DistrictNumber!.Value);
        var letter=_spritePalettes!.ForceLetter(row.ForceColor49.Value);
        int[] outlineNodes=[3,6,11,16],fillNodes=[4,7,12,17];
        Draw(outlineNodes[state],state==3?Dim(outline):outline);
        Draw(fillNodes[state],state==3?Dim(fill):fill);
        string number=row.DistrictNumber.Value.ToString(CultureInfo.InvariantCulture);
        var glyphBox=campaignDistrictFlagGeometry!.Rectangle(18);
        var glyph=campaignDistrictLetters[state];
        _font!.Draw(canvas,number,root+glyphBox.Position+new Vector2(0,_font.CellBaselineY(glyph,glyphBox.Size.Y)),glyph,HorizontalAlignment.Center,glyphBox.Size.X,
            modulate:state is 1 or 2?Colors.White:CanvasColor(state==3?Dim(letter):letter));
        if(marker.CityPresentation.CameraLevel<5)
        {
            var geometry=CityGateMapMarkerSources.SimpleGeometry(marker.Name,false,false);
            int[] nodes=state switch{0=>[3],1=>[5,6],2=>[8,9],_=>[11]};
            foreach(int id in nodes)campaignDistrictPanels[id].Draw(canvas,AreaRectangle(geometry.Plate,point),_spritePalettes[state==0||state==3?4:state==1?5:6],255);
            var profile=campaignDistrictNames[state];var cell=AreaRectangle(geometry.TextCell,point);
            _font.Draw(canvas,marker.Name,cell.Position+new Vector2(0,_font.CellBaselineY(profile,cell.Size.Y)),profile,HorizontalAlignment.Center,cell.Size.X);
        }
        return true;

        void Draw(int node,AreaMarkerColor color)
        {
            var box=campaignDistrictFlagGeometry.Rectangle(node);var rect=new Rect2(root+box.Position,box.Size);
            if(state is 1 or 2)PaletteSprite(canvas,campaignDistrictSprites[node],rect,_spritePalettes![state==1?5:6],255);
            else canvas.DrawTextureRect(campaignDistrictSprites[node],rect,false,CanvasColor(color));
        }
        static AreaMarkerColor Dim(AreaMarkerColor color)=>new(
            AreaMapMarkerSources.DarkenRgb(color.AuthoredPacked),AreaMapMarkerSources.DarkenRgb(color.CpuPacked));
    }

    private bool TryReadCampaignHitRectangle(Marker marker,MainMapCameraFrame frame,out Rect2 rectangle)
    {
        rectangle=default;
        if(!marker.PositionReady||CampaignRow(marker) is not {Enabled:true} row||DirectionSuppresses(marker))return false;
        if(campaignPresentation!.Stage==CampaignDeploymentMapStage.District)
        {
            if(campaignDistrictProjector!(row.HexId,frame) is not {} point)return false;
            //195FF0 uses the name child's tester at every District display
            //level.877440 sets root width to that tester width.8734B0 tests
            //both root and tester; their two-pixel X offset clips the right.
            var hit=CityGateMapMarkerSources.SimpleGeometry(marker.Name,false,false).Hit;
            rectangle=AreaRectangle(hit with{Width=hit.Width-2},point);return true;
        }
        var presentation=ReadCampaignCityPresentation(marker,ReadTargetCityPresentation(marker,frame));
        if(!presentation.Visible||!TryProject(marker.Position,frame.Camera.Matrices.WorldToProjection100,out var p))return false;
        rectangle=AreaRectangle(CityGateMapMarkerSources.NormalHitRectangle(presentation.Display,marker.Troops,marker.Kind==Kind.Gate),p);
        return true;
    }
}
