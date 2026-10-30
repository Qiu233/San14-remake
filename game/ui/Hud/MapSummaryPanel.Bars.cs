using Godot;
using San14.Data;

internal partial class MapSummaryPanel
{
    private readonly Dictionary<int,(int? Palette,UiSourceBarGauge View)> bars=[];
    private void ApplyBars(MapSummaryPage page)
    {
        foreach(var(_,view) in bars.Values)view.Hide();
        foreach(var bar in page.Bars)
        {
            if(!bars.TryGetValue(bar.Node,out var existing)||existing.Palette!=bar.Palette)
            {
                Retire(existing.View);
                var box=source!.Rectangle(bar.Node);
                var view=new UiSourceBarGauge {Name="SummaryBar"+bar.Node,Position=box.Position,ZIndex=bar.Node};AddChild(view);
                view.Configure(resources.Bar,resources.Assets,resources.Shader,bar.Palette);view.Scale=box.Size/view.Size;
                if(bar.InitialCurrent is int initial&&bar.InitialMaximum is int maximum)view.Apply(initial,maximum);
                existing=(bar.Palette,view);bars[bar.Node]=existing;
            }
            existing.View.Apply(bar.Current,bar.Maximum);existing.View.Visible=Shown(bar.Node,page);
        }
    }
}
