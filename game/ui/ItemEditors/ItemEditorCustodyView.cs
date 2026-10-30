using Godot;
using San14.Data;

internal sealed record ItemEditorCustodyChoice(int? OfficerId, bool Flag8);

/// <summary>The item-holder child owns only its two local choices.
/// Its parent applies custody to its own draft after confirmation.</summary>
internal partial class ItemEditorCustodyView : Control
{
    private AdministrationPageChrome chrome = null!;
    private GameResourceRepository repository = null!;
    private PreviewText text = null!;
    private ItemEditorCatalog catalog = null!;
    private IReadOnlyList<ItemEditorCustodyOfficerChoice> officers = [];
    private readonly Dictionary<int, string> names = [];
    private Func<bool>? admitted;
    private Func<int> scrollSpeed = () => (int)ListScrollSpeed.Normal;
    private Action<ItemEditorCustodyChoice?>? closed;
    private UiSourceCommandButton holder = null!, clear = null!;
    private UiSourceRadioButton disabledFlag = null!, enabledFlag = null!;
    private CommandSelectionView? selector;
    private IntelligenceSelectionView? currentSelector;
    private Func<AreaAdministrationQuery<CurrentWorldItemHolderSelection>>? readOfficerSelection;
    private int? officerId;
    private bool flag8, abandoned, retainOnAccept;
    internal ItemEditorCustodyChoice Working => new(officerId, flag8);

    internal void Configure(GameResourceRepository repository, PreviewText text, ItemEditorCatalog catalog,
        ItemEditorNewGameDraft draft, IReadOnlyList<int> ids, Func<bool> admitted,
        Action<ItemEditorCustodyChoice?> closed, Func<int> scrollSpeed)
    {
        if (ids.Count == 0) throw new ArgumentException("Item custody requires an actual selected item.", nameof(ids));
        var originals = ids.Select(draft.Custody).ToArray();
        int? initialHolder = originals.All(value => value.OfficerId == originals[0].OfficerId) ? originals[0].OfficerId : null;
        Configure(repository, text, catalog, draft.Officers,
            new(initialHolder, originals.All(value => value.Flag8)), ids[0], admitted, closed, scrollSpeed);
    }

    internal void Configure(GameResourceRepository repository, PreviewText text, ItemEditorCatalog catalog,
        IReadOnlyList<ItemEditorCustodyOfficerChoice> officers, ItemEditorCustodyChoice initial, int initialItemId,
        Func<bool> admitted, Action<ItemEditorCustodyChoice?> closed, Func<int> scrollSpeed, bool retainOnAccept = false,
        Func<AreaAdministrationQuery<CurrentWorldItemHolderSelection>>? readOfficerSelection = null)
    {
        this.repository = repository; this.text = text; this.catalog = catalog;
        this.admitted = admitted; this.closed = closed; this.scrollSpeed = scrollSpeed;
        this.retainOnAccept = retainOnAccept;
        this.readOfficerSelection = readOfficerSelection;
        this.officers = officers;
        foreach (var person in officers) names.Add(person.Id, person.Name);
        officerId = initial.OfficerId;
        flag8 = officerId is int initialOfficer && names.ContainsKey(initialOfficer) && initial.Flag8;
        Name = "ItemEditorCustody"; ZIndex = 8; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        chrome = new(); AddChild(chrome); chrome.Configure(repository, text, 0x737, RequestBack, Accept);
        var source = new UiSourceLayout(MapMarkerLayout.LoadNamed(repository, "11_02_05_edit_item_ikkatu"));
        var panel = new Control { Name = "OriginalItemCustodyPanel", Position = new(438, 399),
            Size = source.Rectangle(0).Size, MouseFilter = MouseFilterEnum.Ignore };
        chrome.Canvas.AddChild(panel);
        foreach (int node in new[] { 2, 5, 6, 8 })
            source.Art(panel, node, source.Rectangle(node), chrome.Assets, chrome.SpriteShader);
        source.Text(panel, 7, default, catalog.Data(0x425), chrome.Font);
        source.Text(panel, 9, default, catalog.Data(0x730), chrome.Font);
        holder = Button(18, "ItemCustodyChooseOfficer", "", OpenOfficers);
        clear = Button(21, "ItemCustodyNone", catalog.Data(0x5A4), () =>
        { if (CanEdit()) { officerId = null; flag8 = false; Refresh(); } });
        disabledFlag = Radio(27, "ItemCustodyFlag8Off", 0x27F, false, UiSourceRadioSegment.Left);
        enabledFlag = Radio(30, "ItemCustodyFlag8On", 0x280, true, UiSourceRadioSegment.Right);
        SetMeta("source_layout", source.Source.Source.ResourceUri);
        SetMeta("source_initial_item", initialItemId);
        // The actual holder-name and controls are usable independently of the
        // not-yet-published portrait / Base card presentation metadata.
        Refresh();

        UiSourceCommandButton Button(int node, string name, string caption, Action action)
        {
            var box = source.Rectangle(node);
            var button = new UiSourceCommandButton { Name = name, Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            panel.AddChild(button); button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font, caption, action);
            return button;
        }
        UiSourceRadioButton Radio(int node, string name, int caption, bool value, UiSourceRadioSegment segment)
        {
            var box = source.Rectangle(node);
            var button = new UiSourceRadioButton { Name = name, Position = box.Position, Size = box.Size, InputAdmission = CanEdit };
            panel.AddChild(button); button.Configure(repository, chrome.Assets, chrome.SpriteShader, chrome.Font,
                segment, catalog.Data(caption), () => { if (CanEdit()) { flag8 = value; Refresh(); } });
            return button;
        }
    }

