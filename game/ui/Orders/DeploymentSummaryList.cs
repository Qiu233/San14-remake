using Godot;
using San14.Data;

/// <summary>Source summary rows followed by exactly one add sentinel. The list
/// clips all row children, including the portrait extending left of its row.</summary>
internal partial class DeploymentSummaryList : Control
{
    private readonly List<DeploymentSummaryRow> _rows=[];
    internal int? SelectedOfficerId {get;private set;}
    internal int ScrollValue=>GetNode<UiSourceScrollbar>("UnitScrollbar").Value;
    internal void SetScrollValue(int value)=>GetNode<UiSourceScrollbar>("UnitScrollbar").SetValue(value);
    internal void ApplyTroops(int officerId,int troops,DeploymentDraftFigures? figures,IReadOnlyList<string>? tacticNames=null)=>
        _rows.Single(row=>row.GetMeta("officer_id").AsInt32()==officerId).ApplyTroops(troops,figures,tacticNames);

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        IReadOnlyList<DeploymentSummaryRowContent> contents,int? selectedOfficerId,int scrollSpeedSetting,
        Action<int> selected,Action<int> removed,Action add,Func<int>? currentScrollSpeed=null)
    {
        if(contents.Select(row=>row.OfficerId).Distinct().Count()!=contents.Count ||
            selectedOfficerId is int current&&!contents.Any(row=>row.OfficerId==current))
            throw new ArgumentException("Summary rows and current selection must identify the same draft.",nameof(contents));
        var page=new UiSourceLayout(repository,0xFC796D9B);
        Rect2 viewport=page.Rectangle(17);
        Position=viewport.Position;Size=viewport.Size;MouseFilter=MouseFilterEnum.Ignore;
        SelectedOfficerId=selectedOfficerId;
        var clip=new Control {Name="SummaryViewport",Size=Size,ClipContents=true,MouseFilter=MouseFilterEnum.Pass};
        AddChild(clip);
        var scrollBox=page.Rectangle(30);
        var scroll=new UiSourceScrollbar {Name="UnitScrollbar",Position=scrollBox.Position-Position,Size=scrollBox.Size};
        AddChild(scroll);
        var controls=new List<Control>();
        scroll.Configure(repository,assets,shader,Math.Max(0,checked((contents.Count+1)*116)-736),scrollSpeedSetting,value=>
        {
            for(int i=0;i<controls.Count;i++)controls[i].Position=new(0,i*116-value);
        },currentScrollSpeed:currentScrollSpeed);
        clip.GuiInput+=scroll.HandleWheel;
        foreach(var content in contents)
        {
            int officer=content.OfficerId;
            var row=new DeploymentSummaryRow {Name="Summary"+officer,Position=new(0,controls.Count*116),Selected=officer==selectedOfficerId};
            clip.AddChild(row);controls.Add(row);_rows.Add(row);
            row.Configure(repository,assets,shader,font,content,()=>
            {
                SelectedOfficerId=officer;
                foreach(var other in _rows)other.Selected=other==row;
                selected(officer);
            },()=>removed(officer));
            row.GuiInput+=scroll.HandleWheel;
        }
        var sentinel=new DeploymentAddRow {Name="AddDeploymentUnit",Position=new(0,controls.Count*116)};
        clip.AddChild(sentinel);controls.Add(sentinel);
        sentinel.Configure(repository,assets,shader,add);
        sentinel.GuiInput+=scroll.HandleWheel;
        SetMeta("source_list_node",17);
        SetMeta("summary_count",contents.Count);
    }
}
