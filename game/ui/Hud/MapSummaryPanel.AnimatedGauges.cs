using Godot;
using San14.Data;

internal partial class MapSummaryPanel
{
    private readonly Dictionary<string,(int First,int Second,UiSourceRadialGauge View)> animatedGauges=[];
    private void ApplyAnimatedGauge(MapSummaryGauge gauge,MapSummaryPage page)
    {
        string name=gauge.AnimationName!;
        if(!animatedGauges.TryGetValue(name,out var existing)||existing.First!=gauge.Node||existing.Second!=gauge.ReferenceNode)
        {
            Retire(existing.View);
            var view=new UiSourceRadialGauge {Name="SummaryRadialGauge",ZIndex=gauge.Node};AddChild(view);
            view.Configure(source!,resources.Assets,resources.Shader,name,gauge.Node,gauge.ReferenceNode,false);
            existing=(gauge.Node,gauge.ReferenceNode,view);animatedGauges[name]=existing;
        }
        int frame=gauge.Maximum<1?100:unchecked(gauge.Current*100)/gauge.Maximum;
        existing.View.ApplySourceFrame(frame);
        existing.View.Visible=Shown(gauge.Node,page)&&Shown(gauge.ReferenceNode,page);
    }
}
