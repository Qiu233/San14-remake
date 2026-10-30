using Godot;
using San14.Data;

internal partial class MapSummaryPanel
{
    private readonly List<AreaAdministrationTraitMarker> traitRows=[];
    private void ApplyTraits(MapSummaryPage page)
    {
        foreach(var row in traitRows)row.Hide();
        if(page.Traits.Count==0)return;
        var box=source!.Rectangle(3);
        while(traitRows.Count<page.Traits.Count)
        {
            var view=new AreaAdministrationTraitMarker {Name="SummaryTrait"+traitRows.Count,
                Position=box.Position+new Vector2(0,38*traitRows.Count),ZIndex=3};AddChild(view);
            view.Configure(resources.Layout(0xD493EA98),resources.Assets,resources.Shader,resources.Font,box.Size);
            view.MouseFilter=MouseFilterEnum.Ignore;
            traitRows.Add(view);
        }
        for(int i=0;i<page.Traits.Count;i++)
        {
            var row=page.Traits[i];traitRows[i].Apply(row.SourceIndex,row.Text,row.Style,row.Active);
            traitRows[i].Visible=Shown(3,page);
        }
    }
}
