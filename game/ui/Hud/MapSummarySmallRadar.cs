using Godot;
using San14.Data;

/// <summary>CCmnRaderChartSmall's actual normal layout and five-axis mesh.</summary>
internal partial class MapSummarySmallRadar : Control
{
    private UiSourceRadarMesh mesh=null!;
    private int[] maxima=[];
    internal void Configure(MapSummaryResources resources)
    {
        var source=resources.Layout("cmn_chart_s");
        MouseFilter=MouseFilterEnum.Ignore;Size=source.Rectangle(0).Size;
        foreach(var node in source.Nodes.Where(node=>node.Type is 1 or 3))
            source.Art(this,node.Index,source.Rectangle(node.Index),resources.Assets,resources.Shader);
        mesh=new(){Name="SummarySmallRadar"};AddChild(mesh);
        mesh.Configure(source,2,MapSpritePalettes.Create()[14].LT);
        var limits=DeploymentRadarPresentation.ForLocale(resources.Font.Source.Locale).Maximums;
        maxima=[limits.Mobility,limits.FieldAttack,limits.SiegeTroopAttack,limits.WallAttack,limits.Defense];
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }
    internal void Apply(IReadOnlyList<int> values)=>mesh.ApplyValues(values,maxima);
}
