using Godot;
using San14.Data;

/// <summary>A bounded visible-row pool over the caller's prepared candidates.
/// Input changes the selected detail; it never captures or scans a world.</summary>
internal partial class UiSourceDetailSelector : Control
{
    private sealed class Row(UiSourceListRow button) { internal UiSourceListRow Button=button; internal int Slot=-1; }
    private IReadOnlyList<InformationDetailSelectionItem> items=[];
    private bool nextAscending=true;
    private bool? currentSortAscending;
    private readonly List<Row> pool=[];
    private Control rows=null!;
    private UiSourceScrollbar scrollbar=null!;
    private Func<bool>? admitted;
    private Action<int>? selected;
    private ArmyMapUiSources icons=null!;
    private int selection=-1;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository, UiAssets assets, Shader shader, MapFont font,
        PreviewText text, InformationDetailSelectionContext context, InformationListKind kind, int initialId,
        Func<bool> admitted, Action<int>? selected, int? sourceColumnWidth = null, int? sourceCaptionId = null)
    {
        Name="DetailSelection";MouseFilter=MouseFilterEnum.Pass;Size=new(1920,1080);
        currentSortAscending=context.InitialNameSortAscending;
        nextAscending=currentSortAscending is bool direction?!direction:true;
        this.items=context.Items;this.admitted=admitted;this.selected=selected;icons=ArmyMapUiSources.Create();
        var (width,caption,leading)=kind switch
        {
            InformationListKind.Cities=>(76,0x207,false),
            InformationListKind.Forces=>(160,0x1FD,false),
            InformationListKind.Areas=>(132,0x202,true),
            InformationListKind.Districts=>(216,0,false),
            InformationListKind.Officers=>(132,0x211,false),
            InformationListKind.Items=>(ItemInformationSources.SelectionWidth,ItemInformationSources.SelectionCaptionId,false),
            _=>throw new ArgumentOutOfRangeException(nameof(kind))
        };
        width=sourceColumnWidth??width;caption=sourceCaptionId??caption;
        var shell=new UiSourceLayout(repository,0xA3B4A82E);
        var heading=shell.Rectangle(14,new(191,147));
        int inset=leading?48:0,total=width+inset;
        var column=new UiSourceColumnButton {Name="SelectionHeader",Position=heading.Position+new Vector2(inset,0),Size=new(width,48),
            InputAdmission=ReadAdmission,Disabled=items.Any(item=>item.SortUnresolvedOwner is not null)};
        AddChild(column);column.Configure(repository,assets,shader,font,caption==0?"":repository.LoadMessages(text.SourceLocale,
            San14.Data.Formats.MessageKind.Data).Messages.Sections[1].Records[caption].Text??"",sortArrows:true);
        column.ApplySortDirection(currentSortAscending);
        column.SetMeta("unresolved_owner",items.FirstOrDefault(item=>item.SortUnresolvedOwner is not null)?.SortUnresolvedOwner??"");
        column.Pressed+=()=>
        {
            if(!ReadAdmission() || column.Disabled)return;
            Sort(kind,nextAscending);column.ApplySortDirection(nextAscending);
            currentSortAscending=nextAscending;SetMeta("sort_ascending",nextAscending);nextAscending=!nextAscending;
        };
        if(leading)
        {
            var iconHeader=new UiSourceColumnButton {Name="SelectionKindHeader",Position=heading.Position,Size=new(48,48)};
            AddChild(iconHeader);iconHeader.Configure(repository,assets,shader,font,"",icon:"000_cmn_icon_kyoten");
        }
        var box=shell.Rectangle(17,new(191,147));
        rows=new() {Name="DetailSelectionRows",Position=box.Position,Size=new(total,box.Size.Y),ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        AddChild(rows);
        var bar=shell.Rectangle(32,new(191,147));
        scrollbar=new() {Name="DetailSelectionScrollbar",Position=new(191+total+26,bar.Position.Y),Size=bar.Size};
        AddChild(scrollbar);scrollbar.Configure(repository,assets,shader,Math.Max(0,items.Count*48-(int)rows.Size.Y),
            context.ScrollSpeed(),_=>PositionRows(),currentScrollSpeed:context.ScrollSpeed);
        rows.GuiInput+=input=> {if(ReadAdmission())scrollbar.HandleWheel(input);};
        for(int slot=0;slot<Math.Min(14,items.Count);slot++)
        {
            var button=new UiSourceListRow {Name="DetailSelectionRow"+slot,InputAdmission=ReadAdmission,ActionMode=BaseButton.ActionModeEnum.Release};
            rows.AddChild(button);button.Configure(repository,assets,shader,font,"",width,leading,inlineIcon:kind==InformationListKind.Districts);
            var row=new Row(button);pool.Add(row);
            button.Pressed+=()=>
            {
                if(!ReadAdmission() || row.Slot<0)return;
                selection=row.Slot;PositionRows();this.selected?.Invoke(items[selection].Id);
            };
            button.GuiInput+=input=>{if(ReadAdmission())scrollbar.HandleWheel(input);};
        }
        unknownCaption=text.Get("detail_unknown");
        Select(initialId);PositionRows();
        SetMeta("candidate_count",items.Count);SetMeta("source_column_width",width);
    }
    private void Sort(InformationListKind kind,bool ascending)
    {
        // Retain the selected slot, including duplicate identities. Sorting
        // operates only on these prepared values, never on the live World.
        int selectedSlot=selection;
        var ordered=items.Select((item,slot)=>(Item:item,Slot:slot));
        var comparison=Comparer<InformationDetailSelectionItem>.Create((a,b)=>ascending
            ?InformationDetailSelectionSorting.Compare(kind,a,b)
            :InformationDetailSelectionSorting.Compare(kind,b,a));
        var sorted=ordered.OrderBy(entry=>entry.Item,comparison).ToArray();
        items=sorted.Select(entry=>entry.Item).ToArray();
        selection=Array.FindIndex(sorted,entry=>entry.Slot==selectedSlot);
        foreach(var row in pool)row.Slot=-1;
        if(selection>=0)Select(items[selection].Id);
        else PositionRows();
    }
    internal InformationDetailSelectionReturn ReadReturn(int currentId) =>
        new(items.Select(item=>item.Id).ToArray(),currentId,currentSortAscending);
    internal OfficerDetailSelectionReturn ReadOfficerReturn(int currentId) =>
        new(items.Select(item=>item.Id).ToArray(),currentId,currentSortAscending);
    private string unknownCaption="";
    internal void Select(int id)
    {
        if(selection<0 || items[selection].Id!=id)
        {
            selection=-1;
            for(int index=0;index<items.Count;index++)if(items[index].Id==id){selection=index;break;}
        }
        if(selection>=0)
        {
            int top=selection*48;
            if(top<scrollbar.Value)scrollbar.SetValue(top);
            else if(top+48>scrollbar.Value+rows.Size.Y)scrollbar.SetValue(top+48-(int)rows.Size.Y);
        }
        PositionRows();SetMeta("selected_identity",id);
    }
    private bool ReadAdmission()=>!abandoned && IsVisibleInTree() && CanProcess() && (admitted?.Invoke()??false);
    private void PositionRows()
    {
        int first=scrollbar.Value/48;
        for(int slot=0;slot<pool.Count;slot++)
        {
            var row=pool[slot];int index=first+slot;
            if(index>=items.Count){row.Slot=-1;row.Button.Hide();continue;}
            bool changed=row.Slot!=index;row.Slot=index;var item=items[index];
            row.Button.Position=new(0,index*48-scrollbar.Value);row.Button.Selected=index==selection;
            if(changed)
            {
                row.Button.ApplyContents(item.Caption??unknownCaption,
                    item.IconSpriteId is int icon?icons.IconName(new ArmyMapIconSelection(icon,0)):null);
                row.Button.SetMeta("detail_identity",item.Id);row.Button.SetMeta("unresolved_owner",item.UnresolvedOwner??"");
            }
            row.Button.Show();
        }
    }
    public override void _ExitTree()
    {
        abandoned=true;admitted=null;selected=null;items=[];base._ExitTree();
    }
}