    private bool PageAdmitted() => !abandoned && IsVisibleInTree() && admitted?.Invoke() == true;
    private bool CanEdit() => PageAdmitted() && selector is null && currentSelector is null;
    private void Refresh()
    {
        if (abandoned) return;
        bool knownHolder = officerId is null || names.ContainsKey(officerId.Value);
        holder.Caption = officerId is null ? catalog.Data(0x53F) : names.GetValueOrDefault(officerId.Value, "");
        holder.Disabled = !CanEdit() || officers.Count == 0; clear.Disabled = !CanEdit();
        disabledFlag.Selected = !flag8; enabledFlag.Selected = flag8;
        disabledFlag.Disabled = enabledFlag.Disabled = !CanEdit();
        chrome.ApplyDecision(CanEdit() && knownHolder, true); chrome.ApplyReturn(CanEdit());
        SetMeta("holder_name_unresolved", !knownHolder);
        SetMeta("holder_portrait_unresolved", officerId is not null);
    }

    private void OpenOfficers()
    {
        if (!CanEdit()) return;
        if(readOfficerSelection is not null){OpenCurrentOfficers();return;}
        var view = new CommandSelectionView(); selector = view; AddChild(view);
        view.Configure(repository, text, 0x425, [new("name", catalog.Data(0x211), 1416)],
            officers.Select(person => new CommandSelectionRow(person.Id,
                [new CommandSelectionCell(person.Name, SortUnavailable: true)], true)).ToArray(),
            officerId is int id && names.ContainsKey(id) ? [id] : [], 1,
            () => PageAdmitted() && selector == view, result =>
            {
                if (abandoned || selector != view) return;
                selector = null; view.Abandon(); view.QueueFree();
                if (result is not null) officerId = result.Single(); Refresh();
            }, scrollSpeed, requireSelection: true, singleSelection: true);
        view.Name = "ItemCustodyOfficerSelection"; Refresh();
    }
    private void OpenCurrentOfficers()
    {
        var query=readOfficerSelection!();
        if(!query.Available||query.Value is not {} publication)
        {chrome.ApplyDefaultHint(text.Get("information_partial"));SetMeta("current_item_holder_source_missing",query.UnresolvedInput??"current-world-item.holder_selector");return;}
        var pages=publication.Pages;
        var groups=pages.Groups.Select(group=>new IntelligenceSelectionGroup(group.Index,group.Caption,
            group.Columns.Where(column=>column.Visible).Select(column=>new IntelligenceSelectionColumn(column.Id,
                column.Caption,column.Width,column.CellFormat,column.CanSort,column.InitialAscending)).ToArray())).ToArray();
        IReadOnlyList<IntelligenceSelectionRow> Read(int group)
        {
            int[] slots=pages.Groups[group].Columns.Select((column,index)=>(column,index))
                .Where(pair=>pair.column.Visible).Select(pair=>pair.index).ToArray();
            return pages.ReadPage(group).Rows.Select(row=>new IntelligenceSelectionRow(row.OfficerId,
                slots.Select((slot,index)=>
                {
                    var cell=row.Cells[slot];return new IntelligenceSelectionCell(cell.UnresolvedOwner is null?cell.Text??"":text.Get("detail_unknown"),
                        new MapInformationListCellStyle(groups[group].Columns[index].CellFormat,cell.PresetIndex,cell.MarkerColour?.CpuPacked,
                            StatePresets:cell.StatePresets,SourceFormattedText:cell.SourceFormattedText),cell.SortUnresolvedOwner);
                }).ToArray(),names.ContainsKey(row.OfficerId))).ToArray();
        }
        var table=new IntelligenceSelectionTable(publication.TitleMessageId,publication.GuideText,groups,
            pages.CurrentOfficerIds.Length,Read,(group,column,ascending)=>pages.Sort(group,column,ascending).Available);
        var view=new IntelligenceSelectionView();currentSelector=view;AddChild(view);
        try
        {
            view.Configure(repository,text,table,()=>PageAdmitted()&&currentSelector==view,result=>
            {
                if(abandoned||currentSelector!=view)return;
                currentSelector=null;view.Abandon();view.QueueFree();
                if(result is int id)officerId=id;
                Refresh();
            },scrollSpeed);
            view.Name="CurrentWorldItemHolderSelection";Refresh();
        }
        catch{currentSelector=null;view.Abandon();view.QueueFree();Refresh();throw;}
    }
    private void Accept()
    {
        if (!CanEdit() || officerId is int id && !names.ContainsKey(id)) return;
        Finish(new(officerId, flag8));
    }
    private void Finish(ItemEditorCustodyChoice? result)
    {
        if (result is not null && retainOnAccept) { Hide(); closed?.Invoke(result); return; }
        var callback = closed; Abandon(); callback?.Invoke(result);
    }
    internal void ApplyNotice(string value) { if (!abandoned) chrome.ApplyDefaultHint(value); }
    internal void RequestBack()
    {
        if (currentSelector is not null) { currentSelector.RequestCancel(); return; }
        if (selector is not null) { selector.RequestCancel(); return; }
        if (CanEdit()) Finish(null);
    }
    internal void Abandon()
    {
        abandoned = true; admitted = null; closed = null; selector?.Abandon(); selector = null;
        currentSelector?.Abandon();currentSelector=null;readOfficerSelection=null;
        holder?.Abandon(); clear?.Abandon(); disabledFlag?.Abandon(); enabledFlag?.Abandon(); chrome?.Abandon(); Hide();
    }
}
