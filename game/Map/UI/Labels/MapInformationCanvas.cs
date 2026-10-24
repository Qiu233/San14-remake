using Godot;
using San14.Data;

/// <summary>Projects captured information onto the accepted native Main camera.
/// Geometry and resource identities refresh with the publication, not the camera.</summary>
internal partial class MapInformationCanvas : Control
{
    private static readonly Vector2 CanonicalSize = new(1920,1080);
    private readonly Node2D root = new() { Name="InformationCards" };
    private readonly List<(Vector3[] Points,Color Color)> links=[];
    private readonly List<(Control Card,Vector3 Position)> cards=[];
    private readonly List<(Vector2[] Points,Color Color)> screenLinks=[];
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private UiAssets assets=null!;
    private MapFont font=null!;
    private MapFontProfile profile=null!;
    private Shader shader=null!;
    private LocalizedUiSpriteSource sprites=null!;
    private Func<int,Vector3>? worldPosition;
    private Func<bool>? admitted;
    private CeForceMapPalette? palette;
    private MapInformationLayerSnapshot? captured;
    private int? playerForce;
    private bool retired;

    internal void Configure(GameResourceRepository repository,PreviewText text,int? playerForce,
        Func<int,Vector3> worldPosition,CeForceMapPalette? palette,Func<bool> admitted)
    {
        this.repository=repository;this.text=text;this.playerForce=playerForce;
        this.worldPosition=worldPosition;this.palette=palette;this.admitted=admitted;
        MouseFilter=MouseFilterEnum.Ignore;ClipContents=true;
        assets=new(repository);font=new(repository,text.SourceLocale);
        shader=UiSourceSprite.CreateShader();sprites=new(repository);
        var source=ArmyMapMarkerSources.Load(repository);
        profile=MapFontProfile.FromNode(source.Normal.Nodes[0x32],source.Normal.Source.ResourceUri)
            .WithPreset(MapTextPresets.Create()[0]);
        AddChild(root);
        Resized+=ResizeCards;
        ResizeCards();
        SetMeta("information_layer_scope","Actual neighbor control Hexes and capital anchors; native occlusion and original card layout remain to be calibrated.");
    }

    public override void _Process(double delta)
    {
        if(!retired)Visible=admitted?.Invoke()==true;
    }

    internal void Present(MapInformationLayerSnapshot? publication,MainMapCameraFrame frame,
        bool neighbors,bool diplomacy)
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
        foreach(var entry in cards)
        {
            bool projected=D3D11MapMarkers.TryProject(entry.Position,frame.Camera.Matrices.WorldToProjection100,out var screen);
            entry.Card.Visible=diplomacy&&projected&&screen.X>=-200&&screen.X<=2120&&screen.Y>=-120&&screen.Y<=1200;
            if(projected)entry.Card.Position=screen-new Vector2(60,88);
        }
        Visible=admitted?.Invoke()==true;
        QueueRedraw();
    }

    private void Capture(MapInformationLayerSnapshot? publication)
    {
        foreach(var entry in cards){root.RemoveChild(entry.Card);entry.Card.Free();}
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
            var card=new Control {Name=$"DiplomacyForce{row.ForceId}",Size=new(200,110),MouseFilter=MouseFilterEnum.Ignore};
            root.AddChild(card);
            if(row.Flag is {} flag)
            {
                var control=new UiSourceForceFlag {Position=new(0,18)};
                card.AddChild(control);control.Configure(repository,assets,shader,font,flag);
            }
            if(row.Portrait is {Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:{} pack,SpriteName:{} name})
            {
                var face=new UiSourcePortrait {Position=new(40,0),Size=new(64,64)};
                card.AddChild(face);face.ConfigureFace2(repository,assets,shader,sprites.LoadTexturePackSprite(pack,name));
            }
            Label(row.Name,new(0,65));
            if(row.ForceId!=playerForce)
            {
                string relation=row.RelationLevel is int level&&level>=0&&level<6
                    ? text.Get(new[]{"play_diplomacy_relation_hostile","play_diplomacy_relation_treacherous",
                        "play_diplomacy_relation_discordant","play_diplomacy_relation_neutral",
                        "play_diplomacy_relation_friendly","play_diplomacy_relation_close"}[level])
                    : text.Get("detail_unknown");
                if(row.AllianceMonths is >0)relation+="  "+text.Get("play_diplomacy_alliance");
                Label(relation,new(0,88));
            }
            if(row.UnresolvedInput is {} missing)card.SetMeta("unresolved_input",missing);
            cards.Add((card,worldPosition!(row.CapitalHex)));

            void Label(string value,Vector2 position)
            {
                var label=new UiSourceText {Position=position,Size=new(200,24)};
                card.AddChild(label);label.Configure(font,profile);label.Text=value;
            }
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
        foreach(var entry in cards){root.RemoveChild(entry.Card);entry.Card.Free();}
        cards.Clear();links.Clear();screenLinks.Clear();captured=null;
        font?.Dispose();assets?.Dispose();shader?.Dispose();
    }
}
