using System.Globalization;
using Godot;
using San14.Data;

/// <summary>The real06_09_02_offset_00 row. Before/after counts and their
/// differences are supplied by the same captured setup projection.</summary>
internal partial class DistrictSetupDistrictComparisonView:Control
{
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private UiSourceLayout source=null!;
    private readonly Dictionary<int,UiSourceText> labels=[];
    private readonly ArmyMapUiSources icons=ArmyMapUiSources.Create();
    private Control? marker;
    private int? number;
    private string unknown="",language="";
    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,PreviewText text)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;unknown=text.Get("detail_unknown");language=text.SourceLocale;
        source=new UiSourceLayout(repository,0xE4B493F4);Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Ignore;
        foreach(var node in source.Nodes.Where(node=>(node.Type is 1 or 3)&&node.Index!=2))
            source.Art(this,node.Index,source.Rectangle(node.Index),assets,shader,sourceMaterial:true);
        foreach(int node in new[]{3,4,6,7,8,10,11})labels.Add(node,source.Text(this,node,default,"",font));
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void Apply(DistrictSetupDistrictComparison? value)
    {
        Visible=value is not null;if(value is null)return;
        if(value.Number is <1 or >8)throw new InvalidDataException("District comparison number requires its source1..8 marker.");
        if(number!=value.Number)
        {
            if(marker is not null){marker.Hide();RemoveChild(marker);marker.QueueFree();}
            number=value.Number;
            marker=source.Art(this,2,source.Rectangle(2),assets,shader,
                commonSpriteOverride:icons.IconName(new(0x230+value.Number,0)),sourceMaterial:true);
            labels[3].Text=repository.LoadMessages(language,San14.Data.Formats.MessageKind.Data).Messages.Sections[1].Records[0x38C+value.Number].Text
                ??throw new InvalidDataException("Missing district comparison name.");
        }
        labels[4].Text=Number(value.FootholdsBefore);labels[6].Text=Number(value.FootholdsAfter);
        labels[7].SetKnownColourLine(Delta(value.FootholdsBefore,value.FootholdsAfter));
        labels[8].Text=Number(value.OfficersBefore);labels[10].Text=Number(value.OfficersAfter);
        labels[11].SetKnownColourLine(Delta(value.OfficersBefore,value.OfficersAfter));
        if(value.UnresolvedOwner is {} missing)SetMeta("district_comparison_issue",missing);
        else if(HasMeta("district_comparison_issue"))RemoveMeta("district_comparison_issue");
    }
    private string Number(int? value)=>value?.ToString(CultureInfo.InvariantCulture)??unknown;
    private string Delta(int? before,int? after)
    {
        if(before is not int first||after is not int second)return unknown;
        int difference=unchecked(second-first);
        return difference>0?"(^02+"+Number(difference)+"^00)":difference<0?"(^03-"+Math.Abs((long)difference).ToString(CultureInfo.InvariantCulture)+"^00)":"( 0)";
    }
}
