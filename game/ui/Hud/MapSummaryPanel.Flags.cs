using Godot;
using San14.Data;

internal partial class MapSummaryPanel
{
    private readonly Dictionary<int,(MapSummaryForceFlag Identity,UiSourceForceFlag View)> flags=[];
    private void ApplyFlags(MapSummaryPage page)
    {
        foreach(var(_,view) in flags.Values)view.Hide();
        foreach(var flag in page.Flags)
        {
            if(!flags.TryGetValue(flag.Node,out var existing)||existing.Identity!=flag)
            {
                Retire(existing.View);
                var box=source!.Rectangle(flag.Node);
                var view=new UiSourceForceFlag {Name="SummaryFlag"+flag.Node,Position=box.Position,ZIndex=flag.Node};AddChild(view);
                view.Configure(resources.Repository,resources.Assets,resources.Shader,resources.Font,flag.Appearance,neighbor:flag.Neighbor);
                view.Scale=box.Size/view.Size;existing=(flag,view);flags[flag.Node]=existing;
            }
            existing.View.Visible=Shown(flag.Node,page);
        }
    }
}
