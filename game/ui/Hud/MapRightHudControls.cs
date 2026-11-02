using Godot;
using San14.Data;

// This is application presentation state, never scenario-specific state.
// CMainJump's radio construction explicitly selects index zero.
internal sealed class MapRightHudState { internal int Tab; internal bool Collapsed; }

internal partial class MapRightHudButton : UiSourceButton
{
    private UiSourceLayout glyphSource=null!;
    private UiAssets glyphAssets=null!;
    private Shader glyphShader=null!;
    private Rect2 glyphRectangle;
    private Control? glyph;
    private string? currentGlyph;
    private bool isTab;
    // CMainJump's node14 setter selects enum130 when closed and131 when
    // open. Existing source table entries name these two common sprites.
    internal static string FoldIcon(bool collapsed)=>collapsed?"000_cmn_com_icon_close":"000_cmn_com_icon_open";
    internal override UiAudioCue PressedCue=>UiAudioCue.Tab;
    internal static string[] ActionNames(bool tab)=>tab
        ?["0_normal","1_focus","2_push","3_disable","3_disabl_focus"]
        :["00_nomal","1_focus ","2_push","3_disable","4_disable_focus"];
    internal void Configure(UiAssets assets,Shader shader,string layout,Action pressed,bool tab,Rect2? glyphRectangle=null)
    {
        var source=assets.SourceLayout(layout).ResizedSubtree(0,Size,true);
        int[][] layers=tab?[[3],[5,6],[8,9],[11],[13,14]]:[[2],[4,5],[7],[9],[11,12]];
        var art=new UiSourceButtonStateArt(this,source,assets,shader,layers,"RightHudState",new UiSourceButtonStateProfile(source.Source,ActionNames(tab)));
        glyphSource=source;glyphAssets=assets;glyphShader=shader;isTab=tab;
        int icon=tab?15:14;
        // Tab callers resize the backing but retain their authored 28px icon.
        // Its actual position is supplied by CMainJump's inline mount.
        this.glyphRectangle=glyphRectangle??source.Rectangle(icon);
        RebindGlyph(tab?null:FoldIcon(false));
        // 85CFE0/85AC70/85D9B0/859800/859DB0 write palette25..29
        // on tab glyph15. Jump glyph14 uses25..28 and its own final setter.
        ConfigureStates(art.States,state=>{art.ApplyState(state);ApplyGlyphColors(state);});
        ActionMode=ActionModeEnum.Release;
        Pressed+=()=>{if(!Disabled&&SourceInputAdmitted)pressed();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void ApplyFoldState(bool collapsed)
    {
        if(isTab)throw new InvalidOperationException("A radio tab has no fold sprite state.");
        RebindGlyph(FoldIcon(collapsed));
    }
    private void RebindGlyph(string? commonSprite)
    {
        if(glyph is not null&&currentGlyph==commonSprite)return;
        if(glyph is not null){RemoveChild(glyph);glyph.QueueFree();}
        currentGlyph=commonSprite;
        glyph=glyphSource.Art(this,isTab?15:14,glyphRectangle,glyphAssets,glyphShader,
            commonSpriteOverride:commonSprite);
        ApplyGlyphColors(VisualState);
        SetMeta("source_glyph_sprite",commonSprite??glyphSource.Nodes[15].Name);
    }
    private void ApplyGlyphColors(int state)
    {
        if(glyph is null)return;
        var colors=MapSpritePalettes.Create()[25+state];
        if(!isTab&&state==4)
        {
            // 88FF30 calls the direct four-corner writer92FE30 with raw1D,
            // whereas the preceding Jump states call palette writer930D10.
            var direct=MapSpritePalettes.FromAuthored(0x0000001D);
            colors=new(direct,direct,direct,direct);
        }
        foreach(var part in glyph.GetChildren().OfType<UiSourceSprite>())part.SetColors(colors);
    }
}

internal partial class MapDispatchRow : Control
{
    private UiSourceText officer=null!,target=null!,empty=null!,caption=null!;
    private UiSourceBarGauge gauge=null!;
    private UiSourceHitButton hit=null!;
    private MapCommanderFocusView focus=null!;
    private Control? portrait;
    private (string?,string?) portraitKey;
    private int portraitVersion=-1;
    private DispatchHudRow? current;
    private UiAssets assets=null!;private Shader shader=null!;
    private UiSourceLayout source=null!;private GameResourceRepository repository=null!;
    internal Action<int?>? HoveredOfficer;
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        string emptyText,Func<bool> admission,Action<int> focusHex,UiSourceLayout settledSource)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;
        source=settledSource;
        Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        source.Art(this,2,source.Rectangle(2),assets,shader);
        officer=source.Text(this,4,default,"",font);target=source.Text(this,3,default,"",font);
        empty=source.Text(this,5,default,emptyText,font);
        // CPersonSlot's command marker is the caller-mounted cmn_mrker.
        source.Art(this,11,source.Rectangle(11),assets,shader);
        caption=source.Text(this,12,default,"",font);
        gauge=new(){Position=source.Rectangle(6).Position};AddChild(gauge);
        gauge.Configure(MapMarkerLayout.LoadDefinition(repository,"cmn_bar"),assets,shader,null);
        gauge.Scale=source.Rectangle(6).Size/gauge.Size;
        hit=new(){Name="DispatchPerson",Size=Size,IgnoreTextureSize=true,FocusMode=FocusModeEnum.None};AddChild(hit);
        focus=new(){Name="DispatchFocus"};AddChild(focus);focus.Configure(repository,assets,shader,Size);
        hit.Pressed+=()=>{if(admission()&&current?.HexId is int hex)focusHex(hex);};
        hit.MouseEntered+=()=>{if(admission())HoveredOfficer?.Invoke(current?.OfficerId);UpdateFocus();};
        hit.MouseExited+=()=>{HoveredOfficer?.Invoke(null);UpdateFocus();};
        hit.ButtonDown+=UpdateFocus;hit.ButtonUp+=UpdateFocus;
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        void UpdateFocus()=>focus.ApplyInteraction(!hit.Disabled&&hit.IsHovered(),!hit.Disabled&&hit.GetDrawMode() is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed);
    }
    internal void Apply(DispatchHudRow? row,OfficerPortraitRoute? route,bool unresolved)
    {
        current=row;empty.Visible=row is null&&!unresolved;officer.Visible=target.Visible=caption.Visible=gauge.Visible=row is not null;
        // The marker's backing is omitted with the empty Person just as in891730.
        foreach(Node child in GetChildren())if(child is Control c&&c.HasMeta("source_node_index")&&c.GetMeta("source_node_index").AsInt32()==11)c.Visible=row is not null;
        officer.Text=row?.Name??"";target.Text=row?.Target??"";caption.Text=row?.Caption??"";
        hit.Disabled=row?.HexId is null;focus.ApplyInteraction(false,false);
        if(row is not null)gauge.Apply(Math.Clamp(row.Remaining,0,10),10);
        var key=route is{Kind:OfficerPortraitRouteKind.StockNamed}?(route.ResourceName,route.SpriteName):(null,null);
        if(key!=portraitKey||portraitVersion!=repository.PortraitResourceVersion)
        {
            if(portrait is not null){RemoveChild(portrait);portrait.QueueFree();portrait=null;}
            portraitKey=key;
            portraitVersion=repository.PortraitResourceVersion;
            if(key.Item1 is string pack&&key.Item2 is string sprite)
            {
                var pane=source.Rectangle(16);
                var image=assets.Portraits.RequireSource(pack,sprite);
                var face=new UiSourcePortrait{Name="DispatchPortrait",Position=pane.Position,Size=pane.Size};AddChild(face);
                face.ConfigureFace5(repository,assets,shader,image);portrait=face;
            }
        }
        SetMeta("dispatch_person",row?.OfficerId??-1);
    }
}
