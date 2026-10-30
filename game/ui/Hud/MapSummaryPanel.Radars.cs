using Godot;
using San14.Data;

internal partial class MapSummaryPanel
{
    private readonly Dictionary<int,MapSummarySmallRadar> radars=[];
    private void ApplyRadars(MapSummaryPage page)
    {
        foreach(var view in radars.Values)view.Hide();
        foreach(var radar in page.Radars)
        {
            if(!radars.TryGetValue(radar.Node,out var view))
            {
                var box=source!.Rectangle(radar.Node);
                view=new(){Name="SummaryRadar"+radar.Node,Position=box.Position,ZIndex=radar.Node};AddChild(view);
                view.Configure(resources);view.Scale=box.Size/view.Size;radars.Add(radar.Node,view);
            }
            view.Apply(radar.Values);view.Visible=Shown(radar.Node,page);
        }
    }
}
