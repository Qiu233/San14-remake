using Godot;
using San14.Data;

/// <summary>The normal scenery row's retained single-column selector and original
/// Collection scenery body. It does not enter the title Collection world.</summary>
internal partial class MapInformationSceneryDetailView : DetailsView
{
    private CollectionDetailView body=null!;
    private bool abandoned;
    internal void Configure(GameResourceRepository repository,PreviewText text,SceneryCatalogInformation page,
        IReadOnlyList<CollectionDetailEntry> entries,Func<bool> admitted,Action close,Action<int> select,Func<int> scrollSpeed)
    {
        ConfigureSourcePage("InformationSceneryDetail",null,close);
        body=new();AddChild(body);
        body.Configure(repository,text,null,null,page.Detail,entries,()=>!abandoned&&admitted(),close,select,scrollSpeed,currentScenery:page);
    }
    internal void Apply(SceneryCatalogInformation page)=>body.ApplyScenery(page.Detail);
    internal override void RegisterHeader(UiSourceHeaderStack owner)=>body.RegisterHeader(owner);
    public override void _ExitTree(){abandoned=true;body?.Abandon();base._ExitTree();}
}
