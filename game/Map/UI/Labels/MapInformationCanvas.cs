using Godot;
using San14.Data;

/// <summary>Projects captured information onto the accepted native Main camera.
/// Geometry and resource identities refresh with the publication, not the camera.</summary>
internal partial class MapInformationCanvas : Control
{
    private static readonly Vector2 CanonicalSize = new(1920,1080);
    private static readonly int[] DiplomacyDisplayLevels=[0,0,0,0,1,2,2];
    private readonly Node2D root = new() { Name="InformationCards" };
    private readonly List<(Vector3[] Points,Color Color)> links=[];
    private readonly List<(MapDiplomacyCardView Card,BaseReference? Base,bool Own,int Force)> cards=[];
    private D3D11MapMarkers bucket=null!;
    private readonly List<(Vector2[] Points,Color Color)> screenLinks=[];
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Shader shader=null!;
    private MapDiplomacyLayouts diplomacyLayouts=null!;
    private Func<int,Vector3>? worldPosition;
    private Func<bool>? admitted;
    private CeForceMapPalette? palette;
    private MapInformationLayerSnapshot? captured;
    private bool retired;
    private Func<int,bool>? selectionAllowed;
    private Action<int>? selectionPicked;
    private IReadOnlyDictionary<int,AreaAdministrationQuery<IReadOnlyList<MapDiplomacyMarker>>>? intelligenceMarkers;
    private bool intelligenceSpyTags;
    internal void SetIntelligenceForceMarkers(IReadOnlyDictionary<int,AreaAdministrationQuery<IReadOnlyList<MapDiplomacyMarker>>>? markers,bool spy)
    {
        if(ReferenceEquals(intelligenceMarkers,markers)&&intelligenceSpyTags==spy)return;
        intelligenceMarkers=markers;intelligenceSpyTags=spy;
        foreach(var entry in cards)ApplyIntelligenceMarkers(entry.Card,entry.Force);
    }
    private void ApplyIntelligenceMarkers(MapDiplomacyCardView card,int force)
    {
        AreaAdministrationQuery<IReadOnlyList<MapDiplomacyMarker>>? source=intelligenceMarkers?.GetValueOrDefault(force);
        card.SetIntelligenceForceMarkers(intelligenceSpyTags,source?.Value,source?.UnresolvedInput??
            (intelligenceSpyTags&&source is null?"intelligence.selection.spy_markers.owner":null));
    }
    internal void SetDiplomacyTargetSelection(Func<int,bool>? allowed,Action<int>? picked)
    {
        selectionAllowed=allowed;selectionPicked=picked;
        foreach(var entry in cards)ApplySelection(entry.Card,entry.Force);
    }
    private void ApplySelection(MapDiplomacyCardView card,int force)
        =>card.SetSelectionAction(selectionAllowed?.Invoke(force),selectionPicked is {} picked?()=>picked(force):null);

    internal void Configure(GameResourceRepository repository,PreviewText text,D3D11MapMarkers bucket,
        Func<int,Vector3> worldPosition,CeForceMapPalette? palette,Func<bool> admitted)
    {
        this.repository=repository;this.bucket=bucket;
        this.worldPosition=worldPosition;this.palette=palette;this.admitted=admitted;
        MouseFilter=MouseFilterEnum.Ignore;ClipContents=true;
        assets=new(repository);font=new(repository,text.SourceLocale);
        shader=UiSourceSprite.CreateShader();diplomacyLayouts=new(repository);
        AddChild(root);
        Resized+=ResizeCards;
        ResizeCards();
        SetMeta("information_layer_scope","Actual neighbor control Hexes and capital anchors, with source diplomacy layouts and current data; native occlusion remains separate.");
    }

    public override void _Process(double delta)
    {
        if(!retired)
        {
            Visible=admitted?.Invoke()==true;
            foreach(var entry in cards){entry.Card.SetSourceAdmitted(Visible);entry.Card.SetSelectionAdmission(selectionAllowed?.Invoke(entry.Force));}
        }
    }

