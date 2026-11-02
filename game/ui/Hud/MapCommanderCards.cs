using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The right HUD owns its dispatch and commander pages, two source
/// radio tabs and the shared fold state. Current data stays in publications.</summary>
internal partial class MapCommanderCards : Control
{
    private UiAssets? assets;
    private MapRightHudState state=null!;
    private Control shell=null!,commanderPage=null!,dispatchPage=null!,dispatchInput=null!;
    private Control background=null!;
    private Control? backgroundMask;
    private UiSourceType6 backgroundBlur=null!;
    private UiSourceLayout jumpLayout=null!;
    private Rect2 backgroundRectangle;
    private int backgroundHeight=-1;
    private readonly MapRightHudButton[] tabs=new MapRightHudButton[2];
    private MapRightHudButton fold=null!;
    private readonly MapDispatchRow[] dispatchRows=new MapDispatchRow[10];
    private DispatchHudPublication? shownDispatch;
    private IReadOnlyDictionary<int,OfficerPortraitRoute>? shownDispatchPortraits;
    private int dispatchCount;
    private Control otherBacking=null!;
    private UiSourceText otherCaption=null!;
    private string otherFormat="";
    private MapFont? font;
    private Shader? shader;
    private Control canvas=null!;
    private UiSourceCommandButton choose=null!;
    private readonly MapCommanderCardView[] cards=new MapCommanderCardView[5];
    private Func<bool> admission=null!;
    private PreviewText text=null!;
    internal Action<int?>? HoveredOfficer {get;set;}

