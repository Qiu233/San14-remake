using Godot;
using San14.Data;

/// <summary>The startup force's explicit Advantage list uses the original
/// information descriptor, groups and columns; no current campaign is made.</summary>
internal partial class NewGameGeographicAdvantagesView : PagedInformationListView
{
    private bool busy,abandoned;
    private Func<bool>? admitted;
    internal NewGameForcePreview Publication { get; private set; } = null!;

    internal void Configure(GameResourceRepository repository,PreviewText text,
        NewGameForcePreview publication,DefinitionInformationPageSet pages,
        Func<bool> admitted,Action returned,Func<int> scrollSpeed,
        AdministrationPageChrome.Resources resources)
    {
        Publication=publication;this.admitted=admitted;
        ConfigureCommonBackdrop(repository);
        ConfigureList(repository,text,new DefinitionInformationListContent(pages),ReadAdmission,
            returned,_=>{},scrollSpeed,"NewGameGeographicAdvantages","new_game_geographic_group",resources);
        SetMeta("source_information_pane",0x27);
        SetMeta("force_id",publication.ForceId);
    }

    private bool ReadAdmission()=>!abandoned&&!busy&&CanProcess()&&(admitted?.Invoke()??false);
    internal override void RequestClose(){if(ReadAdmission())base.RequestClose();}
    internal void SetBusy(bool value)=>busy=value;
    internal void Abandon(){abandoned=true;admitted=null;Hide();}
    public override void _ExitTree(){admitted=null;base._ExitTree();}
}