    internal void Present(MapInformationLayerSnapshot? publication,MainMapCameraFrame frame,
        bool neighbors,bool diplomacy,SceneryMapCapture? projection)
    {
        if(retired)return;
        if(!ReferenceEquals(captured,publication))Capture(publication);
        screenLinks.Clear();
        if(neighbors&&frame.ZoomSelection.NativeReturnedIndex<5)
        {
            foreach(var link in links)
            {
                // A rejected endpoint splits the line rather than joining across the near plane.
                var run=new List<Vector2>(link.Points.Length);
                foreach(var point in link.Points)
                {
                    if(D3D11MapMarkers.TryProject(point,frame.Camera.Matrices.WorldToProjection100,out var screen))
                        run.Add(screen);
                    else Flush();
                }
                Flush();
                void Flush()
                {
                    if(run.Count>1)screenLinks.Add((run.ToArray(),link.Color));
                    run.Clear();
                }
            }
        }
        bucket.SetInformationPlayer(publication?.CurrentPlayerPhysical);
        var policy=projection?.Presentation.Policy;
        int level=MapLabelPresentation.PrimaryCameraLevel(frame.ZoomSelection);
        int diplomacyDisplay=diplomacy&&policy?.Mode is 0 or 7 or 11?DiplomacyDisplayLevels[level]:-1;
        foreach(var entry in cards)
        {
            bool positionKnown=bucket.TryReadBaseUnitPosition(entry.Base,frame,out var unit);
            MapControlScreenPoint? projected=null;
            if(positionKnown&&projection is not null&&ReferenceEquals(projection.Camera,frame))
            {
                bool? bypass=projection.Presentation.ProjectionClipBypass;
                projected=MapControlProjection.Project(unit,new(0,CityGateMapMarkerSources.WorldHeightOffset,0),
                    frame.Camera.SourceWorldToProjection,projection.Viewport,bypass==true,projection.Surface);
                // An admitted strict point is identical on both bypass branches.
                if(projected is null&&bypass is null)entry.Card.SetMeta("unresolved_projection","map-diplomacy.clip-owner");
            }
            float fade=positionKnown?MapLabelPresentation.DistanceFade(new(unit.X,unit.Y,unit.Z),
                frame.Camera.CurrentPose.Position80,frame.ControllerState.Distance90):float.PositiveInfinity;
            bool visible=projected is not null&&!(level<4&&fade>1f);
            entry.Card.SetSourceAdmitted(admitted?.Invoke()==true);
            entry.Card.SetDisplay(visible?diplomacyDisplay:-1);
            if(projected is {} screen)entry.Card.Position=new Vector2(screen.X,screen.Y)+entry.Card.ProjectionOffset;
            if(positionKnown)bucket.SetDiplomacyOrder(entry.Card,MapLabelPresentation.ControlSortKey(frame,
                new(unit.X,unit.Y,unit.Z),2,entry.Own,false,false));
            else entry.Card.SetMeta("unresolved_projection","map-diplomacy.accepted-Base-unit");
        }
        Visible=admitted?.Invoke()==true;
        bucket.RefreshInformationOrder();
        QueueRedraw();
    }

    private void Capture(MapInformationLayerSnapshot? publication)
    {
        foreach(var entry in cards)bucket.RemoveDiplomacyCard(entry.Card);
        cards.Clear();links.Clear();captured=publication;
        if(publication is null)return;
        foreach(var link in publication.Links)
        {
            Color color=Colors.White;
            if(link.CommonForceColor is byte index&&index<60&&palette is not null)
            {
                var c=palette.FirstTableColor(index,2);
                color=new(c.Red/255f,c.Green/255f,c.Blue/255f,1);
            }
            links.Add((link.Hexes.Select(hex=>worldPosition!(hex)).ToArray(),color));
        }
        foreach(var row in publication.Diplomacy)
        {
            var card=new MapDiplomacyCardView {Name=$"DiplomacyForce{row.ForceId}"};
            bucket.AddDiplomacyCard(card);
            try
            {
                card.Configure(repository,assets,shader,font,diplomacyLayouts,row);
                bucket.AddDiplomacyDrawingParts(card);
                if(row.UnresolvedInput is {} missing)card.SetMeta("unresolved_input",missing);
            }
            catch
            {
                bucket.RemoveDiplomacyCard(card);
                throw;
            }
            cards.Add((card,row.CapitalBase,row.IsCurrentPlayerForce==true,row.ForceId));ApplySelection(card,row.ForceId);ApplyIntelligenceMarkers(card,row.ForceId);
        }
    }

    private void ResizeCards()=>root.Scale=Size/CanonicalSize;

    public override void _Draw()
    {
        DrawSetTransform(Vector2.Zero,0,Size/CanonicalSize);
        foreach(var line in screenLinks)DrawPolyline(line.Points,line.Color,1,true);
        DrawSetTransform(Vector2.Zero);
    }

    internal void Abandon()
    {
        if(retired)return;
        retired=true;Visible=false;SetProcess(false);admitted=null;worldPosition=null;
        Resized-=ResizeCards;
        foreach(var entry in cards)bucket.RemoveDiplomacyCard(entry.Card);
        cards.Clear();links.Clear();screenLinks.Clear();captured=null;
        font?.Dispose();assets?.Dispose();shader?.Dispose();
    }
}
