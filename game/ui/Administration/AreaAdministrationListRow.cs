using Godot;
using San14.Data;
using San14.Client.Gameplay;

/// <summary>A Domestic single-selection row, with the original place icon in
/// its front pane. Releasing a row requests the region child, not a world order.</summary>
internal partial class AreaAdministrationListRow : UiSourceButton
{
    private readonly List<(UiSourceText Text,MapFontProfile Profile)> labels=[];
    private UiSourceSprite icon=null!;
    private UiSourceText unknownIcon=null!;
    private UiAssets assets=null!;
    private MapFont font=null!;
    private Action? released;
    private string unknown="";
    private MapSpritePalettes palettes=null!;

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        PreviewText text,AreaAdministrationListEntry entry,Action released)
    {
        this.assets=assets;this.font=font;this.released=released;unknown=text.Get("detail_unknown");
        Size=new(1464,48);ActionMode=ActionModeEnum.Release;
        var source=new UiSourceLayout(repository,0xF2C06161);var presets=MapTextPresets.Create();palettes=MapSpritePalettes.Create();
        int[] front=[4,6,9,12,14],body=[23,25,28,31,33];var states=new Control[5];
        for(int state=0;state<5;state++)
        {
            var group=new Control {Size=Size,MouseFilter=MouseFilterEnum.Ignore};AddChild(group);
            source.Art(group,front[state],new(Vector2.Zero,new(48,48)),assets,shader);
            source.Art(group,body[state],new(new(48,0),new(1416,48)),assets,shader);states[state]=group;
        }
        icon=new UiSourceSprite {Name="AreaPlaceIcon",Position=source.Rectangle(18).Position,Size=source.Rectangle(18).Size};AddChild(icon);
        // Configure a real source texture before any known icon is published.
        // Unknown and ordinary land both keep this quad hidden.
        icon.Configure(shader,assets.SrgbSprite("000_cmn_icon_kyoten"),UiSourceSprite.Blend.Ordinary);
        unknownIcon=new UiSourceText {Name="UnknownPlaceIcon",Position=icon.Position,Size=icon.Size};AddChild(unknownIcon);
        unknownIcon.Configure(font,MapFontProfile.FromNode(source.Nodes[17],source.Source.Source.ResourceUri).WithPreset(presets[8]));
        int x=48;
        foreach(var column in AreaAdministrationListContent.Columns)
        {
            var profile=new MapFontProfile(source.Source.Source.ResourceUri+"/dynamic"+(40+labels.Count),0,0,
                .875f,.875f,0,0,uint.MaxValue,uint.MaxValue,uint.MaxValue,uint.MaxValue,0,column.Align,column.LineArrange,
                0,0,0xFF000000,0xFF000000,0xFF000000,0xFF000000)
                {WidthFitMode=2,WidthReserve=0,StrokeColour=0xFF000000,StrokeWidth=3};
            var label=new UiSourceText {Name="AreaColumn"+column.Id,Position=new(x+10,10),Size=new(column.Width-20,28)};AddChild(label);
            label.Configure(font,profile.WithPreset(presets[8]));labels.Add((label,profile));x+=column.Width;
        }
        ConfigureStates(states,state=>
        {
            foreach(var (label,profile) in labels){label.Configure(font,profile.WithPreset(presets[8+Math.Min(state,3)]));label.QueueRedraw();}
            icon.SetColors(palettes[36+state]);
            unknownIcon.Configure(font,MapFontProfile.FromNode(source.Nodes[17],source.Source.Source.ResourceUri).WithPreset(presets[8+Math.Min(state,3)]));
            unknownIcon.QueueRedraw();
        });
        Pressed+=()=>{if(!Disabled)this.released?.Invoke();};
        SetMeta("source_layout",source.Source.Source.ResourceUri);Apply(entry);
    }

    internal void Apply(AreaAdministrationListEntry entry)
    {
        if(entry.Cells.Count!=labels.Count)throw new InvalidDataException("Administration cells do not match the source columns.");
        for(int i=0;i<labels.Count;i++)labels[i].Text.Text=entry.Cells[i];
        unknownIcon.Visible=!entry.Place.Available;unknownIcon.Text=unknown;
        string? name=entry.Place.Kind switch {AreaPlaceKind.City=>"000_cmn_icon_kyoten",AreaPlaceKind.Gate=>"000_cmn_icon_gate",
            AreaPlaceKind.Port=>"000_cmn_icon_port",null=>null,_=>throw new InvalidDataException("Unknown administration place icon.")};
        icon.Visible=entry.Place.Available&&name is not null;
        if(icon.Visible){icon.SetSource(assets.SrgbSprite(name!),palettes[36+VisualState]);icon.SetMeta("source_sprite",name!);}
    }
    internal void Abandon(){released=null;Disabled=true;}
}