    internal void Configure(GameResourceRepository repository,PreviewText text,AdministrationPageChrome.Resources shared,
        MapRightHudState state,Func<bool> admission,
        Action<int> focus,Action select,Action<int,int> tactic,Action<int> automatic,Action<int> remove,Action<int> focusDispatch)
    {
        this.text=text;this.admission=admission;this.state=state;
        // CMainJump constructs a new radio controller and selects zero.
        // The separate fold preference outlives that controller.
        state.Tab=0;
        Name="CommanderCards";MouseFilter=MouseFilterEnum.Ignore;ZIndex=2;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets=shared.Assets;font=shared.Font;shader=shared.SpriteShader;
        canvas=new(){Name="CommanderCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        shell=new(){Name="RightHudShell",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};canvas.AddChild(shell);
        // Keep the actual mask -> Type6 boundary before both content pages.
        // Main's existing Type6Host discovers this cut in ordinary tree order.
        background=new(){Name="RightHudBackground",MouseFilter=MouseFilterEnum.Ignore};shell.AddChild(background);
        commanderPage=new(){Name="CommanderPage",MouseFilter=MouseFilterEnum.Ignore};shell.AddChild(commanderPage);
        dispatchPage=new(){Name="DispatchPage",MouseFilter=MouseFilterEnum.Ignore};shell.AddChild(dispatchPage);
        var main=new UiSourceLayout(repository,0x036BB544,
            new Dictionary<int,UiSourceNodeGeometry>{[83]=new(Origin:new Vector2(960,0))});
        var jump=new UiSourceLayout(repository,0xA2D0A6F1,
            new Dictionary<int,UiSourceNodeGeometry>{[6]=new(Origin:Vector2.Zero)});
        Vector2 mount=main.Rectangle(85).Position+jump.Rectangle(9).Position;
        Vector2 mainMount=main.Rectangle(85).Position;
        jumpLayout=jump;backgroundRectangle=jump.Rectangle(7,mainMount);
        backgroundBlur=jump.Blur(background,8,jump.Rectangle(8,mainMount));
        jump.Art(shell,28,jump.Rectangle(28,mainMount),assets,shader);
        int[] tabMounts=[29,34];string[] tabNames=["cmn_btn_tab_s_l","cmn_btn_tab_s_r"];
        for(int index=0;index<2;index++)
        {
            int selected=index;var r=jump.Rectangle(tabMounts[index],mainMount);
            var tab=new MapRightHudButton{Name="RightHudTab"+index,Position=r.Position,Size=r.Size,InputAdmission=admission};
            var glyph=jump.Rectangle(index==0?33:38,mainMount);glyph.Position-=r.Position;
            shell.AddChild(tab);tab.Configure(assets,shader,tabNames[index],()=>{state.Tab=selected;ApplyPages();},true,glyph);tabs[index]=tab;
        }
        var foldPlace=jump.Rectangle(39,mainMount);
        fold=new MapRightHudButton{Name="FoldRightHud",Position=foldPlace.Position,Size=foldPlace.Size,InputAdmission=admission};
        shell.AddChild(fold);fold.Configure(assets,shader,"00_82_02_jump_offset_15",()=>{state.Collapsed=!state.Collapsed;ApplyPages();},false);
        // First empty slot is retained. The source masks a pane sized to the
        // number of rows, rather than five unconditional command cards.
        var array=assets.SourceLayout("00_82_02_jump_offset_20");int[] personSlots=[1,17,19,21,23,25,27,29,31,33];
        dispatchInput=new(){Name="DispatchInputPane",Position=mount+array.Rectangle(1).Position,MouseFilter=MouseFilterEnum.Stop};dispatchPage.AddChild(dispatchInput);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        string noDispatch=messages.Records[0x616].Text??throw new InvalidDataException("Dispatch empty caption is missing.");
        otherFormat=messages.Records[0x370].Text??throw new InvalidDataException("Dispatch overflow caption is missing.");
        otherBacking=array.Art(dispatchPage,36,array.Rectangle(36,mount),assets,shader);
        otherCaption=array.Text(dispatchPage,37,mount,"",font);
        var settledDispatch=MapRightHudSources.DispatchSlot(assets);
        for(int index=0;index<10;index++)
        {
            var row=new MapDispatchRow{Name="DispatchRow"+index,Position=array.Rectangle(personSlots[index]).Position+mount};
            dispatchPage.AddChild(row);row.Configure(repository,assets,shader,font,noDispatch,admission,focusDispatch,settledDispatch);
            row.HoveredOfficer=id=>HoveredOfficer?.Invoke(id);dispatchRows[index]=row;
        }
        var source=assets.SourceLayout(0x1CDA8363);
        Rect2 selection=source.Rectangle(2,mount);
        choose=new UiSourceCommandButton {Name="ChooseCommanders",Position=selection.Position,Size=selection.Size,
            InputAdmission=admission};commanderPage.AddChild(choose);
        string caption=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[0x32E].Text
            ??throw new InvalidDataException("Commander selection caption is missing.");
        choose.Configure(repository,assets,shader,font,caption,()=>{if(admission())select();});
        var bar=MapMarkerLayout.LoadDefinition(repository,"cmn_bar");
        int[] slots=[6,9,10,11,12];
        for(int i=0;i<cards.Length;i++)
        {
            var card=new MapCommanderCardView {Name="CommanderCard"+i,Position=source.Rectangle(slots[i],mount).Position};
            commanderPage.AddChild(card);card.Configure(repository,assets,shader,font,bar,admission,focus,tactic,automatic,remove);
            card.HoveredOfficer=officer=>HoveredOfficer?.Invoke(officer);cards[i]=card;
        }
        ApplyPages();Resized+=Fit;Fit();
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        SetMeta("source_mount_layout",jump.Source.Source.ResourceUri);SetMeta("source_mount_node",9);
        SetMeta("source_mount_position",mount);
    }

    internal void Apply(CommanderHudPublication publication,IReadOnlyDictionary<int,OfficerPortraitRoute> portraits,
        IReadOnlyList<string> shortcuts,bool strategy,bool canSelect,int? selectedUnit,int? selectedSlot)
    {
        choose.Visible=strategy&&publication.Capacity is >0;choose.Disabled=!canSelect||!admission();
        for(int i=0;i<cards.Length;i++)
        {
            var card=publication.Cards.FirstOrDefault(row=>row.Slot==i)
                ??new CommanderHudCard(i,null,text.Get("detail_unknown"),null);
            cards[i].Apply(card,card.OfficerId is int person?portraits.GetValueOrDefault(person):null,
                shortcuts[i],text.Get("detail_unknown"),canSelect,selectedUnit,selectedSlot);
        }
        SetMeta("source_publication_issue",publication.UnresolvedInput??"");
    }

    internal void ApplyDispatch(DispatchHudPublication publication,IReadOnlyDictionary<int,OfficerPortraitRoute> portraits)
    {
        if(ReferenceEquals(publication,shownDispatch)&&ReferenceEquals(portraits,shownDispatchPortraits))return;
        shownDispatch=publication;shownDispatchPortraits=portraits;dispatchCount=Math.Min(10,publication.Rows.Count);
        for(int index=0;index<10;index++)
        {
            var row=index<dispatchCount?publication.Rows[index]:null;
            dispatchRows[index].Visible=index<Math.Max(1,dispatchCount);
            dispatchRows[index].Apply(row,row is null?null:portraits.GetValueOrDefault(row.OfficerId),publication.UnresolvedInput is not null);
        }
        dispatchInput.Size=new(316,Math.Max(1,dispatchCount)*86);
        otherBacking.Visible=otherCaption.Visible=publication.Rows.Count>10;
        otherCaption.Text=otherFormat.Replace("%d",Math.Min(999,Math.Max(0,publication.Rows.Count-10)).ToString(System.Globalization.CultureInfo.InvariantCulture),StringComparison.Ordinal);
        SetMeta("dispatch_total_count",publication.Rows.Count);
        SetMeta("dispatch_unresolved",publication.UnresolvedInput??"");
        ApplyBackgroundHeight();
    }
    private void ApplyPages()
    {
        shell.Position=new(state.Collapsed?320:0,0);
        commanderPage.Visible=state.Tab==1;dispatchPage.Visible=state.Tab==0;
        // A settled collapsed rail lies wholly beyond the viewport. Its fold
        // handle remains a sibling and stays available at the screen edge.
        background.Visible=!state.Collapsed;
        fold.ApplyFoldState(state.Collapsed);
        ApplyBackgroundHeight();
        for(int index=0;index<tabs.Length;index++)if(tabs[index] is {} tab)tab.Selected=state.Tab==index;
        HoveredOfficer?.Invoke(null);
        SetMeta("right_hud_tab",state.Tab);SetMeta("right_hud_collapsed",state.Collapsed);
    }
    private void ApplyBackgroundHeight()
    {
        if(backgroundBlur is null)return;
        // 8990F0 calls897140 with a float32-scaled integer height. The radio
        // command page restores the authored full mask height, independently
        // of commander capacity or of how many cards currently have a Person.
        int height=state.Tab==1?(int)backgroundRectangle.Size.Y:
            (int)(backgroundRectangle.Size.Y*Math.Clamp(dispatchCount,1,10)/10f);
        if(height==backgroundHeight)return;
        backgroundHeight=height;
        if(backgroundMask is not null)
        {background.RemoveChild(backgroundMask);backgroundMask.QueueFree();}
        var rectangle=backgroundRectangle;rectangle.Size=new(rectangle.Size.X,height);
        backgroundMask=jumpLayout.Art(background,7,rectangle,assets!,shader!,blend:UiSourceSprite.Blend.AlphaMask);
        background.MoveChild(backgroundMask,0);
        backgroundBlur.Size=new(backgroundBlur.Size.X,height);
        SetMeta("source_right_hud_blur_height",height);
    }
    private void Fit()
    {
        float scale=Math.Min(Size.X/1920,Size.Y/1080);
        canvas.Scale=Vector2.One*scale;canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    internal void ApplySelection(int? unit,int? slot)
    {foreach(var card in cards)card.ApplySelection(unit,slot);}
    public override void _ExitTree()
    {HoveredOfficer?.Invoke(null);HoveredOfficer=null;}
}
