using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The setup Person tab. All visible groups are replaced from the
/// selected archived-world publication, independently of draft troop edits.</summary>
internal partial class DeploymentOfficerInformationView : Control
{
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private MapFont font=null!;
    private PreviewText text=null!;
    private UiSourceLayout source=null!;
    private AreaAdministrationOfficerChart chart=null!;
    private readonly List<AreaAdministrationTraitMarker> traits=[];
    private readonly Dictionary<int,UiSourceText> labels=[];
    private readonly Dictionary<int,UiSourcePortrait> portraits=[];
    private readonly Dictionary<(string,string),LoadedSprite> sprites=[];
    private LocalizedUiSpriteSource spriteSource=null!;
    private UiSourceSimpleTooltip tooltip=null!;
    private int? tooltipSlot;
    private AreaAdministrationQuery<IReadOnlyList<AreaAdministrationOfficerTrait>>? currentTraits;
    private Func<int,AreaAdministrationQuery<string?>>? traitDescription;
    private bool abandoned;
    private static readonly int[] RelatedFaces=[38,44,50,58,64,70];
    private static readonly int[] RelatedNames=[41,47,53,61,67,73];

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,PreviewText text,Control tooltipHost)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;this.font=font;this.text=text;
        source=new(repository,0xC11503D0);Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        spriteSource=new(repository);
        foreach(int node in new[]{27,28,29,32,40,46,52,54,60,66,72,74,84})
            source.Art(this,node,source.Rectangle(node),assets,shader);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        var presets=MapTextPresets.Create();
        foreach(var binding in new[]{(30,0x03D2),(16,0x023D),(33,0x042C),(34,0x042D),(35,0x042E),(55,0x02BF),(75,0x02BC),(85,0x02BD)})
            source.Text(this,binding.Item1,default,messages.Records[binding.Item2].Text!,font,presets[13]);
        chart=new AreaAdministrationOfficerChart {Name="DeploymentOfficerAbilities",Position=source.Rectangle(3).Position};AddChild(chart);
        chart.Configure(repository,assets,shader,font,text,0x7E8AD398);
        foreach(int node in new[]{31,41,47,53,61,67,73}.Concat(Enumerable.Range(76,8)).Concat(Enumerable.Range(86,8)))
        {
            var label=node==31?source.Text(this,node,default,"",font,presets[5]):source.Text(this,node,default,"",font);
            label.Name="DeploymentOfficerValue"+node;labels[node]=label;
        }
        //7D3870 derives the spacing from authored marker17/19, then makes
        //nine controls using the source's five-row placement period.
        var first=source.Rectangle(17);var second=source.Rectangle(19);
        for(int index=0;index<9;index++)
        {
            var marker=new AreaAdministrationTraitMarker {Name="DeploymentOfficerTrait"+index,
                Position=new(first.Position.X,first.Position.Y+(index%5)*(second.Position.Y-first.Position.Y))};
            AddChild(marker);marker.Configure(repository,assets,shader,font,first.Size);traits.Add(marker);
        }
        tooltip=new UiSourceSimpleTooltip();tooltipHost.AddChild(tooltip);tooltip.Configure(repository,assets,shader,font);
        tooltip.ZIndex=20;
        SetMeta("source_layout",source.Source.Source.ResourceUri);Apply(null);
    }

    internal void Apply(AreaAdministrationQuery<DeploymentOfficerPanePublication>? query,
        Func<int,AreaAdministrationQuery<string?>>? description=null)
    {
        if(abandoned)return;
        tooltipSlot=null;tooltip.Hide();traitDescription=description;
        foreach(var portrait in portraits.Values){portrait.Hide();RemoveChild(portrait);portrait.QueueFree();}portraits.Clear();
        foreach(var label in labels.Values)label.Text="";
        foreach(var marker in traits)marker.Hide();
        currentTraits=null;
        if(query is null){chart.Apply(null,true);labels[31].Hide();RemoveMeta("officer_id");return;}
        labels[31].Show();
        if(query.Value is not { } value)
        {
            RemoveMeta("officer_id");RemoveMeta("officer_name");
            chart.Apply(null);labels[31].Text=text.Get("detail_unknown");
            SetMeta("unresolved_owner",query.UnresolvedInput??"deployment.officer-pane.publication");return;
        }
        SetMeta("officer_id",value.OfficerId);SetMeta("officer_name",value.Name);RemoveMeta("unresolved_owner");
        traitDescription=description??value.TraitTooltip;
        chart.Apply(value.Abilities.Select(a=>new AreaAdministrationAbilityValue(a.Base.Available?a.Base.Value:null,
            a.Effective.Available?a.Effective.Value:null)).ToArray());
        labels[31].Text=value.CommandCapacity.Available?value.CommandCapacity.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"":text.Get("detail_unknown");
        AddPortrait(2,value.Portrait);
        currentTraits=value.Traits;
        if(value.Traits.Value is { } rows)
            for(int i=0;i<Math.Min(rows.Count,traits.Count);i++)
            {var row=rows[i];traits[i].Apply(row.LookupTraitId,row.Name,row.Style);traits[i].Show();}
        else {traits[0].ApplyUnknown(text.Get("detail_unknown"));traits[0].Show();}
        var related=new[]{value.Relations.Father,value.Relations.Mother,value.Relations.Spouse}
            .Concat(value.Relations.SwornBrothers).Take(6).ToArray();
        for(int i=0;i<related.Length;i++)
        {labels[RelatedNames[i]].Text=RelationName(related[i]);if(related[i].Portrait is { } route)AddPortrait(RelatedFaces[i],route);}
        Names(value.Relations.Likes,76);Names(value.Relations.Dislikes,86);
    }

    private void Names(IReadOnlyList<OfficerDetailRelatedPerson> rows,int first)
    {for(int i=0;i<8;i++)labels[first+i].Text=i<rows.Count?RelationName(rows[i]):"";}
    private string RelationName(OfficerDetailRelatedPerson person)=>person.Name.Length>0?person.Name:
        person.UnresolvedOwner is null?"":text.Get("detail_unknown");
    private void AddPortrait(int node,OfficerPortraitRoute route)
    {
        if(route is not {Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:{} pack,SpriteName:{} name})return;
        var key=(pack,name);
        if(!sprites.TryGetValue(key,out var sprite))sprites[key]=sprite=spriteSource.LoadTexturePackSprite(pack,name);
        var box=source.Rectangle(node);var face=new UiSourcePortrait {Name="DeploymentOfficerPortrait"+node,Position=box.Position,Size=box.Size};
        AddChild(face);MoveChild(face,0);face.ConfigureLayoutPane(repository,assets,shader,sprite,source,node);portraits[node]=face;
    }
    public override void _Process(double delta)
    {
        if(abandoned||!IsVisibleInTree()){tooltipSlot=null;tooltip?.Hide();return;}
        var hovered=GetViewport().GuiGetHoveredControl();int slot=traits.FindIndex(row=>row==hovered&&row.IsVisibleInTree());
        if(slot<0){tooltipSlot=null;tooltip.Hide();return;}
        if(tooltipSlot!=slot)
        {
            tooltipSlot=slot;tooltip.Hide();
            if(currentTraits?.Value is { } rows && slot<rows.Count && traitDescription is not null)
            {var value=traitDescription(rows[slot].LookupTraitId);tooltip.DisplayText(value.Available?value.Value:text.Get("detail_unknown"));}
        }
        if(tooltip.Visible)tooltip.UpdatePointer(tooltip.GetParent<Control>().GetLocalMousePosition());
    }
    internal void Abandon(){abandoned=true;traitDescription=null;currentTraits=null;tooltip?.Hide();Hide();}
    public override void _ExitTree(){if(tooltip is not null&&GodotObject.IsInstanceValid(tooltip))tooltip.QueueFree();}
}
