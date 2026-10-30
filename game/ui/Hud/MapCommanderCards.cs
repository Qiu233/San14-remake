using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The five retained original cards, supplied by one current publication.</summary>
internal partial class MapCommanderCards : Control
{
    private UiAssets? assets;
    private MapFont? font;
    private Shader? shader;
    private Control canvas=null!;
    private UiSourceCommandButton choose=null!;
    private readonly MapCommanderCardView[] cards=new MapCommanderCardView[5];
    private Func<bool> admission=null!;
    private PreviewText text=null!;
    internal Action<int?>? HoveredOfficer {get;set;}

    internal void Configure(GameResourceRepository repository,PreviewText text,Func<bool> admission,
        Action<int> focus,Action select,Action<int,int> tactic,Action<int> automatic,Action<int> remove)
    {
        this.text=text;this.admission=admission;
        Name="CommanderCards";MouseFilter=MouseFilterEnum.Ignore;ZIndex=2;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        assets=new(repository);font=new(repository,text.SourceLocale);shader=UiSourceSprite.CreateShader();
        canvas=new(){Name="CommanderCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Ignore};AddChild(canvas);
        var main=new UiSourceLayout(repository,0x036BB544,
            new Dictionary<int,UiSourceNodeGeometry>{[83]=new(Origin:new Vector2(960,0))});
        var jump=new UiSourceLayout(repository,0xA2D0A6F1,
            new Dictionary<int,UiSourceNodeGeometry>{[6]=new(Origin:Vector2.Zero)});
        Vector2 mount=main.Rectangle(85).Position+jump.Rectangle(9).Position;
        var source=new UiSourceLayout(repository,0x1CDA8363);
        Rect2 selection=source.Rectangle(2,mount);
        choose=new UiSourceCommandButton {Name="ChooseCommanders",Position=selection.Position,Size=selection.Size,
            InputAdmission=admission};canvas.AddChild(choose);
        string caption=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[0x32E].Text
            ??throw new InvalidDataException("Commander selection caption is missing.");
        choose.Configure(repository,assets,shader,font,caption,()=>{if(admission())select();});
        var bar=MapMarkerLayout.LoadDefinition(repository,"cmn_bar");
        int[] slots=[6,9,10,11,12];
        for(int i=0;i<cards.Length;i++)
        {
            var card=new MapCommanderCardView {Name="CommanderCard"+i,Position=source.Rectangle(slots[i],mount).Position};
            canvas.AddChild(card);card.Configure(repository,assets,shader,font,bar,admission,focus,tactic,automatic,remove);
            card.HoveredOfficer=officer=>HoveredOfficer?.Invoke(officer);cards[i]=card;
        }
        Resized+=Fit;Fit();
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

    private void Fit()
    {
        float scale=Math.Min(Size.X/1920,Size.Y/1080);
        canvas.Scale=Vector2.One*scale;canvas.Position=(Size-new Vector2(1920,1080)*scale)/2;
    }
    internal void ApplySelection(int? unit,int? slot)
    {foreach(var card in cards)card.ApplySelection(unit,slot);}
    public override void _ExitTree()
    {HoveredOfficer?.Invoke(null);HoveredOfficer=null;font?.Dispose();assets?.Dispose();shader?.Dispose();}
}
