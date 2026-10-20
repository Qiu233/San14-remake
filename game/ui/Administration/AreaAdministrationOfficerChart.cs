using System.Globalization;
using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record AreaAdministrationAbilityValue(int? Base,int? Current);

/// <summary>The original five personal abilities, with a 105-axis radar and
/// independent current/base publications. Text is not clamped to the mesh.</summary>
internal partial class AreaAdministrationOfficerChart : Control
{
    private readonly List<UiSourceText> values=[];
    private UiSourceRadarMesh current=null!,comparison=null!;
    private string unknown="";
    private uint increase,decrease;
    private static readonly int[] Maximums=[105,105,105,105,105];
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,PreviewText text)
    {
        var source=new UiSourceLayout(repository,0x27119CFA);Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        var presets=MapTextPresets.Create();var palettes=MapSpritePalettes.Create();
        unknown=text.Get("detail_unknown");increase=presets[38].LT.CpuPacked;decrease=presets[40].LT.CpuPacked;
        source.Art(this,1,source.Rectangle(1),assets,shader);
        current=Layer(2,"CurrentOfficerRadar",15);comparison=Layer(3,"BaseOfficerRadar",14);
        source.Art(this,4,source.Rectangle(4),assets,shader);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        for(int axis=0;axis<5;axis++)
        {
            source.Text(this,5+axis*2,default,messages.Records[0x0238+axis].Text??throw new InvalidDataException("Missing officer ability caption."),font,presets[13]);
            var value=source.Text(this,6+axis*2,default,unknown,font,presets[0]);value.Name="OfficerAbility"+axis;values.Add(value);
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);Apply(null);
        UiSourceRadarMesh Layer(int node,string name,int palette)
        {var mesh=new UiSourceRadarMesh {Name=name};AddChild(mesh);mesh.Configure(source,node,palettes[palette].LT);return mesh;}
    }
    internal void Apply(IReadOnlyList<AreaAdministrationAbilityValue>? abilities)
    {
        if(abilities is not null&&abilities.Count!=5)throw new InvalidDataException("Officer chart requires five abilities.");
        int[]? actual=abilities?.All(a=>a.Current.HasValue)==true?abilities.Select(a=>a.Current!.Value).ToArray():null;
        int[]? baseline=abilities?.All(a=>a.Current.HasValue&&a.Base.HasValue)==true?abilities.Select(a=>Math.Min(a.Base!.Value,a.Current!.Value)).ToArray():null;
        current.ApplyValues(actual,Maximums);comparison.ApplyValues(baseline,Maximums);
        for(int i=0;i<5;i++)
        {
            var label=values[i];var ability=abilities?[i];
            if(ability?.Current is not int value){label.Text=unknown;continue;}
            if(ability.Base is not int basic){label.Text=unknown;continue;}
            if(basic<1||basic==value)label.Text=Number(value);
            else label.SetRuns(new(Number(basic)),new(value>basic?"+"+Number(unchecked(value-basic)):"-"+Number(unchecked(basic-value)),value>basic?increase:decrease));
        }
    }
    private static string Number(int value)=>value.ToString(CultureInfo.InvariantCulture);
}
