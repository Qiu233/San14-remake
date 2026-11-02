using Godot;
using San14.Data;

internal partial class MapSummaryPanel
{
    private void ApplyPortrait(MapSummaryPortrait portrait,MapSummaryPage page)
    {
        if(!portraits.TryGetValue(portrait.Node,out var existing)||existing.Identity!=portrait)
        {
            Retire(existing.View);portraits.Remove(portrait.Node);
            var box=source!.Rectangle(portrait.Node);
            Control? view=null;
            if(portrait.StandingPortrait is{} standing)
            {
                var pose=new UiSourceStandingPortrait {Name="SummaryStandingPortrait"+portrait.Node,Position=box.Position};
                AddChild(pose);pose.Configure(resources.Repository,resources.Assets,resources.Shader,
                    standing.LoadSources(resources.Assets.Portraits.RequireSource));
                pose.Scale=box.Size/pose.Size;view=pose;
            }
            else if(portrait.Portrait is {Kind:OfficerPortraitRouteKind.StockNamed,ResourceName:{} pack,SpriteName:{} sprite})
            {
                var faceLayout=portrait.LayoutName is{} name?resources.Layout(name):source;
                int pane=portrait.SourceNode??portrait.Node;
                var face=new UiSourcePortrait {Name="SummaryFace"+portrait.Node,Position=box.Position,Size=box.Size};
                AddChild(face);face.ConfigureLayoutPane(resources.Repository,resources.Assets,resources.Shader,
                    resources.Assets.Portraits.RequireSource(pack,sprite),faceLayout,pane);view=face;
            }
            if(view is null)return;
            view.ZIndex=portrait.Node;existing=(portrait,view);portraits[portrait.Node]=existing;
        }
        existing.View.Visible=Shown(portrait.Node,page);
    }
}
