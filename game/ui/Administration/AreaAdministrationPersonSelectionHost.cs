using Godot;
using San14.Data;

/// <summary>One region's retained seven-tab selector. Publication occurrences
/// stay distinct from physical receivers and from raw appointment identities.</summary>
internal sealed class AreaAdministrationPersonSelectionHost(Node parent, GameResourceRepository repository,
    PreviewText text, AreaAdministrationDraft draft, Func<bool> admitted, Action<string> unavailable,
    Func<int> scrollSpeed, Func<int,bool>? candidateAdmission = null) : IDisposable
{
    private readonly AreaAdministrationPersonListState state = new();
    private AreaAdministrationPersonSelectionView? active;
    private bool disposed;
    internal bool IsOpen => active is not null;
    internal int? SelectedPersonId => active?.SelectedPersonId;

    internal void Open(AreaAdministrationPersonSelection selection, Action<int?> completed)
    {
        if (disposed || !admitted() || active is not null) return;
        long revision = draft.Revision;
        IReadOnlyList<int> order = state.Candidates is null
            ? selection.InitialOrder.Available ? selection.InitialOrder.Value! : Enumerable.Range(0,selection.Candidates.Count).ToArray()
            : state.Order.ToArray();
        var first = QueryPage(state.ActivePage,order);
        if (!first.Available) { unavailable(first.UnresolvedInput!); return; }
        var preselected = draft.QueryPersonPreselection(selection);
        if (!preselected.Available) { unavailable(preselected.UnresolvedInput!); return; }
        var view = new AreaAdministrationPersonSelectionView { CandidateAdmission=candidateAdmission }; active = view;
        bool Current() => !disposed && admitted() && active == view && revision == draft.Revision;
        try
        {
            parent.AddChild(view);
            view.Configure(repository,text,AreaAdministrationPersonColumns.For(state.ActivePage),first.Value!,state,
                preselected.Value,scrollSpeed(),(page,rows,column,ascending)=>
                {
                    if (!Current()) return new(null,"area-draft.person_selector_stale");
                    if (column is >=132 and <=134 || column==139)
                        return draft.QueryPersonDevelopmentOrder(selection,rows,column==139?4:column-131,ascending);
                    int? trait = column switch {113=>109,127=>179,114=>110,126=>155,115=>111,116=>112,
                        117=>113,118=>114,119=>115,120=>116,125=>181,211=>138,207=>219,205=>215,206=>218,_=>null};
                    return trait is int raw ? draft.QueryPersonTraitOrder(selection,rows,raw,ascending)
                        : draft.QueryPersonOrder(selection,rows,CityDomesticPersonListContent.SortColumn(column),
                            ascending?CityDomesticPersonSortDirection.Natural:CityDomesticPersonSortDirection.Reversed);
                },value=>
                {
                    if (!Current()) return;
                    Close(); completed(value);
                },reason=> { view.ApplyNotice(text.Get("detail_unknown")); unavailable(reason); },
                (page,rows)=>Current()?QueryPage(page,rows):new(null,"area-draft.person_selector_stale"),scrollSpeed);
            if (!selection.InitialOrder.Available)
            {
                view.SetMeta("initial_order_unresolved",selection.InitialOrder.UnresolvedInput!);
                view.ApplyNotice(text.Get("detail_unknown"));
            }
        }
        catch { Close(); throw; }

        AreaAdministrationQuery<IReadOnlyList<CityDomesticPersonEntry>> QueryPage(int page,IReadOnlyList<int> rows)
        {
            return page switch
            {
                0=>Wrap(draft.QueryPersonBasicPage(selection,rows),p=>AreaAdministrationPersonListContent.Create(repository,text,draft.PersonSelectionWorld,p)),
                1=>Wrap(draft.QueryPersonTraitPage(selection,rows),p=>AreaAdministrationPersonListContent.Create(repository,text,draft.PersonSelectionWorld,p)),
                2=>Wrap(draft.QueryPersonAffiliationPage(selection,rows),p=>AreaAdministrationPersonListContent.Create(repository,text,draft.PersonSelectionWorld,p)),
                3=>Wrap(draft.QueryPersonAbilityPage(selection,rows),p=>AreaAdministrationPersonListContent.Create(repository,text,draft.PersonSelectionWorld,p)),
                4=>Wrap(draft.QueryPersonMeritPage(selection,rows),p=>AreaAdministrationPersonListContent.Create(repository,text,draft.PersonSelectionWorld,p)),
                5=>Wrap(draft.QueryPersonTaskPage(selection,rows),p=>AreaAdministrationPersonListContent.Create(repository,text,draft.PersonSelectionWorld,p)),
                6=>Wrap(draft.QueryPersonPersonalPage(selection,rows),p=>AreaAdministrationPersonListContent.Create(repository,text,draft.PersonSelectionWorld,p)),
                _=>throw new ArgumentOutOfRangeException(nameof(page))
            };
        }
        static AreaAdministrationQuery<IReadOnlyList<CityDomesticPersonEntry>> Wrap<T>(
            AreaAdministrationQuery<AreaAdministrationPersonPage<T>> query,
            Func<AreaAdministrationPersonPage<T>,IReadOnlyList<CityDomesticPersonEntry>> format) =>
            query.Available ? new(format(query.Value!),null) : new(null,query.UnresolvedInput);
    }
    internal bool TryCancel()
    { if (active is not { } view) return false; view.RequestCancel(); return true; }
    private void Close()
    {
        var old=active;active=null;
        if(old is not null&&GodotObject.IsInstanceValid(old)){old.Abandon();old.QueueFree();}
    }
    public void Dispose(){if(disposed)return;disposed=true;Close();}
}
