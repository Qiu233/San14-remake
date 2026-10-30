using System.Globalization;
using Godot;
using San14.Data;

/// <summary>The setup's actual officer-chart mode1. Its current whole value
/// changes colour against raw base; both meshes reuse the common105-axis writer.</summary>
internal partial class DistrictSetupOfficerChart:Control
{
    private readonly List<UiSourceText> values=[];
    private UiSourceRadarMesh current=null!,comparison=null!;
    private string unknown="";
    private static readonly int[] Maximums=[105,105,105,105,105];
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,PreviewText text)
    {
        var source=new UiSourceLayout(repository,0x27119CFA);Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        unknown=text.Get("detail_unknown");var presets=MapTextPresets.Create();var palettes=MapSpritePalettes.Create();
        source.Art(this,1,source.Rectangle(1),assets,shader,sourceMaterial:true);
        current=Layer(2,"DistrictCurrentAbilities",15);comparison=Layer(3,"DistrictBaseAbilities",14);
        source.Art(this,4,source.Rectangle(4),assets,shader,sourceMaterial:true);
        var messages=repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data).Messages.Sections[1];
        for(int axis=0;axis<5;axis++)
        {
            source.Text(this,5+axis*2,default,messages.Records[0x238+axis].Text??throw new InvalidDataException("Missing officer ability caption."),font,presets[13]);
            var label=source.Text(this,6+axis*2,default,unknown,font,presets[0]);label.Name="DistrictLeaderAbility"+axis;values.Add(label);
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);Apply(null);
        UiSourceRadarMesh Layer(int node,string name,int palette)
        {var mesh=new UiSourceRadarMesh{Name=name};AddChild(mesh);mesh.Configure(source,node,palettes[palette].LT);return mesh;}
    }
    internal void Apply(IReadOnlyList<AreaAdministrationAbilityValue>? abilities)
    {
        if(abilities is not null&&abilities.Count!=5)throw new InvalidDataException("District governor requires five abilities.");
        int[]? actual=abilities?.All(a=>a.Current.HasValue)==true?abilities.Select(a=>a.Current!.Value).ToArray():null;
        int[]? baseline=abilities?.All(a=>a.Current.HasValue&&a.Base.HasValue)==true
            ?abilities.Select(a=>Math.Min(a.Current!.Value,a.Base!.Value)).ToArray():null;
        current.ApplyValues(actual,Maximums);comparison.ApplyValues(baseline,Maximums);
        for(int axis=0;axis<5;axis++)
        {
            var ability=abilities?[axis];
            if(ability is null||ability.Current is not int value||ability.Base is not int basic){values[axis].Text=unknown;continue;}
            string number=value.ToString(CultureInfo.InvariantCulture);
            values[axis].SetKnownColourLine(basic<1||basic==value?number:value>basic?"^02"+number+"^00":"^03"+number+"^00");
        }
    }
}
