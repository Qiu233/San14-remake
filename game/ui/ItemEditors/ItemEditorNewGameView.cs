using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>CNewItemManageDlg: the fresh-game choices remain in this draft
/// until its actual confirmation. No title archive is changed by this page.</summary>
internal partial class ItemEditorNewGameView : Control
{
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private ItemEditorCatalog catalog = null!;
    private ItemEditorLibrary library = null!;
    private ItemEditorNewGameDraft draft = null!;
    private AdministrationPageChrome chrome = null!;
    private Func<bool>? admitted;
    private Action<ItemEditorNewGameSelection>? accepted;
    private Action? returned;
    private Func<int> scrollSpeed = () => 3;
    private readonly List<UiSourceCommandButton> commands = [];
    private CommandSelectionView? selector;
    private DeploymentTargetConfirmation? confirmation;
    private ItemEditorCustodyView? custody;
    private bool abandoned;
    internal void Configure(GameResourceRepository repository, PreviewText text, ItemEditorCatalog catalog,
        ItemEditorLibrary library, ItemEditorNewGameDraft draft, Func<bool> admitted,
        Action<ItemEditorNewGameSelection> accepted, Action returned, Func<int> scrollSpeed)
    {
        this.repository=repository; this.text=text; this.catalog=catalog; this.library=library; this.draft=draft;
        this.admitted=admitted; this.accepted=accepted; this.returned=returned; this.scrollSpeed=scrollSpeed;
        Name="NewGameItemManagement"; ZIndex=5; MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome=new(); AddChild(chrome); chrome.Configure(repository,text,0x790,RequestBack,Accept);
        var root=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"02_04_00_edit_top_new"));
        foreach(var node in root.Nodes.Where(node=>(node.Type is 1 or 3)&&node.ParentIndex==0&&node.Index!=1))
            root.Art(chrome.Canvas,node.Index,root.Rectangle(node.Index),chrome.Assets,chrome.SpriteShader);
        var source=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"02_04_00_btn_set_offset_00_ed_pk"));
        var menu=new Control {Name="NewGameItemButtons",Position=root.Rectangle(2).Position,
            Size=source.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Ignore};chrome.Canvas.AddChild(menu);
        foreach(var node in source.Nodes.Where(node=>(node.Type is 1 or 3)&&node.ParentIndex==0))
            source.Art(menu,node.Index,source.Rectangle(node.Index),chrome.Assets,chrome.SpriteShader);
        var heading=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"02_05_00_offset_01"));
        foreach(var (mount,caption) in new[]{(3,0x7D7),(12,0x7D6)})
        {
            var position=source.Rectangle(mount).Position;
            foreach(var node in heading.Nodes.Where(node=>node.Type is 1 or 3))
                heading.Art(menu,node.Index,heading.Rectangle(node.Index,position),chrome.Assets,chrome.SpriteShader);
            heading.Text(menu,2,position,catalog.Data(caption),chrome.Font);
        }
        int[] mounts=[6,9,15], captions=[0x9A,0x737,0x98];
        Action[] actions=[ChooseCreated,ChooseCustody,ChooseHistorical];
        for(int index=0;index<mounts.Length;index++)
        {
            var box=source.Rectangle(mounts[index]);
            var button=new UiSourceCommandButton {Position=box.Position,Size=box.Size,InputAdmission=CanEdit};
            menu.AddChild(button);commands.Add(button);
            button.Configure(repository,chrome.Assets,chrome.SpriteShader,chrome.Font,catalog.Data(captions[index]),actions[index]);
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri); Refresh();
    }
    private bool PageAdmitted()=>!abandoned&&IsVisibleInTree()&&admitted?.Invoke()==true;
    private bool CanEdit()=>PageAdmitted()&&selector is null&&confirmation is null&&custody is null;
    private void Refresh()
    {
        if(abandoned)return;
        bool active=CanEdit(); commands[0].Disabled=!active||library.Created.IsEmpty;
        commands[1].Disabled=!active||!library.Created.Any(item=>draft.IsSelected(item.Id));
        commands[2].Disabled=!active||library.Historical.IsEmpty;
        chrome.ApplyDecision(active,true);chrome.ApplyReturn(active);
    }
    private ItemEditorCandidate Created(CreatedItem item)=>new(item.Id,item.Name,item.Description,item.Values,item.Values,false);
    private CommandSelectionRow Row(ItemEditorCandidate item)=>new(item.Id,
        [new(item.Name,SortUnavailable:true),new(catalog.Kind(item.Values),SortUnavailable:true),
         new(catalog.Ability(item.Values),SortUnavailable:true),new(catalog.Effect(item.Values),SortUnavailable:true),
         new(item.Values.Worth.ToString(),item.Values.Worth)],true);
    private void ChooseCreated()
    {
        if(!CanEdit())return;
        Select(0x7D8,library.Created.Select(Created).ToArray(),library.Created.Where(item=>draft.IsSelected(item.Id)).Select(item=>item.Id).ToArray(),
            ids=>{var selected=ids.ToHashSet();foreach(var item in library.Created)draft.SelectCreated(item.Id,selected.Contains(item.Id));},Game(0x290E));
    }
    private void ChooseHistorical()
    {
        if(!CanEdit())return;
        var rows=catalog.Historical(library,editedOnly:true);
        Select(0x735,rows,rows.Where(item=>draft.IsSelected(item.Id)).Select(item=>item.Id).ToArray(),
            ids=>{var selected=ids.ToHashSet();foreach(var item in rows)draft.SelectHistorical(item.Id,selected.Contains(item.Id));},Game(0x28C2));
    }
    private void Select(int title,IReadOnlyList<ItemEditorCandidate> rows,IReadOnlyList<int> initial,
        Action<IReadOnlyList<int>> apply,string? guide=null)
    {
        var view=new CommandSelectionView();selector=view;AddChild(view);
        view.Configure(repository,text,title,
            [new("name",catalog.Data(0x246),420),new("kind",catalog.Data(0x401),220),new("ability",catalog.Data(0x452),240),
             new("effect",catalog.Data(0x411),240),new("worth",catalog.Data(0x423),180,true)],
            rows.Select(Row).ToArray(),initial,rows.Count,PageAdmitted,result=>
            {
                if(abandoned||selector!=view)return;
                selector=null;view.Abandon();view.QueueFree();if(result is not null)apply(result);Refresh();
            },scrollSpeed,guide);
        Refresh();
    }
    private void ChooseCustody()
    {
        if(!CanEdit())return;
        var rows=library.Created.Where(item=>draft.IsSelected(item.Id)).Select(Created).ToArray();
        Select(0x5AA,rows,[],ids=>{if(ids.Count>0)OpenCustody(ids);});
    }
    private void OpenCustody(IReadOnlyList<int> ids)
    {
        if(abandoned)return;
        var view=new ItemEditorCustodyView();custody=view;AddChild(view);chrome.Hide();
        view.Configure(repository,text,catalog,draft,ids,PageAdmitted,result=>
        {
            if(abandoned||custody!=view)return;
            custody=null;view.Abandon();view.QueueFree();chrome.Show();
            if(result is { } choice)
            {
                void Apply(){draft.AssignCustody(ids,choice.OfficerId,choice.Flag8);Refresh();}
                if(choice.OfficerId is null&&ids.Any(id=>draft.Custody(id).Lifecycle.Status?.Code!=2))
                    Confirm(0x5F41,yes=>{if(yes)Apply();});
                else Apply();
            }
            Refresh();
        },scrollSpeed);Refresh();
    }
    private string Game(int id)=>RegisteredOfficerExchangeMessages.Format(repository,text.SourceLocale,id);
    private void Confirm(int id,Action<bool> closed)
    {
        var view=new DeploymentTargetConfirmation();confirmation=view;AddChild(view);
        view.ConfigureWrappedMessage(repository,text,Game(id),result=>
        {
            if(abandoned||confirmation!=view)return;
            confirmation=null;view.Abandon();view.QueueFree();closed(result);Refresh();
        });Refresh();
    }
    private void Accept(){if(CanEdit())accepted?.Invoke(draft.Commit());}
    internal void RequestBack()
    {
        if(selector is not null){selector.RequestCancel();return;}
        if(confirmation is not null){confirmation.RequestCancel();return;}
        if(custody is not null){custody.RequestBack();return;}
        if(CanEdit())
        {
            var selected=draft.Commit();
            if(!selected.HistoricalIds.IsEmpty||!selected.CreatedIds.IsEmpty)Confirm(0x28C8,yes=>{if(yes)returned?.Invoke();});
            else returned?.Invoke();
        }
    }
    internal void Abandon()
    {
        abandoned=true;admitted=null;accepted=null;returned=null;
        selector?.Abandon();confirmation?.Abandon();custody?.Abandon();chrome?.Abandon();
        foreach(var button in commands)button.Abandon();Hide();
    }
}
