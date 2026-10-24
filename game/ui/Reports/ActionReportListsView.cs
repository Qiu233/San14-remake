using Godot;
using San14.Core;
using San14.Data;
using San14.Data.Formats;

/// <summary>The action report's seven category tabs and persistent list pairs.
/// Its page supplies actual prepared records, strategist updates and navigation.</summary>
internal partial class ActionReportListsView : Control
{
    private static readonly byte[] categories=[1,2,3,4,5,6,0];
    private readonly List<UiSourceTabButton> tabs=[];
    private readonly List<ActionReportList> lists=[];
    private Action<byte>? categoryChanged;
    private Func<bool>? inputAdmitted;
    internal int SelectedIndex {get;private set;}=-1;
    internal byte SelectedCategory=>categories[SelectedIndex];

    internal void Configure(GameResourceRepository repository,UiAssets assets,Shader shader,MapFont font,
        string locale,IReadOnlyDictionary<byte,ActionReportListContent> content,int scrollSpeedSetting,
        Func<int>? currentScrollSpeed,Func<CampaignHistoryRecord,ActionReportDetailRowContent> resolveDetails,
        Action<byte,int,CampaignHistoryRecord?,int> rowEvent,Action<byte> categoryChanged,Func<bool> inputAdmitted,
        byte initialCategory=1)
    {
        ArgumentNullException.ThrowIfNull(inputAdmitted);
        this.categoryChanged=categoryChanged;this.inputAdmitted=inputAdmitted;
        var source=new UiSourceLayout(repository,0xDBB537B1);
        Size=source.Rectangle(0).Size;MouseFilter=MouseFilterEnum.Pass;
        int[] messages=[0x5F7,0x586,0x287,0x2A5,0x3FB,0x835,0x5B5];
        var captions=repository.LoadMessages(locale,MessageKind.Data).Messages.Sections[1];
        for(int i=0;i<categories.Length;i++)
        {
            int ordinal=i;byte category=categories[i];
            if(!content.TryGetValue(category,out var entries))
                throw new ArgumentException("Missing prepared report category "+category,nameof(content));
            var tabBox=source.Rectangle(5+i*2);
            var tab=new UiSourceTabButton{Name="ReportCategory"+category,Position=tabBox.Position,Size=tabBox.Size,
                ActionMode=BaseButton.ActionModeEnum.Release};
            AddChild(tab);tabs.Add(tab);
            uint file=i==0?0x172EDC24u:i==categories.Length-1?0xD45873AAu:0x9AFB27B7u;
            tab.Configure(repository,assets,shader,font,captions.Records[messages[i]].Text
                ??throw new InvalidDataException("Missing report category caption."),file);
            tab.InputAdmission=()=>this.inputAdmitted?.Invoke()==true;
            tab.Pressed+=()=>{if(!tab.Disabled&&tab.SourceInputAdmitted)SelectTab(ordinal);};
            var box=source.Rectangle(category==0?42:25);
            var scroll=source.Rectangle(76);scroll.Position-=box.Position;
            var list=new ActionReportList{Name="ReportList"+category,Position=box.Position,Size=box.Size};
            AddChild(list);lists.Add(list);
            list.Configure(repository,assets,shader,font,locale,category==0,entries,scroll,scrollSpeedSetting,
                currentScrollSpeed,resolveDetails,(index,record,code)=>rowEvent(category,index,record,code));
            list.Hide();
        }
        SetMeta("source_layout",source.Source.Source.ResourceUri);
        int initial=Array.IndexOf(categories,initialCategory);
        if(initial<0)throw new ArgumentOutOfRangeException(nameof(initialCategory));
        SelectTab(initial);
    }

    internal void SelectTab(int index)
    {
        if((uint)index>=(uint)lists.Count)throw new ArgumentOutOfRangeException(nameof(index));
        if(SelectedIndex==index&&lists[index].Visible)return;
        SelectedIndex=index;
        for(int i=0;i<lists.Count;i++){tabs[i].Selected=i==index;lists[i].Visible=i==index;}
        categoryChanged?.Invoke(categories[index]);
    }

    public override void _ExitTree(){categoryChanged=null;inputAdmitted=null;}
}
