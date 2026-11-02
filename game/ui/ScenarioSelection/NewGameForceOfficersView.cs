using Godot;
using San14.Data;
using San14.Client.Gameplay;

internal sealed record NewGameForceOfficersContent(NewGameForceOfficersPreview Source,
    string ForceName, Func<NewGameForceOfficerPreview, OfficerDetailView> Details);

/// <summary>534CF0's ordinary pane14 Person list over the retained preview
/// iterator. Opening a detail preserves this list's tabs, sorting and scroll.</summary>
internal partial class NewGameForceOfficersView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private Func<NewGameForceOfficerPreview, OfficerDetailView>? details;
    private Action? returned;
    private Action<Exception>? failed;
    private OfficerList list = null!;
    private OfficerDetailsView? officerDetails;
    private AdministrationPageChrome.Resources? sourceResources;
    private bool abandoned, busy;
    internal NewGameForceOfficersPreview Publication { get; private set; } = null!;
    internal IReadOnlyList<NewGameForceOfficerPreview> Rows { get; private set; } = [];

    internal void Configure(GameResourceRepository repository, PreviewText text,
        NewGameForceOfficersContent content, Action returned, Action<Exception> failed,
        Func<int>? scrollSpeed = null, AdministrationPageChrome.Resources? resources = null)
    {
        this.repository=repository;this.text=text;this.returned=returned;this.failed=failed;sourceResources=resources;
        details=content.Details;Publication=content.Source;
        Rows=Publication.NativeSortedRows??Publication.Rows;
        var pages=Publication.Pages??throw new InvalidDataException("new-game.officers.Person_list_current");
        Name="NewGameForceOfficers";ZIndex=3;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        list=new OfficerList();AddChild(list);
        list.Configure(repository,text,Publication,pages,ReadAdmission,RequestBack,id=>
        {
            var row=Rows.First(value=>value.OfficerId==id);
            OpenDetails(row);
        },scrollSpeed??(()=>0),resources);
        SetMeta("source_information_pane",0xE);SetMeta("force_id",Publication.ForceId);
        SetMeta("membership_complete",Publication.MembershipComplete);
        SetMeta("native_initial_sort_available",Publication.NativeSortedRows is not null);
        SetMeta("source_force_name",content.ForceName);
    }

    private bool ReadAdmission()=>!abandoned&&!busy&&officerDetails is null&&IsVisibleInTree()&&CanProcess();
    private void OpenDetails(NewGameForceOfficerPreview row)
    {
        if(!ReadAdmission()||details is null||row.DetailOfficerId is null)return;
        var view=new OfficerDetailsView();officerDetails=view;AddChild(view);
        try
        {
            view.Configure(repository,details(row),text,()=>CloseDetails(view),resources:sourceResources);
            list.Hide();list.ProcessMode=ProcessModeEnum.Disabled;
        }
        catch(Exception error)
        {
            CloseDetails(view);
            if(failed is not{} report)throw;
            report(error);
        }
    }
    private void CloseDetails(OfficerDetailsView view)
    {
        if(officerDetails!=view)return;
        officerDetails=null;view.Hide();
        if(view.GetParent()==this)RemoveChild(view);
        view.QueueFree();
        if(!abandoned){list.ProcessMode=busy?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;list.Show();}
    }
    internal void RequestBack()
    {
        if(abandoned||busy)return;
        if(officerDetails is{} view)view.RequestClose();
        else returned?.Invoke();
    }
    internal void SetBusy(bool value)
    {
        busy=value;
        if(list is not null)list.ProcessMode=busy||officerDetails is not null?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
        if(officerDetails is{} view)view.ProcessMode=busy?ProcessModeEnum.Disabled:ProcessModeEnum.Inherit;
    }
    internal void Abandon()
    {
        abandoned=true;details=null;returned=null;failed=null;
        if(officerDetails is{} view)CloseDetails(view);
        if(list is not null)list.ProcessMode=ProcessModeEnum.Disabled;
        Hide();
    }

    private partial class OfficerList : PagedInformationListView
    {
        internal void Configure(GameResourceRepository repository,PreviewText text,
            NewGameForceOfficersPreview publication,OfficerInformationPageSet pages,
            Func<bool> admitted,Action close,Action<int> inspect,Func<int> scrollSpeed,
            AdministrationPageChrome.Resources? resources)
            =>ConfigureList(repository,text,new PreviewContent(publication,pages),admitted,close,inspect,
                scrollSpeed,"NewGameForceOfficerList","officer_information_group",resources,
                sourcePersonCellBounds: true, rowExtentWidth: 1464);
    }

    private sealed class PreviewContent(NewGameForceOfficersPreview publication,OfficerInformationPageSet pages)
        : IPagedInformationListContent
    {
        private readonly OfficerInformationListContent inner=new(pages);
        public string Title=>inner.Title;
        public IReadOnlyList<PagedInformationGroup> Groups=>inner.Groups;
        public IReadOnlyList<int> CurrentIdentities=>inner.CurrentIdentities;
        public int? SelectedIdentity=>inner.SelectedIdentity;
        public bool? NameSortAscending=>inner.NameSortAscending;
        public PagedInformationPage ReadPage(int index)
        {
            var page=inner.ReadPage(index);
            return !publication.MembershipComplete&&page.UnresolvedOwner is null
                ?page with {UnresolvedOwner=string.Join(";",publication.UnresolvedInputs)}:page;
        }
        public AreaAdministrationQuery<PagedInformationPage> Sort(int group,int column,bool ascending)
            =>inner.Sort(group,column,ascending);
        public void SelectSourceIndex(int index)=>inner.SelectSourceIndex(index);
        public AreaAdministrationQuery<PagedInformationPage> AdoptDetailOrder(int group,
            IReadOnlyList<int> identities,int selected,bool? nameSortAscending)
            =>inner.AdoptDetailOrder(group,identities,selected,nameSortAscending);
    }
}
