using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed record DeploymentPersonListEntry(int OfficerId, bool Disabled,
    IReadOnlyList<DeploymentPersonCell> Cells);

/// <summary>Person pages share one publication order, selection and scroll.
/// Only ordinary column directions and the active header belong to each page.</summary>
internal partial class DeploymentPersonSelectionView : Control
{
    private sealed class PersonPage(IReadOnlyList<DeploymentPersonColumn> columns)
    {
        internal IReadOnlyList<DeploymentPersonColumn> Columns { get; } = columns;
        internal Control? Body;
        internal readonly List<(DeploymentPersonListEntry Entry, UiSourcePersonRow Row)> Rows = [];
        internal readonly Dictionary<int,UiSourceColumnButton> Headers = [];
        internal readonly Dictionary<int,bool> NextAscending = [];
        internal int? ActiveColumn;
        internal bool? ActiveAscending;
    }
    private readonly PersonPage[] _pages = [new(DeploymentPersonColumns.Basic),new(DeploymentPersonColumns.Formations),
        new(DeploymentPersonColumns.Linkage),new(DeploymentPersonColumns.TacticsFirst),new(DeploymentPersonColumns.TacticsSecond),
        new(DeploymentPersonColumns.Traits)];
    private readonly List<int> _orderedIds = [], _selected = [];
    private readonly List<UiSourceTabButton> _tabs = [];
    private GameResourceRepository _repository = null!;
    private PreviewText _text = null!;
    private IReadOnlyList<DeploymentPersonListEntry> _basicEntries = [];
    private UiAssets? _assets;
    private MapFont? _font;
    private Shader? _shader;
    private Control _canvas = null!;
    private Action<IReadOnlyList<int>?>? _close;
    private int _maximum, _activePage;
    private UiSourceBottomButton _selectAll = null!;
    private string _selectCaption = "", _clearCaption = "", _checkGlyph = "";
    private UiSourceScrollbar _scroll = null!;
    private bool _nextCheckAscending;
    private Func<IReadOnlyList<DeploymentPersonSortRow>,DeploymentPersonSortColumn,bool,DeploymentPersonOrderQuery>? _order;
    private Func<IReadOnlyList<int>,DeploymentPersonFormationQuery>? _formations;
    private Func<IReadOnlyList<DeploymentPersonSortRow>,int,bool,DeploymentPersonOrderQuery>? _formationOrder;
    private Func<IReadOnlyList<int>,IReadOnlyList<int>,DeploymentPersonLinkageQuery>? _linkage;
    private Func<IReadOnlyList<DeploymentPersonSortRow>,IReadOnlyList<int>,int,bool,DeploymentPersonOrderQuery>? _linkageOrder;
    private Func<IReadOnlyList<int>,int,DeploymentPersonTacticQuery>? _tactics;
    private Func<IReadOnlyList<DeploymentPersonSortRow>,int,bool,DeploymentPersonOrderQuery>? _tacticOrder;
    private Func<int,string?>? _tacticName;
    private Func<IReadOnlyList<int>,DeploymentPersonTraitQuery>? _traits;
    private Func<IReadOnlyList<DeploymentPersonSortRow>,int,bool,DeploymentPersonOrderQuery>? _traitOrder;
    private bool _linkageDirty=true;
    private Action<string>? _unavailable;
    internal IReadOnlyList<int> SelectedOfficerIds => _selected.ToArray();

