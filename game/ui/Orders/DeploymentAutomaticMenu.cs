using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>Make's five ordinary automatic-composition strategies. The caller
/// supplies real strategy availability and owns the resulting draft.</summary>
internal partial class DeploymentAutomaticMenu : UiSourcePulldownMenu
{
    private Action<int?>? closed;
    private bool completing;

    internal void Configure(GameResourceRepository repository,PreviewText text,IReadOnlyList<bool> available,
        Action<int?> closed,Action<Control,string?> hintChanged,AdministrationPageChrome.Resources? resources=null)
    {
        if(available.Count!=5)throw new ArgumentException("Automatic composition has five strategies.",nameof(available));
        Name="AutomaticDeploymentMenu";this.closed=closed;
        var page=new UiSourceLayout(repository,0xFC796D9B);var mount=page.Rectangle(53);
        var messages=repository.LoadMessages(text.SourceLocale,MessageKind.Data).Messages.Sections[1];
        var game=repository.LoadMessages(text.SourceLocale,MessageKind.Game).Messages.Sections[2];
        var items=Enumerable.Range(0,5).Select(index=>new UiSourcePulldownItem(
            messages.Records[0x037F+index].Text??throw new InvalidDataException("Missing automatic strategy caption."),
            available[index],game.Records[available[index]?0x0860+index:0x0865].Literals.Single().Text)).ToArray();
        base.Configure(repository,text.SourceLocale,new(new((int)mount.Position.X-299,(int)mount.Position.Y-6),mount.Size),
            items,UiSourcePulldownOutsideClick.PressPassthrough,Finish,hintChanged,resources:resources);
        for(int index=0;index<5;index++)GetNode<UiSourcePulldownButton>("MenuItem"+index).Name="AutomaticStrategy"+index;
        // The source's generic menu background is explicitly hidden.
        SetMeta("source_layout",page.Source.Source.ResourceUri);SetMeta("source_node_index",53);
    }

    private void Finish(int? strategy)
    {
        if(IsAbandoned||completing)return;completing=true;
        try{closed?.Invoke(strategy);}finally{Abandon();}
    }
    internal override void Abandon()
    {
        closed=null;base.Abandon();
    }
    public override void _ExitTree()
    {closed=null;base._ExitTree();}
}
