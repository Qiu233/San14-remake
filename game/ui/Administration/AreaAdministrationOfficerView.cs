using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record AreaAdministrationOfficerTraitValue(int Id,string Name,byte Style,
    Func<AreaAdministrationQuery<string?>>? Tooltip=null);
internal sealed record AreaAdministrationOfficerValues(string? Name,IReadOnlyList<AreaAdministrationAbilityValue>? Abilities,
    LoadedSprite? Portrait,DetailForceAppearance? Force,IReadOnlyList<AreaAdministrationOfficerTraitValue>? Traits);

/// <summary>The actual common officer card mounted by the region page. Every
/// group is replaced from the same draft publication, including unknowns.</summary>
internal partial class AreaAdministrationOfficerView : Control
{
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private MapFont font=null!;
    private UiSourceLayout source=null!;
    private UiSourceCommandButton nameButton=null!;
    private AreaAdministrationOfficerChart chart=null!;
    private readonly List<AreaAdministrationTraitMarker> traits=[];
    private UiSourceText unknownTraits=null!,unknownFlag=null!;
    private UiSourcePortrait? portrait;
    private Control portraitHost=null!;
    private LoadedSprite? portraitSource;
    private UiSourceForceFlag? flag;
    private DetailForceAppearance? flagAppearance;
    private string unknown="";
    private Action? choose;
    private bool abandoned;
    private UiSourceSimpleTooltip? tooltip;
    private AreaAdministrationTraitMarker? tooltipOwner;
    private AreaAdministrationQuery<string?>? tooltipResult;
    private IReadOnlyList<AreaAdministrationOfficerTraitValue>? currentTraits;
    internal Control SelectionControl=>nameButton;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,PreviewText text,Action choose,
        Control? tooltipHost=null)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;this.font=font;this.choose=choose;
        source=new UiSourceLayout(repository,0xCB7DEBFC);Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;unknown=text.Get("detail_unknown");
        source.Art(this,1,source.Rectangle(1),assets,shader);
        var box=source.Rectangle(2);nameButton=new UiSourceCommandButton {Name="ChooseAreaOfficer",Position=box.Position,Size=box.Size,Disabled=true};AddChild(nameButton);
        nameButton.Configure(repository,assets,shader,font,unknown,()=>{if(!abandoned)this.choose?.Invoke();});
        portraitHost=new Control {Name="OfficerPortraitPane",Size=Size,MouseFilter=MouseFilterEnum.Ignore};AddChild(portraitHost);
        chart=new AreaAdministrationOfficerChart {Name="OfficerAbilities",Position=source.Rectangle(15).Position};AddChild(chart);
        chart.Configure(repository,assets,shader,font,text);
        int[] mounts=[28,33,37,41,45];
        foreach(int node in mounts)
        {
            var marker=new AreaAdministrationTraitMarker {Name="OfficerTrait"+traits.Count,Position=source.Rectangle(node).Position};AddChild(marker);
            marker.Configure(repository,assets,shader,font);marker.SetMeta("source_mount_node",node);traits.Add(marker);
        }
        var markerSource=new UiSourceLayout(repository,0xD493EA98).ResizedSubtree(0,new(158,40),true);
        unknownTraits=markerSource.Text(this,3,source.Rectangle(28).Position,unknown,font,MapTextPresets.Create()[0]);unknownTraits.Name="UnknownOfficerTraits";
        var flagBox=source.Rectangle(12);
        unknownFlag=new UiSourceText {Name="UnknownOfficerFlag",Position=flagBox.Position,Size=flagBox.Size};AddChild(unknownFlag);
        unknownFlag.Configure(font,MapFontProfile.FromNode(markerSource.Nodes[3],markerSource.Source.Source.ResourceUri).WithPreset(MapTextPresets.Create()[0]));unknownFlag.Text=unknown;
        // Frame11 is drawn after the portrait, matching the source pane order.
        var frame=source.Art(this,11,source.Rectangle(11),assets,shader);frame.Name="OfficerPortraitFrame";
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        tooltip=new UiSourceSimpleTooltip();(tooltipHost??GetParent<Control>()).AddChild(tooltip);
        tooltip.Configure(repository,assets,shader,font);
        tooltip.ZIndex=ZIndex+20;
        Apply(new(null,null,null,null,null),false);
    }

    internal void Apply(AreaAdministrationOfficerValues value,bool canChoose)
    {
        if(abandoned)return;
        ClearTooltip();currentTraits=value.Traits;
        nameButton.Caption=value.Name??unknown;nameButton.Disabled=!canChoose;
        chart.Apply(value.Abilities);
        if(!ReferenceEquals(portraitSource,value.Portrait))
        {
            if(portrait is not null){portrait.Hide();portrait.Name="PreviousOfficerPortrait";portrait.QueueFree();portrait=null;}
            portraitSource=value.Portrait;
            if(value.Portrait is { } face)
            {
                var box=source.Rectangle(9);portrait=new UiSourcePortrait {Name="OfficerPortrait",Position=box.Position,Size=box.Size};portraitHost.AddChild(portrait);
                portrait.ConfigureOfficerCard(repository,assets,shader,face);
            }
        }
        unknownFlag.Visible=value.Force is null;
        if(flagAppearance!=value.Force)
        {
            if(flag is not null){flag.Hide();flag.Name="PreviousOfficerFlag";flag.QueueFree();flag=null;}
            flagAppearance=value.Force;
            if(value.Force is { } appearance)
            {
                flag=new UiSourceForceFlag {Name="OfficerForceFlag",Position=source.Rectangle(12).Position};AddChild(flag);
                flag.Configure(repository,assets,shader,font,appearance);
            }
        }
        unknownTraits.Visible=value.Traits is null;
        if(value.Traits is {Count:>5})throw new InvalidDataException("Officer card has more than five compact traits.");
        for(int i=0;i<traits.Count;i++)
        {
            traits[i].Visible=value.Traits is not null&&i<value.Traits.Count;
            if(traits[i].Visible){var trait=value.Traits![i];traits[i].Apply(trait.Id,trait.Name,trait.Style);}
        }
    }
    private void ClearTooltip()
    {
        tooltipOwner=null;tooltipResult=null;
        if(tooltip is not null&&GodotObject.IsInstanceValid(tooltip))tooltip.Hide();
    }
    public override void _Process(double delta)
    {
        if(abandoned||!IsVisibleInTree()||tooltip is null||!GodotObject.IsInstanceValid(tooltip)){ClearTooltip();return;}
        var hovered=GetViewport().GuiGetHoveredControl();
        int index=traits.FindIndex(marker=>marker==hovered&&marker.IsVisibleInTree());
        if(index<0||currentTraits is null||index>=currentTraits.Count){ClearTooltip();return;}
        var marker=traits[index];
        if(tooltipOwner!=marker)
        {
            ClearTooltip();tooltipOwner=marker;
            try
            {
                tooltipResult=currentTraits[index].Tooltip?.Invoke();
                tooltip.DisplayText(tooltipResult is {Available:true}?tooltipResult.Value:null);
            }
            catch(InvalidDataException unknown)
            {tooltipResult=new(null,unknown.Message);tooltip.Hide();}
        }
        if(tooltip.Visible)tooltip.UpdatePointer(tooltip.GetParent<Control>().GetLocalMousePosition());
    }
    public override void _Notification(int what)
    {if(what==NotificationVisibilityChanged&&!IsVisibleInTree())ClearTooltip();}
    internal void Abandon()
    {abandoned=true;choose=null;currentTraits=null;ClearTooltip();SetProcess(false);nameButton?.Abandon();Hide();}
    public override void _ExitTree()
    {
        currentTraits=null;ClearTooltip();
        if(tooltip is not null&&GodotObject.IsInstanceValid(tooltip)){tooltip.QueueFree();tooltip=null;}
    }
}