    internal void Configure(GameResourceRepository repository, PreviewText text,
        IReadOnlyList<DeploymentPersonListEntry> entries, int maximum, IEnumerable<int> selected,
        int scrollSpeedSetting, Action<IReadOnlyList<int>?> close,
        Func<IReadOnlyList<DeploymentPersonSortRow>,DeploymentPersonSortColumn,bool,DeploymentPersonOrderQuery>? order = null,
        Action<string>? unavailable = null,
        Func<IReadOnlyList<int>,DeploymentPersonFormationQuery>? formations = null,
        Func<IReadOnlyList<DeploymentPersonSortRow>,int,bool,DeploymentPersonOrderQuery>? formationOrder = null,
        Func<IReadOnlyList<int>,IReadOnlyList<int>,DeploymentPersonLinkageQuery>? linkage = null,
        Func<IReadOnlyList<DeploymentPersonSortRow>,IReadOnlyList<int>,int,bool,DeploymentPersonOrderQuery>? linkageOrder = null,
        Func<IReadOnlyList<int>,int,DeploymentPersonTacticQuery>? tactics = null,
        Func<IReadOnlyList<DeploymentPersonSortRow>,int,bool,DeploymentPersonOrderQuery>? tacticOrder = null,
        Func<int,string?>? tacticName = null,
        Func<IReadOnlyList<int>,DeploymentPersonTraitQuery>? traits = null,
        Func<IReadOnlyList<DeploymentPersonSortRow>,int,bool,DeploymentPersonOrderQuery>? traitOrder = null,
        Func<int>? currentScrollSpeed = null)
    {
        if (maximum is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(maximum));
        _repository=repository; _text=text; _basicEntries=entries;
        _maximum=maximum; _close=close; _order=order; _unavailable=unavailable;
        _formations=formations; _formationOrder=formationOrder;
        _linkage=linkage; _linkageOrder=linkageOrder;
        _tactics=tactics; _tacticOrder=tacticOrder; _tacticName=tacticName;
        _traits=traits; _traitOrder=traitOrder;
        _orderedIds.AddRange(entries.Select(entry=>entry.OfficerId));
        _selected.AddRange(selected);
        if (_selected.Count > maximum || _selected.Any(id => !_orderedIds.Contains(id)))
            throw new InvalidDataException("Initial person selection does not fit its current publication.");
        Name="DeploymentPersonSelection"; ZIndex=4; MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _assets=new(repository); _font=new(repository,text.SourceLocale); _shader=UiSourceSprite.CreateShader();
        var presets=MapTextPresets.Create();
        _selectCaption=Message(0x003A); _clearCaption=Message(0x003B); _checkGlyph=Message(0x0628);
        _canvas=new Control {Name="SourceCanvas",Size=new(1920,1080),MouseFilter=MouseFilterEnum.Stop};
        AddChild(_canvas);
        _canvas.AddChild(new BackBufferCopy {CopyMode=BackBufferCopy.CopyModeEnum.Viewport});
        _canvas.AddChild(new MapDialogBackdrop {Size=_canvas.Size});
        var title=new UiSourceLayout(repository,0xDC8AC8AA);
        title.Art(_canvas,1,title.Rectangle(1),_assets,_shader);
        title.Text(_canvas,2,default,Message(0x032E),_font,presets[56]);
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        shell.Art(_canvas,3,shell.Rectangle(3,new(191,147)),_assets,_shader);
        int[] tabs=[0x03CD,0x022B,0x0235,0x03DD,0x03DE,0x023D];
        float tabX=230;
        for(int i=0;i<tabs.Length;i++)
        {
            int pageIndex=i;
            string caption=Message(tabs[i]); float width=caption.Length*28+36;
            var tab=new UiSourceTabButton {Name="PersonTab"+i,Position=new(tabX,147),Size=new(width,64),
                Selected=i==0,Disabled=i switch {1=>formations is null,2=>linkage is null,
                    3 or 4=>tactics is null,5=>traits is null,_=>false}};
            _canvas.AddChild(tab); _tabs.Add(tab);
            tab.Configure(repository,_assets,_shader,_font,caption,i==0?0x172EDC24u:i==tabs.Length-1?0xD45873AAu:0x9AFB27B7u);
            tab.Pressed+=()=>SwitchPage(pageIndex);
            tabX+=width-6;
        }
        _scroll=new UiSourceScrollbar {Name="PersonScrollbar",Position=new(1687,265),Size=new(16,597)};
        _canvas.AddChild(_scroll);
        _scroll.Configure(repository,_assets,_shader,Math.Max(0,entries.Count*48-596),scrollSpeedSetting,_=>PositionRows(),currentScrollSpeed:currentScrollSpeed);
        for(int i=0;i<_pages.Length;i++)
            foreach(var column in _pages[i].Columns)
                _pages[i].NextAscending[column.Id]=column.InitialAscending;
        BuildPage(0,entries);
        var accept=new UiSourceRoundButton {Name="AcceptPersonSelection",Position=new(827,955)};
        var cancel=new UiSourceRoundButton {Name="CancelPersonSelection",Position=new(965,955)};
        _canvas.AddChild(accept); _canvas.AddChild(cancel);
        accept.Configure(repository,_assets,_shader,text.SourceLocale,"000_cmn_txt_decision_kettei",()=>Close(_selected.ToArray()));
        cancel.Configure(repository,_assets,_shader,text.SourceLocale,"000_cmn_txt_decision_modoru",RequestCancel);
        _selectAll=new UiSourceBottomButton {Name="SelectAllPersons",Position=new(189,948)};
        _canvas.AddChild(_selectAll);
        _selectAll.Configure(repository,_assets,_shader,_font,_selectCaption,SelectOrClearAll);
        // Ordinary creation consumes one shared selected-first sort.
        Sort(-1);
        RefreshSelection(); Resized+=Fit; Fit();
    }

