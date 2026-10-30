using Godot;
using San14.Data;

internal partial class CampaignEventCommandView
{
    private byte filterMask=CampaignEventCommandPresentation.FreshFilterMask;
    private readonly List<CampaignEventFilterCheck> filters=[];
    private IReadOnlyList<CampaignEventCommandRow> ReadFilteredRows()=>catalog.ReadRows(category,filterMask,sortColumn,sortAscending);
    private void BuildFilter()
    {
        var layout=new UiSourceLayout(MapMarkerLayout.LoadNamed(repository,"09_01_01_event_list_offset_01"));
        var mount=source.Rectangle(113);
        var host=new Control{Name="CampaignEventFilters",Position=mount.Position,Size=layout.Rectangle(0).Size,MouseFilter=MouseFilterEnum.Pass};canvas.AddChild(host);
        // These are the eight original in-place checkbox mounts. There is no
        // substitute availability tab and no second eligibility calculation.
        int[] nodes=[2,5,8,11,14,17,20,23];
        for(int i=0;i<nodes.Length;i++)
        {
            int index=i,node=nodes[i];var box=layout.Rectangle(node);
            var check=new CampaignEventFilterCheck{Name="CampaignEventFilter"+index,Position=box.Position,Size=box.Size,InputAdmission=CanUse};host.AddChild(check);filters.Add(check);
            check.Configure(layout,node,assets,shader,font,Caption(0x4A3+i),()=>Run(()=>
            {
                if(!CanUse())return;
                filterMask^=(byte)(1<<index);RefreshFilterInput();RebuildRows(keepSelection:true);SetMeta("filter_mask",filterMask);
            }));
        }
        SetMeta("filter_mask",filterMask);SetMeta("filter_initial_state_owner","fresh_campaign_event_command");
    }
    private void RefreshFilterInput()
    {
        for(int i=0;i<filters.Count;i++){filters[i].Disabled=busy||abandoned;filters[i].Checked=(filterMask&(1<<i))!=0;}
    }
}

/// <summary>The event filter uses authored in-place tick and caption nodes,
/// rather than the unrelated common function-icon check button.</summary>
internal partial class CampaignEventFilterCheck:UiSourceHitButton
{
    internal Func<bool>? InputAdmission{get;set;}
    private Control tick=null!;
    private Action? toggle;
    internal bool Checked{get=>tick?.Visible==true;set{if(tick is not null)tick.Visible=value;}}
    public override bool _HasPoint(Vector2 point)=>InputAdmission?.Invoke()==true&&base._HasPoint(point);
    internal void Configure(UiSourceLayout source,int node,UiAssets assets,Shader shader,MapFont font,string caption,Action toggle)
    {
        this.toggle=toggle;ActionMode=ActionModeEnum.Release;FocusMode=FocusModeEnum.None;IgnoreTextureSize=true;
        Vector2 origin=source.Rectangle(node).Position;
        var art=source.Rectangle(node+1);art.Position-=origin;
        tick=source.Art(this,node+1,art,assets,shader);
        var label=source.Rectangle(node+2);label.Position-=origin;
        source.Text(this,node+2,default,caption,font,rectangle:label);
        Checked=false;SetMeta("source_layout",source.Source.Source.ResourceUri);SetMeta("source_node_index",node);
        Pressed+=()=>{if(!Disabled&&InputAdmission?.Invoke()==true)this.toggle?.Invoke();};
    }
    internal void Abandon(){toggle=null;InputAdmission=null;Disabled=true;}
}
