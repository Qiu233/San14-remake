using Godot;
using San14.Core;
using San14.Data;

internal sealed record UnitFacilitySelectionEntry(FacilityConstructionChoice Choice, bool? Allowed,
    string? Reason = null, bool CommandSupported = true)
{
    internal bool CanConfirm => Allowed == true && CommandSupported;
}

/// <summary>Two actual source groups over the existing directory and command
/// eligibility. Accepting a row still only enters the existing map placement.</summary>
internal partial class UnitFacilitySelectionView : Control
{
    private AdministrationPageChrome chrome=null!;
    private GameResourceRepository repository=null!;
    private PreviewText text=null!;
    private IReadOnlyList<UnitFacilitySelectionEntry> entries=[];
    private FacilitySelectionPageSet? pages;
    private MapInformationListView? table;
    private Func<int> scrollSpeed=null!;
    private readonly Dictionary<int,int> scrolls=[];
    private readonly Dictionary<int,Dictionary<string,bool>> directions=[];
    private readonly Dictionary<int,(string? Column,bool Ascending)> sorts=[];
    private Action<FacilityConstructionChoice?>? complete;
    private FacilityConstructionChoice? selected;
    private string? focusedKey;
    private int group;
    internal bool SelectionActive=>complete is not null&&!IsQueuedForDeletion()&&IsInsideTree()&&IsVisibleInTree()&&CanProcess();
    internal int? CurrentChoiceDefinitionKey=>SelectionActive&&FacilityConstructionIntent.IsDefinitionKey(focusedKey)
        ?int.Parse(focusedKey!.AsSpan("san14-object-definition:".Length),System.Globalization.CultureInfo.InvariantCulture):null;

    internal void Configure(GameResourceRepository repository,PreviewText text,
        IReadOnlyList<UnitFacilitySelectionEntry> entries,Action<FacilityConstructionChoice?> complete,
        IReadOnlyList<string>? preselectedDefinitionKeys=null,FacilitySelectionPageSet? pages=null,Func<int>? scrollSpeed=null,
        AdministrationPageChrome.Resources? resources=null)
    {
        this.repository=repository;this.text=text;this.entries=entries;this.complete=complete;this.pages=pages;
        this.scrollSpeed=scrollSpeed??(()=>0);
        Name="UnitFacilitySelection";ZIndex=4;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome=new AdministrationPageChrome();AddChild(chrome);
        chrome.Configure(repository,text,0x5B9,RequestCancel,Accept,resources:resources);
        chrome.ApplyDefaultHint(text.Get("play_facility_construction"));
        var seeds=entries.Where(entry=>preselectedDefinitionKeys?.Contains(entry.Choice.DefinitionKey)==true).ToArray();
        if(seeds.Length==1)
        {focusedKey=seeds[0].Choice.DefinitionKey;if(seeds[0].CanConfirm)selected=seeds[0].Choice;}
        if(pages is not null)ShowGroup(0);
        else chrome.ApplyDefaultHint(text.Get("play_facility_order_unavailable"));
        chrome.ApplyDecision(selected is not null,true);
    }
    private void ShowGroup(int index)
    {
        if(pages is null||complete is null||(uint)index>=pages.Groups.Length)return;
        if(table is {} previous)
        {
            scrolls[group]=previous.ScrollValue;previous.Abandon();
            previous.GetParent()?.RemoveChild(previous);previous.QueueFree();
        }
        group=index;var page=pages.ReadPage(index);var descriptor=pages.Groups[index];
        var next=new MapInformationListView();table=next;chrome.Canvas.AddChild(next);
        if(!directions.TryGetValue(index,out var retained))directions[index]=retained=[];
        var sorted=sorts.GetValueOrDefault(index,(null,true));
        next.Configure(repository,text,"",descriptor.Columns.Select(c=>
            new MapInformationListColumn(c.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),c.Caption,c.Width,c.CanSort,c.InitialAscending)).ToArray(),
            Rows(page),()=>SelectionActive&&table==next,RequestCancel,sourceIndex=>
            {
                var row=page.Rows.First(row=>row.SourceIndex==sourceIndex);
                var entry=entries.First(entry=>entry.Choice.DefinitionKey==row.DefinitionKey);
                if(!SelectionActive||!entry.CanConfirm)return;
                focusedKey=entry.Choice.DefinitionKey;selected=entry.Choice;chrome.ApplyDecision(true,true);
            },scrollSpeed,(key,ascending)=>
            {
                int column=int.Parse(key,System.Globalization.CultureInfo.InvariantCulture);
                var result=pages.Sort(index,column,ascending);
                if(!result.Available||result.Value is not {} replacement)
                {chrome.ApplyHoverHint(text.Get("information_partial"));return null;}
                page=replacement;sorts[index]=(key,ascending);return Rows(page);
            },tabCaptions:pages.Groups.Select(g=>g.Caption).ToArray(),selectedTab:index,selectTab:ShowGroup,
            includeHeader:false,completeColumnProfile:true,normalColumnLayout:true,
            initialSortColumn:sorted.Item1,initialSortAscending:sorted.Item2,retainSortDirections:true,
            retainedSortDirections:retained,disableUninspectableRows:true,hoverSourceIndex:sourceIndex=>
            {
                var row=sourceIndex is int slot?page.Rows.FirstOrDefault(row=>row.SourceIndex==slot):null;
                if(row is null){chrome.ApplyHoverHint(null);return;}
                var entry=entries.First(entry=>entry.Choice.DefinitionKey==row.DefinitionKey);
                chrome.ApplyHoverHint(!entry.CommandSupported?text.Get("play_facility_order_unavailable"):
                    entry.Reason=="structure.selection.policy"?text.Get("play_facility_blocked_policy"):
                    entry.Allowed!=true?text.Get("play_facility_unavailable"):entry.Choice.Name);
            },resources:new(chrome.Assets,chrome.Font,chrome.SpriteShader));
        // The actual existing selection chrome owns Return/Decide. Keep the
        // shared list's transparent surface from blocking those live buttons.
        next.MouseFilter=MouseFilterEnum.Ignore;next.SourceCanvas.MouseFilter=MouseFilterEnum.Ignore;
        next.SourceCanvas.GetNode<Control>("InformationListReturn").Hide();
        if(focusedKey is not null)
        {
            var focused=page.Rows.FirstOrDefault(row=>row.DefinitionKey==focusedKey);
            if(focused is not null)next.ApplyDetailReturn(Rows(page),focused.SourceIndex,null);
        }
        next.RestoreScroll(scrolls.GetValueOrDefault(index));
        SetMeta("facility_selection_group",index);

        IReadOnlyList<MapInformationListRow> Rows(FacilitySelectionPage current)=>current.Rows.Select(row=>
        {
            var entry=entries.First(entry=>entry.Choice.DefinitionKey==row.DefinitionKey);
            return new MapInformationListRow(row.SourceIndex,row.Cells.Select(c=>
                c.UnresolvedOwner is null?c.Text??"":text.Get("detail_unknown")).ToArray(),entry.CanConfirm,
                descriptor.Columns.Select(c=>new MapInformationListCellStyle(c.CellFormat)).ToArray());
        }).ToArray();
    }
    private void Accept(){if(selected is not null)Complete(selected);}
    internal void RequestCancel()=>Complete(null);
    internal void ApplyHint(string value)=>chrome.ApplyDefaultHint(value);
    private void Complete(FacilityConstructionChoice? choice)
    {var callback=complete;complete=null;table?.Abandon();callback?.Invoke(choice);}
    internal void Abandon(){complete=null;table?.Abandon();chrome?.Abandon();Hide();}
}