    private string Message(int id) => _repository.LoadMessages(_text.SourceLocale,MessageKind.Data).Messages.Sections[1].Records[id].Text
        ?? throw new InvalidDataException("Missing person-list message.");

    private void BuildPage(int index,IReadOnlyList<DeploymentPersonListEntry> entries,IReadOnlyList<string>? captions=null)
    {
        var page=_pages[index];
        var body=new Control {Name="PersonPage"+index,Size=_canvas.Size,MouseFilter=MouseFilterEnum.Ignore};
        _canvas.AddChild(body); page.Body=body;
        int columnX=213;
        Header(-1,_checkGlyph,48);
        for(int i=0;i<page.Columns.Count;i++)
        {
            var column=page.Columns[i];
            Header(column.Id,captions is not null && i>0 ? captions[i-1] : Message(column.CaptionMessage),column.Width);
        }
        var clip=new Control {Name="PersonListViewport",Position=new(213,264),Size=new(1464,596),
            ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        body.AddChild(clip); clip.GuiInput+=_scroll.HandleWheel;
        var byId=entries.ToDictionary(entry=>entry.OfficerId);
        foreach(int id in _orderedIds)
        {
            var entry=byId[id];
            var row=new UiSourcePersonRow {Name="PersonRow"+page.Rows.Count};
            clip.AddChild(row);
            row.Configure(_repository,_assets!,_shader!,_font!,_checkGlyph,page.Columns,entry.Cells);
            page.Rows.Add((entry,row)); row.GuiInput+=_scroll.HandleWheel;
            row.Pressed+=()=>
            {
                if(_close is null || index!=_activePage || row.Disabled)return;
                if(!_selected.Remove(id) && _selected.Count<_maximum)_selected.Add(id);
                RefreshSelection(selectedChanged:true);
            };
        }
        PositionRows(); ApplyArrows(page);

        void Header(int id,string caption,int width)
        {
            bool queryMissing=id!=-1 && (index==0 || id==0 ? _order is null : index switch
                {1=>_formationOrder is null,2=>_linkageOrder is null,3 or 4=>_tacticOrder is null,_=>_traitOrder is null});
            var header=new UiSourceColumnButton {Name="PersonHeader"+id,Position=new(columnX,214),
                Size=new(width,48),Disabled=queryMissing};
            body.AddChild(header); header.Configure(_repository,_assets!,_shader!,_font!,caption,personList:true);
            page.Headers.Add(id,header); header.Pressed+=()=>{if(index==_activePage)Sort(id);};
            columnX+=width;
        }
    }

    private void SwitchPage(int index)
    {
        if(_close is null || index==_activePage || index<0 || index>=_pages.Length)return;
        var page=_pages[index];
        if(index==2 && (page.Body is null || _linkageDirty) && !RefreshLinkagePage())return;
        if(page.Body is null)
        {
            if(index==1)
            {
                if(_formations is null)return;
                var query=_formations(_orderedIds.ToArray());
                if(!query.Available){_unavailable?.Invoke(query.UnresolvedInput!);return;}
                BuildPage(index,DeploymentPersonListContent.CreateFormations(_repository,_text,_basicEntries,query));
            }
            else if(index is 3 or 4)
            {
                if(_tactics is null)return;
                var query=_tactics(_orderedIds.ToArray(),index==3 ? 0 : 5);
                if(!query.Available){_unavailable?.Invoke(query.UnresolvedInput!);return;}
                BuildPage(index,DeploymentPersonListContent.CreateTactics(_text,_basicEntries,query,_tacticName));
            }
            else
            {
                if(_traits is null)return;
                var query=_traits(_orderedIds.ToArray());
                if(!query.Available){_unavailable?.Invoke(query.UnresolvedInput!);return;}
                BuildPage(index,DeploymentPersonListContent.CreateTraits(_text,_basicEntries,query));
            }
        }
        _pages[_activePage].Body!.Hide();
        _activePage=index; page.Body!.Show();
        // Each body has the same rows and range; sharing the actual scrollbar
        // transfers the leaving page's offset without a reset or another sort.
        foreach(var row in page.Rows)row.Row.ReleaseFocus();
        for(int i=0;i<_tabs.Count;i++)_tabs[i].Selected=i==index;
        RefreshSelection(); ApplyArrows(page);
    }

    private bool RefreshLinkagePage()
    {
        if(_linkage is null)return false;
        var query=_linkage(_orderedIds.ToArray(),_selected.ToArray());
        if(!query.Available){_unavailable?.Invoke(query.UnresolvedInput!);return false;}
        var content=DeploymentPersonListContent.CreateLinkage(_repository,_text,_basicEntries,query);
        var page=_pages[2];
        if(page.Body is null)BuildPage(2,content.Entries,content.Captions);
        else
        {
            var byId=content.Entries.ToDictionary(entry=>entry.OfficerId);
            for(int i=0;i<page.Rows.Count;i++)
            {
                var (previous,row)=page.Rows[i];
                var entry=byId[previous.OfficerId];
                row.ApplyCells(entry.Cells);page.Rows[i]=(entry,row);
            }
            for(int i=0;i<content.Captions.Count;i++)page.Headers[212+i].ApplyCaption(content.Captions[i]);
        }
        _linkageDirty=false;RefreshSelection();return true;
    }

    public override void _Process(double delta)
    {
        if(_close is not null && _activePage==2 && _linkageDirty && !RefreshLinkagePage())SwitchPage(0);
    }

    internal void RequestCancel()=>Close(null);
    internal void Abandon(){_close=null;Hide();}
    public override void _Input(InputEvent input)
    {
        if(IsVisibleInTree()&&!IsQueuedForDeletion()&&input is InputEventMouseButton {ButtonIndex:MouseButton.Right,Pressed:true})
        {GetViewport().SetInputAsHandled();RequestCancel();}
    }
    private void Close(IReadOnlyList<int>? result){var callback=_close;_close=null;callback?.Invoke(result);}
    private void RefreshSelection(bool selectedChanged=false)
    {
        if(selectedChanged)_linkageDirty=true;
        foreach(var page in _pages)
            foreach(var (entry,row) in page.Rows)
            {
                row.Selected=_selected.Contains(entry.OfficerId);
                row.Disabled=entry.Disabled || !row.Selected && _selected.Count>=_maximum;
                row.QueueRedraw();
            }
        int enabledCount=_basicEntries.Count(entry=>!entry.Disabled);
        _selectAll.Disabled=enabledCount==0;
        _selectAll.ApplyCaption(enabledCount==0 || _selected.Count<_maximum && _selected.Count<enabledCount ? _selectCaption : _clearCaption);
    }
    private void SelectOrClearAll()
    {
        if(_close is null || _selectAll.Disabled)return;
        int enabledCount=_basicEntries.Count(entry=>!entry.Disabled);
        if(_selected.Count>=_maximum || _selected.Count>=enabledCount)_selected.Clear();
        else
        {
            var byId=_basicEntries.ToDictionary(entry=>entry.OfficerId);
            foreach(int id in _orderedIds)
            {
                if(_selected.Count>=_maximum)break;
                if(!byId[id].Disabled && !_selected.Contains(id))_selected.Add(id);
            }
        }
        RefreshSelection(selectedChanged:true);
    }
    private void Sort(int columnId)
    {
        if(_close is null)return;
        if(_activePage==2 && _linkageDirty && !RefreshLinkagePage()){SwitchPage(0);return;}
        var page=_pages[_activePage];
        bool ascending=columnId==-1 ? _nextCheckAscending : page.NextAscending[columnId];
        var current=_orderedIds.Select(id=>new DeploymentPersonSortRow(id,_selected.Contains(id))).ToArray();
        DeploymentPersonOrderQuery result;
        if(columnId==-1)
            result=_order is not null ? _order(current,DeploymentPersonSortColumn.Selected,ascending) :
                new(current.OrderBy(row=>ascending ? row.Selected : !row.Selected).Select(row=>row.OfficerId).ToArray(),null);
        else if(_activePage==0 || columnId==0)
        {
            int ordinal=page.Columns.ToList().FindIndex(column=>column.Id==columnId);
            result=_order?.Invoke(current,(DeploymentPersonSortColumn)ordinal,ascending) ?? new(null,"deployment.person-order.unavailable");
        }
        else if(_activePage==1)result=_formationOrder?.Invoke(current,DeploymentPersonColumns.FormationId(columnId),ascending)
            ?? new(null,"deployment.person-formations.unavailable");
        else if(_activePage==2)result=_linkageOrder?.Invoke(current,_selected.ToArray(),columnId-212,ascending)
            ?? new(null,"deployment.person-linkage.unavailable");
        else if(_activePage is 3 or 4)result=_tacticOrder?.Invoke(current,columnId-51,ascending)
            ?? new(null,"deployment.person-tactics.unavailable");
        else result=_traitOrder?.Invoke(current,columnId-61,ascending)
            ?? new(null,"deployment.person-traits.unavailable");
        if(!result.Available){_unavailable?.Invoke(result.UnresolvedInput!);return;}
        _orderedIds.Clear();_orderedIds.AddRange(result.OfficerIds!);
        foreach(var body in _pages.Where(item=>item.Body is not null))
        {
            var byId=body.Rows.ToDictionary(item=>item.Entry.OfficerId);
            var reordered=_orderedIds.Select(id=>byId[id]).ToArray();
            body.Rows.Clear();body.Rows.AddRange(reordered);
        }
        PositionRows();
        if(columnId==-1)_nextCheckAscending=!ascending;
        else page.NextAscending[columnId]=!ascending;
        foreach(var body in _pages)
        {
            body.ActiveColumn=columnId==-1 || body==page ? columnId : null;
            body.ActiveAscending=body.ActiveColumn is null ? null : ascending;
            ApplyArrows(body);
        }
    }
    private static void ApplyArrows(PersonPage page)
    {
        foreach(var (id,header) in page.Headers)header.ApplySortDirection(page.ActiveColumn==id ? page.ActiveAscending : null);
    }
    private void PositionRows()
    {
        foreach(var page in _pages)
            for(int i=0;i<page.Rows.Count;i++)page.Rows[i].Row.Position=new(0,i*48-_scroll.Value);
    }
    private void Fit()
    {
        float factor=Math.Min(Size.X/1920,Size.Y/1080);
        _canvas.Scale=Vector2.One*factor;
        _canvas.Position=(Size-new Vector2(1920,1080)*factor)/2;
    }
    public override void _ExitTree(){_font?.Dispose();_assets?.Dispose();_shader?.Dispose();}
}
