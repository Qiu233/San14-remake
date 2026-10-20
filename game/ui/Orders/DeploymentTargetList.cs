using Godot;
using San14.Data;

internal sealed record DeploymentTargetGroup(int LocationId,string Name,IReadOnlyList<DeploymentTargetRowContent> Rows);

/// <summary>The source target selector. Selection spans every group, while
/// group navigation only changes the visible list and its scroll position.</summary>
internal partial class DeploymentTargetList : Control
{
    private GameResourceRepository repository=null!;
    private UiAssets assets=null!;
    private Shader shader=null!;
    private MapFont font=null!;
    private PreviewText text=null!;
    private IReadOnlyList<DeploymentTargetGroup> groups=[];
    private readonly HashSet<int> selected=[];
    private readonly Dictionary<int,int> scrollPositions=[];
    private readonly List<DeploymentTargetRow> visibleRows=[];
    private UiSourceText groupName=null!;
    private UiSourceButton left=null!,right=null!;
    private Control? list;
    private UiSourceScrollbar? scroll;
    private Action<IReadOnlyList<int>> changed=null!;
    private int groupIndex,scrollSpeed;
    internal int GroupIndex=>groupIndex;
    internal IReadOnlyList<int> SelectedOfficers=>groups.SelectMany(group=>group.Rows)
        .Where(row=>selected.Contains(row.OfficerId)).Select(row=>row.OfficerId).ToArray();

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        PreviewText text,IReadOnlyList<DeploymentTargetGroup> groups,IEnumerable<int> selectedOfficers,
        int scrollSpeedSetting,Action<IReadOnlyList<int>> changed)
    {
        this.repository=repository;this.assets=assets;this.shader=shader;this.font=font;this.text=text;
        this.groups=groups;this.changed=changed;scrollSpeed=scrollSpeedSetting;
        var officers=groups.SelectMany(group=>group.Rows).Select(row=>row.OfficerId).ToArray();
        if(officers.Distinct().Count()!=officers.Length||groups.Select(group=>group.LocationId).Distinct().Count()!=groups.Count)
            throw new ArgumentException("Target groups must own distinct locations and officers.",nameof(groups));
        selected.UnionWith(selectedOfficers);
        if(selected.Any(officer=>!officers.Contains(officer)))
            throw new ArgumentException("Target selection must refer to this draft.",nameof(selectedOfficers));
        var source=new UiSourceLayout(repository,0x38AD0211);
        Position=new(1449,304);Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Pass;
        source.Art(this,2,source.Rectangle(2),assets,shader);
        source.Art(this,3,source.Rectangle(3),assets,shader);
        groupName=source.Text(this,4,default,"",font);groupName.Name="TargetGroupName";
        left=Arrow(false,source.Rectangle(5),()=>ShowGroup(groupIndex-1));
        right=Arrow(true,source.Rectangle(7),()=>ShowGroup(groupIndex+1));
        ShowGroup(0);
        SetMeta("source_layout",source.Source.Source.ResourceUri);
    }

    internal void ApplySelection(IEnumerable<int> officers)
    {
        var values=officers.ToHashSet();
        if(values.Except(groups.SelectMany(group=>group.Rows).Select(row=>row.OfficerId)).Any())
            throw new ArgumentException("Target selection must refer to this draft.",nameof(officers));
        selected.Clear();selected.UnionWith(values);
        foreach(var row in visibleRows)row.ApplySelected(selected.Contains(row.GetMeta("officer_id").AsInt32()));
    }

    internal void ApplyProgress(IReadOnlyList<DeploymentTargetProgress> progress)
    {
        var updates=progress.ToDictionary(value=>value.OfficerId);
        var officers=groups.SelectMany(group=>group.Rows).Select(row=>row.OfficerId).ToHashSet();
        if(updates.Keys.Any(officer=>!officers.Contains(officer)))
            throw new ArgumentException("Target progress must refer to this draft.",nameof(progress));
        groups=groups.Select(group=>group with{Rows=group.Rows.Select(row=>
            updates.TryGetValue(row.OfficerId,out var update)
                ?row with{EstimatedDays=update.EstimatedDays,HasResolvedTarget=update.HasResolvedTarget}:row).ToArray()}).ToArray();
        foreach(var row in visibleRows)
            if(updates.TryGetValue(row.GetMeta("officer_id").AsInt32(),out var update))row.ApplyProgress(update);
    }

    private void ShowGroup(int index)
    {
        if(scroll is not null)scrollPositions[groupIndex]=scroll.Value;
        if(list is not null){list.Hide();list.Name="PreviousTargetList";list.QueueFree();}
        if(scroll is not null){scroll.Hide();scroll.Name="PreviousTargetScrollbar";scroll.QueueFree();}
        visibleRows.Clear();groupIndex=groups.Count==0?0:Math.Clamp(index,0,groups.Count-1);
        groupName.Text=groups.Count==0?"":groups[groupIndex].Name;
        left.Visible=groupIndex>0;right.Visible=groupIndex+1<groups.Count;
        var source=new UiSourceLayout(repository,0x38AD0211);
        Rect2 body=source.Rectangle(9),bar=source.Rectangle(34);
        list=new Control {Name="TargetListViewport",Position=body.Position,Size=body.Size,
            ClipContents=true,MouseFilter=MouseFilterEnum.Pass};AddChild(list);
        scroll=new UiSourceScrollbar {Name="TargetScrollbar",Position=bar.Position,Size=bar.Size};AddChild(scroll);
        var contents=groups.Count==0?Array.Empty<DeploymentTargetRowContent>():groups[groupIndex].Rows;
        scroll.Configure(repository,assets,shader,Math.Max(0,checked(contents.Count*86)-496),scrollSpeed,value=>
        {for(int i=0;i<visibleRows.Count;i++)visibleRows[i].Position=new(0,i*86-value);});
        list.GuiInput+=scroll.HandleWheel;
        foreach(var content in contents)
        {
            int officer=content.OfficerId;
            var row=new DeploymentTargetRow {Name="TargetRow"+officer,Position=new(0,visibleRows.Count*86)};
            list.AddChild(row);visibleRows.Add(row);
            row.Configure(repository,assets,shader,font,text,content,selected.Contains(officer),()=>
            {
                if(!selected.Remove(officer))selected.Add(officer);
                row.ApplySelected(selected.Contains(officer));changed(SelectedOfficers);
            });
            row.GetNode<UiSourceButton>("TargetCard").GuiInput+=scroll.HandleWheel;
            row.GuiInput+=scroll.HandleWheel;
        }
        scroll.SetValue(scrollPositions.GetValueOrDefault(groupIndex));
    }

    private UiSourceButton Arrow(bool isRight,Rect2 box,Action released)
    {
        var source=new UiSourceLayout(repository,isRight?0x3E2AD14Fu:0xE6446CC9u);
        var button=new UiSourceGroupArrow {Name=isRight?"NextTargetGroup":"PreviousTargetGroup",
            Position=box.Position,Size=box.Size,ActionMode=BaseButton.ActionModeEnum.Release};AddChild(button);
        var states=new Control[5];int[] nodes=[3,5,8,11,13];
        for(int state=0;state<states.Length;state++)
            states[state]=source.Art(button,nodes[state],source.Rectangle(nodes[state]),assets,shader);
        button.ConfigureStates(states);button.Pressed+=()=>{if(!button.Disabled)released();};
        button.SetMeta("source_layout",source.Source.Source.ResourceUri);
        return button;
    }
}

internal partial class UiSourceGroupArrow : UiSourceButton
{
    public override bool _HasPoint(Vector2 point)=>new Rect2(8,4,32,24).HasPoint(point);
}
