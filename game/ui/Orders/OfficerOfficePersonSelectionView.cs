using Godot;
using San14.Data;
using System.Globalization;

/// <summary>The appointment state's one authored Person page. Rows read the
/// existing draft appointments; selecting a row is the separate assignment.</summary>
internal partial class OfficerOfficePersonSelectionView : Control
{
    private MapInformationListView? list;
    private Func<bool>? admitted;
    private Action<int?>? completed;
    private Action? dismissed;
    private bool abandoned;

    internal void Configure(GameResourceRepository repository,PreviewText text,
        OfficerOfficePeopleSelection model,string targetOfficeName,Func<bool> admitted,Action<int?> completed,
        Action dismissed,Func<int> scrollSpeed)
    {
        this.admitted=admitted;
        this.completed=completed;this.dismissed=dismissed;
        Name="OfficerOfficePeople";ZIndex=5;MouseFilter=MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var backdrop=new UiSourceCommonBlur();AddChild(backdrop);backdrop.Configure(repository);
        var pages=model.Pages;
        var group=pages.Groups.Single();
        var visible=group.Columns.Where(column=>column.Visible).ToArray();
        var slots=group.Columns.Select((column,slot)=>(column.Id,slot)).ToDictionary(item=>item.Id,item=>item.slot);
        var publication=pages.ReadPage(0);
        string guide=model.GuideText??throw new InvalidDataException("Office selection guide is absent.");
        var peopleBySlot=publication.Rows.ToDictionary(row=>row.SourceIndex,row=>row.OfficerId);
        var view=new MapInformationListView();list=view;AddChild(view);
        view.Configure(repository,text,pages.Title,
            visible.Select(column=>new MapInformationListColumn(Key(column.Id),column.Caption,column.Width,
                column.CanSort,column.InitialAscending)).ToArray(),Rows(publication),
            ReadAdmission,RequestCancel,sourceIndex=>
            {
                if(!ReadAdmission())return;
                pages.SelectSourceIndex(sourceIndex);
                if(pages.SelectedOfficerId is int id && model.Candidates.TryGetValue(id,out var candidate) && candidate.Enabled)
                    Finish(id);
            },scrollSpeed,(key,ascending)=>
            {
                int column=key=="name"?0:int.Parse(key,CultureInfo.InvariantCulture);
                var result=pages.Sort(0,column,ascending);
                if(!result.Available||result.Value is not {} sorted)
                {GD.PushWarning(result.UnresolvedInput??"officer-office.people.sort");return null;}
                publication=sorted;return Rows(publication);
            },notice:guide,
            tabCaptions:[group.Caption],completeColumnProfile:true,normalColumnLayout:false,
            retainSortDirections:true,disableUninspectableRows:true,hoverSourceIndex:ShowFocusHint,
            rowExtentWidth:1464,sourcePersonCellBounds:true);

        void ShowFocusHint(int? slot)
        {
            if(!ReadAdmission())return;
            string message=guide;
            if(slot is int index && peopleBySlot.TryGetValue(index,out int officer) &&
                model.Candidates.TryGetValue(officer,out var candidate) && !candidate.Enabled &&
                candidate.FocusHint is {Available:true,Value:{} focus})message=focus;
            view.ApplyNotice(message);
        }

        //74CEC0 receives this parent-owned offset header with the actual
        //target Post name. Its root uses the caller's (0,122) placement.
        var targetHeader=new UiSourceLayout(repository,0x23A2C14A);
        var palettes=MapSpritePalettes.Create();
        Vector2 headerMount=new(0,122);
        foreach(var node in targetHeader.Nodes.Where(node=>node.Type is 1 or 3))
            targetHeader.Art(view.SourceCanvas,node.Index,targetHeader.Rectangle(node.Index,headerMount),
                view.SourceAssets,view.SourceShader,colors:node.Index is 2 or 5?palettes[30]:null);
        foreach(var node in targetHeader.Nodes.Where(node=>node.Type==6))
            targetHeader.Blur(view.SourceCanvas,node.Index,targetHeader.Rectangle(node.Index,headerMount));
        string Caption(int id)=>repository.LoadMessages(text.SourceLocale,San14.Data.Formats.MessageKind.Data)
            .Messages.Sections[1].Records[id].Text??throw new InvalidDataException("Missing office selector caption.");
        targetHeader.Text(view.SourceCanvas,9,headerMount,Caption(0x05DD),view.SourceFont);
        targetHeader.Text(view.SourceCanvas,10,headerMount,targetOfficeName,view.SourceFont);

        var footer=new UiSourceLayout(repository,0xD8913AB2);
        var dismiss=new UiSourceBottomButton
        {
            Name="DismissOfficePerson",Position=footer.Rectangle(14,new(0,904)).Position,
            Disabled=!model.CanDismiss,InputAdmission=()=>ReadAdmission()&&model.CanDismiss
        };
        view.SourceCanvas.AddChild(dismiss);
        dismiss.Configure(repository,view.SourceAssets,view.SourceShader,view.SourceFont,Caption(0x029E),
            ()=>{if(ReadAdmission()&&model.CanDismiss)this.dismissed?.Invoke();});

        IReadOnlyList<MapInformationListRow> Rows(OfficerInformationPage page)=>page.Rows.Select(row=>
        {
            var cells=visible.Select(column=>row.Cells[slots[column.Id]]).ToArray();
            bool enabled=model.Candidates.TryGetValue(row.OfficerId,out var candidate)&&candidate.Enabled;
            return new MapInformationListRow(row.SourceIndex,
                cells.Select(cell=>cell.UnresolvedOwner is null?cell.Text??"":text.Get("detail_unknown")).ToArray(),enabled,
                cells.Select((cell,index)=>new MapInformationListCellStyle(visible[index].CellFormat,
                    cell.PresetIndex,cell.MarkerColour?.CpuPacked,StatePresets:cell.StatePresets,
                    SourceFormattedText:cell.UnresolvedOwner is null?cell.SourceFormattedText:null)).ToArray());
        }).ToArray();
    }

    private static string Key(int id)=>id==0?"name":id.ToString(CultureInfo.InvariantCulture);
    private bool ReadAdmission()=>!abandoned&&completed is not null&&IsVisibleInTree()&&CanProcess()&&(admitted?.Invoke()??false);
    internal void RequestCancel()=>Finish(null);
    private void Finish(int? officer)
    {
        if(!ReadAdmission())return;
        var callback=completed;completed=null;admitted=null;dismissed=null;
        list?.Abandon();Hide();callback?.Invoke(officer);
    }
    internal void Abandon()
    {
        if(abandoned)return;
        abandoned=true;completed=null;admitted=null;dismissed=null;
        list?.Abandon();Hide();
    }
    public override void _ExitTree()=>Abandon();
}
